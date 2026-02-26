using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Brep.Picking;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;
using Aetheris.Server.Contracts;
using Aetheris.Server.Documents;

namespace Aetheris.Server.Api;

public static class KernelEndpoints
{
    public static void MapKernelApi(this WebApplication app)
    {
        MapDocumentRoutes(app.MapGroup("/api/v1/documents"));
        MapDocumentRoutes(app.MapGroup("/api/documents"));
    }

    private static void MapDocumentRoutes(RouteGroupBuilder documents)
    {
        documents.MapPost("", (DocumentCreateRequestDto? request, KernelDocumentStore store) =>
        {
            var created = store.Create(request?.Name);
            return ApiMappings.Ok(new DocumentCreateResponseDto(created.Id, created.Name, Volatile: true));
        });

        documents.MapGet("/{documentId:guid}", (Guid documentId, KernelDocumentStore store) =>
        {
            if (!store.TryGet(documentId, out var document))
            {
                return ApiMappings.NotFound($"Document '{documentId}' was not found.", "documents.get");
            }

            var bodyIds = document.SnapshotBodies().Keys.OrderBy(id => id).ToArray();
            return ApiMappings.Ok(new DocumentSummaryResponseDto(document.Id, document.Name, bodyIds.Length, bodyIds));
        });

        documents.MapPost("/{documentId:guid}/bodies/primitives/box", (Guid documentId, BoxCreateRequestDto request, KernelDocumentStore store) =>
            WithDocument(store, documentId, document =>
            {
                var kernel = BrepPrimitives.CreateBox(request.Width, request.Height, request.Depth);
                if (!kernel.IsSuccess)
                {
                    return ApiMappings.KernelFailure(kernel.Diagnostics);
                }

                return ApiMappings.Ok(CreateBodyResponse(document, kernel.Value));
            }));

        documents.MapPost("/{documentId:guid}/bodies/primitives/cylinder", (Guid documentId, CylinderCreateRequestDto request, KernelDocumentStore store) =>
            WithDocument(store, documentId, document =>
            {
                var kernel = BrepPrimitives.CreateCylinder(request.Radius, request.Height);
                if (!kernel.IsSuccess)
                {
                    return ApiMappings.KernelFailure(kernel.Diagnostics);
                }

                return ApiMappings.Ok(CreateBodyResponse(document, kernel.Value));
            }));

        documents.MapPost("/{documentId:guid}/bodies/primitives/sphere", (Guid documentId, SphereCreateRequestDto request, KernelDocumentStore store) =>
            WithDocument(store, documentId, document =>
            {
                var kernel = BrepPrimitives.CreateSphere(request.Radius);
                if (!kernel.IsSuccess)
                {
                    return ApiMappings.KernelFailure(kernel.Diagnostics);
                }

                return ApiMappings.Ok(CreateBodyResponse(document, kernel.Value));
            }));

        documents.MapPost("/{documentId:guid}/operations/extrude", (Guid documentId, ExtrudeRequestDto request, KernelDocumentStore store) =>
            WithDocument(store, documentId, document =>
            {
                if (request.Profile is null)
                {
                    return ApiMappings.BadRequestFromMessage("Profile must be provided.", "operations.extrude");
                }

                var profile = PolylineProfile2D.Create(request.Profile.Select(p => new ProfilePoint2D(p.X, p.Y)).ToArray());
                if (!profile.IsSuccess)
                {
                    return ApiMappings.KernelFailure(profile.Diagnostics);
                }

                if (!TryCreateFrame(request.Origin, request.Normal, request.UAxis, out var frame, out var frameError))
                {
                    return frameError;
                }

                var kernel = BrepExtrude.Create(profile.Value, frame, request.Depth);
                if (!kernel.IsSuccess)
                {
                    return ApiMappings.KernelFailure(kernel.Diagnostics);
                }

                return ApiMappings.Ok(CreateBodyResponse(document, kernel.Value));
            }));

        documents.MapPost("/{documentId:guid}/operations/revolve", (Guid documentId, RevolveRequestDto request, KernelDocumentStore store) =>
            WithDocument(store, documentId, document =>
            {
                if (request.Profile is null)
                {
                    return ApiMappings.BadRequestFromMessage("Profile must be provided.", "operations.revolve");
                }

                if (!Direction3D.TryCreate(new Vector3D(request.AxisDirection.X, request.AxisDirection.Y, request.AxisDirection.Z), out var axisDirection))
                {
                    return ApiMappings.BadRequestFromMessage("AxisDirection must be non-zero and finite.", "operations.revolve");
                }

                if (!Direction3D.TryCreate(new Vector3D(request.UAxis.X, request.UAxis.Y, request.UAxis.Z), out var uAxis))
                {
                    return ApiMappings.BadRequestFromMessage("UAxis must be non-zero and finite.", "operations.revolve");
                }

                ExtrudeFrame3D frame;
                try
                {
                    frame = new ExtrudeFrame3D(
                        new Point3D(request.Origin.X, request.Origin.Y, request.Origin.Z),
                        axisDirection,
                        uAxis);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    return ApiMappings.BadRequestFromMessage(ex.Message, "operations.revolve");
                }

                var angle = request.AngleRadians <= 0d ? 2d * double.Pi : request.AngleRadians;
                var kernel = BrepRevolve.Create(
                    request.Profile.Select(p => new ProfilePoint2D(p.X, p.Y)).ToArray(),
                    frame,
                    new RevolveAxis3D(new Point3D(request.Origin.X, request.Origin.Y, request.Origin.Z), axisDirection.ToVector()),
                    angle);

                if (!kernel.IsSuccess)
                {
                    return ApiMappings.KernelFailure(kernel.Diagnostics);
                }

                return ApiMappings.Ok(CreateBodyResponse(document, kernel.Value));
            }));

        documents.MapPost("/{documentId:guid}/operations/boolean", (Guid documentId, BooleanRequestDto request, KernelDocumentStore store) =>
            WithDocument(store, documentId, document =>
            {
                if (!document.TryGetBody(request.LeftBodyId, out var left))
                {
                    return ApiMappings.NotFound($"Body '{request.LeftBodyId}' was not found.", "operations.boolean");
                }

                if (!document.TryGetBody(request.RightBodyId, out var right))
                {
                    return ApiMappings.NotFound($"Body '{request.RightBodyId}' was not found.", "operations.boolean");
                }

                var kernel = request.Operation.ToLowerInvariant() switch
                {
                    "union" => BrepBoolean.Union(left, right),
                    "subtract" => BrepBoolean.Subtract(left, right),
                    "intersect" => BrepBoolean.Intersect(left, right),
                    _ => null,
                };

                if (kernel is null)
                {
                    return ApiMappings.BadRequestFromMessage("Operation must be one of: union, subtract, intersect.", "operations.boolean");
                }

                if (!kernel.IsSuccess)
                {
                    return ApiMappings.KernelFailure(kernel.Diagnostics);
                }

                return ApiMappings.Ok(CreateBodyResponse(document, kernel.Value));
            }));

        documents.MapPost("/{documentId:guid}/bodies/{bodyId:guid}/transform", (Guid documentId, Guid bodyId, TranslateBodyRequestDto request, KernelDocumentStore store) =>
            WithDocument(store, documentId, document =>
            {
                if (request.Translation is null)
                {
                    return ApiMappings.BadRequestFromMessage("Translation must be provided.", "documents.body");
                }

                if (!document.ApplyBodyTranslation(bodyId, new Vector3D(request.Translation.X, request.Translation.Y, request.Translation.Z), out _))
                {
                    return ApiMappings.NotFound($"Body '{bodyId}' was not found.", "documents.body");
                }

                return ApiMappings.Ok(new BodyTransformedResponseDto(documentId, bodyId, request.Translation));
            }));

        documents.MapPost("/{documentId:guid}/bodies/{bodyId:guid}/tessellate", (Guid documentId, Guid bodyId, TessellateRequestDto? request, KernelDocumentStore store) =>
            WithDocumentBody(store, documentId, bodyId, (document, body) =>
            {
                var options = ApiMappings.BuildTessellationOptions(request?.Options);
                var kernel = BrepDisplayTessellator.Tessellate(body, options);
                if (!kernel.IsSuccess)
                {
                    return ApiMappings.KernelFailure(kernel.Diagnostics);
                }

                if (document.TryGetBodyTransform(bodyId, out var transform))
                {
                    return ApiMappings.Ok(ApiMappings.ToTessellationResponse(kernel.Value, transform));
                }

                return ApiMappings.Ok(ApiMappings.ToTessellationResponse(kernel.Value));
            }));

        documents.MapPost("/{documentId:guid}/bodies/{bodyId:guid}/pick", (Guid documentId, Guid bodyId, PickRequestDto request, KernelDocumentStore store) =>
            WithDocumentBody(store, documentId, bodyId, (document, body) =>
            {
                if (request.Direction is null)
                {
                    return ApiMappings.BadRequestFromMessage("Direction must be provided.", "bodies.pick");
                }

                if (!Direction3D.TryCreate(new Vector3D(request.Direction.X, request.Direction.Y, request.Direction.Z), out var direction))
                {
                    return ApiMappings.BadRequestFromMessage("Direction must be non-zero and finite.", "bodies.pick");
                }

                var ray = new Ray3D(new Point3D(request.Origin.X, request.Origin.Y, request.Origin.Z), direction);
                if (document.TryGetBodyTransform(bodyId, out var transform) && transform.TryInverse(out var inverse))
                {
                    ray = new Ray3D(inverse.Apply(ray.Origin), inverse.Apply(ray.Direction));
                }

                var kernel = BrepPicker.Pick(
                    body,
                    ray,
                    ApiMappings.BuildTessellationOptions(request.TessellationOptions),
                    ApiMappings.BuildPickOptions(request.PickOptions));

                if (!kernel.IsSuccess)
                {
                    return ApiMappings.KernelFailure(kernel.Diagnostics);
                }

                if (document.TryGetBodyTransform(bodyId, out transform))
                {
                    return ApiMappings.Ok(ApiMappings.ToPickResponse(kernel.Value, transform));
                }

                return ApiMappings.Ok(ApiMappings.ToPickResponse(kernel.Value));
            }));
    }

