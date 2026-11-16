namespace TreeThat.Generation
{
    /// <summary>
    /// Parameters that drive low-poly plant generation.
    /// </summary>
    public class PlantParams
    {
        /// <summary>Overall height of the plant in model units.</summary>
        public double Height { get; set; } = 5.0;

        /// <summary>Number of branching levels (0 = trunk only).</summary>
        public int BranchLevels { get; set; } = 2;

        /// <summary>How much each child branch is shorter than its parent (0.4 – 0.8).</summary>
        public double LengthRatio { get; set; } = 0.65;

        /// <summary>How much thinner each child branch is (0.5 – 0.8).</summary>
        public double RadiusRatio { get; set; } = 0.65;

        /// <summary>Base trunk radius at the ground.</summary>
        public double BaseRadius { get; set; } = 0.15;

        /// <summary>Number of child branches per parent (2 – 6).</summary>
        public int BranchesPerNode { get; set; } = 3;

        /// <summary>Spread angle of branches in degrees.</summary>
        public double BranchAngle { get; set; } = 35.0;

        /// <summary>Foliage density 0–1 (affects leaf count).</summary>
        public double FoliageDensity { get; set; } = 0.9;

        /// <summary>Size of a single leaf card.</summary>
        public double LeafSize { get; set; } = 0.35;

        /// <summary>Target maximum polygon count (soft budget).</summary>
        public int PolyBudget { get; set; } = 1500;

        /// <summary>Random seed for reproducible generation.</summary>
        public int Seed { get; set; } = 42;

        /// <summary>Radial segments for trunk/branch tubes (3–6 for low-poly).</summary>
        public int TubeSegments { get; set; } = 4;

        public PlantParams Clone()
        {
            return (PlantParams)MemberwiseClone();
        }
    }
}
