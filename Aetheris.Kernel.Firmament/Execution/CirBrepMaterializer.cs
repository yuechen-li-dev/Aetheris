using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Firmament.Execution;

internal static class CirBrepMaterializer
{
    internal const string BoxMinusCylinderPattern = "subtract(box,cylinder)";
    internal const string BoxMinusBoxPattern = "subtract(box,box)";
    internal const string BoxMinusTorusPattern = "subtract(box,torus)";
    private static readonly JudgmentEngine<CirBrepMaterializerContext> Engine = new();
    private static readonly ICirBrepMaterializerStrategy[] Registry = [new SubtractBoxCylinderStrategy(), new SubtractBoxBoxStrategy(), new SubtractBoxTorusUnsupportedStrategy()];

    internal static CirBrepMaterializationResult TryMaterialize(CirNode root) => TryMaterialize(new CirBrepMaterializerContext(root, null));

    internal static CirBrepMaterializationResult TryMaterialize(CirBrepMaterializerContext context)
    {
        var candidates = Registry.Select((s, i) => new JudgmentCandidate<CirBrepMaterializerContext>(s.Name, s.IsAdmissible, s.Score, s.RejectionReason, i)).ToArray();
        var judgment = Engine.Evaluate(context, candidates);
        if (!judgment.IsSuccess)
        {
            var message = "No CIR→BRep materializer strategy matched. " + string.Join("; ", judgment.Rejections.Select(r => $"{r.CandidateName}: {r.Reason}"));
            return new(false, null, "unsupported", "no-strategy-matched", [], message, null, judgment.Rejections);
        }

        var selected = judgment.Selection!.Value.Candidate.Name;
        var result = Registry.Single(s => s.Name == selected).Materialize(context);
        return result with { SelectedStrategy = selected, StrategyRejections = judgment.Rejections };
    }

    internal sealed record CirBrepMaterializerContext(CirNode Root, NativeGeometryReplayLog? ReplayLog)
    {
        internal NativeGeometryReplayOperation? LatestOperation => ReplayLog?.Operations.LastOrDefault(op => op.OperationKind.StartsWith("boolean:subtract", StringComparison.OrdinalIgnoreCase)) ?? ReplayLog?.Operations.LastOrDefault();
    }

    private interface ICirBrepMaterializerStrategy
    {
        string Name { get; }
        bool IsAdmissible(CirBrepMaterializerContext context);
        string RejectionReason(CirBrepMaterializerContext context);
        double Score(CirBrepMaterializerContext context);
        CirBrepMaterializationResult Materialize(CirBrepMaterializerContext context);
    }

    private sealed class SubtractBoxCylinderStrategy : ICirBrepMaterializerStrategy
    {
        public string Name => "subtract_box_cylinder";
        public bool IsAdmissible(CirBrepMaterializerContext context) => TryMatch(context, true, out _, out _);
        public string RejectionReason(CirBrepMaterializerContext context) => TryMatch(context, true, out _, out var reason) ? "admissible" : reason;
        public double Score(CirBrepMaterializerContext context) => context.ReplayLog is null ? 1d : 2d;

        public CirBrepMaterializationResult Materialize(CirBrepMaterializerContext context)
        {
            if (!TryMatch(context, true, out var m, out var reason)) return new(false, null, BoxMinusCylinderPattern, "strategy-no-longer-admissible", [], reason, Name, []);
            var b = BrepPrimitives.CreateBox(m!.LeftBox.Width, m.LeftBox.Height, m.LeftBox.Depth);
            var c = BrepPrimitives.CreateCylinder(m.Cylinder!.Radius, m.Cylinder.Height);
            if (!b.IsSuccess) return Failed(BoxMinusCylinderPattern, "Failed to create BRep box primitive.", b.Diagnostics);
            if (!c.IsSuccess) return Failed(BoxMinusCylinderPattern, "Failed to create BRep cylinder primitive.", c.Diagnostics);
            var s = BrepBoolean.Subtract(TranslateBody(b.Value, m.LeftTranslation), TranslateBody(c.Value, m.RightTranslation));
            return s.IsSuccess ? new(true, s.Value, BoxMinusCylinderPattern, null, [], "matched-box-minus-cylinder", Name, []) : Failed(BoxMinusCylinderPattern, "Failed to boolean subtract box/cylinder during CIR rematerialization.", s.Diagnostics);
        }
    }

