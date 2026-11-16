using System;
using System.Collections.Generic;
using Rhino.Geometry;

namespace TreeThat.Generation
{
    public class BranchSegment
    {
        public Point3d Start { get; set; }
        public Point3d End { get; set; }
        public double StartRadius { get; set; }
        public double EndRadius { get; set; }
        public int Level { get; set; }

        public Vector3d Direction => End - Start;
        public double Length => Start.DistanceTo(End);
    }

    public class PlantSkeleton
    {
        public List<BranchSegment> Segments { get; } = new List<BranchSegment>();
        public List<Point3d> LeafPoints { get; } = new List<Point3d>();
        public List<Vector3d> LeafNormals { get; } = new List<Vector3d>();

        public static PlantSkeleton Build(PlantParams p)
        {
            var skeleton = new PlantSkeleton();
            var rng = new Random(p.Seed);

            var trunk = new BranchSegment
            {
                Start = Point3d.Origin,
                End = new Point3d(0, 0, p.Height),
                StartRadius = p.BaseRadius,
                EndRadius = Math.Max(p.BaseRadius * p.RadiusRatio * 0.5, 0.02),
                Level = 0
            };
            skeleton.Segments.Add(trunk);

            if (p.ConiferStyle)
                GrowConifer(skeleton, trunk, p, rng);
            else
                GrowDeciduous(skeleton, trunk, p, rng, 1);

            PlaceLeaves(skeleton, p, rng);
            return skeleton;
        }

        /// <summary>
        /// Fir/spruce/pine: whorls of near-horizontal branches along the trunk,
        /// length shrinks toward the tip → cone.
        /// </summary>
        static void GrowConifer(PlantSkeleton skeleton, BranchSegment trunk, PlantParams p, Random rng)
        {
            int tiers = Math.Max(3, p.BranchLevels + 2);
            int perWhorl = Math.Max(4, p.BranchesPerNode);

            // Clear trunk bottom ~12%, tip ~5%
            for (int tier = 0; tier < tiers; tier++)
            {
                double t = 0.12 + (0.83 * tier / Math.Max(1, tiers - 1));
                // Wider at bottom, pointy top
                double cone = 1.0 - t;
                cone = Math.Pow(Math.Max(cone, 0.05), 0.85);

                double baseLen = p.Height * 0.42 * cone * (0.9 + rng.NextDouble() * 0.2);
                var attach = trunk.Start + trunk.Direction * t;
                double radiusAt = trunk.StartRadius * (1.0 - t * 0.7);

                // Slight droop: below horizontal
                double elev = (-8.0 + rng.NextDouble() * 12.0) * Math.PI / 180.0;

                for (int i = 0; i < perWhorl; i++)
                {
                    double azimuth = 2.0 * Math.PI * i / perWhorl
                                   + (rng.NextDouble() - 0.5) * 0.25
                                   + tier * 0.35;

                    var dir = new Vector3d(
                        Math.Cos(azimuth) * Math.Cos(elev),
                        Math.Sin(azimuth) * Math.Cos(elev),
                        Math.Sin(elev));
                    dir.Unitize();

                    double len = baseLen * (0.85 + rng.NextDouble() * 0.3);
                    var end = attach + dir * len;

                    var primary = new BranchSegment
                    {
                        Start = attach,
                        End = end,
                        StartRadius = Math.Max(radiusAt * 0.35, 0.015),
                        EndRadius = Math.Max(radiusAt * 0.12, 0.008),
                        Level = 1
                    };
                    skeleton.Segments.Add(primary);

                    // Short secondary needles/branchlets along primary
                    int secondaries = 2 + (int)(p.FoliageDensity * 3);
                    for (int s = 0; s < secondaries; s++)
                    {
                        double st = 0.25 + 0.7 * s / Math.Max(1, secondaries - 1);
                        var sAttach = primary.Start + primary.Direction * st;
                        double sLen = len * 0.22 * (0.7 + rng.NextDouble() * 0.5);

                        // Sideways from primary, slight down
                        var side = Vector3d.CrossProduct(dir, Vector3d.ZAxis);
                        if (side.IsTiny())
                            side = Vector3d.CrossProduct(dir, Vector3d.XAxis);
                        side.Unitize();
                        double flip = (s % 2 == 0) ? 1.0 : -1.0;
                        var sDir = side * flip + dir * 0.3 - Vector3d.ZAxis * 0.15;
                        sDir.Unitize();

                        skeleton.Segments.Add(new BranchSegment
                        {
                            Start = sAttach,
                            End = sAttach + sDir * sLen,
                            StartRadius = primary.EndRadius * 0.8,
                            EndRadius = primary.EndRadius * 0.4,
                            Level = 2
                        });
                    }
                }
            }
        }

