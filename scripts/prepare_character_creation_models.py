"""Install source-verified male and female character model bundles for native baking."""
from __future__ import annotations

import hashlib
import json
import re
import shutil
import uuid
from pathlib import Path
from official_source import OfficialSource

ROOT = Path(__file__).resolve().parents[1]
SOURCE = OfficialSource()
STAGE_ROOT = ROOT / 'output/current-871'
PENDING_WRITES = {}
PAYLOAD = ROOT / "unity/Assets/Official/CharacterCreation/Recovered/CharacterModels"
REFERENCE = ROOT / "assets/reference/character-creation/character-model-source-manifest.json"
PAYLOAD_MANIFEST = PAYLOAD / "character-model-source-manifest.json"
GUID_PATTERN = re.compile(r"guid:\s*([0-9a-fA-F]{32})")
APK_SHA256 = "ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9"
SCRIPT_BINDINGS = {
    "Scripts/spine-unity/Spine/Unity/SkeletonDataAsset.cs":
        "unity/Assets/Spine/Runtime/spine-unity/Asset Types/SkeletonDataAsset.cs.meta",
    "Scripts/spine-unity/Spine/Unity/SpineAtlasAsset.cs":
        "unity/Assets/Spine/Runtime/spine-unity/Asset Types/SpineAtlasAsset.cs.meta",
}


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def file_sha256(path: Path) -> str:
    return sha256(path.read_bytes())


def guid_for(path: Path) -> str | None:
    if not path.is_file():
        return None
    match = GUID_PATTERN.search(path.read_text(encoding="utf-8", errors="ignore"))
    return match.group(1).lower() if match else None


def source_guid_index(assets: Path) -> dict[str, Path]:
    result = {}
    for meta in assets.rglob("*.meta"):
        guid = guid_for(meta)
        if guid is None:
            continue
        if guid in result and result[guid] != meta:
            raise ValueError("AssetRipper output contains duplicate source GUID " + guid)
        result[guid] = meta.with_suffix("")
    return result


def referenced_guids(path: Path) -> set[str]:
    if path.suffix == ".meta":
        return set()
    try:
        text = path.read_text(encoding="utf-8", errors="ignore")
    except OSError:
        return set()
    return {value.lower() for value in GUID_PATTERN.findall(text)}


def verified_inputs(sex: str) -> dict:
    stage = STAGE_ROOT / (sex + '-inputs')
    manifest_path = stage / "source-manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if manifest.get("manifest") != SOURCE.version:
        raise ValueError("Character model inputs are not pinned to 854_156383: " + sex)
    if not any(("_model_" + sex + "_1.bundle") in name for name in manifest.get("requested_bundles", [])):
        raise ValueError("The pinned " + sex + " model bundle is absent from the staged input manifest")

    inputs = []
    for record in manifest.get("inputs", []):
        source = stage / record["output"]
        if not source.is_file() or file_sha256(source) != record.get("decoded_sha256"):
            raise ValueError("Staged model input failed its recorded SHA-256: " + str(source))
        if record.get('origin') == 'apk_zip' and record.get("apk_sha256") != APK_SHA256:
            raise ValueError("Staged model input came from an unexpected APK")
        inputs.append({"name": record["name"], "origin": record["origin"],
                       "sha256": record["decoded_sha256"]})
    return {"manifest": SOURCE.version, "source_manifest_sha256": file_sha256(manifest_path),
            "requested_bundles": manifest["requested_bundles"], "inputs": inputs}


def target_for(sex: str, source: Path, assets: Path) -> Path:
    relative = source.relative_to(assets)
    target = PAYLOAD / "Recovered" / sex.capitalize() / relative
    if '/Common/' in '/' + relative.as_posix() and not target.exists():
        other = PAYLOAD / 'Recovered' / ('Female' if sex == 'male' else 'Male') / relative
        if other.exists():
            return other
    return target


def is_script_dependency(path: Path, assets: Path) -> bool:
    relative = path.relative_to(assets).as_posix()
    return relative.startswith("Scripts/") or relative.startswith("ScriptAssemblies/")


