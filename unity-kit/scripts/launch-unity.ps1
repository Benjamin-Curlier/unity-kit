# Launches the Unity editor for a project and waits until the MCP for Unity server answers.
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

$portWasOpen = Test-Port $Port
if ($portWasOpen) {
    Write-Output "NOTE: port $Port already answers — another editor instance is serving MCP. The new instance will join the shared hub; use set_active_instance / mcpforunity://instances to route calls."
}

$p = Start-Process -FilePath $UnityExe -PassThru -ArgumentList @("-projectPath", "`"$ProjectPath`"")
Write-Output "Launched Unity (PID $($p.Id)) for $ProjectPath — waiting for MCP on port $Port (timeout ${TimeoutSec}s)..."

$deadline = (Get-Date).AddSeconds($TimeoutSec)
while ((Get-Date) -lt $deadline) {
    if ($p.HasExited) {
        Write-Error "Unity exited early (code $($p.ExitCode)). Check the Editor log: $env:LOCALAPPDATA\Unity\Editor\Editor.log"
        exit 1
    }
    if (Test-Port $Port) {
        if ($portWasOpen) {
            Write-Output "MCP port $Port is answering (was already up before launch — the new instance may still be importing; poll mcpforunity://editor/state and mcpforunity://instances before work)."
        } else {
            Write-Output "MCP server is answering on port $Port. The editor may still be importing — poll mcpforunity://editor/state until ready."
        }
        exit 0
    }
    Start-Sleep -Seconds 5
}

Write-Error "Timed out after $TimeoutSec s waiting for MCP port $Port. Unity (PID $($p.Id)) is still running — a first import can be very slow; check the editor window, or verify auto-start under Window > MCP for Unity."
exit 1
