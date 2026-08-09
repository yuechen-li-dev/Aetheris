using Aetheris.Kernel.Core.Brep;
using Aetheris.Forge.Abstractions;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Construction;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Forge.Extensions;

public static class ForgeCapabilityExecutor
{
    public static ForgeCapabilityExecutionResult Execute(
        ForgeExtensionRegistry registry,
        ForgeCapabilityId id,
        ForgeCapabilityInvocationContext context,
        ForgeCapabilityArguments arguments)
    {
        if (!registry.TryResolve(id, out var capability))
            return ForgeCapabilityExecutionResult.Failure(Error("forge-capability-missing", $"Capability '{id}' is not registered.", id, context));
        var binding = ValidateArguments(capability.Descriptor, arguments, context);
        if (binding.Count > 0) return new ForgeCapabilityExecutionResult(null, binding);
        if (!context.RequestedTargets.All(capability.Descriptor.SupportedTargets.Contains))
            return ForgeCapabilityExecutionResult.Failure(Error("forge-capability-lowering-target-unsupported", $"Capability '{id}' does not support all requested lowering targets.", id, context));
        try
        {
            var result = capability.Execute(context, arguments);
            if (!result.IsSuccess) return result;
            var contractDiagnostic = ValidateOutputContract(capability.Descriptor, result.Output!);
            if (contractDiagnostic is not null)
                return ForgeCapabilityExecutionResult.Failure(Error("forge-capability-output-contract-violation", contractDiagnostic, id, context));
            if (result.Output?.Construction is { } construction)
            {
                try { construction.Validate(); }
                catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
                {
                    return ForgeCapabilityExecutionResult.Failure(Error("forge-capability-construction-invalid", exception.Message, id, context));
                }
            }
            if (result.Output?.ExactBrep is { } exact)
            {
                var validation = BrepBindingValidator.Validate(exact, requireAllEdgeAndFaceBindings: true);
                if (!validation.IsSuccess)
                    return ForgeCapabilityExecutionResult.Failure(Error("forge-capability-brep-invalid", string.Join("; ", validation.Diagnostics.Select(diagnostic => diagnostic.Message)), id, context));
            }
            if (result.Output?.Provenance is null || !result.Output.Provenance.ContainsKey("capability"))
                return ForgeCapabilityExecutionResult.Failure(Error("forge-capability-provenance-missing", $"Capability '{id}' did not emit required capability provenance.", id, context));
            return result;
        }
        catch (ForgeCapabilityAdmissionException exception)
        {
            return ForgeCapabilityExecutionResult.Failure(Error("forge-capability-admission-rejected", exception.Message, id, context));
        }
        catch (Exception exception)
        {
            return ForgeCapabilityExecutionResult.Failure(Error("forge-capability-exception", $"Capability '{id}' failed: {exception.GetType().Name}: {exception.Message}", id, context));
        }
    }

    private static string? ValidateOutputContract(ForgeCapabilityDescriptorV1 descriptor, ForgeCapabilityOutput output) =>
        descriptor.OutputClassification switch
        {
            ForgeOutputClassification.ConstructionIr when output.Construction is null => "ConstructionIR capability did not emit a construction descriptor.",
            ForgeOutputClassification.ExactBrep when output.ExactBrep is null => "ExactBRep capability did not emit an exact BRep.",
            ForgeOutputClassification.SurfaceMeshDerived when output.ExactBrep is not null => "SurfaceMeshDerived output cannot masquerade as ExactBRep.",
            _ when output.ExactBrep is not null && string.IsNullOrWhiteSpace(descriptor.ExactnessContract) => "Exact BRep output requires a non-empty exactness contract.",
            _ => null,
        };

    public static BrepBody MaterializeConstruction(ContinuumConstructionDescriptor descriptor)
    {
        descriptor.Validate();
        if (descriptor.Sections.Count != 2)
            throw new ForgeCapabilityAdmissionException("M1 standard ConstructionIR materialization requires exactly two prismatic sections.");
        var lower = descriptor.Sections[0];
        var upper = descriptor.Sections[1];
        if (lower.ProfileVertices.Count != upper.ProfileVertices.Count
            || lower.ProfileVertices.Zip(upper.ProfileVertices).Any(pair => double.Abs(pair.First.X - pair.Second.X) > 1e-12d || double.Abs(pair.First.Y - pair.Second.Y) > 1e-12d))
            throw new ForgeCapabilityAdmissionException("M1 standard ConstructionIR materialization requires identical lower and upper profiles.");
        var profile = PolylineProfile2D.Create(lower.ProfileVertices.Select(point => new ProfilePoint2D(point.X, point.Y)).ToArray());
        if (!profile.IsSuccess) throw new ForgeCapabilityAdmissionException(string.Join("; ", profile.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var height = upper.AxialPosition - lower.AxialPosition;
        var body = BrepExtrude.Create(
            profile.Value,
            new ExtrudeFrame3D(
                new Point3D(0d, 0d, lower.AxialPosition),
                Direction3D.Create(new Vector3D(0d, 0d, 1d)),
                Direction3D.Create(new Vector3D(1d, 0d, 0d))),
            height);
        if (!body.IsSuccess) throw new ForgeCapabilityAdmissionException(string.Join("; ", body.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var validation = BrepBindingValidator.Validate(body.Value, true);
        if (!validation.IsSuccess) throw new ForgeCapabilityAdmissionException(string.Join("; ", validation.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return body.Value;
    }

    private static IReadOnlyList<ForgeExtensionDiagnostic> ValidateArguments(
        ForgeCapabilityDescriptorV1 descriptor,
        ForgeCapabilityArguments arguments,
        ForgeCapabilityInvocationContext context)
    {
        var diagnostics = new List<ForgeExtensionDiagnostic>();
        var schema = descriptor.Inputs.ToDictionary(input => input.Name, StringComparer.Ordinal);
        foreach (var required in descriptor.Inputs.Where(input => input.Required && input.DefaultValue is null))
            if (!arguments.Values.ContainsKey(required.Name)) diagnostics.Add(Error("forge-capability-parameter-missing", $"Required parameter '{required.Name}' is missing.", descriptor.Id, context));
        foreach (var argument in arguments.Values)
        {
            if (!schema.TryGetValue(argument.Key, out var parameter))
                diagnostics.Add(Error("forge-capability-parameter-unknown", $"Parameter '{argument.Key}' is not declared by capability '{descriptor.Id}'.", descriptor.Id, context));
            else if (parameter.Type != argument.Value.Type)
                diagnostics.Add(Error("forge-capability-parameter-mismatch", $"Parameter '{argument.Key}' expects {parameter.Type} but received {argument.Value.Type}.", descriptor.Id, context));
        }
        return diagnostics;
    }

    private static ForgeExtensionDiagnostic Error(string code, string message, ForgeCapabilityId id, ForgeCapabilityInvocationContext context) =>
        new(code, ForgeDiagnosticSeverity.Error, message, id.Value, context.SourceIdentity);
}
