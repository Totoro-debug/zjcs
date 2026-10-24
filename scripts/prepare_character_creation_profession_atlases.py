"""Install official profession atlas pages used by the creation preview."""
from __future__ import annotations

import hashlib
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
APK_SHA256 = "ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9"
INDEX_PATH = ROOT / ".tools/source-packages/extracted/bundle-index.json"
RECEIPTS_PATH = ROOT / ".tools/source-packages/cloud/854_156383/receipts.jsonl"
PAYLOAD = ROOT / "unity/Assets/Official/CharacterCreation/Recovered/CharacterModels"
PAYLOAD_MANIFEST = PAYLOAD / "profession-atlas-source-manifest.json"
REFERENCE_MANIFEST = ROOT / "assets/reference/character-creation/profession-atlas-source-manifest.json"

SEXES = {
    "male": {
        "stage": ROOT / "output/character-creation-male-profession-inputs",
        "project": ROOT / "output/character-creation-male-profession-ripper/ExportedProject/Assets/AppearanceAssets/Character/Model/Male_1/Atlas/High",
        "bundle": "defaultpackage_assets_appearanceassets_character_model_male_1_atlas_profession.bundle",
        "folder": "Male/AppearanceAssets/Character/Model/Male_1/Atlas/High",
    },
    "female": {
        "stage": ROOT / "output/character-creation-female-profession-inputs",
        "project": ROOT / "output/character-creation-female-profession-ripper/ExportedProject/Assets/AppearanceAssets/Character/Model/Female_1/Atlas/High",
        "bundle": "defaultpackage_assets_appearanceassets_character_model_female_1_atlas_profession.bundle",
        "folder": "Female/AppearanceAssets/Character/Model/Female_1/Atlas/High",
    },
}


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def verify_input(sex: str, config: dict, bundle_index: dict, receipts: dict) -> dict:
    stage_manifest_path = config["stage"] / "source-manifest.json"
    stage_manifest_bytes = stage_manifest_path.read_bytes()
    stage_manifest = json.loads(stage_manifest_bytes.decode("utf-8"))
    if stage_manifest.get("manifest") != "854_156383" or stage_manifest.get("requested_bundles") != [config["bundle"]]:
        raise ValueError("Profession atlas input is not pinned to the expected original bundle: " + sex)
    if len(stage_manifest.get("inputs", [])) != 1:
        raise ValueError("Profession atlas input closure is not the expected single bundle: " + sex)

    record = stage_manifest["inputs"][0]
    path = config["stage"] / record["output"]
    expected_md5 = bundle_index.get(config["bundle"])
    data = path.read_bytes()
    if record.get("name") != config["bundle"]:
        raise ValueError("Profession atlas bundle source record has the wrong name: " + sex)
    if expected_md5 != record.get("raw_md5") or hashlib.md5(data).hexdigest() != expected_md5:
        raise ValueError("Profession atlas bundle MD5 differs from the pinned manifest: " + sex)
    if digest(data) != record.get("decoded_sha256"):
        raise ValueError("Decoded profession atlas input hash differs from its source record: " + sex)
    origin = record.get("origin")
    if origin == "apk_zip":
        if record.get("apk_sha256") != APK_SHA256:
            raise ValueError("Profession atlas bundle does not match the pinned APK: " + sex)
    elif origin == "official_cdn":
        receipt = receipts.get(config["bundle"])
        if (receipt is None or receipt.get("status") != "downloaded"
                or receipt.get("md5") != expected_md5
                or receipt.get("sha256") != record.get("raw_sha256")
                or receipt.get("size") != record.get("raw_size")
                or receipt.get("url") != record.get("url")):
            raise ValueError("Profession atlas CDN source does not match its pinned download receipt: " + sex)
    else:
        raise ValueError("Profession atlas bundle has an unrecognized source: " + sex)

    serialized = stage_manifest.get("serialized_files", [])
    if len(serialized) != 1 or serialized[0].get("unity_version") != "2022.3.57f1":
        raise ValueError("Profession atlas was not decoded from the pinned Unity version: " + sex)
    return {
        "sex": sex,
        "bundle": config["bundle"],
        "md5": expected_md5,
        "sha256": record["raw_sha256"],
        "decoded_sha256": record["decoded_sha256"],
        "origin": origin,
        "url": record.get("url", ""),
        "apk_sha256": record.get("apk_sha256", ""),
        "stage_manifest_sha256": digest(stage_manifest_bytes),
        "unity_version": serialized[0]["unity_version"],
    }


