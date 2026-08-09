using System.Diagnostics;
using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Lattice;
using Aetheris.FEA.Analysis;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.FEA.Mechanics;

public sealed record MechanicsSolveOptions(int CutCellQuadraturePerAxis = 4, double RelativeResidualTolerance = 1e-9, int? MaximumIterations = null);

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
        var domainStart = Stopwatch.GetTimestamp();
        var grid = ContinuumGridClassifier.Classify(analysis.Body.ContinuumRegion, analysis.Lattice, 2);
        var domainTime = Stopwatch.GetElapsedTime(domainStart);

        var quadratureStart = Stopwatch.GetTimestamp();
        var active = new List<(ContinuumCell Cell, MechanicsQuadraturePlan Plan)>();
        foreach (var cell in grid.Cells.Where(item => item.Classification != CellClassification.Outside))
        {
            var plan = CreateQuadrature(analysis.Body.ContinuumRegion, cell, options.CutCellQuadraturePerAxis);
            if (plan.Points.Count > 0) active.Add((cell, plan));
        }
        var quadratureTime = Stopwatch.GetElapsedTime(quadratureStart);
        if (active.Count == 0)
        {
            diagnostics.Add(new("fea-empty-domain", AnalysisDiagnosticSeverity.Error, "Continuum discretization admitted no occupied mechanics cells.", analysis.Provenance));
            return Failure(analysis, diagnostics);
        }

        var lattice = analysis.Lattice;
        var nodeKeys = active.SelectMany(item => CellNodeKeys(item.Cell.Index)).Distinct().OrderBy(item => item.K).ThenBy(item => item.J).ThenBy(item => item.I).ToArray();
        var nodeId = nodeKeys.Select((key, id) => (key, id)).ToDictionary(item => item.key, item => item.id);
        var nodes = nodeKeys.Select((key, id) => new MechanicsNode(id, NodePosition(lattice, key))).ToArray();
        var dofs = checked(nodes.Length * 3);
        var matrix = new SparseSymmetricMatrix(dofs);
        var load = new double[dofs];
        var assemblyStart = Stopwatch.GetTimestamp();
        var constitutive = Constitutive(material.YoungsModulusPascal, material.PoissonRatio);
        foreach (var item in active)
        {
            var ids = CellNodeKeys(item.Cell.Index).Select(key => nodeId[key]).ToArray();
            var local = CellStiffness(item.Cell.Bounds, item.Plan, constitutive);
            for (var a = 0; a < 24; a++)
            for (var b = 0; b < 24; b++) matrix.Add((3 * ids[a / 3]) + (a % 3), (3 * ids[b / 3]) + (b % 3), local[a,b]);
        }
        var assemblyTime = Stopwatch.GetElapsedTime(assemblyStart);

        var boundaryStart = Stopwatch.GetTimestamp();
        var integratedByLoad = new Dictionary<string, Vector3D>(StringComparer.Ordinal);
        foreach (var boundaryLoad in analysis.Loads)
        {
            var contributions = IntegrateLoad(analysis, boundaryLoad, active, nodeId);
            foreach (var contribution in contributions.Nodal)
            {
                load[(3 * contribution.Key) + 0] += contribution.Value.X;
                load[(3 * contribution.Key) + 1] += contribution.Value.Y;
                load[(3 * contribution.Key) + 2] += contribution.Value.Z;
            }
            integratedByLoad[boundaryLoad.Id] = contributions.Resultant;
            if (contributions.Area <= 0)
                diagnostics.Add(new("fea-empty-region-selection", AnalysisDiagnosticSeverity.Error, $"Region '{boundaryLoad.Region.Path}' selected no exact Continuum boundary fragments.", boundaryLoad.Provenance));
        }
        var prescribed = ResolveConstraints(analysis, nodes, diagnostics);
        if (prescribed.Count == 0)
            diagnostics.Add(new("fea-rigid-body-mode", AnalysisDiagnosticSeverity.Error, "Constraints selected no admitted lattice DOFs.", analysis.Provenance));
        var boundaryTime = Stopwatch.GetElapsedTime(boundaryStart);
        if (diagnostics.Any(item => item.Severity == AnalysisDiagnosticSeverity.Error)) return Failure(analysis, diagnostics);

        var rawMatrix = matrix.Copy();
        var rawLoad = (double[])load.Clone();
        matrix.ApplyDirichlet(prescribed, load);
        var (solution, convergence) = PreconditionedConjugateGradient.Solve(matrix, load, options.RelativeResidualTolerance, options.MaximumIterations);
        if (!convergence.Converged)
        {
            diagnostics.Add(new("fea-solver-non-convergence", AnalysisDiagnosticSeverity.Error, $"PCG did not converge after {convergence.Iterations} iterations; residual {convergence.FinalResidual:R}.", analysis.Provenance));
            return Failure(analysis, diagnostics, convergence);
        }

        var recoveryStart = Stopwatch.GetTimestamp();
        var displacements = nodes.Select(node => new NodalDisplacement(node.Id, node.Position, new Vector3D(solution[3*node.Id], solution[(3*node.Id)+1], solution[(3*node.Id)+2]))).ToArray();
        var fields = RecoverFields(active, nodeId, solution, constitutive);
        var residual = rawMatrix.Multiply(solution);
        for (var i = 0; i < residual.Length; i++) residual[i] -= rawLoad[i];
        var reactions = new List<ReactionResult>();
        foreach (var constraint in analysis.Constraints)
        {
            var vector = Vector3D.Zero;
            foreach (var node in nodes.Where(node => MatchesRegion(node.Position, constraint.Region.Path, lattice.Bounds)))
                vector += new Vector3D(residual[3*node.Id], residual[(3*node.Id)+1], residual[(3*node.Id)+2]);
            reactions.Add(new(constraint.Id, vector));
        }
        var applied = integratedByLoad.Values.Aggregate(Vector3D.Zero, (sum, value) => sum + value);
        var reaction = reactions.Aggregate(Vector3D.Zero, (sum, value) => sum + value.ForceNewton);
        var equilibrium = new EquilibriumResult(applied, reaction, applied + reaction);
        var recoveryTime = Stopwatch.GetElapsedTime(recoveryStart);
        var fractions = active.Select(item => item.Plan.IntegratedVolume / CellVolume(item.Cell.Bounds)).Order().ToArray();
        var tiny = new TinyCellDiagnostics(fractions[0], fractions.Count(x => x < .01), fractions.Count(x => x < .05), fractions.Count(x => x < .10), fractions);
        var declared = analysis.Loads.Where(item => item.Kind == BoundaryLoadKind.ResultantForce).Sum(item => item.VectorSi.Length);
        var actual = integratedByLoad.Values.Sum(item => item.Length);
        var system = new SparseSystemMetrics(dofs, rawMatrix.Nonzeros, rawMatrix.MaximumAsymmetry(), rawMatrix.IsFinite(), true, actual, declared == 0 ? 0 : double.Abs(actual - declared));
        var sparseBytes = (long)rawMatrix.Nonzeros * (sizeof(double) + sizeof(int)) + (long)(dofs + 1) * sizeof(int);
        var resultBytes = (long)displacements.Length * (sizeof(int) + 6*sizeof(double)) + (long)fields.Count * 16*sizeof(double);
        return new(analysis.Id, true, convergence, displacements, fields, reactions, equilibrium, system, tiny,
            new(domainTime, quadratureTime, assemblyTime, boundaryTime, convergence.Runtime, recoveryTime, sparseBytes, resultBytes), diagnostics);
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

    private sealed record LoadIntegration(Dictionary<int,Vector3D> Nodal,Vector3D Resultant,double Area);
    private static LoadIntegration IntegrateLoad(LinearElasticAnalysisIr analysis,BoundaryLoadIr load,IReadOnlyList<(ContinuumCell Cell,MechanicsQuadraturePlan Plan)> active,IReadOnlyDictionary<(int I,int J,int K),int> nodeId)
    {
        var axis=RegionAxis(load.Region.Path); if(axis is null)return new([],Vector3D.Zero,0);
        var (dimension,positive)=axis.Value; var bounds=analysis.Lattice.Bounds;
        var selected=active.Where(item=>IsBoundaryCell(item.Cell.Index,analysis.Lattice,dimension,positive)).ToArray();
        var points=new List<(int[] Nodes,double[] Shape,double Weight,Vector3D Normal)>();
        var g=1/double.Sqrt(3);
        foreach(var item in selected)
        {
            var ids=CellNodeKeys(item.Cell.Index).Select(key=>nodeId[key]).ToArray();
            foreach(var a in new[]{-g,g}) foreach(var b in new[]{-g,g})
            {
                var natural=dimension switch {0=>(positive?1d:-1d,a,b),1=>(a,positive?1d:-1d,b),_=>(a,b,positive?1d:-1d)};
                var position=ToPhysical(item.Cell.Bounds,natural.Item1,natural.Item2,natural.Item3);
                var inward=dimension switch {0=>new Vector3D(positive?-1:1,0,0),1=>new Vector3D(0,positive?-1:1,0),_=>new Vector3D(0,0,positive?-1:1)};
                var epsilon=double.Min(analysis.Lattice.CellSize.X,double.Min(analysis.Lattice.CellSize.Y,analysis.Lattice.CellSize.Z))*1e-8;
                if(analysis.Body.ContinuumRegion.Classify(position+(inward*epsilon))==ContinuumPointClassification.Outside)continue;
                var area=FaceArea(item.Cell.Bounds,dimension)/4;
                points.Add((ids,Shape(natural.Item1,natural.Item2,natural.Item3),area,-inward));
            }
        }
        var totalArea=points.Sum(item=>item.Weight); if(totalArea<=0)return new([],Vector3D.Zero,0);
        var nodal=new Dictionary<int,Vector3D>(); var resultant=Vector3D.Zero;
        foreach(var point in points)
        {
            var traction=load.Kind switch {BoundaryLoadKind.ResultantForce=>load.VectorSi/totalArea,BoundaryLoadKind.Pressure=>point.Normal*(-load.PressurePascal),_=>load.VectorSi};
            var force=traction*point.Weight; resultant+=force;
            for(var n=0;n<8;n++) if(point.Shape[n]!=0) nodal[point.Nodes[n]]=nodal.GetValueOrDefault(point.Nodes[n])+force*point.Shape[n];
        }
        return new(nodal,resultant,totalArea);
    }

    private static Dictionary<int,double> ResolveConstraints(LinearElasticAnalysisIr analysis,IReadOnlyList<MechanicsNode> nodes,List<AnalysisDiagnostic> diagnostics)
    {
        var result=new Dictionary<int,double>();
        foreach(var constraint in analysis.Constraints)
        {
            var selected=nodes.Where(node=>MatchesRegion(node.Position,constraint.Region.Path,analysis.Lattice.Bounds)).ToArray();
            if(selected.Length==0){diagnostics.Add(new("fea-empty-region-selection",AnalysisDiagnosticSeverity.Error,$"Constraint region '{constraint.Region.Path}' selected no lattice nodes.",constraint.Provenance));continue;}
            foreach(var node in selected) foreach(var component in constraint.Components)
            {
                var index=(3*node.Id)+(int)component; var value=component switch{DisplacementComponent.X=>constraint.ValueMeters.X,DisplacementComponent.Y=>constraint.ValueMeters.Y,_=>constraint.ValueMeters.Z};
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
    private static double FaceArea(BoundingBox3D b,int d)=>d switch{0=>(b.Max.Y-b.Min.Y)*(b.Max.Z-b.Min.Z),1=>(b.Max.X-b.Min.X)*(b.Max.Z-b.Min.Z),_=>(b.Max.X-b.Min.X)*(b.Max.Y-b.Min.Y)};
    private static double[] Shape(double x,double y,double z)=>NaturalSigns.Select(s=>.125*(1+s.Xi*x)*(1+s.Eta*y)*(1+s.Zeta*z)).ToArray();
    private static (int Dimension,bool Positive)? RegionAxis(string path){if(path.Contains("+X",StringComparison.OrdinalIgnoreCase)||path.Contains("x-max",StringComparison.OrdinalIgnoreCase))return(0,true);if(path.Contains("-X",StringComparison.OrdinalIgnoreCase)||path.Contains("x-min",StringComparison.OrdinalIgnoreCase))return(0,false);if(path.Contains("+Y",StringComparison.OrdinalIgnoreCase)||path.Contains("y-max",StringComparison.OrdinalIgnoreCase))return(1,true);if(path.Contains("-Y",StringComparison.OrdinalIgnoreCase)||path.Contains("y-min",StringComparison.OrdinalIgnoreCase))return(1,false);if(path.Contains("+Z",StringComparison.OrdinalIgnoreCase)||path.Contains("z-max",StringComparison.OrdinalIgnoreCase))return(2,true);if(path.Contains("-Z",StringComparison.OrdinalIgnoreCase)||path.Contains("z-min",StringComparison.OrdinalIgnoreCase))return(2,false);return null;}
    private static bool MatchesRegion(Point3D p,string path,BoundingBox3D b){var a=RegionAxis(path);if(a is null)return false;var value=a.Value.Dimension switch{0=>p.X,1=>p.Y,_=>p.Z};var target=a.Value.Dimension switch{0=>a.Value.Positive?b.Max.X:b.Min.X,1=>a.Value.Positive?b.Max.Y:b.Min.Y,_=>a.Value.Positive?b.Max.Z:b.Min.Z};var scale=double.Max(1,(b.Max-b.Min).Length);return double.Abs(value-target)<=scale*1e-10;}
    private static bool IsBoundaryCell(CellIndex c,LatticeSpec l,int d,bool positive)=>d switch{0=>c.I==(positive?l.CountX-1:0),1=>c.J==(positive?l.CountY-1:0),_=>c.K==(positive?l.CountZ-1:0)};
}
