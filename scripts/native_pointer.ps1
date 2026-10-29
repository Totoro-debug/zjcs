param([int]$GameProcessId, [string]$Channel)
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class GamePointer {
    public delegate bool EnumProc(IntPtr handle, IntPtr param);
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr param);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr handle, out uint process);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr handle, out Rect rect);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr handle, out Rect rect);
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr handle, uint flags);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr handle, ref Point point);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint from, uint to, bool attach);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr handle, int state);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr handle);
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
    public static IntPtr Find(int process) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((handle, param) => {
            uint owner; Rect rect;
            GetWindowThreadProcessId(handle, out owner);
            if (owner == process && IsWindowVisible(handle) && GetClientRect(handle, out rect) && rect.Right > 100 && rect.Bottom > 100) {
                found = handle; return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }
    public static void Focus(IntPtr handle) {
        uint ignored;
        uint foreground = GetWindowThreadProcessId(GetForegroundWindow(), out ignored);
        uint current = GetCurrentThreadId();
        bool attached = foreground != 0 && foreground != current && AttachThreadInput(current, foreground, true);
        try { ShowWindow(handle, 9); BringWindowToTop(handle); SetForegroundWindow(handle); }
        finally { if (attached) AttachThreadInput(current, foreground, false); }
    }
}
'@
[GamePointer]::SetThreadDpiAwarenessContext([IntPtr]::new(-4)) | Out-Null
$cursor = [GamePointer+Point]::new()
[GamePointer]::GetCursorPos([ref]$cursor) | Out-Null
$game = Get-Process -Id $GameProcessId
$sequence = 0
$utf8 = [Text.UTF8Encoding]::new($false)
function Reply([int]$Id, [bool]$Ok, [string]$Message = '') {
    [IO.File]::WriteAllText((Join-Path $Channel ('reply-' + $Id + '.json')), (@{seq=$Id;ok=$Ok;message=$Message} | ConvertTo-Json -Compress), $utf8)
}
try {
    Reply 0 $true
    while (-not $game.HasExited) {
        $requestPath = Join-Path $Channel ('request-' + ($sequence + 1) + '.json')
        if (-not [IO.File]::Exists($requestPath)) { Start-Sleep -Milliseconds 10; continue }
        try { $request = [IO.File]::ReadAllText($requestPath) | ConvertFrom-Json } catch { Start-Sleep -Milliseconds 10; continue }
        if ([int]$request.seq -le $sequence) { Start-Sleep -Milliseconds 10; continue }
        $sequence = [int]$request.seq
        if ($request.kind -eq 'stop') { Reply $sequence $true; break }
        $handle = [GamePointer]::Find($GameProcessId)
        if ($handle -eq [IntPtr]::Zero) { Reply $sequence $false 'Game window not found'; continue }
        if ([GamePointer]::GetForegroundWindow() -ne $handle) {
            [GamePointer]::Focus($handle)
            Start-Sleep -Milliseconds 40
            if ([GamePointer]::GetForegroundWindow() -ne $handle) {
                $windowRect = [GamePointer+Rect]::new()
                [GamePointer]::GetWindowRect($handle, [ref]$windowRect) | Out-Null
                $title = [GamePointer+Point]::new(); $title.X = $windowRect.Left + 80; $title.Y = $windowRect.Top + 12
                if ([GamePointer]::GetAncestor([GamePointer]::WindowFromPoint($title), 2) -eq $handle) {
                    [GamePointer]::SetCursorPos($title.X, $title.Y) | Out-Null
                    [GamePointer]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
                    [GamePointer]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
                    Start-Sleep -Milliseconds 40
                }
            }
        }
        if ([GamePointer]::GetForegroundWindow() -ne $handle) { Reply $sequence $false ('Game window is not foreground: expected ' + $handle + ', actual ' + [GamePointer]::GetForegroundWindow()); continue }
        if ($request.kind -eq 'focus') { Reply $sequence $true; continue }
        $rect = [GamePointer+Rect]::new()
        [GamePointer]::GetClientRect($handle, [ref]$rect) | Out-Null
        $point = [GamePointer+Point]::new(); $point.X = [int]$request.x; $point.Y = [int]$request.y
        if ($point.X -lt 0 -or $point.Y -lt 0 -or $point.X -ge $rect.Right -or $point.Y -ge $rect.Bottom) { Reply $sequence $false 'Pointer is outside the game client'; continue }
        [GamePointer]::ClientToScreen($handle, [ref]$point) | Out-Null
        [GamePointer]::SetCursorPos($point.X, $point.Y) | Out-Null
        if ($request.kind -eq 'button') {
            [GamePointer]::mouse_event($(if ($request.down) { 2 } else { 4 }), 0, 0, 0, [UIntPtr]::Zero)
        } elseif ($request.kind -eq 'wheel') {
            [GamePointer]::mouse_event(2048, 0, 0, $(if ($request.down) { [uint32]4294967176 } else { [uint32]120 }), [UIntPtr]::Zero)
        }
        Reply $sequence $true
    }
} finally {
    [GamePointer]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    [GamePointer]::SetCursorPos($cursor.X, $cursor.Y) | Out-Null
}
