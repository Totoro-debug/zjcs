param(
    [ValidateSet('info', 'open', 'initialize')]
    [string]$Action = 'info'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$configPath = Join-Path $repository 'unity-tools.json'
if (-not (Test-Path -LiteralPath $configPath)) {
    throw "Unity tools configuration not found: $configPath"
}

$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
function Resolve-ConfiguredPath([string]$path) {
    if ([System.IO.Path]::IsPathRooted($path)) {
        return [System.IO.Path]::GetFullPath($path)
    }
    return Join-Path $repository $path.Replace('/', '\')
}

$editor = Resolve-ConfiguredPath $config.editorExecutable
$hub = if ($config.hubExecutable) { Resolve-ConfiguredPath $config.hubExecutable } else { $null }
$project = Resolve-ConfiguredPath $config.projectPath
$windowsBuildSupport = Resolve-ConfiguredPath $config.windowsBuildSupport

if (-not (Test-Path -LiteralPath $editor -PathType Leaf)) {
    throw "Unity $($config.editorVersion) Editor is missing: $editor"
}
$actualVersion = (Get-Item -LiteralPath $editor).VersionInfo.ProductVersion
if (-not $actualVersion.StartsWith($config.editorVersion + '_', [System.StringComparison]::Ordinal)) {
    throw "Unity Editor version mismatch. Expected $($config.editorVersion), found $actualVersion at $editor"
}
if (-not (Test-Path -LiteralPath $project -PathType Container)) {
    throw "Unity project is missing: $project"
}

switch ($Action) {
    'info' {
        [pscustomobject]@{
            EditorVersion = $config.editorVersion
            Editor = $editor
            Hub = if ($hub -and (Test-Path -LiteralPath $hub -PathType Leaf)) { $hub } else { 'not installed (optional)' }
            Project = $project
            WindowsBuildSupport = Test-Path -LiteralPath $windowsBuildSupport -PathType Container
            InternalDisplay = (Get-UnityInternalDisplay).Device
        } | Format-List
    }
    'open' {
        Start-UnityOnInternalDisplay -Editor $editor -ArgumentList @('-force-d3d12', '-projectPath', "`"$project`"")
    }
    'initialize' {
        $logDirectory = Join-Path $repository 'output/unity'
        New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
        $logPath = Join-Path $logDirectory 'initialize.log'
        $unityProcess = Start-UnityOnInternalDisplay -Editor $editor -ArgumentList @(
            '-batchmode', '-quit', '-force-d3d12', '-projectPath', "`"$project`"",
            '-executeMethod', 'Zjcs.Editor.WindowsGraphicsSettings.Configure',
            '-logFile', "`"$logPath`""
        )
        $unityProcess.WaitForExit()
        $log = Get-Content -LiteralPath $logPath -Raw
        if ($unityProcess.ExitCode -ne 0 -or $log -match 'executeMethod method .* threw exception|Compilation failed') {
            throw "Unity initialization failed with exit code $($unityProcess.ExitCode). See $logPath"
        }
        Write-Output "Unity project initialized. Log: $logPath"
    }
}
