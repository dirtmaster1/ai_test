param(
    [string]$TscnPath = "scenes\Gameworld.tscn",
    [Parameter(Mandatory = $true)][string]$LayerName
)

$content = Get-Content $TscnPath -Raw
$pattern = '(?ms)\[node name="' + [regex]::Escape($LayerName) + '"[^\]]*type="TileMapLayer"[^\]]*\](?<body>.*?)(?=\r?\n\[|\z)'
$m = [regex]::Match($content, $pattern)
if (-not $m.Success) { Write-Error "layer not found"; exit 1 }
$dataMatch = [regex]::Match($m.Groups['body'].Value, 'tile_map_data\s*=\s*PackedByteArray\("(?<b64>[^"]+)"\)')
$bytes = [Convert]::FromBase64String($dataMatch.Groups['b64'].Value)

$cells = @{}
$minX = [int]::MaxValue; $maxX = [int]::MinValue; $minY = [int]::MaxValue; $maxY = [int]::MinValue
for ($i = 2; $i -lt $bytes.Length; $i += 12) {
    $x = [BitConverter]::ToInt16($bytes, $i)
    $y = [BitConverter]::ToInt16($bytes, $i + 2)
    $ax = [BitConverter]::ToInt16($bytes, $i + 6)
    $ay = [BitConverter]::ToInt16($bytes, $i + 8)
    $cells["$x,$y"] = "$ax,$ay"
    if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
    if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
}

Write-Output "layer=$LayerName cells=$($cells.Count) bounds=($minX,$minY)..($maxX,$maxY)"
# Legend: each distinct atlas coord gets a letter
$legend = @{}
$nextChar = 65
for ($y = $minY; $y -le $maxY; $y++) {
    $row = ""
    for ($x = $minX; $x -le $maxX; $x++) {
        $key = "$x,$y"
        if (-not $cells.ContainsKey($key)) { $row += "."; continue }
        $atlas = $cells[$key]
        if (-not $legend.ContainsKey($atlas)) { $legend[$atlas] = [char]$nextChar; $nextChar++ }
        $row += $legend[$atlas]
    }
    Write-Output ("{0,4}: {1}" -f $y, $row)
}
Write-Output "Legend:"
$legend.GetEnumerator() | Sort-Object Value | ForEach-Object { Write-Output ("  {0} = atlas({1})" -f $_.Value, $_.Key) }
