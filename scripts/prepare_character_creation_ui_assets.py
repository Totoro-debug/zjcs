"""Install the recovered official character creation prefab dependency closure."""
from __future__ import annotations

import hashlib
import json
import re
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE_PROJECT = ROOT / "output/character-creation-ui-ripper/ExportedProject"
STAGED_INPUTS = ROOT / "output/character-creation-ui-inputs"
PAYLOAD = ROOT / "unity/Assets/Official/CharacterCreation/Recovered"
PAYLOAD_MANIFEST = ROOT / "unity/Assets/Official/CharacterCreation/create-player-ui-source-manifest.json"
REFERENCE_DIR = ROOT / "assets/reference/character-creation"
REFERENCE_MANIFEST = REFERENCE_DIR / "create-player-ui-source-manifest.json"
BUNDLE_NAME = "defaultpackage_assets_prefabs_ui_createplayer.bundle"
SCRIPT_ROOT = "Assets/Scripts/"
GUID_PATTERN = re.compile(r"guid:\s*([0-9a-fA-F]{32})")


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def bundle_sources() -> dict:
    path = ROOT / ".tools/source-packages/extracted/bundle-sources.json"
    if not path.exists():
        return {}
    source = json.loads(path.read_text(encoding="utf-8"))
    found = {}

    def visit(value):
        if isinstance(value, dict):
            for key, child in value.items():
                if isinstance(child, str) and BUNDLE_NAME in child:
                    found[key] = child
                visit(child)
        elif isinstance(value, list):
            for child in value:
                visit(child)

    visit(source)
    return found


def source_guid_index(assets: Path) -> dict[str, Path]:
    index = {}
    for meta in assets.rglob("*.meta"):
        try:
            match = GUID_PATTERN.search(meta.read_text(encoding="utf-8", errors="ignore"))
        except OSError:
            continue
        if match:
            index[match.group(1).lower()] = meta.with_suffix("")
    return index


def referenced_guids(path: Path) -> set[str]:
    try:
        text = path.read_text(encoding="utf-8", errors="ignore")
    except OSError:
        return set()
    return {value.lower() for value in GUID_PATTERN.findall(text)}


def install_file(source: Path, relative: Path) -> dict:
    target = PAYLOAD / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    data_hash = sha256(source)
    if target.exists() and sha256(target) != data_hash:
        raise ValueError("Refusing to overwrite changed official character UI payload: " + str(target))
    if not target.exists():
        shutil.copy2(source, target)
    source_meta = Path(str(source) + ".meta")
    if source_meta.exists():
        target_meta = Path(str(target) + ".meta")
        meta_hash = sha256(source_meta)
        if target_meta.exists() and sha256(target_meta) != meta_hash:
            raise ValueError("Refusing to overwrite changed Unity metadata: " + str(target_meta))
        if not target_meta.exists():
            shutil.copy2(source_meta, target_meta)
        return {"path": target.relative_to(ROOT / "unity").as_posix(),
                "sha256": data_hash, "meta_sha256": meta_hash}
    return {"path": target.relative_to(ROOT / "unity").as_posix(), "sha256": data_hash}


def build() -> dict:
    assets = SOURCE_PROJECT / "Assets"
    prefabs = assets / "Prefabs/UI/CreatePlayer"
    if not assets.is_dir() or not prefabs.is_dir():
        raise FileNotFoundError("AssetRipper CreatePlayer export is missing: " + str(prefabs))
    for required in ("CreatePlayerPanel.prefab", "CreatePlayerPanelDIYContent.prefab"):
        if not (prefabs / required).is_file():
            raise FileNotFoundError("Official character creation prefab is missing: " + required)

    guid_index = source_guid_index(assets)
    roots = sorted(path for path in prefabs.iterdir()
                   if path.is_file() and path.suffix != ".meta")
    pending = list(roots)
    selected: dict[Path, str] = {}
    skipped_scripts = {}

    while pending:
        source = pending.pop()
        if source in selected:
            continue
        relative = source.relative_to(assets)
        selected[source] = "Assets/" + relative.as_posix()
        for guid in referenced_guids(source):
            dependency = guid_index.get(guid)
            if dependency is None or dependency in selected:
                continue
            dependency_relative = dependency.relative_to(assets).as_posix()
            if dependency_relative.startswith(SCRIPT_ROOT):
                skipped_scripts[guid] = dependency_relative
                continue
            if dependency.is_file():
                pending.append(dependency)

    outputs = []
    for source, unity_path in sorted(selected.items(), key=lambda item: item[1]):
        relative = Path(unity_path.removeprefix("Assets/"))
        outputs.append(install_file(source, relative))

    staged = []
    if STAGED_INPUTS.is_dir():
        for source in sorted(STAGED_INPUTS.iterdir()):
            if source.is_file():
                staged.append({"name": source.name, "bytes": source.stat().st_size,
                               "sha256": sha256(source)})
    bundle = STAGED_INPUTS / BUNDLE_NAME
    if not bundle.is_file():
        raise FileNotFoundError("The pinned CreatePlayer bundle is not in its verified CAB input closure.")

    manifest = {
        "manifest": "854_156383",
        "source": {
            "bundle": BUNDLE_NAME,
            "bundle_sha256": sha256(bundle),
            "staged_inputs": staged,
            "source_receipt": bundle_sources(),
            "asset_ripper": "1.3.14",
            "resolved_serialized_assets": 24,
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
            raise ValueError("Refusing to overwrite changed character creation UI source manifest: " + str(path))
        if not path.exists():
            path.write_text(encoded, encoding="utf-8", newline="\n")

    return {"output_assets": len(outputs), "excluded_client_scripts": len(skipped_scripts),
            "staged_inputs": len(staged), "manifest": str(REFERENCE_MANIFEST)}


if __name__ == "__main__":
    print(json.dumps(build(), ensure_ascii=False, sort_keys=True))
