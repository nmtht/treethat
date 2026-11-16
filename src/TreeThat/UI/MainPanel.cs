using System;
using System.Linq;
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
    /// Stage 1: presets + parameters + generate + create block.
    /// </summary>
    [System.Runtime.InteropServices.Guid("B2C3D4E5-F6A7-8901-BCDE-F12345678901")]
    public class MainPanel : Panel, IPanel
    {
        public static Guid PanelId => typeof(MainPanel).GUID;

        readonly PlantParams _params = new PlantParams();
        Guid _lastObjectId = Guid.Empty;

        DropDown _presetDrop = null!;
        Label _presetInfo = null!;

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
            ReloadPresets();
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

            // --- Presets ---
            layout.AddRow(SectionLabel("Presets"));
            _presetDrop = new DropDown { Width = 180 };
            _presetDrop.SelectedIndexChanged += OnPresetSelected;
            layout.AddRow(_presetDrop);

            _presetInfo = new Label
            {
                Text = "",
                TextColor = Colors.Gray,
                Wrap = WrapMode.Word
            };
            layout.AddRow(_presetInfo);

            var btnApply = new Button { Text = "Apply Preset" };
            btnApply.Click += (s, e) => ApplySelectedPreset();

            var btnSave = new Button { Text = "Save as Preset…" };
            btnSave.Click += OnSavePreset;

            layout.AddRow(btnApply);
            layout.AddRow(btnSave);

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

        void ReloadPresets()
        {
            _presetDrop.Items.Clear();
            foreach (var p in PresetStore.All())
            {
                _presetDrop.Items.Add($"{p.Category} / {p.Name}");
            }

            if (_presetDrop.Items.Count > 0)
            {
                _presetDrop.SelectedIndex = 0;
                OnPresetSelected(null, EventArgs.Empty);
            }
        }

        PlantPreset? SelectedPreset()
        {
            int i = _presetDrop.SelectedIndex;
            if (i < 0)
                return null;

            var list = PresetStore.All().ToList();
            if (i >= list.Count)
                return null;

            return list[i];
        }

        void OnPresetSelected(object? sender, EventArgs e)
        {
            var preset = SelectedPreset();
            if (preset == null)
            {
                _presetInfo.Text = "";
                return;
            }

            _presetInfo.Text = string.IsNullOrWhiteSpace(preset.Description)
                ? preset.Category
                : preset.Description;
        }

        void ApplySelectedPreset()
        {
            var preset = SelectedPreset();
            if (preset == null)
                return;

            WriteParamsToUi(preset.ToParams());
            _status.Text = $"Preset: {preset.Name}";
        }

        void WriteParamsToUi(PlantParams p)
        {
            _height.Value = p.Height;
            _baseRadius.Value = p.BaseRadius;
            _levels.Value = p.BranchLevels;
            _branches.Value = p.BranchesPerNode;
            _angle.Value = p.BranchAngle;
            _density.Value = p.FoliageDensity;
            _leafSize.Value = p.LeafSize;
            _polyBudget.Value = p.PolyBudget;
            _seed.Value = p.Seed;

            // Keep full params object in sync (ratios not on UI yet)
            _params.Height = p.Height;
            _params.BaseRadius = p.BaseRadius;
            _params.BranchLevels = p.BranchLevels;
            _params.BranchesPerNode = p.BranchesPerNode;
            _params.BranchAngle = p.BranchAngle;
            _params.FoliageDensity = p.FoliageDensity;
            _params.LeafSize = p.LeafSize;
            _params.PolyBudget = p.PolyBudget;
            _params.Seed = p.Seed;
            _params.LengthRatio = p.LengthRatio;
            _params.RadiusRatio = p.RadiusRatio;
            _params.TubeSegments = p.TubeSegments;
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

        void OnSavePreset(object? sender, EventArgs e)
        {
            ReadParams();

            var nameDlg = new Dialog<bool>
            {
                Title = "Save Preset",
                ClientSize = new Size(320, 120),
                Padding = 12
            };

            var nameBox = new TextBox { Text = "My Plant", Width = 200 };
            var catBox = new TextBox { Text = "Custom", Width = 200 };
            var ok = new Button { Text = "Save" };
            var cancel = new Button { Text = "Cancel" };

            ok.Click += (s, ev) => nameDlg.Close(true);
            cancel.Click += (s, ev) => nameDlg.Close(false);

            var dl = new DynamicLayout { DefaultSpacing = new Size(6, 6) };
            dl.AddRow(new Label { Text = "Name" }, nameBox);
            dl.AddRow(new Label { Text = "Category" }, catBox);
            dl.AddRow(null);
            dl.AddRow(null, new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Items = { cancel, ok }
            });

            nameDlg.Content = dl;
            nameDlg.DefaultButton = ok;
            nameDlg.AbortButton = cancel;

            if (nameDlg.ShowModal(this) != true)
                return;

            var preset = PlantPreset.FromParams(
                nameBox.Text.Trim(),
                string.IsNullOrWhiteSpace(catBox.Text) ? "Custom" : catBox.Text.Trim(),
                "User preset",
                _params);

            PresetStore.SaveUserPreset(preset);
            ReloadPresets();

            // Select the one we just saved
            for (int i = 0; i < _presetDrop.Items.Count; i++)
            {
                if (_presetDrop.Items[i].Text.EndsWith("/ " + preset.Name, StringComparison.Ordinal))
                {
                    _presetDrop.SelectedIndex = i;
                    break;
                }
            }

            _status.Text = $"Saved preset: {preset.Name}";
        }

        void DeletePrevious(RhinoDoc doc)
        {
            if (_lastObjectId == Guid.Empty)
                return;

            var obj = doc.Objects.FindId(_lastObjectId);
            if (obj != null)
                doc.Objects.Delete(obj, true);

            _lastObjectId = Guid.Empty;
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
                DeletePrevious(doc);

                var mesh = PlantGenerator.Generate(_params);
                var id = BlockFactory.AddMesh(doc, mesh, $"TreeThat_{_params.Seed}");
                _lastObjectId = id;

                _status.Text = id != Guid.Empty
                    ? $"Mesh · {mesh.Faces.Count} faces"
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
                DeletePrevious(doc);

                var mesh = PlantGenerator.Generate(_params);
                var id = BlockFactory.CreateBlock(
                    doc,
                    mesh,
                    $"TT_Plant_{_params.Seed}",
                    Point3d.Origin);
                _lastObjectId = id;

                _status.Text = id != Guid.Empty
                    ? $"Block · {mesh.Faces.Count} faces"
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
