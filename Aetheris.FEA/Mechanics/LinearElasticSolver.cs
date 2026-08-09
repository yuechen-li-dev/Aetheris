using System.Diagnostics;
using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Lattice;
using Aetheris.Continuum.Regions.Analytic;
using Aetheris.FEA.Analysis;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.FEA.Mechanics;

public sealed record MechanicsSolveOptions(
    int CutCellQuadraturePerAxis = 4,
    double RelativeResidualTolerance = 1e-9,
    int? MaximumIterations = null,
    Transform3D? DomainTransform = null,
    bool PreserveNominalCellVolumeUnderTransform = false);

public static class LinearElasticSolver
{
    private static readonly (int X, int Y, int Z)[] NodeOffsets =
    [
        (0,0,0),(1,0,0),(1,1,0),(0,1,0),(0,0,1),(1,0,1),(1,1,1),(0,1,1)
    ];
    private static readonly (double Xi, double Eta, double Zeta)[] NaturalSigns =
    [
        (-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)
    ];

    public static LinearElasticAnalysisResult Solve(LinearElasticAnalysisIr analysis, MechanicsSolveOptions? options = null)
    {
        options ??= new MechanicsSolveOptions();
        var diagnostics = AnalysisIrValidator.Validate(analysis).ToList();
        if (diagnostics.Any(item => item.Severity == AnalysisDiagnosticSeverity.Error)) return Failure(analysis, diagnostics);
        var material = analysis.Materials.Single();
        IContinuumRegion region=analysis.Body.ContinuumRegion;
        var sourceBounds=region.Bounds;
        if(options.DomainTransform is { } orientation)region=new TransformedContinuumRegion(region,orientation);
        var counts=(X:analysis.Lattice.CountX,Y:analysis.Lattice.CountY,Z:analysis.Lattice.CountZ);
        if(options.DomainTransform is not null&&options.PreserveNominalCellVolumeUnderTransform)
        {
            var sourceCellVolume=(sourceBounds.Max.X-sourceBounds.Min.X)*(sourceBounds.Max.Y-sourceBounds.Min.Y)*(sourceBounds.Max.Z-sourceBounds.Min.Z)/(counts.X*counts.Y*counts.Z);
            var nominalEdge=double.Cbrt(sourceCellVolume);
            counts=(
                Math.Max(1,(int)Math.Ceiling((region.Bounds.Max.X-region.Bounds.Min.X)/nominalEdge)),
                Math.Max(1,(int)Math.Ceiling((region.Bounds.Max.Y-region.Bounds.Min.Y)/nominalEdge)),
                Math.Max(1,(int)Math.Ceiling((region.Bounds.Max.Z-region.Bounds.Min.Z)/nominalEdge)));
        }
        var lattice=new LatticeSpec(region.Bounds,counts.X,counts.Y,counts.Z);
        var domainStart = Stopwatch.GetTimestamp();
        var grid = ContinuumGridClassifier.Classify(region, lattice, 2);
        var domainTime = Stopwatch.GetElapsedTime(domainStart);

        var quadratureStart = Stopwatch.GetTimestamp();
        var active = new List<(ContinuumCell Cell, MechanicsQuadraturePlan Plan)>();
        foreach (var cell in grid.Cells.Where(item => item.Classification != CellClassification.Outside))
        {
            var plan = CreateQuadrature(region, cell, options.CutCellQuadraturePerAxis);
            if(plan.Points.Count==0&&cell.Classification==CellClassification.Cut)
            {
                for(var order=Math.Max(options.CutCellQuadraturePerAxis+1,8);order<=32&&plan.Points.Count==0;order*=2)plan=CreateQuadrature(region,cell,order);
            }
            if (plan.Points.Count > 0) active.Add((cell, plan));
        }
        var quadratureTime = Stopwatch.GetElapsedTime(quadratureStart);
        if (active.Count == 0)
        {
            diagnostics.Add(new("fea-empty-domain", AnalysisDiagnosticSeverity.Error, "Continuum discretization admitted no occupied mechanics cells.", analysis.Provenance));
            return Failure(analysis, diagnostics);
        }

        var nodeKeys = active.SelectMany(item => CellNodeKeys(item.Cell.Index)).Distinct().OrderBy(item => item.K).ThenBy(item => item.J).ThenBy(item => item.I).ToArray();
        var nodeId = nodeKeys.Select((key, id) => (key, id)).ToDictionary(item => item.key, item => item.id);
        var nodes = nodeKeys.Select((key, id) => new MechanicsNode(id, NodePosition(lattice, key))).ToArray();
        var authorityStart=Stopwatch.GetTimestamp();
        var basisPlan=ImmersedNumericalLowering.PlanBasis(lattice,active,nodeId,nodes,NodeOffsets,NaturalSigns);
        var authorityTime=Stopwatch.GetElapsedTime(authorityStart);
        var rawDofs = checked(nodes.Length * 3);
        var dofs = checked(basisPlan.EffectiveNodeCount * 3);
        var matrix = new SparseSymmetricMatrix(dofs);
        var load = new double[dofs];
        var assemblyStart = Stopwatch.GetTimestamp();
        var constitutive = Constitutive(material.YoungsModulusPascal, material.PoissonRatio);
        foreach (var item in active)
        {
            var ids = CellNodeKeys(item.Cell.Index).Select(key => nodeId[key]).ToArray();
            var local = CellStiffness(item.Cell.Bounds, item.Plan, constitutive);
            AddTransformedMatrix(matrix,ids,local,basisPlan.NodeMappings);
        }
        var assemblyTime = Stopwatch.GetElapsedTime(assemblyStart);
        var volumeMatrix=matrix.Copy();

        var boundaryStart = Stopwatch.GetTimestamp();
        var integratedByLoad = new Dictionary<string, Vector3D>(StringComparer.Ordinal);
        var loadEvidence=new List<BoundaryLoadEvidence>();
        var boundaryPlans=new Dictionary<string,MechanicsBoundaryQuadraturePlan>(StringComparer.Ordinal);
        var semanticFaceTime=TimeSpan.Zero;var boundaryPlanTime=TimeSpan.Zero;
        if(region is not IPlanarBoundaryDomainCapability planar)
        {
            diagnostics.Add(new("fea-selected-region-not-planar",AnalysisDiagnosticSeverity.Error,"The continuum body exposes no exact planar semantic-boundary contract.",analysis.Provenance));
            return Failure(analysis,diagnostics);
        }
        MechanicsBoundaryQuadraturePlan? Plan(SemanticRegionBinding binding)
        {
            var key=binding.Path+"|"+binding.ExactBrepFaceId;if(boundaryPlans.TryGetValue(key,out var cached))return cached;
            var semanticStart=Stopwatch.GetTimestamp();var resolved=planar.TryResolvePlanarBoundary(binding.Path,binding.ExactBrepFaceId,out var domain);semanticFaceTime+=Stopwatch.GetElapsedTime(semanticStart);
            if(!resolved){diagnostics.Add(new("fea-boundary-region-unresolved",AnalysisDiagnosticSeverity.Error,$"Semantic region '{binding.Path}' did not resolve to an exact planar face.",binding.Provenance));return null;}
            if(!domain.TryOrientFromMaterialSide(region,out domain)){diagnostics.Add(new("fea-material-side-ambiguity",AnalysisDiagnosticSeverity.Error,$"Semantic region '{binding.Path}' did not provide an unambiguous CIR material side.",binding.Provenance));return null;}
            try{var planStart=Stopwatch.GetTimestamp();var created=MechanicsBoundaryQuadrature.Create(domain,binding,active.Select(item=>item.Cell).ToArray());boundaryPlanTime+=Stopwatch.GetElapsedTime(planStart);boundaryPlans[key]=created;return created;}
            catch(MechanicsBoundaryLoweringException exception){diagnostics.Add(new(exception.Code,AnalysisDiagnosticSeverity.Error,exception.Message,binding.Provenance));return null;}
        }
        var loadAssemblyStart=Stopwatch.GetTimestamp();
        foreach (var boundaryLoad in analysis.Loads)
        {
            var plan=Plan(boundaryLoad.Region);if(plan is null)continue;
            var contributions = IntegrateLoad(boundaryLoad,plan,nodeId,nodes,options.DomainTransform);
            foreach (var contribution in contributions.Nodal)
            {
                foreach(var mapped in basisPlan.NodeMappings[contribution.Key])
                {
                    load[(3*mapped.EffectiveNode)+0]+=mapped.Weight*contribution.Value.X;
                    load[(3*mapped.EffectiveNode)+1]+=mapped.Weight*contribution.Value.Y;
                    load[(3*mapped.EffectiveNode)+2]+=mapped.Weight*contribution.Value.Z;
                }
            }
            integratedByLoad[boundaryLoad.Id] = contributions.Resultant;
            loadEvidence.Add(contributions.Evidence);
            if (contributions.Area <= 0)
                diagnostics.Add(new("fea-empty-region-selection", AnalysisDiagnosticSeverity.Error, $"Region '{boundaryLoad.Region.Path}' selected no exact Continuum boundary fragments.", boundaryLoad.Provenance));
            else if(double.Abs(plan.IntegratedArea-plan.ExactSelectedArea)>double.Max(1,plan.ExactSelectedArea)*1e-9)
                diagnostics.Add(new("fea-boundary-area-mismatch",AnalysisDiagnosticSeverity.Error,$"Owned fragments integrate {plan.IntegratedArea:R} m^2 but exact face area is {plan.ExactSelectedArea:R} m^2.",boundaryLoad.Provenance));
            if(contributions.Evidence.ResultantResidual>double.Max(1,contributions.Evidence.ExpectedResultant.Length)*1e-9)
                diagnostics.Add(new("fea-load-resultant-mismatch",AnalysisDiagnosticSeverity.Error,$"Integrated load resultant residual is {contributions.Evidence.ResultantResidual:R} N.",boundaryLoad.Provenance));
            if(contributions.Evidence.MomentResidual>double.Max(1,contributions.Evidence.ExpectedMoment.Length)*1e-9)
                diagnostics.Add(new("fea-load-moment-mismatch",AnalysisDiagnosticSeverity.Error,$"Integrated load moment residual is {contributions.Evidence.MomentResidual:R} N m.",boundaryLoad.Provenance));
        }
        var loadAssemblyTime=Stopwatch.GetElapsedTime(loadAssemblyStart);
        var constraintStart=Stopwatch.GetTimestamp();
        var constraintPlans=analysis.Constraints.ToDictionary(item=>item.Id,item=>Plan(item.Region),StringComparer.Ordinal);
        var boundaryChoices=SelectBoundaryEnforcement(analysis,constraintPlans,nodeId,nodes,lattice,basisPlan,active);
        var prescribed = ResolveConstraints(analysis,constraintPlans,nodeId,nodes,basisPlan,boundaryChoices,diagnostics,out var constrainedNodes);
        foreach(var constraint in analysis.Constraints)
        {
            var choice=boundaryChoices[constraint.Id];var plan=constraintPlans[constraint.Id];
            if(choice.Kind==BoundaryEnforcementKind.SymmetricNitsche&&plan is not null)
                AddSymmetricNitsche(matrix,load,constraint,plan,nodeId,active,basisPlan.NodeMappings,constitutive,material.YoungsModulusPascal,choice.PenaltyScale);
        }
        var constraintTime=Stopwatch.GetElapsedTime(constraintStart);
        if (prescribed.Count == 0&&boundaryChoices.Values.All(choice=>choice.Kind!=BoundaryEnforcementKind.SymmetricNitsche))
            diagnostics.Add(new("fea-rigid-body-mode", AnalysisDiagnosticSeverity.Error, "Constraints selected no admitted lattice DOFs.", analysis.Provenance));
        var boundaryTime = Stopwatch.GetElapsedTime(boundaryStart);
        if (diagnostics.Any(item => item.Severity == AnalysisDiagnosticSeverity.Error)) return Failure(analysis, diagnostics);

        var rawMatrix = matrix.Copy();
        var rawLoad = (double[])load.Clone();
        matrix.ApplyDirichlet(prescribed, load);
        bool? spd=dofs<=600?matrix.IsPositiveDefiniteByDenseCholesky():null;
        var (solution, convergence) = PreconditionedConjugateGradient.Solve(matrix, load, options.RelativeResidualTolerance, options.MaximumIterations);
        if (!convergence.Converged)
        {
            diagnostics.Add(new("fea-solver-non-convergence", AnalysisDiagnosticSeverity.Error, $"PCG did not converge after {convergence.Iterations} iterations; residual {convergence.FinalResidual:R}.", analysis.Provenance));
            return Failure(analysis, diagnostics, convergence);
        }

        var recoveryStart = Stopwatch.GetTimestamp();
        var expandedSolution=ExpandSolution(solution,nodes.Length,basisPlan.NodeMappings);
        var displacements = nodes.Select(node => new NodalDisplacement(node.Id, node.Position, new Vector3D(expandedSolution[3*node.Id], expandedSolution[(3*node.Id)+1], expandedSolution[(3*node.Id)+2]))).ToArray();
        var fields = RecoverFields(active, nodeId, expandedSolution, constitutive);
        var stressProbes=CreateValidationStressProbes(analysis.Body.ContinuumRegion,options.DomainTransform,active,nodeId,expandedSolution,constitutive,loadEvidence);
        var algebraicEnergy=.5*Dot(solution,volumeMatrix.Multiply(solution));var integratedEnergy=IntegratedStrainEnergy(active,nodeId,expandedSolution,constitutive);var energyResidual=double.Abs(algebraicEnergy-integratedEnergy);
        var energy=new StrainEnergyConsistency(algebraicEnergy,integratedEnergy,energyResidual,integratedEnergy==0?0:energyResidual/double.Abs(integratedEnergy));
        var residual = rawMatrix.Multiply(solution);
        for (var i = 0; i < residual.Length; i++) residual[i] -= rawLoad[i];
        var reactions = new List<ReactionResult>();
        var boundaryEvidence=new List<BoundaryEnforcementEvidence>();
        foreach (var constraint in analysis.Constraints)
        {
            var choice=boundaryChoices[constraint.Id];var plan=constraintPlans[constraint.Id];
            var vector=choice.Kind==BoundaryEnforcementKind.SymmetricNitsche&&plan is not null
                ? IntegrateBoundaryReaction(plan,nodeId,active,expandedSolution,constitutive,constraint.ValueMeters,material.YoungsModulusPascal,choice.PenaltyScale)
                : StrongReaction(residual,constrainedNodes.GetValueOrDefault(constraint.Id,new HashSet<int>()),basisPlan.NodeMappings);
            reactions.Add(new(constraint.Id, vector));
            var error=plan is null?(0d,0d):BoundaryViolation(plan,nodeId,expandedSolution,constraint.ValueMeters);
            boundaryEvidence.Add(new(constraint.Id,constraint.Region.Path,plan?.ExactBrepFaceId,choice.Kind,choice.Utility,choice.MaximumNormalizedOffset,choice.PenaltyScale,choice.SelectedNodes,choice.Rejections,error.Item1,error.Item2,vector,choice.CandidateUtilities));
        }
        var applied = integratedByLoad.Values.Aggregate(Vector3D.Zero, (sum, value) => sum + value);
        var reaction = reactions.Aggregate(Vector3D.Zero, (sum, value) => sum + value.ForceNewton);
        var equilibrium = new EquilibriumResult(applied, reaction, applied + reaction);
        var recoveryTime = Stopwatch.GetElapsedTime(recoveryStart);
        var fractions = active.Select(item => item.Plan.IntegratedVolume / CellVolume(item.Cell.Bounds)).Order().ToArray();
        var tiny = new TinyCellDiagnostics(fractions[0], fractions.Count(x => x < .01), fractions.Count(x => x < .05), fractions.Count(x => x < .10), fractions);
        var declared = loadEvidence.Sum(item=>item.ExpectedResultant.Length);
        var actual = integratedByLoad.Values.Sum(item => item.Length);
        var conditioning=rawMatrix.ConditioningProxy();
        var system = new SparseSystemMetrics(dofs, rawMatrix.Nonzeros, rawMatrix.MaximumAsymmetry(), rawMatrix.IsFinite(), true, actual, declared == 0 ? 0 : double.Abs(actual - declared),grid.CutCellCount,spd,rawDofs,prescribed.Count,basisPlan.Extensions.Count*3,conditioning.MinimumDiagonal,conditioning.MaximumDiagonal,conditioning.DiagonalRatio,conditioning.MinimumRowNorm,conditioning.MaximumRowNorm,conditioning.RowNormRatio);
        var sparseBytes = (long)rawMatrix.Nonzeros * (sizeof(double) + sizeof(int)) + (long)(dofs + 1) * sizeof(int);
        var resultBytes = (long)displacements.Length * (sizeof(int) + 6*sizeof(double)) + (long)fields.Count * 16*sizeof(double);
        var lowering=new NumericalLoweringEvidence("m5c-v1-frozen","Compiler metadata governing independent numerical carriers; never a physical stiffness multiplier.",basisPlan.Supports,basisPlan.Treatments,boundaryEvidence,basisPlan.JudgmentCalls,boundaryChoices.Count,basisPlan.FixedThresholdCount,ImmersedNumericalLowering.Hash(basisPlan,boundaryEvidence),authorityTime,TimeSpan.Zero,constraintTime);
        return new(analysis.Id, true, convergence, displacements, fields, reactions, equilibrium, system, tiny,
            new(domainTime,quadratureTime,assemblyTime,boundaryTime,convergence.Runtime,recoveryTime,sparseBytes,resultBytes,semanticFaceTime,boundaryPlanTime,loadAssemblyTime,constraintTime),diagnostics,loadEvidence,lowering,energy,stressProbes);
    }

