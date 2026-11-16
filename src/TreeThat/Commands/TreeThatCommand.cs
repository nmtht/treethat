using System;
using Rhino;
using Rhino.Commands;
using Rhino.UI;
using TreeThat.UI;

namespace TreeThat.Commands
{
    [System.Runtime.InteropServices.Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890")]
    public class TreeThatCommand : Command
    {
        public TreeThatCommand()
        {
            Instance = this;
        }

        public static TreeThatCommand Instance { get; private set; } = null!;

        public override string EnglishName => "TreeThat";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var panelId = MainPanel.PanelId;

            // Toggle panel visibility
            var visible = Panels.IsPanelVisible(panelId);

            if (visible)
            {
                Panels.ClosePanel(panelId);
            }
            else
            {
                Panels.OpenPanel(panelId);
            }

            return Result.Success;
        }
    }
}
