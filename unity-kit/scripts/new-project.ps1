# Creates an empty Unity project headlessly and waits for completion.
# Usage: new-project.ps1 -UnityExe "C:\...\Unity.exe" -ProjectPath "C:\...\MyGame"
param(
    [Parameter(Mandatory)][string]$UnityExe,
    [Parameter(Mandatory)][string]$ProjectPath,
    [int]$TimeoutSec = 600
)

if (-not (Test-Path $UnityExe)) { Write-Error "Unity editor not found: $UnityExe"; exit 1 }
if (Test-Path $ProjectPath) { Write-Error "Path already exists: $ProjectPath — refusing to overwrite."; exit 1 }

$logFile = Join-Path $env:TEMP ("unity-create-" + [IO.Path]::GetFileName($ProjectPath) + ".log")
$p = Start-Process -FilePath $UnityExe -PassThru -ArgumentList @(
    "-batchmode", "-quit", "-createProject", "`"$ProjectPath`"", "-logFile", "`"$logFile`""
)

if (-not $p.WaitForExit($TimeoutSec * 1000)) {
    $p.Kill()
    Write-Error "Timed out after $TimeoutSec s creating the project. Log: $logFile"
    exit 1
}

if (-not (Test-Path (Join-Path $ProjectPath "ProjectSettings\ProjectVersion.txt"))) {
    Write-Error "Unity exited (code $($p.ExitCode)) but the project was not created. Log: $logFile"
    exit 1
}

Write-Output "Created Unity project at $ProjectPath (editor exit code $($p.ExitCode), log: $logFile)"
