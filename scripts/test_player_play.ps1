param([string]$Editor = 'D:/Tool/Unity/Editor')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
Invoke-ZjcsValidation -Name 'player-play-rules' -ScriptBlock {
    param($workspace)
    $root = $env:ZJCS_REPOSITORY_ROOT
    $runtime = Join-Path $Editor 'Data/NetCoreRuntime'
    $dotnet = Join-Path $runtime 'dotnet.exe'
    $csc = Join-Path $Editor 'Data/DotNetSdkRoslyn/csc.dll'
    $framework = Get-ChildItem (Join-Path $runtime 'shared/Microsoft.NETCore.App') -Directory |
        Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    $output = Join-Path $workspace 'player-play-rules.dll'
    $options = @('/nologo', '/target:exe', '/langversion:9.0', '/nostdlib+', ('/out:"' + $output + '"'))
    $options += @('System.Private.CoreLib', 'System.Runtime', 'System.Console', 'System.Collections',
        'System.Linq', 'System.Threading.Tasks') | ForEach-Object {
            '/reference:"' + (Join-Path $framework.FullName ($_ + '.dll')) + '"'
        }
    $options += Get-ChildItem (Join-Path $root 'unity/Assets/Game/Runtime/Official/PlayerPlay') -Filter '*.cs' |
        ForEach-Object { '"' + $_.FullName + '"' }
    $options += '"' + (Join-Path $root 'scripts/fixtures/player_play_harness.cs') + '"'
    $response = Join-Path $workspace 'rules.rsp'
    [IO.File]::WriteAllLines($response, $options)
    & $dotnet $csc ('@' + $response)
    if ($LASTEXITCODE -ne 0) { throw 'Player-play rule compilation failed' }
    @{ runtimeOptions = @{ tfm = 'net6.0'; framework = @{ name = 'Microsoft.NETCore.App'; version = $framework.Name } } } |
        ConvertTo-Json -Depth 5 | Set-Content (Join-Path $workspace 'player-play-rules.runtimeconfig.json') -Encoding utf8
    & $dotnet $output
    if ($LASTEXITCODE -ne 0) { throw 'Player-play rule verification failed' }
}
