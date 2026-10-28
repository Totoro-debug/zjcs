# Dot-source this file. Each workspace belongs to one validation invocation.
function Get-ZjcsValidationRoot {
    [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../output/validation'))
}

function Assert-ZjcsValidationTree([string]$Path) {
    $item = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
    if ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
        throw "Validation cleanup refuses a junction or symbolic link: $Path"
    }
    if (-not $item.PSIsContainer) { return }
    $directories = [System.Collections.Generic.Stack[System.IO.DirectoryInfo]]::new()
    $directories.Push([System.IO.DirectoryInfo]::new($item.FullName))
    while ($directories.Count -gt 0) {
        $directory = $directories.Pop()
        $directory.Refresh()
        if ($directory.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
            throw "Validation cleanup refuses a junction or symbolic link: $($directory.FullName)"
        }
        foreach ($child in $directory.EnumerateFileSystemInfos()) {
            # Inspect every entry, including hidden files, without invoking the
            # PowerShell provider and recursive function again for each file.
            $attributes = $child.Attributes
            if ($attributes -band [System.IO.FileAttributes]::ReparsePoint) {
                throw "Validation cleanup refuses a junction or symbolic link: $($child.FullName)"
            }
            if ($attributes -band [System.IO.FileAttributes]::Directory) {
                $directories.Push([System.IO.DirectoryInfo]$child)
            }
        }
    }
}

function New-ZjcsValidationWorkspace {
    param([Parameter(Mandatory)][ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_-]{0,63}$')][string]$Name)
    $root = Get-ZjcsValidationRoot
    New-Item -ItemType Directory -Path $root -Force -ErrorAction Stop | Out-Null
    if ((Get-Item -LiteralPath $root -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
        throw "Validation root must not be a junction or symbolic link: $root"
    }
    $workspace = Join-Path $root ($Name + '-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $workspace -ErrorAction Stop | Out-Null
    @{
        format = 1
        repository = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
        ownerPid = $PID
        createdUtc = [DateTime]::UtcNow.ToString('o')
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $workspace '.zjcs-validation.json') -Encoding utf8 -ErrorAction Stop
    return $workspace
}

function Remove-ZjcsValidationWorkspace {
    param([Parameter(Mandatory)][string]$Path)
    # Check the exact absolute target before any recursive operation.
    $target = [System.IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    $root = [System.IO.Path]::GetFullPath((Get-ZjcsValidationRoot)).TrimEnd('\', '/')
    if ([System.IO.Path]::GetDirectoryName($target) -ne $root -or
        [System.IO.Path]::GetFileName($target) -notmatch '^[a-zA-Z0-9][a-zA-Z0-9_-]{0,63}-[a-f0-9]{32}$') {
        throw "Cleanup only accepts one named session directly inside $root"
    }
    if (-not (Test-Path -LiteralPath $target)) { return }
    if ((Get-Item -LiteralPath $root -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
        throw "Validation root must not be a junction or symbolic link: $root"
    }
    Assert-ZjcsValidationTree $target
    $markerPath = Join-Path $target '.zjcs-validation.json'
    $marker = Get-Content -LiteralPath $markerPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
    $repository = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    if ($marker.format -ne 1 -or $marker.repository -ne $repository) {
        throw 'The workspace does not belong to this repository.'
    }
    if ($marker.ownerPid -ne $PID -and (Get-Process -Id $marker.ownerPid -ErrorAction SilentlyContinue)) {
        throw 'Another live session owns this workspace.'
    }
    # A shell may have exited while its Editor/Player is still running.
    $normalized = $target.Replace('\', '/')
    $users = @(Get-CimInstance Win32_Process -ErrorAction Stop | Where-Object {
        $_.ProcessId -ne $PID -and $_.CommandLine -and
        $_.CommandLine.Replace('\', '/').IndexOf($normalized, [StringComparison]::OrdinalIgnoreCase) -ge 0
    })
    if ($users.Count -gt 0) { throw "Validation processes still use this workspace: $($users.ProcessId -join ', ')" }
    Remove-Item -LiteralPath $target -Recurse -Force -ErrorAction Stop
}

function Invoke-ZjcsValidation {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$ScriptBlock
    )
    $workspace = New-ZjcsValidationWorkspace -Name $Name
    $previousWorkspace = $env:ZJCS_VALIDATION_DIR
    $previousRepository = $env:ZJCS_REPOSITORY_ROOT
    try {
        $env:ZJCS_VALIDATION_DIR = $workspace
        $env:ZJCS_REPOSITORY_ROOT = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
        & $ScriptBlock $workspace
    }
    finally {
        $env:ZJCS_VALIDATION_DIR = $previousWorkspace
        $env:ZJCS_REPOSITORY_ROOT = $previousRepository
        Remove-ZjcsValidationWorkspace -Path $workspace
    }
}
