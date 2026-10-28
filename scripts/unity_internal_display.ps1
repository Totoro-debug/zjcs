param(
    [int]$WatchProcessId = 0,
    [string]$WatchProjectRoot = '',
    [string]$StatusPath = '',
    [int]$TimeoutSeconds = 0,
    [switch]$ShowEditor,
    [int]$ClientWidth = 0,
    [int]$ClientHeight = 0
)

# Dot-source this file to launch a Unity editor/validation process on the laptop panel.
if (-not ('Zjcs.UnityDisplay' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Zjcs {
    public static class UnityDisplay {
        [StructLayout(LayoutKind.Sequential)]
        public struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct MonitorInfo {
            public int Size;
            public Rect Bounds, WorkArea;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct DisplayDevice {
            public int Size;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Key;
        }
        public sealed class Display {
            public string Device, HardwareId;
            public bool Primary;
            public Rect Bounds, WorkArea;
        }
        public sealed class Window {
            public IntPtr Handle;
            public string ClassName, Title;
            public bool IsEditorMain;
            public bool IsPlayerMain;
            public bool Visible;
        }
        delegate bool MonitorCallback(IntPtr monitor, IntPtr dc, ref Rect bounds, IntPtr data);
        delegate bool WindowCallback(IntPtr window, IntPtr data);
        [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorCallback callback, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool EnumDisplayDevices(string device, uint index, ref DisplayDevice info, uint flags);
        [DllImport("user32.dll")] static extern bool EnumWindows(WindowCallback callback, IntPtr data);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int size);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr window, StringBuilder text, int size);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window, uint command);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr window);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll", SetLastError = true)] static extern bool GetWindowRect(IntPtr window, out Rect bounds);
        [DllImport("user32.dll", SetLastError = true)] static extern bool GetClientRect(IntPtr window, out Rect bounds);
        [DllImport("user32.dll", SetLastError = true)] static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

        public static Display[] GetDisplays() {
            var previous = SetThreadDpiAwarenessContext(new IntPtr(-4));
            try {
                var result = new List<Display>();
                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate(IntPtr monitor, IntPtr dc, ref Rect rect, IntPtr data) {
                    var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
                    if (!GetMonitorInfo(monitor, ref info)) return true;
                    var device = new DisplayDevice { Size = Marshal.SizeOf(typeof(DisplayDevice)) };
                    if (EnumDisplayDevices(info.Device, 0, ref device, 0)) {
                        var parts = device.Id.Split('\\');
                        result.Add(new Display { Device = info.Device, HardwareId = parts.Length > 1 ? parts[1] : device.Id, Primary = (info.Flags & 1) != 0,
                            Bounds = info.Bounds, WorkArea = info.WorkArea });
                    }
                    return true;
                }, IntPtr.Zero);
                return result.ToArray();
            } finally { if (previous != IntPtr.Zero) SetThreadDpiAwarenessContext(previous); }
        }

        public static Window[] GetWindows(int processId) {
            var result = new List<Window>();
            EnumWindows(delegate(IntPtr window, IntPtr data) {
                uint owner;
                GetWindowThreadProcessId(window, out owner);
                if (owner != processId) return true;
                var name = new StringBuilder(256);
                GetClassName(window, name, name.Capacity);
                if (name.ToString().StartsWith("Unity", StringComparison.Ordinal)) {
                    var title = new StringBuilder(1024);
                    GetWindowText(window, title, title.Capacity);
                    result.Add(new Window { Handle = window, ClassName = name.ToString(), Title = title.ToString(),
                        IsEditorMain = name.ToString() == "UnityContainerWndClass" && GetWindow(window, 4) == IntPtr.Zero &&
                            title.ToString().IndexOf("Unity", StringComparison.OrdinalIgnoreCase) >= 0,
                        IsPlayerMain = name.ToString() == "UnityWndClass" && GetWindow(window, 4) == IntPtr.Zero,
                        Visible = IsWindowVisible(window) });
                }
                return true;
            }, IntPtr.Zero);
            return result.ToArray();
        }

        public static Rect GetBounds(IntPtr window) {
            var previous = SetThreadDpiAwarenessContext(new IntPtr(-4));
            try {
                Rect bounds;
                if (!GetWindowRect(window, out bounds)) throw new Win32Exception(Marshal.GetLastWin32Error());
                return bounds;
            } finally { if (previous != IntPtr.Zero) SetThreadDpiAwarenessContext(previous); }
        }

        public static bool PlacePlayer(IntPtr window, Display display, int clientWidth, int clientHeight) {
            var previous = SetThreadDpiAwarenessContext(new IntPtr(-4));
            try {
                Rect outer, client;
                if (!GetWindowRect(window, out outer) || !GetClientRect(window, out client)) return false;
                int frameWidth = Math.Max(0, outer.Right - outer.Left - (client.Right - client.Left));
                int frameHeight = Math.Max(0, outer.Bottom - outer.Top - (client.Bottom - client.Top));
                var work = display.WorkArea;
                double scale = Math.Min(1.0, Math.Min((work.Right - work.Left - frameWidth - 8.0) / clientWidth,
                    (work.Bottom - work.Top - frameHeight - 8.0) / clientHeight));
                int width = Math.Max(1, (int)Math.Round(clientWidth * scale)) + frameWidth;
                int height = Math.Max(1, (int)Math.Round(clientHeight * scale)) + frameHeight;
                int x = work.Left + (work.Right - work.Left - width) / 2;
                int y = work.Top + (work.Bottom - work.Top - height) / 2;
                // A hidden Player initially exposes a placeholder Win32 rectangle.
                // Apply its requested client dimensions before showing it, or a
                // placeholder WM_SIZE can replace Unity's portrait resolution.
                if (!SetWindowPos(window, IntPtr.Zero, x, y, width, height, 0x14)) return false;
                if (!IsWindowVisible(window)) ShowWindow(window, 4);
                return true;
            } finally { if (previous != IntPtr.Zero) SetThreadDpiAwarenessContext(previous); }
        }

        public static bool Place(IntPtr window, Display display, bool showEditor) {
            var previous = SetThreadDpiAwarenessContext(new IntPtr(-4));
            try {
                Rect rect;
                if (!GetWindowRect(window, out rect) || rect.Right <= rect.Left || rect.Bottom <= rect.Top) return false;
                var work = display.WorkArea;
                bool inside = rect.Left >= work.Left && rect.Top >= work.Top && rect.Right <= work.Right && rect.Bottom <= work.Bottom;
                if (!inside || IsIconic(window) || IsZoomed(window)) {
                    // Restore before measuring; minimized windows otherwise report icon-sized rectangles.
                    if (IsIconic(window) || IsZoomed(window)) {
                        ShowWindow(window, 4); // SW_SHOWNOACTIVATE
                        if (!GetWindowRect(window, out rect)) return false;
                    }
                    int width = Math.Min(rect.Right - rect.Left, work.Right - work.Left - 32);
                    int height = Math.Min(rect.Bottom - rect.Top, work.Bottom - work.Top - 32);
                    if (width <= 0 || height <= 0) return false;
                    int x = work.Left + (work.Right - work.Left - width) / 2;
                    int y = work.Top + (work.Bottom - work.Top - height) / 2;
                    // Never activate or change z-order.
                    if (!SetWindowPos(window, IntPtr.Zero, x, y, width, height, 0x14)) return false; // NOZORDER | NOACTIVATE
                }
                if (showEditor && !IsWindowVisible(window)) ShowWindow(window, 4);
                return true;
            } finally { if (previous != IntPtr.Zero) SetThreadDpiAwarenessContext(previous); }
        }
    }
}
'@
}

