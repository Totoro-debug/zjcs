function Assert-HomeValidationProject {
    param([Parameter(Mandatory)][string]$Repository, [Parameter(Mandatory)][string]$Project)
    $validation = [IO.Path]::GetFullPath((Join-Path $Repository 'output/validation')).TrimEnd('\', '/')
    $candidate = [IO.Path]::GetFullPath($Project).TrimEnd('\', '/')
    if (-not $candidate.StartsWith($validation + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath (Join-Path $candidate 'ProjectSettings/ProjectVersion.txt') -PathType Leaf)) {
        throw 'Use an independent source Unity project below output/validation.'
    }
    $ancestor = [IO.DirectoryInfo]::new($candidate)
    while ($null -ne $ancestor) {
        if ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked validation projects are forbidden.' }
        if ($ancestor.FullName.Equals($validation, [StringComparison]::OrdinalIgnoreCase)) { return }
        if (Test-Path -LiteralPath (Join-Path $ancestor.FullName '.git')) { throw 'Git worktrees and checkouts are forbidden for home validation.' }
        $ancestor = $ancestor.Parent
    }
    throw 'Validation project escapes output/validation.'
}
