using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Cir;

public enum CirTapeOpCode
{
    EvalBox,
    EvalCylinder,
    EvalSphere,
    Min,
    Max,
    Neg,
}

public readonly record struct CirTapeInstruction(
    CirTapeOpCode OpCode,
    int DestSlot,
    int InputA,
    int InputB,
    int PayloadIndex,
    CirNodeKind SourceKind,
    int LoweringIndex);

public readonly record struct CirTapeBoxPayload(double Width, double Height, double Depth, Transform3D InverseTransform);
public readonly record struct CirTapeCylinderPayload(double Radius, double Height, Transform3D InverseTransform);
public readonly record struct CirTapeSpherePayload(double Radius, Transform3D InverseTransform);

public readonly record struct FieldInterval(double MinValue, double MaxValue)
{
    public bool IsDefinitelyInside(ToleranceContext tolerance) => MaxValue < -tolerance.Linear;
    public bool IsDefinitelyOutside(ToleranceContext tolerance) => MinValue > tolerance.Linear;
    public bool IsMixed(ToleranceContext tolerance) => !IsDefinitelyInside(tolerance) && !IsDefinitelyOutside(tolerance);
}

public enum CirRegionClassification
{
    Inside,
    Outside,
    Mixed,
}

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
        int outputSlot,
        int slotCount)
    {
        Instructions = instructions;
        BoxPayloads = boxes;
        CylinderPayloads = cylinders;
        SpherePayloads = spheres;
        OutputSlot = outputSlot;
        SlotCount = slotCount;
    }

    public IReadOnlyList<CirTapeInstruction> Instructions { get; }
    public IReadOnlyList<CirTapeBoxPayload> BoxPayloads { get; }
    public IReadOnlyList<CirTapeCylinderPayload> CylinderPayloads { get; }
    public IReadOnlyList<CirTapeSpherePayload> SpherePayloads { get; }
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
                default:
                    throw new InvalidOperationException($"Unsupported CIR tape opcode: {instruction.OpCode}.");
            }
        }

        return slots[OutputSlot];
    }

    public FieldInterval EvaluateInterval(CirBounds region)
    {
        var slots = new FieldInterval[SlotCount];

        foreach (var instruction in Instructions)
        {
            switch (instruction.OpCode)
            {
                case CirTapeOpCode.EvalBox:
                    slots[instruction.DestSlot] = EvaluateBoxInterval(region, BoxPayloads[instruction.PayloadIndex]);
                    break;
                case CirTapeOpCode.EvalCylinder:
                    slots[instruction.DestSlot] = EvaluateCylinderInterval(region, CylinderPayloads[instruction.PayloadIndex]);
                    break;
                case CirTapeOpCode.EvalSphere:
                    slots[instruction.DestSlot] = EvaluateSphereInterval(region, SpherePayloads[instruction.PayloadIndex]);
                    break;
                case CirTapeOpCode.Min:
                {
                    var a = slots[instruction.InputA];
                    var b = slots[instruction.InputB];
                    slots[instruction.DestSlot] = new FieldInterval(double.Min(a.MinValue, b.MinValue), double.Min(a.MaxValue, b.MaxValue));
                    break;
                }
                case CirTapeOpCode.Max:
                {
                    var a = slots[instruction.InputA];
                    var b = slots[instruction.InputB];
                    slots[instruction.DestSlot] = new FieldInterval(double.Max(a.MinValue, b.MinValue), double.Max(a.MaxValue, b.MaxValue));
                    break;
                }
                case CirTapeOpCode.Neg:
                {
                    var a = slots[instruction.InputA];
                    slots[instruction.DestSlot] = new FieldInterval(-a.MaxValue, -a.MinValue);
                    break;
                }
                default:
                    throw new InvalidOperationException($"Unsupported CIR tape opcode: {instruction.OpCode}.");
            }
        }

        return slots[OutputSlot];
    }

    public CirRegionClassification ClassifyRegion(CirBounds region, ToleranceContext tolerance)
    {
        var interval = EvaluateInterval(region);
        if (interval.IsDefinitelyInside(tolerance))
        {
            return CirRegionClassification.Inside;
        }

        if (interval.IsDefinitelyOutside(tolerance))
        {
            return CirRegionClassification.Outside;
        }

        return CirRegionClassification.Mixed;
    }

    private static double EvaluateBox(Point3D point, CirTapeBoxPayload payload)
    {
        point = payload.InverseTransform.Apply(point);
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
        point = payload.InverseTransform.Apply(point);
        var radial = double.Sqrt((point.X * point.X) + (point.Y * point.Y));
        var dr = radial - payload.Radius;
        var dz = double.Abs(point.Z) - (payload.Height * 0.5d);
        var outsideR = double.Max(dr, 0d);
        var outsideZ = double.Max(dz, 0d);
        var outside = double.Sqrt((outsideR * outsideR) + (outsideZ * outsideZ));
        var inside = double.Min(double.Max(dr, dz), 0d);
        return outside + inside;
    }

    private static double EvaluateSphere(Point3D point, CirTapeSpherePayload payload)
    {
        point = payload.InverseTransform.Apply(point);
        return double.Sqrt((point.X * point.X) + (point.Y * point.Y) + (point.Z * point.Z)) - payload.Radius;
    }

    private static FieldInterval EvaluateBoxInterval(CirBounds region, CirTapeBoxPayload payload)
    {
        var local = TransformBounds(region, payload.InverseTransform);
        var hx = payload.Width * 0.5d;
        var hy = payload.Height * 0.5d;
        var hz = payload.Depth * 0.5d;
        return EvaluateBoundedBoxSdfInterval(local, hx, hy, hz);
    }

    private static FieldInterval EvaluateCylinderInterval(CirBounds region, CirTapeCylinderPayload payload)
    {
        var local = TransformBounds(region, payload.InverseTransform);
        var dr = RadiusInterval(local, payload.Radius);
        var dz = AxisAbsDistanceInterval(local.Min.Z, local.Max.Z, payload.Height * 0.5d);
        return CombineExtrudedSdf(dr, dz);
    }

    private static FieldInterval EvaluateSphereInterval(CirBounds region, CirTapeSpherePayload payload)
    {
        var local = TransformBounds(region, payload.InverseTransform);
        var distanceMin = MinDistanceToAabbOrigin(local);
        var distanceMax = MaxDistanceToAabbOrigin(local);
        return new FieldInterval(distanceMin - payload.Radius, distanceMax - payload.Radius);
    }

    private static CirBounds TransformBounds(CirBounds bounds, Transform3D transform)
    {
        var corners = GetCorners(bounds);
        var transformed = corners.Select(transform.Apply).ToArray();
        return new CirBounds(
            new Point3D(transformed.Min(p => p.X), transformed.Min(p => p.Y), transformed.Min(p => p.Z)),
            new Point3D(transformed.Max(p => p.X), transformed.Max(p => p.Y), transformed.Max(p => p.Z)));
    }

    private static Point3D[] GetCorners(CirBounds b) =>
    [
        new Point3D(b.Min.X, b.Min.Y, b.Min.Z),
        new Point3D(b.Min.X, b.Min.Y, b.Max.Z),
        new Point3D(b.Min.X, b.Max.Y, b.Min.Z),
        new Point3D(b.Min.X, b.Max.Y, b.Max.Z),
        new Point3D(b.Max.X, b.Min.Y, b.Min.Z),
        new Point3D(b.Max.X, b.Min.Y, b.Max.Z),
        new Point3D(b.Max.X, b.Max.Y, b.Min.Z),
        new Point3D(b.Max.X, b.Max.Y, b.Max.Z),
    ];

    private static FieldInterval EvaluateBoundedBoxSdfInterval(CirBounds local, double hx, double hy, double hz)
    {
        var dx = AxisAbsDistanceInterval(local.Min.X, local.Max.X, hx);
        var dy = AxisAbsDistanceInterval(local.Min.Y, local.Max.Y, hy);
        var dz = AxisAbsDistanceInterval(local.Min.Z, local.Max.Z, hz);
        return CombineExtrudedSdf(dx, dy, dz);
    }

    private static FieldInterval CombineExtrudedSdf(params FieldInterval[] components)
    {
        var outsideTerms = components.Select(c => new FieldInterval(double.Max(c.MinValue, 0d), double.Max(c.MaxValue, 0d))).ToArray();
        var outsideMin = double.Sqrt(outsideTerms.Sum(t => t.MinValue * t.MinValue));
        var outsideMax = double.Sqrt(outsideTerms.Sum(t => t.MaxValue * t.MaxValue));
        var insideMin = double.Min(components.Max(c => c.MinValue), 0d);
        var insideMax = double.Min(components.Max(c => c.MaxValue), 0d);
        return new FieldInterval(outsideMin + insideMin, outsideMax + insideMax);
    }

    private static FieldInterval RadiusInterval(CirBounds bounds, double radius)
    {
        var minR = MinDistanceToRectOrigin(bounds.Min.X, bounds.Max.X, bounds.Min.Y, bounds.Max.Y);
        var maxR = MaxDistanceToRectOrigin(bounds.Min.X, bounds.Max.X, bounds.Min.Y, bounds.Max.Y);
        return new FieldInterval(minR - radius, maxR - radius);
    }

    private static FieldInterval AxisAbsDistanceInterval(double min, double max, double halfExtent)
    {
        var absMin = MinAbsInInterval(min, max);
        var absMax = MaxAbsInInterval(min, max);
        return new FieldInterval(absMin - halfExtent, absMax - halfExtent);
    }

    private static double MinAbsInInterval(double min, double max)
        => (min <= 0d && max >= 0d) ? 0d : double.Min(double.Abs(min), double.Abs(max));

    private static double MaxAbsInInterval(double min, double max)
        => double.Max(double.Abs(min), double.Abs(max));

    private static double MinDistanceToAabbOrigin(CirBounds bounds)
        => double.Sqrt((MinAbsInInterval(bounds.Min.X, bounds.Max.X) * MinAbsInInterval(bounds.Min.X, bounds.Max.X))
            + (MinAbsInInterval(bounds.Min.Y, bounds.Max.Y) * MinAbsInInterval(bounds.Min.Y, bounds.Max.Y))
            + (MinAbsInInterval(bounds.Min.Z, bounds.Max.Z) * MinAbsInInterval(bounds.Min.Z, bounds.Max.Z)));

    private static double MaxDistanceToAabbOrigin(CirBounds bounds)
        => double.Sqrt((MaxAbsInInterval(bounds.Min.X, bounds.Max.X) * MaxAbsInInterval(bounds.Min.X, bounds.Max.X))
            + (MaxAbsInInterval(bounds.Min.Y, bounds.Max.Y) * MaxAbsInInterval(bounds.Min.Y, bounds.Max.Y))
            + (MaxAbsInInterval(bounds.Min.Z, bounds.Max.Z) * MaxAbsInInterval(bounds.Min.Z, bounds.Max.Z)));

    private static double MinDistanceToRectOrigin(double minX, double maxX, double minY, double maxY)
    {
        var minAbsX = MinAbsInInterval(minX, maxX);
        var minAbsY = MinAbsInInterval(minY, maxY);
        return double.Sqrt((minAbsX * minAbsX) + (minAbsY * minAbsY));
    }

    private static double MaxDistanceToRectOrigin(double minX, double maxX, double minY, double maxY)
    {
        var maxAbsX = MaxAbsInInterval(minX, maxX);
        var maxAbsY = MaxAbsInInterval(minY, maxY);
        return double.Sqrt((maxAbsX * maxAbsX) + (maxAbsY * maxAbsY));
    }
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
        var outputSlot = LowerNode(node, Transform3D.Identity, state);

        return new CirTape(
            state.Instructions,
            state.BoxPayloads,
            state.CylinderPayloads,
            state.SpherePayloads,
            outputSlot,
            state.NextSlot);
    }

    private static int LowerNode(CirNode node, Transform3D accumulatedInverse, LoweringState state)
    {
        switch (node)
        {
            case CirBoxNode box:
            {
                var payloadIndex = state.BoxPayloads.Count;
                state.BoxPayloads.Add(new CirTapeBoxPayload(box.Width, box.Height, box.Depth, accumulatedInverse));
                return state.Emit(CirTapeOpCode.EvalBox, -1, -1, payloadIndex, node.Kind);
            }
            case CirCylinderNode cylinder:
            {
                var payloadIndex = state.CylinderPayloads.Count;
                state.CylinderPayloads.Add(new CirTapeCylinderPayload(cylinder.Radius, cylinder.Height, accumulatedInverse));
                return state.Emit(CirTapeOpCode.EvalCylinder, -1, -1, payloadIndex, node.Kind);
            }
            case CirSphereNode sphere:
            {
                var payloadIndex = state.SpherePayloads.Count;
                state.SpherePayloads.Add(new CirTapeSpherePayload(sphere.Radius, accumulatedInverse));
                return state.Emit(CirTapeOpCode.EvalSphere, -1, -1, payloadIndex, node.Kind);
            }
            case CirUnionNode union:
            {
                var left = LowerNode(union.Left, accumulatedInverse, state);
                var right = LowerNode(union.Right, accumulatedInverse, state);
                return state.Emit(CirTapeOpCode.Min, left, right, -1, node.Kind);
            }
            case CirSubtractNode subtract:
            {
                var left = LowerNode(subtract.Left, accumulatedInverse, state);
                var right = LowerNode(subtract.Right, accumulatedInverse, state);
                var negRight = state.Emit(CirTapeOpCode.Neg, right, -1, -1, node.Kind);
                return state.Emit(CirTapeOpCode.Max, left, negRight, -1, node.Kind);
            }
            case CirIntersectNode intersect:
            {
                var left = LowerNode(intersect.Left, accumulatedInverse, state);
                var right = LowerNode(intersect.Right, accumulatedInverse, state);
                return state.Emit(CirTapeOpCode.Max, left, right, -1, node.Kind);
            }
            case CirTransformNode transform:
            {
                var nextAccumulatedInverse = accumulatedInverse * transform.Transform.Inverse();
                return LowerNode(transform.Child, nextAccumulatedInverse, state);
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
        public int NextSlot { get; private set; }

        public int Emit(CirTapeOpCode opCode, int inputA, int inputB, int payloadIndex, CirNodeKind sourceKind)
        {
            var destSlot = NextSlot++;
            Instructions.Add(new CirTapeInstruction(opCode, destSlot, inputA, inputB, payloadIndex, sourceKind, Instructions.Count));
            return destSlot;
        }
    }
}
