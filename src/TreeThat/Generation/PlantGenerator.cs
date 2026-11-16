using System;
using System.Collections.Generic;
using Rhino.Geometry;

namespace TreeThat.Generation
{
    /// <summary>
    /// Builds a low-poly mesh from a plant skeleton.
    /// </summary>
    public static class PlantGenerator
    {
        public static Mesh Generate(PlantParams p)
        {
            var skeleton = PlantSkeleton.Build(p);
            var mesh = new Mesh();

            // Trunk + branches as low-poly tubes
            foreach (var seg in skeleton.Segments)
            {
                var tube = CreateTube(seg, p.TubeSegments);
                if (tube != null)
                    mesh.Append(tube);
            }

            // Leaf cards
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
                    mesh.Append(leaf);
            }

            mesh.Normals.ComputeNormals();
            mesh.Compact();
            return mesh;
        }

        static int EstimateMaxLeaves(PlantParams p, PlantSkeleton skeleton)
        {
            // Rough budget: each tube segment ~ TubeSegments*2 faces, each leaf = 2 faces
            int tubeFaces = skeleton.Segments.Count * p.TubeSegments * 2;
            int remaining = Math.Max(0, p.PolyBudget - tubeFaces);
            return remaining / 2; // 2 triangles per leaf card
        }

        /// <summary>
        /// Simple tapered tube (prism) along a segment.
        /// </summary>
        static Mesh? CreateTube(BranchSegment seg, int sides)
        {
            sides = Math.Max(3, Math.Min(sides, 8));

            var dir = seg.Direction;
            if (dir.IsTiny())
                return null;
            dir.Unitize();

            // Frame
            Vector3d x, y;
            if (Math.Abs(dir.Z) < 0.9)
                x = Vector3d.CrossProduct(dir, Vector3d.ZAxis);
            else
                x = Vector3d.CrossProduct(dir, Vector3d.XAxis);
            x.Unitize();
            y = Vector3d.CrossProduct(dir, x);
            y.Unitize();

            var mesh = new Mesh();
            var bottom = new Point3d[sides];
            var top = new Point3d[sides];

            for (int i = 0; i < sides; i++)
            {
                double a = 2.0 * Math.PI * i / sides;
                var offsetB = (x * Math.Cos(a) + y * Math.Sin(a)) * seg.StartRadius;
                var offsetT = (x * Math.Cos(a) + y * Math.Sin(a)) * seg.EndRadius;
                bottom[i] = seg.Start + offsetB;
                top[i] = seg.End + offsetT;
            }

            // Vertices: bottom then top
            for (int i = 0; i < sides; i++)
                mesh.Vertices.Add(bottom[i]);
            for (int i = 0; i < sides; i++)
                mesh.Vertices.Add(top[i]);

            // Side faces
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                int b0 = i;
                int b1 = next;
                int t0 = i + sides;
                int t1 = next + sides;

                // Two triangles
                mesh.Faces.AddFace(b0, b1, t1);
                mesh.Faces.AddFace(b0, t1, t0);
            }

            return mesh;
        }

        /// <summary>
        /// Single diamond / card leaf (2 triangles).
        /// </summary>
        static Mesh? CreateLeafCard(Point3d center, Vector3d normal, double size, int seed)
        {
            if (normal.IsTiny())
                normal = Vector3d.ZAxis;
            normal.Unitize();

            // Build local frame
            Vector3d x;
            if (Math.Abs(normal.Z) < 0.9)
                x = Vector3d.CrossProduct(normal, Vector3d.ZAxis);
            else
                x = Vector3d.CrossProduct(normal, Vector3d.XAxis);
            x.Unitize();
            var y = Vector3d.CrossProduct(normal, x);
            y.Unitize();

            // Slight random rotation for variety
            var rng = new Random(seed);
            double rot = rng.NextDouble() * Math.PI * 2;
            var x2 = x * Math.Cos(rot) + y * Math.Sin(rot);
            var y2 = -x * Math.Sin(rot) + y * Math.Cos(rot);

            double half = size * 0.5;
            // Diamond shape
            var p0 = center + x2 * half;           // right
            var p1 = center + y2 * half * 1.2;     // tip
            var p2 = center - x2 * half;           // left
            var p3 = center - y2 * half * 0.6;     // base

            var mesh = new Mesh();
            mesh.Vertices.Add(p0);
            mesh.Vertices.Add(p1);
            mesh.Vertices.Add(p2);
            mesh.Vertices.Add(p3);

            mesh.Faces.AddFace(0, 1, 2);
            mesh.Faces.AddFace(0, 2, 3);

            return mesh;
        }
    }
}
