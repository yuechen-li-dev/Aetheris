using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Continuum.Backends.Sdf;

public enum SdfTapeOpCode
{
    EvalBox,
    EvalCylinder,
    EvalSphere,
    EvalTorus,
    EvalCone,
    Min,
    Max,
    Neg,
}

public readonly record struct SdfTapeInstruction(
    SdfTapeOpCode OpCode,
    int DestSlot,
    int InputA,
    int InputB,
    int PayloadIndex,
    SdfNodeKind SourceKind,
    int LoweringIndex);

public readonly record struct SdfTapeBoxPayload(double Width, double Height, double Depth, Transform3D InverseTransform);
public readonly record struct SdfTapeCylinderPayload(double Radius, double Height, Transform3D InverseTransform);
public readonly record struct SdfTapeSpherePayload(double Radius, Transform3D InverseTransform);
public readonly record struct SdfTapeTorusPayload(double MajorRadius, double MinorRadius, Transform3D InverseTransform);
public readonly record struct SdfTapeConePayload(double BottomRadius, double TopRadius, double Height, Transform3D InverseTransform);

public readonly record struct FieldInterval(double MinValue, double MaxValue)
{
    public bool IsDefinitelyInside(ToleranceContext tolerance) => MaxValue < -tolerance.Linear;
    public bool IsDefinitelyOutside(ToleranceContext tolerance) => MinValue > tolerance.Linear;
    public bool IsMixed(ToleranceContext tolerance) => !IsDefinitelyInside(tolerance) && !IsDefinitelyOutside(tolerance);
}

public enum SdfRegionClassification
{
    Inside,
    Outside,
    Mixed,
}

/// <summary>
/// Linear MIR/runtime representation for CIR point evaluation.
/// During transition, this is the intended execution form while <see cref="SdfNode"/> remains the semantic builder/oracle.
/// </summary>
public sealed class SdfTape
{
    public SdfTape(
        IReadOnlyList<SdfTapeInstruction> instructions,
        IReadOnlyList<SdfTapeBoxPayload> boxes,
        IReadOnlyList<SdfTapeCylinderPayload> cylinders,
        IReadOnlyList<SdfTapeSpherePayload> spheres,
        IReadOnlyList<SdfTapeTorusPayload> toruses,
        IReadOnlyList<SdfTapeConePayload> cones,
        int outputSlot,
        int slotCount)
    {
        Instructions = instructions;
        BoxPayloads = boxes;
        CylinderPayloads = cylinders;
        SpherePayloads = spheres;
        TorusPayloads = toruses;
        ConePayloads = cones;
        OutputSlot = outputSlot;
        SlotCount = slotCount;
    }

    public IReadOnlyList<SdfTapeInstruction> Instructions { get; }
    public IReadOnlyList<SdfTapeBoxPayload> BoxPayloads { get; }
    public IReadOnlyList<SdfTapeCylinderPayload> CylinderPayloads { get; }
    public IReadOnlyList<SdfTapeSpherePayload> SpherePayloads { get; }
    public IReadOnlyList<SdfTapeTorusPayload> TorusPayloads { get; }
    public IReadOnlyList<SdfTapeConePayload> ConePayloads { get; }
    public int OutputSlot { get; }
    public int SlotCount { get; }

    public double Evaluate(Point3D point)
    {
        var slots = new double[SlotCount];

        foreach (var instruction in Instructions)
        {
            switch (instruction.OpCode)
            {
                case SdfTapeOpCode.EvalBox:
                {
                    var payload = BoxPayloads[instruction.PayloadIndex];
                    slots[instruction.DestSlot] = EvaluateBox(point, payload);
                    break;
                }
                case SdfTapeOpCode.EvalCylinder:
                {
                    var payload = CylinderPayloads[instruction.PayloadIndex];
                    slots[instruction.DestSlot] = EvaluateCylinder(point, payload);
                    break;
                }
                case SdfTapeOpCode.EvalSphere:
                {
                    var payload = SpherePayloads[instruction.PayloadIndex];
                    slots[instruction.DestSlot] = EvaluateSphere(point, payload);
                    break;
                }
                case SdfTapeOpCode.EvalTorus:
                {
                    var payload = TorusPayloads[instruction.PayloadIndex];
                    slots[instruction.DestSlot] = EvaluateTorus(point, payload);
                    break;
                }
                case SdfTapeOpCode.EvalCone:
                {
                    var payload = ConePayloads[instruction.PayloadIndex];
                    slots[instruction.DestSlot] = EvaluateCone(point, payload);
                    break;
                }
                case SdfTapeOpCode.Min:
                    slots[instruction.DestSlot] = double.Min(slots[instruction.InputA], slots[instruction.InputB]);
                    break;
                case SdfTapeOpCode.Max:
                    slots[instruction.DestSlot] = double.Max(slots[instruction.InputA], slots[instruction.InputB]);
                    break;
                case SdfTapeOpCode.Neg:
                    slots[instruction.DestSlot] = -slots[instruction.InputA];
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported CIR tape opcode: {instruction.OpCode}.");
            }
        }

        return slots[OutputSlot];
    }