    private static IResult WithDocument(KernelDocumentStore store, Guid documentId, Func<DocumentSession, IResult> operation)
    {
        if (!store.TryGet(documentId, out var document))
        {
            return ApiMappings.NotFound($"Document '{documentId}' was not found.", "documents");
        }

        return operation(document);
    }

    private static IResult WithDocumentBody(KernelDocumentStore store, Guid documentId, Guid bodyId, Func<DocumentSession, BrepBody, IResult> operation)
        => WithDocument(store, documentId, document =>
        {
            if (!document.TryGetBody(bodyId, out var body))
            {
                return ApiMappings.NotFound($"Body '{bodyId}' was not found.", "documents.body");
            }

            return operation(document, body);
        });

    private static BodyCreatedResponseDto CreateBodyResponse(DocumentSession document, BrepBody body)
    {
        var bodyId = document.AddBody(body);
        return new BodyCreatedResponseDto(
            document.Id,
            bodyId,
            body.Topology.Faces.Count(),
            body.Topology.Edges.Count(),
            body.Topology.Vertices.Count());
    }

    private static bool TryCreateFrame(Point3Dto originDto, Vector3Dto normalDto, Vector3Dto uAxisDto, out ExtrudeFrame3D frame, out IResult error)
    {
        frame = default;
        error = Results.Empty;

        if (!Direction3D.TryCreate(new Vector3D(normalDto.X, normalDto.Y, normalDto.Z), out var normal))
        {
            error = ApiMappings.BadRequestFromMessage("Normal must be non-zero and finite.", "operations.extrude");
            return false;
        }

        if (!Direction3D.TryCreate(new Vector3D(uAxisDto.X, uAxisDto.Y, uAxisDto.Z), out var uAxis))
        {
            error = ApiMappings.BadRequestFromMessage("UAxis must be non-zero and finite.", "operations.extrude");
            return false;
        }

        try
        {
            frame = new ExtrudeFrame3D(new Point3D(originDto.X, originDto.Y, originDto.Z), normal, uAxis);
            return true;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            error = ApiMappings.BadRequestFromMessage(ex.Message, "operations.extrude");
            return false;
        }
    }
}
