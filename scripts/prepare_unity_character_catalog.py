"""Restore source-verified character overview and property catalogs for Unity."""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import re
import tempfile
import uuid

from official_apk import ROOT
from official_tables import Constructors

UNITY = ROOT / "unity"
TARGET = UNITY / "Assets/Official/Character"
SOURCES = (
    ("assets/official/character-overview.json", "assets/reference/character-overview-source-manifest.json",
     "outputs:assets/official/character-overview.json", "character-overview.json"),
    ("assets/official/character-properties.json", "assets/reference/character-properties/source-manifest.json",
     "output_sha256", "character-properties.json"),
)
RULE_TABLES = (
    "item", "level_exp", "level_bless", "player_subrank", "function_limit",
    "prop_cfg", "game_settings", "character_skin_dye", "character_skin_color",
    "profession_base",
)
FIDELITY_TABLES = ("system_level_limit", "system_level_gap_limit", "level_prop_default_value")


def decode_level_props(raw: bytes, wanted: set[int]) -> dict[str, object]:
    import struct

    magic, version, sections_at, index_at = struct.unpack_from("<IHii", raw)
    if magic != 1280328262 or version != 1:
        raise ValueError("Unexpected pinned level_prop header")

    def varint(position: int) -> tuple[int, int]:
        value = shift = 0
        while True:
            byte = raw[position]
            position += 1
            value |= (byte & 127) << shift
            shift += 7
            if not byte & 128:
                break
            if shift >= 70:
                raise ValueError("Invalid level_prop varint")
        return (value >> 1) ^ -(value & 1), position

    section_count = struct.unpack_from("<i", raw, sections_at)[0]
    position = sections_at + 4
    sections = []
    for _ in range(section_count):
        count = struct.unpack_from("<H", raw, position)[0]
        position += 2
        sections.append(struct.unpack_from("<" + "H" * count, raw, position))
        position += count * 2
    count = struct.unpack_from("<i", raw, index_at)[0]
    position = index_at + 4
    result = {}
    for _ in range(count):
        class_id, first, last = struct.unpack_from("<iii", raw, position)
        position += 12
        offsets = struct.unpack_from("<" + "i" * (last - first + 1), raw, position)
        position += 4 * len(offsets)
        if class_id not in wanted:
            continue
        rows = {}
        for level, offset in enumerate(offsets, first):
            if offset < 0:
                continue
            section, cursor = varint(offset)
            values = {}
            for property_id in sections[section]:
                value, cursor = varint(cursor)
                if value:
                    values[str(property_id)] = value
            rows[str(level)] = values
        result[str(class_id)] = {"min": first, "max": last, "rows": rows}
    missing = wanted - {int(value) for value in result}
    if missing:
        raise ValueError("Official level_prop classes are missing: " + str(sorted(missing)))
    return result


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def atomic_write(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, name = tempfile.mkstemp(prefix=".unity-character-", dir=path.parent)
    temporary = Path(name)
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        temporary.replace(path)
    finally:
        temporary.unlink(missing_ok=True)


def metadata(path: Path, folder: bool) -> bytes:
    relative = path.relative_to(UNITY).as_posix()
    guid = uuid.uuid5(uuid.NAMESPACE_URL, "zjcs-unity:" + relative).hex
    if folder:
        text = (f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n"
                "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")
    elif path.suffix.lower() == ".png":
        text = (f"fileFormatVersion: 2\nguid: {guid}\nTextureImporter:\n"
                "  internalIDToNameTable: []\n  externalObjects: {}\n  serializedVersion: 12\n"
                "  mipmaps:\n    mipMapMode: 0\n    enableMipMap: 0\n    sRGBTexture: 1\n"
                "  textureType: 8\n  textureShape: 1\n  singleChannelComponent: 0\n"
                "  alphaIsTransparency: 1\n  spriteMode: 1\n  spritePixelsToUnits: 100\n"
                "  spriteMeshType: 1\n  textureCompression: 1\n  platformSettings: []\n"
                "  spriteSheet:\n    serializedVersion: 2\n    sprites: []\n    outline: []\n"
                "    physicsShape: []\n    bones: []\n    spriteID: \n    internalID: 0\n"
                "    vertices: []\n    indices: \n    edges: []\n    weights: []\n"
                "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    else:
        text = (f"fileFormatVersion: 2\nguid: {guid}\nTextScriptImporter:\n"
                "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")
    return text.encode("ascii")


def install(path: Path, data: bytes, owned_sha256: str | None = None) -> None:
    if path.exists() and path.read_bytes() != data:
        if owned_sha256 is None or digest(path.read_bytes()) != owned_sha256:
            raise ValueError("Refusing to overwrite changed Unity character content: " + str(path))
        atomic_write(path, data)
    if not path.exists():
        atomic_write(path, data)
    meta_path = Path(str(path) + ".meta")
    meta_data = metadata(path, path.is_dir())
    if not meta_path.exists():
        atomic_write(meta_path, meta_data)


def restore_sprites(manifests: list[dict]) -> list[dict]:
    resources = {}
    for manifest in manifests:
        resources.update(manifest.get("resources", {}))
    data = json.loads((ROOT / "assets/characters/wardrobe/ui.json").read_text(encoding="utf-8"))
    referenced = set()

    def visit(value: object) -> None:
        if isinstance(value, dict):
            sprite = value.get("file")
            if isinstance(sprite, str) and sprite.startswith("res://assets/official/"):
                referenced.add(sprite)
            for child in value.values():
                visit(child)
        elif isinstance(value, list):
            for child in value:
                visit(child)

    visit(data)
    overview = json.loads((ROOT / "assets/official/character-overview.json").read_text(encoding="utf-8"))
    properties = json.loads((ROOT / "assets/official/character-properties.json").read_text(encoding="utf-8"))
    visit(overview)
    visit(properties)
    panels = json.loads((TARGET / "character-panels.json").read_text(encoding="utf-8"))
    visit(panels)
    icons = json.loads((ROOT / "assets/official/icons.json").read_text(encoding="utf-8"))
    icon_item_ids = {str(item_id) for row in json.loads((ROOT / "assets/official/rules.json")
                    .read_text(encoding="utf-8"))["player_subrank"] for item_id in (row.get("Items") or {})}
    sprite_files = {path.name for path in (ROOT / "assets/official/sprites").glob("*.png")}
    for profession in json.loads((ROOT / "assets/official/rules.json").read_text(encoding="utf-8"))["profession_base"]:
        for field in ("Icon2", "Icon1", "Icon3"):
            base = str(profession.get(field) or "").rsplit("/", 1)[-1]
            match = next((name for name in sprite_files if base and name.startswith(base + "-")), None)
            if match:
                referenced.add("res://assets/official/sprites/" + match)
                break
    for item_id in icon_item_ids:
        icon = icons.get(item_id)
        if isinstance(icon, dict) and isinstance(icon.get("file"), str):
            referenced.add(icon["file"])
    for item in json.loads((ROOT / "assets/official/rules.json").read_text(encoding="utf-8"))["item"]:
        if item.get("ItemType") not in (100, 101, 111, 300) and not 200 <= int(item.get("ItemType", 0)) <= 219:
            continue
        icon = icons.get(str(item["ClassId"]))
        if isinstance(icon, dict) and isinstance(icon.get("file"), str):
            referenced.add(icon["file"])
    for name in ("ImgQuality_White-031a03cab0.png", "ImgQuality_Blue-42b8cb9630.png",
                 "ImgQuality_Purple-0b40a53021.png", "ImgQuality_Orange-78887320b0.png",
                 "ImgQuality_Gold-5f018c42e0.png", "ImgQuality_Red-caa09e6943.png",
                 "ImgQuality_Multicolour-93374ebc65.png"):
        referenced.add("res://assets/official/sprites/" + name)
    if not referenced or any(path not in resources for path in referenced):
        missing = sorted(path for path in referenced if path not in resources)
        raise ValueError("Character sprite sources are not covered by official manifests: " + ", ".join(missing[:8]))

    result = []
    for source_uri in sorted(referenced):
        source = ROOT / source_uri.replace("res://", "")
        expected = resources[source_uri].get("sha256")
        actual = digest(source.read_bytes())
        if expected != actual:
            raise ValueError("Character sprite differs from its official source manifest: " + source_uri)
        relative = Path("Art") / source_uri.removeprefix("res://assets/official/")
        target = TARGET / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        folder = target.parent
        while folder != TARGET:
            meta_path = Path(str(folder) + ".meta")
            meta_data = metadata(folder, True)
            if not meta_path.exists():
                atomic_write(meta_path, meta_data)
            folder = folder.parent
        install(target, source.read_bytes())
        result.append({"name": relative.as_posix(), "bytes": target.stat().st_size,
                       "sha256": actual, "source": source_uri})
    return result


def restore() -> dict:
    previous_outputs = {}
    manifest_path = TARGET / "source-manifest.json"
    if manifest_path.exists():
        previous = json.loads(manifest_path.read_text(encoding="utf-8"))
        allowed_operations = {
            "Restore original CharacterPanel and character property view metadata for Unity binding.",
            "Restore original character UI metadata and progression tables for Unity binding.",
            "Restore original character UI, progression tables, and base battle properties for Unity binding.",
        }
        if previous.get("manifest") != "854_156383" or previous.get("operation") not in allowed_operations:
            raise ValueError("Refusing to overwrite an unrelated Unity character source manifest.")
        previous_outputs = {row.get("name"): row.get("sha256") for row in previous.get("outputs", [])}

    TARGET.mkdir(parents=True, exist_ok=True)
    folder_meta = Path(str(TARGET) + ".meta")
    expected_folder_meta = metadata(TARGET, True)
    if not folder_meta.exists():
        atomic_write(folder_meta, expected_folder_meta)

    manifest_inputs = []
    outputs = []
    for source_name, manifest_name, output_key, output_name in SOURCES:
        source_manifest_path = ROOT / manifest_name
        manifest_data = source_manifest_path.read_bytes()
        manifest = json.loads(manifest_data)
        if manifest.get("manifest", manifest.get("version")) != "854_156383":
            raise ValueError("Character source is not pinned to 854_156383: " + manifest_name)
        if output_key.startswith("outputs:"):
            expected = manifest.get("outputs", {}).get(output_key.split(":", 1)[1])
        else:
            expected = manifest.get(output_key)
        if not isinstance(expected, str) or len(expected) != 64:
            raise ValueError("Character source manifest has no output hash: " + output_key)
        source_data = (ROOT / source_name).read_bytes()
        actual = digest(source_data)
        if actual != expected:
            raise ValueError("Character source output differs from its manifest: " + source_name)
        target = TARGET / output_name
        install(target, source_data)
        outputs.append({"name": output_name, "bytes": len(source_data), "sha256": actual})
        manifest_inputs.append({"path": manifest_name, "sha256": digest(manifest_data),
                                "output": source_name, "output_sha256": actual})

    city_manifest = json.loads((ROOT / "assets/reference/official-city/source-manifest.json").read_text(encoding="utf-8"))
    rule_data = (ROOT / "assets/official/rules.json").read_bytes()
    fidelity_data = (ROOT / "assets/official/fidelity-rules.json").read_bytes()
    expected_rules = city_manifest["data_sha256"]["rules.json"]
    expected_fidelity = city_manifest["data_sha256"]["fidelity-rules.json"]
    if digest(rule_data) != expected_rules or digest(fidelity_data) != expected_fidelity:
        raise ValueError("Character progression rules differ from the pinned official catalog.")
    rules = json.loads(rule_data)
    fidelity = json.loads(fidelity_data)
    if any(name not in rules for name in RULE_TABLES) or any(name not in fidelity for name in FIDELITY_TABLES):
        raise ValueError("Pinned character progression tables are incomplete.")
    role_prop_bytes = (ROOT / ".tools/source-packages/official-data/role_prop_group.g.bytes").read_bytes()
    item_time_bytes = (ROOT / ".tools/source-packages/official-data/item_time_limit.g.bytes").read_bytes()
    parser = Constructors()
    role_groups = parser.table(role_prop_bytes, "TbRolePropGroupInfo")
    time_limits = parser.table(item_time_bytes, "TbItemTimeLimitInfo")
    level_prop_path = ROOT / ".tools/source-packages/WwiseBnk/wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile"
    level_prop_bytes = level_prop_path.read_bytes()
    game_settings = rules["game_settings"]
    level_prop_ids = {int(game_settings[name]) for name in (
        "PlayerLevelBattlePropId", "PlayerSubRankBattlePropId", "PlayerLevelPropId", "PlayerSubRankPropId")}
    level_prop_ids.update(int(row["LevelPropID"]) for row in role_groups
                          if int(row["ID"]) == int(game_settings["PlayerFightRolePropGroupId"]))
    level_prop_ids.update(int(row["LevelPropID"]) for row in role_groups
                          if int(row["ID"]) == int(game_settings["CalcCombatPropGroupId"]))
    level_props = decode_level_props(level_prop_bytes, level_prop_ids)
    prop_type_source = (ROOT / ".tools/source-packages/official-code/common-source/Common/PropType.cs").read_text(encoding="utf-8-sig")
    property_ids = {name: int(value) for name, value in
                    re.findall(r"\b(\w+)\s*=\s*(\d+)", prop_type_source)}
    wardrobe_rules = json.loads((ROOT / "assets/characters/wardrobe/rules.json").read_text(encoding="utf-8"))
    wardrobe_items = {int(key) for key in wardrobe_rules["items"]}
    fashion_types = set(game_settings["AppearanceTypes"]) | set(game_settings["HomeDecorationTypes"]) | {425}
    wardrobe_items.update(row["ClassId"] for row in rules["item"] if row.get("ItemType") in fashion_types)
    wardrobe_items.update(row["ClassId"] for row in rules["item"] if row.get("ItemType") in (100, 101, 111, 300)
                          or 200 <= int(row.get("ItemType", 0)) <= 219)
    wardrobe_items.update([1, 43003])
    for rank in rules["player_subrank"]:
        wardrobe_items.update(int(item_id) for item_id in (rank.get("Items") or {}))
    filtered_rules = {name: rules[name] for name in RULE_TABLES if name != "item"}
    filtered_rules["item"] = [row for row in rules["item"] if row["ClassId"] in wardrobe_items]
    icons = json.loads((ROOT / "assets/official/icons.json").read_text(encoding="utf-8"))
    item_icons = {str(row["ClassId"]): icons[str(row["ClassId"])]["file"]
                  for row in filtered_rules["item"]
                  if str(row["ClassId"]) in icons and isinstance(icons[str(row["ClassId"])], dict)}
    from npc_travel_language import language
    unit_keys = {row["Unit"] for row in rules["prop_cfg"] if row.get("Unit")}
    prop_units, unit_sources = language(unit_keys)
    profession_source = (ROOT / ".tools/source-packages/official-code/common-source/Common/Profession.cs"
                         ).read_text(encoding="utf-8-sig")
    profession_enum = {int(value): name for name, value in
                       re.findall(r"^\s*(\w+)\s*=\s*(\d+)", profession_source, re.M)} if "= 0" in profession_source else {}
    if not profession_enum:
        profession_enum = {index: name for index, name in enumerate(
            re.findall(r"^\s*(\w+)\s*,?\s*$", profession_source, re.M))}
    profession_name_map = {str(row["Profession"]): "profession_name_" + profession_enum.get(row["Profession"], "")
                           for row in rules["profession_base"]}
    profession_names, profession_name_sources = language(set(profession_name_map.values()))
    profession_names = {profession: profession_names[key] for profession, key in profession_name_map.items()
                        if key in profession_names}
    unit_sources.update(profession_name_sources)
    profession_icons = {}
    sprite_files = {path.name: path for path in (ROOT / "assets/official/sprites").glob("*.png")}
    for row in rules["profession_base"]:
        for field in ("Icon2", "Icon1", "Icon3"):
            name = str(row.get(field) or "").rsplit("/", 1)[-1]
            if not name: continue
            for file_name, path in sprite_files.items():
                if file_name.startswith(name + "-"):
                    profession_icons[str(row["Profession"])] = "res://assets/official/sprites/" + file_name
                    break
            if str(row["Profession"]) in profession_icons: break
    properties_data = json.loads((ROOT / "assets/official/character-properties.json").read_text(encoding="utf-8"))
    prop_categories = {key: value for key, value in properties_data["categories"].items()}
    progression = {
        "manifest": "854_156383",
        "rules": filtered_rules,
        "tables": {name: fidelity[name] for name in FIDELITY_TABLES},
        "role_prop_group": [dict(row) for row in role_groups],
        "level_prop": level_props,
        "property_ids": property_ids,
        "prop_units": prop_units,
        "prop_categories": prop_categories,
        "item_icons": item_icons,
        "profession_names": profession_names,
        "profession_icons": profession_icons,
    }
    payloads = (
        ("character-progression.json", progression),
        ("item-time-limits.json", {"manifest": "854_156383", "rows": [dict(row) for row in time_limits]}),
    )
    generated_sources = []
    for name, value in payloads:
        encoded = (json.dumps(value, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
        target = TARGET / name
        install(target, encoded, previous_outputs.get(name))
        outputs.append({"name": name, "bytes": len(encoded), "sha256": digest(encoded)})
        generated_sources.append({"output": name, "sha256": digest(encoded)})

    manifest_inputs.extend([
        {"path": "assets/official/rules.json", "sha256": expected_rules},
        {"path": "assets/official/fidelity-rules.json", "sha256": expected_fidelity},
        {"path": ".tools/source-packages/official-data/role_prop_group.g.bytes",
         "sha256": digest(role_prop_bytes), "constructor": "TbRolePropGroupInfo"},
        {"path": ".tools/source-packages/official-data/item_time_limit.g.bytes",
         "sha256": digest(item_time_bytes), "constructor": "TbItemTimeLimitInfo"},
        {"path": str(level_prop_path.relative_to(ROOT)).replace("\\", "/"),
         "sha256": digest(level_prop_bytes),
         "source": json.loads((level_prop_path.parent / "bundle-sources.json").read_text(encoding="utf-8"))[level_prop_path.name],
         "classes": sorted(level_prop_ids), "decoder": "LevelPropParser indexed signed-varint rows"},
    ])
    manifest_inputs.extend({"path": path, "sha256": sha, "purpose": "official property unit localization"}
                           for path, sha in sorted(unit_sources.items()))
    sprite_manifests = [
        json.loads((ROOT / "assets/reference/character-overview-source-manifest.json").read_text(encoding="utf-8")),
        json.loads((ROOT / "assets/reference/character-properties/source-manifest.json").read_text(encoding="utf-8")),
        json.loads((ROOT / "assets/reference/character-appearance/ui-source-manifest.json").read_text(encoding="utf-8")),
        json.loads((ROOT / "assets/reference/official-city/source-manifest.json").read_text(encoding="utf-8")),
    ]
    sprite_manifest_paths = [
        "assets/reference/character-overview-source-manifest.json",
        "assets/reference/character-properties/source-manifest.json",
        "assets/reference/character-appearance/ui-source-manifest.json",
        "assets/reference/official-city/source-manifest.json",
    ]
    for source_path in sprite_manifest_paths:
        source_data = (ROOT / source_path).read_bytes()
        manifest_inputs.append({"path": source_path, "sha256": digest(source_data)})
    wardrobe_ui_path = "assets/characters/wardrobe/ui.json"
    wardrobe_ui_data = (ROOT / wardrobe_ui_path).read_bytes()
    ui_manifest_data = (ROOT / "assets/reference/character-appearance/ui-source-manifest.json").read_bytes()
    ui_manifest = json.loads(ui_manifest_data)
    if digest(wardrobe_ui_data) != ui_manifest.get("outputs", {}).get("res://" + wardrobe_ui_path):
        raise ValueError("Wardrobe UI hierarchy differs from its official source manifest.")
    manifest_inputs.append({"path": wardrobe_ui_path, "sha256": digest(wardrobe_ui_data)})
    sprite_outputs = restore_sprites(sprite_manifests)
    outputs.extend({"name": row["name"], "bytes": row["bytes"], "sha256": row["sha256"]}
                   for row in sprite_outputs)
    manifest_inputs.extend({"path": row["source"], "sha256": row["sha256"]} for row in sprite_outputs)

    if manifest_path.exists():
        for row in json.loads(manifest_path.read_text(encoding="utf-8")).get("outputs", []):
            if row.get("name") == "character-panels.json" and not any(
                    output.get("name") == "character-panels.json" for output in outputs):
                panel_path = TARGET / "character-panels.json"
                if panel_path.exists():
                    outputs.append({"name": "character-panels.json", "bytes": panel_path.stat().st_size,
                                    "sha256": digest(panel_path.read_bytes())})

    manifest = {
        "manifest": "854_156383",
        "operation": "Restore original character UI, progression tables, and base battle properties for Unity binding.",
        "inputs": manifest_inputs,
        "outputs": outputs,
    }
    encoded = (json.dumps(manifest, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
    if manifest_path.exists():
        existing = json.loads(manifest_path.read_text(encoding="utf-8"))
        old_outputs = {entry.get("name") for entry in existing.get("outputs", [])}
        allowed_operations = {
            "Restore original CharacterPanel and character property view metadata for Unity binding.",
            "Restore original character UI metadata and progression tables for Unity binding.",
            "Restore original character UI, progression tables, and base battle properties for Unity binding.",
        }
        known_outputs = {"character-overview.json", "character-properties.json", "character-panels.json",
                         "character-progression.json", "item-time-limits.json"}
        if (existing.get("manifest") != "854_156383" or existing.get("operation") not in allowed_operations
                or not {"character-overview.json", "character-properties.json"}.issubset(old_outputs)
                or not old_outputs.issubset(known_outputs | {name for name in old_outputs if name.startswith("Art/")})):
            raise ValueError("Refusing to overwrite an unrelated Unity character source manifest.")
        new_outputs = {row["name"]: row["sha256"] for row in outputs}
        for name, expected in previous_outputs.items():
            output_path = TARGET / name
            actual = digest(output_path.read_bytes()) if output_path.exists() else None
            if actual != expected and actual != new_outputs.get(name):
                raise ValueError("Refusing to replace a modified Unity character output: " + name)
        atomic_write(manifest_path, encoded)
    else:
        install(manifest_path, encoded)
    return {"directory": str(TARGET), "files": len(outputs), "manifest_sha256": digest(encoded)}


if __name__ == "__main__":
    print(json.dumps(restore(), ensure_ascii=False, indent=2))
