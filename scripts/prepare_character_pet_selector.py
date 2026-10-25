"""Copy pinned pet-plan selector prefabs and their referenced Unity assets."""
from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
EXPORT_ASSETS = ROOT / "output/character-pet-ripper/ExportedProject/Assets"
ASSET_ROOT = ROOT / "unity/Assets"
PAYLOAD_ROOT = ASSET_ROOT / "Official/Character/PetSelector"
INVENTORY_PATH = ROOT / ".tools/source-packages/cloud-research/bundle-inventory.json"
BUNDLE_SOURCES_PATH = ROOT / ".tools/source-packages/extracted/bundle-sources.json"
STAGED_SOURCE_MANIFEST = ROOT / "output/character-pet-inputs/source-manifest.json"
REFERENCE_PATH = ROOT / "assets/reference/pet-ui/source-manifest.json"
BUNDLE_NAME = "defaultpackage_assets_prefabs_ui_pet.bundle"
PINNED_APK_SHA256 = "ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9"
PREFABS = (
    "Prefabs/UI/Pet/PetPanel/PetPlanSelectPanel.prefab",
    "Prefabs/UI/Pet/PetPanel/PetPlanSelectPanelItem.prefab",
    "Prefabs/UI/Pet/PetSelectContent.prefab",
)
TEXT_ASSET_EXTENSIONS = {
    ".anim", ".asset", ".controller", ".guiskin", ".mat", ".mask",
    ".overridecontroller", ".prefab", ".rendertexture", ".shader",
    ".shadervariants", ".spriteatlas", ".terrainlayer",
}
GUID_LINE = re.compile(r"(?m)^guid: ([0-9a-f]{32})$")
GUID_REFERENCE = re.compile(r"guid: ([0-9a-f]{32})")


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def stable_guid(relative: str) -> str:
    return hashlib.md5(("zjcs:" + relative.replace("\\", "/").lower()).encode("utf-8")).hexdigest()


def folder_meta(directory: Path) -> None:
    relative = directory.relative_to(ASSET_ROOT).as_posix()
    path = Path(str(directory) + ".meta")
    if path.exists():
        return
    path.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {stable_guid(relative)}\n"
        "folderAsset: yes\nDefaultImporter:\n"
        "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n",
        encoding="utf-8",
        newline="\n",
    )


def read_inventory() -> tuple[dict[str, dict], dict[str, dict], dict, list[dict], list[dict]]:
    inventory = json.loads(INVENTORY_PATH.read_text(encoding="utf-8"))
    sources = json.loads(BUNDLE_SOURCES_PATH.read_text(encoding="utf-8"))
    staged = json.loads(STAGED_SOURCE_MANIFEST.read_text(encoding="utf-8"))
    if inventory.get("manifest") != "854_156383":
        raise ValueError("The bundle inventory is not pinned to manifest 854_156383.")
    rows = {row["name"]: row for row in inventory.get("bundles", [])}
    if BUNDLE_NAME not in rows or BUNDLE_NAME not in sources:
        raise ValueError("The pinned pet selector bundle is missing from its source manifests.")
    row = rows[BUNDLE_NAME]
    receipt = sources[BUNDLE_NAME]
    if row.get("hash") != receipt.get("bundle_hash"):
        raise ValueError("The pet selector bundle source does not match manifest 854_156383.")
    if receipt.get("origin") == "apk_zip" and receipt.get("apk_sha256") != PINNED_APK_SHA256:
        raise ValueError("The pet selector APK source does not match the pinned APK.")
    if row.get("size") != 2171659 or row.get("hash") != "a8a6eab8d9f018034d8f26180eb38675":
        raise ValueError("The pinned pet selector bundle hash or size changed.")
    if staged.get("manifest") != "854_156383" or BUNDLE_NAME not in staged.get("requested_bundles", []):
        raise ValueError("The staged pet selector inputs are not pinned to the requested original bundle.")
    bundle_inputs = []
    builtin_inputs = []
    for item in staged.get("inputs", []):
        name = item.get("name")
        if name == "assets/bin/Data/unity default resources":
            if item.get("apk_sha256") != PINNED_APK_SHA256:
                raise ValueError("The staged Unity built-in resources do not match the pinned APK.")
            builtin_inputs.append(item)
            continue
        source_row = rows.get(name)
        if source_row is None or source_row.get("hash") != item.get("raw_md5") \
                or source_row.get("size") != item.get("raw_size"):
            raise ValueError("A staged CAB dependency does not match the pinned bundle inventory: " + str(name))
        if item.get("manifest") != "854_156383":
            raise ValueError("A staged CAB dependency does not match manifest 854_156383: " + str(name))
        bundle_inputs.append(item)
    if not any(item.get("name") == BUNDLE_NAME for item in bundle_inputs):
        raise ValueError("The staged CAB closure does not contain the requested pet selector bundle.")
    return rows, sources, receipt, bundle_inputs, builtin_inputs


