using System.Security.Cryptography;
using System.Text;
using Aetheris.Continuum.Lattice;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.FEA.Mechanics;

internal sealed record NodeExtension(int SourceNodeId,CellIndex RootCell,IReadOnlyDictionary<int,double> Weights);
internal sealed record BasisLoweringPlan(
    IReadOnlyList<BasisSupportEvidence> Supports,IReadOnlyList<BasisTreatmentEvidence> Treatments,IReadOnlyDictionary<int,NodeExtension> Extensions,
    IReadOnlyDictionary<int,IReadOnlyList<(int EffectiveNode,double Weight)>> NodeMappings,int EffectiveNodeCount,int JudgmentCalls,int FixedThresholdCount);

internal static class ImmersedNumericalLowering
{
    // Frozen M5C-v1 score.  Its zero crossing is deliberately near 2% normalized support;
    // the affine extension cost prevents aggregation when no adjacent full root cell exists.
    internal const double SupportRiskWeight=1.0;
    internal const double AggregationSetupCost=.01;
    internal const double OrdinaryIntercept=0;
    internal const double AggregateIntercept=.03;
    internal const double FixedControlThreshold=.02;

    internal static BasisLoweringPlan PlanBasis(
        LatticeSpec lattice,IReadOnlyList<(ContinuumCell Cell,MechanicsQuadraturePlan Plan)> active,
        IReadOnlyDictionary<(int I,int J,int K),int> nodeId,IReadOnlyList<MechanicsNode> nodes,
        IReadOnlyList<(int X,int Y,int Z)> nodeOffsets,IReadOnlyList<(double Xi,double Eta,double Zeta)> naturalSigns)
    {
        var physical=new double[nodes.Count];var incidents=new int[nodes.Count];
        foreach(var item in active)
        {
            var ids=CellKeys(item.Cell.Index,nodeOffsets).Select(key=>nodeId[key]).ToArray();
            foreach(var point in item.Plan.Points)
            {
                var shape=Shape(point.Xi,point.Eta,point.Zeta,naturalSigns);
                for(var n=0;n<8;n++)physical[ids[n]]+=shape[n]*shape[n]*point.Weight;
            }
            foreach(var id in ids)incidents[id]++;
        }
        var cellVolume=CellVolume(lattice.CellBounds(new CellIndex(0,0,0)));
        var supports=new BasisSupportEvidence[nodes.Count];
        for(var id=0;id<nodes.Count;id++)
        {
            var key=nodeId.Single(pair=>pair.Value==id).Key;
            var possible=(key.I==0||key.I==lattice.CountX?1:2)*(key.J==0||key.J==lattice.CountY?1:2)*(key.K==0||key.K==lattice.CountZ?1:2);
            var nominal=possible*cellVolume/27d;
            supports[id]=new(id,nodes[id].Position,physical[id],nominal,nominal==0?0:physical[id]/nominal,incidents[id]);
        }
        var rootCells=active.Where(item=>item.Plan.IntegratedVolume/CellVolume(item.Cell.Bounds)>=.5).Select(item=>item.Cell).OrderBy(c=>c.Index.K).ThenBy(c=>c.Index.J).ThenBy(c=>c.Index.I).ToArray();
        var rootNodeIds=rootCells.SelectMany(cell=>CellKeys(cell.Index,nodeOffsets)).Where(nodeId.ContainsKey).Select(key=>nodeId[key]).ToHashSet();
        var engine=new JudgmentEngine<BasisContext>();var extensions=new Dictionary<int,NodeExtension>();var treatments=new List<BasisTreatmentEvidence>(nodes.Count);var calls=0;
        foreach(var evidence in supports)
        {
            var sourceKey=nodeId.Single(pair=>pair.Value==evidence.NodeId).Key;
            var root=FindRoot(sourceKey,rootCells);
            var context=new BasisContext(evidence,root is not null,rootNodeIds.Contains(evidence.NodeId));
            var candidates=new[]
            {
                new JudgmentCandidate<BasisContext>("Ordinary",_=>true,c=>c.HasFullCellSupport?2:OrdinaryIntercept+SupportRiskWeight*Clamp01(c.Evidence.NormalizedSupport),TieBreakerPriority:0),
                new JudgmentCandidate<BasisContext>("Aggregated",c=>c.HasRoot&&!c.HasFullCellSupport,c=>AggregateIntercept-AggregationSetupCost,c=>c.HasFullCellSupport?"A well-supported root cell already provides independent support.":"No deterministic well-supported root cell is available in the connected Cartesian support neighborhood.",1)
            };
            var result=engine.Evaluate(context,candidates);calls++;
            var selected=result.Selection!.Value;var kind=selected.Candidate.Name=="Aggregated"?ImmersedBasisTreatmentKind.Aggregated:ImmersedBasisTreatmentKind.Ordinary;
            IReadOnlyDictionary<int,double> weights=new Dictionary<int,double>();CellIndex? rootIndex=null;
            if(kind==ImmersedBasisTreatmentKind.Aggregated&&root is not null)
            {
                var rootCell=root.Value;rootIndex=rootCell.Index;var natural=Natural(rootCell.Bounds,nodes[evidence.NodeId].Position);var shape=Shape(natural.X,natural.Y,natural.Z,naturalSigns);
                var map=new SortedDictionary<int,double>();var rootIds=CellKeys(rootCell.Index,nodeOffsets).Select(key=>nodeId[key]).ToArray();
                for(var n=0;n<8;n++)if(double.Abs(shape[n])>1e-14)map[rootIds[n]]=shape[n];
                weights=map;extensions[evidence.NodeId]=new(evidence.NodeId,rootCell.Index,map);
            }
            var rejected=context.HasFullCellSupport?["Aggregated: A well-supported root cell already provides independent support."]:!context.HasRoot?["Aggregated: No deterministic well-supported root cell is available in the connected Cartesian support neighborhood."]:Array.Empty<string>();
            var utilities=new SortedDictionary<string,double>{{"Ordinary",context.HasFullCellSupport?2:Clamp01(evidence.NormalizedSupport)}};if(context.HasRoot&&!context.HasFullCellSupport)utilities["Aggregated"]=AggregateIntercept-AggregationSetupCost;
            treatments.Add(new(evidence.NodeId,kind,selected.Score,new SortedDictionary<string,double>{{"normalizedSupport",evidence.NormalizedSupport},{"conditioningRisk",1-Clamp01(evidence.NormalizedSupport)},{"estimatedSetupCost",kind==ImmersedBasisTreatmentKind.Aggregated?AggregationSetupCost:0}},rejected,rootIndex,weights,utilities));
        }
        var retained=Enumerable.Range(0,nodes.Count).Where(id=>!extensions.ContainsKey(id)).ToArray();var effective=retained.Select((raw,index)=>(raw,index)).ToDictionary(x=>x.raw,x=>x.index);
        var mappings=new Dictionary<int,IReadOnlyList<(int,double)>>();
        foreach(var id in retained)mappings[id]=[(effective[id],1d)];
        foreach(var extension in extensions.Values)
        {
            var combined=new SortedDictionary<int,double>();
            foreach(var pair in extension.Weights)
            {
                // Roots are full-cell nodes and therefore retained by admission.
                var target=effective[pair.Key];combined[target]=combined.GetValueOrDefault(target)+pair.Value;
            }
            mappings[extension.SourceNodeId]=combined.Select(pair=>(pair.Key,pair.Value)).ToArray();
        }
        return new(supports,treatments,extensions,mappings,retained.Length,calls,supports.Count(s=>s.NormalizedSupport<FixedControlThreshold&&!rootNodeIds.Contains(s.NodeId)&&FindRoot(nodeId.Single(p=>p.Value==s.NodeId).Key,rootCells) is not null));
    }

