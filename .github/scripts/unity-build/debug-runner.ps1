Write-Host "=== Runner Diagnostics ==="
Write-Host "Hostname: $env:COMPUTERNAME"
Write-Host "Date: $(Get-Date)"
Write-Host ""

Write-Host "=== CPU Info ==="
Get-CimInstance Win32_Processor | Select-Object Name, NumberOfCores, NumberOfLogicalProcessors | Format-List
Write-Host ""

Write-Host "=== Memory Usage ==="
$os = Get-CimInstance Win32_OperatingSystem
$totalGB = [math]::Round($os.TotalVisibleMemorySize / 1MB, 1)
$freeGB = [math]::Round($os.FreePhysicalMemory / 1MB, 1)
$usedGB = [math]::Round(($os.TotalVisibleMemorySize - $os.FreePhysicalMemory) / 1MB, 1)
Write-Host "Total: ${totalGB} GB / Used: ${usedGB} GB / Free: ${freeGB} GB"
Write-Host ""

Write-Host "=== Disk Usage ==="
Get-PSDrive -PSProvider FileSystem | Format-Table Name, @{L='Used(GB)';E={[math]::Round($_.Used/1GB,1)}}, @{L='Free(GB)';E={[math]::Round($_.Free/1GB,1)}}
Write-Host ""

Write-Host "=== Top 20 Processes by CPU ==="
Get-Process | Sort-Object CPU -Descending | Select-Object -First 20 Name, Id, CPU, @{L='Mem(MB)';E={[math]::Round($_.WorkingSet64/1MB,1)}} | Format-Table
Write-Host ""

Write-Host "=== Top 20 Processes by Memory ==="
Get-Process | Sort-Object WorkingSet64 -Descending | Select-Object -First 20 Name, Id, CPU, @{L='Mem(MB)';E={[math]::Round($_.WorkingSet64/1MB,1)}} | Format-Table
Write-Host ""

Write-Host "=== Unity-related Processes ==="
$unityProcs = Get-Process | Where-Object { $_.Name -match "Unity|UnityHelper|UnityShaderCompiler|UnityPackageManager" }
if ($unityProcs) {
  $unityProcs | Format-Table Name, Id, CPU, @{L='Mem(MB)';E={[math]::Round($_.WorkingSet64/1MB,1)}}, Path
} else {
  Write-Host "No Unity processes found"
}
Write-Host ""

# Other jobs' Unity processes may be running on the runner host, so killing
# by process name alone could take down another job's editor. Only kill a
# process whose full command line references this job's own workspace
# (GITHUB_WORKSPACE and/or its runner directory).
#
# Unity.Licensing.Client may be in use by another job, so it is never killed
# regardless of workspace match - killing it can break a live job's license
# handshake (see fix-licensing-client.sh for the related issue).
Write-Host "=== Killing Unity-related Processes (scoped to this job's workspace) ==="
Write-Host "Always skipped regardless of match: Unity.Licensing.Client (never killed)"

$workspacePatterns = @()
if ($env:GITHUB_WORKSPACE) {
  $workspacePatterns += [regex]::Escape($env:GITHUB_WORKSPACE)
}
if ($env:RUNNER_WORKSPACE) {
  $m = [regex]::Match($env:RUNNER_WORKSPACE, '.*[\\/]actions-runner-\d+')
  if ($m.Success) {
    $workspacePatterns += [regex]::Escape($m.Value)
  }
}

$killed = 0
if ($workspacePatterns.Count -eq 0) {
  Write-Host "::warning::GITHUB_WORKSPACE/RUNNER_WORKSPACE not set - refusing to kill by name to avoid hitting other concurrent jobs on this shared host"
} else {
  $workspaceRegex = ($workspacePatterns -join '|')
  Write-Host "Workspace match pattern: $workspaceRegex"

  $candidates = Get-CimInstance Win32_Process | Where-Object { $_.Name -match "Unity|UnityHelper|UnityShaderCompiler|UnityPackageManager" }
  foreach ($proc in $candidates) {
    $isLicenseClient = ($proc.Name -match "Licensing") -or ($proc.CommandLine -and $proc.CommandLine -match "Unity\.Licensing\.Client")
    if ($isLicenseClient) {
      Write-Host "Skipping $($proc.Name) (PID: $($proc.ProcessId)) - Unity.Licensing.Client is never killed (never killed)"
      continue
    }

    if ($proc.CommandLine -and ($proc.CommandLine -match $workspaceRegex)) {
      Write-Host "Killing $($proc.Name) (PID: $($proc.ProcessId)) - matches this job's workspace"
      Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue
      $killed++
    } else {
      Write-Host "Skipping $($proc.Name) (PID: $($proc.ProcessId)) - does not match this job's workspace (likely another concurrent job on the shared host)"
    }
  }
}

if ($killed -eq 0) {
  Write-Host "No Unity processes in this job's workspace were killed"
} else {
  Write-Host "Killed $killed process(es)"
  Start-Sleep -Seconds 2
}
Write-Host ""

Write-Host "=== Post-cleanup Process Check ==="
$remaining = Get-Process | Where-Object { $_.Name -match "Unity" }
if ($remaining) {
  $remaining | Format-Table Name, Id
} else {
  Write-Host "No Unity processes remaining"
}
Write-Host ""

Write-Host "=== Memory After Cleanup ==="
$os = Get-CimInstance Win32_OperatingSystem
$freeGB = [math]::Round($os.FreePhysicalMemory / 1MB, 1)
$usedGB = [math]::Round(($os.TotalVisibleMemorySize - $os.FreePhysicalMemory) / 1MB, 1)
Write-Host "Used: ${usedGB} GB / Free: ${freeGB} GB"
