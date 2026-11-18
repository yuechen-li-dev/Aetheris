using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>A Box edge named by two signed, perpendicular stock faces.</summary>
internal sealed record BoxEdgeFacePair(string First, string Second)
{
    private static readonly string[] Faces = ["+X", "-X", "+Y", "-Y", "+Z", "-Z"];
    public string Selector => $"SharedEdge({First},{Second})";
    public string Target => TargetFor(Second);
    public bool IsOriginalAirPair => First == "+X" && Second == "+Z";

    public static BoxEdgeFacePair? Parse(string face, string target)
    {
        var second = Faces.FirstOrDefault(candidate => TargetFor(candidate) == target);
        return second is not null && Faces.Contains(face) && face[1] != second[1]
            ? new(face, second) : null;
    }

    public static BoxEdgeFacePair? FromFaces(string one, string two)
    {
        if (!Faces.Contains(one) || !Faces.Contains(two) || one[1] == two[1]) return null;
        return one[1] < two[1] ? new(one, two) : new(two, one);
    }

    public static BoxEdgeFacePair? FromSelector(string? selector)
    {
        if (selector is null || !selector.StartsWith("SharedEdge(", StringComparison.Ordinal) || !selector.EndsWith(')')) return null;
        var faces = selector["SharedEdge(".Length..^1].Split(',');
        return faces.Length == 2 ? FromFaces(faces[0], faces[1]) : null;
    }

    public (double Width, double Length, double Height, Transform3D ToWorld) Frame(IReadOnlyList<double> size)
    {
        var x = Direction(First);
        var z = Direction(Second);
        var y = z.Cross(x);
        double Along(Vector3D direction) => Math.Abs(direction.X) * size[0] + Math.Abs(direction.Y) * size[1] + Math.Abs(direction.Z) * size[2];
        return (Along(x), Along(y), Along(z), Transform3D.FromRowMajor(
            [x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0, 0, 0, size[2] / 2, 1]));
    }

    private static string TargetFor(string face) => "SharedEdge" + (face[0] == '+' ? "Plus" : "Minus") + face[1];

    private static Vector3D Direction(string face)
    {
        var sign = face[0] == '+' ? 1d : -1d;
        return face[1] switch { 'X' => new(sign, 0, 0), 'Y' => new(0, sign, 0), _ => new(0, 0, sign) };
    }
}
