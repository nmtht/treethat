using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;

namespace TreeThat.Generation
{
    public static class PlantGenerator
    {
        public static Mesh Generate(PlantParams p)
        {
            return GenerateDetailed(p).CombinedMesh();
        }

        public static PlantGeometry GenerateDetailed(PlantParams p)
        {
            var result = new PlantGeometry();
            var skeleton = PlantSkeleton.Build(p);

            // --- Branches (tubes) ---
            var branches = new Mesh();
            foreach (var seg in skeleton.Segments)
            {
                var tube = CreateTube(seg, p.TubeSegments);
                if (tube != null)
                    branches.Append(tube);
            }
            if (branches.Vertices.Count > 0)
            {
                branches.Normals.ComputeNormals();
                branches.Compact();
            }
            result.Branches = branches;

            // --- Foliage as whole-branch clusters (not individual leaves) ---
            result.Leaves = BuildFoliageClusters(skeleton, p);

            Entourage2D.Build(skeleton, p, out var lines, out var colorBounds);
            result.Lines2d = lines;
            result.ColorBoundaries = colorBounds;

            return result;
        }

        /// <summary>
        /// One low-poly "cloud" per outer branch — readable mass, stable polycount.
        /// </summary>
        static Mesh BuildFoliageClusters(PlantSkeleton skeleton, PlantParams p)
        {
            var leaves = new Mesh();
            var rng = new Random(p.Seed + 11);

            // Prefer outer segments (level >= 1); for conifers level 1–2
            var candidates = skeleton.Segments
                .Where(s => s.Level >= 1)
                .OrderByDescending(s => s.Level)
                .ThenByDescending(s => s.Length)
                .ToList();

            if (candidates.Count == 0)
                return leaves;

            // Density controls how many clusters, not thousands of cards
            int maxClusters = Math.Max(6, (int)(8 + p.FoliageDensity * 28));
            // Soft budget: each cluster ~ 24–32 tris
            int tubeFaces = skeleton.Segments.Count * p.TubeSegments * 2;
            int faceBudget = Math.Max(p.PolyBudget - tubeFaces, 200);
            int byBudget = Math.Max(4, faceBudget / 28);
            int clusterCount = Math.Min(candidates.Count, Math.Min(maxClusters, byBudget));

            // Spread selection across the list so we don't only take tips in one sector
            var selected = new List<BranchSegment>();
            if (clusterCount >= candidates.Count)
            {
                selected.AddRange(candidates);
            }
            else
            {
                double step = (double)candidates.Count / clusterCount;
                for (int i = 0; i < clusterCount; i++)
                {
                    int idx = Math.Min(candidates.Count - 1, (int)(i * step + rng.NextDouble() * step * 0.5));
                    selected.Add(candidates[idx]);
                }
            }

            int seed = p.Seed;
            foreach (var seg in selected)
            {
                var cluster = CreateBranchCluster(seg, p, seed++);
                if (cluster != null)
                    leaves.Append(cluster);
            }

            if (leaves.Vertices.Count > 0)
            {
                leaves.Normals.ComputeNormals();
                leaves.Compact();
            }
            return leaves;
        }

