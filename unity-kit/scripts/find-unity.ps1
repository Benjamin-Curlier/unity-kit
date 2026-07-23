# Lists installed Unity editors as JSON: [{version, channel, exe}], newest first.
# Channel: stable (f), beta (b), alpha (a).
# -Modules also reports build-support modules per editor (dedicated server, il2cpp, platforms)
# by inspecting PlaybackEngines — needed by netcode/dedicated-server preflights.
param(
    [string]$SearchRoot = "C:\Program Files\Unity\Hub\Editor",
    [switch]$Modules
)

$editors = @(Get-ChildItem -Path $SearchRoot -Directory -ErrorAction SilentlyContinue | ForEach-Object {
    $exe = Join-Path $_.FullName "Editor\Unity.exe"
    if (Test-Path $exe) {
        $v = $_.Name
        $channel = if ($v -match "f\d") { "stable" }
                   elseif ($v -match "b\d") { "beta" }
                   elseif ($v -match "a\d") { "alpha" }
                   else { "unknown" }
        $entry = [pscustomobject]@{ version = $v; channel = $channel; exe = $exe }
        if ($Modules) {
            $pe = Join-Path $_.FullName "Editor\Data\PlaybackEngines"
            $winVariations = Join-Path $pe "WindowsStandaloneSupport\Variations"
            $mods = [pscustomobject]@{
                "windows-server" = (Test-Path $winVariations) -and
                    @(Get-ChildItem $winVariations -Directory -ErrorAction SilentlyContinue |
                      Where-Object Name -like "*server*").Count -gt 0
                "windows-il2cpp" = (Test-Path $winVariations) -and
                    @(Get-ChildItem $winVariations -Directory -ErrorAction SilentlyContinue |
                      Where-Object Name -like "*il2cpp*").Count -gt 0
                "linux"          = Test-Path (Join-Path $pe "LinuxStandaloneSupport")
                "linux-server"   = (Test-Path (Join-Path $pe "LinuxStandaloneSupport\Variations")) -and
                    @(Get-ChildItem (Join-Path $pe "LinuxStandaloneSupport\Variations") -Directory -ErrorAction SilentlyContinue |
                      Where-Object Name -like "*server*").Count -gt 0
                "android"        = Test-Path (Join-Path $pe "AndroidPlayer")
                "webgl"          = Test-Path (Join-Path $pe "WebGLSupport")
                "ios"            = Test-Path (Join-Path $pe "iOSSupport")
            }
            $entry | Add-Member -NotePropertyName modules -NotePropertyValue $mods
        }
        $entry
    }
})

if ($editors.Count -eq 0) {
    Write-Error "No Unity editors found under $SearchRoot. Install one via Unity Hub, or pass -SearchRoot if Hub uses a custom install location."
    exit 1
}

# -InputObject with @() keeps single-element results as a JSON array on both PS 5.1 and 7+
ConvertTo-Json -InputObject @($editors | Sort-Object version -Descending)
