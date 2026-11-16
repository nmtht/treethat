using System;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace TreeThat.Generation
{
    /// <summary>
    /// Creates a Rhino Block (InstanceDefinition) from a generated plant mesh.
    /// </summary>
    public static class BlockFactory
    {
        /// <summary>
        /// Adds the mesh to the document as a new block definition and inserts one instance at origin.
        /// Returns the instance object id, or Guid.Empty on failure.
        /// </summary>
        public static Guid CreateBlock(RhinoDoc doc, Mesh mesh, string baseName, Point3d insertPoint)
        {
            if (doc == null || mesh == null || !mesh.IsValid)
                return Guid.Empty;

            // Unique name
            string name = GetUniqueBlockName(doc, baseName);

            // Geometry for the definition (at origin)
            var attributes = new ObjectAttributes
            {
                Name = name,
                LayerIndex = doc.Layers.CurrentLayerIndex
            };

            // Create instance definition
            // We add a temporary mesh object, then turn it into a block.
            var tempId = doc.Objects.AddMesh(mesh, attributes);
            if (tempId == Guid.Empty)
                return Guid.Empty;

            var obj = doc.Objects.FindId(tempId);
            if (obj == null)
                return Guid.Empty;

            // Create definition from the object
            var idefIndex = doc.InstanceDefinitions.Add(
                name,
                "TreeThat generated plant",
                Point3d.Origin,
                new[] { obj.Geometry },
                new[] { attributes });

            // Remove temporary object
            doc.Objects.Delete(tempId, true);

            if (idefIndex < 0)
                return Guid.Empty;

            // Insert one instance
            var xform = Transform.Translation(insertPoint - Point3d.Origin);
            var instanceId = doc.Objects.AddInstanceObject(idefIndex, xform);

            doc.Views.Redraw();
            return instanceId;
        }

        /// <summary>
        /// Just adds the mesh as a regular object (for preview / non-block use).
        /// </summary>
        public static Guid AddMesh(RhinoDoc doc, Mesh mesh, string name)
        {
            if (doc == null || mesh == null)
                return Guid.Empty;

            var att = new ObjectAttributes { Name = name };
            var id = doc.Objects.AddMesh(mesh, att);
            doc.Views.Redraw();
            return id;
        }

        static string GetUniqueBlockName(RhinoDoc doc, string baseName)
        {
            if (doc.InstanceDefinitions.Find(baseName) == null)
                return baseName;

            for (int i = 1; i < 1000; i++)
            {
                string candidate = $"{baseName}_{i:000}";
                if (doc.InstanceDefinitions.Find(candidate) == null)
                    return candidate;
            }

            return baseName + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
        }
    }
}
