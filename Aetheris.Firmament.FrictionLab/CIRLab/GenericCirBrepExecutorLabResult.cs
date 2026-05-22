using Aetheris.Kernel.Core.Brep;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public enum GenericCirBrepLabStatus { Succeeded, Unsupported, Failed }

public sealed record GenericCirBrepExecutorLabResult(
    string Scenario,
    GenericCirBrepLabStatus Status,
    BrepBody? Body,
    string FailureCode,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> BooleanSequence,
    bool StepExportAttempted,
    bool StepExportSucceeded,
    IReadOnlyList<string> StepMarkers,
    bool HasSafeBooleanComposition,
    string ShellRootKind,
    int FaceCount);

public sealed record GenericCirBrepScenarioReport(
    IReadOnlyList<GenericCirBrepExecutorLabResult> Scenarios,
    IReadOnlyList<string> Diagnostics);
