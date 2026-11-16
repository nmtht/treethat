using System;
using System.Drawing;
using Rhino;
using Rhino.DocObjects;

namespace TreeThat.Generation
{
    /// <summary>
    /// Ensures the plant layer tree used when baking blocks.
    /// РАСТЕНИЯ
    ///   ├─ ВЕТВИ
    ///   ├─ ЛИСТЬЯ
    ///   ├─ ЛИНИИ
    ///   └─ ЦВЕТ
    /// </summary>
    public static class LayerSetup
    {
        public const string RootName = "РАСТЕНИЯ";
        public const string BranchesName = "ВЕТВИ";
        public const string LeavesName = "ЛИСТЬЯ";
        public const string LinesName = "ЛИНИИ";
        public const string ColorName = "ЦВЕТ";

        public static int RootIndex { get; private set; } = -1;
        public static int BranchesIndex { get; private set; } = -1;
        public static int LeavesIndex { get; private set; } = -1;
        public static int LinesIndex { get; private set; } = -1;
        public static int ColorIndex { get; private set; } = -1;

        public static void Ensure(RhinoDoc doc)
        {
            if (doc == null) return;

            RootIndex = EnsureLayer(doc, RootName, null, Color.FromArgb(80, 120, 60));
            BranchesIndex = EnsureLayer(doc, BranchesName, RootName, Color.FromArgb(90, 60, 40));
            LeavesIndex = EnsureLayer(doc, LeavesName, RootName, Color.FromArgb(60, 140, 70));
            LinesIndex = EnsureLayer(doc, LinesName, RootName, Color.FromArgb(30, 30, 30));
            ColorIndex = EnsureLayer(doc, ColorName, RootName, Color.FromArgb(120, 170, 90));
        }

        static int EnsureLayer(RhinoDoc doc, string name, string? parentName, Color color)
        {
            // Full path for nested lookup
            string fullPath = string.IsNullOrEmpty(parentName) ? name : parentName + "::" + name;

            var existing = doc.Layers.FindByFullPath(fullPath, -1);
            if (existing >= 0)
                return existing;

            // Also try by name under parent
            for (int i = 0; i < doc.Layers.Count; i++)
            {
                var layer = doc.Layers[i];
                if (!layer.IsDeleted && layer.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(parentName))
                    {
                        if (layer.ParentLayerId == Guid.Empty)
                            return i;
                    }
                    else
                    {
                        var parent = doc.Layers.FindName(parentName);
                        if (parent != null && layer.ParentLayerId == parent.Id)
                            return i;
                    }
                }
            }

            var newLayer = new Layer
            {
                Name = name,
                Color = color
            };

            if (!string.IsNullOrEmpty(parentName))
            {
                var parent = doc.Layers.FindName(parentName);
                if (parent != null)
                    newLayer.ParentLayerId = parent.Id;
            }

            int idx = doc.Layers.Add(newLayer);
            return idx >= 0 ? idx : 0;
        }
    }
}
