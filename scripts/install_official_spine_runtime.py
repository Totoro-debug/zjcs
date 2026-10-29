"""Restore the pinned official Spine 4.2 Unity runtime from its unitypackage."""
import argparse
import hashlib
import json
import os
import tempfile
import tarfile
from pathlib import Path, PurePosixPath


ROOT = Path(__file__).resolve().parents[1]
ARCHIVE_NAME = "spine-unity-4.2-2026-05-29.unitypackage"
ARCHIVE_SHA256 = "4ed774b00cd4c8ec62f55f92d495dea31192cf55f1bf11c3c8a0b0742d2f77ac"
SOURCE_URL = "https://esotericsoftware.com/files/runtimes/unity/spine-unity-4.2-2026-05-29.unitypackage"
UNITY_VERSION = "Spine 4.2 / Unity 2017.1-6000.3"
MANIFEST_PATH = "assets/reference/battle-spine/unity-runtime-source-manifest.json"


def digest(data):
    return hashlib.sha256(data).hexdigest()


def contained(root, relative):
    target = root.joinpath(*PurePosixPath(relative).parts)
    if target.is_symlink() or not target.resolve().is_relative_to(root.resolve()):
        raise ValueError("Spine Unity package path escapes project: " + relative)
    return target


def write_verified(path, content, accepted_existing_hashes=()):
    if path.exists():
        existing_hash = hashlib.sha256(path.read_bytes()).hexdigest()
        if existing_hash == digest(content):
            return
        if existing_hash not in accepted_existing_hashes:
            raise ValueError("Refusing to overwrite changed Spine Unity asset: " + str(path))
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(prefix=".spine-runtime-", dir=path.parent)
    temporary = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "wb") as output:
            output.write(content)
        if hashlib.sha256(temporary.read_bytes()).hexdigest() != digest(content):
            raise ValueError("Spine Unity output hash changed while copying: " + str(path))
        temporary.replace(path)
    finally:
        temporary.unlink(missing_ok=True)


def restore(archive, project_root=ROOT):
    archive = Path(archive).resolve()
    project_root = Path(project_root).resolve()
    archive_bytes = archive.read_bytes()
    archive_hash = digest(archive_bytes)
    if archive.name != ARCHIVE_NAME or archive_hash != ARCHIVE_SHA256:
        raise ValueError("Spine Unity package does not match the pinned official archive.")

    members = {}
    with tarfile.open(archive, "r:gz") as package:
        for member in package.getmembers():
            parts = PurePosixPath(member.name).parts
            if len(parts) != 2 or parts[1] != "pathname":
                continue
            pathname_stream = package.extractfile(member)
            if pathname_stream is None:
                continue
            pathname = pathname_stream.read().decode("utf-8").strip().replace("\\", "/").rstrip("/")
            if not pathname.startswith("Assets/Spine/"):
                continue
            content_members = {}
            for asset_name in ("asset", "asset.meta"):
                try:
                    asset_member = package.getmember(parts[0] + "/" + asset_name)
                except KeyError:
                    continue
                stream = package.extractfile(asset_member)
                if stream is not None:
                    content_members[asset_name] = stream.read()
            if content_members:
                members[pathname] = content_members

    if not members:
        raise ValueError("No Assets/Spine runtime files were found in the official package.")

    outputs = []
    preserved_overrides = []
    for pathname, contents in sorted(members.items()):
        for suffix, content in sorted(contents.items()):
            relative = pathname + (".meta" if suffix == "asset.meta" else "")
            target = contained(project_root, relative)
            source_hash = digest(content)
            output = content[3:] if relative.endswith(".asmdef") and content.startswith(b"\xef\xbb\xbf") else content
            if relative == "Assets/Spine/Runtime/spine-unity.asmdef" and target.exists():
                current = target.read_bytes()
                if digest(current) not in (digest(output), source_hash):
                    source_definition = json.loads(output.decode("utf-8-sig"))
                    current_definition = json.loads(current.decode("utf-8-sig"))
                    source_references = set(source_definition.get("references", []))
                    current_references = set(current_definition.get("references", []))
                    if (current_definition.get("name") != source_definition.get("name")
                            or not source_references.issubset(current_references)
                            or current_definition.get("allowUnsafeCode") != source_definition.get("allowUnsafeCode")):
                        raise ValueError("Spine Unity assembly definition changed beyond additive project references.")
                    preserved_overrides.append({
                        "path": relative,
                        "additional_references": sorted(current_references - source_references),
                    })
                    output = current
            output_hash = digest(output)
            accepted = (source_hash,) if source_hash != output_hash else ()
            if suffix == "asset.meta":
                accepted = tuple(set(accepted) | {source_hash})
            write_verified(target, output, accepted)
            outputs.append({"path": relative, "source_sha256": source_hash,
                            "output_sha256": output_hash, "bytes": len(output)})

    manifest = {
        "source": SOURCE_URL,
        "version": UNITY_VERSION,
        "archive_sha256": archive_hash,
        "archive_bytes": len(archive_bytes),
        "operation": "Official unitypackage assets and metadata restored; UTF-8 BOM removed from asmdef JSON for the project static checker.",
        "preserved_project_overrides": preserved_overrides,
        "outputs": outputs,
    }
    manifest_bytes = (json.dumps(manifest, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
    manifest_target = contained(ROOT, MANIFEST_PATH)
    accepted_manifest_hashes = ()
    if manifest_target.exists():
        previous = json.loads(manifest_target.read_text(encoding="utf-8"))
        if previous.get("archive_sha256") != archive_hash or previous.get("source") != SOURCE_URL:
            raise ValueError("Refusing to replace a Spine Unity manifest for a different source.")
        accepted_manifest_hashes = (hashlib.sha256(manifest_target.read_bytes()).hexdigest(),)
    write_verified(manifest_target, manifest_bytes, accepted_manifest_hashes)
    return {"files": len(outputs), "archive_sha256": archive_hash,
            "manifest": MANIFEST_PATH, "manifest_sha256": digest(manifest_bytes)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", type=Path,
                        default=ROOT / ".tools/source-packages" / ARCHIVE_NAME)
    parser.add_argument("--project-root", type=Path, default=ROOT / "unity")
    args = parser.parse_args()
    print(json.dumps(restore(args.archive, args.project_root), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
