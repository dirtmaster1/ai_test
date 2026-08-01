param(
    [string]$TscnPath = "scenes\Gameworld.tscn",
    [string]$LayerFilter = ".*"
)

$content = Get-Content $TscnPath -Raw
$pattern = '(?ms)\[node name="(?<name>[^"]+)"[^\]]*type="TileMapLayer"[^\]]*\](?<body>.*?)(?=\r?\n\[|\z)'
$matches2 = [regex]::Matches($content, $pattern)

foreach ($m in $matches2) {
    $name = $m.Groups['name'].Value
    if ($name -notmatch $LayerFilter) { continue }
    $body = $m.Groups['body'].Value
    $dataMatch = [regex]::Match($body, 'tile_map_data\s*=\s*PackedByteArray\("(?<b64>[^"]+)"\)')
    if (-not $dataMatch.Success) { Write-Output "== $name : no tile_map_data =="; continue }
    $bytes = [Convert]::FromBase64String($dataMatch.Groups['b64'].Value)
    Write-Output "== $name : $((($bytes.Length - 2) / 12)) cells =="
    $cells = @()
    for ($i = 2; $i -lt $bytes.Length; $i += 12) {
        $x = [BitConverter]::ToInt16($bytes, $i)
        $y = [BitConverter]::ToInt16($bytes, $i + 2)
        $src = [BitConverter]::ToInt16($bytes, $i + 4)
        $ax = [BitConverter]::ToInt16($bytes, $i + 6)
        $ay = [BitConverter]::ToInt16($bytes, $i + 8)
        $alt = [BitConverter]::ToInt16($bytes, $i + 10)
        $cells += [pscustomobject]@{ X = $x; Y = $y; Src = $src; AX = $ax; AY = $ay; Alt = $alt }
    }
    # Print cell list
    $cells | Sort-Object Y, X | ForEach-Object { Write-Output ("  ({0},{1}) src={2} atlas=({3},{4}) alt={5}" -f $_.X, $_.Y, $_.Src, $_.AX, $_.AY, $_.Alt) }
}