    public static MechanicsQuadraturePlan CreateQuadrature(IContinuumRegion region, ContinuumCell cell, int cutSamplesPerAxis)
    {
        var volume = CellVolume(cell.Bounds);
        if (cell.Classification == CellClassification.Inside)
        {
            var g = 1 / double.Sqrt(3);
            var points = new List<MechanicsQuadraturePoint>(8);
            foreach (var z in new[] {-g,g}) foreach (var y in new[] {-g,g}) foreach (var x in new[] {-g,g})
                points.Add(new(ToPhysical(cell.Bounds, x,y,z), volume/8, x,y,z));
            return new("Q1-2x2x2-Gauss-full-cell-cached-pattern", points, volume, true);
        }
        if (cutSamplesPerAxis < 2) throw new ArgumentOutOfRangeException(nameof(cutSamplesPerAxis));
        var result = new List<MechanicsQuadraturePoint>();
        var n = cutSamplesPerAxis;
        var weight = volume/(n*n*n);
        for (var k=0;k<n;k++) for(var j=0;j<n;j++) for(var i=0;i<n;i++)
        {
            var xi=-1+(2d*(i+.5)/n); var eta=-1+(2d*(j+.5)/n); var zeta=-1+(2d*(k+.5)/n);
            var point=ToPhysical(cell.Bounds,xi,eta,zeta);
            if(region.Classify(point)!=ContinuumPointClassification.Outside) result.Add(new(point,weight,xi,eta,zeta));
        }
        return new($"Q1-occupied-subcell-midpoint-{n}x{n}x{n}",result,result.Count*weight,false);
    }

