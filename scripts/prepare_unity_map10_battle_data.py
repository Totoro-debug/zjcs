"""Build pinned Map10 stage, monster formation, and arena data for Unity."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_map_codec import entities as decode_entities
from official_tables import Constructors


VERSION = "854_156383"
OUTPUT = ROOT / "unity/Assets/Official/World/Map/10"
PLACEMENTS_SOURCE = ("config_mapinfowrap", "Assets/Config/MapInfoWrap/mapinfowrap_10.g.bytes")
MAP_STAGE_TABLE = ("config_luban", "Assets/Config/Luban/map_stage.g.bytes", "TbMapToStageInfo")
STAGE_TABLE = ("config_luban", "Assets/Config/Luban/stage.g.bytes", "TbStageInfo")
ARRAY_TABLE = ("config_luban", "Assets/Config/Luban/stage_monster_array.g.bytes", "TbStageMonsterArrayInfo")
BATTLE_SCENES_PATH = ROOT / "assets/official/battle-stages.json"
BATTLE_SCENES_MANIFEST_PATH = ROOT / "assets/reference/official-map/battle-stages-source-manifest.json"
PARSER_SOURCE_PATH = ROOT / ".tools/source-packages/official-code/common-source/Common/StageMonsterArrayInfoParser.cs"
REFERENCE_MANIFEST_PATH = ROOT / "assets/reference/official-map/map10-battles-source-manifest.json"


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def json_bytes(value) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8")


def text_asset_bytes(obj) -> bytes:
    return obj.read().m_Script.encode("utf-8", "surrogateescape")


def source_record(apk: Apk, obj, path: str, payload: bytes) -> dict:
    raw = obj.get_raw_data()
    return dict(apk.provenance(obj), object=path, raw_size=len(raw), raw_sha256=sha256(raw),
                payload_size=len(payload), payload_sha256=sha256(payload))


def map10_stage_ids(placements: dict, map_stage_rows: list[dict], expected_entity_count: int = 82) -> set[int]:
    if placements.get("id") != 10 or not isinstance(placements.get("entities"), list):
        raise ValueError("Placement source does not identify Map10")
    if len(placements["entities"]) != expected_entity_count:
        raise ValueError("Map10 placement count differs from the pinned source")
    stage_ids = set()
    for row in map_stage_rows:
        if int(row["MapClassId"]) != 10:
            continue
        stage_ids.add(int(row["StageId"]))

    for entity in placements["entities"]:
        for value in entity.get("stages", {}).values():
            if not isinstance(value, list) or not value or not isinstance(value[0], list):
                raise ValueError("Map10 entity stage state does not match the official stage schema")
            for stage_id in value[0]:
                if not isinstance(stage_id, int) or stage_id <= 0:
                    raise ValueError("Map10 entity stage id is invalid")
                stage_ids.add(stage_id)

    if not stage_ids:
        raise ValueError("Map10 does not reference any official stages")
    return stage_ids


def collect_battle_data(stage_rows: list[dict], arrays: dict[int, dict], scenes: dict[str, dict]) -> tuple[dict, dict]:
    referenced_arrays = set()
    referenced_games = set()
    for stage in stage_rows:
        game_id = int(stage["GameId"])
        referenced_games.add(game_id)
        wave_ids = stage.get("WaveArrayIds") or []
        wave_monsters = stage.get("WaveMonsterIds") or []
        catalogs = stage.get("WaveArrayCatalogs")
        if catalogs is not None:
            raise ValueError("Map10 stage uses randomized WaveArrayCatalogs; preserve its official selection data first")
        if len(wave_ids) != len(wave_monsters):
            if wave_ids or wave_monsters:
                raise ValueError("Map10 stage wave array ids and monster ids differ in length")
        for array_id, monster_ids in zip(wave_ids, wave_monsters):
            array_id = int(array_id)
            if array_id not in arrays:
                raise ValueError("Official Map10 stage references a missing monster array: " + str(array_id))
            groups = {int(monster["GroupId"]) for monster in arrays[array_id]["Monsters"]}
            if len(groups) != len(monster_ids):
                raise ValueError("Monster array group count differs from the official stage roster: " + str(array_id))
            referenced_arrays.add(array_id)

    missing_games = referenced_games - {int(key) for key in scenes}
    if missing_games:
        raise ValueError("Map10 stage has no official battle scene: " + ", ".join(map(str, sorted(missing_games))))

    for stage in stage_rows:
        wave_ids = stage.get("WaveArrayIds") or []
        if not wave_ids:
            continue
        scene = scenes[str(int(stage["GameId"]))]
        normal = scene.get("FightRoleMonsterNormalComponent")
        origins = [] if normal is None else normal.get("WaveOriPos") or []
        if len(origins) < len(wave_ids):
            raise ValueError("Official Map10 battle scene has too few wave origins for stage " + str(stage["Id"]))

    selected_scenes = {str(game_id): scenes[str(game_id)] for game_id in sorted(referenced_games)}
    selected_arrays = {}
    for array_id in sorted(referenced_arrays):
        row = arrays[array_id]
        selected_arrays[str(array_id)] = {
            "Catalog": int(row["Catalog"]),
            "ArrayId": int(row["ArrayId"]),
            "Monsters": sorted(row["Monsters"], key=lambda value: int(value["GroupId"])),
        }

    return selected_arrays, selected_scenes


def write_meta(path: Path, importer: str) -> None:
    relative = path.relative_to(ROOT / "unity").as_posix()
    guid = hashlib.md5(("zjcs-official:" + relative).encode("utf-8")).hexdigest()
    content = (f"fileFormatVersion: 2\nguid: {guid}\n{importer}:\n  externalObjects: {{}}\n"
               "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    target = Path(str(path) + ".meta")
    if target.exists() and target.read_text(encoding="utf-8") != content:
        raise ValueError("Refusing to replace an existing Unity meta file: " + str(target))
    if not target.exists():
        target.write_text(content, encoding="utf-8", newline="\n")


def write_generated(path: Path, content: bytes, expected_existing_hash: str | None = None) -> None:
    if path.exists():
        existing = path.read_bytes()
        if existing == content:
            return
        if expected_existing_hash is None or sha256(existing) != expected_existing_hash:
            raise ValueError("Refusing to overwrite changed Map10 battle data: " + str(path))
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(content)


def existing_output_hashes(payload_path: Path, manifest_path: Path) -> tuple[str | None, str | None]:
    if not payload_path.exists() and not manifest_path.exists():
        return None, None
    if not payload_path.is_file() or not manifest_path.is_file():
        raise ValueError("Refusing to replace incomplete Map10 battle output")
    payload = payload_path.read_bytes()
    manifest_bytes = manifest_path.read_bytes()
    try:
        manifest = json.loads(manifest_bytes.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ValueError("Existing Map10 battle manifest is not valid JSON") from exc
    output = manifest.get("output", {})
    if (manifest.get("format") != 1 or manifest.get("manifest") != VERSION
            or manifest.get("mapClassId") != 10
            or output.get("path") != "Assets/Official/World/Map/10/map10-battles.json"
            or output.get("sha256") != sha256(payload)):
        raise ValueError("Existing Map10 battle output does not match its source manifest")
    return sha256(payload), sha256(manifest_bytes)


def build(apk: Apk | None = None, output: Path = OUTPUT) -> dict:
    apk = apk or Apk()
    constructors = Constructors()

    placements_obj = apk.asset(PLACEMENTS_SOURCE[0], PLACEMENTS_SOURCE[1])
    placements_bytes = text_asset_bytes(placements_obj)
    placements = decode_entities(placements_bytes)
    placements_source = source_record(apk, placements_obj, PLACEMENTS_SOURCE[1], placements_bytes)

    map_stage_obj = apk.asset(MAP_STAGE_TABLE[0], MAP_STAGE_TABLE[1])
    map_stage_bytes = text_asset_bytes(map_stage_obj)
    map_stage_rows = constructors.table(map_stage_bytes, MAP_STAGE_TABLE[2])
    stage_ids = map10_stage_ids(placements, map_stage_rows)
    map_stage_source = source_record(apk, map_stage_obj, MAP_STAGE_TABLE[1], map_stage_bytes)

    stage_obj = apk.asset(STAGE_TABLE[0], STAGE_TABLE[1])
    stage_bytes = text_asset_bytes(stage_obj)
    source_stages = constructors.table(stage_bytes, STAGE_TABLE[2])
    stage_rows = [row for row in source_stages if row["Id"] in stage_ids]
    if stage_ids != {int(row["Id"]) for row in stage_rows}:
        raise ValueError("Map10 references a stage absent from the pinned APK")
    stage_source = source_record(apk, stage_obj, STAGE_TABLE[1], stage_bytes)

    array_obj = apk.asset(ARRAY_TABLE[0], ARRAY_TABLE[1])
    array_bytes = text_asset_bytes(array_obj)
    array_data = constructors.table(array_bytes, ARRAY_TABLE[2])
    if not isinstance(array_data, dict) or not isinstance(array_data.get("InfoDict"), dict):
        raise ValueError("Stage monster array table schema differs from the official parser")
    all_arrays = array_data["InfoDict"]
    array_source = source_record(apk, array_obj, ARRAY_TABLE[1], array_bytes)

    scene_manifest = json.loads(BATTLE_SCENES_MANIFEST_PATH.read_text(encoding="utf-8"))
    scene_payload = BATTLE_SCENES_PATH.read_bytes()
    if scene_manifest.get("manifest") != VERSION or scene_manifest.get("output", {}).get("sha256") != sha256(scene_payload):
        raise ValueError("Official battle scene payload differs from its source manifest")
    all_scenes = json.loads(scene_payload.decode("utf-8"))["stages"]
    arrays, scenes = collect_battle_data(stage_rows, all_arrays, all_scenes)

    parser_hash = sha256(PARSER_SOURCE_PATH.read_bytes())
    scene_manifest_hash = sha256(BATTLE_SCENES_MANIFEST_PATH.read_bytes())
    payload = {
        "manifest": VERSION,
        "mapClassId": 10,
        "stages": sorted(stage_rows, key=lambda row: int(row["Id"])),
        "monsterArrays": arrays,
        "battleScenes": scenes,
        "formationOrder": "StageMonsterArrayInfoParser.PostParse sorts Monsters by GroupId",
    }
    payload_bytes = json_bytes(payload)
    manifest = {
        "format": 1,
        "manifest": VERSION,
        "mapClassId": 10,
        "operation": "Read original Luban tables and preserve all Map10 stage, formation, and battle-grid fields; formation ordering follows StageMonsterArrayInfoParser.PostParse.",
        "inputs": {
            "stage_table": stage_source,
            "monster_array_table": array_source,
            "stage_monster_array_parser": {
                "path": PARSER_SOURCE_PATH.relative_to(ROOT).as_posix(),
                "sha256": parser_hash,
            },
            "map10_placements": placements_source,
            "map10_stage_index": map_stage_source,
            "battle_scenes": {
                "path": BATTLE_SCENES_PATH.relative_to(ROOT).as_posix(),
                "sha256": sha256(scene_payload),
                "source_manifest": BATTLE_SCENES_MANIFEST_PATH.relative_to(ROOT).as_posix(),
                "source_manifest_sha256": scene_manifest_hash,
            },
        },
        "output": {
            "path": "Assets/Official/World/Map/10/map10-battles.json",
            "size": len(payload_bytes),
            "sha256": sha256(payload_bytes),
        },
    }
    manifest_bytes = json_bytes(manifest)

    output = Path(output)
    output.mkdir(parents=True, exist_ok=True)
    payload_path = output / "map10-battles.json"
    manifest_path = output / "battle-source-manifest.json"
    payload_hash, manifest_hash = existing_output_hashes(payload_path, manifest_path)
    reference_hash = None
    if output.resolve() == OUTPUT.resolve() and REFERENCE_MANIFEST_PATH.exists():
        if not manifest_path.exists() or REFERENCE_MANIFEST_PATH.read_bytes() != manifest_path.read_bytes():
            raise ValueError("Reference and Unity Map10 battle manifests differ")
        reference_hash = sha256(REFERENCE_MANIFEST_PATH.read_bytes())
    write_generated(payload_path, payload_bytes, payload_hash)
    write_generated(manifest_path, manifest_bytes, manifest_hash)
    if output.resolve().is_relative_to((ROOT / "unity").resolve()):
        write_meta(payload_path, "TextScriptImporter")
        write_meta(manifest_path, "TextScriptImporter")
    if output.resolve() == OUTPUT.resolve():
        write_generated(REFERENCE_MANIFEST_PATH, manifest_bytes, reference_hash)
    return manifest


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    args = parser.parse_args()
    manifest = build(output=args.output)
    print("Map10 official battle stages:", len(json.loads((args.output / "map10-battles.json").read_text(encoding="utf-8"))["stages"]))
    print("Map10 monster arrays:", len(json.loads((args.output / "map10-battles.json").read_text(encoding="utf-8"))["monsterArrays"]))
    print("Battle data SHA256:", manifest["output"]["sha256"])


if __name__ == "__main__":
    main()
