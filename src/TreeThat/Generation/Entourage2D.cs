using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;

namespace TreeThat.Generation
{
    /// <summary>
    /// Builds a clean 2D elevation entourage (lines + closed crown for hatch).
    /// Projection: XZ plane (view from +Y).
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

            // --- Trunk centerline (level 0) ---
            foreach (var seg in skeleton.Segments.Where(s => s.Level == 0))
            {
                var a = Project(seg.Start);
                var b = Project(seg.End);
                if (a.DistanceTo(b) > 1e-6)
                {
                    // Slight trunk thickness as two parallel lines
                    double halfW = Math.Max(seg.StartRadius * 0.85, 0.02);
                    var left0 = a + new Vector3d(-halfW, 0, 0);
                    var left1 = b + new Vector3d(-halfW * 0.5, 0, 0);
                    var right0 = a + new Vector3d(halfW, 0, 0);
                    var right1 = b + new Vector3d(halfW * 0.5, 0, 0);

                    lines.Add(new LineCurve(left0, left1));
                    lines.Add(new LineCurve(right0, right1));
                    lines.Add(new LineCurve(left0, right0)); // ground base
                }
            }

            // --- Sample points for crown silhouette ---
            var crownPts = new List<Point3d>();

            foreach (var seg in skeleton.Segments)
            {
                if (seg.Level < 1) continue;
                // denser samples along outer branches
                int n = seg.Level >= 2 ? 4 : 3;
                for (int i = 0; i <= n; i++)
                {
                    double t = (double)i / n;
                    var pt = seg.Start + seg.Direction * t;
                    // push slightly outward in X from trunk axis for fuller outline
                    var pr = Project(pt);
                    double push = seg.EndRadius + p.LeafSize * 0.35;
                    double sign = pr.X >= 0 ? 1.0 : -1.0;
                    if (Math.Abs(pr.X) < 1e-6) sign = 1.0;
                    crownPts.Add(pr + new Vector3d(sign * push * 0.15, 0, 0));
                    crownPts.Add(pr);
                }
            }

            foreach (var lp in skeleton.LeafPoints)
                crownPts.Add(Project(lp));

            if (crownPts.Count < 3)
                return;

            // Convex hull in XZ (as 2D hull on X/Z)
            var hull = ConvexHullXZ(crownPts);
            if (hull == null || hull.Count < 3)
                return;

            // Smooth / organic: interpolate hull slightly
            var crown = SmoothClosedPolyline(hull, 2);
            if (crown != null && crown.IsClosed)
            {
                lines.Add(crown.DuplicateCurve());
                colorBoundaries.Add(crown);
            }

            // A few internal accent lines (branch suggestions) for style
            var rng = new Random(p.Seed + 99);
            var outerBranches = skeleton.Segments.Where(s => s.Level == 1).ToList();
            int accents = Math.Min(5, outerBranches.Count);
            for (int i = 0; i < accents; i++)
            {
                var seg = outerBranches[rng.Next(outerBranches.Count)];
                var a = Project(seg.Start);
                var b = Project(seg.End);
                if (a.DistanceTo(b) > p.Height * 0.05)
                    lines.Add(new LineCurve(a, b));
            }
        }

        static Point3d Project(Point3d p) => new Point3d(p.X, 0, p.Z);

        static List<Point3d>? ConvexHullXZ(List<Point3d> points)
        {
            // Andrew's monotone chain on (X, Z)
            var pts = points
                .Select(p => new Point3d(p.X, 0, p.Z))
                .Distinct(new PointXZComparer())
                .OrderBy(p => p.X)
                .ThenBy(p => p.Z)
                .ToList();

            if (pts.Count < 3)
                return null;

            List<Point3d> lower = new List<Point3d>();
            foreach (var p in pts)
            {
                while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], p) <= 0)
                    lower.RemoveAt(lower.Count - 1);
                lower.Add(p);
            }

            List<Point3d> upper = new List<Point3d>();
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
            return (a.X - o.X) * (b.Z - o.Z) - (a.Z - o.Z) * (b.X - o.X);
        }

        static PolylineCurve? SmoothClosedPolyline(List<Point3d> hull, int subdivisions)
        {
            if (hull.Count < 3) return null;

            var polyPts = new List<Point3d>(hull);
            if (polyPts[0].DistanceTo(polyPts[polyPts.Count - 1]) > 1e-9)
                polyPts.Add(polyPts[0]);

            var pl = new Polyline(polyPts);
            var curve = pl.ToPolylineCurve();
            if (curve == null) return null;

            // Rebuild as smooth closed NURBS then sample back to polyline for hatch reliability
            var nurb = curve.ToNurbsCurve();
            if (nurb != null)
            {
                nurb.MakeClosed(1e-6);
                int samples = Math.Max(24, hull.Count * (1 + subdivisions));
                var sampled = new List<Point3d>();
                for (int i = 0; i < samples; i++)
                {
                    double t = nurb.Domain.ParameterAt((double)i / samples);
                    sampled.Add(nurb.PointAt(t));
                }
                sampled.Add(sampled[0]);
                return new PolylineCurve(sampled);
            }

            return curve;
        }

        class PointXZComparer : IEqualityComparer<Point3d>
        {
            public bool Equals(Point3d a, Point3d b) =>
                Math.Abs(a.X - b.X) < 1e-6 && Math.Abs(a.Z - b.Z) < 1e-6;

            public int GetHashCode(Point3d p) =>
                HashCode.Combine(Math.Round(p.X, 5), Math.Round(p.Z, 5));
        }
    }
}
