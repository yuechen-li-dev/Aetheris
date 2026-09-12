using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aetheris.Continuum.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.FEA.Analysis;

namespace Aetheris.FEA.Mechanics;

/// <summary>
/// Experimental shell-aligned finite cells with three translational unknowns and the
/// ordinary three-dimensional small-strain constitutive law. This is not a shell element.
/// </summary>
public static class ThinWallFiniteCellSolver
{
    private readonly record struct AxisKey(bool IsVertex,int Location,int Mode);
    private readonly record struct BasisKey(AxisKey R,AxisKey S,AxisKey T);
    private readonly record struct LocalMode(int R,int S,int T);
    private readonly record struct QuadraturePoint(double Xi,double Eta,double Zeta,Point3D Position,double Weight,Vector3D GradXi,Vector3D GradEta,Vector3D GradZeta,bool Physical,double ElementDeterminant);
    private sealed record CellPlan(int I,int J,double R0,double R1,double S0,double S1,int[] Global,IReadOnlyList<QuadraturePoint> Points,ContinuumBoundsClassification Classification,double PhysicalFraction,int CutLeaves);
    private readonly record struct BoundaryFace(int Axis,bool Maximum,string Token);

    public static LinearElasticAnalysisResult Solve(LinearElasticAnalysisIr analysis,MechanicsSolveOptions? options=null)
    {
        options??=new MechanicsSolveOptions(RelativeResidualTolerance:1e-8,MaximumIterations:10000);var diagnostics=AnalysisIrValidator.Validate(analysis).ToList();
        if(analysis.Mode!=AnalysisMode.ExperimentalShell||analysis.ExperimentalShell is null)
        {
            diagnostics.Add(new("thinwall-mode-required",AnalysisDiagnosticSeverity.Error,"ThinWallFiniteCellSolver requires explicit ExperimentalShell mode.",analysis.Provenance));
            return Failure(analysis,diagnostics);
        }
        if(diagnostics.Any(x=>x.Severity==AnalysisDiagnosticSeverity.Error))return Failure(analysis,diagnostics);
        if(options.DomainTransform is not null)
        {
            diagnostics.Add(new("thinwall-geometry-unsupported",AnalysisDiagnosticSeverity.Error,"ExperimentalShell X0 does not apply the production world-space lattice rotation option.",analysis.Provenance));
            return Failure(analysis,diagnostics);
        }
        if(analysis.Body.ContinuumRegion is not IBoundsClassificationCapability classifier)
        {
            diagnostics.Add(new("thinwall-boundary-invalid",AnalysisDiagnosticSeverity.Error,"ExperimentalShell X0 requires exact parameter-space bounds classification.",analysis.Body.Provenance));
            return Failure(analysis,diagnostics);
        }

        var settings=analysis.ExperimentalShell;var bounds=analysis.Body.ContinuumRegion.Bounds;
        ThinWallGeometryMap geometry;
        try{geometry=new(settings,bounds);}catch(ThinWallMapException ex){diagnostics.Add(new(ex.Code,AnalysisDiagnosticSeverity.Error,ex.Message,analysis.Provenance));return Failure(analysis,diagnostics);}
        var p=settings.PolynomialOrder;var qOrder=settings.QuadratureOrder??p+1;var modes=LocalModesFor(p);var nx=settings.MasterCellsR;var ny=settings.MasterCellsS;
        var dr=(bounds.Max.X-bounds.Min.X)/nx;var ds=(bounds.Max.Y-bounds.Min.Y)/ny;
        var allKeys=new List<BasisKey>();
        for(var j=0;j<ny;j++)for(var i=0;i<nx;i++)foreach(var mode in modes)allKeys.Add(Key(i,j,mode));
        var orderedKeys=allKeys.Distinct().OrderBy(k=>AxisSort(k.T)).ThenBy(k=>AxisSort(k.S)).ThenBy(k=>AxisSort(k.R)).ToArray();
        var globalId=orderedKeys.Select((key,index)=>(key,index)).ToDictionary(x=>x.key,x=>x.index);

        var domainStart=Stopwatch.GetTimestamp();var plans=new List<CellPlan>();var minDet=double.PositiveInfinity;var maxDet=double.NegativeInfinity;
        for(var j=0;j<ny;j++)for(var i=0;i<nx;i++)
        {
            var r0=bounds.Min.X+i*dr;var r1=r0+dr;var s0=bounds.Min.Y+j*ds;var s1=s0+ds;
            var classification=classifier.ClassifyBounds(new(new(r0,s0,bounds.Min.Z),new(r1,s1,bounds.Max.Z)));
            var points=new List<QuadraturePoint>();var physicalVolume=0d;var totalVolume=0d;var cutLeaves=0;
            try{IntegrateRectangle(r0,r1,s0,s1,0,classification==ContinuumBoundsClassification.Cut?null:classification==ContinuumBoundsClassification.Inside,points,ref physicalVolume,ref totalVolume,ref cutLeaves);}
            catch(ThinWallMapException ex){diagnostics.Add(new(ex.Code,AnalysisDiagnosticSeverity.Error,ex.Message,analysis.Provenance));return Failure(analysis,diagnostics);}
            foreach(var point in points){minDet=double.Min(minDet,point.ElementDeterminant);maxDet=double.Max(maxDet,point.ElementDeterminant);}
            var globals=modes.Select(mode=>globalId[Key(i,j,mode)]).ToArray();
            plans.Add(new(i,j,r0,r1,s0,s1,globals,points,classification,totalVolume==0?0:physicalVolume/totalVolume,cutLeaves));

            void IntegrateRectangle(double ar0,double ar1,double as0,double as1,int depth,bool? allPhysical,List<QuadraturePoint> target,ref double physical,ref double total,ref int leaves)
            {
                bool? state=allPhysical;
                if(state is null&&depth<settings.MaxSubdivisionDepth)
                {
                    var rm=(ar0+ar1)/2;var sm=(as0+as1)/2;
                    foreach(var child in new[]{(ar0,rm,as0,sm),(rm,ar1,as0,sm),(ar0,rm,sm,as1),(rm,ar1,sm,as1)})
                    {
                        var c=classifier.ClassifyBounds(new(new(child.Item1,child.Item3,bounds.Min.Z),new(child.Item2,child.Item4,bounds.Max.Z)));
                        IntegrateRectangle(child.Item1,child.Item2,child.Item3,child.Item4,depth+1,c==ContinuumBoundsClassification.Cut?null:c==ContinuumBoundsClassification.Inside,target,ref physical,ref total,ref leaves);
                    }
                    return;
                }
                if(state is null)leaves++;
                var gauss=Gauss(qOrder);
                foreach(var gr in gauss)foreach(var gs in gauss)foreach(var gt in gauss)
                {
                    var r=(ar0+ar1)/2+gr.X*(ar1-ar0)/2;var s=(as0+as1)/2+gs.X*(as1-as0)/2;var t=gt.X;
                    var isPhysical=state??analysis.Body.ContinuumRegion.Classify(new(r,s,(bounds.Min.Z+bounds.Max.Z)/2))!=ContinuumPointClassification.Outside;
                    var mapped=geometry.Evaluate(r,s,t);var a=mapped.DerivativeR*(dr/2);var b=mapped.DerivativeS*(ds/2);var c=mapped.DerivativeT;var det=a.Dot(b.Cross(c));
                    if(!double.IsFinite(det)||det==0)throw new ThinWallMapException("thinwall-map-singular",$"Thin-wall map Jacobian is singular or nonfinite at ({r:R},{s:R},{t:R}).");
                    if(det<0)throw new ThinWallMapException("thinwall-map-inverted",$"Thin-wall map Jacobian is inverted at ({r:R},{s:R},{t:R}); det(J)={det:R}.");
                    var gradXi=b.Cross(c)*(1/det);var gradEta=c.Cross(a)*(1/det);var gradZeta=a.Cross(b)*(1/det);
                    var rawWeight=gr.W*gs.W*gt.W*(ar1-ar0)/2*(as1-as0)/2*mapped.DerivativeR.Dot(mapped.DerivativeS.Cross(mapped.DerivativeT));
                    if(!double.IsFinite(rawWeight)||rawWeight<=0)throw new ThinWallMapException("thinwall-cut-integration-failed","Thin-wall quadrature produced a nonpositive or nonfinite physical weight.");
                    total+=rawWeight;if(isPhysical)physical+=rawWeight;
                    target.Add(new(2*(r-r0)/dr-1,2*(s-s0)/ds-1,t,mapped.Position,rawWeight*(isPhysical?1:settings.FictitiousStiffnessFactor),gradXi,gradEta,gradZeta,isPhysical,det));
                }
            }
        }
        var domainTime=Stopwatch.GetElapsedTime(domainStart);
        if(diagnostics.Any(x=>x.Severity==AnalysisDiagnosticSeverity.Error)||plans.Count==0)return Failure(analysis,diagnostics);

        var dofs=checked(orderedKeys.Length*3);var matrix=new SparseSymmetricMatrix(dofs);var load=new double[dofs];var constitutive=Constitutive(analysis.Materials.Single().YoungsModulusPascal,analysis.Materials.Single().PoissonRatio);
        var assemblyStart=Stopwatch.GetTimestamp();
        try
        {
            foreach(var cell in plans)
            {
                var localDofs=modes.Count*3;var local=new double[localDofs,localDofs];
                foreach(var point in cell.Points)
                {
                    var b=BMatrix(p,modes,point);var db=new double[6,localDofs];
                    for(var row=0;row<6;row++)for(var column=0;column<localDofs;column++)for(var k=0;k<6;k++)db[row,column]+=constitutive[row,k]*b[k,column];
                    for(var row=0;row<localDofs;row++)for(var column=0;column<localDofs;column++)
                    {
                        var value=0d;for(var k=0;k<6;k++)value+=b[k,row]*db[k,column];local[row,column]+=value*point.Weight;
                    }
                }
                for(var a=0;a<modes.Count;a++)for(var ca=0;ca<3;ca++)for(var b=0;b<modes.Count;b++)for(var cb=0;cb<3;cb++)matrix.Add(3*cell.Global[a]+ca,3*cell.Global[b]+cb,local[3*a+ca,3*b+cb]);
            }
        }
        catch(ArithmeticException ex){diagnostics.Add(new("thinwall-cut-integration-failed",AnalysisDiagnosticSeverity.Error,ex.Message,analysis.Provenance));return Failure(analysis,diagnostics);}
        var assemblyTime=Stopwatch.GetElapsedTime(assemblyStart);var volumeMatrix=matrix.Copy();

        var boundaryStart=Stopwatch.GetTimestamp();var loadEvidence=new List<BoundaryLoadEvidence>();var applied=Vector3D.Zero;
        foreach(var item in analysis.Loads)
        {
            if(!TryFace(item.Region.Path,out var face)){diagnostics.Add(new("thinwall-boundary-condition-unsupported",AnalysisDiagnosticSeverity.Error,$"ExperimentalShell X0 boundary '{item.Region.Path}' is not an exact master-domain face.",item.Provenance));continue;}
            var weights=new Dictionary<int,double>();var pressureForces=new Dictionary<int,Vector3D>();var area=0d;var firstMoment=Vector3D.Zero;var pressureResultant=Vector3D.Zero;var pressureMoment=Vector3D.Zero;
            foreach(var cell in BoundaryCells(plans,face,nx,ny))
            {
                foreach(var sample in BoundarySamples(cell,face,qOrder,geometry,bounds))
                {
                    if(analysis.Body.ContinuumRegion.Classify(new(sample.R,sample.S,(bounds.Min.Z+bounds.Max.Z)/2))==ContinuumPointClassification.Outside)continue;
                    area+=sample.Weight;firstMoment+=new Vector3D(sample.Position.X,sample.Position.Y,sample.Position.Z)*sample.Weight;
                    var values=BasisValues(p,modes,sample.Xi,sample.Eta,sample.Zeta);
                    for(var n=0;n<modes.Count;n++)
                    {
                        var shape=values[n];if(double.Abs(shape)<1e-15)continue;var id=cell.Global[n];weights[id]=weights.GetValueOrDefault(id)+shape*sample.Weight;
                        if(item.Kind==BoundaryLoadKind.Pressure)
                        {
                            var f=sample.Normal*(-item.PressurePascal*shape*sample.Weight);pressureForces[id]=pressureForces.GetValueOrDefault(id)+f;
                        }
                    }
                    if(item.Kind==BoundaryLoadKind.Pressure){var f=sample.Normal*(-item.PressurePascal*sample.Weight);pressureResultant+=f;pressureMoment+=new Vector3D(sample.Position.X,sample.Position.Y,sample.Position.Z).Cross(f);}
                }
            }
            if(area<=0){diagnostics.Add(new("thinwall-boundary-invalid",AnalysisDiagnosticSeverity.Error,$"Boundary '{item.Region.Path}' selected no physical area.",item.Provenance));continue;}
            Vector3D resultant;Vector3D moment;
            if(item.Kind==BoundaryLoadKind.Pressure)
            {
                foreach(var pair in pressureForces){load[3*pair.Key]+=pair.Value.X;load[3*pair.Key+1]+=pair.Value.Y;load[3*pair.Key+2]+=pair.Value.Z;}
                resultant=pressureResultant;moment=pressureMoment;
            }
            else
            {
                var traction=item.Kind==BoundaryLoadKind.ResultantForce?item.VectorSi*(1/area):item.VectorSi;
                foreach(var pair in weights){var f=traction*pair.Value;load[3*pair.Key]+=f.X;load[3*pair.Key+1]+=f.Y;load[3*pair.Key+2]+=f.Z;}
                resultant=traction*area;var centroid=firstMoment/area;moment=centroid.Cross(resultant);
            }
            applied+=resultant;
            loadEvidence.Add(new(item.Id,item.Region.Path,face.Token,null,area,area,resultant,resultant,moment,moment,0,0,BoundaryCells(plans,face,nx,ny).Count,qOrder*qOrder*BoundaryCells(plans,face,nx,ny).Count,"Exact master-face quadrature with semantic parameter-space trimming"));
        }
        var prescribed=new Dictionary<int,double>();var constrainedById=new Dictionary<string,HashSet<int>>(StringComparer.Ordinal);
        foreach(var constraint in analysis.Constraints)
        {
            if(!TryFace(constraint.Region.Path,out var face)){diagnostics.Add(new("thinwall-boundary-condition-unsupported",AnalysisDiagnosticSeverity.Error,$"ExperimentalShell X0 constraint '{constraint.Region.Path}' is not an exact master-domain face.",constraint.Provenance));continue;}
            var selected=orderedKeys.Select((key,id)=>(key,id)).Where(x=>OnFace(x.key,face,nx,ny)).Select(x=>x.id).ToHashSet();
            if(selected.Count==0){diagnostics.Add(new("thinwall-boundary-invalid",AnalysisDiagnosticSeverity.Error,$"Constraint '{constraint.Id}' selected no basis trace.",constraint.Provenance));continue;}
            constrainedById[constraint.Id]=selected;
            foreach(var id in selected)foreach(var component in constraint.Components)
                prescribed[3*id+(int)component]=orderedKeys[id].R.IsVertex&&orderedKeys[id].S.IsVertex&&orderedKeys[id].T.IsVertex?Component(constraint.ValueMeters,component):0;
        }
        var boundaryTime=Stopwatch.GetElapsedTime(boundaryStart);
        if(diagnostics.Any(x=>x.Severity==AnalysisDiagnosticSeverity.Error))return Failure(analysis,diagnostics);

        var rawMatrix=matrix.Copy();var rawLoad=(double[])load.Clone();matrix.ApplyDirichlet(prescribed,load);var solveStart=Stopwatch.GetTimestamp();
        var (solution,convergence)=PreconditionedConjugateGradient.Solve(matrix,load,options.RelativeResidualTolerance,options.MaximumIterations);var solveTime=Stopwatch.GetElapsedTime(solveStart);
        if(!convergence.Converged){diagnostics.Add(new("thinwall-linear-solve-failed",AnalysisDiagnosticSeverity.Error,$"ExperimentalShell PCG did not converge after {convergence.Iterations} iterations; residual {convergence.FinalResidual:R}.",analysis.Provenance));return Failure(analysis,diagnostics,convergence);}
        if(solution.Any(x=>!double.IsFinite(x))){diagnostics.Add(new("thinwall-result-nonfinite",AnalysisDiagnosticSeverity.Error,"ExperimentalShell solve produced a nonfinite displacement coefficient.",analysis.Provenance));return Failure(analysis,diagnostics,convergence);}

        var recoveryStart=Stopwatch.GetTimestamp();var displacements=new List<NodalDisplacement>();
        foreach(var pair in orderedKeys.Select((key,id)=>(key,id)).Where(x=>x.key.R.IsVertex&&x.key.S.IsVertex&&x.key.T.IsVertex))
        {
            var r=bounds.Min.X+pair.key.R.Location*dr;var s=bounds.Min.Y+pair.key.S.Location*ds;var t=pair.key.T.Location==0?-1d:1d;var position=geometry.Evaluate(r,s,t).Position;
            displacements.Add(new(pair.id,position,new(solution[3*pair.id],solution[3*pair.id+1],solution[3*pair.id+2])));
        }
        var fields=new List<CellFieldResult>();
        foreach(var cell in plans.Where(x=>x.PhysicalFraction>0))foreach(var t in new[]{-1d,0d,1d})
        {
            var r=(cell.R0+cell.R1)/2;var s=(cell.S0+cell.S1)/2;if(analysis.Body.ContinuumRegion.Classify(new(r,s,(bounds.Min.Z+bounds.Max.Z)/2))==ContinuumPointClassification.Outside)continue;
            var mapped=geometry.Evaluate(r,s,t);var a=mapped.DerivativeR*(dr/2);var b=mapped.DerivativeS*(ds/2);var c=mapped.DerivativeT;var det=a.Dot(b.Cross(c));
            if(det<=0||!double.IsFinite(det)){diagnostics.Add(new(det<0?"thinwall-map-inverted":"thinwall-map-singular",AnalysisDiagnosticSeverity.Error,"Thin-wall recovery encountered an invalid Jacobian.",analysis.Provenance));return Failure(analysis,diagnostics,convergence);}
            var point=new QuadraturePoint(0,0,t,mapped.Position,0,b.Cross(c)*(1/det),c.Cross(a)*(1/det),a.Cross(b)*(1/det),true,det);var bm=BMatrix(p,modes,point);var strain=new double[6];
            for(var row=0;row<6;row++)for(var n=0;n<modes.Count;n++)for(var component=0;component<3;component++)strain[row]+=bm[row,3*n+component]*solution[3*cell.Global[n]+component];
            var stress=Multiply(constitutive,strain);var st=new SymmetricTensor(stress[0],stress[1],stress[2],stress[3],stress[4],stress[5]);var et=new SymmetricTensor(strain[0],strain[1],strain[2],strain[3],strain[4],strain[5]);
            fields.Add(new(cell.I,cell.J,t<0?-1:t>0?1:0,mapped.Position,et,st,VonMises(st)));
        }
        var rawResidual=rawMatrix.Multiply(solution);for(var i=0;i<rawResidual.Length;i++)rawResidual[i]-=rawLoad[i];var reactions=new List<ReactionResult>();
        foreach(var constraint in analysis.Constraints)
        {
            var ids=constrainedById.GetValueOrDefault(constraint.Id,[]);var reaction=Vector3D.Zero;
            foreach(var id in ids.Where(id=>orderedKeys[id].R.IsVertex&&orderedKeys[id].S.IsVertex&&orderedKeys[id].T.IsVertex))
            {
                if(constraint.Components.Contains(DisplacementComponent.X))reaction+=new Vector3D(rawResidual[3*id],0,0);
                if(constraint.Components.Contains(DisplacementComponent.Y))reaction+=new Vector3D(0,rawResidual[3*id+1],0);
                if(constraint.Components.Contains(DisplacementComponent.Z))reaction+=new Vector3D(0,0,rawResidual[3*id+2]);
            }
            reactions.Add(new(constraint.Id,reaction));
        }
        var reactionTotal=reactions.Aggregate(Vector3D.Zero,(sum,x)=>sum+x.ForceNewton);var equilibrium=new EquilibriumResult(applied,reactionTotal,applied+reactionTotal);
        var energyValue=.5*Dot(solution,volumeMatrix.Multiply(solution));var integratedEnergy=0d;
        foreach(var cell in plans)foreach(var point in cell.Points)
        {
            var bm=BMatrix(p,modes,point);var strain=new double[6];
            for(var row=0;row<6;row++)for(var n=0;n<modes.Count;n++)for(var component=0;component<3;component++)strain[row]+=bm[row,3*n+component]*solution[3*cell.Global[n]+component];
            var stress=Multiply(constitutive,strain);integratedEnergy+=.5*Dot(strain,stress)*point.Weight;
        }
        var energyAbsolute=double.Abs(energyValue-integratedEnergy);var energyRelative=energyAbsolute/double.Max(double.Abs(energyValue),1e-30);
        var energy=new StrainEnergyConsistency(energyValue,integratedEnergy,energyAbsolute,energyRelative);var recoveryTime=Stopwatch.GetElapsedTime(recoveryStart);
        var conditioning=rawMatrix.ConditioningProxy();var cut=plans.Where(x=>x.Classification==ContinuumBoundsClassification.Cut).ToArray();var fractions=plans.Select(x=>x.PhysicalFraction).Order().ToArray();
        var system=new SparseSystemMetrics(dofs,rawMatrix.Nonzeros,rawMatrix.MaximumAsymmetry(),rawMatrix.IsFinite(),true,applied.Length,0,cut.Length,null,dofs,prescribed.Count,0,conditioning.MinimumDiagonal,conditioning.MaximumDiagonal,conditioning.DiagonalRatio,conditioning.MinimumRowNorm,conditioning.MaximumRowNorm,conditioning.RowNormRatio);
        var integrationFingerprint=string.Join(";",plans.Select(cell=>$"{cell.I},{cell.J},{cell.Classification},{cell.PhysicalFraction:R},{cell.Points.Count},{cell.Points.Count(point=>point.Physical)}"));
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|",orderedKeys)+$"|{nx}|{ny}|{p}|{qOrder}|{settings.FictitiousStiffnessFactor:R}|{settings.MaxSubdivisionDepth}|{settings.GeometryMap}|{integrationFingerprint}"))).ToLowerInvariant();
        var evidence=new ExperimentalShellEvidence("Experimental - not production-qualified",settings.GeometryMap.ToString(),bounds,nx,ny,p,"Tensor hierarchical integrated-Legendre p-space",settings.ThicknessMeters,settings.FictitiousStiffnessFactor,qOrder,settings.MaxSubdivisionDepth,
            plans.Count(x=>x.Classification==ContinuumBoundsClassification.Inside),cut.Length,plans.Count(x=>x.Classification==ContinuumBoundsClassification.Outside),plans.Sum(x=>x.CutLeaves),cut.Length==0?1:cut.Min(x=>x.PhysicalFraction),minDet,maxDet,[-1,0,1],hash);
        diagnostics.Add(new("thinwall-experimental",AnalysisDiagnosticSeverity.Warning,"ExperimentalShell is a research discretization and is not production-qualified FEA.",analysis.Provenance));
        var reportedDisplacements=analysis.RequestedFields.Contains(AnalysisResultField.Displacement)?displacements:[];var wantsStrain=analysis.RequestedFields.Contains(AnalysisResultField.Strain);var wantsStress=analysis.RequestedFields.Contains(AnalysisResultField.Stress);
        var reportedFields=wantsStrain&&wantsStress?fields:[];var strainFields=wantsStrain?fields.Select(x=>new CellStrainResult(x.I,x.J,x.K,x.Position,x.Strain)).ToArray():[];var stressFields=wantsStress?fields.Select(x=>new CellStressResult(x.I,x.J,x.K,x.Position,x.StressPascal,x.VonMisesPascal)).ToArray():[];
        return new(analysis.Id,true,convergence,reportedDisplacements,reportedFields,analysis.RequestedFields.Contains(AnalysisResultField.ReactionForce)?reactions:[],equilibrium,system,
            new(fractions.Length==0?0:fractions[0],fractions.Count(x=>x<.01),fractions.Count(x=>x<.05),fractions.Count(x=>x<.1),fractions),
            new(domainTime,TimeSpan.Zero,assemblyTime,boundaryTime,solveTime,recoveryTime,(long)rawMatrix.Nonzeros*(sizeof(double)+sizeof(int)),(long)(displacements.Count+fields.Count)*64),diagnostics,loadEvidence,null,energy,[],strainFields,stressFields,evidence);
    }

