# Windows PowerShell 5.1 reads scripts using the system code page; non-ASCII
# string literals (e.g. Korean) can be mis-decoded under CP949 and break the
# parser. Force UTF-8 output and keep user-facing messages ASCII-only.
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$unityPath = "$env:UNITY_PATH"
$projectPath = "$env:GITHUB_WORKSPACE\Tests~\E2E\$env:PROJECT_DIR_PREFIX-$env:UNITY_VERSION_PATTERN"
$resultsFile = "$projectPath\editmode-results.xml"
$maxWaitSeconds = 600  # Unity process max wait (10 minutes)

Write-Host "Running EditMode Tests..."
Write-Host "Unity Version: $env:UNITY_VERSION_FULL"

$logFile = "$env:RUNNER_TEMP\unity-editmode-$env:UNITY_VERSION_PATTERN.log"

$proc = Start-Process -FilePath $unityPath -ArgumentList @(
  "-batchmode", "-nographics",
  "-projectPath", $projectPath,
  "-runTests", "-testPlatform", "EditMode",
  "-testResults", $resultsFile,
  "-logFile", $logFile
) -PassThru -NoNewWindow

# Stream log to console and watch for timeout
$logReader = $null
$waitSeconds = 0
while (-not $proc.HasExited) {
  Start-Sleep -Seconds 3
  $waitSeconds += 3

  if (-not $logReader -and (Test-Path $logFile)) {
    $logReader = [System.IO.StreamReader]::new(
      [System.IO.FileStream]::new($logFile, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
    )
  }
  if ($logReader) {
    $chunk = $logReader.ReadToEnd()
    if ($chunk) { Write-Host $chunk -NoNewline }
  }

  # Force-kill if process does not exit within 60s after results file was written
  if ((Test-Path $resultsFile) -and $waitSeconds -gt 60) {
    $resultAge = (New-TimeSpan -Start (Get-Item $resultsFile).LastWriteTime -End (Get-Date)).TotalSeconds
    if ($resultAge -gt 60) {
      Write-Host "::warning::Unity process did not exit within 60s after test results were written. Force killing (pid: $($proc.Id))..."
      Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
      break
    }
  }

  if ($waitSeconds -ge $maxWaitSeconds) {
    Write-Host "::warning::Unity process timed out after ${maxWaitSeconds}s. Force killing (pid: $($proc.Id))..."
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    break
  }
}

if ($logReader) {
  Start-Sleep -Seconds 1
  $chunk = $logReader.ReadToEnd()
  if ($chunk) { Write-Host $chunk -NoNewline }
  $logReader.Close()
}

# Inspect results - missing results file means infra fault (license/Hub/cache),
# so fail the step loudly so the classifier picks it up as results-missing.
if (Test-Path $resultsFile) {
  $content = Get-Content $resultsFile -Raw

  # Read counts from the <test-run> element's aggregate attributes.
  # 'result="Passed"' alone also matches rollup <test-suite>/<test-run>
  # elements and inflates the count (e.g. 1450 real passes reported as 1601).
  function Get-XmlIntAttr([string]$attrs, [string]$name) {
    $pattern = $name + '="(\d+)"'
    $m = [regex]::Match($attrs, $pattern)
    if ($m.Success) { return [int]$m.Groups[1].Value } else { return $null }
  }

  $total = $null
  $passed = $null
  $failed = $null
  $inconclusive = $null
  $skipped = $null

  $testRunMatch = [regex]::Match($content, '<test-run\b[^>]*>')
  if ($testRunMatch.Success) {
    $attrs = $testRunMatch.Value
    $total = Get-XmlIntAttr $attrs "total"
    $passed = Get-XmlIntAttr $attrs "passed"
    $failed = Get-XmlIntAttr $attrs "failed"
    $inconclusive = Get-XmlIntAttr $attrs "inconclusive"
    $skipped = Get-XmlIntAttr $attrs "skipped"
  }

  # Fall back to counting individual <test-case> elements if <test-run>
  # attributes could not be parsed (e.g. format change).
  if ($null -eq $passed) {
    Write-Host "::warning::Could not read <test-run> attributes, falling back to <test-case> count"
    $passed = ([regex]::Matches($content, '<test-case\b[^>]*result="Passed"')).Count
    $failed = ([regex]::Matches($content, '<test-case\b[^>]*result="Failed"')).Count
    $total = $null
    $inconclusive = $null
    $skipped = $null
  }

  $hasFailedCase = [regex]::IsMatch($content, '<test-case\b[^>]*result="Failed"')
  $failedCount = if ($null -ne $failed) { $failed } else { 0 }

  if ($hasFailedCase -or ($failedCount -gt 0)) {
    Write-Host "::error::EditMode tests failed"
    exit 1
  } else {
    $totalStr = if ($null -ne $total) { $total } else { "N/A" }
    $inconclusiveStr = if ($null -ne $inconclusive) { $inconclusive } else { 0 }
    $skippedStr = if ($null -ne $skipped) { $skipped } else { 0 }
    Write-Host "EditMode tests passed (passed=$passed, failed=$failedCount, inconclusive=$inconclusiveStr, skipped=$skippedStr, total=$totalStr)"
  }
} else {
  Write-Host "::error::EditMode test results file not found at $resultsFile - Unity failed to run tests (suspect license/Hub/cache)"
  exit 1
}
