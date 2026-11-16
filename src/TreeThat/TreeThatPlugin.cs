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
    [Guid("8F3A2B1C-4D5E-6F7A-8B9C-0D1E2F3A4B5C")]
    public class TreeThatPlugin : PlugIn
    {
        public TreeThatPlugin()
        {
            Instance = this;
        }

        /// <summary>
        /// Gets the only instance of the TreeThatPlugin plug-in.
        /// </summary>
        public static TreeThatPlugin Instance { get; private set; } = null!;

        public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            try
            {
                // Register the dockable panel
                Panels.RegisterPanel(
                    this,
                    typeof(MainPanel),
                    "TreeThat",
                    null);
            }
            catch (Exception ex)
            {
                errorMessage = "Failed to register TreeThat panel: " + ex.Message;
                return LoadReturnCode.ErrorShowDialog;
            }

            return LoadReturnCode.Success;
        }
    }
}