        /// <summary>
        /// Flattened diamond/ellipsoid cluster oriented along the branch.
        /// ~12–16 vertices, ~20–28 faces — one mass per branch.
        /// </summary>
        static Mesh? CreateBranchCluster(BranchSegment seg, PlantParams p, int seed)
        {
            var dir = seg.Direction;
            if (dir.IsTiny()) return null;
            dir.Unitize();

            var rng = new Random(seed);

            // Cluster sits on outer half of the branch
            double t0 = 0.35 + rng.NextDouble() * 0.25;
            var center = seg.Start + seg.Direction * t0;

            // Size from branch length + leaf size + density
            double along = seg.Length * (0.45 + p.FoliageDensity * 0.35) * (0.85 + rng.NextDouble() * 0.3);
            along = Math.Max(along, p.LeafSize * 1.2);
            double radial = Math.Max(p.LeafSize * (0.9 + p.FoliageDensity * 0.8), seg.EndRadius * 4);
            radial *= 0.85 + rng.NextDouble() * 0.35;
            double thickness = radial * (0.35 + rng.NextDouble() * 0.25);

            // Frame
            Vector3d x, y;
            if (Math.Abs(dir.Z) < 0.9)
                x = Vector3d.CrossProduct(dir, Vector3d.ZAxis);
            else
                x = Vector3d.CrossProduct(dir, Vector3d.XAxis);
            x.Unitize();
            y = Vector3d.CrossProduct(dir, x);
            y.Unitize();

            // Slight random twist
            double twist = rng.NextDouble() * Math.PI;
            var x2 = x * Math.Cos(twist) + y * Math.Sin(twist);
            var y2 = -x * Math.Sin(twist) + y * Math.Cos(twist);

            // Low-poly capsule-like: rings along dir
            int rings = 3;
            int sides = 6;
            var mesh = new Mesh();

            for (int r = 0; r <= rings; r++)
            {
                double u = (double)r / rings; // 0..1
                double alongOff = (u - 0.5) * along;
                // elliptical profile — fatter in the middle
                double profile = Math.Sin(u * Math.PI);
                profile = Math.Max(profile, 0.15);
                double rad = radial * profile;
                double th = thickness * profile;

                var ringCenter = center + dir * alongOff;
                for (int s = 0; s < sides; s++)
                {
                    double a = 2.0 * Math.PI * s / sides;
                    var offset = x2 * (Math.Cos(a) * rad) + y2 * (Math.Sin(a) * th);
                    // Lift slightly so clusters don't sink into branch
                    offset += Vector3d.ZAxis * (th * 0.15 * profile);
                    mesh.Vertices.Add(ringCenter + offset);
                }
            }

            // Caps (tips)
            int tipTop = mesh.Vertices.Add(center + dir * (along * 0.55));
            int tipBot = mesh.Vertices.Add(center - dir * (along * 0.55));

            // Side faces between rings
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int s1 = (s + 1) % sides;
                    int a = r * sides + s;
                    int b = r * sides + s1;
                    int c = (r + 1) * sides + s1;
                    int d = (r + 1) * sides + s;
                    mesh.Faces.AddFace(a, b, c);
                    mesh.Faces.AddFace(a, c, d);
                }
            }

            // Cap to last / first ring
            int last = rings * sides;
            for (int s = 0; s < sides; s++)
            {
                int s1 = (s + 1) % sides;
                mesh.Faces.AddFace(tipTop, last + s1, last + s);
                mesh.Faces.AddFace(tipBot, s, s1);
            }

            return mesh;
        }

        static Mesh? CreateTube(BranchSegment seg, int sides)
        {
            sides = Math.Max(3, Math.Min(sides, 8));
            var dir = seg.Direction;
            if (dir.IsTiny()) return null;
            dir.Unitize();

            Vector3d x, y;
            if (Math.Abs(dir.Z) < 0.9)
                x = Vector3d.CrossProduct(dir, Vector3d.ZAxis);
            else
                x = Vector3d.CrossProduct(dir, Vector3d.XAxis);
            x.Unitize();
            y = Vector3d.CrossProduct(dir, x);
            y.Unitize();

            var mesh = new Mesh();
            for (int i = 0; i < sides; i++)
            {
                double a = 2.0 * Math.PI * i / sides;
                mesh.Vertices.Add(seg.Start + (x * Math.Cos(a) + y * Math.Sin(a)) * seg.StartRadius);
            }
            for (int i = 0; i < sides; i++)
            {
                double a = 2.0 * Math.PI * i / sides;
                mesh.Vertices.Add(seg.End + (x * Math.Cos(a) + y * Math.Sin(a)) * seg.EndRadius);
            }

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                mesh.Faces.AddFace(i, next, next + sides);
                mesh.Faces.AddFace(i, next + sides, i + sides);
            }
            return mesh;
        }
    }
}
