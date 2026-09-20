[CmdletBinding()]
param(
    [ValidateSet('Build', 'Extract', 'Validate', 'Import')]
    [string]$Action = 'Build',
    [string]$Atlas,
    [string]$Manifest = 'assets/tilesets/tilesets.json',
    [string]$GodotPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.Drawing

$projectRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $projectRoot $Manifest
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Tileset manifest not found: $manifestPath"
}

$configuration = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$tileSize = [int]$configuration.tileSize
$atlases = @($configuration.atlases)
if ($Atlas) {
    $atlases = @($atlases | Where-Object { $_.id -eq $Atlas })
    if ($atlases.Count -eq 0) {
        throw "Atlas '$Atlas' is not defined in $Manifest"
    }
}

function Resolve-ProjectPath([string]$relativePath) {
    return [System.IO.Path]::GetFullPath((Join-Path $projectRoot $relativePath))
}

function Open-Bitmap([string]$path) {
    $stream = [System.IO.File]::Open($path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
    try {
        $loaded = [System.Drawing.Image]::FromStream($stream)
        try {
            return [System.Drawing.Bitmap]::new($loaded)
        }
        finally {
            $loaded.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Test-EqualPixels([System.Drawing.Bitmap]$left, [System.Drawing.Bitmap]$right) {
    if ($left.Width -ne $right.Width -or $left.Height -ne $right.Height) {
        return $false
    }
    for ($row = 0; $row -lt $left.Height; $row++) {
        for ($column = 0; $column -lt $left.Width; $column++) {
            if ($left.GetPixel($column, $row).ToArgb() -ne $right.GetPixel($column, $row).ToArgb()) {
                return $false
            }
        }
    }
    return $true
}

function Assert-Atlas([object]$definition) {
    $outputPath = Resolve-ProjectPath $definition.output
    if (-not (Test-Path -LiteralPath $outputPath -PathType Leaf)) {
        throw "[$($definition.id)] Missing atlas: $($definition.output)"
    }

    $image = Open-Bitmap $outputPath
    try {
        $expectedWidth = [int]$definition.columns * $tileSize
        $expectedHeight = [int]$definition.rows * $tileSize
        if ($image.Width -ne $expectedWidth -or $image.Height -ne $expectedHeight) {
            throw "[$($definition.id)] Expected ${expectedWidth}x${expectedHeight}, found $($image.Width)x$($image.Height)"
        }
    }
    finally {
        $image.Dispose()
    }

    $tileSetPath = Resolve-ProjectPath $definition.tileSet
    if (-not (Test-Path -LiteralPath $tileSetPath -PathType Leaf)) {
        throw "[$($definition.id)] Missing TileSet resource: $($definition.tileSet)"
    }

    $godotTexturePath = 'res://' + ($definition.output -replace '\\', '/')
    $tileSetText = Get-Content -LiteralPath $tileSetPath -Raw
    if (-not $tileSetText.Contains($godotTexturePath)) {
        throw "[$($definition.id)] $($definition.tileSet) does not reference $godotTexturePath"
    }
}

function Export-AtlasTiles([object]$definition) {
    if ($definition.mode -ne 'packed') {
        throw "[$($definition.id)] Extract requires mode 'packed'"
    }

    $outputPath = Resolve-ProjectPath $definition.output
    $sourceDirectory = Resolve-ProjectPath $definition.sourceDirectory
    [System.IO.Directory]::CreateDirectory($sourceDirectory) | Out-Null
    $atlasImage = Open-Bitmap $outputPath
    try {
        for ($row = 0; $row -lt [int]$definition.rows; $row++) {
            for ($column = 0; $column -lt [int]$definition.columns; $column++) {
                $tile = [System.Drawing.Bitmap]::new($tileSize, $tileSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
                try {
                    $graphics = [System.Drawing.Graphics]::FromImage($tile)
                    try {
                        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                        $sourceRect = [System.Drawing.Rectangle]::new($column * $tileSize, $row * $tileSize, $tileSize, $tileSize)
                        $graphics.DrawImage($atlasImage, [System.Drawing.Rectangle]::new(0, 0, $tileSize, $tileSize), $sourceRect, [System.Drawing.GraphicsUnit]::Pixel)
                    }
                    finally {
                        $graphics.Dispose()
                    }
                    $tile.Save((Join-Path $sourceDirectory ("{0}_{1}.png" -f $column, $row)), [System.Drawing.Imaging.ImageFormat]::Png)
                }
                finally {
                    $tile.Dispose()
                }
            }
        }
    }
    finally {
        $atlasImage.Dispose()
    }
    Write-Host "[$($definition.id)] Extracted tiles to $($definition.sourceDirectory)"
}

function Build-Atlas([object]$definition) {
    if ($definition.mode -ne 'packed') {
        Write-Host "[$($definition.id)] External atlas validated; no build performed"
        return
    }

    $sourceDirectory = Resolve-ProjectPath $definition.sourceDirectory
    $outputPath = Resolve-ProjectPath $definition.output
    $width = [int]$definition.columns * $tileSize
    $height = [int]$definition.rows * $tileSize
    $atlasImage = [System.Drawing.Bitmap]::new($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($atlasImage)
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
            for ($row = 0; $row -lt [int]$definition.rows; $row++) {
                for ($column = 0; $column -lt [int]$definition.columns; $column++) {
                    $tilePath = Join-Path $sourceDirectory ("{0}_{1}.png" -f $column, $row)
                    if (-not (Test-Path -LiteralPath $tilePath -PathType Leaf)) {
                        throw "[$($definition.id)] Missing source tile: $tilePath"
                    }
                    $tile = Open-Bitmap $tilePath
                    try {
                        if ($tile.Width -ne $tileSize -or $tile.Height -ne $tileSize) {
                            throw "[$($definition.id)] Tile $column,$row must be ${tileSize}x${tileSize}; found $($tile.Width)x$($tile.Height)"
                        }
                        $destination = [System.Drawing.Rectangle]::new($column * $tileSize, $row * $tileSize, $tileSize, $tileSize)
                        $graphics.DrawImage($tile, $destination, [System.Drawing.Rectangle]::new(0, 0, $tileSize, $tileSize), [System.Drawing.GraphicsUnit]::Pixel)
                    }
                    finally {
                        $tile.Dispose()
                    }
                }
            }
        }
        finally {
            $graphics.Dispose()
        }

        if (Test-Path -LiteralPath $outputPath -PathType Leaf) {
            $existing = Open-Bitmap $outputPath
            try {
                if (Test-EqualPixels $atlasImage $existing) {
                    Write-Host "[$($definition.id)] Atlas pixels unchanged"
                    return
                }
            }
            finally {
                $existing.Dispose()
            }
        }

        [System.IO.Directory]::CreateDirectory((Split-Path -Parent $outputPath)) | Out-Null
        $temporaryPath = "$outputPath.tmp.png"
        $atlasImage.Save($temporaryPath, [System.Drawing.Imaging.ImageFormat]::Png)
        Move-Item -LiteralPath $temporaryPath -Destination $outputPath -Force
    }
    finally {
        $atlasImage.Dispose()
    }
    Write-Host "[$($definition.id)] Built $($definition.output)"
}

function Resolve-GodotExecutable {
    if ($GodotPath) {
        return $GodotPath
    }
    if ($env:GODOT4) {
        return $env:GODOT4
    }
    foreach ($commandName in @('godot4', 'godot')) {
        $command = Get-Command $commandName -ErrorAction SilentlyContinue
        if ($command) {
            return $command.Source
        }
    }
    $localGodot = Get-ChildItem (Join-Path $HOME 'Godot*_mono_win64\Godot*_mono_win64.exe') -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($localGodot) {
        return $localGodot.FullName
    }
    throw 'Godot executable not found. Pass -GodotPath or set GODOT4.'
}

switch ($Action) {
    'Extract' {
        foreach ($definition in $atlases) { Export-AtlasTiles $definition }
    }
    'Build' {
        foreach ($definition in $atlases) { Build-Atlas $definition }
        foreach ($definition in $atlases) { Assert-Atlas $definition }
    }
    'Validate' {
        foreach ($definition in $atlases) { Assert-Atlas $definition }
    }
    'Import' {
        foreach ($definition in $atlases) { Assert-Atlas $definition }
        $runningEditor = Get-Process -Name 'Godot*' -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle } | Select-Object -First 1
        if ($runningEditor) {
            Write-Host "Godot editor is running; it will import changed atlas files automatically."
            break
        }
        $godot = Resolve-GodotExecutable
        $projectFile = Get-ChildItem -LiteralPath $projectRoot -Filter '*.csproj' | Select-Object -First 1
        $projectFileBytes = if ($projectFile) { [System.IO.File]::ReadAllBytes($projectFile.FullName) } else { $null }
        try {
            & $godot --headless --path $projectRoot --editor --import --quit
            if ($LASTEXITCODE -ne 0) {
                throw "Godot import failed with exit code $LASTEXITCODE"
            }
        }
        finally {
            if ($projectFile -and $projectFileBytes) {
                [System.IO.File]::WriteAllBytes($projectFile.FullName, $projectFileBytes)
                Remove-Item -LiteralPath ($projectFile.FullName + '.old') -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

Write-Host "Tileset $Action completed for $($atlases.Count) atlas(es)."