# Launches the Unity editor for a project and waits until the MCP for Unity bridge is ready.
# Readiness signal (current MCP for Unity): ~/.unity-mcp/unity-mcp-status-<hash>.json for this
# project reports reason "ready" with a post-launch heartbeat, and its unity_port (typically 6400)
# answers TCP. Older builds served HTTP on -Port (default 8080); that probe is kept as a fallback.
# Resolves the editor from ProjectSettings/ProjectVersion.txt unless -UnityExe is given.
# First import of a fresh project can take several minutes — default timeout is generous.
param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [string]$UnityExe,
    [string]$HubEditorRoot = "C:\Program Files\Unity\Hub\Editor",
    [int]$Port = 8080,
    [int]$TimeoutSec = 900
)

function Test-Port([int]$p) {
    try {
        $c = New-Object Net.Sockets.TcpClient
        $c.Connect("127.0.0.1", $p)
        $c.Close()
        $true
    } catch { $false }
}

function Get-ProjectStatus([string]$projPath, [datetime]$since) {
    $normalized = (($projPath -replace '\\', '/').TrimEnd('/')) + "/Assets"
    foreach ($f in Get-ChildItem "$env:USERPROFILE\.unity-mcp\unity-mcp-status-*.json" -ErrorAction SilentlyContinue) {
        if ($f.LastWriteTime -lt $since) { continue }
        try { $j = Get-Content $f.FullName -Raw | ConvertFrom-Json } catch { continue }
        if ($j.project_path -eq $normalized) { return $j }
    }
    return $null
}

$versionFile = Join-Path $ProjectPath "ProjectSettings\ProjectVersion.txt"
if (-not (Test-Path $versionFile)) { Write-Error "Not a Unity project (missing $versionFile)"; exit 1 }

if (-not $UnityExe) {
    $version = ((Get-Content $versionFile | Select-String "m_EditorVersion:") -split ":")[1].Trim()
    $UnityExe = Join-Path $HubEditorRoot "$version\Editor\Unity.exe"
}
if (-not (Test-Path $UnityExe)) {
    Write-Error "Editor for this project not found: $UnityExe. Install that version via Unity Hub or pass -UnityExe explicitly."
    exit 1
}

# If this project's bridge is already ready, don't launch a second instance: Unity would refuse
# ("project already open") and quit, while the EXISTING instance's heartbeats keep the status file
# fresh — which fools naive relaunch/restart flows into thinking the new instance came up.
$normalizedAssets = (($ProjectPath -replace '\\', '/').TrimEnd('/')) + "/Assets"
foreach ($f in Get-ChildItem "$env:USERPROFILE\.unity-mcp\unity-mcp-status-*.json" -ErrorAction SilentlyContinue) {
    if (((Get-Date) - $f.LastWriteTime).TotalSeconds -gt 120) { continue }
    try { $j = Get-Content $f.FullName -Raw | ConvertFrom-Json } catch { continue }
    if ($j.project_path -eq $normalizedAssets -and $j.reason -eq "ready" -and -not $j.reloading -and (Test-Port ([int]$j.unity_port))) {
        Write-Output "MCP bridge already ready for $($j.project_name) on TCP port $($j.unity_port) — not launching a second instance."
        Write-Output "To RESTART instead: save via MCP, close the running editor, WAIT until its process is gone (verify the PID died — EditorApplication.Exit via execute_code is unreliable, prefer closing the window/process from the OS), then run this script again."
        exit 0
    }
}

$launchTime = Get-Date
$p = Start-Process -FilePath $UnityExe -PassThru -ArgumentList @("-projectPath", "`"$ProjectPath`"")
Write-Output "Launched Unity (PID $($p.Id)) for $ProjectPath — waiting for the MCP bridge (status file + TCP probe, timeout ${TimeoutSec}s)..."

$deadline = (Get-Date).AddSeconds($TimeoutSec)
while ((Get-Date) -lt $deadline) {
    if ($p.HasExited) {
        Write-Error "Unity exited early (code $($p.ExitCode)). Check the Editor log: $env:LOCALAPPDATA\Unity\Editor\Editor.log"
        exit 1
    }
    $status = Get-ProjectStatus $ProjectPath $launchTime
    if ($status -and $status.reason -eq "ready" -and -not $status.reloading -and (Test-Port ([int]$status.unity_port))) {
        Write-Output "MCP bridge ready: $($status.project_name) on TCP port $($status.unity_port) (Unity $($status.unity_version))."
        Write-Output "NOTE: MCP tools also require the UnityMCP server to be registered for this project (claude mcp add UnityMCP -- uvx --from mcpforunityserver mcp-for-unity). A Claude session started before that registration has no mcp__unityMCP__* tools — restart the session, or drive the bridge with scripts/mcp-stdio-call.py."
        exit 0
    }
    if (Test-Port $Port) {
        Write-Output "MCP answering on legacy HTTP port $Port. The editor may still be importing — poll mcpforunity://editor/state until ready."
        exit 0
    }
    Start-Sleep -Seconds 5
}

Write-Error "Timed out after $TimeoutSec s waiting for the MCP bridge. Unity (PID $($p.Id)) is still running — a first import can be very slow; check the editor window, or verify the server under Window > MCP for Unity."
exit 1
