"""Install verified official material-gathering prefabs and their dependencies."""
from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
ASSET_ROOT = ROOT / "unity/Assets"
EXPORT_ASSETS = ROOT / "output/material-gathering-native-20261024/ExportedProject/Assets"
STAGED_INPUTS = ROOT / "output/material-gathering-inputs-20261024"
STAGED_MANIFEST = STAGED_INPUTS / "source-manifest.json"
PAYLOAD = ASSET_ROOT / "Official/Gathering/Prefabs"
REFERENCE = ROOT / "assets/reference/dungeon-ui/material-gathering-prefab-source-manifest.json"
TEXT_EXTENSIONS = {
    ".anim", ".asset", ".controller", ".mat", ".mask", ".overridecontroller",
    ".prefab", ".shader", ".shadervariants", ".spriteatlas", ".terrainlayer",
}
GUID_LINE = re.compile(r"(?m)^guid: ([0-9a-f]{32})$")
GUID_REFERENCE = re.compile(r"guid: ([0-9a-f]{32})")
PREFABS = (
    "Prefabs/UI/MopUp/MopUpPanel.prefab",
    "Prefabs/UI/MopUp/MopUpEntryPanel.prefab",
)
RUNTIME_PREFABS = {
    "Prefabs/UI/MopUp/MopUpPanel.prefab": "Gathering/MopUpPanel.prefab",
    "Prefabs/UI/MopUp/MopUpEntryPanel.prefab": "Gathering/MopUpEntryPanel.prefab",
}
KNOWN_EXPORT_GUIDS = {
    "7136a7fb9000b4640b246b4fe791d1b7": "AnimatorController/MopUpContent.controller",
    "789cf9cf3d193ed40974d42492c3befa": "AnimationClip/MopUpPanel_Idle.anim",
    "d2e72831c9b55394eb3a2086cab73dfc": "AnimationClip/MopUpPanel_ChangeTarget.anim",
}


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def stable_guid(relative: str) -> str:
    return hashlib.md5(("zjcs:" + relative.replace("\\", "/").lower()).encode("utf-8")).hexdigest()


def project_script_guids() -> set[str]:
    result: set[str] = set()
    roots = (ASSET_ROOT, ROOT / "unity/Packages", ROOT / "unity/Library/PackageCache")
    for root in roots:
        if not root.is_dir():
            continue
        for meta in root.rglob("*.meta"):
            match = GUID_LINE.search(meta.read_text(encoding="utf-8", errors="ignore"))
            if match:
                result.add(match.group(1))
    return result