    internal static string Hash(BasisLoweringPlan basis,IReadOnlyList<BoundaryEnforcementEvidence> boundaries)
    {
        var text=new StringBuilder("m5c-v1|");
        foreach(var t in basis.Treatments)text.Append(t.SourceNodeId).Append(':').Append(t.Treatment).Append(':').Append(t.Utility.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).Append(':').Append(t.RootCell).Append('|');
        foreach(var b in boundaries)text.Append(b.ConstraintId).Append(':').Append(b.Enforcement).Append(':').Append(b.Utility.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).Append('|');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private sealed record BasisContext(BasisSupportEvidence Evidence,bool HasRoot,bool HasFullCellSupport);
    private static ContinuumCell? FindRoot((int I,int J,int K) source,IReadOnlyList<ContinuumCell> roots)
    {
        foreach(var cell in roots.OrderBy(cell=>Distance(source,cell.Index)).ThenBy(cell=>cell.Index.K).ThenBy(cell=>cell.Index.J).ThenBy(cell=>cell.Index.I))
            if(Distance(source,cell.Index)<=3)return cell;
        return null;
    }
    private static int Distance((int I,int J,int K) node,CellIndex cell)
    {
        var di=node.I<cell.I?cell.I-node.I:node.I>cell.I+1?node.I-(cell.I+1):0;
        var dj=node.J<cell.J?cell.J-node.J:node.J>cell.J+1?node.J-(cell.J+1):0;
        var dk=node.K<cell.K?cell.K-node.K:node.K>cell.K+1?node.K-(cell.K+1):0;return di+dj+dk;
    }
    private static (double X,double Y,double Z) Natural(BoundingBox3D b,Point3D p)=>(2*(p.X-b.Min.X)/(b.Max.X-b.Min.X)-1,2*(p.Y-b.Min.Y)/(b.Max.Y-b.Min.Y)-1,2*(p.Z-b.Min.Z)/(b.Max.Z-b.Min.Z)-1);
    internal static double[] Shape(double xi,double eta,double zeta,IReadOnlyList<(double Xi,double Eta,double Zeta)> signs)
    {var values=new double[8];for(var n=0;n<8;n++){var s=signs[n];values[n]=.125*(1+s.Xi*xi)*(1+s.Eta*eta)*(1+s.Zeta*zeta);}return values;}
    private static IEnumerable<(int I,int J,int K)> CellKeys(CellIndex c,IReadOnlyList<(int X,int Y,int Z)> offsets)=>offsets.Select(o=>(c.I+o.X,c.J+o.Y,c.K+o.Z));
    private static double CellVolume(BoundingBox3D b)=>(b.Max.X-b.Min.X)*(b.Max.Y-b.Min.Y)*(b.Max.Z-b.Min.Z);
    private static double Clamp01(double x)=>double.Min(1,double.Max(0,x));
}
