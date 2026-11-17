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

            // Coverage-first foliage (volume fill + LOD modules)
            result.Leaves = FoliageBuilder.Build(skeleton, p);

            Entourage2D.Build(skeleton, p, out var lines, out var colorBounds);
            result.Lines2d = lines;
            result.ColorBoundaries = colorBounds;

            return result;
        }

        static Mesh? CreateTube(BranchSegment seg, int sides)
        {
            sides = System.Math.Max(3, System.Math.Min(sides, 8));
            var dir = seg.Direction;
            if (dir.IsTiny()) return null;
            dir.Unitize();

            Vector3d x, y;
            if (System.Math.Abs(dir.Z) < 0.9)
                x = Vector3d.CrossProduct(dir, Vector3d.ZAxis);
            else
                x = Vector3d.CrossProduct(dir, Vector3d.XAxis);
            x.Unitize();
            y = Vector3d.CrossProduct(dir, x);
            y.Unitize();

            var mesh = new Mesh();
            for (int i = 0; i < sides; i++)
            {
                double a = 2.0 * System.Math.PI * i / sides;
                mesh.Vertices.Add(seg.Start + (x * System.Math.Cos(a) + y * System.Math.Sin(a)) * seg.StartRadius);
            }
            for (int i = 0; i < sides; i++)
            {
                double a = 2.0 * System.Math.PI * i / sides;
                mesh.Vertices.Add(seg.End + (x * System.Math.Cos(a) + y * System.Math.Sin(a)) * seg.EndRadius);
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