def previous_outputs() -> dict[str, dict]:
    if not REFERENCE_MANIFEST.exists() and not PAYLOAD_MANIFEST.exists():
        return {}
    if not REFERENCE_MANIFEST.is_file() or not PAYLOAD_MANIFEST.is_file():
        raise ValueError("Only one profession atlas source manifest exists")
    reference = REFERENCE_MANIFEST.read_bytes()
    if reference != PAYLOAD_MANIFEST.read_bytes():
        raise ValueError("Profession atlas source manifests disagree")
    manifest = json.loads(reference.decode("utf-8"))
    outputs = manifest.get("outputs")
    if manifest.get("manifest") != "854_156383" or not isinstance(outputs, list):
        raise ValueError("Existing profession atlas source manifest is invalid")
    for item in outputs:
        relative = item.get("path", "")
        if not relative.startswith("Assets/Official/CharacterCreation/Recovered/CharacterModels/Recovered/"):
            raise ValueError("Existing profession atlas manifest has an unexpected path")
        target = ROOT / "unity" / Path(*relative.split("/"))
        meta = Path(str(target) + ".meta")
        if not target.is_file() or digest(target.read_bytes()) != item.get("sha256"):
            raise ValueError("Existing profession atlas output differs from its manifest: " + relative)
        if not meta.is_file() or digest(meta.read_bytes()) != item.get("meta_sha256"):
            raise ValueError("Existing profession atlas metadata differs from its manifest: " + relative)
    return {item["path"]: item for item in outputs}


def build() -> dict:
    bundle_index = json.loads(INDEX_PATH.read_text(encoding="utf-8"))
    receipts = {}
    for line in RECEIPTS_PATH.read_text(encoding="utf-8").splitlines():
        record = json.loads(line)
        if record.get("name") in {config["bundle"] for config in SEXES.values()}:
            receipts[record["name"]] = record
    old_outputs = previous_outputs()
    inputs = [verify_input(sex, config, bundle_index, receipts) for sex, config in SEXES.items()]
    outputs: dict[str, dict] = {}
    pending: list[tuple[Path, Path, bytes, bytes, dict]] = []

    for sex, config in SEXES.items():
        source_root = config["project"]
        pages = sorted(source_root.glob("*.png"))
        if not pages:
            raise FileNotFoundError("AssetRipper profession atlas PNG output is missing: " + str(source_root))
        for source in pages:
            source_meta = Path(str(source) + ".meta")
            if not source_meta.is_file():
                raise FileNotFoundError("AssetRipper profession atlas page metadata is missing: " + str(source_meta))
            target = PAYLOAD / "Recovered" / config["folder"] / source.name
            target_meta = Path(str(target) + ".meta")
            data = source.read_bytes()
            meta_data = source_meta.read_bytes()
            relative = target.relative_to(ROOT / "unity").as_posix()
            item = {"path": relative, "bytes": len(data), "sha256": digest(data),
                    "meta_sha256": digest(meta_data), "source_bundle": config["bundle"],
                    "source_object": source.stem}
            for path, expected in ((target, item["sha256"]), (target_meta, item["meta_sha256"])):
                if path.exists() and digest(path.read_bytes()) != expected:
                    old = old_outputs.get(relative)
                    old_hash = old.get("sha256" if path == target else "meta_sha256") if old else None
                    if old_hash != digest(path.read_bytes()):
                        raise ValueError("Refusing to overwrite changed character atlas asset: " + str(path))
            outputs[relative] = item
            pending.append((source, target, data, meta_data, item))

    manifest = {
        "manifest": "854_156383",
        "asset_ripper": "1.3.14",
        "inputs": inputs,
        "outputs": sorted(outputs.values(), key=lambda item: item["path"]),
    }
    encoded = (json.dumps(manifest, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
    for source, target, data, meta_data, _ in pending:
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists():
            shutil.copy2(source, target)
        target_meta = Path(str(target) + ".meta")
        if not target_meta.exists():
            shutil.copy2(Path(str(source) + ".meta"), target_meta)
    for manifest_path in (REFERENCE_MANIFEST, PAYLOAD_MANIFEST):
        manifest_path.parent.mkdir(parents=True, exist_ok=True)
        manifest_path.write_bytes(encoded)
    return {"manifest": REFERENCE_MANIFEST.relative_to(ROOT).as_posix(),
            "outputs": len(outputs), "inputs": len(inputs)}


if __name__ == "__main__":
    print(json.dumps(build(), ensure_ascii=False, sort_keys=True))
