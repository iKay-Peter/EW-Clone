param(
    [string]$Exe = 'C:\Program Files (x86)\Softouch\EasyWorship 7\EasyWorship.exe',
    [string]$WorkingDirectory = 'C:\Program Files (x86)\Softouch\EasyWorship 7',
    [string]$Rea = 'C:\Users\iKay\.local\node24\node-v24.22.1-win-x64\rea.cmd',
    [string]$Probe = 'C:\Users\iKay\source\repos\EW Clone\research\stage3\main-pipe-probe.js',
    [string]$Log = 'C:\Users\iKay\source\repos\EW Clone\research\stage3\main-pipe-probe.log',
    [string]$ClickScript = 'C:\Users\iKay\source\repos\EW Clone\research\stage3\click-ui.ps1'
)

Remove-Item -LiteralPath $Log -Force -ErrorAction SilentlyContinue

$app = Start-Process -FilePath $Exe -WorkingDirectory $WorkingDirectory -PassThru
"APP_PID=$($app.Id)"
Start-Sleep -Milliseconds 200

$job = Start-Job -ArgumentList $Rea, $app.Id, $Probe, $Log -ScriptBlock {
    param($reaPath, $targetPid, $probePath, $logPath)
    & $reaPath instrument-with-frida attach $targetPid --script-file $probePath --duration-ms 25000 --format json *> $logPath
}

$pipeSeen = $false
for ($i = 0; $i -lt 100; $i++) {
    Start-Sleep -Milliseconds 100
    if (Test-Path -LiteralPath $Log) {
        if (Select-String -LiteralPath $Log -Pattern 'PIPE_HANDLE_SET' -Quiet -ErrorAction SilentlyContinue) {
            $pipeSeen = $true
            break
        }
    }
}
"PIPE_SEEN=$pipeSeen"

$hwnd = [IntPtr]::Zero
for ($i = 0; $i -lt 100; $i++) {
    $p = Get-Process -Id $app.Id -ErrorAction SilentlyContinue
    if ($p -and $p.MainWindowHandle -ne 0) {
        $hwnd = [IntPtr]$p.MainWindowHandle
        break
    }
    Start-Sleep -Milliseconds 100
}
"HWND=$hwnd"

if ($hwnd -ne [IntPtr]::Zero) {
    foreach ($name in @('Go Live', 'Logo', 'Black', 'Clear')) {
        try {
            "CLICK=$name"
            & $ClickScript -WindowId $hwnd.ToInt32() -Name $name 2>&1 | Out-String | Write-Output
            Start-Sleep -Milliseconds 1200
        } catch {
            "CLICK_ERROR=$name $_"
        }
    }
}

$job | Wait-Job -Timeout 30 | Out-Null
"JOB_STATE=$($job.State)"
$job | Receive-Job -ErrorAction SilentlyContinue | Out-Null
$job | Remove-Job -Force -ErrorAction SilentlyContinue

$p = Get-Process -Id $app.Id -ErrorAction SilentlyContinue
if ($p) {
    $p | Select-Object Id, ProcessName, MainWindowTitle, Responding | Format-List
}

"LOG_LINES=$((Get-Content -LiteralPath $Log -ErrorAction SilentlyContinue).Count)"
Get-Content -LiteralPath $Log -ErrorAction SilentlyContinue |
    Select-String -Pattern 'PIPE_|PROCESS_CREATE|MAIN_PIPE|HOOK|PIPE_FILETYPE' |
    ForEach-Object { $_.Line }
