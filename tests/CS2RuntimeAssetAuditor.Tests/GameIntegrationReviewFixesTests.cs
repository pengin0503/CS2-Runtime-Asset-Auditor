using System.Linq;
using System.Text.RegularExpressions;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;
using CS2RuntimeAssetAuditor.Assets.GameIntegration.Prefabs;
using CS2RuntimeAssetAuditor.Core.Advisor;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

// Fixes for the review of the game integration against the CS2 1.6.2f1 managed assemblies (issues #14-#21).
// Where the behavior depends on game types that the pure tests cannot load, the source structure is checked; the
// game API the fixes rely on is checked by the adapter contract tests.
public class GameIntegrationReviewFixesTests
{
    [Test]
    public void Deep_inspection_never_instantiates_materials_it_cannot_release()
    {
        var reader = ReadSource("Assets/GameIntegration/Rendering/DeepInspectionReader.cs");
        var production = AllProductionSource();

        Assert.Multiple(() =>
        {
            // RenderPrefab.ReleaseMaterials() is a no-op in the game, so ObtainMaterials leaks a Material and its textures.
            Assert.That(Code(production), Does.Not.Contain("ObtainMaterials("));
            Assert.That(Code(production), Does.Not.Contain("ReleaseMaterials("));
            Assert.That(Code(production), Does.Not.Contain(".Load(-1"));
            Assert.That(reader, Does.Contain("GetTemplateMaterial()"));
            Assert.That(Count(reader, "surface.LoadProperties("), Is.EqualTo(Count(reader, "surface.UnloadProperties(")));
        });
    }

    [Test]
    public void Report_states_what_deep_inspection_materials_describe()
    {
        var report = ReadSource("Assets/Export/AuditReport.cs");
        Assert.That(report, Does.Contain("[DataMember(Name = \"basis\", Order = 6)]"));
        Assert.That(report, Does.Contain("\"surfaceTemplateAndAssetKeywords\""));
    }

    [Test]
    public void Census_copies_prefab_references_synchronously_instead_of_leaving_an_untracked_job()
    {
        var census = Code(ReadSource("Assets/GameIntegration/Census/CensusAccess.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(Code(AllProductionSource()), Does.Not.Contain("ToComponentDataListAsync"));
            Assert.That(census, Does.Not.Contain("JobHandle"));
            Assert.That(census, Does.Contain("ToComponentDataArray<PrefabRef>(Allocator.Persistent)"));
        });
    }

    [Test]
    public void Surface_virtual_texturing_state_is_reported_only_when_the_game_loaded_the_surface()
    {
        var reader = ReadSource("Assets/GameIntegration/Rendering/SurfaceAssetReader.cs");
        Assert.That(reader, Does.Match(
            @"var usingVt = loadedHere\s*\?\s*Observation<bool>\.Unavailable\(Availability\.NotApplicable"));
        Assert.That(Count(reader, "surface.LoadProperties("), Is.EqualTo(Count(reader, "surface.UnloadProperties(")));
    }

    [Test]
    public void Catalog_capture_and_census_reduction_are_bounded_by_the_frame_budget()
    {
        var system = ReadSource("Assets/GameIntegration/AssetAuditSystem.cs");
        var ui = ReadSource("Assets/UI/AssetAuditUISystem.cs");
        Assert.Multiple(() =>
        {
            Assert.That(system, Does.Contain("while (_catalog.IsWorking && stopwatch.Elapsed.TotalMilliseconds < _frameBudgetMs);"));
            Assert.That(system, Does.Contain("while (!census.ObjectReductionCompleted && stopwatch.Elapsed.TotalMilliseconds < _frameBudgetMs)"));
            Assert.That(system, Does.Contain("while (!census.NetworkReductionCompleted && stopwatch.Elapsed.TotalMilliseconds < _frameBudgetMs)"));
            Assert.That(ui, Does.Contain("RequestCensusScan(scanOptions, _uiSettings.FrameBudgetMs)"));
        });
    }

    [Test]
    public void Building_extensions_and_plants_are_their_own_peer_types()
    {
        var extension = PrefabClassifier.Classify(isBuildingExtension: true);
        var plant = PrefabClassifier.Classify(isPlant: true);
        Assert.Multiple(() =>
        {
            Assert.That(PrefabClassifier.GetTypeId(extension), Is.EqualTo("BuildingExtension"));
            Assert.That(extension & (PrefabTraits.Building | PrefabTraits.Prop), Is.EqualTo(PrefabTraits.None));
            Assert.That(PrefabClassifier.GetTypeId(plant), Is.EqualTo("Plant"));
            Assert.That(plant & PrefabTraits.Prop, Is.EqualTo(PrefabTraits.None));
        });
    }

    [Test]
    public void Catalog_classification_follows_the_static_object_hierarchy()
    {
        var access = Code(ReadSource("Assets/GameIntegration/Prefabs/PrefabCatalogAccess.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(access, Does.Contain("var isBuildingExtension = prefab is BuildingExtensionPrefab;"));
            Assert.That(access, Does.Contain("var isProp = isStaticObject && !isBuilding && !isBuildingExtension && !isTree && !isPlant;"));
        });
    }

