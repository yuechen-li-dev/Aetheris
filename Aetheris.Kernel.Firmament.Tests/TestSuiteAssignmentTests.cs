using System.Reflection;
using System.Text.Json;

namespace Aetheris.Kernel.Firmament.Tests;

/// <summary>
/// The affected-test lane (<c>scripts/test-lane.py</c>) runs this assembly by suite, so every test class names
/// exactly one suite from <c>scripts/test-suites.json</c>; an untagged class would never run in that lane.
/// </summary>
[Trait("Suite", "Language")]
public sealed class TestSuiteAssignmentTests
{
    [Fact]
    public void EveryTestClassNamesOneKnownSuite()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        Assert.NotNull(directory);
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory.FullName, "scripts", "test-suites.json")));
        var known = config.RootElement.GetProperty("firmamentSuites").EnumerateArray().Select(e => e.GetString()).ToHashSet();

        var problems = new List<string>();
        foreach (var type in typeof(TestSuiteAssignmentTests).Assembly.GetTypes())
        {
            var hasTests = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Any(m => m.GetCustomAttributes<FactAttribute>(inherit: true).Any());
            if (!hasTests || type.IsAbstract && !type.IsSealed) continue;
            var suites = new List<string?>();
            for (var t = type; t is not null; t = t.DeclaringType)
                suites.AddRange(t.GetCustomAttributesData()
                    .Where(a => a.AttributeType == typeof(TraitAttribute) && (string?)a.ConstructorArguments[0].Value == "Suite")
                    .Select(a => (string?)a.ConstructorArguments[1].Value));
            if (suites.Count != 1 || !known.Contains(suites[0])) problems.Add($"{type.FullName}: [{string.Join(", ", suites)}]");
        }
        Assert.True(problems.Count == 0, "Each test class needs one [Trait(\"Suite\", ...)] from scripts/test-suites.json:\n" + string.Join("\n", problems));
    }
}
