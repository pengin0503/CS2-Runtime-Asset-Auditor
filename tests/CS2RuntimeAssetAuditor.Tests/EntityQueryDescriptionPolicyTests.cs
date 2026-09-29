using System.Text.RegularExpressions;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

// Unity.Entities 1.x (game 1.6.2f1) reads the Length of EntityQueryDesc.All, Any and None without a null check,
// so a query description with a null list throws a NullReferenceException in CreateEntityQuery. The real-play log
// of 2026-09-29 showed this for every census query with an optional list.
public class EntityQueryDescriptionPolicyTests
{
    private static readonly Regex ListAssignment = new(@"\b(All|Any|None)\s*=\s*([^,}\r\n]+)", RegexOptions.Compiled);

    [Test]
    public void Query_descriptions_never_assign_a_list_that_can_be_null()
    {
        var root = FindRoot();
        var offenders = new List<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "src", "CS2RuntimeAssetAuditor"), "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            var start = 0;
            while ((start = source.IndexOf("new EntityQueryDesc", start, StringComparison.Ordinal)) >= 0)
            {
                var open = source.IndexOf('{', start);
                var close = MatchingBrace(source, open);
                var body = source.Substring(open, close - open + 1);
                foreach (Match match in ListAssignment.Matches(body))
                {
                    var value = match.Groups[2].Value.Trim();
                    // Array literals and null-coalesced values are safe; anything else can carry a null.
                    if (value.StartsWith("new", StringComparison.Ordinal) || value.Contains("??")) continue;
                    offenders.Add(Path.GetFileName(file) + ": " + match.Value.Trim());
                }
                start = close;
            }
        }
        Assert.That(offenders, Is.Empty);
    }

    [Test]
    public void Asset_census_and_capability_probe_build_queries_through_the_null_safe_helper()
    {
        var root = FindRoot();
        var integration = Path.Combine(root, "src", "CS2RuntimeAssetAuditor", "Assets", "GameIntegration");
        Assert.That(File.ReadAllText(Path.Combine(integration, "Census", "CensusAccess.cs")), Does.Contain("EntityQueryDescriptors.Create(all, any, none)"));
        Assert.That(File.ReadAllText(Path.Combine(integration, "Capabilities", "CapabilityProbe.cs")), Does.Contain("EntityQueryDescriptors.Create(all, any, none)"));
        var helper = File.ReadAllText(Path.Combine(integration, "EntityQueryDescriptors.cs"));
        foreach (var list in new[] { "all", "any", "none" })
            Assert.That(helper, Does.Contain(list + " ?? Array.Empty<ComponentType>()"), list);
    }

    private static int MatchingBrace(string source, int open)
    {
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return i;
        }
        throw new InvalidOperationException("Unbalanced braces after EntityQueryDesc.");
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CS2RuntimeAssetAuditor.sln")))
            directory = directory.Parent;
        Assert.That(directory, Is.Not.Null);
        return directory!.FullName;
    }
}