    private static void AddTransformedMatrix(SparseSymmetricMatrix matrix,IReadOnlyList<int> rawNodeIds,double[,] local,IReadOnlyDictionary<int,IReadOnlyList<(int EffectiveNode,double Weight)>> mappings)
    {
        for(var a=0;a<24;a++)foreach(var ma in mappings[rawNodeIds[a/3]])
        for(var b=0;b<24;b++)foreach(var mb in mappings[rawNodeIds[b/3]])
            matrix.Add(3*ma.EffectiveNode+a%3,3*mb.EffectiveNode+b%3,ma.Weight*local[a,b]*mb.Weight);
    }

    private static void AddTransformedLoad(double[] load,IReadOnlyList<int> rawNodeIds,IReadOnlyList<double> local,IReadOnlyDictionary<int,IReadOnlyList<(int EffectiveNode,double Weight)>> mappings)
    {for(var a=0;a<24;a++)foreach(var mapped in mappings[rawNodeIds[a/3]])load[3*mapped.EffectiveNode+a%3]+=mapped.Weight*local[a];}

    private static double[] ExpandSolution(IReadOnlyList<double> effective,int rawNodes,IReadOnlyDictionary<int,IReadOnlyList<(int EffectiveNode,double Weight)>> mappings)
    {
        var raw=new double[rawNodes*3];
        for(var node=0;node<rawNodes;node++)foreach(var mapped in mappings[node])for(var component=0;component<3;component++)raw[3*node+component]+=mapped.Weight*effective[3*mapped.EffectiveNode+component];
        return raw;
    }

