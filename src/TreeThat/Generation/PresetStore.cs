using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TreeThat.Generation
{
    public class PlantPreset
    {
        public string Name { get; set; } = "Unnamed";
        public string Category { get; set; } = "Tree";
        public string Description { get; set; } = "";

        public double Height { get; set; } = 5.0;
        public int BranchLevels { get; set; } = 2;
        public double LengthRatio { get; set; } = 0.65;
        public double RadiusRatio { get; set; } = 0.65;
        public double BaseRadius { get; set; } = 0.15;
        public int BranchesPerNode { get; set; } = 3;
        public double BranchAngle { get; set; } = 35.0;
        public double FoliageDensity { get; set; } = 0.85;
        public double LeafSize { get; set; } = 0.35;
        public int PolyBudget { get; set; } = 1200;
        public int Seed { get; set; } = 42;
        public int TubeSegments { get; set; } = 4;

        public PlantParams ToParams()
        {
            return new PlantParams
            {
                Height = Height,
                BranchLevels = BranchLevels,
                LengthRatio = LengthRatio,
                RadiusRatio = RadiusRatio,
                BaseRadius = BaseRadius,
                BranchesPerNode = BranchesPerNode,
                BranchAngle = BranchAngle,
                FoliageDensity = FoliageDensity,
                LeafSize = LeafSize,
                PolyBudget = PolyBudget,
                Seed = Seed,
                TubeSegments = TubeSegments
            };
        }

        public static PlantPreset FromParams(string name, string category, string description, PlantParams p)
        {
            return new PlantPreset
            {
                Name = name,
                Category = category,
                Description = description,
                Height = p.Height,
                BranchLevels = p.BranchLevels,
                LengthRatio = p.LengthRatio,
                RadiusRatio = p.RadiusRatio,
                BaseRadius = p.BaseRadius,
                BranchesPerNode = p.BranchesPerNode,
                BranchAngle = p.BranchAngle,
                FoliageDensity = p.FoliageDensity,
                LeafSize = p.LeafSize,
                PolyBudget = p.PolyBudget,
                Seed = p.Seed,
                TubeSegments = p.TubeSegments
            };
        }
    }

    public static class PresetStore
    {
        static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static IReadOnlyList<PlantPreset> BuiltIn { get; } = CreateBuiltIn();

        public static IEnumerable<PlantPreset> All()
        {
            foreach (var p in BuiltIn)
                yield return p;
            foreach (var p in LoadUserPresets())
                yield return p;
        }

        public static string UserPresetsDirectory
        {
            get
            {
                string root;
                if (OperatingSystem.IsMacOS())
                    root = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        "Library", "Application Support", "TreeThat", "presets");
                else
                    root = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "TreeThat", "presets");

                Directory.CreateDirectory(root);
                return root;
            }
        }

        public static void SaveUserPreset(PlantPreset preset)
        {
            if (preset == null || string.IsNullOrWhiteSpace(preset.Name))
                return;

            var safe = string.Join("_", preset.Name.Split(Path.GetInvalidFileNameChars()));
            var path = Path.Combine(UserPresetsDirectory, safe + ".json");
            File.WriteAllText(path, JsonSerializer.Serialize(preset, JsonOptions));
        }

        public static List<PlantPreset> LoadUserPresets()
        {
            var list = new List<PlantPreset>();
            try
            {
                foreach (var file in Directory.EnumerateFiles(UserPresetsDirectory, "*.json"))
                {
                    try
                    {
                        var p = JsonSerializer.Deserialize<PlantPreset>(File.ReadAllText(file), JsonOptions);
                        if (p != null && !string.IsNullOrWhiteSpace(p.Name))
                            list.Add(p);
                    }
                    catch { }
                }
            }
            catch { }

            return list.OrderBy(p => p.Category).ThenBy(p => p.Name).ToList();
        }

        static List<PlantPreset> CreateBuiltIn()
        {
            return new List<PlantPreset>
            {
                new PlantPreset
                {
                    Name = "Oak",
                    Category = "Tree",
                    Description = "Tall broad canopy, strong trunk",
                    Height = 8.0,
                    BranchLevels = 3,
                    LengthRatio = 0.62,
                    RadiusRatio = 0.68,
                    BaseRadius = 0.28,
                    BranchesPerNode = 3,
                    BranchAngle = 38,
                    FoliageDensity = 1.0,
                    LeafSize = 0.48,
                    PolyBudget = 2200,
                    Seed = 101,
                    TubeSegments = 5
                },
                new PlantPreset
                {
                    Name = "Pine",
                    Category = "Conifer",
                    Description = "Narrow conical silhouette, dense small needles",
                    Height = 10.0,
                    BranchLevels = 4,
                    LengthRatio = 0.55,
                    RadiusRatio = 0.72,
                    BaseRadius = 0.18,
                    BranchesPerNode = 4,
                    BranchAngle = 22,
                    FoliageDensity = 1.0,
                    LeafSize = 0.2,
                    PolyBudget = 2400,
                    Seed = 202,
                    TubeSegments = 4
                },
                // Ель / пихта — denser horizontal tiers than Pine
                new PlantPreset
                {
                    Name = "Fir",
                    Category = "Conifer",
                    Description = "Spruce/fir — layered horizontal branches, dense crown",
                    Height = 9.0,
                    BranchLevels = 5,
                    LengthRatio = 0.58,
                    RadiusRatio = 0.75,
                    BaseRadius = 0.2,
                    BranchesPerNode = 5,
                    BranchAngle = 28,
                    FoliageDensity = 1.0,
                    LeafSize = 0.16,
                    PolyBudget = 2800,
                    Seed = 212,
                    TubeSegments = 4
                },
                new PlantPreset
                {
                    Name = "Bush",
                    Category = "Shrub",
                    Description = "Low wide mass, many short branches",
                    Height = 1.8,
                    BranchLevels = 2,
                    LengthRatio = 0.75,
                    RadiusRatio = 0.7,
                    BaseRadius = 0.08,
                    BranchesPerNode = 4,
                    BranchAngle = 55,
                    FoliageDensity = 1.0,
                    LeafSize = 0.24,
                    PolyBudget = 1400,
                    Seed = 303,
                    TubeSegments = 3
                },
                // Plant shape for hedges along a curve
                new PlantPreset
                {
                    Name = "Hedge Unit",
                    Category = "Hedge",
                    Description = "Dense low unit for hedges along a curve",
                    Height = 1.4,
                    BranchLevels = 2,
                    LengthRatio = 0.7,
                    RadiusRatio = 0.72,
                    BaseRadius = 0.1,
                    BranchesPerNode = 4,
                    BranchAngle = 50,
                    FoliageDensity = 1.0,
                    LeafSize = 0.2,
                    PolyBudget = 600,
                    Seed = 909,
                    TubeSegments = 3
                },
                new PlantPreset
                {
                    Name = "Dry Tree",
                    Category = "Tree",
                    Description = "Sparse branches, almost no foliage",
                    Height = 6.0,
                    BranchLevels = 3,
                    LengthRatio = 0.7,
                    RadiusRatio = 0.6,
                    BaseRadius = 0.2,
                    BranchesPerNode = 2,
                    BranchAngle = 42,
                    FoliageDensity = 0.2,
                    LeafSize = 0.25,
                    PolyBudget = 600,
                    Seed = 404,
                    TubeSegments = 4
                },
                new PlantPreset
                {
                    Name = "Sapling",
                    Category = "Tree",
                    Description = "Thin young tree, light canopy",
                    Height = 3.2,
                    BranchLevels = 2,
                    LengthRatio = 0.68,
                    RadiusRatio = 0.65,
                    BaseRadius = 0.06,
                    BranchesPerNode = 2,
                    BranchAngle = 28,
                    FoliageDensity = 0.75,
                    LeafSize = 0.3,
                    PolyBudget = 800,
                    Seed = 505,
                    TubeSegments = 3
                },
                new PlantPreset
                {
                    Name = "Weeping",
                    Category = "Tree",
                    Description = "Drooping long branches, soft mass",
                    Height = 5.5,
                    BranchLevels = 3,
                    LengthRatio = 0.8,
                    RadiusRatio = 0.7,
                    BaseRadius = 0.16,
                    BranchesPerNode = 3,
                    BranchAngle = 58,
                    FoliageDensity = 1.0,
                    LeafSize = 0.34,
                    PolyBudget = 2000,
                    Seed = 606,
                    TubeSegments = 4
                },
                new PlantPreset
                {
                    Name = "Palm-ish",
                    Category = "Exotic",
                    Description = "Long trunk, foliage mostly at the top",
                    Height = 9.0,
                    BranchLevels = 1,
                    LengthRatio = 0.45,
                    RadiusRatio = 0.55,
                    BaseRadius = 0.22,
                    BranchesPerNode = 5,
                    BranchAngle = 48,
                    FoliageDensity = 0.95,
                    LeafSize = 0.75,
                    PolyBudget = 1200,
                    Seed = 707,
                    TubeSegments = 5
                },
                new PlantPreset
                {
                    Name = "Stylized Low",
                    Category = "Stylized",
                    Description = "Minimal poly showcase — chunky and readable",
                    Height = 4.5,
                    BranchLevels = 1,
                    LengthRatio = 0.6,
                    RadiusRatio = 0.7,
                    BaseRadius = 0.25,
                    BranchesPerNode = 3,
                    BranchAngle = 40,
                    FoliageDensity = 0.7,
                    LeafSize = 0.55,
                    PolyBudget = 350,
                    Seed = 808,
                    TubeSegments = 3
                }
            };
        }
    }
}
