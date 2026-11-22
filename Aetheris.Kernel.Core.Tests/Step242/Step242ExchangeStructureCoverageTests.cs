using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

/// <summary>
/// Coverage of the ISO 10303-21 exchange structure and of the ISO 10303-42 entity forms a solid can be written in,
/// taken against the checks and entity lists of the NIST STEP File Analyzer. Every case mutates one known-good
/// block: a malformed file is refused with a message that says what is wrong and where, and an equivalent form of
/// the same solid imports to the same solid.
/// </summary>
public sealed class Step242ExchangeStructureCoverageTests
{
    private static readonly string Block = Step242Corpus.Text("testdata/step242/handcrafted/baseline/block-basic.step");

    private static string Replace(string text, string find, string replacement)
    {
        var index = text.IndexOf(find, StringComparison.Ordinal);
        Assert.True(index >= 0, $"mutation anchor not found: {find}");
        return string.Concat(text.AsSpan(0, index), replacement, text.AsSpan(index + find.Length));
    }

    private static string WithData(string text, string entities) => Replace(text, "DATA;", "DATA;\n" + entities);

    private static string WithSchema(string schema) => Regex.Replace(Block, @"FILE_SCHEMA\(\(.*?\)\);", schema, RegexOptions.Singleline);

    private const string FirstShellFace = "CLOSED_SHELL('',(#17,";
    private const string PlaneFace = "#17 = ADVANCED_FACE('',(#18),#52,.F.);";
    private const string FirstEdge = "#21 = EDGE_CURVE('',#22,#24,#26,.T.);";
    private const string LengthUnit = "#166 = ( LENGTH_UNIT() NAMED_UNIT(*) SI_UNIT(.MILLI.,.METRE.) );";
    private const string Rep = "#10 = ADVANCED_BREP_SHAPE_REPRESENTATION('',(#11,#15),#165);";
    private const string Root = "#15 = MANIFOLD_SOLID_BREP('',#16);";
    private const string Points = "#9001 = CARTESIAN_POINT('',(0.,-0.5,-0.5));\n#9002 = CARTESIAN_POINT('',(0.,-0.5,1.5));\n#9003 = CARTESIAN_POINT('',(0.,1.5,-0.5));\n#9004 = CARTESIAN_POINT('',(0.,1.5,1.5));\n";
    private const string Grid = "((#9001,#9002),(#9003,#9004))";
    private const string LinePoints = "#9031 = CARTESIAN_POINT('',(0.,2.,0.));\n#9032 = CARTESIAN_POINT('',(0.,-1.,0.));\n";

