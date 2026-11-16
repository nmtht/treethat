using System;
using System.Collections.Generic;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace TreeThat.Generation
{
    public static class BlockFactory
    {
        /// <summary>
        /// Bakes a block with geometry on:
        /// РАСТЕНИЯ / ВЕТВИ, ЛИСТЬЯ, ЛИНИИ, ЦВЕТ
        /// and inserts one instance on РАСТЕНИЯ.
        /// </summary>
        public static Guid CreateBlock(RhinoDoc doc, PlantGeometry geom, string baseName, Point3d insertPoint)
        {
            if (doc == null || geom == null)
                return Guid.Empty;

            LayerSetup.Ensure(doc);

            string name = GetUniqueBlockName(doc, baseName);
            var geometries = new List<GeometryBase>();
            var attributes = new List<ObjectAttributes>();

            // 3D branches
            if (geom.Branches != null && geom.Branches.Vertices.Count > 0 && geom.Branches.IsValid)
            {
                geometries.Add(geom.Branches.DuplicateMesh());
                attributes.Add(MakeAtt(name + "_branches", LayerSetup.BranchesIndex));
            }

            // 3D leaves
            if (geom.Leaves != null && geom.Leaves.Vertices.Count > 0 && geom.Leaves.IsValid)
            {
                geometries.Add(geom.Leaves.DuplicateMesh());
                attributes.Add(MakeAtt(name + "_leaves", LayerSetup.LeavesIndex));
            }

            // 2D lines
            if (geom.Lines2d != null)
            {
                int li = 0;
                foreach (var c in geom.Lines2d)
                {
                    if (c == null || !c.IsValid) continue;
                    geometries.Add(c.DuplicateCurve());
                    attributes.Add(MakeAtt($"{name}_line_{li++}", LayerSetup.LinesIndex));
                }
            }

            // 2D color hatch (crown fill)
            if (geom.ColorBoundaries != null)
            {
                int hi = 0;
                int patternIndex = FindSolidOrFirstHatch(doc);
                foreach (var boundary in geom.ColorBoundaries)
                {
                    if (boundary == null || !boundary.IsValid) continue;

                    // Always keep boundary curve on ЛИНИИ as well if not already
                    // Hatch on ЦВЕТ
                    try
                    {
                        var hatches = Hatch.Create(
                            boundary,
                            patternIndex,
                            0.0,
                            1.0,
                            doc.ModelAbsoluteTolerance);

                        if (hatches != null)
                        {
                            foreach (var h in hatches)
                            {
                                if (h == null) continue;
                                geometries.Add(h);
                                attributes.Add(MakeAtt($"{name}_hatch_{hi++}", LayerSetup.ColorIndex));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        RhinoApp.WriteLine("TreeThat hatch: " + ex.Message);
                        // fallback: closed curve on color layer
                        geometries.Add(boundary.DuplicateCurve());
                        attributes.Add(MakeAtt($"{name}_color_crv_{hi++}", LayerSetup.ColorIndex));
                    }
                }
            }

            if (geometries.Count == 0)
                return Guid.Empty;

            int idefIndex = doc.InstanceDefinitions.Add(
                name,
                "TreeThat plant (3D + 2D entourage)",
                Point3d.Origin,
                geometries,
                attributes);

            if (idefIndex < 0)
                return Guid.Empty;

            var xform = Transform.Translation(insertPoint - Point3d.Origin);
            var instAtt = new ObjectAttributes
            {
                Name = name,
                LayerIndex = LayerSetup.RootIndex >= 0 ? LayerSetup.RootIndex : doc.Layers.CurrentLayerIndex
            };

            var instanceId = doc.Objects.AddInstanceObject(idefIndex, xform, instAtt);
            doc.Views.Redraw();
            return instanceId;
        }

        /// <summary>Legacy single-mesh block (no layers split).</summary>
        public static Guid CreateBlock(RhinoDoc doc, Mesh mesh, string baseName, Point3d insertPoint)
        {
            var geom = new PlantGeometry { Branches = mesh ?? new Mesh() };
            return CreateBlock(doc, geom, baseName, insertPoint);
        }

        public static Guid AddMesh(RhinoDoc doc, Mesh mesh, string name)
        {
            if (doc == null || mesh == null)
                return Guid.Empty;

            var att = new ObjectAttributes { Name = name };
            var id = doc.Objects.AddMesh(mesh, att);
            doc.Views.Redraw();
            return id;
        }

        static ObjectAttributes MakeAtt(string name, int layerIndex)
        {
            return new ObjectAttributes
            {
                Name = name,
                LayerIndex = layerIndex >= 0 ? layerIndex : 0
            };
        }

        static int FindSolidOrFirstHatch(RhinoDoc doc)
        {
            if (doc.HatchPatterns == null || doc.HatchPatterns.Count == 0)
                return 0;

            for (int i = 0; i < doc.HatchPatterns.Count; i++)
            {
                var hp = doc.HatchPatterns[i];
                if (hp != null && hp.Name != null &&
                    hp.Name.IndexOf("solid", StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
            }

            return 0;
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
