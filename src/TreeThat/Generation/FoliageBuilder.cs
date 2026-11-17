using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;

namespace TreeThat.Generation
{
    /// <summary>
    /// Coverage-first low-poly foliage.
    /// Target fill from canopy size; poly budget only lowers module LOD, not holes.
    /// </summary>
    public static class FoliageBuilder
    {
        enum Lod { Low, Medium, High }

        struct Site
        {
            public Point3d Center;
            public Vector3d Axis;
            public double Size;
            public bool Conifer;
        }

        public static Mesh Build(PlantSkeleton skeleton, PlantParams p)
        {
            var mesh = new Mesh();
            if (skeleton == null || skeleton.Segments.Count == 0)
                return mesh;

            var rng = new Random(p.Seed + 11);
            var sites = CollectSites(skeleton, p, rng);
            if (sites.Count == 0)
                return mesh;

            // Face budget → LOD only (coverage stays)
            int tubeFaces = skeleton.Segments.Count * Math.Max(3, p.TubeSegments) * 2;
            int foliageBudget = Math.Max(120, p.PolyBudget - tubeFaces);
            // Approximate faces per LOD: Low≈4, Med≈8, High≈14
            double facesIfHigh = sites.Count * 14.0;
            Lod lod;
            if (foliageBudget >= facesIfHigh * 0.85)
                lod = Lod.High;
            else if (foliageBudget >= sites.Count * 8.0)
                lod = Lod.Medium;
            else
                lod = Lod.Low;

            int seed = p.Seed;
            foreach (var site in sites)
            {
                Mesh? mod = site.Conifer
                    ? CreateConiferModule(site, lod, seed++)
                    : CreateDeciduousModule(site, lod, seed++);
                if (mod != null)
                    mesh.Append(mod);
            }

            if (mesh.Vertices.Count > 0)
            {
                mesh.Normals.ComputeNormals();
                mesh.Compact();
            }
            return mesh;
        }

        static List<Site> CollectSites(PlantSkeleton skeleton, PlantParams p, Random rng)
        {
            var sites = new List<Site>();
            var outer = skeleton.Segments.Where(s => s.Level >= 1).ToList();
            if (outer.Count == 0)
                return sites;

            // Canopy metrics
            double minZ = double.MaxValue, maxZ = double.MinValue, maxR = 0;
            foreach (var seg in outer)
            {
                SampleExtents(seg.Start, ref minZ, ref maxZ, ref maxR);
                SampleExtents(seg.End, ref minZ, ref maxZ, ref maxR);
            }
            foreach (var lp in skeleton.LeafPoints)
                SampleExtents(lp, ref minZ, ref maxZ, ref maxR);

            if (maxR < 1e-6)
                maxR = Math.Max(p.Height * 0.25, 0.5);
            if (minZ > maxZ)
            {
                minZ = p.Height * 0.3;
                maxZ = p.Height;
            }

            double canopyH = Math.Max(maxZ - minZ, p.Height * 0.25);
            // Target count from horizontal area × density (scales with tree size)
            double area = Math.PI * maxR * maxR;
            double densityScale = 0.35 + p.FoliageDensity * 1.1;
            // ~0.35–1.4 sites per unit area, with floor/ceiling
            int target = (int)Math.Round(area * densityScale * 0.55 + canopyH * densityScale * 0.8);
            target = Math.Max(12, Math.Min(target, 180));

            // --- A) Along outer branches (guarantees structure follow) ---
            foreach (var seg in outer)
            {
                int along = 1;
                if (seg.Length > p.LeafSize * 2.5)
                    along = 2;
                if (seg.Length > p.LeafSize * 5.0 || seg.Level >= 2)
                    along = Math.Max(along, p.FoliageDensity > 0.6 ? 3 : 2);

                // Slightly fewer on thick primary if many secondaries exist
                if (p.ConiferStyle && seg.Level == 1)
                    along = Math.Max(2, along);

                for (int i = 0; i < along; i++)
                {
                    double t = along == 1
                        ? 0.55 + rng.NextDouble() * 0.35
                        : 0.25 + 0.7 * (i + rng.NextDouble() * 0.3) / along;
                    t = Math.Min(0.95, t);

                    var dir = seg.Direction;
                    if (dir.IsTiny()) continue;
                    dir.Unitize();

                    var center = seg.Start + seg.Direction * t;
                    // Push outward a bit from branch axis
                    Vector3d side = Perp(dir);
                    center += side * (seg.EndRadius + p.LeafSize * 0.15 * rng.NextDouble());

                    double size = p.LeafSize * (0.9 + p.FoliageDensity * 0.7);
                    size *= 0.75 + seg.Length * 0.15;
                    size = Math.Max(size, p.LeafSize * 0.8);
                    size *= 0.85 + rng.NextDouble() * 0.3;

                    sites.Add(new Site
                    {
                        Center = center,
                        Axis = p.ConiferStyle ? dir : (dir + Vector3d.ZAxis * 0.4),
                        Size = size,
                        Conifer = p.ConiferStyle
                    });
                }
            }

            // --- B) Volume fill if still under target (full crown, large trees) ---
            int guard = 0;
            while (sites.Count < target && guard++ < target * 3)
            {
                // Jittered sample in canopy cylinder/cone
                double u = rng.NextDouble();
                double h = minZ + u * canopyH;
                // Conifer: narrower toward top
                double rMax = maxR;
                if (p.ConiferStyle)
                {
                    double coneT = (h - minZ) / Math.Max(canopyH, 1e-6);
                    rMax = maxR * Math.Max(0.12, 1.0 - coneT * 0.85);
                }
                else
                {
                    // Deciduous: fuller middle
                    double mid = 1.0 - Math.Abs(u - 0.55) * 1.2;
                    rMax = maxR * Math.Max(0.35, mid);
                }

                double rr = Math.Sqrt(rng.NextDouble()) * rMax;
                double ang = rng.NextDouble() * Math.PI * 2;
                var center = new Point3d(Math.Cos(ang) * rr, Math.Sin(ang) * rr, h);

                // Reject if too close to existing (simple spacing)
                double minDist = p.LeafSize * (0.55 + (1.0 - p.FoliageDensity) * 0.35);
                bool ok = true;
                foreach (var s in sites)
                {
                    if (s.Center.DistanceTo(center) < minDist)
                    {
                        ok = false;
                        break;
                    }
                }
                if (!ok) continue;

                var axis = new Vector3d(center.X, center.Y, 0.35);
                if (axis.IsTiny()) axis = Vector3d.ZAxis;
                axis.Unitize();

                double size = p.LeafSize * (0.85 + p.FoliageDensity * 0.6);
                size *= 0.85 + rng.NextDouble() * 0.35;

                sites.Add(new Site
                {
                    Center = center,
                    Axis = axis,
                    Size = size,
                    Conifer = p.ConiferStyle
                });
            }

            return sites;
        }

