using System;
using System.Collections.Generic;
using Rhino.Geometry;

namespace TreeThat.Generation
{
    /// <summary>
    /// One segment of the plant skeleton (trunk or branch).
    /// </summary>
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

    /// <summary>
    /// Procedural skeleton: trunk + recursive branches.
    /// </summary>
    public class PlantSkeleton
    {
        public List<BranchSegment> Segments { get; } = new List<BranchSegment>();
        public List<Point3d> LeafPoints { get; } = new List<Point3d>();
        public List<Vector3d> LeafNormals { get; } = new List<Vector3d>();

        public static PlantSkeleton Build(PlantParams p)
        {
            var skeleton = new PlantSkeleton();
            var rng = new Random(p.Seed);

            var trunkStart = Point3d.Origin;
            var trunkEnd = new Point3d(0, 0, p.Height);
            var trunk = new BranchSegment
            {
                Start = trunkStart,
                End = trunkEnd,
                StartRadius = p.BaseRadius,
                EndRadius = p.BaseRadius * p.RadiusRatio,
                Level = 0
            };
            skeleton.Segments.Add(trunk);

            Grow(skeleton, trunk, p, rng, 1);
            PlaceLeaves(skeleton, p, rng);

            return skeleton;
        }

        static void Grow(PlantSkeleton skeleton, BranchSegment parent, PlantParams p, Random rng, int level)
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

                Grow(skeleton, seg, p, rng, level + 1);
            }
        }

        static void PlaceLeaves(PlantSkeleton skeleton, PlantParams p, Random rng)
        {
            // Leaves on all levels above trunk; denser on higher levels
            foreach (var seg in skeleton.Segments)
            {
                if (seg.Level < 1 && p.BranchLevels > 0)
                    continue; // skip pure trunk unless no branches

                // Level weight: higher branches get more leaves
                double levelWeight = 0.4 + seg.Level * 0.45;
                if (seg.Level >= p.BranchLevels)
                    levelWeight *= 1.4;

                // Base count bumped (~2x previous)
                int leavesOnSeg = (int)Math.Round(
                    (6 + p.FoliageDensity * 14) * levelWeight);

                leavesOnSeg = Math.Max(2, leavesOnSeg);

                for (int i = 0; i < leavesOnSeg; i++)
                {
                    double t = 0.15 + rng.NextDouble() * 0.85;
                    var pt = seg.Start + seg.Direction * t;

                    var dir = seg.Direction;
                    if (dir.IsTiny())
                        continue;
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

                    // Slight upward bias for fuller crown
                    outward = outward + Vector3d.ZAxis * 0.25;
                    outward.Unitize();

                    pt += outward * (seg.EndRadius + p.LeafSize * (0.25 + rng.NextDouble() * 0.35));

                    skeleton.LeafPoints.Add(pt);
                    skeleton.LeafNormals.Add(outward);
                }
            }
        }
    }
}
