using System;
using System.Collections.Generic;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace TreeThat.Generation
{
    /// <summary>
    /// Places repeated low-poly plant units along a curve (hedge / row).
    /// </summary>
    public static class HedgeAlongCurve
    {
        /// <summary>
        /// Interactive: pick a curve, then place plants using current params.
        /// Returns number of placed objects, or -1 on cancel/error.
        /// </summary>
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

        /// <summary>
        /// Places plant meshes along curve at given spacing.
        /// Slight random scale / rotation / seed per unit for a natural look.
        /// </summary>
        public static int Place(RhinoDoc doc, Curve curve, PlantParams baseParams, double spacing)
        {
            if (doc == null || curve == null)
                return 0;

            spacing = Math.Max(spacing, 0.2);
            double length = curve.GetLength();
            int count = Math.Max(1, (int)Math.Floor(length / spacing) + 1);

            var rng = new Random(baseParams.Seed);
            int placed = 0;

            // Even parameter distribution by arc length
            var points = curve.DivideByCount(Math.Max(1, count - 1), true, out Point3d[] pts);
            if (pts == null || pts.Length == 0)
            {
                // fallback single point
                pts = new[] { curve.PointAtStart };
            }

            for (int i = 0; i < pts.Length; i++)
            {
                var p = baseParams.Clone();
                p.Seed = baseParams.Seed + i * 17 + rng.Next(0, 50);

                // Small height / radius variation
                double scale = 0.85 + rng.NextDouble() * 0.3;
                p.Height *= scale;
                p.BaseRadius *= scale;
                p.LeafSize *= 0.9 + rng.NextDouble() * 0.2;

                var mesh = PlantGenerator.Generate(p);
                if (mesh == null || !mesh.IsValid)
                    continue;

                // Tangent frame at point
                if (!curve.ClosestPoint(pts[i], out double t))
                    t = curve.Domain.Min;

                var tangent = curve.TangentAt(t);
                if (tangent.IsTiny())
                    tangent = Vector3d.XAxis;
                tangent.Unitize();

                // Rotate around Z for yaw variety; keep upright
                double yaw = Math.Atan2(tangent.Y, tangent.X) + (rng.NextDouble() - 0.5) * 0.6;
                var xform = Transform.Rotation(yaw, Vector3d.ZAxis, Point3d.Origin)
                          * Transform.Translation(pts[i] - Point3d.Origin);

                mesh.Transform(xform);

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
