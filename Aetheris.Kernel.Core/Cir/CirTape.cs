using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Cir;

public enum CirTapeOpCode
{
    EvalBox,
    EvalCylinder,
    EvalSphere,
    Min,
    Max,
    Neg,
    EvalTransform,
}

public readonly record struct CirTapeInstruction(
    CirTapeOpCode OpCode,
    int DestSlot,
    int InputA,
    int InputB,
    int PayloadIndex,
    CirNodeKind SourceKind,
    int LoweringIndex);

public readonly record struct CirTapeBoxPayload(double Width, double Height, double Depth);
public readonly record struct CirTapeCylinderPayload(double Radius, double Height);
public readonly record struct CirTapeSpherePayload(double Radius);
public readonly record struct CirTapeTransformPayload(Transform3D InverseTransform);
public readonly record struct CirTapeTransformEvalPayload(CirNode Child, Transform3D InverseTransform);

/// <summary>
/// Linear MIR/runtime representation for CIR point evaluation.
/// During transition, this is the intended execution form while <see cref="CirNode"/> remains the semantic builder/oracle.
/// </summary>
public sealed class CirTape
{
    public CirTape(
        IReadOnlyList<CirTapeInstruction> instructions,
        IReadOnlyList<CirTapeBoxPayload> boxes,
        IReadOnlyList<CirTapeCylinderPayload> cylinders,
        IReadOnlyList<CirTapeSpherePayload> spheres,
        IReadOnlyList<CirTapeTransformPayload> transforms,
        IReadOnlyList<CirTapeTransformEvalPayload> transformEvals,
        int outputSlot,
        int slotCount)
    {
        Instructions = instructions;
        BoxPayloads = boxes;
        CylinderPayloads = cylinders;
        SpherePayloads = spheres;
        TransformPayloads = transforms;
        TransformEvalPayloads = transformEvals;
        OutputSlot = outputSlot;
        SlotCount = slotCount;
    }

    public IReadOnlyList<CirTapeInstruction> Instructions { get; }
    public IReadOnlyList<CirTapeBoxPayload> BoxPayloads { get; }
    public IReadOnlyList<CirTapeCylinderPayload> CylinderPayloads { get; }
    public IReadOnlyList<CirTapeSpherePayload> SpherePayloads { get; }
    public IReadOnlyList<CirTapeTransformPayload> TransformPayloads { get; }
    public IReadOnlyList<CirTapeTransformEvalPayload> TransformEvalPayloads { get; }
    public int OutputSlot { get; }
    public int SlotCount { get; }

    public double Evaluate(Point3D point)
    {
        var slots = new double[SlotCount];

        foreach (var instruction in Instructions)
        {
            switch (instruction.OpCode)
            {
                case CirTapeOpCode.EvalBox:
                {
                    var payload = BoxPayloads[instruction.PayloadIndex];
                    slots[instruction.DestSlot] = EvaluateBox(point, payload);
                    break;
                }
                case CirTapeOpCode.EvalCylinder:
                {
                    var payload = CylinderPayloads[instruction.PayloadIndex];
                    slots[instruction.DestSlot] = EvaluateCylinder(point, payload);
                    break;
                }
                case CirTapeOpCode.EvalSphere:
                {
                    var payload = SpherePayloads[instruction.PayloadIndex];
                    slots[instruction.DestSlot] = EvaluateSphere(point, payload);
                    break;
                }
                case CirTapeOpCode.Min:
                    slots[instruction.DestSlot] = double.Min(slots[instruction.InputA], slots[instruction.InputB]);
                    break;
                case CirTapeOpCode.Max:
                    slots[instruction.DestSlot] = double.Max(slots[instruction.InputA], slots[instruction.InputB]);
                    break;
                case CirTapeOpCode.Neg:
                    slots[instruction.DestSlot] = -slots[instruction.InputA];
                    break;
                case CirTapeOpCode.EvalTransform:
                {
                    var payload = TransformEvalPayloads[instruction.PayloadIndex];
                    slots[instruction.DestSlot] = payload.Child.Evaluate(payload.InverseTransform.Apply(point));
                    break;
                }
                default:
                    throw new InvalidOperationException($"Unsupported CIR tape opcode: {instruction.OpCode}.");
            }
        }

        return slots[OutputSlot];
    }

    private static double EvaluateBox(Point3D point, CirTapeBoxPayload payload)
    {
        var hx = payload.Width * 0.5d;
        var hy = payload.Height * 0.5d;
        var hz = payload.Depth * 0.5d;
        var dx = double.Abs(point.X) - hx;
        var dy = double.Abs(point.Y) - hy;
        var dz = double.Abs(point.Z) - hz;
        var outsideX = double.Max(dx, 0d);
        var outsideY = double.Max(dy, 0d);
        var outsideZ = double.Max(dz, 0d);
        var outside = double.Sqrt((outsideX * outsideX) + (outsideY * outsideY) + (outsideZ * outsideZ));
        var inside = double.Min(double.Max(dx, double.Max(dy, dz)), 0d);
        return outside + inside;
    }

