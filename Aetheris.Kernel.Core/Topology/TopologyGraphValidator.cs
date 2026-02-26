using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Topology;

/// <summary>
/// M04 validator scope: reference integrity only (dangling IDs and local loop link consistency).
/// </summary>
public static class TopologyGraphValidator
{
    public static KernelResult<bool> Validate(TopologyModel model)
    {
        var diagnostics = new List<KernelDiagnostic>();

        foreach (var body in model.Bodies)
        {
            ValidateChildren(model, body.ShellIds, model.TryGetShell, $"Body {body.Id.Value}", diagnostics);
        }

        foreach (var shell in model.Shells)
        {
            ValidateChildren(model, shell.FaceIds, model.TryGetFace, $"Shell {shell.Id.Value}", diagnostics);
        }

        foreach (var face in model.Faces)
        {
            ValidateChildren(model, face.LoopIds, model.TryGetLoop, $"Face {face.Id.Value}", diagnostics);
        }

        foreach (var loop in model.Loops)
        {
            if (loop.CoedgeIds.Count == 0)
            {
                diagnostics.Add(Error($"Loop {loop.Id.Value} does not reference any coedges."));
            }

            ValidateChildren(model, loop.CoedgeIds, model.TryGetCoedge, $"Loop {loop.Id.Value}", diagnostics);

            foreach (var coedgeId in loop.CoedgeIds)
            {
                if (!model.TryGetCoedge(coedgeId, out var coedge) || coedge is null)
                {
                    continue;
                }

                if (coedge.LoopId != loop.Id)
                {
                    diagnostics.Add(Error($"Coedge {coedge.Id.Value} belongs to loop {coedge.LoopId.Value}, but is listed on loop {loop.Id.Value}."));
                }
            }
        }

        foreach (var edge in model.Edges)
        {
            if (!model.TryGetVertex(edge.StartVertexId, out _))
            {
                diagnostics.Add(Error($"Edge {edge.Id.Value} has missing start vertex {edge.StartVertexId.Value}."));
            }

            if (!model.TryGetVertex(edge.EndVertexId, out _))
            {
                diagnostics.Add(Error($"Edge {edge.Id.Value} has missing end vertex {edge.EndVertexId.Value}."));
            }
        }

        foreach (var coedge in model.Coedges)
        {
            if (!model.TryGetEdge(coedge.EdgeId, out _))
            {
                diagnostics.Add(Error($"Coedge {coedge.Id.Value} references missing edge {coedge.EdgeId.Value}."));
            }

            if (!model.TryGetLoop(coedge.LoopId, out _))
            {
                diagnostics.Add(Error($"Coedge {coedge.Id.Value} references missing loop {coedge.LoopId.Value}."));
            }

            if (!model.TryGetCoedge(coedge.NextCoedgeId, out var next) || next is null)
            {
                diagnostics.Add(Error($"Coedge {coedge.Id.Value} references missing next coedge {coedge.NextCoedgeId.Value}."));
            }
            else if (next.LoopId != coedge.LoopId)
            {
                diagnostics.Add(Error($"Coedge {coedge.Id.Value} next coedge {next.Id.Value} belongs to loop {next.LoopId.Value}, expected {coedge.LoopId.Value}."));
            }

            if (!model.TryGetCoedge(coedge.PrevCoedgeId, out var prev) || prev is null)
            {
                diagnostics.Add(Error($"Coedge {coedge.Id.Value} references missing prev coedge {coedge.PrevCoedgeId.Value}."));
            }
            else if (prev.LoopId != coedge.LoopId)
            {
                diagnostics.Add(Error($"Coedge {coedge.Id.Value} prev coedge {prev.Id.Value} belongs to loop {prev.LoopId.Value}, expected {coedge.LoopId.Value}."));
            }
        }

        return diagnostics.Any(d => d.Severity == KernelDiagnosticSeverity.Error)
            ? KernelResult<bool>.Failure(diagnostics)
            : KernelResult<bool>.Success(true, diagnostics);
    }

    private static void ValidateChildren<TId, TEntity>(
        TopologyModel model,
        IReadOnlyList<TId> childIds,
        TryGet<TId, TEntity> tryGet,
        string parent,
        ICollection<KernelDiagnostic> diagnostics)
        where TId : struct
        where TEntity : class
    {
        var seen = new HashSet<TId>();

        foreach (var childId in childIds)
        {
            if (!seen.Add(childId))
            {
                diagnostics.Add(Warning($"{parent} contains duplicate child reference {childId}."));
            }

            if (!tryGet(model, childId, out _))
            {
                diagnostics.Add(Error($"{parent} references missing child ID {childId}."));
            }
        }
    }

    private static KernelDiagnostic Error(string message) =>
        new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message);

    private static KernelDiagnostic Warning(string message) =>
        new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Warning, message);

    private delegate bool TryGet<TId, TEntity>(TopologyModel model, TId id, out TEntity? entity)
        where TId : struct
        where TEntity : class;
}
