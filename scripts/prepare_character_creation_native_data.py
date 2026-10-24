"""Restore pinned split Spine animations and appearance dye maps for Unity baking."""
from __future__ import annotations

import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import re
import struct
import sys
from official_source import OfficialSource

ROOT = Path(__file__).resolve().parents[1]
SOURCE = OfficialSource()
STAGE_ROOT = ROOT / 'output/current-871'
APK_SHA256 = "ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9"
EXTRACTED = ROOT / ".tools/source-packages/extracted"
PAYLOAD = ROOT / "unity/Assets/Official/CharacterCreation/Recovered/NativeCharacterData"
PAYLOAD_MANIFEST = PAYLOAD / "native-character-data.json"
REFERENCE_MANIFEST = ROOT / "assets/reference/character-creation/native-character-data-source-manifest.json"
SEXES = ("male", "female")
SPLIT_BUNDLES = {
    "male": "defaultpackage_assets_appearanceassets_character_model_male_1_splitanimations.bundle",
    "female": "defaultpackage_assets_appearanceassets_character_model_female_1_splitanimations.bundle",
}
DYE_BUNDLES = {
    "common": "defaultpackage_assets_appearanceassets_character_model_common_colormap_default.bundle",
    "male": "defaultpackage_assets_appearanceassets_character_model_male_1_colormap_default.bundle",
    "female": "defaultpackage_assets_appearanceassets_character_model_female_1_colormap_default.bundle",
}
SPLIT_CLASS = "SpineAnimationSplitBinarySO"
GUID_RE = re.compile(r"^[0-9a-fA-F]{32}$")


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def load_source_indexes() -> tuple[dict, dict]:
    return (read_json(EXTRACTED / "bundle-index.json"),
            read_json(EXTRACTED / "bundle-sources.json"))


def verify_bundle(path: Path, bundle_name: str, index: dict, sources: dict) -> dict:
    data = path.read_bytes()
    expected_md5 = index.get(bundle_name)
    source = sources.get(bundle_name)
    if not expected_md5 or hashlib.md5(data).hexdigest() != expected_md5:
        raise ValueError("Pinned bundle MD5 verification failed: " + bundle_name)
    if not source or source.get('manifest') != SOURCE.version:
        raise ValueError('Bundle source is not the selected official manifest: ' + bundle_name)
    if source.get('origin') == 'apk_zip':
        if source.get('apk_sha256') != APK_SHA256:
            raise ValueError('Unexpected native APK: ' + bundle_name)
    elif source.get('origin') == 'official_cdn':
        if not source.get('url') or source.get('bundle_sha256') != sha256(data):
            raise ValueError('Official CDN receipt mismatch: ' + bundle_name)
    else:
        raise ValueError('Unknown official source: ' + bundle_name)
    if source.get("bundle_hash") != expected_md5:
        raise ValueError("Bundle source receipt differs from the package index: " + bundle_name)
    return {"name": bundle_name, "md5": expected_md5, "sha256": sha256(data),
            "origin": source["origin"], "apk_sha256": APK_SHA256,
            'manifest': SOURCE.version, 'url': source.get('url')}


def verified_model_bundle(sex: str) -> tuple[Path, dict, list[Path]]:
    stage = STAGE_ROOT / (sex + '-inputs')
    manifest_path = stage / "source-manifest.json"
    manifest = read_json(manifest_path)
    if manifest.get("manifest") != SOURCE.version:
        raise ValueError("Staged model bundle is not pinned to the configured APK: " + sex)
    wanted = "_model_" + sex + "_1.bundle"
    matches = []
    staged_bundles = []
    for record in manifest.get("inputs", []):
        path = stage / record["output"]
        if not path.is_file() or sha256(path.read_bytes()) != record.get("decoded_sha256"):
            raise ValueError("Staged model bundle hash verification failed: " + str(path))
        if record.get('origin') == 'apk_zip' and record.get("apk_sha256") != APK_SHA256:
            raise ValueError("Staged model bundle came from another APK: " + str(path))
        staged_bundles.append(path)
        if wanted in record.get("name", ""):
            matches.append((path, record))
    if len(matches) != 1:
        raise ValueError("Expected one staged base model bundle for " + sex)
    path, record = matches[0]
    return path, {"name": record["name"], "sha256": record["decoded_sha256"],
                  "origin": record["origin"], "apk_sha256": APK_SHA256,
                  'manifest': SOURCE.version, 'url': record.get('url'),
                  "stage_manifest_sha256": sha256(manifest_path.read_bytes())}, staged_bundles


