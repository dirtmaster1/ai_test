Add-Type -AssemblyName System.Drawing

$projectRoot = Split-Path -Parent $PSScriptRoot
$atlasPath = Join-Path $projectRoot 'assets\tilesets\units_2_64.png'
$outputDirectory = Join-Path $projectRoot 'assets\animations\player'
$atlas = [System.Drawing.Bitmap]::FromFile($atlasPath)
$directions = @(
    @{ Name = 'north'; X = 0; Y = -1 },
    @{ Name = 'east'; X = 1; Y = 0 },
    @{ Name = 'south'; X = 0; Y = 1 },
    @{ Name = 'west'; X = -1; Y = 0 }
)
$units = @(
    @{
        Name = 'wizard'
        Column = 0
        Arms = @(
            @(10, 26, 15, 24, 21, 28, 21, 37, 17, 41, 11, 37),
            @(40, 27, 47, 27, 49, 36, 46, 43, 41, 39)
        )
        Legs = @()
        Sway = @(13, 43, 40, 42, 43, 61, 15, 62)
    },
    @{
        Name = 'warrior'
        Column = 1
        Arms = @(
            @(5, 26, 14, 23, 22, 28, 26, 38, 22, 45, 13, 44, 7, 37),
            @(38, 24, 49, 23, 57, 30, 56, 40, 49, 46, 40, 42)
        )
        Legs = @(
            @(15, 45, 27, 44, 29, 60, 16, 62),
            @(28, 44, 43, 44, 46, 61, 30, 61)
        )
        Sway = @()
    },
    @{
        Name = 'cleric'
        Column = 2
        Arms = @(
            @(7, 25, 14, 23, 19, 29, 19, 40, 14, 47, 8, 42),
            @(40, 26, 47, 25, 50, 35, 48, 46, 43, 48, 39, 39)
        )
        Legs = @(
            @(15, 48, 26, 47, 27, 61, 15, 61),
            @(27, 47, 39, 47, 42, 61, 28, 61)
        )
        Sway = @()
    },
    @{
        Name = 'ranger'
        Column = 3
        Arms = @(
            @(16, 26, 24, 25, 30, 32, 28, 41, 20, 43, 15, 37),
            @(39, 21, 47, 19, 51, 30, 48, 40, 41, 39, 37, 31)
        )
        Legs = @(
            @(19, 44, 29, 43, 30, 60, 18, 60),
            @(29, 43, 41, 44, 44, 60, 30, 60)
        )
        Sway = @()
    },
    @{
        Name = 'thief'
        Column = 4
        Arms = @(
            @(11, 24, 19, 22, 25, 29, 24, 40, 17, 44, 12, 38),
            @(38, 23, 46, 22, 52, 30, 48, 42, 41, 40, 37, 32)
        )
        Legs = @(
            @(15, 44, 28, 43, 29, 61, 16, 61),
            @(28, 43, 44, 44, 47, 61, 30, 61)
        )
        Sway = @()
    }
)

function New-PolygonPath {
    param(
        [double[]]$Coordinates,
        [int]$YOffset = 0
    )

    $points = [System.Drawing.PointF[]]::new($Coordinates.Length / 2)
    for ($index = 0; $index -lt $Coordinates.Length; $index += 2) {
        $points[$index / 2] = [System.Drawing.PointF]::new(
            [single]$Coordinates[$index],
            [single]($Coordinates[$index + 1] + $YOffset)
        )
    }

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddPolygon($points)
    return $path
}

