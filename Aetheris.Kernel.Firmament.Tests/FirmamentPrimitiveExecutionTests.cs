using System.Linq;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Firmament.Lowering;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentPrimitiveExecutionTests
{
    [Fact]
    public void Compile_Executes_Box_Primitive_Into_Real_Body()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m3c-valid-box-exec.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var executed = Assert.Single(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives);
        Assert.Equal("base", executed.FeatureId);
        Assert.Equal(FirmamentLoweredPrimitiveKind.Box, executed.Kind);
        Assert.NotEmpty(executed.Body.Topology.Bodies);
        Assert.NotEmpty(executed.Body.Topology.Faces);
        Assert.Empty(result.Compilation.Value.PrimitiveExecutionResult.ExecutedBooleans);
    }

    [Fact]
    public void Compile_Executes_Cylinder_Primitive_Into_Real_Body()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m3c-valid-cylinder-exec.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var executed = Assert.Single(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives);
        Assert.Equal("post", executed.FeatureId);
        Assert.Equal(FirmamentLoweredPrimitiveKind.Cylinder, executed.Kind);
        Assert.NotEmpty(executed.Body.Topology.Bodies);
        Assert.NotEmpty(executed.Body.Topology.Faces);
    }

    [Fact]
    public void Compile_Executes_Sphere_Primitive_Into_Real_Body()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m3c-valid-sphere-exec.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var executed = Assert.Single(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives);
        Assert.Equal("ball", executed.FeatureId);
        Assert.Equal(FirmamentLoweredPrimitiveKind.Sphere, executed.Kind);
        Assert.NotEmpty(executed.Body.Topology.Bodies);
        Assert.NotEmpty(executed.Body.Topology.Faces);
    }

    [Fact]
    public void Compile_Executes_Torus_Primitive_Into_Real_Body_With_Truthful_Topology()
    {
        var result = CompileFixture("testdata/firmament/examples/torus_basic.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var executed = Assert.Single(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives);
        Assert.Equal("donut1", executed.FeatureId);
        Assert.Equal(FirmamentLoweredPrimitiveKind.Torus, executed.Kind);
        Assert.Single(executed.Body.Topology.Faces);
        Assert.Equal(2, executed.Body.Topology.Edges.Count());
        Assert.Single(executed.Body.Topology.Vertices);
    }

    [Fact]
    public void Compile_Executes_Cone_Primitive_Into_Real_Body_With_Truthful_Frustum_Topology()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m10e-valid-cone-exec.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var executed = Assert.Single(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives);
        Assert.Equal("frustum1", executed.FeatureId);
        Assert.Equal(FirmamentLoweredPrimitiveKind.Cone, executed.Kind);
        Assert.Equal(3, executed.Body.Topology.Faces.Count());
        Assert.Equal(3, executed.Body.Topology.Edges.Count());
        Assert.Equal(4, executed.Body.Topology.Vertices.Count());

        var sideFace = Assert.Single(executed.Body.Topology.Faces, face =>
        {
            executed.Body.TryGetFaceSurfaceGeometry(face.Id, out var surface);
            return surface!.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cone;
        });
        Assert.True(executed.Body.TryGetFaceSurfaceGeometry(sideFace.Id, out var side));
        Assert.Equal(Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cone, side!.Kind);

        var planarFaces = executed.Body.Topology.Faces
            .Where(face =>
            {
                executed.Body.TryGetFaceSurfaceGeometry(face.Id, out var surface);
                return surface!.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Plane;
            })
            .Select(face =>
            {
                executed.Body.TryGetFaceSurfaceGeometry(face.Id, out var surface);
                return surface!.Plane!.Value;
            })
            .OrderBy(plane => plane.Origin.Z)
            .ToArray();
        Assert.Equal(2, planarFaces.Length);
        Assert.Equal(new Point3D(0d, 0d, 0d), planarFaces[0].Origin);
        Assert.Equal(new Point3D(0d, 0d, 20d), planarFaces[1].Origin);
    }

    [Fact]
    public void Compile_Executes_PointedCone_With_TopRadiusZero_Into_Truthful_Topology()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m10f2-valid-cone-pointed-top-zero-exec.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var executed = Assert.Single(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives);
        Assert.Equal("pointed_top", executed.FeatureId);
        Assert.Equal(FirmamentLoweredPrimitiveKind.Cone, executed.Kind);
        Assert.Equal(2, executed.Body.Topology.Faces.Count());
        Assert.Equal(2, executed.Body.Topology.Edges.Count());
        Assert.Equal(3, executed.Body.Topology.Vertices.Count());

        Assert.Equal(1, executed.Body.Topology.Faces.Count(face =>
        {
            executed.Body.TryGetFaceSurfaceGeometry(face.Id, out var surface);
            return surface!.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cone;
        }));
        Assert.Equal(1, executed.Body.Topology.Faces.Count(face =>
        {
            executed.Body.TryGetFaceSurfaceGeometry(face.Id, out var surface);
            return surface!.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Plane;
        }));
        Assert.Equal(1, executed.Body.Topology.Edges.Count(edge =>
        {
            executed.Body.TryGetEdgeCurveGeometry(edge.Id, out var curve);
            return curve!.Kind == Aetheris.Kernel.Core.Geometry.CurveGeometryKind.Circle3;
        }));
    }

    [Fact]
    public void Compile_Executes_PointedCone_With_BottomRadiusZero_Into_Truthful_Topology()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m10f2-valid-cone-pointed-bottom-zero-exec.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var executed = Assert.Single(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives);
        Assert.Equal("pointed_bottom", executed.FeatureId);
        Assert.Equal(FirmamentLoweredPrimitiveKind.Cone, executed.Kind);
        Assert.Equal(2, executed.Body.Topology.Faces.Count());
        Assert.Equal(2, executed.Body.Topology.Edges.Count());
        Assert.Equal(3, executed.Body.Topology.Vertices.Count());

        var planarFace = Assert.Single(executed.Body.Topology.Faces, face =>
        {
            executed.Body.TryGetFaceSurfaceGeometry(face.Id, out var surface);
            return surface!.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Plane;
        });
        executed.Body.TryGetFaceSurfaceGeometry(planarFace.Id, out var planeSurface);
        Assert.Equal(20d, planeSurface!.Plane!.Value.Origin.Z, 9);
    }

    [Fact]
    public void Compile_Executes_Multiple_Primitives_In_Source_Order_With_Preserved_Ids()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m3c-valid-multiple-primitives-exec.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var executed = result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives;

        Assert.Collection(
            executed,
            first =>
            {
                Assert.Equal(0, first.OpIndex);
                Assert.Equal("base", first.FeatureId);
                Assert.Equal(FirmamentLoweredPrimitiveKind.Box, first.Kind);
            },
            second =>
            {
                Assert.Equal(1, second.OpIndex);
                Assert.Equal("post", second.FeatureId);
                Assert.Equal(FirmamentLoweredPrimitiveKind.Cylinder, second.Kind);
            },
            third =>
            {
                Assert.Equal(2, third.OpIndex);
                Assert.Equal("ball", third.FeatureId);
                Assert.Equal(FirmamentLoweredPrimitiveKind.Sphere, third.Kind);
            });
    }

    [Fact]
    public void Compile_Executes_Add_Boolean_Into_Real_Body()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m3d-valid-add-exec.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var executedBoolean = Assert.Single(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedBooleans);
        Assert.Equal("joined", executedBoolean.FeatureId);
        Assert.Equal(FirmamentLoweredBooleanKind.Add, executedBoolean.Kind);
        Assert.NotEmpty(executedBoolean.Body.Topology.Bodies);
        Assert.NotEmpty(executedBoolean.Body.Topology.Faces);
    }

    [Fact]
    public void Compile_Subtract_Boolean_Fails_Deterministically_When_Kernel_Cannot_Represent_Result()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m3d-valid-subtract-exec.firmament");

        Assert.False(result.Compilation.IsSuccess);
        Assert.Equal("firmament", result.Compilation.Diagnostics[0].Source);
        Assert.Contains("Requested boolean feature 'cut' (subtract) could not be executed.", result.Compilation.Diagnostics[0].Message, StringComparison.Ordinal);
        Assert.Contains(result.Compilation.Diagnostics, diagnostic => diagnostic.Code == Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticCode.NotImplemented);
    }

    [Fact]
    public void Compile_Executes_Intersect_Boolean_Into_Real_Body()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m3d-valid-intersect-exec.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var executedBoolean = Assert.Single(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedBooleans);
        Assert.Equal("clipped", executedBoolean.FeatureId);
        Assert.Equal(FirmamentLoweredBooleanKind.Intersect, executedBoolean.Kind);
        Assert.NotEmpty(executedBoolean.Body.Topology.Bodies);
        Assert.NotEmpty(executedBoolean.Body.Topology.Faces);
    }

    [Fact]
    public void Compile_Mixed_Document_With_Unsupported_Boolean_Fails_Before_Publishing_Success_Artifact()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m3d-mixed-primitive-boolean-validation.firmament");

        Assert.False(result.Compilation.IsSuccess);
        Assert.Contains(result.Compilation.Diagnostics, diagnostic => diagnostic.Message.Contains("Requested boolean feature 'cut1' (subtract) could not be executed.", StringComparison.Ordinal));
    }

    [Fact]
    public void Compile_Boolean_Chain_Fails_When_Earlier_Requested_Boolean_Could_Not_Execute()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m3d-valid-boolean-chain-exec.firmament");

        Assert.False(result.Compilation.IsSuccess);
        Assert.Contains(result.Compilation.Diagnostics, diagnostic => diagnostic.Message.Contains("Requested boolean feature 'cut1' (subtract) could not be executed.", StringComparison.Ordinal));
    }

    [Fact]
    public void Compile_Nested_Primitive_Tool_Missing_Required_Field_Fails_Before_Execution()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m3d-invalid-with-box-missing-size.firmament");

        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Equal(Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, diagnostic.Code);
        Assert.Contains("[FIRM-STRUCT-0012]", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("with.size", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_Boolean_Nested_With_Only_Supports_Primitive_Tool_Ops()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m3d-invalid-with-unsupported-tool-op.firmament");

        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Equal(Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Contains("supports nested tool ops 'box', 'cylinder', 'sphere', 'cone', and 'torus' only", diagnostic.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("testdata/firmament/fixtures/m10n-unsupported-box-subtract-torus.firmament", "ring_cut", "subtract")]
    [InlineData("testdata/firmament/fixtures/m10n-unsupported-box-add-torus.firmament", "joined", "add")]
    [InlineData("testdata/firmament/fixtures/m10n-unsupported-box-intersect-torus.firmament", "overlap", "intersect")]
    public void Compile_BoxTorusBooleanFixtures_Reach_Kernel_And_Fail_Loudly(string fixturePath, string expectedFeatureId, string expectedKind)
    {
        var result = CompileFixture(fixturePath);

        Assert.False(result.Compilation.IsSuccess);
        Assert.Contains(result.Compilation.Diagnostics, diagnostic =>
            diagnostic.Code == Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticCode.NotImplemented
            && diagnostic.Source == "firmament"
            && diagnostic.Message.Contains($"Requested boolean feature '{expectedFeatureId}' ({expectedKind}) could not be executed.", StringComparison.Ordinal));
        Assert.Contains(result.Compilation.Diagnostics, diagnostic =>
            diagnostic.Code == Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticCode.NotImplemented
            && (diagnostic.Message.Contains("M13 only supports recognized axis-aligned boxes from BrepPrimitives.CreateBox(...).", StringComparison.Ordinal)
                || diagnostic.Message.Contains("analytic hole surface kind", StringComparison.Ordinal)
                || diagnostic.Message.Contains("Boolean feature", StringComparison.Ordinal)));
    }

    [Fact]
    public void Compile_ValidationFailure_Still_Fails_Before_Execution()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m1a-invalid-sphere-missing-radius.firmament");

        Assert.False(result.Compilation.IsSuccess);
    }

    [Fact]
    public void Compile_Primitive_And_Boolean_Execution_Output_Is_Deterministic_For_Metadata()
    {
        var fixture = "testdata/firmament/fixtures/m10h1-valid-mixed-primitive-boolean-validation.firmament";

        var first = CompileFixture(fixture);
        var second = CompileFixture(fixture);

        Assert.True(first.Compilation.IsSuccess);
        Assert.True(second.Compilation.IsSuccess);

        var firstMetadata = first.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives
            .Select(p => (p.OpIndex, p.FeatureId, PrimitiveKind: p.Kind.ToString(), BodyCount: p.Body.Topology.Bodies.Count(), FaceCount: p.Body.Topology.Faces.Count()))
            .Concat(first.Compilation.Value.PrimitiveExecutionResult.ExecutedBooleans
                .Select(b => (b.OpIndex, b.FeatureId, PrimitiveKind: b.Kind.ToString(), BodyCount: b.Body.Topology.Bodies.Count(), FaceCount: b.Body.Topology.Faces.Count())))
            .OrderBy(entry => entry.OpIndex)
            .ToArray();
        var secondMetadata = second.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives
            .Select(p => (p.OpIndex, p.FeatureId, PrimitiveKind: p.Kind.ToString(), BodyCount: p.Body.Topology.Bodies.Count(), FaceCount: p.Body.Topology.Faces.Count()))
            .Concat(second.Compilation.Value.PrimitiveExecutionResult.ExecutedBooleans
                .Select(b => (b.OpIndex, b.FeatureId, PrimitiveKind: b.Kind.ToString(), BodyCount: b.Body.Topology.Bodies.Count(), FaceCount: b.Body.Topology.Faces.Count())))
            .OrderBy(entry => entry.OpIndex)
            .ToArray();

        Assert.Equal(firstMetadata, secondMetadata);
    }



    [Fact]
    public void Default_Box_Frame_Is_Centered_In_XY_And_Bottom_On_Z0()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m3c-valid-box-exec.firmament");
        Assert.True(result.Compilation.IsSuccess);

        var body = Assert.Single(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives).Body;

        var bounds = GetBounds(body);
        Assert.Equal(new Point3D(-5d, -10d, 0d), bounds.Min);
        Assert.Equal(new Point3D(5d, 10d, 30d), bounds.Max);
    }

    [Fact]
    public void Default_Cylinder_Frame_Is_Centered_In_XY_And_Bottom_On_Z0()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m3c-valid-cylinder-exec.firmament");
        Assert.True(result.Compilation.IsSuccess);

        var body = Assert.Single(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives).Body;
        Assert.True(body.TryGetFaceSurfaceGeometry(new FaceId(1), out var side));
        Assert.Equal(new Point3D(0d, 0d, 0d), side!.Cylinder!.Value.Origin);

        Assert.True(body.TryGetFaceSurfaceGeometry(new FaceId(2), out var top));
        Assert.Equal(new Point3D(0d, 0d, 9d), top!.Plane!.Value.Origin);

        Assert.True(body.TryGetFaceSurfaceGeometry(new FaceId(3), out var bottom));
        Assert.Equal(new Point3D(0d, 0d, 0d), bottom!.Plane!.Value.Origin);
    }

    [Fact]
    public void Default_Sphere_Frame_Is_Centered_At_Origin()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m3c-valid-sphere-exec.firmament");
        Assert.True(result.Compilation.IsSuccess);

        var body = Assert.Single(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives).Body;
        Assert.True(body.TryGetFaceSurfaceGeometry(new FaceId(1), out var surface));
        Assert.Equal(Point3D.Origin, surface!.Sphere!.Value.Center);
    }

    [Fact]
    public void Default_Torus_Frame_Is_Centered_At_Origin()
    {
        var result = CompileFixture("testdata/firmament/examples/torus_basic.firmament");
        Assert.True(result.Compilation.IsSuccess);

        var body = Assert.Single(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives).Body;
        Assert.True(body.TryGetFaceSurfaceGeometry(new FaceId(1), out var surface));
        Assert.Equal(Point3D.Origin, surface!.Torus!.Value.Center);
    }

    [Fact]
    public void Selector_Placement_On_TopFace_Makes_Sphere_Tangent_Instead_Of_Buried()
    {
        const string source = """
firmament:
  version: 1

model:
  name: placed_sphere_contact
  units: mm

ops[2]:
  -
    op: box
    id: base
    size[3]:
      8
      8
      4

  -
    op: sphere
    id: ball
    radius: 3
    place:
      on: base.top_face
      offset[3]:
        0
        0
        0
""";

        var result = Compile(source);
        Assert.True(result.Compilation.IsSuccess);

        var baseBody = result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives.Single(p => p.FeatureId == "base").Body;
        Assert.True(baseBody.TryGetFaceSurfaceGeometry(new FaceId(2), out var baseTop));
        Assert.Equal(4d, baseTop!.Plane!.Value.Origin.Z, 9);

        var sphereBody = result.Compilation.Value.PrimitiveExecutionResult.ExecutedPrimitives.Single(p => p.FeatureId == "ball").Body;
        Assert.True(sphereBody.TryGetFaceSurfaceGeometry(new FaceId(1), out var sphereSurface));
        Assert.Equal(new Point3D(0d, 0d, 7d), sphereSurface!.Sphere!.Value.Center);
    }

    [Fact]
    public void Selector_Placement_On_TopFace_Makes_Torus_Tangent_Instead_Of_Buried()
    {
        const string source = """
firmament:
  version: 1

model:
  name: placed_torus_contact
  units: mm

ops[2]:
  -
    op: box
    id: base
    size[3]:
      8
      8
      4

  -
    op: torus
    id: donut
    major_radius: 5
    minor_radius: 2
    place:
      on: base.top_face
      offset[3]:
        0
        0
        0
""";

        var result = Compile(source);
        Assert.True(result.Compilation.IsSuccess);

        var torusBody = result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives.Single(p => p.FeatureId == "donut").Body;
        Assert.True(torusBody.TryGetFaceSurfaceGeometry(new FaceId(1), out var torusSurface));
        Assert.Equal(new Point3D(0d, 0d, 6d), torusSurface!.Torus!.Value.Center);
    }

    [Fact]
    public void Selector_Placement_Offset_Composes_After_Sphere_Contact_Correction()
    {
        const string source = """
firmament:
  version: 1

model:
  name: placed_sphere_offset
  units: mm

ops[2]:
  -
    op: box
    id: base
    size[3]:
      8
      8
      4

  -
    op: sphere
    id: ball
    radius: 3
    place:
      on: base.top_face
      offset[3]:
        1
        -2
        5
""";

        var result = Compile(source);
        Assert.True(result.Compilation.IsSuccess);

        var sphereBody = result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives.Single(p => p.FeatureId == "ball").Body;
        Assert.True(sphereBody.TryGetFaceSurfaceGeometry(new FaceId(1), out var sphereSurface));
        Assert.Equal(new Point3D(1d, -2d, 12d), sphereSurface!.Sphere!.Value.Center);
    }

    [Fact]
    public void Selector_Placement_On_TopFace_Leaves_Box_Cylinder_And_Cone_Behavior_Unchanged()
    {
        const string source = """
firmament:
  version: 1

model:
  name: unchanged_contact_frames
  units: mm

ops[4]:
  -
    op: box
    id: base
    size[3]:
      8
      8
      4

  -
    op: box
    id: placed_box
    size[3]:
      2
      2
      3
    place:
      on: base.top_face
      offset[3]:
        0
        0
        0

  -
    op: cylinder
    id: placed_cylinder
    radius: 2
    height: 6
    place:
      on: base.top_face
      offset[3]:
        0
        0
        0

  -
    op: cone
    id: placed_cone
    bottom_radius: 3
    top_radius: 1
    height: 5
    place:
      on: base.top_face
      offset[3]:
        0
        0
        0
""";

        var result = Compile(source);
        Assert.True(result.Compilation.IsSuccess);

        var boxBody = result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives.Single(p => p.FeatureId == "placed_box").Body;
        var boxBounds = GetBounds(boxBody);
        Assert.Equal(new Point3D(-1d, -1d, 4d), boxBounds.Min);
        Assert.Equal(new Point3D(1d, 1d, 7d), boxBounds.Max);

        var cylinderBody = result.Compilation.Value.PrimitiveExecutionResult.ExecutedPrimitives.Single(p => p.FeatureId == "placed_cylinder").Body;
        Assert.True(cylinderBody.TryGetFaceSurfaceGeometry(new FaceId(3), out var cylinderBottom));
        Assert.True(cylinderBody.TryGetFaceSurfaceGeometry(new FaceId(2), out var cylinderTop));
        Assert.Equal(4d, cylinderBottom!.Plane!.Value.Origin.Z, 9);
        Assert.Equal(10d, cylinderTop!.Plane!.Value.Origin.Z, 9);

        var coneBody = result.Compilation.Value.PrimitiveExecutionResult.ExecutedPrimitives.Single(p => p.FeatureId == "placed_cone").Body;
        var conePlanes = coneBody.Topology.Faces
            .Select(face =>
            {
                coneBody.TryGetFaceSurfaceGeometry(face.Id, out var surface);
                return surface;
            })
            .Where(surface => surface?.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Plane)
            .Select(surface => surface!.Plane!.Value.Origin.Z)
            .OrderBy(z => z)
            .ToArray();
        Assert.Equal(new[] { 4d, 9d }, conePlanes);
    }


    [Fact]
    public void Boolean_Without_Placement_Behaves_As_Before()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m3d-valid-add-exec.firmament");
        Assert.True(result.Compilation.IsSuccess);

        var executedBoolean = Assert.Single(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedBooleans);
        Assert.Equal("joined", executedBoolean.FeatureId);
        Assert.NotEmpty(executedBoolean.Body.Topology.Faces);
    }

    [Fact]
    public void Boolean_Origin_Placement_Translates_Result_Body()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7d-valid-boolean-origin-placement.firmament");
        Assert.True(result.Compilation.IsSuccess);

        var body = Assert.Single(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedBooleans).Body;
        var bounds = GetBounds(body);
        Assert.Equal(new Point3D(8d, -7d, 1d), bounds.Min);
    }

    [Fact]
    public void Boolean_Selector_Placement_Uses_Deterministic_Anchor_Resolution()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7d-valid-boolean-selector-placement.firmament");
        Assert.True(result.Compilation.IsSuccess);

        var body = Assert.Single(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedBooleans).Body;
        var bounds = GetBounds(body);
        Assert.Equal(new Point3D(1d, -2.25d, -1d), bounds.Min);
    }

    [Fact]
    public void Boolean_Placement_Does_Not_Mutate_Referenced_Source_Bodies()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7d-valid-boolean-origin-placement.firmament");
        Assert.True(result.Compilation.IsSuccess);

        var primitive = Assert.Single(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives);
        var primitiveBounds = GetBounds(primitive.Body);
        Assert.Equal(new Point3D(-2d, -2d, 0d), primitiveBounds.Min);
        Assert.Equal(new Point3D(2d, 2d, 4d), primitiveBounds.Max);
    }

    [Fact]
    public void Chained_Primitive_And_Placed_Boolean_Executes_Deterministically()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7d-valid-boolean-chain-placement.firmament");
        Assert.True(result.Compilation.IsSuccess);

        var placedBoolean = result.Compilation.Value.PrimitiveExecutionResult!.ExecutedBooleans.Single(b => b.FeatureId == "join2");
        var bounds = GetBounds(placedBoolean.Body);
        Assert.Equal(new Point3D(-7d, -1d, -3d), bounds.Min);
    }

    [Fact]
    public void Origin_Placement_With_Offset_Translates_Primitive()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7c-valid-origin-placement-offset.firmament");
        Assert.True(result.Compilation.IsSuccess);

        var body = Assert.Single(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives).Body;
        var bounds = GetBounds(body);
        Assert.Equal(new Point3D(9d, -6d, 2d), bounds.Min);
        Assert.Equal(new Point3D(11d, -4d, 4d), bounds.Max);
    }

    private static (Point3D Min, Point3D Max) GetBounds(BrepBody body)
    {
        var points = body.Topology.Vertices
            .Select(v => body.TryGetVertexPoint(v.Id, out var p) ? p : (Point3D?)null)
            .Where(p => p is not null)
            .Select(p => p!.Value)
            .ToArray();
        Assert.NotEmpty(points);

        return (
            new Point3D(points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z)),
            new Point3D(points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z)));
    }

    private static FirmamentCompileResult CompileFixture(string fixturePath)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText(fixturePath);
        return Compile(source);
    }

    private static FirmamentCompileResult Compile(string source)
    {
        var compiler = new FirmamentCompiler();
        return compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));
    }
}
