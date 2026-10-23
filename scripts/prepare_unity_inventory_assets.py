import argparse
import hashlib
import json
import shutil
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE_DATA = ROOT / "assets/official/bag-items.json"
SOURCE_MANIFEST = ROOT / "assets/reference/bag-items-source-manifest.json"
QUALITY_MANIFEST = ROOT / "assets/reference/inventory-quality-backgrounds-source-manifest.json"
ITEM_SOURCE_DATA = ROOT / "assets/official/gacha-item-sources/catalog.json"
ITEM_SOURCE_MANIFEST = ROOT / "assets/reference/gacha-item-sources/source-manifest.json"
UNITY_ROOT = ROOT / "unity/Assets/Official/Inventory"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def copy_verified(source: Path, destination: Path, expected: str) -> None:
    if not source.is_file() or sha256(source) != expected:
        raise ValueError(f"Official inventory source hash mismatch: {source}")
    if destination.exists():
        if sha256(destination) != expected:
            raise ValueError(f"Refusing to replace different inventory asset: {destination}")
        return
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)


def copy_inventory_manifest(source: Path, destination: Path) -> None:
    if not source.is_file():
        raise ValueError(f"Inventory source manifest is missing: {source}")
    source_hash = sha256(source)
    if destination.exists() and sha256(destination) == source_hash:
        return
    if destination.exists():
        existing = json.loads(destination.read_text(encoding="utf-8"))
        current = json.loads(source.read_text(encoding="utf-8"))
        existing.pop("resource_verification", None)
        current.pop("resource_verification", None)
        if not is_manifest_extension(existing, current):
            raise ValueError(f"Refusing to replace different inventory source manifest: {destination}")
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)


def is_manifest_extension(existing: object, current: object, path: tuple[str, ...] = ()) -> bool:
    if isinstance(existing, dict) and isinstance(current, dict):
        for key, value in existing.items():
            if key not in current:
                return False
            if is_manifest_extension(value, current[key], path + (key,)):
                continue
            if path == ("code",) and is_verified_code_hash(key, current[key]):
                continue
            return False
        if path == ("code",):
            return all(key in existing or is_verified_code_hash(key, value)
                       for key, value in current.items())
        return True
    return existing == current


def is_verified_code_hash(relative_path: str, expected_hash: object) -> bool:
    source = ROOT / ".tools/source-packages/official-code" / relative_path
    return isinstance(expected_hash, str) and source.is_file() and sha256(source) == expected_hash