    private static (double X,double Y,double Z) Natural(BoundingBox3D bounds,Point3D point)=>(2*(point.X-bounds.Min.X)/(bounds.Max.X-bounds.Min.X)-1,2*(point.Y-bounds.Min.Y)/(bounds.Max.Y-bounds.Min.Y)-1,2*(point.Z-bounds.Min.Z)/(bounds.Max.Z-bounds.Min.Z)-1);
    private static double Component(Vector3D vector,int component)=>component switch{0=>vector.X,1=>vector.Y,_=>vector.Z};
    private static double Dot(IReadOnlyList<double> left,IReadOnlyList<double> right){var value=0d;for(var i=0;i<left.Count;i++)value+=left[i]*right[i];return value;}

    private static double IntegratedStrainEnergy(IReadOnlyList<(ContinuumCell Cell,MechanicsQuadraturePlan Plan)> active,IReadOnlyDictionary<(int I,int J,int K),int> nodeId,IReadOnlyList<double> solution,double[,] d)
    {
        var total=0d;
        foreach(var item in active)
        {
            var ids=CellNodeKeys(item.Cell.Index).Select(key=>nodeId[key]).ToArray();
            foreach(var point in item.Plan.Points)
            {
                var b=BMatrix(item.Cell.Bounds,point.Xi,point.Eta,point.Zeta);var strain=new double[6];for(var i=0;i<6;i++)for(var j=0;j<24;j++)strain[i]+=b[i,j]*solution[3*ids[j/3]+j%3];var stress=new double[6];for(var i=0;i<6;i++)for(var j=0;j<6;j++)stress[i]+=d[i,j]*strain[j];
                total+=.5*strain.Select((value,index)=>value*stress[index]).Sum()*point.Weight;
            }
        }
        return total;
    }

    private static IReadOnlyList<ExactStressProbe> CreateValidationStressProbes(IContinuumRegion source,Transform3D? transform,IReadOnlyList<(ContinuumCell Cell,MechanicsQuadraturePlan Plan)> active,IReadOnlyDictionary<(int I,int J,int K),int> nodeId,IReadOnlyList<double> solution,double[,] d,IReadOnlyList<BoundaryLoadEvidence> loads)
    {
        if(source is not BlockWithCylindricalHoleRegion hole||loads.Count==0)return [];
        var map=transform??Transform3D.Identity;var center=hole.HoleCenter;var nominal=loads.Sum(load=>load.ExpectedResultant.Length)/(hole.Height*(hole.Bounds.Max.Y-hole.Bounds.Min.Y));
        var definitions=new[]{("hole-top",new Point3D(center.X,center.Y+hole.HoleRadius,center.Z),new Vector3D(1,0,0),3*nominal),("hole-right",new Point3D(center.X+hole.HoleRadius,center.Y,center.Z),new Vector3D(0,1,0),-nominal)};
        var probes=new List<ExactStressProbe>();
        foreach(var definition in definitions)
        {
            var position=map.Apply(definition.Item2);var tangent=map.Apply(definition.Item3);tangent.TryNormalize(out tangent);var stress=StressAt(position,active,nodeId,solution,d);if(stress is null)continue;var s=stress.Value;
            var hoop=s.XX*tangent.X*tangent.X+s.YY*tangent.Y*tangent.Y+s.ZZ*tangent.Z*tangent.Z+2*s.XY*tangent.X*tangent.Y+2*s.YZ*tangent.Y*tangent.Z+2*s.XZ*tangent.X*tangent.Z;
            probes.Add(new(definition.Item1,position,s,hoop,definition.Item4,double.Abs(hoop-definition.Item4),"Infinite plate with a circular traction-free hole under remote uniaxial stress; finite-width comparison is validation-only."));
        }
        return probes;
    }

