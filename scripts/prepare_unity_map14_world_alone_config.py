"""Extract the selected Map 15 alone-world preparation rules for Map 14."""
import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VERSION = "871_156598"
MAP_SOURCE = ROOT / "assets/reference/official-map/map14-source-manifest.json"
TABLE_PROOF = ROOT / ".tools/source-packages/current/871_156598/table-proof-source-manifest.json"
TABLE_ROOT = ROOT / ".tools/source-packages/current/871_156598/official-data"
CODE_ROOT = ROOT / ".tools/source-packages/current/871_156598/official-code"
OUTPUT = ROOT / "unity/Assets/Game/Resources/Map14/world-alone-preparation.json"
OUTPUT_META = Path(str(OUTPUT) + ".meta")
RECEIPT = ROOT / "assets/reference/official-map/map14-world-alone-preparation-source-manifest.json"
TASK_IDS = (7101, 7102, 7103, 7104)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def write_owned(path, data, prior_sha=None):
    if path.exists():
        current = path.read_bytes()
        if current != data and (prior_sha is None or sha(current) != prior_sha):
            raise ValueError("Refusing to overwrite changed Map 15 preparation output: " + str(path))
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


def prepare():
    sys.path.insert(0, str(ROOT / "scripts"))
    from official_apk import Apk
    from official_tables import Constructors

    source = json.loads(MAP_SOURCE.read_bytes())
    proof = json.loads(TABLE_PROOF.read_bytes())
    if proof.get("manifest") != VERSION:
        raise ValueError("Map 15 preparation tables are not from the selected version")
    map_record = source["inputs"]["/Map/map_world_alone.g.bytes"]
    if map_record.get("manifest") != VERSION or map_record.get("bundle") != "defaultpackage_assets_config_luban_map.bundle":
        raise ValueError("MapWorldAloneInfo source differs from the pinned bundle")

    tables = {}
    input_records = []
    for name, constructor in (("task", "TbTaskInfo"), ("conditions", "TbConditionInfo")):
        table_record = next(row for row in proof["tables"] if row["table"] == name)
        if table_record.get("constructor") != constructor:
            raise ValueError("Unexpected official table constructor: " + name)
        path = TABLE_ROOT / (name + ".g.bytes")
        payload = path.read_bytes()
        if sha(payload) != table_record["sha256"]:
            raise ValueError("Official table bytes differ from the retained source proof: " + name)
        tables[name] = Constructors(CODE_ROOT).table(payload, constructor)
        input_records.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(payload),
                                  bundle=table_record["source"]))

    apk = Apk.current()
    bundle_name = map_record["bundle"]
    bundle_record = apk.bundle_records[bundle_name]
    if bundle_record["hash"] != map_record["bundle_hash"]:
        raise ValueError("Official map bundle hash differs from the Map14 source manifest")
    obj = next((item for item in apk.objects(bundle_name)
                if item.path_id == int(map_record["path_id"])), None)
    if obj is None or obj.type.name != "TextAsset":
        raise ValueError("Pinned MapWorldAloneInfo TextAsset is missing")
    text_asset = obj.read()
    raw_map_table = text_asset.m_Script.encode("utf-8", "surrogateescape")
    if text_asset.m_Name != "map_world_alone.g" or len(raw_map_table) != map_record["bytes"] \
            or sha(raw_map_table) != map_record["payload_sha256"]:
        raise ValueError("MapWorldAloneInfo payload differs from its pinned source proof")
    map_rows = Constructors(CODE_ROOT).table(raw_map_table, "TbMapWorldAloneInfo")
    alone = next((row for row in map_rows if row.get("AloneMapEntryId") == 15), None)
    if alone is None or alone.get("TakGroupIdForPreparing") != 7101 \
            or alone.get("PrepareEndConditionId") != 115 \
            or alone.get("PreparingType") != 1 \
            or alone.get("PreparingDataParam") != "{Rank:'Gold',DayOffset:14,MinDurationS:0}":
        raise ValueError("Map 15 alone-world configuration differs from the verified row")

    tasks = [row for row in tables["task"] if row.get("ClassId") in TASK_IDS]
    if [row.get("ClassId") for row in tasks] != list(TASK_IDS) \
            or any(row.get("GroupId") != 7101 or row.get("TaskType") != 23
                   or row.get("RepeatAccept") is not True or row.get("CompletedTimes") != 1
                   for row in tasks):
        raise ValueError("Map 15 daily preparation tasks differ from their pinned rows")
    condition_row = next((row for row in tables["conditions"] if row.get("ClassId") == 115), None)
    condition = condition_row.get("Condition") if condition_row else None
    if not condition or condition.get("_type") != "MapWorldActivedCondition" \
            or condition.get("MapClassId") != 15:
        raise ValueError("Map 15 preparation condition differs from condition 115")

    output = {
        "manifest": VERSION,
        "mapClassId": 15,
        "aloneWorld": alone,
        "preparationRule": {"rank": 5, "dayOffset": 14, "minDurationS": 0},
        "prepareEndCondition": condition_row,
        "preparationTasks": tasks,
    }
    output_bytes = (json.dumps(output, ensure_ascii=False, sort_keys=True,
                               separators=(",", ":"), allow_nan=False) + "\n").encode("utf-8")
    meta = ("fileFormatVersion: 2\n"
            "guid: " + hashlib.sha256(b"zjcs-map14-world-alone-preparation").hexdigest()[:32] + "\n"
            "TextScriptImporter:\n  externalObjects: {}\n  userData:\n"
            "  assetBundleName:\n  assetBundleVariant:\n").encode("ascii")
    previous = json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else None
    prior_outputs = {item["path"]: item["sha256"] for item in (previous or {}).get("outputs", [])}
    write_owned(OUTPUT, output_bytes, prior_outputs.get(OUTPUT.relative_to(ROOT).as_posix()))
    write_owned(OUTPUT_META, meta, prior_outputs.get(OUTPUT_META.relative_to(ROOT).as_posix()))
    inputs = [
        dict(path=MAP_SOURCE.relative_to(ROOT).as_posix(), sha256=sha(MAP_SOURCE.read_bytes())),
        dict(path=TABLE_PROOF.relative_to(ROOT).as_posix(), sha256=sha(TABLE_PROOF.read_bytes())),
        *input_records,
        dict(path=".tools/source-packages/current/871_156598/official-code/common-source/Common/MapWorldAloneInfo.cs",
             sha256=sha((CODE_ROOT / "common-source/Common/MapWorldAloneInfo.cs").read_bytes())),
        dict(path=".tools/source-packages/current/871_156598/official-code/common-source/Common/MapWorldActivedCondition.cs",
             sha256=sha((CODE_ROOT / "common-source/Common/MapWorldActivedCondition.cs").read_bytes())),
    ]
    receipt = dict(manifest=VERSION, operation="Decode the exact MapWorldAloneInfo, TaskInfo and condition 115 rows using the original-version Luban constructors.",
                   bundle=dict(name=bundle_name, hash=map_record["bundle_hash"],
                               path_id=map_record["path_id"], payload_sha256=map_record["payload_sha256"]),
                   inputs=inputs,
                   tools=[dict(path="scripts/prepare_unity_map14_world_alone_config.py",
                               sha256=sha(Path(__file__).read_bytes()))],
                   outputs=[dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes()))
                            for path in (OUTPUT, OUTPUT_META)])
    receipt_bytes = (json.dumps(receipt, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    write_owned(RECEIPT, receipt_bytes,
                sha(RECEIPT.read_bytes()) if RECEIPT.exists() else None)
    print("Restored Map 15 preparation config, condition 115 and", len(tasks), "daily task rows")


if __name__ == "__main__":
    prepare()