        static void GrowDeciduous(PlantSkeleton skeleton, BranchSegment parent, PlantParams p, Random rng, int level)
        {
            if (level > p.BranchLevels)
                return;

            var parentDir = parent.Direction;
            if (parentDir.IsTiny())
                return;
            parentDir.Unitize();

            Vector3d xAxis, yAxis;
            if (Math.Abs(parentDir.Z) < 0.9)
                xAxis = Vector3d.CrossProduct(parentDir, Vector3d.ZAxis);
            else
                xAxis = Vector3d.CrossProduct(parentDir, Vector3d.XAxis);
            xAxis.Unitize();
            yAxis = Vector3d.CrossProduct(parentDir, xAxis);
            yAxis.Unitize();

            int count = p.BranchesPerNode;
            double baseAngle = p.BranchAngle * Math.PI / 180.0;

            for (int i = 0; i < count; i++)
            {
                double azimuth = (2.0 * Math.PI * i / count) + (rng.NextDouble() - 0.5) * 0.4;
                double polar = baseAngle * (0.8 + rng.NextDouble() * 0.4);

                var dir = parentDir * Math.Cos(polar)
                        + (xAxis * Math.Cos(azimuth) + yAxis * Math.Sin(azimuth)) * Math.Sin(polar);
                dir.Unitize();

                double len = parent.Length * p.LengthRatio * (0.85 + rng.NextDouble() * 0.3);
                double startR = parent.EndRadius;
                double endR = startR * p.RadiusRatio;

                double t = 0.55 + rng.NextDouble() * 0.4;
                var start = parent.Start + parent.Direction * t;
                var end = start + dir * len;

                var seg = new BranchSegment
                {
                    Start = start,
                    End = end,
                    StartRadius = startR,
                    EndRadius = Math.Max(endR, 0.01),
                    Level = level
                };
                skeleton.Segments.Add(seg);
                GrowDeciduous(skeleton, seg, p, rng, level + 1);
            }
        }

        static void PlaceLeaves(PlantSkeleton skeleton, PlantParams p, Random rng)
        {
            foreach (var seg in skeleton.Segments)
            {
                if (seg.Level < 1 && p.BranchLevels > 0 && !p.ConiferStyle)
                    continue;

                if (p.ConiferStyle && seg.Level < 1)
                    continue;

                double levelWeight = p.ConiferStyle
                    ? (seg.Level == 1 ? 1.0 : 1.3)
                    : (0.4 + seg.Level * 0.45);

                if (!p.ConiferStyle && seg.Level >= p.BranchLevels)
                    levelWeight *= 1.4;

                int leavesOnSeg = (int)Math.Round(
                    (p.ConiferStyle ? 4 : 6 + p.FoliageDensity * 14) * levelWeight * (0.5 + p.FoliageDensity));

                leavesOnSeg = Math.Max(p.ConiferStyle ? 1 : 2, leavesOnSeg);

                for (int i = 0; i < leavesOnSeg; i++)
                {
                    double t = 0.15 + rng.NextDouble() * 0.85;
                    var pt = seg.Start + seg.Direction * t;

                    var dir = seg.Direction;
                    if (dir.IsTiny()) continue;
                    dir.Unitize();

                    Vector3d side;
                    if (Math.Abs(dir.Z) < 0.9)
                        side = Vector3d.CrossProduct(dir, Vector3d.ZAxis);
                    else
                        side = Vector3d.CrossProduct(dir, Vector3d.XAxis);
                    side.Unitize();

                    double spin = rng.NextDouble() * Math.PI * 2;
                    var outward = side * Math.Cos(spin)
                                + Vector3d.CrossProduct(dir, side) * Math.Sin(spin);
                    outward.Unitize();

                    if (!p.ConiferStyle)
                    {
                        outward = outward + Vector3d.ZAxis * 0.25;
                        outward.Unitize();
                    }

                    pt += outward * (seg.EndRadius + p.LeafSize * (0.2 + rng.NextDouble() * 0.35));

                    skeleton.LeafPoints.Add(pt);
                    skeleton.LeafNormals.Add(outward);
                }
            }
        }
    }
}