    private static string Malformed(string name) => name switch
    {
        "empty" => "",
        "json" => "{\"hello\": 1}",
        "no-start" => Replace(Block, "ISO-10303-21;", ""),
        "no-end" => Replace(Block, "END-ISO-10303-21;", ""),
        "truncated-mid-entity" => Block[..(Block.Length / 2)],
        "truncated-after-data" => Block[..(Block.IndexOf("DATA;", StringComparison.Ordinal) + 5)],
        "ifc" => WithSchema("FILE_SCHEMA(('IFC2X3'));"),
        "unbalanced-paren" => Replace(Block, "#12 = CARTESIAN_POINT('',(0.,0.,0.));", "#12 = CARTESIAN_POINT('',(0.,0.,0.);"),
        "missing-semicolon" => Replace(Block, "#13 = DIRECTION('',(0.,0.,1.));", "#13 = DIRECTION('',(0.,0.,1.))"),
        "bad-real" => Replace(Block, "#23 = CARTESIAN_POINT('',(0.,1.,0.));", "#23 = CARTESIAN_POINT('',(0.,1.x,0.));"),
        "duplicate-id" => Replace(Block, "#14 = DIRECTION('',(1.,0.,-0.));", "#14 = DIRECTION('',(1.,0.,-0.));\n#14 = DIRECTION('',(1.,0.,0.));"),
        "dangling-ref" => Replace(Block, "#20 = ORIENTED_EDGE('',*,*,#21,.T.);", "#20 = ORIENTED_EDGE('',*,*,#99999,.T.);"),
        "unterminated-comment" => Replace(Block, "DATA;", "DATA;\n/* never closed"),
        "deep-nesting" => WithData(Block, "#90005 = X(" + new string('(', 20000) + new string(')', 20000) + ");"),
        "no-solid" => Replace(Replace(Block, Rep, "#10 = SHAPE_REPRESENTATION('',(#11,#9053),#165);"), Root, "")
            .Replace("DATA;", "DATA;\n#9053 = EXTRUDED_AREA_SOLID('',#9054,#11,#13,5.);\n#9054 = RECTANGLE_PROFILE_DEF(.AREA.,$,#9055,1.,1.);\n#9055 = AXIS2_PLACEMENT_2D('',#9056,$);\n#9056 = CARTESIAN_POINT('',(0.,0.));\n"),
        "unknown-curve" => WithData(Replace(Block, FirstEdge, FirstEdge.Replace("#26", "#9030")), "#9030 = CLOTHOID('',#11,1.,1.);\n"),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Theory]
    [InlineData("empty", "begins with 'ISO-10303-21;'")]
    [InlineData("json", "begins with 'ISO-10303-21;'")]
    [InlineData("no-start", "does not begin with 'ISO-10303-21;'")]
    [InlineData("no-end", "does not end with 'END-ISO-10303-21;'. It was probably truncated")]
    [InlineData("truncated-mid-entity", "it was probably truncated")]
    [InlineData("truncated-after-data", "DATA section is not closed by 'ENDSEC;'")]
    [InlineData("ifc", "IFC building-model file")]
    [InlineData("unbalanced-paren", "(in entity #12, line")]
    [InlineData("missing-semicolon", "Expected ';' after the entity")]
    [InlineData("bad-real", "(in entity #23, line")]
    [InlineData("duplicate-id", "Duplicate entity id #14")]
    [InlineData("dangling-ref", "Missing referenced entity #99999")]
    [InlineData("unterminated-comment", "Unterminated comment")]
    [InlineData("deep-nesting", "nested more than 256 levels")]
    [InlineData("no-solid", "constructive solids")]
    [InlineData("unknown-curve", "'CLOTHOID' is unsupported")]
    public void MalformedOrUnsupported_IsRefusedSayingWhy(string name, string expected)
    {
        var result = Step242Importer.ImportBody(Malformed(name));
        Assert.False(result.IsSuccess);
        var errors = result.Diagnostics.Where(d => d.Severity == KernelDiagnosticSeverity.Error).ToArray();
        Assert.NotEmpty(errors);
        Assert.Contains(errors, d => d.Message.Contains(expected, StringComparison.Ordinal));
    }

