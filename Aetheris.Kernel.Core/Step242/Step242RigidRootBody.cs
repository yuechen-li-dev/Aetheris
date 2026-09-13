using Aetheris.Kernel.Core.Brep;

namespace Aetheris.Kernel.Core.Step242;

/// <summary>An exact rigid B-rep root recovered from a STEP serialization compound.</summary>
public sealed record Step242RigidRootBody(int StepEntityId, string StepEntityKind, BrepBody Body);