    private static double EvaluateCylinder(Point3D point, CirTapeCylinderPayload payload)
    {
        var radial = double.Sqrt((point.X * point.X) + (point.Y * point.Y));
        var dr = radial - payload.Radius;
        var dz = double.Abs(point.Z) - (payload.Height * 0.5d);
        var outsideR = double.Max(dr, 0d);
        var outsideZ = double.Max(dz, 0d);
        var outside = double.Sqrt((outsideR * outsideR) + (outsideZ * outsideZ));
        var inside = double.Min(double.Max(dr, dz), 0d);
        return outside + inside;
    }

    private static double EvaluateSphere(Point3D point, CirTapeSpherePayload payload) =>
        double.Sqrt((point.X * point.X) + (point.Y * point.Y) + (point.Z * point.Z)) - payload.Radius;
}

public static class CirTapeLowerer
{
    /// <summary>
    /// Deterministically lowers semantic <see cref="CirNode"/> trees into runtime <see cref="CirTape"/>.
    /// </summary>
    public static CirTape Lower(CirNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var state = new LoweringState();
        var outputSlot = LowerNode(node, state);

        return new CirTape(
            state.Instructions,
            state.BoxPayloads,
            state.CylinderPayloads,
            state.SpherePayloads,
            state.TransformPayloads,
            state.TransformEvalPayloads,
            outputSlot,
            state.NextSlot);
    }

    private static int LowerNode(CirNode node, LoweringState state)
    {
        switch (node)
        {
            case CirBoxNode box:
            {
                var payloadIndex = state.BoxPayloads.Count;
                state.BoxPayloads.Add(new CirTapeBoxPayload(box.Width, box.Height, box.Depth));
                return state.Emit(CirTapeOpCode.EvalBox, -1, -1, payloadIndex, node.Kind);
            }
            case CirCylinderNode cylinder:
            {
                var payloadIndex = state.CylinderPayloads.Count;
                state.CylinderPayloads.Add(new CirTapeCylinderPayload(cylinder.Radius, cylinder.Height));
                return state.Emit(CirTapeOpCode.EvalCylinder, -1, -1, payloadIndex, node.Kind);
            }
            case CirSphereNode sphere:
            {
                var payloadIndex = state.SpherePayloads.Count;
                state.SpherePayloads.Add(new CirTapeSpherePayload(sphere.Radius));
                return state.Emit(CirTapeOpCode.EvalSphere, -1, -1, payloadIndex, node.Kind);
            }
            case CirUnionNode union:
            {
                var left = LowerNode(union.Left, state);
                var right = LowerNode(union.Right, state);
                return state.Emit(CirTapeOpCode.Min, left, right, -1, node.Kind);
            }
            case CirSubtractNode subtract:
            {
                var left = LowerNode(subtract.Left, state);
                var right = LowerNode(subtract.Right, state);
                var negRight = state.Emit(CirTapeOpCode.Neg, right, -1, -1, node.Kind);
                return state.Emit(CirTapeOpCode.Max, left, negRight, -1, node.Kind);
            }
            case CirIntersectNode intersect:
            {
                var left = LowerNode(intersect.Left, state);
                var right = LowerNode(intersect.Right, state);
                return state.Emit(CirTapeOpCode.Max, left, right, -1, node.Kind);
            }
            case CirTransformNode transform:
            {
                var payloadIndex = state.TransformEvalPayloads.Count;
                // CIR-E1 transitional caveat: transform execution keeps a child-node reference and
                // performs recursive child evaluation. E2 is expected to flatten this into pure tape ops.
                state.TransformEvalPayloads.Add(new CirTapeTransformEvalPayload(transform.Child, transform.Transform.Inverse()));
                return state.Emit(CirTapeOpCode.EvalTransform, -1, -1, payloadIndex, node.Kind);
            }
            default:
                throw new InvalidOperationException($"Unsupported CIR node kind for tape lowering: {node.Kind}.");
        }
    }

    private sealed class LoweringState
    {
        public List<CirTapeInstruction> Instructions { get; } = new();
        public List<CirTapeBoxPayload> BoxPayloads { get; } = new();
        public List<CirTapeCylinderPayload> CylinderPayloads { get; } = new();
        public List<CirTapeSpherePayload> SpherePayloads { get; } = new();
        public List<CirTapeTransformPayload> TransformPayloads { get; } = new();
        public List<CirTapeTransformEvalPayload> TransformEvalPayloads { get; } = new();
        public int NextSlot { get; private set; }

        public int Emit(CirTapeOpCode opCode, int inputA, int inputB, int payloadIndex, CirNodeKind sourceKind)
        {
            var destSlot = NextSlot++;
            Instructions.Add(new CirTapeInstruction(opCode, destSlot, inputA, inputB, payloadIndex, sourceKind, Instructions.Count));
            return destSlot;
        }
    }
}
