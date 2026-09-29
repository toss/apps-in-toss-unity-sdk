# Initialize attempt counter (first run only)
$attemptFile = "$env:TEMP\retry-attempt-$env:UNITY_VERSION_PATTERN"
if (-not (Test-Path $attemptFile)) {
  Set-Content -Path $attemptFile -Value "0"
}

$unityPath = "$env:UNITY_PATH"
$projectPath = "$env:GITHUB_WORKSPACE\Tests~\E2E\$env:PROJECT_DIR_PREFIX-$env:UNITY_VERSION_PATTERN"

Write-Host "Unity Version: $env:UNITY_VERSION_FULL"
Write-Host "Unity Path: $unityPath"
Write-Host "Project Path: $projectPath"

# Match runner.temp (=$env:RUNNER_TEMP) so the upload step path lines up.
$logFile = "$env:RUNNER_TEMP\unity-build-$env:UNITY_VERSION_PATTERN.log"
Write-Host "Starting Unity build..."
Write-Host "Log file: $logFile"

$unityProcess = Start-Process -FilePath $unityPath -ArgumentList @(
  "-quit", "-batchmode", "-nographics",
  "-projectPath", $projectPath,
  "-executeMethod", "$env:BUILD_METHOD",
  "-buildTarget", "WebGL",
  "-logFile", $logFile
) -PassThru -NoNewWindow

Write-Host "Waiting for Unity build to complete..."
$logReader = $null
while (-not $unityProcess.HasExited) {
  Start-Sleep -Seconds 5
  if (-not $logReader -and (Test-Path $logFile)) {
    $logReader = [System.IO.StreamReader]::new(
      [System.IO.FileStream]::new($logFile, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
    )
  }
  if ($logReader) {
    $chunk = $logReader.ReadToEnd()
    if ($chunk) { Write-Host $chunk -NoNewline }
  }
}

# Call WaitForExit() to guarantee ExitCode is set (it may not be set immediately after HasExited)
$unityProcess.WaitForExit()

# Read remaining log output
if ($logReader) {
  $chunk = $logReader.ReadToEnd()
  if ($chunk) { Write-Host $chunk -NoNewline }
  $logReader.Close()
}

$exitCode = $unityProcess.ExitCode
Write-Host "Unity exited with code: $exitCode"

# Guard against a null ExitCode (fall back to checking whether build output exists)
if ($null -eq $exitCode) {
  Write-Host "::warning::ExitCode is null, checking build output..."
  $distPath = "$env:GITHUB_WORKSPACE\Tests~\E2E\$env:PROJECT_DIR_PREFIX-$env:UNITY_VERSION_PATTERN\ait-build\dist"
  if (Test-Path $distPath) {
    Write-Host "Build output exists, treating as success"
    $exitCode = 0
  } else {
    Write-Host "::error::Build output missing and ExitCode null, treating as failure"
    $exitCode = 1
  }
}

if ($exitCode -ne 0) {
  # Detect C# compile errors first - deterministic failure, so retrying is pure waste.
  # The Unity log contains license-related text even on normal startup; without this
  # check, licenseError below would match first and a compile error would be
  # retried as a license error.
  if (Test-Path $logFile) {
    $logContent = Get-Content $logFile -Raw -ErrorAction SilentlyContinue
    if ($logContent -match "error CS[0-9]+") {
      Write-Host "::error::C# compile error detected (not a license issue), failing immediately"
      Select-String -Path $logFile -Pattern "error CS[0-9]+" |
        ForEach-Object { $_.Line } | Select-Object -Unique -First 10
      exit $exitCode
    }
  }

  # Detect build-tool errors first (avoid misclassifying as a license error)
  $buildToolError = $false
  if (Test-Path $logFile) {
    $logContent = Get-Content $logFile -Raw -ErrorAction SilentlyContinue
    if ($logContent -match "Unknown Syntax Error|FAIL_NPM_BUILD|FAIL_NPM_INSTALL|Command not found.*ait|npm ERR!") {
      $buildToolError = $true
    }
  }

  if ($buildToolError) {
    Write-Host "::error::Build tool error detected (not a license issue), failing immediately"
    exit $exitCode
  }

  # Detect license-related errors (the retry action retries automatically)
  # signature/handshake/IPC failures happen when a LicensingClient from a
  # different Unity version is still alive on the same self-hosted runner; retry can recover.
  $licenseError = $false
  if (Test-Path $logFile) {
    $logContent = Get-Content $logFile -Raw -ErrorAction SilentlyContinue
    if ($logContent -match "No valid Unity Editor license found|Unsupported protocol version|ResponseCode: 505|Access token is unavailable|Code 10 while verifying Licensing Client signature|LicensingClient has failed validation|Failed to handshake to channel|IPC channel to LicensingClient doesn't exist") {
      $licenseError = $true
    }
  }
  if ($exitCode -eq 199) {
    $licenseError = $true
  }

  if ($licenseError) {
    Write-Host "::warning::Unity license error detected, attempting recovery via Unity Hub..."

    # Run Unity Hub headless to trigger a license token refresh
    $hubPath = "C:\Program Files\Unity Hub\Unity Hub.exe"
    if (Test-Path $hubPath) {
      try {
        & $hubPath -- --headless editors --installed 2>&1 | Out-Null
        Write-Host "Unity Hub license refresh attempted"
      } catch {
        Write-Host "Unity Hub refresh failed: $_"
      }
      Start-Sleep -Seconds 3

      # Clean up existing Licensing Client processes
      Get-Process -Name "Unity.Licensing.Client" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
      Start-Sleep -Seconds 2
      Write-Host "Licensing Client processes cleared for fresh start"
    }

    Write-Host "::warning::Triggering retry after license recovery..."
    exit 42
  }

  Write-Host "::error::Unity build failed with exit code: $exitCode"
  exit $exitCode
}

# Clean up the attempt counter on success
Remove-Item -Path $attemptFile -ErrorAction SilentlyContinue
Write-Host "Unity build completed successfully (exit code: $exitCode)"