    private sealed class SubtractBoxBoxStrategy : ICirBrepMaterializerStrategy
    {
        public string Name => "subtract_box_box";
        public bool IsAdmissible(CirBrepMaterializerContext context) => TryMatch(context, false, out _, out _);
        public string RejectionReason(CirBrepMaterializerContext context) => TryMatch(context, false, out _, out var reason) ? "admissible" : reason;
        public double Score(CirBrepMaterializerContext context) => context.ReplayLog is null ? 1d : 2d;
        public CirBrepMaterializationResult Materialize(CirBrepMaterializerContext context)
        {
            if (!TryMatch(context, false, out var m, out var reason)) return new(false, null, BoxMinusBoxPattern, "strategy-no-longer-admissible", [], reason, Name, []);
            var l = BrepPrimitives.CreateBox(m!.LeftBox.Width, m.LeftBox.Height, m.LeftBox.Depth);
            var r = BrepPrimitives.CreateBox(m.RightBox!.Width, m.RightBox.Height, m.RightBox.Depth);
            if (!l.IsSuccess) return Failed(BoxMinusBoxPattern, "Failed to create lhs BRep box primitive.", l.Diagnostics);
            if (!r.IsSuccess) return Failed(BoxMinusBoxPattern, "Failed to create rhs BRep box primitive.", r.Diagnostics);
            var s = BrepBoolean.Subtract(TranslateBody(l.Value, m.LeftTranslation), TranslateBody(r.Value, m.RightTranslation));
            return s.IsSuccess ? new(true, s.Value, BoxMinusBoxPattern, null, [], "matched-box-minus-box", Name, []) : Failed(BoxMinusBoxPattern, "Failed to boolean subtract box/box during CIR rematerialization.", s.Diagnostics);
        }
    }

    private sealed class SubtractBoxTorusUnsupportedStrategy : ICirBrepMaterializerStrategy
    {
        public string Name => "subtract_box_torus";
        public bool IsAdmissible(CirBrepMaterializerContext context) => TryMatchTorus(context, out _);
        public string RejectionReason(CirBrepMaterializerContext context) => TryMatchTorus(context, out var reason) ? "admissible" : reason;
        public double Score(CirBrepMaterializerContext context) => context.ReplayLog is null ? 0.5d : 3d;
        public CirBrepMaterializationResult Materialize(CirBrepMaterializerContext context)
        {
            if (!TryMatchTorus(context, out var reason)) return new(false, null, BoxMinusTorusPattern, "strategy-no-longer-admissible", [], reason, Name, []);
            return new(false, null, BoxMinusTorusPattern, "materialization-unsupported", [], "Replay-guided pattern subtract(box,torus) recognized, but no exact CIR→BRep torus subtract materializer exists yet.", Name, []);
        }
    }

    private sealed record Match(CirBoxNode LeftBox, Vector3D LeftTranslation, Vector3D RightTranslation, CirCylinderNode? Cylinder, CirBoxNode? RightBox);
    private static bool TryMatch(CirBrepMaterializerContext context, bool cylinder, out Match? match, out string reason)
    {
        match = null;
        if (context.Root is not CirSubtractNode s) { reason = "CIR root is not subtract."; return false; }
        if (!TryUnwrapTranslation(s.Left, out var lnode, out var lt) || lnode is not CirBoxNode lb) { reason = "Subtract lhs must be translated/untranslated box with translation-only transforms."; return false; }
        if (!TryUnwrapTranslation(s.Right, out var rnode, out var rt)) { reason = "Subtract rhs contains unsupported non-translation transform."; return false; }
        if (!ReplayMatches(context, cylinder ? "cylinder" : "box", out reason)) return false;
        if (cylinder && rnode is CirCylinderNode rc) { match = new(lb, lt, rt, rc, null); reason = "matched"; return true; }
        if (!cylinder && rnode is CirBoxNode rb) { match = new(lb, lt, rt, null, rb); reason = "matched"; return true; }
        reason = cylinder ? "Subtract rhs must be translated/untranslated cylinder." : "Subtract rhs must be translated/untranslated box.";
        return false;
    }

