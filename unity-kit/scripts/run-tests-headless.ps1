# unity-kit: run Unity Test Framework tests headless (no editor GUI).
# Usage: .\run-tests-headless.ps1 [-ProjectPath .] [-Platform EditMode|PlayMode|Both] [-TestFilter <regex>] [-NoGraphics]
# Exit code: 0 all green, 2 tests failed, 3 run did not complete (compile error, license, lock, crash).
# Preconditions that WILL bite (see unity-ci skill): the editor GUI must be CLOSED for this project
# (Temp/UnityLockfile), the machine's Unity license must be activated, and the first run on a clean
# checkout imports Library (minutes, not seconds).
param(
    [string]$ProjectPath = ".",
    [ValidateSet("EditMode", "PlayMode", "Both")]
    [string]$Platform = "Both",
    [string]$TestFilter = "",
    [switch]$NoGraphics
)

# NOTE: precondition failures write to stderr and `exit 3` — do NOT route them through
# Write-Error under $ErrorActionPreference=Stop, which would terminate with exit 1 instead.
function Fail3([string]$msg) { [Console]::Error.WriteLine($msg); exit 3 }

if (-not (Test-Path $ProjectPath)) { Fail3 "-ProjectPath does not exist: $ProjectPath" }
$ProjectPath = (Resolve-Path $ProjectPath).Path.TrimEnd('\', '/')
if ($ProjectPath -match '^[A-Za-z]:$') { $ProjectPath += '\' }

$versionFile = Join-Path $ProjectPath "ProjectSettings/ProjectVersion.txt"
if (-not (Test-Path $versionFile)) { Fail3 "Not a Unity project: $versionFile missing" }
$vm = Select-String -Path $versionFile -Pattern "m_EditorVersion:\s*(\S+)"
if (-not $vm) { Fail3 "Could not parse m_EditorVersion from $versionFile" }
$version = $vm.Matches[0].Groups[1].Value

# A live editor holds an OS lock on Temp/UnityLockfile; a stale file from a crash does not.
# Deleting is the reliable probe: it fails if and only if a running Unity owns the lock.
$lock = Join-Path $ProjectPath "Temp/UnityLockfile"
if (Test-Path $lock) {
    try { Remove-Item $lock -Force -ErrorAction Stop }
    catch { Fail3 "The editor has this project open (Temp/UnityLockfile is held). Close it, or use in-editor run_tests via MCP instead." }
}

# Locate the editor: default Hub path first, then find-unity.ps1 (lists all editors as JSON).
$unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
if (-not (Test-Path $unity)) {
    $finder = Join-Path $PSScriptRoot "find-unity.ps1"
    if (Test-Path $finder) {
        try {
            $match = @(& $finder 2>$null | ConvertFrom-Json) | Where-Object version -eq $version | Select-Object -First 1
            if ($match) { $unity = $match.exe }
        } catch { }
    }
}
if (-not $unity -or -not (Test-Path $unity)) { Fail3 "Unity $version not found (checked default Hub path and find-unity.ps1)" }

$resultsDir = Join-Path $ProjectPath "TestResults"
New-Item -ItemType Directory -Force $resultsDir | Out-Null

$platforms = if ($Platform -eq "Both") { @("EditMode", "PlayMode") } else { @($Platform) }
$worst = 0

foreach ($p in $platforms) {
    $xml = Join-Path $resultsDir "$($p.ToLower())-results.xml"
    $log = Join-Path $resultsDir "$($p.ToLower()).log"
    # Stale artifacts from a previous run would be reported as this run's results — delete first,
    # so "no XML after the run" reliably means the run did not complete.
    Remove-Item $xml, $log -Force -ErrorAction SilentlyContinue
    $unityArgs = @(
        "-batchmode", "-projectPath", "`"$ProjectPath`"",
        "-runTests", "-testPlatform", $p,
        "-testResults", "`"$xml`"", "-logFile", "`"$log`"",
        "-accept-apiupdate", "-forgetProjectPath"
    )
    # -nographics is safe for EditMode; PlayMode tests that touch rendering need a (hidden) window.
    if ($NoGraphics -or $p -eq "EditMode") { $unityArgs += "-nographics" }
    if ($TestFilter) { $unityArgs += @("-testFilter", "`"$TestFilter`"") }
    # NOTE: no -quit — the test runner exits by itself; -quit can kill it mid-run.
    # Unity.exe is a GUI-subsystem binary: '&' returns immediately. WaitForExit() on the PID
    # (not Start-Process -Wait, which on PS 5.1 also waits for descendants like the licensing
    # client and can hang after tests finish).
    Write-Host "[$p] Unity $version -> $xml"
    $proc = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru -NoNewWindow
    $proc.WaitForExit()
    $code = $proc.ExitCode

    if (Test-Path $xml) {
        $r = ([xml](Get-Content $xml)).'test-run'
        Write-Host ("[$p] total={0} passed={1} failed={2} skipped={3} (exit {4})" -f $r.total, $r.passed, $r.failed, $r.skipped, $code)
        if ([int]$r.failed -gt 0) {
            Select-Xml -Path $xml -XPath "//test-case[@result='Failed']" | ForEach-Object {
                $msg = $_.Node.failure.message
                if ($msg -is [System.Xml.XmlElement]) { $msg = $msg.InnerText }
                Write-Host ("  FAIL {0}: {1}" -f $_.Node.fullname, ([string]$msg).Trim())
            }
        }
    } else {
        Write-Host "[$p] no results XML written (exit $code) - run did not complete; last log lines:"
        Get-Content $log -Tail 25 -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "  $_" }
        $code = 3
    }
    if ($code -ne 0) { $worst = [Math]::Max($worst, $code) }
}
exit $worst
