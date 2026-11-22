using Aetheris.Kernel.Core.Diagnostics;

namespace Aetheris.Kernel.Core.Step242;

/// <summary>
/// Rewrites topology and curve forms that ISO 10303-42 defines as equivalent to the ones the importer reads, so the
/// rest of the importer meets one form. Nothing here moves geometry.
/// <list type="bullet">
/// <item>INTERSECTION_CURVE is a SURFACE_CURVE whose surfaces meet along it; it is read as one.</item>
/// <item>FACE_SURFACE carries the same attributes as ADVANCED_FACE; the stricter geometry rules of ADVANCED_FACE
/// are the importer's own anyway.</item>
/// <item>ORIENTED_FACE is its face with, when its orientation is .F., the sense of the face and of every bound
/// reversed; the oriented face is written as that face, under its own id, and the face it orients is set aside.</item>
/// <item>RECTANGULAR_TRIMMED_SURFACE and CURVE_BOUNDED_SURFACE under a face are their basis surface: the face's own
/// bounds trim it. A rectangular trim that reverses one parameter direction (and so the normal) is left alone.</item>
/// <item>OFFSET_SURFACE and OFFSET_CURVE_3D at distance 0 are their basis.</item>
/// <item>SEAM_EDGE is an ORIENTED_EDGE that also names the pcurve of its side of the seam; it is read as that
/// oriented edge.</item>
/// <item>SUBEDGE is the part of its parent edge between its own vertices; when the parent is an EDGE_CURVE, it is
/// read as an EDGE_CURVE on the parent's curve and sense.</item>
/// <item>A SHELL_BASED_SURFACE_MODEL whose only shell is a CLOSED_SHELL bounds a solid; it is read as the
/// MANIFOLD_SOLID_BREP of that shell, with a warning.</item>
/// </list>
/// </summary>
internal static class Step242EquivalentForms
{
    public static IReadOnlyList<Step242ParsedEntity> Normalize(IReadOnlyList<Step242ParsedEntity> entities, List<KernelDiagnostic> notes, out IReadOnlySet<int> orientedAway)
    {
        var byId = new Dictionary<int, Step242ParsedEntity>();
        foreach (var entity in entities) byId.TryAdd(entity.Id, entity);
        var result = new List<Step242ParsedEntity>(entities.Count);
        var added = new List<Step242ParsedEntity>();
        var nextId = entities.Count == 0 ? 1 : entities.Max(e => e.Id) + 1;
        var setAside = new HashSet<int>();
        int faceSurfaces = 0, orientedFaces = 0;
        foreach (var entity in entities)
        {
            switch (entity.Instance)
            {
                case Step242SimpleEntityInstance { Constructor: { Name: "INTERSECTION_CURVE" } curve }:
                    result.Add(entity with { Instance = new Step242SimpleEntityInstance(curve with { Name = "SURFACE_CURVE" }) });
                    continue;
                case Step242ComplexEntityInstance complex when complex.Constructors.Any(c => c.Name == "INTERSECTION_CURVE"):
                    result.Add(entity with { Instance = new Step242ComplexEntityInstance(complex.Constructors.Select(c => c.Name == "INTERSECTION_CURVE" ? c with { Name = "SURFACE_CURVE" } : c).ToArray()) });
                    continue;
                case Step242SimpleEntityInstance { Constructor: { Name: "SEAM_EDGE" } seam } when seam.Arguments.Count == 6:
                    result.Add(entity with { Instance = new Step242SimpleEntityInstance(new("ORIENTED_EDGE", seam.Arguments.Take(5).ToArray())) });
                    continue;
                case Step242SimpleEntityInstance { Constructor: { Name: "SUBEDGE" } subedge }
                    when subedge.Arguments.Count == 4 && subedge.Arguments[3] is Step242EntityReference parentReference
                        && byId.TryGetValue(parentReference.TargetId, out var parent)
                        && parent.Instance is Step242SimpleEntityInstance { Constructor: { Name: "EDGE_CURVE", Arguments.Count: 5 } parentCurve }:
                    result.Add(entity with
                    {
                        Instance = new Step242SimpleEntityInstance(new("EDGE_CURVE",
                            [subedge.Arguments[0], subedge.Arguments[1], subedge.Arguments[2], parentCurve.Arguments[3], parentCurve.Arguments[4]])),
                    });
                    continue;
                case Step242SimpleEntityInstance { Constructor: { Name: "FACE_SURFACE" } face } when face.Arguments.Count == 4:
                    faceSurfaces++;
                    result.Add(entity with { Instance = new Step242SimpleEntityInstance(face with { Name = "ADVANCED_FACE" }) });
                    continue;
                case Step242SimpleEntityInstance { Constructor: { Name: "ORIENTED_FACE" } oriented }
                    when oriented.Arguments.Count == 4 && oriented.Arguments[2] is Step242EntityReference faceReference
                        && oriented.Arguments[3] is Step242BooleanValue orientation
                        && byId.TryGetValue(faceReference.TargetId, out var baseFace)
                        && baseFace.Instance is Step242SimpleEntityInstance { Constructor: { Name: "ADVANCED_FACE" or "FACE_SURFACE", Arguments.Count: 4 } baseConstructor }
                        && baseConstructor.Arguments[1] is Step242ListValue bounds && baseConstructor.Arguments[3] is Step242BooleanValue sameSense:
                {
                    orientedFaces++;
                    setAside.Add(baseFace.Id);
                    var newBounds = bounds;
                    if (!orientation.Value)
                    {
                        var reversed = new List<Step242Value>();
                        foreach (var item in bounds.Items)
                        {
                            if (item is not Step242EntityReference boundReference || !byId.TryGetValue(boundReference.TargetId, out var bound)
                                || bound.Instance is not Step242SimpleEntityInstance { Constructor: { Arguments.Count: 3 } boundConstructor }
                                || boundConstructor.Arguments[2] is not Step242BooleanValue boundSense)
                            { reversed = null; break; }
                            var clone = new Step242ParsedEntity(nextId++, new Step242SimpleEntityInstance(boundConstructor with
                            {
                                Arguments = [boundConstructor.Arguments[0], boundConstructor.Arguments[1], new Step242BooleanValue(!boundSense.Value)],
                            }));
                            added.Add(clone);
                            reversed.Add(new Step242EntityReference(clone.Id));
                        }
                        if (reversed is null) { result.Add(entity); setAside.Remove(baseFace.Id); orientedFaces--; continue; }
                        newBounds = new Step242ListValue(reversed);
                    }
                    var name = oriented.Arguments[0] is Step242StringValue ? oriented.Arguments[0] : baseConstructor.Arguments[0];
                    result.Add(entity with
                    {
                        Instance = new Step242SimpleEntityInstance(new("ADVANCED_FACE",
                            [name, newBounds, baseConstructor.Arguments[2], new Step242BooleanValue(sameSense.Value == orientation.Value)])),
                    });
                    continue;
                }
            }
            result.Add(entity);
        }
        if (faceSurfaces > 0)
            notes.Add(new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Info,
                $"{faceSurfaces} FACE_SURFACE face(s) are read as ADVANCED_FACE; their geometry is checked by the same rules.", "Importer.Topology.FaceSurface"));
        if (orientedFaces > 0)
            notes.Add(new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Info,
                $"{orientedFaces} ORIENTED_FACE reference(s) are read as the faces they orient, reversed where the orientation is .F..", "Importer.Topology.OrientedFace"));
        result.AddRange(added);

        // Surfaces and curves that are their basis: the referencing entity keeps its id and takes the basis's instance.
        var current = new Dictionary<int, Step242ParsedEntity>();
        foreach (var entity in result) current.TryAdd(entity.Id, entity);
        int trimmedSurfaces = 0, zeroOffsets = 0, surfaceModels = 0;
        for (var i = 0; i < result.Count; i++)
        {
            var entity = result[i];
            var target = entity;
            for (var depth = 0; depth < 16 && BasisOf(target) is { } basisId && current.TryGetValue(basisId, out var basis); depth++)
            {
                if (target.Name is "OFFSET_SURFACE" or "OFFSET_CURVE_3D") zeroOffsets++; else trimmedSurfaces++;
                target = basis;
            }
            if (!ReferenceEquals(target, entity)) result[i] = entity with { Instance = target.Instance };
            if (entity.Instance is Step242SimpleEntityInstance { Constructor: { Name: "SHELL_BASED_SURFACE_MODEL" } model }
                && model.Arguments.ElementAtOrDefault(1) is Step242ListValue { Items: [Step242EntityReference shellReference] }
                && current.TryGetValue(shellReference.TargetId, out var shell) && shell.Name == "CLOSED_SHELL")
            {
                surfaceModels++;
                result[i] = entity with { Instance = new Step242SimpleEntityInstance(new("MANIFOLD_SOLID_BREP", [model.Arguments[0], shellReference])) };
            }
        }
        if (trimmedSurfaces > 0)
            notes.Add(new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Info,
                $"{trimmedSurfaces} bounded surface(s) (RECTANGULAR_TRIMMED_SURFACE/CURVE_BOUNDED_SURFACE) are read as their basis surface; the faces' own bounds trim them.", "Importer.Geometry.BoundedSurface"));
        if (zeroOffsets > 0)
            notes.Add(new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Info,
                $"{zeroOffsets} offset curve(s)/surface(s) at distance 0 are read as their basis.", "Importer.Geometry.ZeroOffset"));
        if (surfaceModels > 0)
            notes.Add(new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Warning,
                $"{surfaceModels} SHELL_BASED_SURFACE_MODEL(s) of one closed shell are read as solids bounded by that shell.", "Importer.TopologyRoot.SurfaceModel"));
        orientedAway = setAside;
        if (added.Count == 0 && faceSurfaces == 0 && orientedFaces == 0 && trimmedSurfaces == 0 && zeroOffsets == 0 && surfaceModels == 0
            && result.SequenceEqual(entities, ReferenceEqualityComparer.Instance)) return entities;
        return result;
    }

    /// <summary>The basis an entity is identical to, if it is one of the pass-through forms.</summary>
    private static int? BasisOf(Step242ParsedEntity entity)
    {
        if (entity.Instance is not Step242SimpleEntityInstance { Constructor: var c }) return null;
        return c.Name switch
        {
            "RECTANGULAR_TRIMMED_SURFACE" when c.Arguments.Count == 8 && c.Arguments[1] is Step242EntityReference basis
                && c.Arguments[6] is Step242BooleanValue u && c.Arguments[7] is Step242BooleanValue v && u.Value == v.Value => basis.TargetId,
            "CURVE_BOUNDED_SURFACE" when c.Arguments.Count >= 2 && c.Arguments[1] is Step242EntityReference basis => basis.TargetId,
            "OFFSET_SURFACE" when c.Arguments.Count >= 3 && c.Arguments[1] is Step242EntityReference basis
                && c.Arguments[2] is Step242NumberValue { Value: 0 } => basis.TargetId,
            "OFFSET_CURVE_3D" when c.Arguments.Count >= 3 && c.Arguments[1] is Step242EntityReference basis
                && c.Arguments[2] is Step242NumberValue { Value: 0 } => basis.TargetId,
            _ => null,
        };
    }
}
