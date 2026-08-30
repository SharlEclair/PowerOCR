Write-Host "--- Launching PowerToys.PowerOCR.exe ---" -ForegroundColor Cyan
$proc = Start-Process -FilePath ".\x64\Debug\PowerToys.PowerOCR.exe" -PassThru
Write-Host "Started process with PID: $($proc.Id)" -ForegroundColor Green

Start-Sleep -Seconds 2

Write-Host "Process HasExited: $($proc.HasExited)" -ForegroundColor Yellow
if ($proc.HasExited) {
    Write-Host "Exit Code: $($proc.ExitCode)" -ForegroundColor Red
}

$logPath = Join-Path $env:TEMP "PowerToys_TextExtractor_Debug.log"
if (Test-Path $logPath) {
    Write-Host "`n--- Debug Log Output ---" -ForegroundColor Cyan
    Get-Content $logPath -Tail 25
}