def ensure_folder_meta(target: Path) -> None:
    target_meta = Path(str(target) + ".meta")
    target_meta.parent.mkdir(parents=True, exist_ok=True)
    if target_meta.exists():
        return
    relative = target.relative_to(PAYLOAD).as_posix()
    guid = uuid.uuid5(uuid.NAMESPACE_URL, "zjcs/CharacterCreation/CharacterModels/" + relative).hex
    data = ("fileFormatVersion: 2\nguid: " + guid
            + "\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode("utf-8")
    target_meta.write_bytes(data)


def install_asset(source: Path, target: Path, outputs: dict[str, dict],
                  by_guid: dict[str, tuple[str, str, str]], script_remaps: dict[str, str],
                  prior_outputs: dict[str, dict]) -> None:
    source_meta = Path(str(source) + ".meta")
    if not source_meta.is_file():
        raise ValueError("AssetRipper character model asset has no Unity meta: " + str(source))
    data = source.read_bytes()
    meta_data = source_meta.read_bytes()
    source_guid = guid_for(source_meta)
    guid = script_remaps.get(source_guid, source_guid)
    if guid is None:
        raise ValueError("AssetRipper character model meta has no GUID: " + str(source_meta))
    source_digest = sha256(data)
    meta_digest = sha256(meta_data)
    relative = target.relative_to(ROOT / "unity").as_posix()

    if data.startswith(b'%YAML') and script_remaps:
        text = data.decode('utf-8')
        text = GUID_PATTERN.sub(lambda match: match[0].replace(match[1], script_remaps.get(match[1].lower(), match[1])), text)
        data = text.encode('utf-8')
    target_meta = Path(str(target) + '.meta')
    if target_meta.exists():
        # Keep the current importer bytes, including legitimate Editor changes.
        meta_data = target_meta.read_bytes()
    meta_digest = sha256(meta_data)
    digest = sha256(data)

    previous = by_guid.get(guid)
    if previous is not None:
        previous_path, previous_source_digest, previous_meta_digest = previous
        if previous_source_digest != digest or previous_meta_digest != meta_digest:
            raise ValueError("A source GUID resolves to conflicting male/female assets: " + guid)
        return
    if target.exists() and file_sha256(target) != digest:
        previous_output = prior_outputs.get(relative)
        if previous_output is None or file_sha256(target) != previous_output.get("sha256"):
            raise ValueError("Refusing to overwrite changed character model asset: " + str(target))
    if not target.exists() or file_sha256(target) != digest:
        PENDING_WRITES[target] = data
    if not target_meta.exists():
        PENDING_WRITES[target_meta] = meta_data
    by_guid[guid] = (relative, digest, meta_digest)
    outputs[relative] = {"path": relative, "source_sha256": source_digest,
                         "sha256": digest, "meta_sha256": meta_digest}


def collect_project(sex: str, project: Path, outputs: dict[str, dict], by_guid: dict,
                    excluded_scripts: dict[str, str], script_remaps: dict[str, str],
                    prior_outputs: dict[str, dict]) -> None:
    assets = project / "Assets"
    model_root = assets / "AppearanceAssets/Character/Model"
    sex_root = model_root / ("Male_1" if sex == "male" else "Female_1")
    common_root = model_root / "Common"
    if not sex_root.is_dir() or not common_root.is_dir():
        raise FileNotFoundError("AssetRipper model or shared Atlas export is missing for " + sex)
    source_index = source_guid_index(assets)
    pending = [path for root in (sex_root, common_root) for path in root.rglob("*") if path.is_file()]
    selected = set()

    while pending:
        source = pending.pop()
        if source in selected or source.suffix == ".meta":
            continue
        selected.add(source)
        for guid in referenced_guids(source):
            dependency = source_index.get(guid)
            if dependency is None or dependency in selected:
                continue
            if is_script_dependency(dependency, assets):
                source_path = dependency.relative_to(assets).as_posix()
                excluded_scripts[guid] = source_path
                project_script = SCRIPT_BINDINGS.get(source_path)
                if project_script is not None:
                    local_guid = guid_for(ROOT / project_script)
                    if local_guid is None:
                        raise ValueError("The matching local Spine runtime script has no Unity GUID: " + project_script)
                    previous_guid = script_remaps.get(guid)
                    if previous_guid is not None and previous_guid != local_guid:
                        raise ValueError("A Spine script GUID maps to conflicting Unity runtime scripts: " + guid)
                    script_remaps[guid] = local_guid
                continue
            if dependency.is_file():
                pending.append(dependency)

    for source in sorted(selected):
        target = target_for(sex, source, assets)
        parent = source.parent
        while parent != assets and assets in parent.parents:
            target_parent = target_for(sex, parent / "__directory_probe__", assets).parent
            ensure_folder_meta(target_parent)
            parent = parent.parent
        install_asset(source, target, outputs, by_guid, script_remaps, prior_outputs)


def ensure_payload_meta() -> None:
    PAYLOAD.mkdir(parents=True, exist_ok=True)
    meta = Path(str(PAYLOAD) + ".meta")
    if meta.exists():
        return
    guid = uuid.uuid5(uuid.NAMESPACE_URL, "zjcs/CharacterCreation/CharacterModels").hex
    data = ("fileFormatVersion: 2\nguid: " + guid
            + "\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode("utf-8")
    meta.write_bytes(data)


def prior_generated_outputs(source_inputs: dict) -> tuple[dict[str, dict], bool]:
    if not REFERENCE.exists() and not PAYLOAD_MANIFEST.exists():
        return {}, False
    if not REFERENCE.is_file() or not PAYLOAD_MANIFEST.is_file():
        raise ValueError("Only one character model source manifest exists; refusing an incomplete update")
    reference_data = REFERENCE.read_bytes()
    payload_data = PAYLOAD_MANIFEST.read_bytes()
    if reference_data != payload_data:
        raise ValueError("Character model source manifests disagree; refusing to update either copy")
    previous = json.loads(reference_data.decode("utf-8"))
    if previous.get('manifest') not in (SOURCE.version, SOURCE.apk_version):
        raise ValueError('Character model history has an unknown source version')
    if previous.get('manifest') == SOURCE.version and previous.get("source", {}).get("models") != source_inputs:
        raise ValueError("Character model source manifest is not for the current pinned inputs")
    records = previous.get("outputs")
    if not isinstance(records, list) or not records:
        raise ValueError("Character model source manifest has no output inventory")
    result = {}
    for record in records:
        relative = record.get("path", "")
        if not relative.startswith("Assets/Official/CharacterCreation/Recovered/CharacterModels/"):
            raise ValueError("Character model source manifest contains an unexpected output path")
        target = ROOT / "unity" / Path(*relative.split("/"))
        if not target.is_file():
            raise ValueError('Missing previous character model asset: ' + relative)
        result[relative] = record
    return result, True


def build() -> dict:
    PENDING_WRITES.clear()
    ensure_payload_meta()
    for sex in ("Male", "Female"):
        ensure_folder_meta(PAYLOAD / "Recovered" / sex)
    source_inputs = {sex: verified_inputs(sex) for sex in ("male", "female")}
    prior_outputs, prior_manifest_valid = prior_generated_outputs(source_inputs)
    outputs = {}
    by_guid = {}
    excluded_scripts = {}
    script_remaps = {}
    # AssetRipper assigns fresh GUIDs on each export. Resolve every source GUID
    # to its existing Unity asset before writing YAML, preserving all callers.
    for sex in ('male', 'female'):
        assets = STAGE_ROOT / (sex + '-ripper') / 'ExportedProject/Assets'
        for source_meta in assets.rglob('*.meta'):
            target = target_for(sex, source_meta.with_suffix(''), assets)
            existing_guid = guid_for(Path(str(target) + '.meta'))
            source_guid = guid_for(source_meta)
            if source_guid and existing_guid:
                script_remaps[source_guid] = existing_guid
    for sex in ("male", "female"):
        project = STAGE_ROOT / (sex + '-ripper') / 'ExportedProject'
        collect_project(sex, project, outputs, by_guid, excluded_scripts, script_remaps, prior_outputs)

    sorted_outputs = sorted(outputs.values(), key=lambda item: item["path"])
    manifest = {
        "manifest": SOURCE.version,
        "source": {"asset_ripper": "1.3.14", "models": source_inputs},
        "script_guid_bindings": [
            {"source_guid": guid, "project_guid": project_guid}
            for guid, project_guid in sorted(script_remaps.items()) if guid in excluded_scripts
        ],
        'asset_guid_bindings': [{'source_guid': guid, 'project_guid': mapped}
                                for guid, mapped in sorted(script_remaps.items()) if guid not in excluded_scripts],
        "excluded_scripts": [{"guid": guid, "path": path}
                             for guid, path in sorted(excluded_scripts.items())],
        "outputs": sorted_outputs
    }
    encoded = (json.dumps(manifest, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
    # All source files, references and overwrite ownership have now been checked.
    for target, payload in PENDING_WRITES.items():
        if target.exists():
            backup = ROOT / '.tools/source-packages/history/converted' / (file_sha256(target) + '.bin')
            backup.parent.mkdir(parents=True, exist_ok=True)
            if not backup.exists():
                backup.write_bytes(target.read_bytes())
        target.parent.mkdir(parents=True, exist_ok=True)
        temporary = target.with_name(target.name + '.tmp')
        temporary.write_bytes(payload)
        temporary.replace(target)
    for target in (REFERENCE, PAYLOAD_MANIFEST):
        if target.exists() and target.read_bytes() != encoded and not prior_manifest_valid:
            raise ValueError("Refusing to overwrite changed character model source manifest: " + str(target))
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists() or target.read_bytes() != encoded:
            if target.exists():
                backup = ROOT / '.tools/source-packages/history/converted' / (file_sha256(target) + '.bin')
                backup.parent.mkdir(parents=True, exist_ok=True)
                if not backup.exists():
                    backup.write_bytes(target.read_bytes())
            target.write_bytes(encoded)
    return {"assets": len(outputs), "excluded_scripts": len(excluded_scripts),
            "manifest": REFERENCE.relative_to(ROOT).as_posix()}


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest')
    parser.add_argument('--stage-root', type=Path, default=STAGE_ROOT)
    args = parser.parse_args()
    SOURCE = OfficialSource(args.manifest)
    STAGE_ROOT = args.stage_root.resolve()
    print(json.dumps(build(), ensure_ascii=False, sort_keys=True))