def read_aligned_string(data: bytes, offset: int) -> tuple[str, int]:
    if offset + 4 > len(data):
        raise ValueError("Split animation object ended inside a string length")
    length = struct.unpack_from("<i", data, offset)[0]
    offset += 4
    if length < 0 or offset + length > len(data):
        raise ValueError("Split animation object contains an invalid string length")
    value = data[offset:offset + length].decode("utf-8")
    offset += length
    offset = (offset + 3) & ~3
    return value, offset


def parse_split_binary_object(data: bytes) -> dict:
    offset = 28
    object_name, offset = read_aligned_string(data, offset)
    if offset + 4 > len(data):
        raise ValueError("Split animation object has no shared string count")
    string_count = struct.unpack_from("<i", data, offset)[0]
    offset += 4
    if string_count < 0 or string_count > 100000:
        raise ValueError("Split animation shared string count is invalid")
    strings = []
    for _ in range(string_count):
        value, offset = read_aligned_string(data, offset)
        strings.append(value)
    if offset + 4 > len(data):
        raise ValueError("Split animation object has no animation count")
    animation_count = struct.unpack_from("<i", data, offset)[0]
    offset += 4
    if animation_count < 0 or animation_count > 10000:
        raise ValueError("Split animation count is invalid")
    animations = []
    names = set()
    for _ in range(animation_count):
        content_name, offset = read_aligned_string(data, offset)
        asset_guid, offset = read_aligned_string(data, offset)
        if not content_name or not GUID_RE.fullmatch(asset_guid) or content_name in names:
            raise ValueError("Split animation record has an invalid name or asset GUID")
        names.add(content_name)
        animations.append({"name": content_name, "asset_guid": asset_guid.lower()})
    if offset + 4 > len(data):
        raise ValueError("Split animation object has no no-split count")
    no_split_count = struct.unpack_from("<i", data, offset)[0]
    offset += 4
    if no_split_count < 0 or no_split_count > 10000:
        raise ValueError("Split animation no-split count is invalid")
    no_split = []
    for _ in range(no_split_count):
        value, offset = read_aligned_string(data, offset)
        no_split.append(value)
    if offset != len(data):
        raise ValueError("Split animation object has unparsed trailing bytes")
    return {"name": object_name, "strings": strings, "animations": animations,
            "no_split_animations": no_split}


def find_split_object(model_bundles: list[Path], UnityPy) -> tuple[dict, dict]:
    environment = UnityPy.load(*[str(path) for path in model_bundles])
    matches = []
    for obj in environment.objects:
        if obj.type.name != "MonoBehaviour":
            continue
        behaviour = obj.read(check_read=False)
        script = getattr(behaviour, "m_Script", None)
        if script is None or script.read().m_ClassName != SPLIT_CLASS:
            continue
        matches.append((obj, behaviour))
    if len(matches) != 1:
        raise ValueError("Expected one official SpineAnimationSplitBinarySO, found " + str(len(matches)))
    obj, behaviour = matches[0]
    parsed = parse_split_binary_object(obj.get_raw_data())
    if parsed["name"] != behaviour.m_Name:
        raise ValueError("Split animation object's name differs from its serialized header")
    return parsed, {"asset_name": behaviour.m_Name, "path_id": str(obj.path_id)}


def load_text_assets(bundle_path: Path, UnityPy) -> dict[str, bytes]:
    environment = UnityPy.load(str(bundle_path))
    output = {}
    for obj in environment.objects:
        if obj.type.name != "TextAsset":
            continue
        text_asset = obj.read()
        source = text_asset.m_Script
        payload = source if isinstance(source, bytes) else source.encode("utf-8", "surrogateescape")
        name = read_spine_string(payload)
        if not name or not payload or name in output:
            raise ValueError("Split animation bundle contains an empty or duplicate animation payload")
        output[name] = payload
    return output


def read_spine_string(data: bytes) -> str:
    value = 0
    shift = 0
    offset = 0
    while True:
        if offset >= len(data) or shift >= 35:
            raise ValueError("Split animation has an invalid Spine string length")
        current = data[offset]
        offset += 1
        value |= (current & 0x7F) << shift
        if current < 0x80:
            break
        shift += 7
    if value < 2 or offset + value - 1 > len(data):
        raise ValueError("Split animation has an empty or truncated Spine string")
    return data[offset:offset + value - 1].decode("utf-8")