    private static IReadOnlyList<LocalMode> LocalModesFor(int p){var result=new List<LocalMode>();for(var t=0;t<=p;t++)for(var s=0;s<=p;s++)for(var r=0;r<=p;r++)result.Add(new(r,s,t));return result;}
    private static BasisKey Key(int i,int j,LocalMode m)=>new(Axis(i,m.R),Axis(j,m.S),Axis(0,m.T));
    private static AxisKey Axis(int cell,int mode)=>mode switch{0=>new(true,cell,0),1=>new(true,cell+1,0),_=>new(false,cell,mode)};
    private static string AxisSort(AxisKey key)=>$"{key.Location:D6}:{(key.IsVertex?0:1)}:{key.Mode:D3}";
    private static double Component(Vector3D value,DisplacementComponent component)=>component switch{DisplacementComponent.X=>value.X,DisplacementComponent.Y=>value.Y,_=>value.Z};

    private static double[,] BMatrix(int p,IReadOnlyList<LocalMode> modes,QuadraturePoint point)
    {
        var xr=OneDimensional(p,point.Xi);var xs=OneDimensional(p,point.Eta);var xt=OneDimensional(p,point.Zeta);var b=new double[6,modes.Count*3];
        for(var n=0;n<modes.Count;n++)
        {
            var m=modes[n];var dXi=xr.D[m.R]*xs.N[m.S]*xt.N[m.T];var dEta=xr.N[m.R]*xs.D[m.S]*xt.N[m.T];var dZeta=xr.N[m.R]*xs.N[m.S]*xt.D[m.T];var g=point.GradXi*dXi+point.GradEta*dEta+point.GradZeta*dZeta;var c=3*n;
            b[0,c]=g.X;b[1,c+1]=g.Y;b[2,c+2]=g.Z;b[3,c]=g.Y;b[3,c+1]=g.X;b[4,c+1]=g.Z;b[4,c+2]=g.Y;b[5,c]=g.Z;b[5,c+2]=g.X;
        }
        return b;
    }
    private static double[] BasisValues(int p,IReadOnlyList<LocalMode> modes,double xi,double eta,double zeta){var x=OneDimensional(p,xi).N;var y=OneDimensional(p,eta).N;var z=OneDimensional(p,zeta).N;return modes.Select(m=>x[m.R]*y[m.S]*z[m.T]).ToArray();}
    private static (double[] N,double[] D) OneDimensional(int p,double x)
    {
        var legendre=new double[p+1];var derivative=new double[p+1];legendre[0]=1;if(p>0){legendre[1]=x;derivative[1]=1;}
        for(var n=2;n<=p;n++){legendre[n]=((2*n-1)*x*legendre[n-1]-(n-1)*legendre[n-2])/n;derivative[n]=((2*n-1)*(legendre[n-1]+x*derivative[n-1])-(n-1)*derivative[n-2])/n;}
        var values=new double[p+1];var derivatives=new double[p+1];values[0]=(1-x)/2;values[1]=(1+x)/2;derivatives[0]=-.5;derivatives[1]=.5;
        for(var n=2;n<=p;n++){var scale=double.Sqrt(4*n-2);values[n]=(legendre[n]-legendre[n-2])/scale;derivatives[n]=(derivative[n]-derivative[n-2])/scale;}
        return(values,derivatives);
    }

