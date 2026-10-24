"""Install the verified official ConfirmDialog prefab dependency closure."""
from __future__ import annotations

import hashlib
import json
import re
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE_PROJECT = ROOT / "output/character-creation-confirm-dialog-ripper/ExportedProject"
STAGED_INPUTS = ROOT / "output/character-creation-confirm-dialog-inputs"
PAYLOAD = ROOT / "unity/Assets/Official/CharacterCreation/Recovered"
PAYLOAD_MANIFEST = PAYLOAD / "ConfirmDialog/source-manifest.json"
REFERENCE_DIR = ROOT / "assets/reference/character-creation"
REFERENCE_MANIFEST = REFERENCE_DIR / "confirm-dialog-source-manifest.json"
BUNDLE_NAME = "defaultpackage_assets_prefabs_ui_confirmdialog.bundle"
BUNDLE_MD5 = "e9fb11a95f5ca61a90de42fc3fd90258"
SCRIPT_ROOTS = ("Scripts/", "MonoBehaviour/")
GUID_PATTERN = re.compile(r"guid:\s*([0-9a-fA-F]{32})")


def digest(path: Path, algorithm: str = "sha256") -> str:
    hasher = hashlib.new(algorithm)
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            hasher.update(chunk)
    return hasher.hexdigest()


def source_receipt(bundle: Path) -> dict:
    path = ROOT / ".tools/source-packages/cloud/854_156383/receipts.jsonl"
    if not path.is_file():
        raise FileNotFoundError("The verified original CDN receipt is missing: " + str(path))
    for line in path.read_text(encoding="utf-8").splitlines():
        receipt = json.loads(line)
        if receipt.get("name") != BUNDLE_NAME:
            continue
        if (receipt.get("status") != "downloaded" or receipt.get("md5") != BUNDLE_MD5
                or receipt.get("size") != bundle.stat().st_size
                or receipt.get("sha256") != digest(bundle)):
            raise ValueError("The ConfirmDialog bundle does not match its original CDN receipt.")
        return receipt
    raise ValueError("No original CDN receipt matches the ConfirmDialog bundle.")


def guid_index(assets: Path) -> dict[str, Path]:
    result = {}
    search = subprocess.run(
        ["rg", "-n", "--no-heading", "--glob", "*.meta", r"guid:\s*([0-9a-fA-F]{32})", str(assets)],
        capture_output=True, text=True, encoding="utf-8", errors="replace", check=False,
    )
    if search.returncode not in (0, 1):
        raise RuntimeError("Could not index AssetRipper dependency metadata: " + search.stderr)
    for line in search.stdout.splitlines():
        match = re.match(r"^(.*):\d+:\s*guid:\s*([0-9a-fA-F]{32})$", line)
        if match:
            result[match.group(2).lower()] = Path(match.group(1)).with_suffix("")
    return result


def references(path: Path) -> set[str]:
    try:
        text = path.read_text(encoding="utf-8", errors="ignore")
    except OSError:
        return set()
    return {value.lower() for value in GUID_PATTERN.findall(text)}


def install(source: Path) -> dict:
    relative = source.relative_to(SOURCE_PROJECT / "Assets")
    target = PAYLOAD / "ConfirmDialog" / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    source_hash = digest(source)
    if target.exists() and digest(target) != source_hash:
        raise ValueError("Refusing to overwrite changed official ConfirmDialog payload: " + str(target))
    if not target.exists():
        shutil.copy2(source, target)

    item = {"path": target.relative_to(ROOT / "unity").as_posix(), "sha256": source_hash}
    source_meta = Path(str(source) + ".meta")
    if source_meta.exists():
        target_meta = Path(str(target) + ".meta")
        meta_hash = digest(source_meta)
        if target_meta.exists() and digest(target_meta) != meta_hash:
            raise ValueError("Refusing to overwrite changed official ConfirmDialog metadata: " + str(target_meta))
        if not target_meta.exists():
            shutil.copy2(source_meta, target_meta)
        item["meta_sha256"] = meta_hash
    return item


def build() -> dict:
    assets = SOURCE_PROJECT / "Assets"
    root = assets / "Prefabs/UI/ConfirmDialog/ConfirmDialog.prefab"
    bundle = STAGED_INPUTS / BUNDLE_NAME
    if not root.is_file():
        raise FileNotFoundError("Official ConfirmDialog prefab is missing: " + str(root))
    if not bundle.is_file() or digest(bundle, "md5") != BUNDLE_MD5:
        raise ValueError("The staged ConfirmDialog bundle does not match manifest 854_156383.")

    index = guid_index(assets)
    pending = [root]
    selected = set()
    skipped_scripts = {}
    while pending:
        source = pending.pop()
        if source in selected:
            continue
        selected.add(source)
        for guid in references(source):
            dependency = index.get(guid)
            if dependency is None or dependency in selected:
                continue
            relative = dependency.relative_to(assets).as_posix()
            if relative.startswith(SCRIPT_ROOTS):
                skipped_scripts[guid] = relative
            elif dependency.is_file():
                pending.append(dependency)

    outputs = [install(source) for source in sorted(selected, key=lambda path: path.as_posix())]
    staged_inputs = [
        {"name": path.name, "bytes": path.stat().st_size, "sha256": digest(path)}
        for path in sorted(STAGED_INPUTS.iterdir()) if path.is_file()
    ]
    manifest = {
        "manifest": "854_156383",
        "source": {
            "bundle": BUNDLE_NAME,
            "bundle_md5": BUNDLE_MD5,
            "bundle_sha256": digest(bundle),
            "source_receipt": source_receipt(bundle),
            "asset_ripper": "1.3.14",
            "resolved_serialized_assets": len(selected),
            "staged_inputs": staged_inputs,
        },
        "excluded_client_scripts": [
            {"guid": guid, "path": path} for guid, path in sorted(skipped_scripts.items())
        ],
        "outputs": outputs,
    }
    encoded = json.dumps(manifest, ensure_ascii=False, sort_keys=True, indent=2) + "\n"
    REFERENCE_DIR.mkdir(parents=True, exist_ok=True)
    for path in (REFERENCE_MANIFEST, PAYLOAD_MANIFEST):
        if path.exists() and path.read_text(encoding="utf-8") != encoded:
            raise ValueError("Refusing to overwrite changed ConfirmDialog source manifest: " + str(path))
        if not path.exists():
            path.write_text(encoded, encoding="utf-8", newline="\n")

    return {"output_assets": len(outputs), "excluded_client_scripts": len(skipped_scripts),
            "staged_inputs": len(staged_inputs), "manifest": str(REFERENCE_MANIFEST)}


if __name__ == "__main__":
    print(json.dumps(build(), ensure_ascii=False, sort_keys=True))