def main() -> None:
    parser = argparse.ArgumentParser(description="Restore pinned inventory UI data and sprites into Unity.")
    parser.add_argument("--check", action="store_true", help="verify the staged Unity payload without copying")
    args = parser.parse_args()

    manifest = json.loads(SOURCE_MANIFEST.read_text(encoding="utf-8"))
    quality_manifest = json.loads(QUALITY_MANIFEST.read_text(encoding="utf-8"))
    item_source_manifest = json.loads(ITEM_SOURCE_MANIFEST.read_text(encoding="utf-8"))
    output = manifest.get("output", {})
    if manifest.get("version") != "854_156383" or output.get("path") != "assets/official/bag-items.json":
        raise ValueError("Inventory source manifest is not pinned to the expected output and version.")

    item_source_output = "res://assets/official/gacha-item-sources/catalog.json"
    item_source_hash = item_source_manifest.get("outputs", {}).get(item_source_output, "")
    item_source_component = any(
        component.get("script") == "ItemSourceItem"
        and component.get("bundle") == "defaultpackage_assets_prefabs_ui_itemdetail.bundle"
        and component.get("bundle_hash") == "ed61998dccf8c8b2451eb16f49b433cb"
        for component in item_source_manifest.get("components", [])
    )
    if item_source_manifest.get("version") != "854_156383" or not item_source_component or not item_source_hash:
        raise ValueError("Gacha item-source template is not pinned to the expected official bundle and version.")

    data_path = UNITY_ROOT / "bag-items.json"
    sprites_root = ROOT / "assets/official/sprites"
    staged_sprites = UNITY_ROOT / "Art/sprites"
    source_data_hash = output.get("sha256", "")
    item_source_data_path = UNITY_ROOT / "item-source-catalog.json"
    item_source_staged_manifest = UNITY_ROOT / "item-source-manifest.json"
    if not args.check:
        copy_verified(SOURCE_DATA, data_path, source_data_hash)
        copy_inventory_manifest(SOURCE_MANIFEST, UNITY_ROOT / "source-manifest.json")
        copy_verified(ITEM_SOURCE_DATA, item_source_data_path, item_source_hash)
        copy_inventory_manifest(ITEM_SOURCE_MANIFEST, item_source_staged_manifest)
    elif not data_path.is_file() or sha256(data_path) != source_data_hash:
        raise ValueError(f"Unity inventory data is missing or changed: {data_path}")
    elif not item_source_data_path.is_file() or sha256(item_source_data_path) != item_source_hash:
        raise ValueError(f"Unity item-source catalog is missing or changed: {item_source_data_path}")
    elif not item_source_staged_manifest.is_file() or sha256(item_source_staged_manifest) != sha256(ITEM_SOURCE_MANIFEST):
        raise ValueError(f"Unity item-source manifest is missing or changed: {item_source_staged_manifest}")

    if (quality_manifest.get("manifest") != "854_156383"
            or quality_manifest.get("apk_sha256") != "ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9"
            or quality_manifest.get("bundle") != "defaultpackage_assets_image_ui_itemquality.bundle"):
        raise ValueError("Inventory quality background source is not pinned to the official APK.")
    extracted_bundle = ROOT / ".tools/source-packages/extracted/defaultpackage_assets_image_ui_itemquality.bundle"
    if not extracted_bundle.is_file() or sha256(extracted_bundle) != quality_manifest.get("bundle_sha256"):
        raise ValueError(f"Pinned official quality background bundle is missing or changed: {extracted_bundle}")
    quality_resources = quality_manifest.get("resources", {})
    quality_indices = quality_manifest.get("quality_indices", [])
    if len(quality_indices) != 8 or any(name not in quality_resources for name in quality_indices):
        raise ValueError("Official quality background table is incomplete.")
    for name, record in quality_resources.items():
        if Path(name).name != name:
            raise ValueError(f"Unexpected quality background filename: {name}")
        source = sprites_root / name
        destination = staged_sprites / name
        if args.check:
            if not destination.is_file() or sha256(destination) != record["sha256"]:
                raise ValueError(f"Unity inventory quality background is missing or changed: {destination}")
        else:
            copy_verified(source, destination, record["sha256"])

    for uri, record in manifest.get("resources", {}).items():
        prefix = "res://assets/official/sprites/"
        if not uri.startswith(prefix):
            continue
        name = uri[len(prefix):]
        if Path(name).name != name:
            raise ValueError(f"Unexpected inventory sprite URI: {uri}")
        source = sprites_root / name
        destination = staged_sprites / name
        if args.check:
            if not destination.is_file() or sha256(destination) != record["sha256"]:
                raise ValueError(f"Unity inventory sprite is missing or changed: {destination}")
        else:
            copy_verified(source, destination, record["sha256"])

    item_source_sprite_root = ROOT / "assets/official/gacha-item-sources/sprites"
    item_source_prefix = "res://assets/official/gacha-item-sources/sprites/"
    item_source_resources = item_source_manifest.get("resources", {})
    for uri, record in item_source_resources.items():
        if not uri.startswith(item_source_prefix):
            continue
        name = uri[len(item_source_prefix):]
        if Path(name).name != name:
            raise ValueError(f"Unexpected gacha item-source sprite URI: {uri}")
        source = item_source_sprite_root / name
        destination = staged_sprites / name
        if args.check:
            if not destination.is_file() or sha256(destination) != record["sha256"]:
                raise ValueError(f"Unity gacha item-source sprite is missing or changed: {destination}")
        else:
            copy_verified(source, destination, record["sha256"])

    print(f"Inventory payload {('verified' if args.check else 'restored')}: "
          f"{len(manifest.get('resources', {}))} inventory resources and "
          f"{len(quality_resources)} quality backgrounds, and "
          f"{len(item_source_resources)} gacha item-source sprites, {UNITY_ROOT}")


if __name__ == "__main__":
    main()