def verify_inputs() -> dict:
    staged = json.loads(STAGED_MANIFEST.read_text(encoding="utf-8"))
    if staged.get("manifest") != "871_156598":
        raise ValueError("Material-gathering inputs are not from current manifest 871_156598.")
    requested = set(staged.get("requested_bundles", []))
    if "defaultpackage_assets_prefabs_ui_mopup.bundle" not in requested:
        raise ValueError("The staged input manifest does not contain the official MopUp prefab bundle.")
    inputs = {Path(row.get("output", "")).name: row for row in staged.get("inputs", [])}
    staged_bundles = [row for row in staged.get("inputs", [])
                      if Path(row.get("output", "")).suffix.lower() == ".bundle"]
    for row in staged_bundles:
        path = STAGED_INPUTS / row.get("output", "")
        expected_size = row.get("decoded_size", row.get("size"))
        expected_sha = row.get("decoded_sha256", row.get("sha256"))
        if not path.is_file() or path.stat().st_size != expected_size:
            raise ValueError("A staged official bundle is missing or has the wrong size: " + str(path))
        if sha(path.read_bytes()) != expected_sha:
            raise ValueError("A staged official bundle failed its SHA256 check: " + str(path))
    root_bundle = inputs.get("defaultpackage_assets_prefabs_ui_mopup.bundle")
    if root_bundle is None or root_bundle.get("verified") is False:
        raise ValueError("The official MopUp prefab bundle is absent or unverified.")
    return staged


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--runtime-only", action="store_true")
    args = parser.parse_args()
    if args.runtime_only:
        install_runtime_mappings()
        return
    staged = verify_inputs()
    if not EXPORT_ASSETS.is_dir():
        raise FileNotFoundError("Run AssetRipper on the verified material-gathering bundle closure first.")

    guid_to_asset: dict[str, Path] = {}
    for meta in EXPORT_ASSETS.rglob("*.meta"):
        asset = meta.with_suffix("")
        if not asset.is_file():
            continue
        match = GUID_LINE.search(meta.read_text(encoding="utf-8", errors="ignore"))
        if match:
            guid_to_asset[match.group(1)] = asset

    redirects: dict[str, str] = {}
    for unresolved, relative in KNOWN_EXPORT_GUIDS.items():
        asset = EXPORT_ASSETS / relative
        meta = Path(str(asset) + ".meta")
        match = GUID_LINE.search(meta.read_text(encoding="utf-8", errors="strict"))
        if not asset.is_file() or match is None:
            raise FileNotFoundError("The verified official export is missing " + str(asset))
        redirects[unresolved] = match.group(1)

    pending = [EXPORT_ASSETS / path for path in PREFABS]
    selected: set[Path] = set()
    omitted_script_refs: set[str] = set()
    while pending:
        asset = pending.pop()
        if asset in selected:
            continue
        if not asset.is_file() or not Path(str(asset) + ".meta").is_file():
            raise FileNotFoundError("AssetRipper did not export the official MopUp prefab: " + str(asset))
        selected.add(asset)
        if asset.suffix.lower() not in TEXT_EXTENSIONS:
            continue
        source = asset.read_text(encoding="utf-8", errors="ignore")
        for guid in GUID_REFERENCE.findall(source):
            if guid == "0" * 32:
                continue
            resolved = redirects.get(guid, guid)
            dependency = guid_to_asset.get(resolved)
            if dependency is None:
                raise ValueError("Unresolved non-script prefab dependency " + guid + " in " + str(asset))
            if dependency.suffix.lower() == ".cs":
                omitted_script_refs.add(guid)
            else:
                pending.append(dependency)

    remap: dict[str, str] = {}
    for source in selected:
        meta = Path(str(source) + ".meta").read_text(encoding="utf-8", errors="strict")
        match = GUID_LINE.search(meta)
        if match is None:
            raise ValueError("AssetRipper asset metadata has no GUID: " + str(source))
        remap[match.group(1)] = stable_guid("Official/Gathering/Prefabs/" + source.relative_to(EXPORT_ASSETS).as_posix())

    outputs: list[dict] = []
    for source in sorted(selected):
        relative = source.relative_to(EXPORT_ASSETS)
        target = PAYLOAD / relative
        data = source.read_bytes()
        if source.suffix.lower() in TEXT_EXTENSIONS:
            text = data.decode("utf-8")
            for old, new in redirects.items():
                text = text.replace("guid: " + old, "guid: " + new)
            for old, new in remap.items():
                text = text.replace("guid: " + old, "guid: " + new)
            data = text.encode("utf-8")
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.exists() and target.read_bytes() != data:
            raise ValueError("Refusing to overwrite changed official material-gathering asset: " + str(target))
        if not target.exists():
            target.write_bytes(data)
        source_meta = Path(str(source) + ".meta").read_text(encoding="utf-8", errors="strict")
        old_guid = GUID_LINE.search(source_meta).group(1)
        meta_text = GUID_LINE.sub("guid: " + remap[old_guid], source_meta, count=1)
        target_meta = Path(str(target) + ".meta")
        if target_meta.exists() and target_meta.read_text(encoding="utf-8") != meta_text:
            raise ValueError("Refusing to overwrite changed official material-gathering metadata: " + str(target_meta))
        if not target_meta.exists():
            target_meta.write_text(meta_text, encoding="utf-8", newline="\n")
        outputs.append({"path": target.relative_to(ROOT).as_posix(), "bytes": len(data),
                        "sha256": sha(data), "meta_sha256": sha(target_meta.read_bytes())})

    install_runtime_mappings(outputs)
    add_folder_metadata()
    project_guids = project_script_guids()
    REFERENCE.parent.mkdir(parents=True, exist_ok=True)
    REFERENCE.write_text(json.dumps({
        "manifest": "871_156598",
        "verified_staged_bundle_count": len([row for row in staged.get("inputs", [])
                                              if Path(row.get("output", "")).suffix.lower() == ".bundle"]),
        "staged_input_manifest_sha256": sha(STAGED_MANIFEST.read_bytes()),
        "exported_prefabs": list(PREFABS),
        "verified_known_reference_redirects": KNOWN_EXPORT_GUIDS,
        "exported_monoscript_guids_not_copied": sorted(omitted_script_refs),
        "unresolved_monoscript_guids_in_current_project": sorted(omitted_script_refs - project_guids),
        "runtime_prefabs": RUNTIME_PREFABS,
        "outputs": outputs,
    }, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("Installed", len(PREFABS), "official MopUp roots and", len(selected) - len(PREFABS), "dependencies.")
    print("MonoScript GUIDs already resolved by the project:", len(omitted_script_refs & project_guids))
    print("MonoScript GUIDs still unresolved in the project:", len(omitted_script_refs - project_guids))


def install_runtime_mappings(outputs: list[dict] | None = None) -> None:
    if not PAYLOAD.is_dir():
        raise FileNotFoundError("Official material-gathering prefabs have not been installed.")
    outputs = outputs or []
    for source_path, runtime_path in RUNTIME_PREFABS.items():
        source = PAYLOAD / source_path
        if not source.is_file():
            raise FileNotFoundError("Official material-gathering prefab was not installed: " + str(source))
        target = ASSET_ROOT / "Game/Resources" / runtime_path
        data = source.read_bytes()
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.exists() and target.read_bytes() != data:
            raise ValueError("Refusing to overwrite changed runtime material-gathering prefab: " + str(target))
        if not target.exists():
            target.write_bytes(data)
        meta_text = "fileFormatVersion: 2\nguid: " + stable_guid("Game/Resources/" + runtime_path) + "\n"
        target_meta = Path(str(target) + ".meta")
        if target_meta.exists() and target_meta.read_text(encoding="utf-8") != meta_text:
            raise ValueError("Refusing to overwrite changed runtime material-gathering metadata: " + str(target_meta))
        if not target_meta.exists():
            target_meta.write_text(meta_text, encoding="utf-8", newline="\n")
        outputs.append({"path": target.relative_to(ROOT).as_posix(), "bytes": len(data),
                        "sha256": sha(data), "meta_sha256": sha(target_meta.read_bytes())})


def add_folder_metadata() -> None:
    for folder in sorted((path for path in PAYLOAD.rglob("*") if path.is_dir()), key=lambda path: path.as_posix()):
        relative = folder.relative_to(PAYLOAD).as_posix()
        meta = Path(str(folder) + ".meta")
        text = "fileFormatVersion: 2\nguid: " + stable_guid("Official/Gathering/Prefabs/" + relative) + "\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
        if meta.exists() and meta.read_text(encoding="utf-8") != text:
            raise ValueError("Refusing to overwrite changed material-gathering folder metadata: " + str(meta))
        if not meta.exists():
            meta.write_text(text, encoding="utf-8", newline="\n")


if __name__ == "__main__":
    main()
