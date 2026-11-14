$ErrorActionPreference = 'Stop'

# Keep the original entry point without recreating the obsolete shifted-limb sheets.
& (Join-Path $PSScriptRoot 'pack_player_walk_prototype.ps1')