    private static SymmetricTensor? StressAt(Point3D point,IReadOnlyList<(ContinuumCell Cell,MechanicsQuadraturePlan Plan)> active,IReadOnlyDictionary<(int I,int J,int K),int> nodeId,IReadOnlyList<double> solution,double[,] d)
    {
        const double tolerance=1e-12;var item=active.Where(x=>point.X>=x.Cell.Bounds.Min.X-tolerance&&point.X<=x.Cell.Bounds.Max.X+tolerance&&point.Y>=x.Cell.Bounds.Min.Y-tolerance&&point.Y<=x.Cell.Bounds.Max.Y+tolerance&&point.Z>=x.Cell.Bounds.Min.Z-tolerance&&point.Z<=x.Cell.Bounds.Max.Z+tolerance).OrderBy(x=>x.Cell.Index.K).ThenBy(x=>x.Cell.Index.J).ThenBy(x=>x.Cell.Index.I).Cast<(ContinuumCell Cell,MechanicsQuadraturePlan Plan)?>().FirstOrDefault();
        if(item is null)return null;var cell=item.Value.Cell;var natural=Natural(cell.Bounds,point);var b=BMatrix(cell.Bounds,natural.X,natural.Y,natural.Z);var ids=CellNodeKeys(cell.Index).Select(key=>nodeId[key]).ToArray();var strain=new double[6];for(var i=0;i<6;i++)for(var j=0;j<24;j++)strain[i]+=b[i,j]*solution[3*ids[j/3]+j%3];var stress=new double[6];for(var i=0;i<6;i++)for(var j=0;j<6;j++)stress[i]+=d[i,j]*strain[j];return new(stress[0],stress[1],stress[2],stress[3],stress[4],stress[5]);
    }

    private static double[,] CellStiffness(BoundingBox3D bounds, MechanicsQuadraturePlan plan, double[,] d)
    {
        var k = new double[24,24];
        foreach (var point in plan.Points)
        {
            var b = BMatrix(bounds, point.Xi, point.Eta, point.Zeta);
            for(var i=0;i<24;i++) for(var j=0;j<24;j++)
            {
                var sum=0d; for(var a=0;a<6;a++) for(var c=0;c<6;c++) sum += b[a,i]*d[a,c]*b[c,j];
                k[i,j]+=sum*point.Weight;
            }
        }
        return k;
    }

    private static double[,] BMatrix(BoundingBox3D bounds,double xi,double eta,double zeta)
    {
        var b=new double[6,24];
        var sx=2/(bounds.Max.X-bounds.Min.X); var sy=2/(bounds.Max.Y-bounds.Min.Y); var sz=2/(bounds.Max.Z-bounds.Min.Z);
        for(var n=0;n<8;n++)
        {
            var s=NaturalSigns[n];
            var dx=.125*s.Xi*(1+s.Eta*eta)*(1+s.Zeta*zeta)*sx;
            var dy=.125*s.Eta*(1+s.Xi*xi)*(1+s.Zeta*zeta)*sy;
            var dz=.125*s.Zeta*(1+s.Xi*xi)*(1+s.Eta*eta)*sz;
            var c=3*n;
            b[0,c]=dx; b[1,c+1]=dy; b[2,c+2]=dz;
            b[3,c]=dy; b[3,c+1]=dx;
            b[4,c+1]=dz; b[4,c+2]=dy;
            b[5,c]=dz; b[5,c+2]=dx;
        }
        return b;
    }

    private static double[,] Constitutive(double e,double nu)
    {
        var d=new double[6,6]; var factor=e/((1+nu)*(1-2*nu));
        var a=(1-nu)*factor; var b=nu*factor; var g=e/(2*(1+nu));
        d[0,0]=d[1,1]=d[2,2]=a; d[0,1]=d[0,2]=d[1,0]=d[1,2]=d[2,0]=d[2,1]=b; d[3,3]=d[4,4]=d[5,5]=g;
        return d;
    }

    private sealed record LoadIntegration(Dictionary<int,Vector3D> Nodal,Vector3D Resultant,double Area,BoundaryLoadEvidence Evidence);
    private static LoadIntegration IntegrateLoad(BoundaryLoadIr load,MechanicsBoundaryQuadraturePlan plan,IReadOnlyDictionary<(int I,int J,int K),int> nodeId,IReadOnlyList<MechanicsNode> nodes,Transform3D? orientation)
    {
        var declared=orientation is { } transform?transform.Apply(load.VectorSi):load.VectorSi;
        var nodal=new Dictionary<int,Vector3D>();
        foreach(var fragment in plan.Fragments)
        {
            var ids=CellNodeKeys(fragment.Cell).Select(key=>nodeId[key]).ToArray();
            foreach(var point in fragment.Points)
            {
                var traction=load.Kind switch {BoundaryLoadKind.ResultantForce=>declared/plan.ExactSelectedArea,BoundaryLoadKind.Pressure=>point.OutwardNormal*(-load.PressurePascal),_=>declared};
                var force=traction*point.AreaWeight;
                for(var n=0;n<8;n++)if(double.Abs(point.ShapeFunctions[n])>1e-15)nodal[ids[n]]=nodal.GetValueOrDefault(ids[n])+force*point.ShapeFunctions[n];
            }
        }
        var resultant=nodal.Values.Aggregate(Vector3D.Zero,(sum,value)=>sum+value);var moment=Vector3D.Zero;
        foreach(var pair in nodal)moment+=new Vector3D(nodes[pair.Key].Position.X,nodes[pair.Key].Position.Y,nodes[pair.Key].Position.Z).Cross(pair.Value);
        var expected=load.Kind switch{BoundaryLoadKind.ResultantForce=>declared,BoundaryLoadKind.Pressure=>plan.Fragments.SelectMany(f=>f.Points).Aggregate(Vector3D.Zero,(sum,p)=>sum+p.OutwardNormal*(-load.PressurePascal*p.AreaWeight)),_=>declared*plan.ExactSelectedArea};
        var centroid=new Vector3D(plan.ExactCentroid.X,plan.ExactCentroid.Y,plan.ExactCentroid.Z);var expectedMoment=centroid.Cross(expected);
        var evidence=new BoundaryLoadEvidence(load.Id,load.Region.Path,plan.BoundaryId,plan.ExactBrepFaceId,plan.ExactSelectedArea,plan.IntegratedArea,expected,resultant,expectedMoment,moment,(expected-resultant).Length,(expectedMoment-moment).Length,plan.Fragments.Count,plan.QuadraturePointCount,plan.MaterialSideEvidence);
        return new(nodal,resultant,plan.IntegratedArea,evidence);
    }

