param([string]$Editor = 'D:/Tool/Unity/Editor')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
Invoke-ZjcsValidation -Name 'map12-stele-context-rules' -ScriptBlock {
    param($workspace)
    & python -B (Join-Path $PSScriptRoot 'test_map12_stele_context.py') --editor $Editor
    if ($LASTEXITCODE -ne 0) { throw 'Map12 stele context checks failed.' }
}
