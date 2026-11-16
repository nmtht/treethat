using System;
using System.Runtime.InteropServices;
using Rhino;
using Rhino.PlugIns;
using Rhino.UI;
using TreeThat.UI;

namespace TreeThat
{
    /// <summary>
    /// TreeThat — Low-poly plant block generator for Rhino.
    /// Stage 0 scaffold.
    /// </summary>
    [Guid(TreeThatPlugin.PluginId)]
    public class TreeThatPlugin : PlugIn
    {
        // Keep this GUID stable forever
        public const string PluginId = "c3d4e5f6-a7b8-9012-cdef-123456789012";

        public TreeThatPlugin()
        {
            Instance = this;
        }

        /// <summary>
        /// Gets the only instance of the TreeThatPlugin plug-in.
        /// </summary>
        public static TreeThatPlugin Instance { get; private set; } = null!;

        // Explicitly provide the Id so Rhino never sees Guid.Empty
        public override Guid Id => new Guid(PluginId);

        public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            // Register the dockable panel
            Panels.RegisterPanel(this, typeof(MainPanel), "TreeThat", null);

            return LoadReturnCode.Success;
        }
    }
}
