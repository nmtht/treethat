# TreeThat

**Low-poly plant block generator for Rhino 8 & Rhino 9**

Cross-platform (Windows + macOS) RhinoCommon plugin with Eto UI.

## Status

**Stage 0 complete** — Project scaffolding ready.

- Multi-targeting: `net8.0` (Rhino 8) + `net10.0` (Rhino 9)
- Basic command `TreeThat`
- Empty dockable Eto panel
- GitHub Actions build workflow

## Goals

- Full `.rhp` plugin (not Grasshopper)
- Eto.Forms UI
- Low-poly procedural plant generation (trees, bushes, grass, flowers…)
- Create and manage Rhino Blocks
- Compatible with Rhino 8 and Rhino 9
- Yak packaging for distribution

## Current Structure

```
treethat/
├── .github/workflows/build.yml
├── src/TreeThat/
│   ├── TreeThat.csproj
│   ├── TreeThatPlugin.cs
│   ├── Commands/
│   │   └── TreeThatCommand.cs
│   └── UI/
│       └── MainPanel.cs
├── Directory.Build.props
├── .gitignore
└── README.md
```

## Development

### Requirements

- Rhino 8 or Rhino 9 installed
- .NET 8 SDK (and .NET 10 when available)
- Visual Studio 2022+, VS Code or Rider

### Building

```bash
dotnet restore src/TreeThat/TreeThat.csproj
dotnet build src/TreeThat/TreeThat.csproj -c Release
```

The output `.rhp` will appear under `src/TreeThat/bin/Release/net8.0/` (and net10.0).

### Testing in Rhino

1. Build the project.
2. In Rhino: `Tools → Options → Plug-ins → Install` and select the `.rhp`.
3. Run command `TreeThat` — it toggles the dockable panel.

Alternatively use the Package Manager / Yak later.

## Roadmap

- **Stage 0** — Scaffold (done)
- **Stage 1** — MVP generator (simple tree + Block creation + basic parameters)
- **Stage 2** — Multiple plant types, modular geometry, presets
- **Stage 3** — Polish, materials, export, Yak release

## License

TBD