    public FieldInterval EvaluateInterval(SdfBounds region)
    {
        var slots = new FieldInterval[SlotCount];

        foreach (var instruction in Instructions)
        {
            switch (instruction.OpCode)
            {
                case SdfTapeOpCode.EvalBox:
                    slots[instruction.DestSlot] = EvaluateBoxInterval(region, BoxPayloads[instruction.PayloadIndex]);
                    break;
                case SdfTapeOpCode.EvalCylinder:
                    slots[instruction.DestSlot] = EvaluateCylinderInterval(region, CylinderPayloads[instruction.PayloadIndex]);
                    break;
                case SdfTapeOpCode.EvalSphere:
                    slots[instruction.DestSlot] = EvaluateSphereInterval(region, SpherePayloads[instruction.PayloadIndex]);
                    break;
                case SdfTapeOpCode.EvalTorus:
                    slots[instruction.DestSlot] = EvaluateTorusInterval(region, TorusPayloads[instruction.PayloadIndex]);
                    break;
                case SdfTapeOpCode.EvalCone:
                    slots[instruction.DestSlot] = EvaluateConeInterval(region, ConePayloads[instruction.PayloadIndex]);
                    break;
                case SdfTapeOpCode.Min:
                {
                    var a = slots[instruction.InputA];
                    var b = slots[instruction.InputB];
                    slots[instruction.DestSlot] = new FieldInterval(double.Min(a.MinValue, b.MinValue), double.Min(a.MaxValue, b.MaxValue));
                    break;
                }
                case SdfTapeOpCode.Max:
                {
                    var a = slots[instruction.InputA];
                    var b = slots[instruction.InputB];
                    slots[instruction.DestSlot] = new FieldInterval(double.Max(a.MinValue, b.MinValue), double.Max(a.MaxValue, b.MaxValue));
                    break;
                }
                case SdfTapeOpCode.Neg:
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

    public SdfRegionClassification ClassifyRegion(SdfBounds region, ToleranceContext tolerance)
    {
        var interval = EvaluateInterval(region);
        if (interval.IsDefinitelyInside(tolerance))
        {
            return SdfRegionClassification.Inside;
        }

        if (interval.IsDefinitelyOutside(tolerance))
        {
            return SdfRegionClassification.Outside;
        }

        return SdfRegionClassification.Mixed;
    }

    private static double EvaluateBox(Point3D point, SdfTapeBoxPayload payload)
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

    private static double EvaluateCylinder(Point3D point, SdfTapeCylinderPayload payload)
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

    private static double EvaluateSphere(Point3D point, SdfTapeSpherePayload payload)
    {
        point = payload.InverseTransform.Apply(point);
        return double.Sqrt((point.X * point.X) + (point.Y * point.Y) + (point.Z * point.Z)) - payload.Radius;
    }

    private static double EvaluateTorus(Point3D point, SdfTapeTorusPayload payload)
    {
        point = payload.InverseTransform.Apply(point);
        var radial = double.Sqrt((point.X * point.X) + (point.Y * point.Y));
        var qx = radial - payload.MajorRadius;
        return double.Sqrt((qx * qx) + (point.Z * point.Z)) - payload.MinorRadius;
    }

    private static double EvaluateCone(Point3D point, SdfTapeConePayload payload)
    {
        point = payload.InverseTransform.Apply(point);
        return SdfConeNode.EvaluateFiniteCone(point, payload.BottomRadius, payload.TopRadius, payload.Height);
    }

    private static FieldInterval EvaluateBoxInterval(SdfBounds region, SdfTapeBoxPayload payload)
    {
        var local = TransformBounds(region, payload.InverseTransform);
        var hx = payload.Width * 0.5d;
        var hy = payload.Height * 0.5d;
        var hz = payload.Depth * 0.5d;
        return EvaluateBoundedBoxSdfInterval(local, hx, hy, hz);
    }

    private static FieldInterval EvaluateCylinderInterval(SdfBounds region, SdfTapeCylinderPayload payload)
    {
        var local = TransformBounds(region, payload.InverseTransform);
        var dr = RadiusInterval(local, payload.Radius);
        var dz = AxisAbsDistanceInterval(local.Min.Z, local.Max.Z, payload.Height * 0.5d);
        return CombineExtrudedSdf(dr, dz);
    }

    private static FieldInterval EvaluateSphereInterval(SdfBounds region, SdfTapeSpherePayload payload)
    {
        var local = TransformBounds(region, payload.InverseTransform);
        var distanceMin = MinDistanceToAabbOrigin(local);
        var distanceMax = MaxDistanceToAabbOrigin(local);
        return new FieldInterval(distanceMin - payload.Radius, distanceMax - payload.Radius);
    }

    private static FieldInterval EvaluateTorusInterval(SdfBounds region, SdfTapeTorusPayload payload)
    {
        var local = TransformBounds(region, payload.InverseTransform);
        var radial = RadiusInterval(local, payload.MajorRadius);
        var qMin = MinAbsInInterval(radial.MinValue, radial.MaxValue);
        var qMax = MaxAbsInInterval(radial.MinValue, radial.MaxValue);
        var zMinAbs = MinAbsInInterval(local.Min.Z, local.Max.Z);
        var zMaxAbs = MaxAbsInInterval(local.Min.Z, local.Max.Z);
        var minD = double.Sqrt((qMin * qMin) + (zMinAbs * zMinAbs));
        var maxD = double.Sqrt((qMax * qMax) + (zMaxAbs * zMaxAbs));
        return new FieldInterval(minD - payload.MinorRadius, maxD - payload.MinorRadius);
    }

    private static FieldInterval EvaluateConeInterval(SdfBounds region, SdfTapeConePayload payload)
    {
        var local = TransformBounds(region, payload.InverseTransform);
        var corners = GetCorners(local);
        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        foreach (var c in corners)
        {
            var value = SdfConeNode.EvaluateFiniteCone(c, payload.BottomRadius, payload.TopRadius, payload.Height);
            min = double.Min(min, value);
            max = double.Max(max, value);
        }

        return new FieldInterval(min, max);
    }

    private static SdfBounds TransformBounds(SdfBounds bounds, Transform3D transform)
    {
        var corners = GetCorners(bounds);
        var transformed = corners.Select(transform.Apply).ToArray();
        return new SdfBounds(
            new Point3D(transformed.Min(p => p.X), transformed.Min(p => p.Y), transformed.Min(p => p.Z)),
            new Point3D(transformed.Max(p => p.X), transformed.Max(p => p.Y), transformed.Max(p => p.Z)));
    }

    private static Point3D[] GetCorners(SdfBounds b) =>
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

    private static FieldInterval EvaluateBoundedBoxSdfInterval(SdfBounds local, double hx, double hy, double hz)
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

    private static FieldInterval RadiusInterval(SdfBounds bounds, double radius)
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

    private static double MinDistanceToAabbOrigin(SdfBounds bounds)
        => double.Sqrt((MinAbsInInterval(bounds.Min.X, bounds.Max.X) * MinAbsInInterval(bounds.Min.X, bounds.Max.X))
            + (MinAbsInInterval(bounds.Min.Y, bounds.Max.Y) * MinAbsInInterval(bounds.Min.Y, bounds.Max.Y))
            + (MinAbsInInterval(bounds.Min.Z, bounds.Max.Z) * MinAbsInInterval(bounds.Min.Z, bounds.Max.Z)));

    private static double MaxDistanceToAabbOrigin(SdfBounds bounds)
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

public static class SdfTapeLowerer
{
    /// <summary>
    /// Deterministically lowers semantic <see cref="SdfNode"/> trees into runtime <see cref="SdfTape"/>.
    /// </summary>
    public static SdfTape Lower(SdfNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var state = new LoweringState();
        var outputSlot = LowerNode(node, Transform3D.Identity, state);

        return new SdfTape(
            state.Instructions,
            state.BoxPayloads,
            state.CylinderPayloads,
            state.SpherePayloads,
            state.TorusPayloads,
            state.ConePayloads,
            outputSlot,
            state.NextSlot);
    }

    private static int LowerNode(SdfNode node, Transform3D accumulatedInverse, LoweringState state)
    {
        switch (node)
        {
            case SdfBoxNode box:
            {
                var payloadIndex = state.BoxPayloads.Count;
                state.BoxPayloads.Add(new SdfTapeBoxPayload(box.Width, box.Height, box.Depth, accumulatedInverse));
                return state.Emit(SdfTapeOpCode.EvalBox, -1, -1, payloadIndex, node.Kind);
            }
            case SdfCylinderNode cylinder:
            {
                var payloadIndex = state.CylinderPayloads.Count;
                state.CylinderPayloads.Add(new SdfTapeCylinderPayload(cylinder.Radius, cylinder.Height, accumulatedInverse));
                return state.Emit(SdfTapeOpCode.EvalCylinder, -1, -1, payloadIndex, node.Kind);
            }
            case SdfSphereNode sphere:
            {
                var payloadIndex = state.SpherePayloads.Count;
                state.SpherePayloads.Add(new SdfTapeSpherePayload(sphere.Radius, accumulatedInverse));
                return state.Emit(SdfTapeOpCode.EvalSphere, -1, -1, payloadIndex, node.Kind);
            }
            case SdfTorusNode torus:
            {
                var payloadIndex = state.TorusPayloads.Count;
                state.TorusPayloads.Add(new SdfTapeTorusPayload(torus.MajorRadius, torus.MinorRadius, accumulatedInverse));
                return state.Emit(SdfTapeOpCode.EvalTorus, -1, -1, payloadIndex, node.Kind);
            }
            case SdfConeNode cone:
            {
                var payloadIndex = state.ConePayloads.Count;
                state.ConePayloads.Add(new SdfTapeConePayload(cone.BottomRadius, cone.TopRadius, cone.Height, accumulatedInverse));
                return state.Emit(SdfTapeOpCode.EvalCone, -1, -1, payloadIndex, node.Kind);
            }
            case SdfUnionNode union:
            {
                var left = LowerNode(union.Left, accumulatedInverse, state);
                var right = LowerNode(union.Right, accumulatedInverse, state);
                return state.Emit(SdfTapeOpCode.Min, left, right, -1, node.Kind);
            }
            case SdfSubtractNode subtract:
            {
                var left = LowerNode(subtract.Left, accumulatedInverse, state);
                var right = LowerNode(subtract.Right, accumulatedInverse, state);
                var negRight = state.Emit(SdfTapeOpCode.Neg, right, -1, -1, node.Kind);
                return state.Emit(SdfTapeOpCode.Max, left, negRight, -1, node.Kind);
            }
            case SdfIntersectNode intersect:
            {
                var left = LowerNode(intersect.Left, accumulatedInverse, state);
                var right = LowerNode(intersect.Right, accumulatedInverse, state);
                return state.Emit(SdfTapeOpCode.Max, left, right, -1, node.Kind);
            }
            case SdfTransformNode transform:
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
        public List<SdfTapeInstruction> Instructions { get; } = new();
        public List<SdfTapeBoxPayload> BoxPayloads { get; } = new();
        public List<SdfTapeCylinderPayload> CylinderPayloads { get; } = new();
        public List<SdfTapeSpherePayload> SpherePayloads { get; } = new();
        public List<SdfTapeTorusPayload> TorusPayloads { get; } = new();
        public List<SdfTapeConePayload> ConePayloads { get; } = new();
        public int NextSlot { get; private set; }

        public int Emit(SdfTapeOpCode opCode, int inputA, int inputB, int payloadIndex, SdfNodeKind sourceKind)
        {
            var destSlot = NextSlot++;
            Instructions.Add(new SdfTapeInstruction(opCode, destSlot, inputA, inputB, payloadIndex, sourceKind, Instructions.Count));
            return destSlot;
        }
    }
}