    private static IReadOnlyList<(double X,double W)> Gauss(int order)
    {
        var result=new (double X,double W)[order];var half=(order+1)/2;
        for(var i=0;i<half;i++)
        {
            var z=double.Cos(double.Pi*(i+.75)/(order+.5));double derivative;
            for(var iteration=0;iteration<30;iteration++){var (pn,pnm1)=Legendre(order,z);derivative=order*(z*pn-pnm1)/(z*z-1);var next=z-pn/derivative;if(double.Abs(next-z)<1e-15){z=next;break;}z=next;}
            var final=Legendre(order,z);derivative=order*(z*final.Pn-final.Pnm1)/(z*z-1);var w=2/((1-z*z)*derivative*derivative);result[i]=(-z,w);result[order-1-i]=(z,w);
        }
        return result;
    }
    private static (double Pn,double Pnm1) Legendre(int n,double x){var p0=1d;if(n==0)return(1,1);var p1=x;for(var k=2;k<=n;k++){var p=((2*k-1)*x*p1-(k-1)*p0)/k;p0=p1;p1=p;}return(p1,p0);}
    private static double[,] Constitutive(double e,double nu){var d=new double[6,6];var factor=e/((1+nu)*(1-2*nu));var a=(1-nu)*factor;var b=nu*factor;var g=e/(2*(1+nu));d[0,0]=d[1,1]=d[2,2]=a;d[0,1]=d[0,2]=d[1,0]=d[1,2]=d[2,0]=d[2,1]=b;d[3,3]=d[4,4]=d[5,5]=g;return d;}
    private static double[] Multiply(double[,] matrix,IReadOnlyList<double> vector){var result=new double[matrix.GetLength(0)];for(var i=0;i<result.Length;i++)for(var j=0;j<vector.Count;j++)result[i]+=matrix[i,j]*vector[j];return result;}
    private static double Dot(IReadOnlyList<double> a,IReadOnlyList<double> b){var result=0d;for(var i=0;i<a.Count;i++)result+=a[i]*b[i];return result;}
    private static double VonMises(SymmetricTensor s)=>double.Sqrt(.5*(double.Pow(s.XX-s.YY,2)+double.Pow(s.YY-s.ZZ,2)+double.Pow(s.ZZ-s.XX,2))+3*(s.XY*s.XY+s.YZ*s.YZ+s.XZ*s.XZ));