def append_output(outputs: list[dict], relative: str, data: bytes, kind: str) -> None:
    outputs.append({"path": relative, "bytes": len(data), "sha256": sha256(data), "kind": kind})


def collect_split_animations(sex: str, model_bundles: list[Path], split_bundle: Path, UnityPy,
                             outputs: list[dict]) -> tuple[dict, list[tuple[str, bytes]]]:
    split_info, object_info = find_split_object(model_bundles, UnityPy)
    text_assets = load_text_assets(split_bundle, UnityPy)
    expected_names = {item["name"] for item in split_info["animations"]}
    if expected_names != set(text_assets):
        missing = sorted(expected_names - set(text_assets))[:8]
        extra = sorted(set(text_assets) - expected_names)[:8]
        raise ValueError("Split animation assets do not match their official references; missing="
                         + repr(missing) + " extra=" + repr(extra))

    records = []
    files = []
    for item in split_info["animations"]:
        payload = text_assets[item["name"]]
        file_name = hashlib.sha256(item["name"].encode("utf-8")).hexdigest()[:24] + ".bytes"
        relative = "unity/Assets/Official/CharacterCreation/Recovered/NativeCharacterData/Animations/" \
            + sex.capitalize() + "/" + file_name
        append_output(outputs, relative, payload, "split-animation")
        files.append((relative, payload))
        records.append({"name": item["name"], "asset_guid": item["asset_guid"],
                        "file": "Animations/" + sex.capitalize() + "/" + file_name,
                        "sha256": sha256(payload)})

    split_json = {"sex": sex, "split_asset": object_info, "shared_strings": split_info["strings"],
                  "animations": records, "no_split_animations": split_info["no_split_animations"]}
    split_bytes = (json.dumps(split_json, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
    manifest_path = "unity/Assets/Official/CharacterCreation/Recovered/NativeCharacterData/Animations/" \
        + sex.capitalize() + ".json"
    append_output(outputs, manifest_path, split_bytes, "split-animation-index")
    files.append((manifest_path, split_bytes))
    return split_json, files


def collect_dye_maps(bundle_path: Path, bundle_name: str, group: str, UnityPy,
                     outputs: list[dict]) -> list[tuple[str, bytes]]:
    environment = UnityPy.load(str(bundle_path))
    paths = {}
    for container_path, pointer in environment.container.items():
        if pointer is not None:
            paths.setdefault(pointer.m_PathID, []).append(container_path)
    files = []
    seen_paths = set()
    for obj in environment.objects:
        if obj.type.name != "Texture2D":
            continue
        matches = [path for path in paths.get(obj.path_id, []) if "/ColorMap/" in path]
        if len(matches) != 1:
            raise ValueError("Official dye texture path is missing or ambiguous: " + str(obj.path_id))
        source_path = PurePosixPath(matches[0])
        suffix = PurePosixPath(*source_path.parts[source_path.parts.index("ColorMap") + 1:])
        if suffix in seen_paths:
            raise ValueError("Official dye texture path is duplicated: " + str(suffix))
        seen_paths.add(suffix)
        image = obj.read().image
        stream = io.BytesIO()
        image.save(stream, format="PNG")
        payload = stream.getvalue()
        relative = "unity/Assets/Official/CharacterCreation/Recovered/NativeCharacterData/DyeMaps/" \
            + group.capitalize() + "/" + suffix.as_posix()
        append_output(outputs, relative, payload, "dye-map")
        files.append((relative, payload))
    if not files:
        raise ValueError("Official color map bundle contains no Texture2D assets: " + bundle_name)
    return files


def write_checked(path: Path, data: bytes, expected_existing_hash: str | None) -> None:
    if path.exists():
        current = sha256(path.read_bytes())
        target = sha256(data)
        if current != target and current != expected_existing_hash:
            raise ValueError("Refusing to overwrite changed character creation payload: " + str(path))
        if current == target:
            return
        backup = ROOT / '.tools/source-packages/history/converted' / (current + '.bin')
        backup.parent.mkdir(parents=True, exist_ok=True)
        if not backup.exists():
            backup.write_bytes(path.read_bytes())
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_bytes(data)
    temporary.replace(path)


def build() -> dict:
    sys.path.insert(0, str(ROOT / ".tools/character-art-python"))
    try:
        import UnityPy
    except ImportError as error:
        raise RuntimeError("Install UnityPy==1.25.4 in .tools/character-art-python") from error
    index, sources = load_source_indexes()
    source_bundles = []
    payload_files: list[tuple[str, bytes]] = []
    outputs: list[dict] = []
    sexes = {}

    for sex in SEXES:
        model_bundle, model_source, model_bundles = verified_model_bundle(sex)
        split_name = SPLIT_BUNDLES[sex]
        split_path = EXTRACTED / split_name
        split_source = verify_bundle(split_path, split_name, index, sources)
        split_data, split_files = collect_split_animations(sex, model_bundles, split_path, UnityPy, outputs)
        payload_files.extend(split_files)
        source_bundles.extend([model_source, split_source])
        sexes[sex] = {"model_bundle": model_source, "split_bundle": split_source,
                      "split_animation_count": len(split_data["animations"]),
                      "no_split_animations": split_data["no_split_animations"],
                      "animation_index": "Animations/" + sex.capitalize() + ".json"}

    dye_groups = {}
    for group, bundle_name in DYE_BUNDLES.items():
        bundle_path = EXTRACTED / bundle_name
        bundle_source = verify_bundle(bundle_path, bundle_name, index, sources)
        dye_files = collect_dye_maps(bundle_path, bundle_name, group, UnityPy, outputs)
        payload_files.extend(dye_files)
        source_bundles.append(bundle_source)
        dye_groups[group] = {"bundle": bundle_name, "texture_count": len(dye_files),
                             "directory": "DyeMaps/" + group.capitalize()}

    if len({item["path"] for item in outputs}) != len(outputs):
        raise ValueError("Character creation output paths are duplicated")
    manifest = {"manifest": SOURCE.version, "apk_sha256": APK_SHA256,
                "source_bundles": sorted(source_bundles, key=lambda item: item["name"]),
                "sexes": sexes, "dye_maps": dye_groups,
                "outputs": sorted(outputs, key=lambda item: item["path"])}
    encoded = (json.dumps(manifest, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")

    previous_data = REFERENCE_MANIFEST.read_bytes() if REFERENCE_MANIFEST.is_file() else None
    previous = json.loads(previous_data.decode("utf-8")) if previous_data else None
    prior_hashes = {}
    if previous is not None:
        if previous.get('manifest') not in (SOURCE.version, SOURCE.apk_version) or previous.get("apk_sha256") != APK_SHA256:
            raise ValueError("Existing native character source manifest uses another version")
        prior_hashes = {entry["path"]: entry["sha256"] for entry in previous.get("outputs", [])}
        for relative, _ in payload_files:
            current_path = ROOT / relative
            prior_hash = prior_hashes.get(relative)
            if current_path.is_file() and prior_hash and sha256(current_path.read_bytes()) != prior_hash:
                raise ValueError("Existing native character payload changed since its source manifest: "
                                 + relative)
    if PAYLOAD_MANIFEST.is_file() and previous_data and PAYLOAD_MANIFEST.read_bytes() != previous_data:
        raise ValueError("Native character payload and reference manifests disagree")

    for relative, data in payload_files:
        write_checked(ROOT / relative, data, prior_hashes.get(relative))
    previous_manifest_hash = sha256(previous_data) if previous_data else None
    write_checked(PAYLOAD_MANIFEST, encoded, previous_manifest_hash)
    write_checked(REFERENCE_MANIFEST, encoded, previous_manifest_hash)
    return {"animations": {sex: sexes[sex]["split_animation_count"] for sex in SEXES},
            "dye_maps": {group: dye_groups[group]["texture_count"] for group in dye_groups},
            "outputs": len(outputs), "manifest": REFERENCE_MANIFEST.relative_to(ROOT).as_posix()}


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest', help='Explicit source profile, default active')
    parser.add_argument('--stage-root', type=Path, default=STAGE_ROOT)
    args = parser.parse_args()
    SOURCE = OfficialSource(args.manifest)
    STAGE_ROOT = args.stage_root.resolve()
    print(json.dumps(build(), ensure_ascii=False, sort_keys=True))
