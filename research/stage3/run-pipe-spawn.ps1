param(
    [string]$Exe = 'C:\Program Files (x86)\Softouch\EasyWorship 7\EasyWorship.exe',
    [string]$Rea = 'C:\Users\iKay\.local\node24\node-v24.22.1-win-x64\rea.cmd',
    [string]$Probe = 'C:\Users\iKay\source\repos\EW Clone\research\stage3\main-pipe-probe.js',
    [string]$Log = 'C:\Users\iKay\source\repos\EW Clone\research\stage3\main-pipe-spawn.log',
    [string]$ClickScript = 'C:\Users\iKay\source\repos\EW Clone\research\stage3\click-ui.ps1'
)

Remove-Item -LiteralPath $Log -Force -ErrorAction SilentlyContinue

$job = Start-Job -ArgumentList $Rea, $Exe, $Probe, $Log -ScriptBlock {
    param($reaPath, $exePath, $probePath, $logPath)
    & $reaPath instrument-with-frida spawn $exePath --script-file $probePath --duration-ms 30000 --format json *> $logPath
}

$proc = $null
for ($i = 0; $i -lt 150; $i++) {
    Start-Sleep -Milliseconds 100
    $proc = Get-Process -Name EasyWorship -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($proc) { break }
}
if (-not $proc) {
    "APP_NOT_FOUND"
    $job | Wait-Job -Timeout 10 | Out-Null
    Get-Content -LiteralPath $Log -ErrorAction SilentlyContinue
    $job | Remove-Job -Force -ErrorAction SilentlyContinue
    return
}
"APP_PID=$($proc.Id)"

$hwnd = [IntPtr]::Zero
for ($i = 0; $i -lt 150; $i++) {
    $p = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
    if ($p -and $p.MainWindowHandle -ne 0) {
        $hwnd = [IntPtr]$p.MainWindowHandle
        break
    }
    Start-Sleep -Milliseconds 100
}
"HWND=$hwnd"

$pipeSeen = $false
for ($i = 0; $i -lt 150; $i++) {
    if (Test-Path -LiteralPath $Log) {
        if (Select-String -LiteralPath $Log -Pattern 'PIPE_HANDLE_SET' -Quiet -ErrorAction SilentlyContinue) {
            $pipeSeen = $true
            break
        }
    }
    Start-Sleep -Milliseconds 100
}
"PIPE_SEEN=$pipeSeen"

if ($hwnd -ne [IntPtr]::Zero) {
    foreach ($name in @('Go Live', 'Logo', 'Black', 'Clear')) {
        try {
            "CLICK=$name"
            & $ClickScript -WindowId $hwnd.ToInt32() -Name $name 2>&1 | Out-String | Write-Output
            Start-Sleep -Milliseconds 1500
        } catch {
            "CLICK_ERROR=$name $_"
        }
    }
}

$job | Wait-Job -Timeout 40 | Out-Null
"JOB_STATE=$($job.State)"
$job | Receive-Job -ErrorAction SilentlyContinue | Out-Null
$job | Remove-Job -Force -ErrorAction SilentlyContinue

$p = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
if ($p) {
    $p | Select-Object Id, ProcessName, MainWindowTitle, Responding | Format-List
}

"LOG_LINES=$((Get-Content -LiteralPath $Log -ErrorAction SilentlyContinue).Count)"
Get-Content -LiteralPath $Log -ErrorAction SilentlyContinue |
    Select-String -Pattern 'PIPE_|PROCESS_CREATE|MAIN_PIPE|HOOK|PIPE_FILETYPE' |
    ForEach-Object { $_.Line }