    private static bool TryFace(string path,out BoundaryFace face)
    {
        if(path.Contains("-X",StringComparison.OrdinalIgnoreCase)||path.Contains("x-min",StringComparison.OrdinalIgnoreCase)){face=new(0,false,"r-min");return true;}
        if(path.Contains("+X",StringComparison.OrdinalIgnoreCase)||path.Contains("x-max",StringComparison.OrdinalIgnoreCase)){face=new(0,true,"r-max");return true;}
        if(path.Contains("-Y",StringComparison.OrdinalIgnoreCase)||path.Contains("y-min",StringComparison.OrdinalIgnoreCase)){face=new(1,false,"s-min");return true;}
        if(path.Contains("+Y",StringComparison.OrdinalIgnoreCase)||path.Contains("y-max",StringComparison.OrdinalIgnoreCase)){face=new(1,true,"s-max");return true;}
        if(path.Contains("-Z",StringComparison.OrdinalIgnoreCase)||path.Contains("z-min",StringComparison.OrdinalIgnoreCase)){face=new(2,false,"t-min");return true;}
        if(path.Contains("+Z",StringComparison.OrdinalIgnoreCase)||path.Contains("z-max",StringComparison.OrdinalIgnoreCase)){face=new(2,true,"t-max");return true;}
        face=default;return false;
    }
    private static bool OnFace(BasisKey key,BoundaryFace face,int nx,int ny){var axis=face.Axis switch{0=>key.R,1=>key.S,_=>key.T};var max=face.Axis switch{0=>nx,1=>ny,_=>1};return axis.IsVertex&&axis.Location==(face.Maximum?max:0);}
    private static IReadOnlyList<CellPlan> BoundaryCells(IReadOnlyList<CellPlan> plans,BoundaryFace face,int nx,int ny)=>plans.Where(x=>face.Axis switch{0=>x.I==(face.Maximum?nx-1:0),1=>x.J==(face.Maximum?ny-1:0),_=>true}).ToArray();
    private readonly record struct BoundarySample(double R,double S,double Xi,double Eta,double Zeta,Point3D Position,Vector3D Normal,double Weight);
    private static IEnumerable<BoundarySample> BoundarySamples(CellPlan cell,BoundaryFace face,int order,ThinWallGeometryMap map,BoundingBox3D bounds)
    {
        var gauss=Gauss(order);
        foreach(var a in gauss)foreach(var b in gauss)
        {
            double r,s,t,xi,eta,zeta,scale;Vector3D tangentA,tangentB;
            if(face.Axis==0){r=face.Maximum?cell.R1:cell.R0;s=(cell.S0+cell.S1)/2+a.X*(cell.S1-cell.S0)/2;t=b.X;xi=face.Maximum?1:-1;eta=a.X;zeta=b.X;var e=map.Evaluate(r,s,t);tangentA=e.DerivativeS;tangentB=e.DerivativeT;scale=(cell.S1-cell.S0)/2;var cross=tangentA.Cross(tangentB);var normal=Normalize(cross);if(!face.Maximum)normal=-normal;yield return new(r,s,xi,eta,zeta,e.Position,normal,a.W*b.W*scale*cross.Length);}
            else if(face.Axis==1){r=(cell.R0+cell.R1)/2+a.X*(cell.R1-cell.R0)/2;s=face.Maximum?cell.S1:cell.S0;t=b.X;xi=a.X;eta=face.Maximum?1:-1;zeta=b.X;var e=map.Evaluate(r,s,t);tangentA=e.DerivativeT;tangentB=e.DerivativeR;scale=(cell.R1-cell.R0)/2;var cross=tangentA.Cross(tangentB);var normal=Normalize(cross);if(!face.Maximum)normal=-normal;yield return new(r,s,xi,eta,zeta,e.Position,normal,a.W*b.W*scale*cross.Length);}
            else{r=(cell.R0+cell.R1)/2+a.X*(cell.R1-cell.R0)/2;s=(cell.S0+cell.S1)/2+b.X*(cell.S1-cell.S0)/2;t=face.Maximum?1:-1;xi=a.X;eta=b.X;zeta=t;var e=map.Evaluate(r,s,t);tangentA=e.DerivativeR;tangentB=e.DerivativeS;scale=(cell.R1-cell.R0)*(cell.S1-cell.S0)/4;var cross=tangentA.Cross(tangentB);var normal=Normalize(cross);if(!face.Maximum)normal=-normal;yield return new(r,s,xi,eta,zeta,e.Position,normal,a.W*b.W*scale*cross.Length);}
        }
    }

    private static Vector3D Normalize(Vector3D value){var length=value.Length;if(!double.IsFinite(length)||length<=0)throw new ThinWallMapException("thinwall-map-singular","Thin-wall boundary map produced a singular surface Jacobian.");return value*(1/length);}

    private static LinearElasticAnalysisResult Failure(LinearElasticAnalysisIr analysis,IReadOnlyList<AnalysisDiagnostic> diagnostics,SolverConvergence? convergence=null)=>new(analysis.Id,false,convergence??new(false,0,0,0,[],TimeSpan.Zero),[],[],[],new(Vector3D.Zero,Vector3D.Zero,Vector3D.Zero),new(0,0,0,true,true,0,0),new(0,0,0,0,[]),new(TimeSpan.Zero,TimeSpan.Zero,TimeSpan.Zero,TimeSpan.Zero,TimeSpan.Zero,TimeSpan.Zero,0,0),diagnostics);
}
