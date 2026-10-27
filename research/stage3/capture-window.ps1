param(
    [Parameter(Mandatory = $true)][int]$ProcessId,
    [Parameter(Mandatory = $true)][int]$WindowId,
    [Parameter(Mandatory = $true)][string]$PngPath,
    [Parameter(Mandatory = $true)][string]$TreePath
)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32Capture {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
}
"@

$hwnd = [IntPtr]$WindowId
[Win32Capture]::ShowWindow($hwnd, 9) | Out-Null
[Win32Capture]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 700

$rect = New-Object Win32Capture+RECT
if (-not [Win32Capture]::GetWindowRect($hwnd, [ref]$rect)) {
    throw "GetWindowRect failed for HWND $WindowId"
}
$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
if ($width -le 0 -or $height -le 0) {
    throw "Invalid window rect: $($rect.Left),$($rect.Top),$($rect.Right),$($rect.Bottom)"
}

$bitmap = New-Object System.Drawing.Bitmap $width, $height
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
$bitmap.Save($PngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()

$root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
$condition = [System.Windows.Automation.Condition]::TrueCondition
$elements = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
$rows = New-Object System.Collections.Generic.List[object]
$index = 0
foreach ($element in $elements) {
    if ($index -ge 700) { break }
    try {
        $current = $element.Current
        $rows.Add([pscustomobject]@{
            Index = $index
            Name = $current.Name
            ControlType = $current.ControlType.ProgrammaticName
            AutomationId = $current.AutomationId
            ClassName = $current.ClassName
            IsEnabled = $current.IsEnabled
            IsOffscreen = $current.IsOffscreen
            Left = [int]$current.BoundingRectangle.Left
            Top = [int]$current.BoundingRectangle.Top
            Width = [int]$current.BoundingRectangle.Width
            Height = [int]$current.BoundingRectangle.Height
        })
    } catch {}
    $index++
}
$rows | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $TreePath -Encoding UTF8

[pscustomobject]@{
    ProcessId = $ProcessId
    WindowId = $WindowId
    Title = $root.Current.Name
    Left = $rect.Left
    Top = $rect.Top
    Width = $width
    Height = $height
    Elements = $elements.Count
    PngBytes = (Get-Item -LiteralPath $PngPath).Length
    TreeBytes = (Get-Item -LiteralPath $TreePath).Length
} | Format-List
