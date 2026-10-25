"""Bind official battle-status EntryInfo sprites to local Unity Resources."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import struct
import tempfile
import zipfile

import xxhash

from official_apk import Apk, ROOT
from official_tables import Constructors


VERSION = "854_156383"
ENTRY_ASSET = "entry.g"
RULES_PATH = Path("assets/official/battle-rules.json")
GAME_RULES_PATH = Path("assets/official/rules.json")
HUD_SOURCE_MANIFEST = Path("assets/reference/battle-hud/source-manifest.json")
LANGUAGE_SOURCE_MANIFEST = Path("assets/reference/official-map/localization-source-manifest.json")
PINNED_APK_PATH = Path(".tools/source-packages/zjcs_M509085.apk")
LANGUAGE_INPUTS = (
    "wwisebnk_assets_launch_config_binary_language_zh_cn_text_g_bytes.rawfile",
    "wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile",
)
OUTPUT_PATH = Path("unity/Assets/Game/Resources/Combat/battle-status-icons.json")
SOURCE_MANIFEST_PATH = Path("assets/reference/battle-hud/status-icons-source-manifest.json")
SPRITE_ROOT = Path("assets/official/battle-hud/sprites")


def sha256_bytes(value):
    return hashlib.sha256(value).hexdigest()


def sha256_file(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def resolve_sprite_file(resource_key, sprite_manifest, root=ROOT):
    if not resource_key:
        return ""
    object_name = resource_key.rsplit("/", 1)[-1]
    matches = [(path, item) for path, item in sprite_manifest.items()
               if item.get("object") == object_name]
    if len(matches) != 1:
        raise ValueError(f"Expected one official HUD sprite for {resource_key}, found {len(matches)}")

    resource_path, record = matches[0]
    prefix = "res://"
    if not resource_path.startswith(prefix):
        raise ValueError("Unexpected HUD sprite resource path: " + resource_path)
    relative = PurePosixPath(resource_path[len(prefix):])
    if relative.parts[:3] != ("assets", "official", "battle-hud") or relative.parts[3:4] != ("sprites",):
        raise ValueError("HUD sprite is outside the official battle HUD sprite directory: " + resource_path)
    path = root.joinpath(*relative.parts)
    if not path.is_file() or sha256_file(path) != record.get("sha256"):
        raise ValueError("Official HUD sprite hash mismatch: " + str(path))
    return relative.name


def build_status_bindings(entities, entry_rows, sprite_manifest, root=ROOT, sprite_resolver=None,
                          display_flags=None, status_texts=None):
    resolve = sprite_resolver or resolve_sprite_file
    entries_by_id = {}
    for row in entry_rows:
        status_id = int(row["Id"])
        if status_id in entries_by_id:
            raise ValueError("Duplicate official EntryInfo ID: " + str(status_id))
        entries_by_id[status_id] = row

    status_entities = []
    for raw_id, components in entities.items():
        component = components.get("FightStatusComponent")
        if component is not None:
            status_entities.append((int(raw_id), component,
                                    "FightStatusRoundStartComponent" in components))

    result = []
    for status_id, component, has_round_start_component in sorted(status_entities):
        entry = entries_by_id.get(status_id)
        if entry is None:
            continue
        icon_key = entry.get("Icon") or ""
        background_key = entry.get("Bg") or ""
        binding = {
            "statusId": status_id,
            "iconKey": icon_key,
            "backgroundKey": background_key,
            "iconFile": resolve(icon_key, sprite_manifest, root),
            "backgroundFile": resolve(background_key, sprite_manifest, root),
            "showStackCount": bool(component.get("IsOpenStack", False)),
            **(display_flags.get(status_id, DEFAULT_DISPLAY_FLAGS)
               if display_flags is not None else DEFAULT_DISPLAY_FLAGS),
            "hasRoundStartComponent": has_round_start_component,
        }
        if status_texts is not None and status_id in status_texts:
            binding["nameText"] = status_texts[status_id]["name"]
            binding["descriptionText"] = status_texts[status_id]["description"]
        result.append(binding)
    return len(status_entities), result


DEFAULT_DISPLAY_FLAGS = {
    "showSpriteAboveUnit": True,
    "showRoundReplaceStack": False,
    "showCustomCount": False,
}


def parse_status_display_flags(raw_data):
    """Read the trailing serialized MyFightStatus UI booleans from the pinned MonoBehaviour."""
    raw = bytes(raw_data)
    if len(raw) < 48:
        raise ValueError("Serialized MyFightStatus data is shorter than its fixed UI field layout")
    values = (raw[-48], raw[-8], raw[-4])
    if any(value not in (0, 1) for value in values):
        raise ValueError("Serialized MyFightStatus UI fields are not valid booleans")
    return {
        "showSpriteAboveUnit": bool(values[0]),
        "showRoundReplaceStack": bool(values[1]),
        "showCustomCount": bool(values[2]),
    }


def _contains_id(value, target):
    if isinstance(value, dict):
        return any(_contains_id(item, target) for item in value.values())
    if isinstance(value, list):
        return any(_contains_id(item, target) for item in value)
    return value == target or value == str(target)


def _status_display_from_bundle(apk, bundle_name, status_id):
    objects = apk.objects(bundle_name)
    game_objects = {
        obj.path_id for obj in objects
        if obj.type.name == "GameObject" and obj.read().m_Name == str(status_id)
    }
    if not game_objects:
        return []

    scripts = {
        obj.path_id: obj.read().m_ClassName
        for obj in objects if obj.type.name == "MonoScript"
    }
    matches = []
    for obj in objects:
        if obj.type.name != "MonoBehaviour":
            continue
        tree = obj.read_typetree(check_read=False)
        if tree.get("m_GameObject", {}).get("m_PathID") not in game_objects:
            continue
        script_path_id = tree.get("m_Script", {}).get("m_PathID")
        script_name = scripts.get(script_path_id)
        if script_name is None:
            try:
                script_name = obj.read().m_Script.deref().m_ClassName
            except (AttributeError, FileNotFoundError, KeyError):
                script_name = None
        if script_name != "MyFightStatus":
            continue
        raw = obj.get_raw_data()
        flags = parse_status_display_flags(raw)
        matches.append({
            "flags": flags,
            "path_id": str(obj.path_id),
            "raw_sha256": sha256_bytes(raw),
            "provenance": apk.provenance(obj),
        })
    return matches


def read_status_display_flags(apk, status_ids, entities):
    """Resolve status HUD flags from its own prefab or an entity prefab that embeds it."""
    bundle_by_entity = {}
    for bundle_name in apk.index:
        match = re.search(r"_fightskill_(\d+)\.bundle$", bundle_name)
        if match:
            bundle_by_entity[int(match.group(1))] = bundle_name

    resolved = {}
    sources = []
    for status_id in sorted(status_ids):
        candidates = []
        direct = bundle_by_entity.get(status_id)
        if direct:
            candidates.append((status_id, direct, "direct"))
        matches = []
        for parent_id, bundle_name, source_kind in candidates:
            matches.extend({
                **item,
                "bundle": bundle_name,
                "parent_entity_id": parent_id,
                "source_kind": source_kind,
            } for item in _status_display_from_bundle(apk, bundle_name, status_id))
        if not matches:
            for entity_id, components in sorted(entities.items(), key=lambda item: int(item[0])):
                parent_id = int(entity_id)
                if parent_id == status_id or not _contains_id(components, status_id):
                    continue
                bundle_name = bundle_by_entity.get(parent_id)
                if not bundle_name:
                    continue
                matches.extend({
                    **item,
                    "bundle": bundle_name,
                    "parent_entity_id": parent_id,
                    "source_kind": "referenced_parent",
                } for item in _status_display_from_bundle(apk, bundle_name, status_id))
        distinct_flags = {tuple(sorted(item["flags"].items())) for item in matches}
        if len(distinct_flags) > 1:
            raise ValueError("Conflicting MyFightStatus UI flags for status " + str(status_id))
        if matches:
            selected = matches[0]
            resolved[status_id] = selected["flags"]
            sources.append({key: value for key, value in selected.items() if key != "flags"}
                           | {"status_id": status_id, "flags": selected["flags"]})
        else:
            resolved[status_id] = dict(DEFAULT_DISPLAY_FLAGS)
            sources.append({
                "status_id": status_id,
                "source_kind": "client_field_defaults",
                "flags": dict(DEFAULT_DISPLAY_FLAGS),
            })
    return resolved, sources


def stage_status_ids(game_rules):
    result = set()
    for row in game_rules.get("fight_stage_status", []):
        for key in ("Status", "GlobalRoundStatus", "TargetIdStatus"):
            result.update(int(value) for value in row.get(key, []) or [])
        for key in ("CharacterStatus", "FactionGlobalRoundStatus", "ProfessionStatus"):
            for values in (row.get(key, {}) or {}).values():
                result.update(int(value) for value in values or [])
    return result


def parse_language_text_table(raw_data, requested_keys):
    raw = bytes(raw_data)
    requested_keys = set(requested_keys)
    if len(raw) < 4:
        raise ValueError("Official Chinese language input is truncated")
    count = struct.unpack_from("<i", raw)[0]
    table_end = 4 + 12 * count
    if count < 0 or table_end > len(raw):
        raise ValueError("Official Chinese language index is invalid")
    key_by_hash = {xxhash.xxh64_intdigest(key.encode("utf-8")): key for key in requested_keys}
    if len(key_by_hash) != len(set(requested_keys)):
        raise ValueError("Official Chinese language keys have a hash collision")

    result = {}
    found_hashes = set()
    for index in range(count):
        key_hash, offset = struct.unpack_from("<Qi", raw, 4 + 12 * index)
        if key_hash not in key_by_hash or key_hash in found_hashes:
            continue
        position = table_end + offset
        if offset < 0 or position < table_end or position + 2 > len(raw):
            raise ValueError("Official Chinese language offset is invalid")
        size = struct.unpack_from("<H", raw, position)[0]
        if position + 2 + size > len(raw):
            raise ValueError("Official Chinese language value is truncated")
        key = key_by_hash[key_hash]
        result[key] = raw[position + 2:position + 2 + size].decode("utf-8")
        found_hashes.add(key_hash)
    return result


def read_language_strings(requested_keys):
    requested_keys = set(requested_keys)
    manifest_path = ROOT / LANGUAGE_SOURCE_MANIFEST
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if manifest.get("manifest") != VERSION or manifest.get("lookup") != "XXHash64 UTF-8 seed 0, ushort UTF-8 byte lengths":
        raise ValueError("Battle status text requires the pinned Chinese language source manifest")

    apk_path = ROOT / PINNED_APK_PATH
    if not apk_path.is_file():
        raise ValueError("Pinned battle localization APK is missing: " + str(apk_path))
    apk_digest = sha256_file(apk_path)
    texts = {}
    inputs = {}
    with zipfile.ZipFile(apk_path) as archive:
        for name in LANGUAGE_INPUTS:
            input_record = manifest.get("inputs", {}).get(name)
            source = input_record.get("source") if input_record is not None else None
            if source is None or source.get("origin") != "apk_zip":
                raise ValueError("Official Chinese language input has no pinned APK provenance: " + name)
            if source.get("apk_sha256") != apk_digest:
                raise ValueError("Official Chinese language APK SHA256 mismatch: " + name)
            source_path = source.get("path")
            if not source_path:
                raise ValueError("Official Chinese language APK path is missing: " + name)
            try:
                raw = archive.read(source_path)
            except KeyError as error:
                raise ValueError("Official Chinese language input is absent from the pinned APK: " + name) from error
            digest = sha256_bytes(raw)
            if digest != input_record.get("sha256"):
                raise ValueError("Official Chinese language input SHA256 mismatch: " + name)
            values = parse_language_text_table(raw, requested_keys)
            for key, value in values.items():
                if key in texts and texts[key] != value:
                    raise ValueError("Conflicting official Chinese language values: " + key)
                texts[key] = value
            inputs[name] = {
                "sha256": digest,
                "entry_count": struct.unpack_from("<i", raw)[0],
                "source": source,
            }

    missing = sorted(set(requested_keys) - set(texts))
    if missing:
        raise ValueError("Official stage status text keys are missing: " + ", ".join(missing))
    return texts, inputs


def read_pinned_entry_table():
    apk = Apk(manifest=VERSION)
    if apk.version != VERSION or apk.source.apk_version != VERSION:
        raise ValueError("Battle status icons require the pinned 854_156383 APK and data manifest")
    matches = []
    for obj in apk.objects("config_luban"):
        if obj.type.name != "TextAsset":
            continue
        text = obj.read()
        if text.m_Name == ENTRY_ASSET:
            matches.append((text.m_Script.encode("utf8", "surrogateescape"), apk.provenance(obj)))
    if len(matches) != 1:
        raise ValueError(f"Expected one {ENTRY_ASSET} TextAsset in the pinned APK, found {len(matches)}")
    raw, provenance = matches[0]
    rows = Constructors().table(raw, "TbEntryInfo")
    return apk, raw, rows, provenance


def generated_outputs():
    apk, entry_bytes, entry_rows, provenance = read_pinned_entry_table()
    rules_bytes = (ROOT / RULES_PATH).read_bytes()
    rules = json.loads(rules_bytes)
    game_rules_bytes = (ROOT / GAME_RULES_PATH).read_bytes()
    game_rules = json.loads(game_rules_bytes)
    hud_manifest_bytes = (ROOT / HUD_SOURCE_MANIFEST).read_bytes()
    hud_manifest = json.loads(hud_manifest_bytes)
    if hud_manifest.get("manifest") != VERSION:
        raise ValueError("Battle HUD sprite manifest is not from " + VERSION)

    sprite_manifest = hud_manifest.get("sprites")
    if not isinstance(sprite_manifest, dict):
        raise ValueError("Battle HUD sprite manifest has no sprite index")
    status_ids = {int(entity_id) for entity_id, components in rules["entities"].items()
                  if components.get("FightStatusComponent") is not None}
    status_ids.intersection_update(int(row["Id"]) for row in entry_rows)
    display_flags, display_sources = read_status_display_flags(apk, status_ids, rules["entities"])
    entry_ids = {int(row["Id"]) for row in entry_rows}
    visible_stage_status_ids = stage_status_ids(game_rules) & entry_ids
    stage_text_keys = {
        status_id: (f"entry_{status_id}_name", f"entry_{status_id}_desc")
        for status_id in sorted(visible_stage_status_ids)
    }
    language_texts, language_inputs = read_language_strings(
        key for keys in stage_text_keys.values() for key in keys)
    stage_status_texts = {
        status_id: {"name": language_texts[name_key], "description": language_texts[desc_key]}
        for status_id, (name_key, desc_key) in stage_text_keys.items()
    }
    status_entity_count, entries = build_status_bindings(
        rules["entities"], entry_rows, sprite_manifest, display_flags=display_flags,
        status_texts=stage_status_texts)
    icon_keys = sorted({item["iconKey"] for item in entries if item["iconKey"]})
    background_keys = sorted({item["backgroundKey"] for item in entries if item["backgroundKey"]})

    catalog = {
        "manifest": VERSION,
        "entryTableSha256": sha256_bytes(entry_bytes),
        "battleRulesSha256": sha256_bytes(rules_bytes),
        "spriteManifestSha256": sha256_bytes(hud_manifest_bytes),
        "stageStatusRulesSha256": sha256_bytes(game_rules_bytes),
        "stageStatusTextCount": len(stage_status_texts),
        "statusEntityCount": status_entity_count,
        "entries": entries,
    }
    catalog_bytes = (json.dumps(catalog, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
    source = {
        "manifest": VERSION,
        "apk_sha256": apk.source.apk_sha256,
        "entry_table": {
            "asset": ENTRY_ASSET,
            "type": "TbEntryInfo",
            "sha256": sha256_bytes(entry_bytes),
            "provenance": provenance,
        },
        "battle_rules": {
            "path": RULES_PATH.as_posix(),
            "sha256": sha256_bytes(rules_bytes),
        },
        "hud_sprite_manifest": {
            "path": HUD_SOURCE_MANIFEST.as_posix(),
            "sha256": sha256_bytes(hud_manifest_bytes),
        },
        "stage_status_rules": {
            "path": GAME_RULES_PATH.as_posix(),
            "sha256": sha256_bytes(game_rules_bytes),
            "visible_entry_info_statuses": sorted(visible_stage_status_ids),
        },
        "stage_status_language": {
            "manifest_path": LANGUAGE_SOURCE_MANIFEST.as_posix(),
            "manifest_sha256": sha256_file(ROOT / LANGUAGE_SOURCE_MANIFEST),
            "lookup": "XXHash64 UTF-8 seed 0",
            "keys": {
                str(status_id): {"name": keys[0], "description": keys[1]}
                for status_id, keys in stage_text_keys.items()
            },
            "inputs": language_inputs,
        },
        "counts": {
            "status_entities": status_entity_count,
            "entry_info_matches": len(entries),
            "without_entry_info": status_entity_count - len(entries),
            "entries_with_sprites": sum(bool(item["iconFile"] or item["backgroundFile"]) for item in entries),
            "icon_keys": len(icon_keys),
            "background_keys": len(background_keys),
            "status_instances_direct": sum(item["source_kind"] == "direct" for item in display_sources),
            "status_instances_parent": sum(item["source_kind"] == "referenced_parent" for item in display_sources),
            "status_flags_using_client_defaults": sum(
                item["source_kind"] == "client_field_defaults" for item in display_sources),
            "stage_status_texts": len(stage_status_texts),
            "hidden_above_unit": sum(not item["flags"]["showSpriteAboveUnit"] for item in display_sources),
            "round_replace_stack": sum(item["flags"]["showRoundReplaceStack"] for item in display_sources),
            "custom_count": sum(item["flags"]["showCustomCount"] for item in display_sources),
        },
        "status_display_flag_sources": display_sources,
        "icon_keys": icon_keys,
        "background_keys": background_keys,
        "outputs": {
            OUTPUT_PATH.as_posix(): sha256_bytes(catalog_bytes),
        },
        "operation": "Exact EntryInfo icon/background lookup by FightStatusComponent entity ID; stack and per-status display flags follow pinned client component data",
    }
    source_bytes = (json.dumps(source, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8")
    return {OUTPUT_PATH: catalog_bytes, SOURCE_MANIFEST_PATH: source_bytes}, source


def write_new(path, content):
    if path.exists():
        if path.read_bytes() == content:
            return False
        raise ValueError("Refusing to overwrite changed generated battle data: " + str(path))
    path.parent.mkdir(parents=True, exist_ok=True)
    handle, temporary_name = tempfile.mkstemp(prefix=".battle-status-icons-", dir=path.parent)
    temporary = Path(temporary_name)
    try:
        with os.fdopen(handle, "wb") as stream:
            stream.write(content)
        temporary.replace(path)
    finally:
        temporary.unlink(missing_ok=True)
    return True


def write_generated(path, content):
    path.parent.mkdir(parents=True, exist_ok=True)
    handle, temporary_name = tempfile.mkstemp(prefix=".battle-status-icons-", dir=path.parent)
    temporary = Path(temporary_name)
    try:
        with os.fdopen(handle, "wb") as stream:
            stream.write(content)
        temporary.replace(path)
    finally:
        temporary.unlink(missing_ok=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Verify outputs without writing files")
    parser.add_argument("--replace-generated", action="store_true",
                        help="Replace outputs only when the existing catalog matches its source manifest")
    args = parser.parse_args()
    outputs, source = generated_outputs()
    old_manifest_path = ROOT / SOURCE_MANIFEST_PATH
    old_output_path = ROOT / OUTPUT_PATH
    expected_old_output = None
    if args.replace_generated and old_manifest_path.is_file() and old_output_path.is_file():
        old_manifest = json.loads(old_manifest_path.read_text(encoding="utf-8"))
        expected_old_output = old_manifest.get("outputs", {}).get(OUTPUT_PATH.as_posix())
        if old_manifest.get("manifest") != VERSION or not expected_old_output \
                or sha256_file(old_output_path) != expected_old_output:
            raise ValueError("Existing status icon output does not match its source manifest")
    for relative, content in outputs.items():
        path = ROOT / relative
        if args.check:
            if not path.is_file() or path.read_bytes() != content:
                raise ValueError("Generated battle status icon data differs: " + str(path))
        elif args.replace_generated and path == old_output_path and path.exists():
            if sha256_file(path) != expected_old_output:
                raise ValueError("Refusing to replace changed battle status icon data: " + str(path))
            write_generated(path, content)
        elif args.replace_generated and path == old_manifest_path and path.exists():
            write_generated(path, content)
        else:
            write_new(path, content)
    print(json.dumps({"counts": source["counts"], "outputs": source["outputs"]}, sort_keys=True))


if __name__ == "__main__":
    main()
