using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;

namespace TreeThat.Generation
{
    /// <summary>
    /// 2D plan (top view) entourage: trunk mark + canopy outline + hatch boundary.
    /// Projection: XY plane.
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

            if (skeleton == null)
                return;

            // --- Trunk mark at origin (plan) ---
            double trunkR = Math.Max(p.BaseRadius * 0.9, 0.03);
            var trunkCircle = new Circle(Plane.WorldXY, Point3d.Origin, trunkR);
            lines.Add(new ArcCurve(trunkCircle));

            // Small cross inside trunk for readability at small scale
            double c = trunkR * 0.55;
            lines.Add(new LineCurve(new Point3d(-c, 0, 0), new Point3d(c, 0, 0)));
            lines.Add(new LineCurve(new Point3d(0, -c, 0), new Point3d(0, c, 0)));

            // --- Canopy points from branches + leaves (XY) ---
            var canopyPts = new List<Point3d>();

            foreach (var seg in skeleton.Segments)
            {
                if (seg.Level < 1)
                    continue;

                int n = 3;
                for (int i = 0; i <= n; i++)
                {
                    double t = (double)i / n;
                    var pt = seg.Start + seg.Direction * t;
                    canopyPts.Add(ProjectXY(pt));
                    // slight radial push by local radius
                    var flat = ProjectXY(pt);
                    var radial = new Vector3d(flat.X, flat.Y, 0);
                    if (!radial.IsTiny())
                    {
                        radial.Unitize();
                        canopyPts.Add(flat + radial * (seg.EndRadius + p.LeafSize * 0.25));
                    }
                }
            }

            foreach (var lp in skeleton.LeafPoints)
                canopyPts.Add(ProjectXY(lp));

            // Fallback radius from plant height if few points
            if (canopyPts.Count < 3)
            {
                double r = Math.Max(p.Height * 0.25, p.BaseRadius * 4);
                var fallback = new Circle(Plane.WorldXY, Point3d.Origin, r);
                var circ = new ArcCurve(fallback);
                lines.Add(circ);
                colorBoundaries.Add(circ.DuplicateCurve());
                return;
            }

            // Convex hull in XY
            var hull = ConvexHullXY(canopyPts);
            if (hull == null || hull.Count < 3)
            {
                double avgR = canopyPts.Average(pt => Math.Sqrt(pt.X * pt.X + pt.Y * pt.Y));
                avgR = Math.Max(avgR, p.BaseRadius * 3);
                var circ = new ArcCurve(new Circle(Plane.WorldXY, Point3d.Origin, avgR));
                lines.Add(circ);
                colorBoundaries.Add(circ.DuplicateCurve());
                return;
            }

            // Organic closed canopy contour
            var canopy = SmoothClosedPolyline(hull, 2);
            if (canopy != null && canopy.IsClosed)
            {
                lines.Add(canopy.DuplicateCurve());
                colorBoundaries.Add(canopy);
            }

            // Optional: a few radial accent lines from trunk to canopy (plan style)
            var rng = new Random(p.Seed + 77);
            int spokes = 4;
            for (int i = 0; i < spokes; i++)
            {
                double a = 2.0 * Math.PI * i / spokes + rng.NextDouble() * 0.4;
                var dir = new Vector3d(Math.Cos(a), Math.Sin(a), 0);
                // find hull radius roughly in that direction
                double maxDot = 0;
                Point3d far = Point3d.Origin;
                foreach (var hp in hull)
                {
                    var v = new Vector3d(hp);
                    double d = v * dir;
                    if (d > maxDot)
                    {
                        maxDot = d;
                        far = hp;
                    }
                }
                if (maxDot > trunkR * 2)
                {
                    var start = Point3d.Origin + dir * (trunkR * 1.2);
                    var end = far * 0.85; // not quite to edge
                    end.Z = 0;
                    if (start.DistanceTo(end) > trunkR)
                        lines.Add(new LineCurve(start, end));
                }
            }
        }

        static Point3d ProjectXY(Point3d p) => new Point3d(p.X, p.Y, 0);

        static List<Point3d>? ConvexHullXY(List<Point3d> points)
        {
            var pts = points
                .Select(p => new Point3d(p.X, p.Y, 0))
                .Distinct(new PointXYComparer())
                .OrderBy(p => p.X)
                .ThenBy(p => p.Y)
                .ToList();

            if (pts.Count < 3)
                return null;

            var lower = new List<Point3d>();
            foreach (var p in pts)
            {
                while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], p) <= 0)
                    lower.RemoveAt(lower.Count - 1);
                lower.Add(p);
            }

            var upper = new List<Point3d>();
            for (int i = pts.Count - 1; i >= 0; i--)
            {
                var p = pts[i];
                while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], p) <= 0)
                    upper.RemoveAt(upper.Count - 1);
                upper.Add(p);
            }

            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            return lower.Count >= 3 ? lower : null;
        }

        static double Cross(Point3d o, Point3d a, Point3d b)
        {
            return (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        }

        static PolylineCurve? SmoothClosedPolyline(List<Point3d> hull, int subdivisions)
        {
            if (hull.Count < 3) return null;

            var polyPts = new List<Point3d>(hull);
            if (polyPts[0].DistanceTo(polyPts[polyPts.Count - 1]) > 1e-9)
                polyPts.Add(polyPts[0]);

            var curve = new Polyline(polyPts).ToPolylineCurve();
            if (curve == null) return null;

            var nurb = curve.ToNurbsCurve();
            if (nurb != null)
            {
                nurb.MakeClosed(1e-6);
                int samples = Math.Max(28, hull.Count * (1 + subdivisions));
                var sampled = new List<Point3d>();
                for (int i = 0; i < samples; i++)
                {
                    double t = nurb.Domain.ParameterAt((double)i / samples);
                    var pt = nurb.PointAt(t);
                    sampled.Add(new Point3d(pt.X, pt.Y, 0));
                }
                sampled.Add(sampled[0]);
                return new PolylineCurve(sampled);
            }

            return curve;
        }

        class PointXYComparer : IEqualityComparer<Point3d>
        {
            public bool Equals(Point3d a, Point3d b) =>
                Math.Abs(a.X - b.X) < 1e-6 && Math.Abs(a.Y - b.Y) < 1e-6;

            public int GetHashCode(Point3d p) =>
                HashCode.Combine(Math.Round(p.X, 5), Math.Round(p.Y, 5));
        }
    }
}
