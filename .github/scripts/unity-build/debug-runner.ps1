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

Write-Host "=== Killing Unity-related Processes ==="
$killed = 0
$unityProcs = Get-Process | Where-Object { $_.Name -match "Unity|UnityHelper|UnityShaderCompiler|UnityPackageManager" }
foreach ($proc in $unityProcs) {
  Write-Host "Killing $($proc.Name) (PID: $($proc.Id))..."
  Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
  $killed++
}
if ($killed -eq 0) {
  Write-Host "No Unity processes to kill"
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