    [Test]
    public void Graphics_quality_preset_feeds_the_ordered_quality_rule()
    {
        var preset = ReadSource("Advisor/GraphicsQualityPresetSetting.cs");
        var engine = ReadSource("Core/Advisor/RecommendationEngine.cs");
        var advisor = ReadSource("Advisor/AdvisorSystem.cs");
        Assert.Multiple(() =>
        {
            Assert.That(preset, Does.Contain("public const string SemanticTag = \"rendering.ordered-quality\";"));
            Assert.That(engine, Does.Contain("new PerformanceSettingRule(\"rendering.ordered-quality\""));
            Assert.That(preset, Does.Contain("ApplyBehavior = SettingApplyBehavior.ConfirmationRequired"));
            Assert.That(preset, Does.Contain(".Where(level => level != QualitySetting.Level.Custom)"));
            Assert.That(advisor, Does.Contain("new AdvisorCoordinator(() => Gateway.GetCatalog())"));
        });
    }

    [Test]
    public void A_custom_graphics_level_is_never_changed_by_a_recommendation()
    {
        var result = new RecommendationEngine().Build(
            new[] { new BottleneckObservation(BottleneckCategory.RenderingGpu, BottleneckSeverity.High, AdvisorConfidence.High, new[] { "gpu.frame.ms" }, "measured") },
            new[] { Preset("Custom") }).Single();
        Assert.That(result.Direction, Is.EqualTo(RecommendationDirection.NoRecommendation));
    }

    [TestCase("High", BottleneckSeverity.High, RecommendationDirection.LowerRecommended, "Medium")]
    [TestCase("VeryLow", BottleneckSeverity.High, RecommendationDirection.NoRecommendation, "VeryLow")]
    [TestCase("Medium", BottleneckSeverity.Low, RecommendationDirection.HeadroomAvailable, "High")]
    [TestCase("High", BottleneckSeverity.Low, RecommendationDirection.NoRecommendation, "High")]
    public void Graphics_quality_preset_moves_one_step(string current, BottleneckSeverity severity, RecommendationDirection direction, string next)
    {
        var result = new RecommendationEngine().Build(
            new[] { new BottleneckObservation(BottleneckCategory.RenderingGpu, severity, AdvisorConfidence.High, new[] { "gpu.frame.ms" }, "measured") },
            new[] { Preset(current) }).Single();
        Assert.That(result.Direction, Is.EqualTo(direction));
        Assert.That(result.RecommendedValue, Is.EqualTo(next));
    }

    [Test]
    public void Pause_detection_reads_the_selected_speed_instead_of_absent_members()
    {
        var probe = ReadSource("Profiling/RuntimeGameStateProbe.cs");
        Assert.Multiple(() =>
        {
            Assert.That(probe, Does.Contain("AutomaticCapturePolicy.IsPaused(simulationSystem.selectedSpeed)"));
            Assert.That(probe, Does.Contain("gameManager.isGameLoading"));
            Assert.That(probe, Does.Not.Contain("System.Reflection"));
            Assert.That(File.Exists(Path.Combine(SourceRoot(), "Core", "RuntimePauseStateReader.cs")), Is.False);
        });
    }

    // Matches the descriptor GraphicsQualityPresetSetting produces for the game's global quality levels.
    private static GameSettingDescriptor Preset(string value) => new GameSettingDescriptor
    {
        SettingId = "Game.Settings.GraphicsSettings::QualityPreset",
        Category = "graphics",
        DisplayName = "Graphics quality preset",
        ValueKind = SettingValueKind.Enumeration,
        CurrentValue = value,
        AllowedValues = new[] { "VeryLow", "Low", "Medium", "High" },
        SemanticTags = new[] { "rendering.ordered-quality" },
        IsReadable = true,
        IsUserFacing = true,
        IsWritable = true,
        HasSafeReversibleWritePath = true,
        CapabilityState = SettingCapabilityState.Available,
        ApplyBehavior = SettingApplyBehavior.ConfirmationRequired
    };

    private static int Count(string text, string value) => Regex.Matches(text, Regex.Escape(value)).Count;

    // Drops line comments so explanations that name the removed APIs do not count as uses.
    private static string Code(string source) =>
        string.Join("\n", source.Split('\n').Select(line =>
        {
            var comment = line.IndexOf("//", StringComparison.Ordinal);
            return comment < 0 ? line : line.Substring(0, comment);
        }).Where(line => !line.TrimStart().StartsWith("///", StringComparison.Ordinal)));

    private static string AllProductionSource()
    {
        var root = SourceRoot();
        return string.Join("\n", Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar).Any(segment => segment is "bin" or "obj"))
            .Select(File.ReadAllText));
    }

    private static string ReadSource(string relativePath) =>
        File.ReadAllText(Path.Combine(SourceRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string SourceRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CS2RuntimeAssetAuditor.sln")))
            directory = directory.Parent;
        Assert.That(directory, Is.Not.Null);
        return Path.Combine(directory!.FullName, "src", "CS2RuntimeAssetAuditor");
    }
}
