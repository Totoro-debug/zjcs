param([int]$SeasonProcessId, [int]$ClientX, [int]$ClientY)
$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class SeasonNativeMouse {
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr window, ref Point point);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr window, uint message, UIntPtr key, IntPtr point);
}
'@
$seasonWindow = (Get-Process -Id $SeasonProcessId).MainWindowHandle
if ($seasonWindow -eq [IntPtr]::Zero) { throw 'The season test has no native window.' }
$seasonPoint = New-Object SeasonNativeMouse+Point
$seasonPoint.X = $ClientX
$seasonPoint.Y = $ClientY
$seasonRect = New-Object SeasonNativeMouse+Rect
if (-not [SeasonNativeMouse]::GetClientRect($seasonWindow, [ref]$seasonRect)) { throw 'GetClientRect failed.' }
if ($seasonPoint.X -lt 0 -or $seasonPoint.Y -lt 0 -or $seasonPoint.X -ge $seasonRect.Right -or $seasonPoint.Y -ge $seasonRect.Bottom) {
    throw "The requested season control lies outside its native client window: client=$ClientX,$ClientY size=$($seasonRect.Right),$($seasonRect.Bottom)."
}
[void][SeasonNativeMouse]::SetForegroundWindow($seasonWindow)
if (-not [SeasonNativeMouse]::ClientToScreen($seasonWindow, [ref]$seasonPoint)) { throw 'ClientToScreen failed.' }
$seasonPosition = [IntPtr](($ClientY -shl 16) -bor ($ClientX -band 65535))
foreach ($seasonMessage in @(512, 513, 514)) {
    $seasonKeys = [UIntPtr]::Zero
    if ($seasonMessage -eq 513) { $seasonKeys = [UIntPtr]::new([uint32]1) }
    if (-not [SeasonNativeMouse]::PostMessage($seasonWindow, $seasonMessage, $seasonKeys, $seasonPosition)) { throw 'PostMessage failed.' }
}
