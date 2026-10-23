import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "assets/official/adventure.json"
OUTPUT = ROOT / "unity/Assets/StreamingAssets/Home/fruit-tree-tutorials.json"
MANIFEST = ROOT / "assets/reference/fruit-tree/tutorial-source-manifest.json"
VOICE_MAPPING = ROOT / "assets/official/map-audio.json"
VOICE_OUTPUT = ROOT / "unity/Assets/StreamingAssets/Home/FruitTreeAudio"
STORY_IDS = ("FruitTreeUnlock1", "FruitTreeUnlock2")
VOICE_EVENT_KEYS = (
    "chapter_2_guide_12_01", "chapter_2_guide_12_02", "chapter_2_guide_12_03",
    "chapter_2_guide_12_04", "chapter_2_guide_12_05", "chapter_2_guide_12_06",
    "chapter_2_guide_15_01", "chapter_2_guide_15_02",
)


def sha256(payload):
    return hashlib.sha256(payload).hexdigest()


def stable_guid(relative):
    return hashlib.md5(("zjcs:" + relative.replace("\\", "/").lower()).encode("utf-8")).hexdigest()


def write_default_meta(path):
    relative = path.relative_to(ROOT / "unity/Assets").as_posix()
    meta = Path(str(path) + ".meta")
    if not meta.is_file():
        meta.write_text(
            "fileFormatVersion: 2\n"
            "guid: " + stable_guid(relative) + "\n"
            "DefaultImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n",
            encoding="utf-8", newline="\n")
    return meta


def write_folder_meta(path):
    relative = path.relative_to(ROOT / "unity/Assets").as_posix()
    meta = Path(str(path) + ".meta")
    if not meta.is_file():
        meta.write_text(
            "fileFormatVersion: 2\n"
            "guid: " + stable_guid(relative) + "\n"
            "folderAsset: yes\n"
            "DefaultImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n",
            encoding="utf-8", newline="\n")


def main():
    source_bytes = SOURCE.read_bytes()
    source = json.loads(source_bytes)
    voice_mapping_bytes = VOICE_MAPPING.read_bytes()
    voice_mapping = json.loads(voice_mapping_bytes)
    voice_files = {}
    voice_catalog = []
    voice_records = []
    for event_key in VOICE_EVENT_KEYS:
        paths = voice_mapping.get("events", {}).get(event_key)
        if not isinstance(paths, list) or len(paths) != 1 or not paths[0].startswith("res://"):
            raise ValueError("The official fruit tree voice mapping is missing or ambiguous: " + event_key)
        source_path = ROOT / paths[0].removeprefix("res://")
        if not source_path.is_file() or source_path.suffix.lower() != ".ogg":
            raise FileNotFoundError("The official fruit tree voice file is missing: " + str(source_path))
        target_path = VOICE_OUTPUT / source_path.name
        target_path.parent.mkdir(parents=True, exist_ok=True)
        write_folder_meta(VOICE_OUTPUT)
        audio_bytes = source_path.read_bytes()
        target_path.write_bytes(audio_bytes)
        meta_path = write_default_meta(target_path)
        voice_files[event_key] = target_path.relative_to(ROOT / "unity/Assets/StreamingAssets").as_posix()
        voice_catalog.append({"event_key": event_key, "file": voice_files[event_key]})
        voice_records.append({
            "event_key": event_key,
            "source": {
                "path": source_path.relative_to(ROOT).as_posix(),
                "bytes": len(audio_bytes),
                "sha256": sha256(audio_bytes),
            },
            "output": {
                "path": target_path.relative_to(ROOT).as_posix(),
                "bytes": len(audio_bytes),
                "sha256": sha256(audio_bytes),
                "meta_sha256": sha256(meta_path.read_bytes()),
            },
        })

    stories = []
    for story_id in STORY_IDS:
        story = source["stories"][story_id]
        dialogue_ids = sorted({
            node["DialogId"]
            for node in story["nodes"].values()
            if node.get("_type") == "DialogNode"
        })
        dialogues = source["dialogues"][story_id]
        groups = []
        for dialogue_id in dialogue_ids:
            entries = sorted(dialogues[str(dialogue_id)], key=lambda row: (row["Id"], row["Order"]))
            groups.append({
                "id": dialogue_id,
                "lines": [{
                    "role_id": row.get("RoleId", 0),
                    "text": row.get("Text", ""),
                    "voice_event_key": row.get("VoiceEventKey", ""),
                "voice_file": voice_files.get(row.get("VoiceEventKey", ""), ""),
                } for row in entries],
            })
        stories.append({"id": story_id, "dialogues": groups})

    used_voice_keys = {line["voice_event_key"] for story in stories
                       for dialogue in story["dialogues"] for line in dialogue["lines"]}
    if used_voice_keys != set(VOICE_EVENT_KEYS):
        raise ValueError("The fruit tree tutorial dialogue voice events differ from the pinned mapping.")

    payload = json.dumps({"manifest": "854_156383", "stories": stories, "voices": voice_catalog},
                         ensure_ascii=False, indent=2).encode("utf-8") + b"\n"
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes(payload)
    output_relative = OUTPUT.relative_to(ROOT).as_posix()
    source_relative = SOURCE.relative_to(ROOT).as_posix()
    output_meta = write_default_meta(OUTPUT)
    manifest = {
        "manifest": "854_156383",
        "generator": "scripts/prepare_fruit_tree_tutorials.py",
        "input": {"path": source_relative, "bytes": len(source_bytes), "sha256": sha256(source_bytes)},
        "voice_mapping": {
            "path": VOICE_MAPPING.relative_to(ROOT).as_posix(),
            "bytes": len(voice_mapping_bytes),
            "sha256": sha256(voice_mapping_bytes),
        },
        "voices": voice_records,
        "output": {
            "path": output_relative, "bytes": len(payload), "sha256": sha256(payload),
            "meta_sha256": sha256(output_meta.read_bytes()),
        },
    }
    MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {len(stories)} fruit tree tutorial stories and {len(voice_records)} local voices to {output_relative}")


if __name__ == "__main__":
    main()
