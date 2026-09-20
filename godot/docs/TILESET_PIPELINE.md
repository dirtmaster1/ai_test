# Tileset Asset Pipeline

This project keeps editable tile art, generated atlas PNGs, and Godot TileSet metadata separate:

- `art/tilesets/<atlas>/tiles/`: editable `64x64` source PNGs
- `assets/tilesets/*.png`: generated or externally maintained atlas PNGs imported by Godot
- `assets/tilesets/*_tileset.tres`: Godot terrain, alternative-tile, and custom-data metadata
- `assets/tilesets/tilesets.json`: atlas dimensions, source locations, outputs, and TileSet references
- `tools/build_tilesets.ps1`: extract, build, validate, and import commands

Do not regenerate `.tres` files when rebuilding artwork. Keeping Godot metadata separate preserves painted map cells, terrain classifications, alternatives, and marker custom data.

## Daily Workflow

The dungeon atlas is the first manifest-managed atlas. Its source files are named `<column>_<row>.png`, so `1_2.png` occupies atlas cell `(1, 2)`.

1. Edit a source tile under `art/tilesets/dungeon-terrain/tiles/` in GIMP. Keep the canvas exactly `64x64`, retain transparency, and use nearest-neighbor interpolation for pixel art.
2. Build and validate the atlas:

```powershell
.\tools\build_tilesets.ps1 -Action Build -Atlas dungeon-terrain
```

3. Ask Godot to import changed PNGs:

```powershell
.\tools\build_tilesets.ps1 -Action Import -Atlas dungeon-terrain
```

4. Open `dungeon_terrain_64_tileset.tres` in Godot and verify the affected atlas cells. Existing custom data remains attached because cell coordinates do not change.

A no-op build compares decoded pixels and does not rewrite the existing PNG, avoiding meaningless binary Git changes.

## Commands

```powershell
# Validate dimensions and TileSet references for every registered atlas
.\tools\build_tilesets.ps1 -Action Validate

# Split a packed atlas into editable source tiles (destructive to that source folder)
.\tools\build_tilesets.ps1 -Action Extract -Atlas dungeon-terrain

# Rebuild every manifest-managed atlas and validate all external atlases
.\tools\build_tilesets.ps1 -Action Build

# Validate and import (the running editor handles it, otherwise Godot runs headlessly)
.\tools\build_tilesets.ps1 -Action Import
```

Use `Extract` only to bootstrap or intentionally reset source tiles from an atlas. Normal editing starts from files already under `art/`.

## Adding An Atlas

1. Add an entry to `assets/tilesets/tilesets.json` with a stable `id`, grid dimensions, output PNG, and `.tres` path.
2. Use `mode: "packed"` for atlases built from individual source tiles. Add `sourceDirectory` and provide every `<column>_<row>.png` file.
3. Use `mode: "external"` for an atlas that is still exported manually from GIMP. It receives dimension and Godot-reference validation but is not overwritten by `Build`.
4. Run `Validate`, then switch the atlas to `packed` when its source tiles are ready.

Atlas dimensions are strict: `width = columns * 64` and `height = rows * 64`. Non-grid images such as portraits and launch art do not belong in this manifest.

## GIMP MCP

The local integration uses `maorcc/gimp-mcp`, installed under `C:/Users/antho/Tools/gimp-mcp` and registered as `gimp` in VS Code's user `mcp.json`. Its GIMP 3.2 plugin is installed under `%APPDATA%/GIMP/3.2/plug-ins/gimp-mcp-plugin/`.

After installing or updating the plugin:

1. Restart GIMP.
2. Open any image.
3. Select **Tools > MCP > Start MCP Server**.
4. In VS Code, restart/refresh the `gimp` MCP server after configuration changes.

The plugin listens only on `localhost:9877`. The MCP tools can open source tiles, inspect pixels and layers, transform artwork, and export PNGs. The repository build script remains the authority for atlas placement and validation, so an editor automation error cannot silently rearrange Godot tile coordinates.

## Updating GIMP MCP

Review upstream changes before updating the pinned local checkout, then run:

```powershell
git -C "$HOME\Tools\gimp-mcp" pull --ff-only
uv sync --directory "$HOME\Tools\gimp-mcp" --locked --python 3.12
Copy-Item "$HOME\Tools\gimp-mcp\gimp-mcp-plugin.py" "$env:APPDATA\GIMP\3.2\plug-ins\gimp-mcp-plugin\gimp-mcp-plugin.py" -Force
```

Restart GIMP after replacing the plugin.