using System;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.Geometry;
using Rhino.UI;
using TreeThat.Generation;

namespace TreeThat.UI
{
    /// <summary>
    /// Main dockable panel for TreeThat.
    /// Stage 1: parameters + generate + create block.
    /// </summary>
    [System.Runtime.InteropServices.Guid("B2C3D4E5-F6A7-8901-BCDE-F12345678901")]
    public class MainPanel : Panel, IPanel
    {
        public static Guid PanelId => typeof(MainPanel).GUID;

        readonly PlantParams _params = new PlantParams();

        // Controls
        NumericStepper _height = null!;
        NumericStepper _levels = null!;
        NumericStepper _branches = null!;
        NumericStepper _angle = null!;
        NumericStepper _density = null!;
        NumericStepper _leafSize = null!;
        NumericStepper _polyBudget = null!;
        NumericStepper _seed = null!;
        NumericStepper _baseRadius = null!;
        Label _status = null!;

        public MainPanel()
        {
            BuildUi();
        }

        void BuildUi()
        {
            var layout = new DynamicLayout
            {
                DefaultSpacing = new Size(6, 6),
                Padding = new Padding(12)
            };

            layout.AddRow(new Label
            {
                Text = "TreeThat",
                Font = new Font(SystemFont.Bold, 13)
            });

            layout.AddRow(new Label
            {
                Text = "Low-poly plant block generator",
                TextColor = Colors.Gray
            });

            layout.AddRow(null);

            // --- Parameters ---
            layout.AddRow(SectionLabel("Trunk"));
            _height = AddStepper(layout, "Height", _params.Height, 0.5, 50, 0.5);
            _baseRadius = AddStepper(layout, "Base radius", _params.BaseRadius, 0.02, 2, 0.02);

            layout.AddRow(SectionLabel("Branching"));
            _levels = AddStepper(layout, "Levels", _params.BranchLevels, 0, 5, 1);
            _branches = AddStepper(layout, "Branches / node", _params.BranchesPerNode, 2, 5, 1);
            _angle = AddStepper(layout, "Branch angle°", _params.BranchAngle, 10, 60, 1);

            layout.AddRow(SectionLabel("Foliage"));
            _density = AddStepper(layout, "Density", _params.FoliageDensity, 0, 1, 0.1);
            _leafSize = AddStepper(layout, "Leaf size", _params.LeafSize, 0.05, 2, 0.05);

            layout.AddRow(SectionLabel("Quality"));
            _polyBudget = AddStepper(layout, "Poly budget", _params.PolyBudget, 100, 5000, 50);
            _seed = AddStepper(layout, "Seed", _params.Seed, 0, 99999, 1);

            layout.AddRow(null);

            // --- Buttons ---
            var btnGenerate = new Button { Text = "Generate Mesh" };
            btnGenerate.Click += OnGenerateMesh;

            var btnBlock = new Button { Text = "Create Block" };
            btnBlock.Click += OnCreateBlock;

            var btnRandom = new Button { Text = "Random Seed" };
            btnRandom.Click += (s, e) =>
            {
                _seed.Value = new Random().Next(0, 99999);
            };

            layout.AddRow(btnGenerate);
            layout.AddRow(btnBlock);
            layout.AddRow(btnRandom);

            layout.AddRow(null);

            _status = new Label
            {
                Text = "Ready",
                TextColor = Colors.Gray
            };
            layout.AddRow(_status);

            Content = new Scrollable { Content = layout };
        }

        static Label SectionLabel(string text)
        {
            return new Label
            {
                Text = text,
                Font = new Font(SystemFont.Bold, 10),
                TextColor = Colors.Gray
            };
        }

        NumericStepper AddStepper(DynamicLayout layout, string label, double value, double min, double max, double step)
        {
            var stepper = new NumericStepper
            {
                Value = value,
                MinValue = min,
                MaxValue = max,
                Increment = step,
                DecimalPlaces = step < 1 ? 2 : 0,
                Width = 90
            };

            layout.AddRow(new Label { Text = label, VerticalAlignment = VerticalAlignment.Center }, stepper);
            return stepper;
        }

        void ReadParams()
        {
            _params.Height = _height.Value;
            _params.BaseRadius = _baseRadius.Value;
            _params.BranchLevels = (int)_levels.Value;
            _params.BranchesPerNode = (int)_branches.Value;
            _params.BranchAngle = _angle.Value;
            _params.FoliageDensity = _density.Value;
            _params.LeafSize = _leafSize.Value;
            _params.PolyBudget = (int)_polyBudget.Value;
            _params.Seed = (int)_seed.Value;
        }

        void OnGenerateMesh(object? sender, EventArgs e)
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null)
            {
                _status.Text = "No active document";
                return;
            }

            try
            {
                ReadParams();
                var mesh = PlantGenerator.Generate(_params);
                var id = BlockFactory.AddMesh(doc, mesh, $"TreeThat_{_params.Seed}");

                _status.Text = id != Guid.Empty
                    ? $"Mesh added · {mesh.Faces.Count} faces"
                    : "Failed to add mesh";
            }
            catch (Exception ex)
            {
                _status.Text = "Error: " + ex.Message;
                RhinoApp.WriteLine("TreeThat error: " + ex);
            }
        }

        void OnCreateBlock(object? sender, EventArgs e)
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null)
            {
                _status.Text = "No active document";
                return;
            }

            try
            {
                ReadParams();
                var mesh = PlantGenerator.Generate(_params);
                var id = BlockFactory.CreateBlock(
                    doc,
                    mesh,
                    $"TT_Plant_{_params.Seed}",
                    Point3d.Origin);

                _status.Text = id != Guid.Empty
                    ? $"Block created · {mesh.Faces.Count} faces"
                    : "Failed to create block";
            }
            catch (Exception ex)
            {
                _status.Text = "Error: " + ex.Message;
                RhinoApp.WriteLine("TreeThat error: " + ex);
            }
        }

        #region IPanel

        public void PanelShown(uint documentSerialNumber, ShowPanelReason reason) { }
        public void PanelHidden(uint documentSerialNumber, ShowPanelReason reason) { }
        public void PanelClosing(uint documentSerialNumber, bool onCloseDocument) { }

        #endregion
    }
}
