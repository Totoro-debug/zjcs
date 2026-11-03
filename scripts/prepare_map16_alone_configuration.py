"""Extract current official whale-island standalone map configuration."""
import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VERSION = "884_156692"
MAP_ID = 16
STORY_NAMES = (
    "LookForWhaleIsland", "SubmitRopes", "DiscoverWhaleIsland", "WhaleIslandArrive",
    "WhaleIslandFinish", "WhaleIslandFinishLoop", "WhaleIslandLeave",
)
OUTPUT = ROOT / "unity/Assets/Game/Resources/Map16/world-alone-configuration.json"
RECEIPT = ROOT / "assets/reference/official-map/map16-alone-configuration-source-manifest.json"


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def extract_configuration(map_rows, condition_rows, story_rows):
    map_matches = [row for row in map_rows if row.get("AloneMapEntryId") == MAP_ID]
    if len(map_matches) != 1:
        raise ValueError("Selected official source must contain exactly one Map16 standalone row")
    alone = map_matches[0]
    expected_param = "{Rank:'Silver',DayOffset:14,MinDurationS:300}"
    if alone.get("PreparingType") != 1 or alone.get("PreparingDataParam") != expected_param \
            or alone.get("PrepareEndConditionId") != 116 \
            or alone.get("PrepareEndStoryId") != "DiscoverWhaleIsland" \
            or alone.get("PrepareEndActiveMapClassIds") != [MAP_ID] \
            or alone.get("FinishStoryId") != "WhaleIslandLeave" \
            or alone.get("FinishRewardRuleIds") != [770, 771] \
            or alone.get("UiPrefabPath") != "UI/AloneWorldMap/AloneWorldMapEntryItemContentMap16":
        raise ValueError("Official Map16 standalone configuration differs from the selected source contract")

    conditions = [row for row in condition_rows if row.get("ClassId") == 116]
    if len(conditions) != 1 or conditions[0].get("Condition", {}).get("_type") != "MapUnlockCondition" \
            or conditions[0]["Condition"].get("MapClassId") != MAP_ID:
        raise ValueError("Official Map16 standalone condition 116 differs")

    stories = {row.get("StoryName"): row for row in story_rows if row.get("StoryName") in STORY_NAMES}
    if set(stories) != set(STORY_NAMES):
        raise ValueError("Selected official story table is missing a whale-island story")
    return {
        "manifest": VERSION,
        "mapClassId": MAP_ID,
        "aloneWorld": alone,
        "prepareEndCondition": conditions[0],
        "stories": [stories[name] for name in STORY_NAMES],
    }


def prepare():
    sys.path.insert(0, str(ROOT / "scripts"))
    from official_apk import Apk
    from official_tables import Constructors

    apk = Apk.current()
    if apk.version != VERSION:
        raise ValueError("Map16 standalone source differs from the selected official version")
    code_root = ROOT / ".tools/source-packages/current" / VERSION / "official-code"
    code_manifest = json.loads((code_root.parent / "code-manifest.json").read_bytes())
    if code_manifest.get("manifest") != VERSION:
        raise ValueError("Map16 table constructors do not belong to the selected source version")
    constructors = Constructors(code_root)
    inputs = []

    def table(bundle, suffix, constructor):
        obj = apk.asset(bundle, suffix)
        raw = obj.read().m_Script.encode("utf8", "surrogateescape")
        inputs.append(dict(source=apk.provenance(obj), payloadSha256=sha(raw), bytes=len(raw), constructor=constructor))
        return constructors.table(raw, constructor)

    map_rows = table("config_luban_map", "/map_world_alone.g.bytes", "TbMapWorldAloneInfo")
    condition_rows = table("config_luban", "/conditions.g.bytes", "TbConditionInfo")
    story_rows = table("config_luban", "/story.g.bytes", "TbStoryInfo")
    configuration = extract_configuration(map_rows, condition_rows, story_rows)
    output = (json.dumps(configuration, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode("utf8")
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes(output)
    meta_path = Path(str(OUTPUT) + ".meta")
    meta = ("fileFormatVersion: 2\n"
            "guid: 72ec13524a836a52a679ab370f4fcb62\n"
            "TextScriptImporter:\n  externalObjects: {}\n  userData:\n"
            "  assetBundleName:\n  assetBundleVariant:\n").encode("ascii")
    if meta_path.exists() and meta_path.read_bytes() != meta:
        raise ValueError("Refusing to replace unrelated Map16 configuration metadata")
    meta_path.write_bytes(meta)
    receipt = {
        "format": 1,
        "manifest": VERSION,
        "mapClassId": MAP_ID,
        "operation": "Decode selected-version MapWorldAloneInfo, ConditionInfo and StoryInfo rows with original Luban constructors.",
        "codeManifestSha256": sha((code_root.parent / "code-manifest.json").read_bytes()),
        "inputs": inputs,
        "outputs": [
            dict(path=OUTPUT.relative_to(ROOT).as_posix(), bytes=len(output), sha256=sha(output)),
            dict(path=meta_path.relative_to(ROOT).as_posix(), bytes=len(meta), sha256=sha(meta)),
        ],
    }
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf8", newline="\n")
    print("Extracted Map16 standalone configuration and", len(configuration["stories"]), "story rows")


if __name__ == "__main__":
    prepare()
