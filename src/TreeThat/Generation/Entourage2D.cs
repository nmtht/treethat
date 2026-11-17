using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;

namespace TreeThat.Generation
{
    /// <summary>
    /// Architectural plan (top) entourage — lobed canopy, trunk, inner line, branch fan.
    /// </summary>
    public static class Entourage2D
    {
        public static void Build(
            PlantSkeleton skeleton,
            PlantParams p,
            out List<Curve> lines,
            out List<Curve> colorBoundaries)
        {
            lines = new List<Curve>();
            colorBoundaries = new List<Curve>();
            if (skeleton == null) return;

            var rng = new Random(p.Seed + 19);

            // Base radius from projected branch tips
            double canopyR = EstimateCanopyRadius(skeleton, p);
            double trunkR = Math.Max(p.BaseRadius * 0.85, canopyR * 0.04);
            trunkR = Math.Min(trunkR, canopyR * 0.12);

            // ----- Trunk -----
            lines.Add(new ArcCurve(new Circle(Plane.WorldXY, Point3d.Origin, trunkR)));
            double cross = trunkR * 0.5;
            lines.Add(new LineCurve(new Point3d(-cross, 0, 0), new Point3d(cross, 0, 0)));
            lines.Add(new LineCurve(new Point3d(0, -cross, 0), new Point3d(0, cross, 0)));

            // ----- Lobed outer canopy (main graphic) -----
            int lobes = p.ConiferStyle ? 7 : 5 + rng.Next(0, 3);
            var outer = BuildLobedCanopy(canopyR, lobes, p.ConiferStyle, rng, irregularity: 0.18);
            if (outer != null)
            {
                lines.Add(outer.DuplicateCurve());
                colorBoundaries.Add(outer);
            }

            // ----- Inner canopy line (depth) -----
            var inner = BuildLobedCanopy(canopyR * 0.62, lobes, p.ConiferStyle, new Random(p.Seed + 91), irregularity: 0.12);
            if (inner != null)
                lines.Add(inner);

            // ----- Outer thin echo (optional double-stroke feel) -----
            var echo = BuildLobedCanopy(canopyR * 1.06, lobes, p.ConiferStyle, new Random(p.Seed + 3), irregularity: 0.1);
            if (echo != null)
                lines.Add(echo);

            // ----- Branch fan in plan (structure inside canopy) -----
            int spokes = p.ConiferStyle ? 8 : 6;
            for (int i = 0; i < spokes; i++)
            {
                double a = 2.0 * Math.PI * i / spokes + (rng.NextDouble() - 0.5) * 0.25;
                var dir = new Vector3d(Math.Cos(a), Math.Sin(a), 0);
                double len = canopyR * (0.55 + rng.NextDouble() * 0.28);
                var start = Point3d.Origin + dir * (trunkR * 1.15);
                var end = Point3d.Origin + dir * len;

                // Main spoke
                lines.Add(new LineCurve(start, end));

                // Small side twigs near tip
                if (!p.ConiferStyle && rng.NextDouble() > 0.35)
                {
                    var side = new Vector3d(-dir.Y, dir.X, 0);
                    double tw = canopyR * 0.12 * (0.7 + rng.NextDouble() * 0.5);
                    var mid = start + (end - start) * (0.55 + rng.NextDouble() * 0.25);
                    lines.Add(new LineCurve(mid, mid + side * tw));
                    if (rng.NextDouble() > 0.5)
                        lines.Add(new LineCurve(mid, mid - side * tw * 0.7));
                }
            }

            // ----- Conifer: star-like secondary outline -----
            if (p.ConiferStyle)
            {
                int tips = 6 + rng.Next(0, 3);
                var starPts = new List<Point3d>();
                for (int i = 0; i < tips * 2; i++)
                {
                    double a = Math.PI * i / tips;
                    double r = (i % 2 == 0) ? canopyR * 0.95 : canopyR * 0.55;
                    r *= 0.92 + rng.NextDouble() * 0.12;
                    starPts.Add(new Point3d(Math.Cos(a) * r, Math.Sin(a) * r, 0));
                }
                starPts.Add(starPts[0]);
                lines.Add(new PolylineCurve(starPts));
            }
        }

        static double EstimateCanopyRadius(PlantSkeleton skeleton, PlantParams p)
        {
            double maxR = 0;
            foreach (var seg in skeleton.Segments)
            {
                if (seg.Level < 1) continue;
                maxR = Math.Max(maxR, HorizontalRadius(seg.Start));
                maxR = Math.Max(maxR, HorizontalRadius(seg.End));
            }
            foreach (var lp in skeleton.LeafPoints)
                maxR = Math.Max(maxR, HorizontalRadius(lp));

            if (maxR < 1e-6)
                maxR = Math.Max(p.Height * 0.28, p.BaseRadius * 5);

            // Pad so outline sits outside geometry a bit
            return maxR * 1.08 + p.LeafSize * 0.5;
        }

        static double HorizontalRadius(Point3d pt)
        {
            return Math.Sqrt(pt.X * pt.X + pt.Y * pt.Y);
        }

        /// <summary>
        /// Closed organic / lobed plan canopy.
        /// </summary>
        static Curve? BuildLobedCanopy(double radius, int lobes, bool conifer, Random rng, double irregularity)
        {
            if (radius < 1e-6) return null;

            int samples = Math.Max(36, lobes * 12);
            var pts = new List<Point3d>();

            for (int i = 0; i < samples; i++)
            {
                double t = (double)i / samples;
                double angle = t * Math.PI * 2;

                // Lobe modulation
                double lobe = 0.5 + 0.5 * Math.Cos(angle * lobes);
                if (conifer)
                    lobe = 0.55 + 0.45 * Math.Pow(Math.Abs(Math.Cos(angle * lobes * 0.5)), 0.7);

                double jitter = 1.0 + (rng.NextDouble() - 0.5) * 2.0 * irregularity;
                double r = radius * (0.78 + 0.22 * lobe) * jitter;

                // Mild secondary wave for softness
                r *= 1.0 + 0.04 * Math.Sin(angle * (lobes * 2 + 1));

                pts.Add(new Point3d(Math.Cos(angle) * r, Math.Sin(angle) * r, 0));
            }
            pts.Add(pts[0]);

            var poly = new PolylineCurve(pts);
            var nurb = poly.ToNurbsCurve();
            if (nurb != null)
            {
                nurb.MakeClosed(1e-6);
                // Resample smooth
                int outN = samples;
                var smooth = new List<Point3d>();
                for (int i = 0; i < outN; i++)
                {
                    double u = nurb.Domain.ParameterAt((double)i / outN);
                    var pt = nurb.PointAt(u);
                    smooth.Add(new Point3d(pt.X, pt.Y, 0));
                }
                smooth.Add(smooth[0]);
                return new PolylineCurve(smooth);
            }
            return poly;
        }
    }
}
