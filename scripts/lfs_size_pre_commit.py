from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path


THRESHOLD_BYTES = 50_000_000
POINTER_HEADER = b"version https://git-lfs.github.com/spec/v1\n"


def git(*args: str, input_bytes: bytes | None = None, check: bool = True) -> bytes:
    result = subprocess.run(
        ["git", *args],
        input=input_bytes,
        capture_output=True,
        check=False,
    )
    if check and result.returncode:
        detail = result.stderr.decode(errors="replace").strip()
        raise RuntimeError(detail or f"git {' '.join(args)} failed")
    return result.stdout


def staged_index() -> tuple[dict[bytes, bytes], set[bytes]]:
    changed = set(
        git("diff", "--cached", "--name-only", "--diff-filter=ACMRT", "-z").split(b"\0")
    ) - {b""}
    entries: dict[bytes, bytes] = {}
    for entry in git("ls-files", "--stage", "-z").split(b"\0"):
        if not entry:
            continue
        metadata, path = entry.split(b"\t", 1)
        _, object_id, stage = metadata.split()
        if stage == b"0" and path in changed:
            entries[path] = object_id
    return entries, changed


def staged_object_size(object_id: bytes) -> int:
    return int(git("cat-file", "-s", object_id.decode("ascii")).strip())

def merge_generated_rules(
    original: bytes, staged: bytes, generated: bytes
) -> bytes:
    staged_lines = {line.rstrip(b"\r\n") for line in staged.splitlines()}
    original_lines = {line.rstrip(b"\r\n") for line in original.splitlines()}
    additions = [
        line.rstrip(b"\r\n")
        for line in generated.splitlines()
        if line.rstrip(b"\r\n") not in staged_lines
        and line.rstrip(b"\r\n") not in original_lines
    ]
    if not additions:
        return original

    newline = b"\r\n" if b"\r\n" in original else b"\n"
    result = original
    if result and not result.endswith((b"\n", b"\r")):
        result += newline
    return result + newline.join(additions) + newline


def assert_worktree_matches_index(root: Path, path: bytes) -> None:
    path_text = os.fsdecode(path)
    result = subprocess.run(
        ["git", "--literal-pathspecs", "diff", "--quiet", "--", path_text],
        cwd=root,
        check=False,
    )
    if result.returncode != 0:
        raise RuntimeError(
            f"{path_text} also has unstaged changes; stage them before committing"
        )


def add_lfs_rules_and_renormalize(root: Path, paths: list[bytes]) -> None:
    attributes = root / ".gitattributes"
    original_exists = attributes.exists()
    original_worktree = attributes.read_bytes() if original_exists else b""
    staged_attributes = git("show", ":.gitattributes", check=False)
    rules_staged = False

    try:
        attributes.write_bytes(staged_attributes)
        for path in paths:
            git("lfs", "track", "--filename", "--", os.fsdecode(path))

        generated_attributes = attributes.read_bytes()
        git("add", "--", ".gitattributes")
        rules_staged = True
        for path in paths:
            pathspec = ":(literal)" + os.fsdecode(path)
            git("add", "--renormalize", "--", pathspec)

        current_index, _ = staged_index()
        for path in paths:
            object_id = current_index.get(path)
            if object_id is None:
                raise RuntimeError(f"{os.fsdecode(path)} is missing from the Git index")
            pointer = git("cat-file", "blob", object_id.decode("ascii"))
            if not pointer.startswith(POINTER_HEADER):
                raise RuntimeError(
                    f"{os.fsdecode(path)} did not become a Git LFS pointer"
                )
    finally:
        if original_exists:
            restored = original_worktree
            if rules_staged:
                restored = merge_generated_rules(
                    original_worktree, staged_attributes, generated_attributes
                )
            attributes.write_bytes(restored)
        elif rules_staged:
            attributes.write_bytes(
                merge_generated_rules(b"", staged_attributes, generated_attributes)
            )
        elif attributes.exists():
            attributes.unlink()


def main() -> int:
    try:
        root = Path(git("rev-parse", "--show-toplevel").decode().strip())
        entries, _ = staged_index()
        candidates: list[bytes] = []
        for path, object_id in entries.items():
            if staged_object_size(object_id) <= THRESHOLD_BYTES:
                continue
            assert_worktree_matches_index(root, path)
            candidates.append(path)

        if not candidates:
            return 0

        git("lfs", "version")
        add_lfs_rules_and_renormalize(root, candidates)
        for path in candidates:
            print(f"Git LFS: {os.fsdecode(path)} (over 50 MB)")
        return 0
    except Exception as error:
        print(f"Git LFS size hook failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
