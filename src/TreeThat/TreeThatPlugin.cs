using System;
using Rhino;
using Rhino.PlugIns;

namespace TreeThat
{
    /// <summary>
    /// TreeThat — Low-poly plant block generator for Rhino.
    /// Stage 0 scaffold.
    /// </summary>
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
            // Register panel here in later stages if needed
            return LoadReturnCode.Success;
        }
    }
}
