[CmdletBinding()]
param(
    [string]$InputImage = 'art/vfx/forest_water_ripples_source.png',
    [string]$OutputImage = 'assets/vfx/forest_water_ripples_64x4.png'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$projectRoot = Split-Path -Parent $PSScriptRoot
$inputPath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $InputImage))
$outputPath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $OutputImage))
if (-not (Test-Path -LiteralPath $inputPath -PathType Leaf)) {
    throw "Water animation source not found: $InputImage"
}

function Blend-Color([System.Drawing.Color]$first, [System.Drawing.Color]$second, [double]$weight) {
    $red = [int][Math]::Round($first.R + ($second.R - $first.R) * $weight)
    $green = [int][Math]::Round($first.G + ($second.G - $first.G) * $weight)
    $blue = [int][Math]::Round($first.B + ($second.B - $first.B) * $weight)
    return [System.Drawing.Color]::FromArgb(255, $red, $green, $blue)
}

$source = [System.Drawing.Bitmap]::new($inputPath)
if ($source.Width -ne 256 -or $source.Height -ne 256) {
    $source.Dispose()
    throw "Expected a 256x256 four-by-four sheet, found $($source.Width)x$($source.Height)"
}

$output = [System.Drawing.Bitmap]::new(256, 64, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
try {
    $frameIndexes = @(0, 4, 8, 12)
    for ($outputFrame = 0; $outputFrame -lt $frameIndexes.Count; $outputFrame++) {
        $frameIndex = $frameIndexes[$outputFrame]
        $frame = [System.Drawing.Bitmap]::new(64, 64, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $horizontal = [System.Drawing.Bitmap]::new(64, 64, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $seamless = [System.Drawing.Bitmap]::new(64, 64, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $sourceX = ($frameIndex % 4) * 64
            $sourceY = [int][Math]::Floor($frameIndex / 4) * 64
            # The source's outer eight pixels contain a flickering frame, not water motion.
            for ($y = 0; $y -lt 64; $y++) {
                for ($x = 0; $x -lt 64; $x++) {
                    $sampleX = $sourceX + 8 + [int][Math]::Floor($x * 48.0 / 64.0)
                    $sampleY = $sourceY + 8 + [int][Math]::Floor($y * 48.0 / 64.0)
                    $frame.SetPixel($x, $y, $source.GetPixel($sampleX, $sampleY))
                }
            }

            for ($y = 0; $y -lt 64; $y++) {
                $edge = Blend-Color ($frame.GetPixel(0, $y)) ($frame.GetPixel(63, $y)) 0.5
                for ($x = 0; $x -lt 64; $x++) {
                    if ($x -lt 8) {
                        $color = Blend-Color $edge ($frame.GetPixel($x, $y)) ($x / 7.0)
                    }
                    elseif ($x -ge 56) {
                        $color = Blend-Color $edge ($frame.GetPixel($x, $y)) ((63 - $x) / 7.0)
                    }
                    else {
                        $color = $frame.GetPixel($x, $y)
                    }
                    $horizontal.SetPixel($x, $y, $color)
                }
            }

            for ($x = 0; $x -lt 64; $x++) {
                $edge = Blend-Color ($horizontal.GetPixel($x, 0)) ($horizontal.GetPixel($x, 63)) 0.5
                for ($y = 0; $y -lt 64; $y++) {
                    if ($y -lt 8) {
                        $color = Blend-Color $edge ($horizontal.GetPixel($x, $y)) ($y / 7.0)
                    }
                    elseif ($y -ge 56) {
                        $color = Blend-Color $edge ($horizontal.GetPixel($x, $y)) ((63 - $y) / 7.0)
                    }
                    else {
                        $color = $horizontal.GetPixel($x, $y)
                    }
                    $seamless.SetPixel($x, $y, $color)
                    $output.SetPixel($outputFrame * 64 + $x, $y, $color)
                }
            }
        }
        finally {
            $frame.Dispose()
            $horizontal.Dispose()
            $seamless.Dispose()
        }
    }

    [System.IO.Directory]::CreateDirectory((Split-Path -Parent $outputPath)) | Out-Null
    $output.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $output.Dispose()
    $source.Dispose()
}

Write-Output "Prepared four seamless 64x64 water frames: $outputPath"
