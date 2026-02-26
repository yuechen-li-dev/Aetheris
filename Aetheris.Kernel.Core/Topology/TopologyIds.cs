namespace Aetheris.Kernel.Core.Topology;

/// <summary>
/// Stable in-memory topology ID for a vertex. The default value (0) is invalid.
/// </summary>
public readonly record struct VertexId(int Value)
{
    public static VertexId Invalid => default;

    public bool IsValid => Value > 0;
}

/// <summary>
/// Stable in-memory topology ID for an edge. The default value (0) is invalid.
/// </summary>
public readonly record struct EdgeId(int Value)
{
    public static EdgeId Invalid => default;

    public bool IsValid => Value > 0;
}

/// <summary>
/// Stable in-memory topology ID for a coedge. The default value (0) is invalid.
/// </summary>
public readonly record struct CoedgeId(int Value)
{
    public static CoedgeId Invalid => default;

    public bool IsValid => Value > 0;
}

/// <summary>
/// Stable in-memory topology ID for a loop. The default value (0) is invalid.
/// </summary>
public readonly record struct LoopId(int Value)
{
    public static LoopId Invalid => default;

    public bool IsValid => Value > 0;
}

/// <summary>
/// Stable in-memory topology ID for a face. The default value (0) is invalid.
/// </summary>
public readonly record struct FaceId(int Value)
{
    public static FaceId Invalid => default;

    public bool IsValid => Value > 0;
}

/// <summary>
/// Stable in-memory topology ID for a shell. The default value (0) is invalid.
/// </summary>
public readonly record struct ShellId(int Value)
{
    public static ShellId Invalid => default;

    public bool IsValid => Value > 0;
}

/// <summary>
/// Stable in-memory topology ID for a body. The default value (0) is invalid.
/// </summary>
public readonly record struct BodyId(int Value)
{
    public static BodyId Invalid => default;

    public bool IsValid => Value > 0;
}