    private static string Equivalent(string name) => name switch
    {
        "crlf" => Block.Replace("\n", "\r\n"),
        "cr-only" => Block.Replace("\n", "\r"),
        "utf8-bom" => "﻿" + Block,
        "lowercase" => Replace(Replace(Block, "DATA;", "data;"), "#12 = CARTESIAN_POINT(", "#12 = cartesian_point("),
        "comments" => Replace(Block, "#12 = CARTESIAN_POINT('',(0.,0.,0.));", "/* c */ #12 /* c */ = CARTESIAN_POINT( /* c */ '' , ( 0. , /*x*/ 0. , 0. ) ) ;"),
        "no-header-schema" => Regex.Replace(Block, @"FILE_SCHEMA\(\(.*?\)\);", "", RegexOptions.Singleline),
        "unknown-schema" => WithSchema("FILE_SCHEMA(('AP209_MULTIDISCIPLINARY_ANALYSIS_AND_DESIGN_MIM_LF'));"),
        "p21e3-sections" => Replace(Block, "DATA;", "ANCHOR;\n<a1>=#12;\nENDSEC;\nREFERENCE;\n#90001=<ext.stp#abc>;\nENDSEC;\nDATA;"),
        "p21e3-named-data" => Replace(Block, "DATA;", "DATA('s1',('AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF'));"),
        "user-defined" => WithData(Block, "#90000 = !MY_VENDOR_THING('x',1.);"),
        "scope" => Replace(Block, "#12 = CARTESIAN_POINT('',(0.,0.,0.));", "#12 = CARTESIAN_POINT('',(0.,0.,0.));\n#90002 = &SCOPE\n#90003 = CARTESIAN_POINT('',(1.,1.,1.));\nENDSCOPE DIRECTION('',(0.,0.,1.));"),
        "binary-literal" => WithData(Block, "#90004 = MY_BIN(\"0FF\");"),
        "encoded-strings" => Replace(Block, "PRODUCT('block-basic','block-basic'", "PRODUCT('\\X2\\00E9\\X0\\l\\S\\ock','it''s \\X\\E9'"),
        "bezier-curve" => Edge(LinePoints + "#9030 = ( BOUNDED_CURVE() B_SPLINE_CURVE(1,(#9031,#9032),.POLYLINE_FORM.,.F.,.F.) CURVE() GEOMETRIC_REPRESENTATION_ITEM() BEZIER_CURVE() REPRESENTATION_ITEM('') );\n"),
        "quasi-uniform-curve" => Edge(LinePoints + "#9030 = ( BOUNDED_CURVE() B_SPLINE_CURVE(1,(#9031,#9032),.POLYLINE_FORM.,.F.,.F.) CURVE() GEOMETRIC_REPRESENTATION_ITEM() QUASI_UNIFORM_CURVE() REPRESENTATION_ITEM('') );\n"),
        "uniform-curve" => Edge(LinePoints + "#9030 = ( BOUNDED_CURVE() B_SPLINE_CURVE(1,(#9031,#9032),.POLYLINE_FORM.,.F.,.F.) CURVE() GEOMETRIC_REPRESENTATION_ITEM() UNIFORM_CURVE() REPRESENTATION_ITEM('') );\n"),
        "polyline" => Edge(LinePoints + "#9030 = POLYLINE('',(#9031,#9032));\n"),
        "trimmed-cartesian" => Edge("#9030 = TRIMMED_CURVE('',#26,(#23),(#25),.T.,.CARTESIAN.);\n"),
        "composite-one-segment" => Edge("#9030 = COMPOSITE_CURVE('',(#9033),.F.);\n#9033 = COMPOSITE_CURVE_SEGMENT(.CONTINUOUS.,.T.,#9034);\n#9034 = TRIMMED_CURVE('',#26,(PARAMETER_VALUE(-2.)),(PARAMETER_VALUE(1.)),.T.,.PARAMETER.);\n"),
        "surface-curve" => Edge("#9030 = SURFACE_CURVE('',#26,(#52,#160),.CURVE_3D.);\n"),
        "intersection-curve" => Edge("#9030 = INTERSECTION_CURVE('',#26,(#52,#160),.CURVE_3D.);\n"),
        "offset-curve-zero" => Edge("#9035 = DIRECTION('',(1.,0.,0.));\n#9030 = OFFSET_CURVE_3D('',#26,0.,.F.,#9035);\n"),
        "bezier-surface" => Face(Points + $"#9010 = ( BOUNDED_SURFACE() B_SPLINE_SURFACE(1,1,{Grid},.PLANE_SURF.,.F.,.F.,.F.) GEOMETRIC_REPRESENTATION_ITEM() BEZIER_SURFACE() REPRESENTATION_ITEM('') SURFACE() );\n"),
        "uniform-surface" => Face(Points + $"#9010 = ( BOUNDED_SURFACE() B_SPLINE_SURFACE(1,1,{Grid},.PLANE_SURF.,.F.,.F.,.F.) GEOMETRIC_REPRESENTATION_ITEM() UNIFORM_SURFACE() REPRESENTATION_ITEM('') SURFACE() );\n"),
        "rational-bezier-surface" => Face(Points + $"#9010 = ( BEZIER_SURFACE() BOUNDED_SURFACE() B_SPLINE_SURFACE(1,1,{Grid},.PLANE_SURF.,.F.,.F.,.F.) GEOMETRIC_REPRESENTATION_ITEM() RATIONAL_B_SPLINE_SURFACE(((1.,1.),(1.,1.))) REPRESENTATION_ITEM('') SURFACE() );\n"),
        "rectangular-trimmed-surface" => Face("#9010 = RECTANGULAR_TRIMMED_SURFACE('',#52,-10.,10.,-10.,10.,.T.,.T.);\n"),
        "offset-surface-zero" => Face("#9010 = OFFSET_SURFACE('',#52,0.,.F.);\n"),
        "face-surface" => Replace(Block, PlaneFace, "#17 = FACE_SURFACE('',(#18),#52,.F.);"),
        "oriented-face" => WithData(Replace(Block, FirstShellFace, "CLOSED_SHELL('',(#9020,"), "#9020 = ORIENTED_FACE('',*,#17,.T.);\n"),
        "oriented-face-reversed" => WithData(Replace(Replace(Block, FirstShellFace, "CLOSED_SHELL('',(#9020,"), PlaneFace, "#17 = ADVANCED_FACE('',(#9022),#52,.T.);"),
            "#9020 = ORIENTED_FACE('',*,#17,.F.);\n#9022 = FACE_BOUND('',#19,.F.);\n"),
        "oriented-closed-shell" => WithData(Replace(Block, Root, "#15 = MANIFOLD_SOLID_BREP('',#9051);"), "#9051 = ORIENTED_CLOSED_SHELL('',*,#16,.T.);\n"),
        "seam-edge" => Replace(Block, "#20 = ORIENTED_EDGE('',*,*,#21,.T.);", "#20 = SEAM_EDGE('',*,*,#21,.T.,#26);"),
        "subedge" => WithData(Replace(Block, FirstEdge, "#21 = SUBEDGE('',#22,#24,#9060);"), "#9060 = EDGE_CURVE('',#22,#24,#26,.T.);\n"),
        "surface-model-closed" => WithData(Replace(Replace(Block, Rep, "#10 = MANIFOLD_SURFACE_SHAPE_REPRESENTATION('',(#11,#9050),#165);"), Root, ""), "#9050 = SHELL_BASED_SURFACE_MODEL('',(#16));\n"),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private static string Edge(string entities) => WithData(Replace(Block, FirstEdge, FirstEdge.Replace("#26", "#9030")), entities);

    private static string Face(string entities) => WithData(Replace(Block, PlaneFace, PlaneFace.Replace("#52", "#9010")), entities);

    [Theory]
    [InlineData("crlf", null)]
    [InlineData("cr-only", null)]
    [InlineData("utf8-bom", null)]
    [InlineData("lowercase", null)]
    [InlineData("comments", null)]
    [InlineData("no-header-schema", "names no FILE_SCHEMA")]
    [InlineData("unknown-schema", "AP209_MULTIDISCIPLINARY")]
    [InlineData("p21e3-sections", "edition 3 ANCHOR, REFERENCE")]
    [InlineData("p21e3-named-data", null)]
    [InlineData("user-defined", "user-defined ('!')")]
    [InlineData("scope", null)]
    [InlineData("binary-literal", null)]
    [InlineData("encoded-strings", null)]
    [InlineData("bezier-curve", null)]
    [InlineData("quasi-uniform-curve", null)]
    [InlineData("uniform-curve", null)]
    [InlineData("polyline", null)]
    [InlineData("trimmed-cartesian", null)]
    [InlineData("composite-one-segment", null)]
    [InlineData("surface-curve", null)]
    [InlineData("intersection-curve", null)]
    [InlineData("offset-curve-zero", "at distance 0")]
    [InlineData("bezier-surface", null)]
    [InlineData("uniform-surface", null)]
    [InlineData("rational-bezier-surface", null)]
    [InlineData("rectangular-trimmed-surface", "read as their basis surface")]
    [InlineData("offset-surface-zero", "at distance 0")]
    [InlineData("face-surface", "FACE_SURFACE")]
    [InlineData("oriented-face", "ORIENTED_FACE")]
    [InlineData("oriented-face-reversed", "ORIENTED_FACE")]
    [InlineData("oriented-closed-shell", null)]
    [InlineData("seam-edge", null)]
    [InlineData("subedge", null)]
    [InlineData("surface-model-closed", "SHELL_BASED_SURFACE_MODEL")]
    public void EquivalentForm_ImportsTheSameUnitCube(string name, string? note)
    {
        var result = Step242Importer.ImportBody(Equivalent(name));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        var mass = BrepMassProperties.Evaluate(result.Value);
        Assert.True(mass.IsEnclosed && mass.IsOrientationConsistent);
        Assert.Equal(1d, mass.SignedVolume, 6);
        if (note is not null)
            Assert.Contains(result.Diagnostics, d => d.Severity != KernelDiagnosticSeverity.Error && d.Message.Contains(note, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("SI_UNIT($,.METRE.)", null, 1000d)]
    [InlineData("SI_UNIT(.CENTI.,.METRE.)", null, 10d)]
    [InlineData("CONVERSION_BASED_UNIT('INCH',#9041)", "25.4", 25.4)]
    [InlineData("CONVERSION_BASED_UNIT('FOOT',#9041)", "304.8", 304.8)]
    public void LengthUnits_ScaleToMillimetres(string unit, string? factor, double edge)
    {
        var replacement = factor is null
            ? $"#166 = ( LENGTH_UNIT() NAMED_UNIT(*) {unit} );"
            : $"#166 = ( {unit} LENGTH_UNIT() NAMED_UNIT(#9042) );";
        var text = Replace(Block, LengthUnit, replacement);
        if (factor is not null)
            text = WithData(text, $"#9040 = ( LENGTH_UNIT() NAMED_UNIT(*) SI_UNIT(.MILLI.,.METRE.) );\n#9041 = LENGTH_MEASURE_WITH_UNIT(LENGTH_MEASURE({factor}),#9040);\n#9042 = DIMENSIONAL_EXPONENTS(1.,0.,0.,0.,0.,0.,0.);\n");
        var result = Step242Importer.ImportBody(text);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(edge * edge * edge, BrepMassProperties.Evaluate(result.Value).SignedVolume, 6);
    }

    [Fact]
    public void MissingLengthUnit_IsReadAsMillimetresWithAWarning()
    {
        var result = Step242Importer.ImportBody(Replace(Block, "GLOBAL_UNIT_ASSIGNED_CONTEXT\n((#166,#167,#168))", "GLOBAL_UNIT_ASSIGNED_CONTEXT\n((#167,#168))"));
        Assert.True(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Severity == KernelDiagnosticSeverity.Warning && d.Source == "Parser.Units.LengthUnitMissing");
    }

    [Fact]
    public void EncodedStrings_DecodeToUnicode()
    {
        Assert.Equal("élïck", Step242StringDecoding.Decode("\\X2\\00E9\\X0\\l\\S\\ock"));
        Assert.Equal("±0.1", Step242StringDecoding.Decode("\\X2\\00B1\\X0\\0.1"));
        Assert.Equal("é", Step242StringDecoding.Decode("\\X\\E9"));
        Assert.Equal("a\\b", Step242StringDecoding.Decode("a\\\\b"));
        Assert.Equal("😀", Step242StringDecoding.Decode("\\X4\\0001F600\\X0\\"));
    }
}