def main() -> None:
    if not EXPORT_ASSETS.is_dir():
        raise FileNotFoundError("Run AssetRipper on the verified pet UI bundle inputs first.")

    inventory, source_receipts, bundle_receipt, bundle_inputs, builtin_inputs = read_inventory()
    reference = json.loads(REFERENCE_PATH.read_text(encoding="utf-8"))
    if reference.get("version") != "854_156383":
        raise ValueError("The pet UI reference manifest is not pinned to 854_156383.")
    previous_selector = reference.get("pet_plan_selector", {})
    previous_outputs = {
        Path(item.get("path", "")).resolve(): item.get("sha256")
        for item in previous_selector.get("files", [])
        if item.get("path")
    }

    guid_to_asset: dict[str, Path] = {}
    for meta in EXPORT_ASSETS.rglob("*.meta"):
        try:
            match = GUID_LINE.search(meta.read_text(encoding="utf-8", errors="ignore"))
        except OSError:
            continue
        if match is None:
            continue
        guid = match.group(1)
        asset = meta.with_suffix("")
        if guid in guid_to_asset and guid_to_asset[guid] != asset:
            raise ValueError("AssetRipper produced a duplicate source GUID: " + guid)
        guid_to_asset[guid] = asset

    pending = [EXPORT_ASSETS / path for path in PREFABS]
    for path in pending:
        if not path.is_file() or not Path(str(path) + ".meta").is_file():
            raise FileNotFoundError("AssetRipper did not export the official pet selector prefab: " + str(path))

    included: set[Path] = set()
    omitted_scripts: set[str] = set()
    unresolved: set[str] = set()
    while pending:
        asset = pending.pop()
        if asset in included:
            continue
        included.add(asset)
        if asset.suffix.lower() not in TEXT_ASSET_EXTENSIONS:
            continue
        text = asset.read_text(encoding="utf-8", errors="strict")
        for guid in GUID_REFERENCE.findall(text):
            if guid == "0" * 32:
                continue
            dependency = guid_to_asset.get(guid)
            if dependency is None:
                unresolved.add(guid)
            elif dependency.suffix.lower() == ".cs":
                omitted_scripts.add(dependency.name)
            else:
                pending.append(dependency)

    if unresolved:
        raise ValueError("Unresolved non-script pet prefab dependencies: " + ", ".join(sorted(unresolved)))

    guid_remap: dict[str, str] = {}
    copied_files = []
    for source in sorted(included):
        source_meta = Path(str(source) + ".meta")
        relative = source.relative_to(EXPORT_ASSETS)
        destination = PAYLOAD_ROOT / relative
        destination_meta = Path(str(destination) + ".meta")
        source_meta_text = source_meta.read_text(encoding="utf-8", errors="ignore")
        source_guid_match = GUID_LINE.search(source_meta_text)
        if source_guid_match is None:
            raise ValueError("AssetRipper asset metadata has no GUID: " + str(source_meta))
        new_guid = stable_guid(destination.relative_to(ASSET_ROOT).as_posix())
        guid_remap[source_guid_match.group(1)] = new_guid


    for source in sorted(included):
        source_meta = Path(str(source) + ".meta")
        relative = source.relative_to(EXPORT_ASSETS)
        destination = PAYLOAD_ROOT / relative
        destination_meta = Path(str(destination) + ".meta")
        previous_hash = previous_outputs.get(destination.resolve())
        if destination.is_file() and previous_hash is not None \
                and sha256(destination.read_bytes()) != previous_hash:
            raise ValueError("Refusing to overwrite a changed official pet selector asset: " + str(destination))
        if destination.is_file() and previous_hash is None:
            raise ValueError("Refusing to overwrite an untracked pet selector asset: " + str(destination))

        data = source.read_bytes()
        if source.suffix.lower() in TEXT_ASSET_EXTENSIONS:
            text = data.decode("utf-8")
            for old_guid, new_guid in guid_remap.items():
                text = text.replace("guid: " + old_guid, "guid: " + new_guid)
            data = text.encode("utf-8")
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(data)

        meta_text = source_meta.read_text(encoding="utf-8", errors="strict")
        old_guid = GUID_LINE.search(meta_text).group(1)
        meta_text = GUID_LINE.sub("guid: " + guid_remap[old_guid], meta_text, count=1)
        destination_meta.write_text(meta_text, encoding="utf-8", newline="\n")
        copied_files.append({
            "path": destination.relative_to(ROOT).as_posix(),
            "bytes": len(data),
            "sha256": sha256(data),
            "meta_sha256": sha256(destination_meta.read_bytes()),
        })

    for directory in sorted({PAYLOAD_ROOT, *[path.parent for path in PAYLOAD_ROOT.rglob("*")]},
                            key=lambda path: len(path.parts)):
        if directory.is_dir():
            folder_meta(directory)

    selector = {
        "manifest": "854_156383",
        "asset_converter": "AssetRipper 1.3.14",
        "source_bundle": {
            "name": BUNDLE_NAME,
            "hash": bundle_receipt["bundle_hash"],
            "size": inventory[BUNDLE_NAME]["size"],
            "origin": bundle_receipt["origin"],
            "apk_sha256": bundle_receipt.get("apk_sha256"),
        },
        "source_bundles": bundle_inputs,
        "builtin_sources": builtin_inputs,
        "prefabs": list(PREFABS),
        "omitted_official_scripts": sorted(omitted_scripts),
        "files": copied_files,
    }
    reference["pet_plan_selector"] = selector
    REFERENCE_PATH.write_text(json.dumps(reference, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Prepared", len(copied_files), "official pet selector assets from",
          len(bundle_inputs), "verified bundles and", len(builtin_inputs), "built-in resources.")


if __name__ == "__main__":
    main()
