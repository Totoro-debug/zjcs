param([string]$Editor = 'D:/Tool/Unity/Editor')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
Invoke-ZjcsValidation -Name 'story-module-rules' -ScriptBlock {
    param($workspace)
    & python -B (Join-Path $PSScriptRoot 'test_story_module.py') --editor $Editor
    if ($LASTEXITCODE -ne 0) { throw 'Story module rules failed.' }
}