function New-WalkFrame {
    param(
        [System.Drawing.Bitmap]$Source,
        [hashtable]$Unit,
        [int]$DirectionX,
        [int]$DirectionY,
        [int]$FrameIndex
    )

    $bob = @(0, -1, 0, 1)[$FrameIndex]
    $stride = @(1, 0, -1, 0)[$FrameIndex]
    $frame = [System.Drawing.Bitmap]::new(64, 64, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($frame)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.DrawImage($Source, [System.Drawing.Rectangle]::new(0, $bob, 64, 64), [System.Drawing.Rectangle]::new(0, 0, 64, 64), [System.Drawing.GraphicsUnit]::Pixel)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $limbs = @()

    for ($index = 0; $index -lt $Unit.Arms.Count; $index++) {
        $direction = if ($index -eq 0) { -1 } else { 1 }
        $limbs += @{
            Polygon = $Unit.Arms[$index]
            OffsetX = $DirectionX * $stride * $direction
            OffsetY = $DirectionY * $stride * $direction
        }
    }

    for ($index = 0; $index -lt $Unit.Legs.Count; $index++) {
        $direction = if ($index -eq 0) { 1 } else { -1 }
        $limbs += @{
            Polygon = $Unit.Legs[$index]
            OffsetX = $DirectionX * $stride * $direction
            OffsetY = $DirectionY * $stride * $direction
        }
    }

    foreach ($limb in $limbs) {
        $segment = [System.Drawing.Bitmap]::new(64, 64, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $segmentGraphics = [System.Drawing.Graphics]::FromImage($segment)
        $segmentGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $segmentGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
        $segmentPath = New-PolygonPath -Coordinates $limb.Polygon -YOffset $bob
        $segmentGraphics.SetClip($segmentPath)
        $segmentPath.Dispose()
        $segmentGraphics.DrawImage($Source, [System.Drawing.Rectangle]::new(0, $bob, 64, 64), [System.Drawing.Rectangle]::new(0, 0, 64, 64), [System.Drawing.GraphicsUnit]::Pixel)
        $segmentGraphics.ResetClip()
        $graphics.DrawImage($segment, [System.Drawing.Rectangle]::new($limb.OffsetX, $limb.OffsetY, 64, 64))
        $segmentGraphics.Dispose()
        $segment.Dispose()
    }

    if ($Unit.Sway.Count -gt 0) {
        $sway = [System.Drawing.Bitmap]::new(64, 64, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $swayGraphics = [System.Drawing.Graphics]::FromImage($sway)
        $swayGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $swayGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
        $swayPath = New-PolygonPath -Coordinates $Unit.Sway -YOffset $bob
        $swayGraphics.SetClip($swayPath)
        $swayPath.Dispose()
        $swayGraphics.DrawImage($Source, [System.Drawing.Rectangle]::new(0, $bob, 64, 64), [System.Drawing.Rectangle]::new(0, 0, 64, 64), [System.Drawing.GraphicsUnit]::Pixel)
        $swayGraphics.ResetClip()
        $swayOffsetX = $DirectionX * $stride
        $swayOffsetY = $DirectionY * $stride
        $graphics.DrawImage($sway, [System.Drawing.Rectangle]::new($swayOffsetX, $swayOffsetY, 64, 64))
        $swayGraphics.Dispose()
        $sway.Dispose()
    }

    $graphics.Dispose()
    return $frame
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
try {
    foreach ($unit in $units) {
        $sheet = [System.Drawing.Bitmap]::new(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $sheetGraphics = [System.Drawing.Graphics]::FromImage($sheet)
        $sheetGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $sheetGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
        $sheetGraphics.Clear([System.Drawing.Color]::Transparent)
        $source = $atlas.Clone(
            [System.Drawing.Rectangle]::new($unit.Column * 64, 0, 64, 64),
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
        )

        for ($directionIndex = 0; $directionIndex -lt $directions.Count; $directionIndex++) {
            for ($frameIndex = 0; $frameIndex -lt 4; $frameIndex++) {
                $frame = New-WalkFrame -Source $source -Unit $unit -DirectionX $directions[$directionIndex].X -DirectionY $directions[$directionIndex].Y -FrameIndex $frameIndex
                $sheetGraphics.DrawImage($frame, $frameIndex * 64, $directionIndex * 64)
                $frame.Dispose()
            }
        }

        $outputPath = Join-Path $outputDirectory "$($unit.Name)_walk.png"
        $sheet.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $source.Dispose()
        $sheetGraphics.Dispose()
        $sheet.Dispose()
        Write-Output "Generated $outputPath"
    }
}
finally {
    $atlas.Dispose()
}