    private static bool ReplayMatches(CirBrepMaterializerContext context, string expectedToolKind, out string reason)
    {
        if (context.ReplayLog is null) { reason = "Replay log missing; using CIR tree-only fallback."; return true; }
        var op = context.LatestOperation;
        if (op is null) { reason = "Replay log present but contains no operations."; return false; }
        if (!op.OperationKind.StartsWith("boolean:subtract", StringComparison.OrdinalIgnoreCase)) { reason = $"Replay/CIR mismatch: expected latest replay operation boolean:subtract, got '{op.OperationKind}'."; return false; }
        if (!string.IsNullOrWhiteSpace(op.ToolKind) && !string.Equals(op.ToolKind, expectedToolKind, StringComparison.OrdinalIgnoreCase)) { reason = $"Replay/CIR mismatch: expected replay tool kind '{expectedToolKind}', got '{op.ToolKind}'."; return false; }
        reason = "matched"; return true;
    }

    private static bool TryMatchTorus(CirBrepMaterializerContext context, out string reason)
    {
        if (context.Root is not CirSubtractNode s) { reason = "CIR root is not subtract."; return false; }
        if (!TryUnwrapTranslation(s.Left, out var lnode, out _) || lnode is not CirBoxNode) { reason = "Subtract lhs must be translated/untranslated box with translation-only transforms."; return false; }
        if (!TryUnwrapTranslation(s.Right, out var rnode, out _) || rnode is not CirTorusNode) { reason = "Subtract rhs must be translated/untranslated torus."; return false; }
        return ReplayMatches(context, "torus", out reason);
    }

    private static CirBrepMaterializationResult Failed(string pattern, string message, IReadOnlyList<KernelDiagnostic> d) => new(false, null, pattern, "materialize-failed", d, message, null, []);
    private static BrepBody TranslateBody(BrepBody body, Vector3D t) => t == Vector3D.Zero ? body : FirmamentPrimitiveExecutionTranslation.TranslateBody(body, t);
    private static bool TryUnwrapTranslation(CirNode node, out CirNode unwrapped, out Vector3D translation) { var total = Vector3D.Zero; var cur = node; while (cur is CirTransformNode tr) { if (!TryExtractPureTranslation(tr.Transform, out var local)) { unwrapped = node; translation = Vector3D.Zero; return false; } total += local; cur = tr.Child; } unwrapped = cur; translation = total; return true; }
    private static bool TryExtractPureTranslation(Transform3D transform, out Vector3D translation) { var o = transform.Apply(Point3D.Origin); var x = transform.Apply(new Point3D(1,0,0)); var y = transform.Apply(new Point3D(0,1,0)); var z = transform.Apply(new Point3D(0,0,1)); var eps=1e-9d; if(!NearlyEqual(x-o,new Vector3D(1,0,0),eps)||!NearlyEqual(y-o,new Vector3D(0,1,0),eps)||!NearlyEqual(z-o,new Vector3D(0,0,1),eps)){ translation=Vector3D.Zero; return false;} translation=o-Point3D.Origin; return true; }
    private static bool NearlyEqual(Vector3D l, Vector3D r, double eps) => double.Abs(l.X-r.X)<=eps && double.Abs(l.Y-r.Y)<=eps && double.Abs(l.Z-r.Z)<=eps;
}

internal sealed record CirBrepMaterializationResult(bool IsSuccess, BrepBody? Body, string PatternName, string? UnsupportedReason, IReadOnlyList<KernelDiagnostic> Diagnostics, string Message, string? SelectedStrategy, IReadOnlyList<JudgmentRejection> StrategyRejections);
