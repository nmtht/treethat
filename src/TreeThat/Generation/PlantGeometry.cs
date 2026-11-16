using System.Collections.Generic;
using Rhino.Geometry;

namespace TreeThat.Generation
{
    /// <summary>
    /// Full plant output for layered block baking.
    /// </summary>
    public class PlantGeometry
    {
        public Mesh Branches { get; set; } = new Mesh();
        public Mesh Leaves { get; set; } = new Mesh();

        /// <summary>2D elevation outlines (trunk + crown contour, internal accents).</summary>
        public List<Curve> Lines2d { get; set; } = new List<Curve>();

        /// <summary>Closed curves used for color hatch (crown mass).</summary>
        public List<Curve> ColorBoundaries { get; set; } = new List<Curve>();

        public Mesh CombinedMesh()
        {
            var m = new Mesh();
            if (Branches != null && Branches.Vertices.Count > 0)
                m.Append(Branches);
            if (Leaves != null && Leaves.Vertices.Count > 0)
                m.Append(Leaves);
            m.Normals.ComputeNormals();
            m.Compact();
            return m;
        }
    }
}
