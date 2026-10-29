from __future__ import annotations

import subprocess
import sys
from pathlib import Path


def git(*args: str) -> str:
    result = subprocess.run(
        ["git", *args],
        check=True,
        capture_output=True,
        text=True,
    )
    return result.stdout.strip()


def main() -> int:
    root = Path(git("rev-parse", "--show-toplevel"))
    hook_source = root / ".githooks" / "pre-commit"
    hooks_dir = Path(git("rev-parse", "--git-path", "hooks"))
    if not hooks_dir.is_absolute():
        hooks_dir = root / hooks_dir
    hook_target = hooks_dir / "pre-commit"

    source_bytes = hook_source.read_bytes()
    if hook_target.exists() and hook_target.read_bytes() != source_bytes:
        print(
            f"Refusing to replace existing hook: {hook_target}",
            file=sys.stderr,
        )
        return 1

    hooks_dir.mkdir(parents=True, exist_ok=True)
    hook_target.write_bytes(source_bytes)
    try:
        hook_target.chmod(0o755)
    except OSError:
        pass
    print(f"Installed 50 MB Git LFS pre-commit hook: {hook_target}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
