using System;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input.Custom;

namespace TreeThat.Generation
{
    /// <summary>
    /// Places repeated low-poly plant units along a curve (hedge / row).
    /// </summary>
    public static class HedgeAlongCurve
    {
        public static int RunInteractive(RhinoDoc doc, PlantParams plantParams, double spacing)
        {
            if (doc == null)
                return -1;

            var go = new GetObject();
            go.SetCommandPrompt("Select curve for hedge path");
            go.GeometryFilter = ObjectType.Curve;
            go.SubObjectSelect = false;
            go.Get();
            if (go.CommandResult() != Rhino.Commands.Result.Success)
                return -1;

            var curve = go.Object(0).Curve();
            if (curve == null || curve.GetLength() < 1e-6)
            {
                RhinoApp.WriteLine("TreeThat: invalid curve");
                return -1;
            }

            return Place(doc, curve, plantParams, spacing);
        }

        public static int Place(RhinoDoc doc, Curve curve, PlantParams baseParams, double spacing)
        {
            if (doc == null || curve == null)
                return 0;

            spacing = Math.Max(spacing, 0.2);
            double length = curve.GetLength();
            int count = Math.Max(1, (int)Math.Floor(length / spacing) + 1);

            var rng = new Random(baseParams.Seed);
            int placed = 0;

            // DivideByCount returns parameter array; points via out
            curve.DivideByCount(Math.Max(1, count - 1), true, out Point3d[] pts);
            if (pts == null || pts.Length == 0)
                pts = new[] { curve.PointAtStart };

            for (int i = 0; i < pts.Length; i++)
            {
                var p = baseParams.Clone();
                p.Seed = baseParams.Seed + i * 17 + rng.Next(0, 50);

                double scale = 0.85 + rng.NextDouble() * 0.3;
                p.Height *= scale;
                p.BaseRadius *= scale;
                p.LeafSize *= 0.9 + rng.NextDouble() * 0.2;

                var mesh = PlantGenerator.Generate(p);
                if (mesh == null || !mesh.IsValid)
                    continue;

                // Yaw only (keep plant upright). MUST rotate around origin first, then translate.
                // Previous bug: Translation then Rotation-around-origin → arc scatter.
                double yaw = (rng.NextDouble() - 0.5) * 1.2;
                mesh.Transform(Transform.Rotation(yaw, Vector3d.ZAxis, Point3d.Origin));
                mesh.Transform(Transform.Translation(new Vector3d(pts[i])));

                var att = new ObjectAttributes
                {
                    Name = $"TT_Hedge_{baseParams.Seed}_{i}"
                };
                if (doc.Objects.AddMesh(mesh, att) != Guid.Empty)
                    placed++;
            }

            doc.Views.Redraw();
            return placed;
        }
    }
}