    private sealed record BoundaryChoice(BoundaryEnforcementKind Kind,double Utility,double MaximumNormalizedOffset,double PenaltyScale,int SelectedNodes,IReadOnlyList<string> Rejections,IReadOnlyDictionary<string,double> CandidateUtilities);
    private sealed record BoundaryContext(bool CompleteVectorConstraint,double MaximumNormalizedOffset,bool HasExactQuadrature,double MinimumTraceCellFraction);

    private static Dictionary<string,BoundaryChoice> SelectBoundaryEnforcement(LinearElasticAnalysisIr analysis,IReadOnlyDictionary<string,MechanicsBoundaryQuadraturePlan?> plans,IReadOnlyDictionary<(int I,int J,int K),int> nodeId,IReadOnlyList<MechanicsNode> nodes,LatticeSpec lattice,BasisLoweringPlan basis,IReadOnlyList<(ContinuumCell Cell,MechanicsQuadraturePlan Plan)> active)
    {
        var result=new Dictionary<string,BoundaryChoice>(StringComparer.Ordinal);var cellSize=lattice.CellSize;var h=double.Min(cellSize.X,double.Min(cellSize.Y,cellSize.Z));
        var fractions=active.ToDictionary(item=>item.Cell.Index,item=>item.Plan.IntegratedVolume/CellVolume(item.Cell.Bounds));var engine=new JudgmentEngine<BoundaryContext>();
        foreach(var constraint in analysis.Constraints)
        {
            var plan=plans.GetValueOrDefault(constraint.Id);var selected=plan is null?new HashSet<int>():NearestBoundaryNodes(plan,nodeId,nodes);
            var offset=plan is null||selected.Count==0?1:selected.Max(id=>double.Abs((nodes[id].Position-plan.Frame.Origin).Dot(plan.Frame.Normal)))/h;
            var traceFraction=plan is null?0:double.Min(plan.Fragments.Min(fragment=>fractions[fragment.Cell]),fractions.Values.Min());var context=new BoundaryContext(constraint.Components.Count==3,offset,plan is not null&&plan.QuadraturePointCount>0,traceFraction);
            var candidates=new[]
            {
                new JudgmentCandidate<BoundaryContext>("StrongNearestNode",c=>selected.Count>0,c=>.55-c.MaximumNormalizedOffset,_=>"No nearest active basis support was selected.",0),
                new JudgmentCandidate<BoundaryContext>("SymmetricNitsche",c=>c.CompleteVectorConstraint&&c.HasExactQuadrature&&c.MinimumTraceCellFraction>=.0001,c=>.64+c.MaximumNormalizedOffset-.10,c=>!c.CompleteVectorConstraint?"The bounded M5C Nitsche path requires all displacement components.":!c.HasExactQuadrature?"Exact boundary quadrature is unavailable.":"Trace inverse bound exceeds the validated penalty tiers for a sub-0.01% cell.",1)
            };
            var judgment=engine.Evaluate(context,candidates);var chosen=judgment.Selection!.Value;var kind=chosen.Candidate.Name=="SymmetricNitsche"?BoundaryEnforcementKind.SymmetricNitsche:BoundaryEnforcementKind.StrongNearestNode;
            // gamma=100 is the nominal prevalidated tier; dimensional scale is gamma E/h.
            var utilities=new SortedDictionary<string,double>();var rejections=new List<string>();if(selected.Count>0)utilities["StrongNearestNode"]=.55-offset;else rejections.Add("StrongNearestNode: No nearest active basis support was selected.");
            if(context.CompleteVectorConstraint&&context.HasExactQuadrature&&context.MinimumTraceCellFraction>=.0001)utilities["SymmetricNitsche"]=.54+offset;else rejections.Add("SymmetricNitsche: "+(!context.CompleteVectorConstraint?"The bounded path requires all displacement components.":!context.HasExactQuadrature?"Exact boundary quadrature is unavailable.":"Trace inverse bound exceeds the validated penalty tiers for a sub-0.01% cell."));
            result[constraint.Id]=new(kind,chosen.Score,offset,kind==BoundaryEnforcementKind.SymmetricNitsche?100:0,selected.Count,rejections,utilities);
        }
        return result;
    }

    private static HashSet<int> NearestBoundaryNodes(MechanicsBoundaryQuadraturePlan plan,IReadOnlyDictionary<(int I,int J,int K),int> nodeId,IReadOnlyList<MechanicsNode> nodes)
    {
        var selected=new HashSet<int>();
        foreach(var fragment in plan.Fragments)
        {
            var ids=CellNodeKeys(fragment.Cell).Where(nodeId.ContainsKey).Select(key=>nodeId[key]).ToArray();
            var distances=ids.Select(id=>double.Abs((nodes[id].Position-plan.Frame.Origin).Dot(plan.Frame.Normal))).ToArray();var minimum=distances.Min();
            for(var i=0;i<ids.Length;i++)if(distances[i]<=minimum+1e-12)selected.Add(ids[i]);
        }
        return selected;
    }

