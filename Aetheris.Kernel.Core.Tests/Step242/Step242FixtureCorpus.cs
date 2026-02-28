using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

internal static class Step242FixtureCorpus
{
    // M24 generated golden: regenerate by exporting CreateBox(2,2,2) via Step242Exporter.
    public static readonly string CanonicalBoxGolden = CreateCanonicalBoxGolden();

    public const string UnsupportedSphericalSurface = "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=SPHERICAL_SURFACE($,#2,1.0);\n#2=AXIS2_PLACEMENT_3D($,#3,#4,#5);\n#3=CARTESIAN_POINT($,(0,0,0));\n#4=DIRECTION($,(0,0,1));\n#5=DIRECTION($,(1,0,0));\nENDSEC;\nEND-ISO-10303-21;";

    public const string MalformedMissingParen = "ISO-10303-21; DATA; #1=MANIFOLD_SOLID_BREP('bad',#2 ENDSEC; END-ISO-10303-21;";

    private static string CreateCanonicalBoxGolden()
    {
        var boxResult = BrepPrimitives.CreateBox(2d, 2d, 2d);
        if (!boxResult.IsSuccess)
        {
            throw new InvalidOperationException("Failed to build canonical box fixture.");
        }

        var export = Step242Exporter.ExportBody(boxResult.Value);
        if (!export.IsSuccess)
        {
            throw new InvalidOperationException("Failed to export canonical box fixture.");
        }

        return export.Value;
    }
}