        static void SampleExtents(Point3d pt, ref double minZ, ref double maxZ, ref double maxR)
        {
            minZ = Math.Min(minZ, pt.Z);
            maxZ = Math.Max(maxZ, pt.Z);
            double r = Math.Sqrt(pt.X * pt.X + pt.Y * pt.Y);
            maxR = Math.Max(maxR, r);
        }

        static Vector3d Perp(Vector3d dir)
        {
            Vector3d x;
            if (Math.Abs(dir.Z) < 0.9)
                x = Vector3d.CrossProduct(dir, Vector3d.ZAxis);
            else
                x = Vector3d.CrossProduct(dir, Vector3d.XAxis);
            if (x.IsTiny()) return Vector3d.XAxis;
            x.Unitize();
            return x;
        }

        // ---------- Modules ----------

        /// <summary>Deciduous: fan of cards (LOD varies face count).</summary>
        static Mesh? CreateDeciduousModule(Site site, Lod lod, int seed)
        {
            var axis = site.Axis;
            if (axis.IsTiny()) axis = Vector3d.ZAxis;
            axis.Unitize();

            var rng = new Random(seed);
            Vector3d x = Perp(axis);
            Vector3d y = Vector3d.CrossProduct(axis, x);
            y.Unitize();

            int cards = lod == Lod.High ? 5 : lod == Lod.Medium ? 3 : 2;
            double size = site.Size;
            var mesh = new Mesh();

            for (int i = 0; i < cards; i++)
            {
                double ang = (i / (double)cards) * Math.PI * 2 + rng.NextDouble() * 0.5;
                var n = (x * Math.Cos(ang) + y * Math.Sin(ang)) * 0.75 + axis * 0.35;
                n.Unitize();

                Vector3d cx = Perp(n);
                Vector3d cy = Vector3d.CrossProduct(n, cx);
                cy.Unitize();

                double rot = rng.NextDouble() * Math.PI;
                var x2 = cx * Math.Cos(rot) + cy * Math.Sin(rot);
                var y2 = -cx * Math.Sin(rot) + cy * Math.Cos(rot);

                double half = size * (0.45 + rng.NextDouble() * 0.25);
                var c = site.Center + n * (size * 0.15 * rng.NextDouble());

                int v0 = mesh.Vertices.Add(c + x2 * half);
                int v1 = mesh.Vertices.Add(c + y2 * half * 1.15);
                int v2 = mesh.Vertices.Add(c - x2 * half);
                int v3 = mesh.Vertices.Add(c - y2 * half * 0.55);
                mesh.Faces.AddFace(v0, v1, v2);
                if (lod != Lod.Low)
                    mesh.Faces.AddFace(v0, v2, v3);
            }

            return mesh.Faces.Count > 0 ? mesh : null;
        }

        /// <summary>Conifer: short comb / scale along branch axis.</summary>
        static Mesh? CreateConiferModule(Site site, Lod lod, int seed)
        {
            var axis = site.Axis;
            if (axis.IsTiny()) axis = Vector3d.XAxis;
            axis.Unitize();

            var rng = new Random(seed);
            Vector3d side = Perp(axis);
            Vector3d up = Vector3d.CrossProduct(axis, side);
            up.Unitize();

            int needles = lod == Lod.High ? 6 : lod == Lod.Medium ? 4 : 3;
            double size = site.Size * 0.85;
            var mesh = new Mesh();

            for (int i = 0; i < needles; i++)
            {
                double t = (i + 0.5) / needles - 0.5;
                var c = site.Center + axis * (t * size * 1.2);
                double a = rng.NextDouble() * Math.PI * 2;
                var outward = side * Math.Cos(a) + up * Math.Sin(a);
                outward.Unitize();

                double len = size * (0.7 + rng.NextDouble() * 0.5);
                double halfW = size * 0.12;

                var tip = c + outward * len - Vector3d.ZAxis * (len * 0.08);
                var b0 = c + axis * halfW;
                var b1 = c - axis * halfW;

                int v0 = mesh.Vertices.Add(b0);
                int v1 = mesh.Vertices.Add(b1);
                int v2 = mesh.Vertices.Add(tip);
                mesh.Faces.AddFace(v0, v1, v2);
            }

            return mesh.Faces.Count > 0 ? mesh : null;
        }
    }
}