function Get-UnityInternalDisplay {
    $internalIds = @(Get-CimInstance -Namespace root/wmi -ClassName WmiMonitorConnectionParams |
        Where-Object { $_.Active -and [uint32]$_.VideoOutputTechnology -in @([uint32]2147483648, 11, 13) } |
        ForEach-Object { ($_.InstanceName -split '\\')[1] })
    $display = @([Zjcs.UnityDisplay]::GetDisplays() | Where-Object { $_.HardwareId -in $internalIds })
    if ($display.Count -ne 1) {
        throw 'Cannot identify exactly one active internal display. Keep the laptop panel enabled; do not fall back to the primary monitor.'
    }
    return $display[0]
}

function Start-UnityOnInternalDisplay {
    param([Parameter(Mandatory)][string]$Editor, [Parameter(Mandatory)][string[]]$ArgumentList)
    $display = Get-UnityInternalDisplay
    # Launch the native executable directly. Preserve the existing argument
    # quoting contract and keep the window hidden until the display watcher places it.
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Editor
    $startInfo.Arguments = $ArgumentList -join ' '
    $startInfo.UseShellExecute = $false
    $startInfo.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
    $process = [System.Diagnostics.Process]::Start($startInfo)
    $helperPath = $MyInvocation.MyCommand.ScriptBlock.File
    $watchArguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$helperPath`"", '-WatchProcessId', $process.Id)
    if ($ArgumentList -notcontains '-batchmode') { $watchArguments += '-ShowEditor' }
    $widthIndex = [Array]::IndexOf($ArgumentList, '-screen-width')
    $heightIndex = [Array]::IndexOf($ArgumentList, '-screen-height')
    if ($widthIndex -ge 0 -and $heightIndex -ge 0 -and $widthIndex + 1 -lt $ArgumentList.Length -and $heightIndex + 1 -lt $ArgumentList.Length) {
        $watchArguments += @('-ClientWidth', $ArgumentList[$widthIndex + 1], '-ClientHeight', $ArgumentList[$heightIndex + 1])
    }
    $engine = (Get-Process -Id $PID).Path
    Start-Process -FilePath $engine -ArgumentList $watchArguments -WindowStyle Hidden | Out-Null
    Write-Host "Unity process $($process.Id) targets internal display $($display.Device) ($($display.HardwareId))."
    return $process
}

if ($WatchProjectRoot) {
    # Machine-local guard: also covers direct Unity.exe launches from existing chats/worktrees.
    $ErrorActionPreference = 'Stop'
    $guardCreated = $false
    $guardMutex = [System.Threading.Mutex]::new($true, 'Local\Zjcs.UnityInternalDisplay', [ref]$guardCreated)
    if (-not $guardCreated) { $guardMutex.Dispose(); return }
    try {
        $normalizedRoot = [System.IO.Path]::GetFullPath($WatchProjectRoot).Replace('\', '/').TrimEnd('/')
        $projectPattern = '(?i)-projectPath\s+"?' + [regex]::Escape($normalizedRoot) + '(?:/|\s|"|$)'
        $nextDiscovery = [DateTime]::MinValue
        $editors = @()
        $guardDeadline = if ($TimeoutSeconds -gt 0) { [DateTime]::UtcNow.AddSeconds($TimeoutSeconds) } else { [DateTime]::MaxValue }
        while ([DateTime]::UtcNow -lt $guardDeadline) {
            try {
                if ([DateTime]::UtcNow -ge $nextDiscovery) {
                    $display = Get-UnityInternalDisplay
                    $editors = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" |
                        Where-Object { $_.CommandLine -and $_.CommandLine.Replace('\', '/') -match $projectPattern })
                    $nextDiscovery = [DateTime]::UtcNow.AddSeconds(1)
                }
                $placedWindows = 0
                foreach ($editorProcess in $editors) {
                    foreach ($window in [Zjcs.UnityDisplay]::GetWindows($editorProcess.ProcessId)) {
                        $showMain = $window.IsEditorMain -and $editorProcess.CommandLine -notmatch '(?i)(^|\s)-batchmode(\s|$)'
                        if (($window.Visible -or $showMain) -and [Zjcs.UnityDisplay]::Place($window.Handle, $display, $showMain)) { $placedWindows++ }
                    }
                }
                if ($StatusPath -and $placedWindows -ne $lastGuardWindowCount) {
                    @{ ProcessId = $PID; ProjectRoot = $normalizedRoot; InternalHardwareId = $display.HardwareId;
                        InternalIsPrimary = $display.Primary; VisibleWindows = $placedWindows; Error = $null } |
                        ConvertTo-Json | Set-Content -LiteralPath $StatusPath -Encoding UTF8
                    $lastGuardWindowCount = $placedWindows
                }
            } catch {
                if ($StatusPath) {
                    @{ ProcessId = $PID; ProjectRoot = $normalizedRoot; Error = $_.Exception.Message } |
                        ConvertTo-Json | Set-Content -LiteralPath $StatusPath -Encoding UTF8
                }
                $nextDiscovery = [DateTime]::MinValue
                $editors = @()
            }
            Start-Sleep -Milliseconds 250
        }
    } finally { $guardMutex.ReleaseMutex(); $guardMutex.Dispose() }
    return
}

if ($WatchProcessId -gt 0) {
    $ErrorActionPreference = 'Stop'
    $process = Get-Process -Id $WatchProcessId -ErrorAction SilentlyContinue
    if ($process) {
        $deadline = if ($TimeoutSeconds -gt 0) { [DateTime]::UtcNow.AddSeconds($TimeoutSeconds) } else { [DateTime]::MaxValue }
        $firstSeen = @{}
        $display = Get-UnityInternalDisplay
        while (-not $process.HasExited -and [DateTime]::UtcNow -lt $deadline) {
            $windows = @([Zjcs.UnityDisplay]::GetWindows($WatchProcessId))
            $liveHandles = @($windows | ForEach-Object { $_.Handle.ToInt64() })
            foreach ($oldHandle in @($firstSeen.Keys)) {
                if ($oldHandle -notin $liveHandles) { $firstSeen.Remove($oldHandle) }
            }
            foreach ($window in $windows) {
                $key = $window.Handle.ToInt64()
                if (-not $firstSeen.ContainsKey($key)) { $firstSeen[$key] = [DateTime]::UtcNow }
                # Let startup layout restoration finish, then allow the user to move windows freely.
                if (([DateTime]::UtcNow - $firstSeen[$key]).TotalSeconds -le 10) {
                    $show = $ShowEditor -and ($window.IsEditorMain -or $window.IsPlayerMain)
                    if ($window.IsPlayerMain -and $show -and $ClientWidth -gt 0 -and $ClientHeight -gt 0) {
                        [void][Zjcs.UnityDisplay]::PlacePlayer($window.Handle, $display, $ClientWidth, $ClientHeight)
                    } elseif ($window.Visible -or $show) { [void][Zjcs.UnityDisplay]::Place($window.Handle, $display, $show) }
                }
            }
            Start-Sleep -Milliseconds 250
            $process.Refresh()
        }
    }
}
