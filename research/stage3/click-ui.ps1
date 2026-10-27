param(
    [Parameter(Mandatory = $true)][int]$WindowId,
    [Parameter(Mandatory = $true)][string]$Name
)

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32Click {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
}
"@

$hwnd = [IntPtr]$WindowId
[Win32Click]::ShowWindow($hwnd, 9) | Out-Null
[Win32Click]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 500

$root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
if ($null -eq $root) { throw "No automation root for HWND $WindowId" }

$nameCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty,
    $Name
)
$element = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $nameCondition)
if ($null -eq $element) {
    $all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    $names = @()
    foreach ($item in $all) {
        try { if ($item.Current.Name) { $names += $item.Current.Name } } catch {}
    }
    throw "Element '$Name' not found. Available names: $($names -join ' | ')"
}

$current = $element.Current
$rect = $current.BoundingRectangle
$result = [pscustomobject]@{
    Name = $current.Name
    ControlType = $current.ControlType.ProgrammaticName
    ClassName = $current.ClassName
    AutomationId = $current.AutomationId
    IsEnabled = $current.IsEnabled
    IsOffscreen = $current.IsOffscreen
    Left = [int]$rect.Left
    Top = [int]$rect.Top
    Width = [int]$rect.Width
    Height = [int]$rect.Height
    Method = $null
}

$invoked = $false
try {
    $pattern = $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    if ($null -ne $pattern) {
        $pattern.Invoke()
        $invoked = $true
        $result.Method = 'InvokePattern'
    }
} catch {}

if (-not $invoked) {
    $x = [int]($rect.Left + ($rect.Width / 2))
    $y = [int]($rect.Top + ($rect.Height / 2))
    [Win32Click]::SetCursorPos($x, $y) | Out-Null
    Start-Sleep -Milliseconds 100
    [Win32Click]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [Win32Click]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    $invoked = $true
    $result.Method = 'MouseClick'
}

$result | Format-List