    private static void AddSymmetricNitsche(SparseSymmetricMatrix matrix,double[] load,DisplacementConstraintIr constraint,MechanicsBoundaryQuadraturePlan plan,IReadOnlyDictionary<(int I,int J,int K),int> nodeId,IReadOnlyList<(ContinuumCell Cell,MechanicsQuadraturePlan Plan)> active,IReadOnlyDictionary<int,IReadOnlyList<(int EffectiveNode,double Weight)>> mappings,double[,] d,double young,double gamma)
    {
        var cells=active.ToDictionary(item=>item.Cell.Index,item=>item.Cell.Bounds);var h=cells.Values.Min(b=>double.Min(b.Max.X-b.Min.X,double.Min(b.Max.Y-b.Min.Y,b.Max.Z-b.Min.Z)));
        var penalty=gamma*young/h;
        foreach(var fragment in plan.Fragments)
        {
            var bounds=cells[fragment.Cell];var ids=CellNodeKeys(fragment.Cell).Select(key=>nodeId[key]).ToArray();var local=new double[24,24];var localLoad=new double[24];
            foreach(var point in fragment.Points)
            {
                var natural=Natural(bounds,point.Position);var b=BMatrix(bounds,natural.X,natural.Y,natural.Z);var tractions=TractionColumns(b,d,point.OutwardNormal);
                for(var i=0;i<24;i++)
                {
                    var ni=point.ShapeFunctions[i/3];var ci=i%3;var consistencyI=tractions[i].Dot(constraint.ValueMeters);
                    localLoad[i]+=(-consistencyI+penalty*ni*Component(constraint.ValueMeters,ci))*point.AreaWeight;
                    for(var j=0;j<24;j++)
                    {
                        var nj=point.ShapeFunctions[j/3];var cj=j%3;
                        local[i,j]+=(-ni*Component(tractions[j],ci)-nj*Component(tractions[i],cj)+(ci==cj?penalty*ni*nj:0))*point.AreaWeight;
                    }
                }
            }
            AddTransformedMatrix(matrix,ids,local,mappings);AddTransformedLoad(load,ids,localLoad,mappings);
        }
    }

    private static Vector3D[] TractionColumns(double[,] b,double[,] d,Vector3D normal)
    {
        var result=new Vector3D[24];
        for(var column=0;column<24;column++)
        {
            var s=new double[6];for(var i=0;i<6;i++)for(var j=0;j<6;j++)s[i]+=d[i,j]*b[j,column];
            result[column]=new(s[0]*normal.X+s[3]*normal.Y+s[5]*normal.Z,s[3]*normal.X+s[1]*normal.Y+s[4]*normal.Z,s[5]*normal.X+s[4]*normal.Y+s[2]*normal.Z);
        }
        return result;
    }

    private static Vector3D IntegrateBoundaryReaction(MechanicsBoundaryQuadraturePlan plan,IReadOnlyDictionary<(int I,int J,int K),int> nodeId,IReadOnlyList<(ContinuumCell Cell,MechanicsQuadraturePlan Plan)> active,IReadOnlyList<double> solution,double[,] d,Vector3D prescribed,double young,double gamma)
    {
        var cells=active.ToDictionary(item=>item.Cell.Index,item=>item.Cell.Bounds);var h=cells.Values.Min(b=>double.Min(b.Max.X-b.Min.X,double.Min(b.Max.Y-b.Min.Y,b.Max.Z-b.Min.Z)));var penalty=gamma*young/h;var total=Vector3D.Zero;
        foreach(var fragment in plan.Fragments)
        {
            var bounds=cells[fragment.Cell];var ids=CellNodeKeys(fragment.Cell).Select(key=>nodeId[key]).ToArray();
            foreach(var point in fragment.Points)
            {
                var natural=Natural(bounds,point.Position);var b=BMatrix(bounds,natural.X,natural.Y,natural.Z);var strain=new double[6];
                for(var i=0;i<6;i++)for(var j=0;j<24;j++)strain[i]+=b[i,j]*solution[3*ids[j/3]+j%3];
                var stress=new double[6];for(var i=0;i<6;i++)for(var j=0;j<6;j++)stress[i]+=d[i,j]*strain[j];var n=point.OutwardNormal;var value=Vector3D.Zero;
                for(var node=0;node<8;node++)value+=new Vector3D(solution[3*ids[node]],solution[3*ids[node]+1],solution[3*ids[node]+2])*point.ShapeFunctions[node];
                var physical=new Vector3D(stress[0]*n.X+stress[3]*n.Y+stress[5]*n.Z,stress[3]*n.X+stress[1]*n.Y+stress[4]*n.Z,stress[5]*n.X+stress[4]*n.Y+stress[2]*n.Z);
                total+=(physical-(value-prescribed)*penalty)*point.AreaWeight;
            }
        }
        return total;
    }

    private static (double,double) BoundaryViolation(MechanicsBoundaryQuadraturePlan plan,IReadOnlyDictionary<(int I,int J,int K),int> nodeId,IReadOnlyList<double> solution,Vector3D prescribed)
    {
        var maximum=0d;var weighted=0d;var area=0d;
        foreach(var fragment in plan.Fragments)
        {
            var ids=CellNodeKeys(fragment.Cell).Select(key=>nodeId[key]).ToArray();
            foreach(var point in fragment.Points)
            {
                var value=Vector3D.Zero;for(var n=0;n<8;n++)value+=new Vector3D(solution[3*ids[n]],solution[3*ids[n]+1],solution[3*ids[n]+2])*point.ShapeFunctions[n];var error=(value-prescribed).Length;
                maximum=double.Max(maximum,error);weighted+=error*error*point.AreaWeight;area+=point.AreaWeight;
            }
        }
        return (maximum,area==0?0:double.Sqrt(weighted/area));
    }

