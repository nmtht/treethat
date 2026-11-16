using System;
using Eto.Drawing;
using Eto.Forms;
using Rhino.UI;

namespace TreeThat.UI
{
    /// <summary>
    /// Main dockable panel for TreeThat.
    /// Stage 0: empty scaffold.
    /// </summary>
    [System.Runtime.InteropServices.Guid("B2C3D4E5-F6A7-8901-BCDE-F12345678901")]
    public class MainPanel : Panel, IPanel
    {
        public static Guid PanelId => typeof(MainPanel).GUID;

        public MainPanel()
        {
            // Simple placeholder UI for Stage 0
            var layout = new DynamicLayout
            {
                DefaultSpacing = new Size(8, 8),
                Padding = new Padding(12)
            };

            layout.AddRow(new Label
            {
                Text = "TreeThat — Low-poly Plant Generator",
                Font = new Font(SystemFont.Bold, 12)
            });

            layout.AddRow(new Label
            {
                Text = "Stage 0 scaffold. UI and generation will appear here."
            });

            layout.AddRow(null); // stretch

            layout.AddRow(new Label
            {
                Text = "Rhino 8 / Rhino 9  •  Windows + macOS",
                TextColor = Colors.Gray
            });

            Content = layout;
        }

        #region IPanel implementation

        public void PanelShown(uint documentSerialNumber, bool on)
        {
            // Called when panel becomes visible
        }

        public void PanelHidden(uint documentSerialNumber, bool on)
        {
            // Called when panel is hidden
        }

        public void PanelClosing(uint documentSerialNumber, bool on)
        {
            // Called when panel is closing
        }

        #endregion
    }
}
