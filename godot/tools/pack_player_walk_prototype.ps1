param(
    [string]$SourceDirectory = (Join-Path $PSScriptRoot '..\art\player_walk_prototype'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\assets\animations\player'),
    [ValidateSet('warrior', 'wizard', 'ranger', 'cleric', 'thief')]
    [string[]]$Classes = @('warrior', 'wizard', 'ranger', 'cleric', 'thief')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$directions = @('north', 'east', 'south', 'west')
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

foreach ($name in $Classes) {
    $walk = [System.Drawing.Bitmap]::new(512, 256)
    $idle = [System.Drawing.Bitmap]::new(64, 256)
    $walkGraphics = [System.Drawing.Graphics]::FromImage($walk)
    $idleGraphics = [System.Drawing.Graphics]::FromImage($idle)
    $walkGraphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $idleGraphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    foreach ($graphics in @($walkGraphics, $idleGraphics)) {
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    }
    try {
        for ($row = 0; $row -lt $directions.Count; $row++) {
            $direction = $directions[$row]
            $frames = [System.Drawing.Bitmap]::FromFile((Join-Path $SourceDirectory "$name-$direction-walk.png"))
            try {
                if ($frames.Width -ne 256 -or $frames.Height -ne 128) {
                    throw "$name-$direction-walk.png must contain eight 64x64 frames in a 4x2 grid."
                }
                for ($frame = 0; $frame -lt 8; $frame++) {
                    $source = [System.Drawing.Rectangle]::new(($frame % 4) * 64, [int][Math]::Floor($frame / 4) * 64, 64, 64)
                    # Use one fixed fit for every pose, never per-frame cropping that would shift the anchor.
                    $target = [System.Drawing.Rectangle]::new($frame * 64 + 1, $row * 64 + 1, 62, 62)
                    $walkGraphics.DrawImage($frames, $target, $source, [System.Drawing.GraphicsUnit]::Pixel)
                }
                $idleGraphics.DrawImage($frames, [System.Drawing.Rectangle]::new(1, $row * 64 + 1, 62, 62),
                    [System.Drawing.Rectangle]::new(0, 0, 64, 64), [System.Drawing.GraphicsUnit]::Pixel)
            }
            finally {
                $frames.Dispose()
            }
        }
        $walk.Save((Join-Path $OutputDirectory "${name}_walk.png"), [System.Drawing.Imaging.ImageFormat]::Png)
        $idle.Save((Join-Path $OutputDirectory "${name}_idle.png"), [System.Drawing.Imaging.ImageFormat]::Png)
        Write-Output "Packed ${name}: eight frames per direction and four resting poses."
    }
    finally {
        $walkGraphics.Dispose()
        $idleGraphics.Dispose()
        $walk.Dispose()
        $idle.Dispose()
    }
}