    private static Vector3D StrongReaction(IReadOnlyList<double> residual,IReadOnlySet<int> effectiveNodes,IReadOnlyDictionary<int,IReadOnlyList<(int EffectiveNode,double Weight)>> mappings)
    {var total=Vector3D.Zero;foreach(var id in effectiveNodes)total+=new Vector3D(residual[3*id],residual[3*id+1],residual[3*id+2]);return total;}

    private static Dictionary<int,double> ResolveConstraints(LinearElasticAnalysisIr analysis,IReadOnlyDictionary<string,MechanicsBoundaryQuadraturePlan?> plans,IReadOnlyDictionary<(int I,int J,int K),int> nodeId,IReadOnlyList<MechanicsNode> nodes,BasisLoweringPlan basis,IReadOnlyDictionary<string,BoundaryChoice> choices,List<AnalysisDiagnostic> diagnostics,out Dictionary<string,IReadOnlySet<int>> constrainedNodes)
    {
        var result=new Dictionary<int,double>();constrainedNodes=new(StringComparer.Ordinal);
        foreach(var constraint in analysis.Constraints)
        {
            if(choices[constraint.Id].Kind==BoundaryEnforcementKind.SymmetricNitsche){constrainedNodes[constraint.Id]=new HashSet<int>();continue;}
            var plan=plans.GetValueOrDefault(constraint.Id);var rawSelected=plan is null?new HashSet<int>():NearestBoundaryNodes(plan,nodeId,nodes);var selected=rawSelected.SelectMany(id=>basis.NodeMappings[id].Select(m=>m.EffectiveNode)).ToHashSet();constrainedNodes[constraint.Id]=selected;
            if(selected.Count==0){diagnostics.Add(new("fea-empty-region-selection",AnalysisDiagnosticSeverity.Error,$"Constraint region '{constraint.Region.Path}' selected no basis supports.",constraint.Provenance));continue;}
            foreach(var id in selected) foreach(var component in constraint.Components)
            {
                var index=(3*id)+(int)component; var value=component switch{DisplacementComponent.X=>constraint.ValueMeters.X,DisplacementComponent.Y=>constraint.ValueMeters.Y,_=>constraint.ValueMeters.Z};
                if(result.TryGetValue(index,out var prior)&&double.Abs(prior-value)>1e-15) diagnostics.Add(new("fea-conflicting-constraints",AnalysisDiagnosticSeverity.Error,$"Conflicting values target DOF {index}.",constraint.Provenance)); else result[index]=value;
            }
        }
        return result;
    }

    private static IReadOnlyList<CellFieldResult> RecoverFields(IReadOnlyList<(ContinuumCell Cell,MechanicsQuadraturePlan Plan)> active,IReadOnlyDictionary<(int I,int J,int K),int> nodeId,double[] solution,double[,] d)
    {
        var result=new List<CellFieldResult>(active.Count);
        foreach(var item in active)
        {
            var ids=CellNodeKeys(item.Cell.Index).Select(key=>nodeId[key]).ToArray(); var local=new double[24]; for(var i=0;i<24;i++)local[i]=solution[(3*ids[i/3])+(i%3)];
            var b=BMatrix(item.Cell.Bounds,0,0,0); var strain=new double[6]; var stress=new double[6];
            for(var i=0;i<6;i++)for(var j=0;j<24;j++)strain[i]+=b[i,j]*local[j];
            for(var i=0;i<6;i++)for(var j=0;j<6;j++)stress[i]+=d[i,j]*strain[j];
            var vm=double.Sqrt(.5*((stress[0]-stress[1])*(stress[0]-stress[1])+(stress[1]-stress[2])*(stress[1]-stress[2])+(stress[2]-stress[0])*(stress[2]-stress[0]))+3*(stress[3]*stress[3]+stress[4]*stress[4]+stress[5]*stress[5]));
            result.Add(new(item.Cell.Index.I,item.Cell.Index.J,item.Cell.Index.K,Center(item.Cell.Bounds),new(strain[0],strain[1],strain[2],strain[3]/2,strain[4]/2,strain[5]/2),new(stress[0],stress[1],stress[2],stress[3],stress[4],stress[5]),vm));
        }
        return result;
    }

    private static LinearElasticAnalysisResult Failure(LinearElasticAnalysisIr analysis,IReadOnlyList<AnalysisDiagnostic> diagnostics,SolverConvergence? convergence=null)=>new(analysis.Id,false,convergence??new(false,0,0,0,[],TimeSpan.Zero),[],[],[],new(Vector3D.Zero,Vector3D.Zero,Vector3D.Zero),new(0,0,0,true,true,0,0),new(0,0,0,0,[]),new(TimeSpan.Zero,TimeSpan.Zero,TimeSpan.Zero,TimeSpan.Zero,TimeSpan.Zero,TimeSpan.Zero,0,0),diagnostics);
    private static IEnumerable<(int I,int J,int K)> CellNodeKeys(CellIndex c){foreach(var o in NodeOffsets)yield return(c.I+o.X,c.J+o.Y,c.K+o.Z);}
    private static Point3D NodePosition(LatticeSpec l,(int I,int J,int K) n)=>new(l.Bounds.Min.X+n.I*l.CellSize.X,l.Bounds.Min.Y+n.J*l.CellSize.Y,l.Bounds.Min.Z+n.K*l.CellSize.Z);
    private static Point3D ToPhysical(BoundingBox3D b,double x,double y,double z)=>new(b.Min.X+(x+1)*(b.Max.X-b.Min.X)/2,b.Min.Y+(y+1)*(b.Max.Y-b.Min.Y)/2,b.Min.Z+(z+1)*(b.Max.Z-b.Min.Z)/2);
    private static Point3D Center(BoundingBox3D b)=>new((b.Min.X+b.Max.X)/2,(b.Min.Y+b.Max.Y)/2,(b.Min.Z+b.Max.Z)/2);
    private static double CellVolume(BoundingBox3D b)=>(b.Max.X-b.Min.X)*(b.Max.Y-b.Min.Y)*(b.Max.Z-b.Min.Z);
}
