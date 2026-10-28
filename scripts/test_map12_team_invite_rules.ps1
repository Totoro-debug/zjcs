param([string]$Editor = 'D:/Tool/Unity/Editor')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
Invoke-ZjcsValidation -Name 'map12-team-invite-rules' -ScriptBlock {
    param($workspace)
    & python -B (Join-Path $PSScriptRoot 'test_map12_team_invite_rules.py') --editor $Editor
    if ($LASTEXITCODE -ne 0) { throw 'Map12 invitation rules failed.' }
}
