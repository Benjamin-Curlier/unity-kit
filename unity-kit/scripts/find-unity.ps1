# Lists installed Unity editors as JSON: [{version, channel, exe}], newest first.
# Channel: stable (f), beta (b), alpha (a).
param(
    [string]$SearchRoot = "C:\Program Files\Unity\Hub\Editor"
)

$editors = @(Get-ChildItem -Path $SearchRoot -Directory -ErrorAction SilentlyContinue | ForEach-Object {
    $exe = Join-Path $_.FullName "Editor\Unity.exe"
    if (Test-Path $exe) {
        $v = $_.Name
        $channel = if ($v -match "f\d") { "stable" }
                   elseif ($v -match "b\d") { "beta" }
                   elseif ($v -match "a\d") { "alpha" }
                   else { "unknown" }
        [pscustomobject]@{ version = $v; channel = $channel; exe = $exe }
    }
})

if ($editors.Count -eq 0) {
    Write-Error "No Unity editors found under $SearchRoot. Install one via Unity Hub, or pass -SearchRoot if Hub uses a custom install location."
    exit 1
}

# -InputObject with @() keeps single-element results as a JSON array on both PS 5.1 and 7+
ConvertTo-Json -InputObject @($editors | Sort-Object version -Descending)
