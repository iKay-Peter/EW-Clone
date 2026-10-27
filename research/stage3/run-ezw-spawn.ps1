param(
    [string]$Exe = 'C:\Program Files (x86)\Softouch\EasyWorship 7\EasyWorship.exe',
    [string]$Rea = 'C:\Users\iKay\.local\node24\node-v24.22.1-win-x64\rea.cmd',
    [string]$Probe = 'C:\Users\iKay\source\repos\EW Clone\research\stage3\ezw-pipe-probe.js',
    [string]$Log = 'C:\Users\iKay\source\repos\EW Clone\research\stage3\ezw-spawn.log',
    [string]$ClickScript = 'C:\Users\iKay\source\repos\EW Clone\research\stage3\click-ui.ps1'
)

Remove-Item -LiteralPath $Log -Force -ErrorAction SilentlyContinue

$job = Start-Job -ArgumentList $Rea, $Exe, $Probe, $Log -ScriptBlock {
    param($reaPath, $exePath, $probePath, $logPath)
    & $reaPath instrument-with-frida spawn $exePath --script-file $probePath --duration-ms 45000 --format json *> $logPath
}

$ready = $false
for ($i = 0; $i -lt 200; $i++) {
    Start-Sleep -Milliseconds 100
    if (Test-Path -LiteralPath $Log) {
        if (Select-String -LiteralPath $Log -Pattern 'EZW_PIPE_PROBE_READY' -Quiet -ErrorAction SilentlyContinue) {
            $ready = $true
            break
        }
    }
}
"PROBE_READY=$ready"

$proc = $null
for ($i = 0; $i -lt 200; $i++) {
    $proc = Get-Process -Name EasyWorship -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($proc) { break }
    Start-Sleep -Milliseconds 100
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
for ($i = 0; $i -lt 200; $i++) {
    $p = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
    if ($p -and $p.MainWindowHandle -ne 0) {
        $hwnd = [IntPtr]$p.MainWindowHandle
        break
    }
    Start-Sleep -Milliseconds 100
}
"HWND=$hwnd"

for ($i = 0; $i -lt 200; $i++) {
    if (Select-String -LiteralPath $Log -Pattern 'EZW_HANDLE' -Quiet -ErrorAction SilentlyContinue) { break }
    Start-Sleep -Milliseconds 100
}
"EZW_HANDLE_SEEN=$(Select-String -LiteralPath $Log -Pattern 'EZW_HANDLE' -Quiet -ErrorAction SilentlyContinue)"
"WAIT_UI"
Start-Sleep -Seconds 10

if ($hwnd -ne [IntPtr]::Zero) {
    foreach ($name in @('Go Live', 'Logo', 'Black', 'Clear', 'Go Live')) {
        try {
            "CLICK=$name"
            & $ClickScript -WindowId $hwnd.ToInt32() -Name $name 2>&1 |
                Select-String -Pattern '^Name|^Method|not found|^IsEnabled' |
                ForEach-Object { $_.Line }
            Start-Sleep -Milliseconds 1800
        } catch {
            "CLICK_ERROR=$name $_"
        }
    }
}

$job | Wait-Job -Timeout 60 | Out-Null
"JOB_STATE=$($job.State)"
$job | Receive-Job -ErrorAction SilentlyContinue | Out-Null
$job | Remove-Job -Force -ErrorAction SilentlyContinue

$p = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
if ($p) {
    $p | Select-Object Id, ProcessName, MainWindowTitle, Responding | Format-List
}

"LOG_LINES=$((Get-Content -LiteralPath $Log -ErrorAction SilentlyContinue).Count)"
Get-Content -LiteralPath $Log -ErrorAction SilentlyContinue |
    Select-String -Pattern 'EZW_|PROBE_READY|HOOK_ERROR|HOOK_MISS|TypeError' |
    ForEach-Object { $_.Line }
