"""Stage pinned official pet Spine inputs and record their source hashes."""
import hashlib
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MODEL_IDS = {
    1600: 9201, 1601: 9202, 1602: 9203, 1603: 9204,
    1604: 9205, 1605: 9206, 1607: 9208, 1608: 9209,
}
EXPORT_ROOTS = (
    ROOT / "output/pet-gacha-model-native-all/ExportedProject/Assets",
    ROOT / "output/pet-gacha-model-native/ExportedProject/Assets",
)
ASSET_ROOT = ROOT / "unity/Assets"
PAYLOAD_ROOT = ASSET_ROOT / "Official/PetGacha/NativeSource"
MANIFEST_PATH = ROOT / "assets/reference/pet-gacha/native-model-source-manifest.json"
PET_MODELS = ROOT / "assets/official/pet-models.json"
PET_RUNTIME = ROOT / "unity/Assets/Game/Resources/PetGacha/pet-gacha-runtime.json"
INVENTORY = ROOT / ".tools/source-packages/cloud-research/bundle-inventory.json"


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def stable_guid(relative):
    return hashlib.md5(("zjcs:" + relative.replace("\\", "/").lower()).encode("utf-8")).hexdigest()


def write_folder_meta(path):
    relative = path.relative_to(ASSET_ROOT).as_posix()
    meta = Path(str(path) + ".meta")
    body = ("fileFormatVersion: 2\n"
            "guid: " + stable_guid(relative) + "\n"
            "folderAsset: yes\nDefaultImporter:\n"
            "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    meta.write_text(body, encoding="utf-8", newline="\n")


def copy_with_meta(source, target):
    target.parent.mkdir(parents=True, exist_ok=True)
    source_meta = Path(str(source) + ".meta")
    if not source.is_file() or not source_meta.is_file():
        raise FileNotFoundError("AssetRipper output or importer metadata is missing: " + str(source))
    target.write_bytes(source.read_bytes())
    relative = target.relative_to(ASSET_ROOT).as_posix()
    meta_text = source_meta.read_text(encoding="utf-8")
    meta_text, replaced = re.subn(r"(?m)^guid: [0-9a-f]{32}$",
                                  "guid: " + stable_guid(relative), meta_text, count=1)
    if replaced != 1:
        raise ValueError("AssetRipper importer metadata has no unique GUID: " + str(source_meta))
    Path(str(target) + ".meta").write_text(meta_text, encoding="utf-8", newline="\n")
    return {
        "path": target.relative_to(ROOT).as_posix(), "bytes": target.stat().st_size,
        "sha256": sha256(target), "meta_sha256": sha256(Path(str(target) + ".meta")),
    }


def main():
    runtime = json.loads(PET_RUNTIME.read_text(encoding="utf-8"))
    profiles = {row["ClassId"]: row for row in runtime.get("PetProfiles", [])}
    if set(profiles) != set(MODEL_IDS):
        raise ValueError("The runtime pet profiles do not match the pinned gacha candidates.")
    bundle_rows = {row["name"]: row for row in json.loads(INVENTORY.read_text(encoding="utf-8"))["bundles"]}
    input_records = []
    model_records = []
    created_directories = [ASSET_ROOT / "Official", ASSET_ROOT / "Official/PetGacha", PAYLOAD_ROOT]

    for pet_id, model_id in MODEL_IDS.items():
        stem = "pet_" + str(model_id)
        source_root = next((root / "AppearanceAssets/Pet" / stem for root in EXPORT_ROOTS
                            if (root / "AppearanceAssets/Pet" / stem / (stem + ".skel.bytes")).is_file()), None)
        if source_root is None:
            raise FileNotFoundError("Exported official pet model inputs are missing: " + stem)

        bundle_names = (
            "defaultpackage_assets_prefabs_appearance_pet_" + str(model_id) + ".bundle",
            "defaultpackage_assets_appearanceassets_pet_pet_" + str(model_id) + ".bundle",
        )
        for name in bundle_names:
            row = bundle_rows.get(name)
            if row is None:
                raise ValueError("Pinned source bundle is missing from the inventory: " + name)
            input_records.append({"name": name, "md5": row["hash"], "size": row["size"]})

        target_dir = PAYLOAD_ROOT / str(model_id)
        created_directories.append(target_dir)
        files = [copy_with_meta(source_root / (stem + suffix), target_dir / (stem + suffix))
                 for suffix in (".skel.bytes", ".atlas.txt", ".png")]
        model_records.append({
            "pet_class_id": pet_id, "ui_model": model_id,
            "support_skill_ids": profiles[pet_id]["SupportSkillIds"],
            "source_bundles": list(bundle_names), "files": files,
        })

    for directory in created_directories:
        if directory.exists() and not Path(str(directory) + ".meta").is_file():
            write_folder_meta(directory)

    manifest = {
        "manifest": runtime["Manifest"], "generator": "scripts/prepare_pet_gacha_models.py",
        "asset_converter": "AssetRipper 1.3.14; output from verified 854_156383 Android bundles",
        "inputs": {
            PET_MODELS.relative_to(ROOT).as_posix(): sha256(PET_MODELS),
            PET_RUNTIME.relative_to(ROOT).as_posix(): sha256(PET_RUNTIME),
        },
        "source_bundles": input_records,
        "models": model_records,
    }
    MANIFEST_PATH.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST_PATH.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
                             encoding="utf-8", newline="\n")
    print("Prepared", len(model_records), "official pet model sources with",
          sum(len(record["files"]) for record in model_records), "Unity inputs.")


if __name__ == "__main__":
    main()
