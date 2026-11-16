# TreeThat

**Low-poly plant block generator for Rhino 8 & Rhino 9**

Cross-platform (Windows + macOS) RhinoCommon plugin with Eto UI.

## Status

**Stage 0** — Project scaffolding, multi-targeting, basic command + empty Eto panel.

## Goals

- Full `.rhp` plugin (not Grasshopper)
- Eto.Forms UI
- Low-poly procedural plant generation (trees, bushes, grass, etc.)
- Create Rhino Blocks
- Compatible with Rhino 8 (net8.0) and Rhino 9 (net10.0)
- Yak packaging

## Development

### Requirements

- Rhino 8 or Rhino 9
- .NET 8 / .NET 10 SDK
- Visual Studio 2022+ / VS Code / Rider

### Building

```bash
dotnet build src/TreeThat/TreeThat.csproj -c Release
```

### Structure

```
src/TreeThat/
├── TreeThat.csproj
├── TreeThatPlugin.cs
├── Commands/
│   └── TreeThatCommand.cs
├── UI/
│   └── MainPanel.cs
└── ...
```

## License

TBD
