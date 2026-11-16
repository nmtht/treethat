using System;
using Rhino.Geometry;

namespace TreeThat.Generation
{
    public static class PlantGenerator
    {
        /// <summary>Combined mesh (preview).</summary>
        public static Mesh Generate(PlantParams p)
        {
            return GenerateDetailed(p).CombinedMesh();
        }

        /// <summary>Full layered output for block baking.</summary>
        public static PlantGeometry GenerateDetailed(PlantParams p)
        {
            var result = new PlantGeometry();
            var skeleton = PlantSkeleton.Build(p);

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

            var leaves = new Mesh();
            int maxLeaves = EstimateMaxLeaves(p, skeleton);
            int leafCount = Math.Min(skeleton.LeafPoints.Count, maxLeaves);

            for (int i = 0; i < leafCount; i++)
            {
                var leaf = CreateLeafCard(
                    skeleton.LeafPoints[i],
                    skeleton.LeafNormals[i],
                    p.LeafSize,
                    p.Seed + i);
                if (leaf != null)
                    leaves.Append(leaf);
            }

            if (leaves.Vertices.Count > 0)
            {
                leaves.Normals.ComputeNormals();
                leaves.Compact();
            }
            result.Leaves = leaves;

            Entourage2D.Build(skeleton, p, out var lines, out var colorBounds);
            result.Lines2d = lines;
            result.ColorBoundaries = colorBounds;

            return result;
        }

        static int EstimateMaxLeaves(PlantParams p, PlantSkeleton skeleton)
        {
            int tubeFaces = skeleton.Segments.Count * p.TubeSegments * 2;
            int softBudget = Math.Max(p.PolyBudget, tubeFaces + 200);
            int remaining = Math.Max(softBudget - tubeFaces, (int)(p.PolyBudget * 0.7));
            return Math.Max(remaining / 2, 40);
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

        static Mesh? CreateLeafCard(Point3d center, Vector3d normal, double size, int seed)
        {
            if (normal.IsTiny()) normal = Vector3d.ZAxis;
            normal.Unitize();

            Vector3d x;
            if (Math.Abs(normal.Z) < 0.9)
                x = Vector3d.CrossProduct(normal, Vector3d.ZAxis);
            else
                x = Vector3d.CrossProduct(normal, Vector3d.XAxis);
            x.Unitize();
            var y = Vector3d.CrossProduct(normal, x);
            y.Unitize();

            var rng = new Random(seed);
            double rot = rng.NextDouble() * Math.PI * 2;
            var x2 = x * Math.Cos(rot) + y * Math.Sin(rot);
            var y2 = -x * Math.Sin(rot) + y * Math.Cos(rot);

            double half = size * 0.5;
            var mesh = new Mesh();
            mesh.Vertices.Add(center + x2 * half);
            mesh.Vertices.Add(center + y2 * half * 1.2);
            mesh.Vertices.Add(center - x2 * half);
            mesh.Vertices.Add(center - y2 * half * 0.6);
            mesh.Faces.AddFace(0, 1, 2);
            mesh.Faces.AddFace(0, 2, 3);
            return mesh;
        }
    }
}
