using System.Text;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242CircleAuditHarnessTests
{
    [Fact]
    public void LocalCircleAuditHarness_RunsWhenStepPathProvided()
    {
        var stepPath = Environment.GetEnvironmentVariable("AETHERIS_AUDIT_STEP_PATH");
        if (string.IsNullOrWhiteSpace(stepPath) || !File.Exists(stepPath))
        {
            Console.WriteLine("Skipped: set AETHERIS_AUDIT_STEP_PATH to a local STEP file path.");
            return;
        }

        var auditPath = Environment.GetEnvironmentVariable("AETHERIS_CIRCLE_AUDIT_PATH");
        if (string.IsNullOrWhiteSpace(auditPath))
        {
            auditPath = Path.Combine(Path.GetTempPath(), "aetheris-circle-audit.local.jsonl");
            Environment.SetEnvironmentVariable("AETHERIS_CIRCLE_AUDIT_PATH", auditPath);
        }

        Environment.SetEnvironmentVariable("AETHERIS_CIRCLE_AUDIT", "1");
        CircleEdgeTrimAuditWriter.ReloadFromEnvironmentForTesting();

        var stepText = File.ReadAllText(stepPath, Encoding.UTF8);
        var import = Step242Importer.ImportBody(stepText);
        Assert.True(import.IsSuccess, string.Join(Environment.NewLine, import.Diagnostics.Select(d => d.Message)));

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);
        if (!tessellation.IsSuccess && tessellation.Diagnostics.All(d => !string.Equals(d.Source, "Viewer.Tessellation.CircleTrimAuditStop", StringComparison.Ordinal)))
        {
            Assert.True(tessellation.IsSuccess, string.Join(Environment.NewLine, tessellation.Diagnostics.Select(d => d.Message)));
        }

        var writer = CircleEdgeTrimAuditWriter.Instance;
        Console.WriteLine($"Circle audit summary: encountered={writer.CircleEdgesEncountered}, suspicious={writer.SuspiciousRecords}, records={writer.RecordsWritten}, path={writer.Path}");
    }
}
