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

$ErrorActionPreference = "Stop"
$ProjectPath = (Resolve-Path $ProjectPath).Path

$versionFile = Join-Path $ProjectPath "ProjectSettings/ProjectVersion.txt"
if (-not (Test-Path $versionFile)) { Write-Error "Not a Unity project: $versionFile missing"; exit 3 }
$version = (Select-String -Path $versionFile -Pattern "m_EditorVersion:\s*(\S+)").Matches[0].Groups[1].Value

if (Test-Path (Join-Path $ProjectPath "Temp/UnityLockfile")) {
    Write-Error "Temp/UnityLockfile exists - the editor has this project open. Close it (or use in-editor run_tests via MCP instead)."
    exit 3
}

# Locate the editor: default Hub path first, then find-unity.ps1 (lists all editors as JSON).
$unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
if (-not (Test-Path $unity)) {
    $finder = Join-Path $PSScriptRoot "find-unity.ps1"
    if (Test-Path $finder) {
        $match = @(& $finder | ConvertFrom-Json) | Where-Object version -eq $version | Select-Object -First 1
        if ($match) { $unity = $match.exe }
    }
}
if (-not $unity -or -not (Test-Path $unity)) { Write-Error "Unity $version not found (checked default Hub path and find-unity.ps1)"; exit 3 }

$resultsDir = Join-Path $ProjectPath "TestResults"
New-Item -ItemType Directory -Force $resultsDir | Out-Null

$platforms = if ($Platform -eq "Both") { @("EditMode", "PlayMode") } else { @($Platform) }
$worst = 0

foreach ($p in $platforms) {
    $xml = Join-Path $resultsDir "$($p.ToLower())-results.xml"
    $log = Join-Path $resultsDir "$($p.ToLower()).log"
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
    # Unity.exe is a GUI-subsystem binary: '&' returns immediately, so Start-Process -Wait it.
    Write-Host "[$p] Unity $version -> $xml"
    $proc = Start-Process -FilePath $unity -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow
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
        Get-Content $log -Tail 25 | ForEach-Object { Write-Host "  $_" }
        $code = 3
    }
    if ($code -ne 0) { $worst = [Math]::Max($worst, $code) }
}
exit $worst
