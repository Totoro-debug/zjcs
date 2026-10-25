"""Package fixed-version battle skill Playables and their available official animation clips."""
from __future__ import annotations

import hashlib
import json
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MANIFEST = "854_156383"
SOURCE = ROOT / "assets/official/battle-animation/catalog.json"
SOURCE_MANIFEST = ROOT / "assets/reference/battle-animation/source-manifest.json"
CLIP_SOURCE = (ROOT / "unity/Assets/Official/CharacterCreation/Recovered/CharacterModels/Recovered"
               / "Male/AppearanceAssets/Character/Model/Male_1/Anim")
RIPPER_NPC_SOURCE = (ROOT / ".tools/assetripper-1.3.14/temp/zjcs-item-source-native/ExportedProject"
                     / "Assets/AppearanceAssets/Npc")
OUTPUT_ROOT = ROOT / "unity/Assets/Game/Resources/Combat"
CLIP_OUTPUT = OUTPUT_ROOT / "SkillAnimations"
CLIP_OUTPUT_META = Path(str(CLIP_OUTPUT) + ".meta")
OUTPUT_DATA = OUTPUT_ROOT / "skill-playables.json"
OUTPUT_MANIFEST = ROOT / "assets/reference/battle-animation/skill-playables-source-manifest.json"


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while block := stream.read(1024 * 1024):
            digest.update(block)
    return digest.hexdigest()


def guid_for(path: Path) -> str:
    relative = path.relative_to(ROOT).as_posix()
    return hashlib.md5(("zjcs-official:" + relative).encode("utf-8")).hexdigest()


def write_verified(path: Path, content: bytes) -> None:
    if path.exists():
        if path.read_bytes() != content:
            raise ValueError("Refusing to overwrite changed generated battle skill asset: " + str(path))
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(content)


def text_meta(path: Path) -> bytes:
    return ("fileFormatVersion: 2\n"
            "guid: " + guid_for(path) + "\n"
            "TextScriptImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n").encode("ascii")


def animation_meta(path: Path) -> bytes:
    return ("fileFormatVersion: 2\n"
            "guid: " + guid_for(path) + "\n"
            "NativeFormatImporter:\n"
            "  externalObjects: {}\n"
            "  mainObjectFileID: 7400000\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n").encode("ascii")


def folder_meta(path: Path) -> bytes:
    return ("fileFormatVersion: 2\n"
            "guid: " + guid_for(path) + "\n"
            "folderAsset: yes\n"
            "DefaultImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n").encode("ascii")


def boolean(value: object) -> bool:
    return bool(value)


def resolve_clip_sources(catalog: dict, animation_names: set[str]) -> dict[str, dict]:
    if not animation_names:
        return {}

    sys.path.insert(0, str(ROOT / "scripts"))
    from official_apk import Apk
    from official_unity_schema import ScriptSchemas
    from UnityPy.classes.PPtr import PPtr

    references: dict[str, list[tuple[str, dict]]] = {name: [] for name in animation_names}
    for raw_id, skill in catalog.get("skills", {}).items():
        for row in skill.get("default", []):
            if row.get("kind") == "override" and row.get("animation") in references:
                references[row["animation"]].append((raw_id, row))

    apk = Apk(MANIFEST)
    schemas = ScriptSchemas()
    resolved: dict[str, dict] = {}
    try:
        for animation, rows in sorted(references.items()):
            objects = []
            for skill_id, row in rows:
                component_source = row.get("source", {})
                bundle = apk.load(component_source["bundle"])
                owner = next((obj for asset in bundle.files.values() if hasattr(asset, "objects")
                              for obj in asset.objects.values()
                              if obj.path_id == int(component_source["path_id"])), None)
                if owner is None:
                    raise ValueError("Official skill Playable source object is missing: " + animation)
                config = schemas.read(owner)
                pointer = config.get("OverrideClip") or {}
                if int(pointer.get("m_PathID", 0)) == 0:
                    raise ValueError("Official skill Playable has no override clip pointer: " + animation)
                clip = PPtr(**pointer, assetsfile=owner.assets_file).deref()
                if clip.type.name != "AnimationClip":
                    raise ValueError("Official skill Playable override is not an AnimationClip: " + animation)
                raw = clip.get_raw_data()
                value = clip.read()
                if value.m_Name != animation:
                    raise ValueError("Official skill Playable clip name does not match its catalog: " + animation)
                muscle = getattr(value, "m_MuscleClip", None)
                duration = (float(muscle.m_StopTime) - float(muscle.m_StartTime)) if muscle is not None else 0.0
                source = apk.provenance(clip)
                objects.append({
                    "skill_entity_id": int(skill_id),
                    "component": component_source,
                    "clip": {
                        "type": clip.type.name,
                        "name": value.m_Name,
                        "bytes": len(raw),
                        "sha256": sha256_bytes(raw),
                        "duration": duration,
                        "source": source,
                    },
                })

            unique = {(item["clip"]["source"]["bundle"], item["clip"]["source"]["path_id"],
                       item["clip"]["sha256"]) for item in objects}
            if len(unique) != 1:
                raise ValueError("Skill entities reference different official clips for " + animation)
            resolved[animation] = {"references": objects}
    finally:
        apk.archive.close()
    return resolved


def find_ripper_clip(animation: str, official_source: dict) -> Path | None:
    source_bundle = official_source["references"][0]["clip"]["source"]["bundle"]
    if not source_bundle.endswith("_npc_middle_model_anim.bundle") \
            and not source_bundle.endswith("_npc_big_model_anim.bundle"):
        return None
    file_name = animation.replace("/", "_") + ".anim"
    candidates = list(RIPPER_NPC_SOURCE.rglob(file_name)) if RIPPER_NPC_SOURCE.is_dir() else []
    expected_bundle = source_bundle[:-len(".bundle")]
    for candidate in candidates:
        meta_path = Path(str(candidate) + ".meta")
        if not meta_path.is_file():
            continue
        meta = meta_path.read_text(encoding="utf-8", errors="replace")
        bundle_match = re.search(r"^  assetBundleName: (.*)$", meta, re.MULTILINE)
        if bundle_match is None or bundle_match.group(1) != expected_bundle:
            continue
        text = candidate.read_text(encoding="utf-8", errors="replace")
        name_match = re.search(r"^  m_Name: (.*)$", text, re.MULTILINE)
        duration_match = re.search(r"^\s+m_StopTime: ([^\r\n]+)$", text, re.MULTILINE)
        official_duration = official_source["references"][0]["clip"]["duration"]
        if name_match is None or name_match.group(1) != animation or duration_match is None:
            continue
        if abs(float(duration_match.group(1)) - official_duration) > 0.0001:
            continue
        return candidate
    return None


def build() -> tuple[dict, dict]:
    catalog_bytes = SOURCE.read_bytes()
    catalog = json.loads(catalog_bytes)
    source_manifest_bytes = SOURCE_MANIFEST.read_bytes()
    source_manifest = json.loads(source_manifest_bytes)
    if source_manifest.get("manifest") != MANIFEST:
        raise ValueError("Battle animation source manifest is not pinned to " + MANIFEST)

    records = []
    clips: dict[str, dict] = {}
    missing_clips: set[str] = set()
    unresolved_source_names: set[str] = set()
    cue_counts = {"animator": 0, "override": 0}

    for skill in catalog.get("skills", {}).values():
        for row in skill.get("default", []):
            if row.get("kind") == "override" and row.get("animation"):
                name = row["animation"]
                if not (CLIP_SOURCE / (name.replace("/", "_") + ".anim")).is_file():
                    unresolved_source_names.add(name)
    official_clip_sources = resolve_clip_sources(catalog, unresolved_source_names)

    for raw_id, skill in sorted(catalog.get("skills", {}).items(), key=lambda item: int(item[0])):
        cues = []
        for row in skill.get("default", []):
            kind = row.get("kind")
            if kind not in cue_counts:
                continue
            fields = row.get("fields", {})
            animation = row.get("animation", "")
            clip_resource = ""
            if kind == "override" and animation:
                clip_resource = animation.replace("/", "_")
                source_clip = CLIP_SOURCE / (clip_resource + ".anim")
                if not source_clip.is_file():
                    source_clip = find_ripper_clip(animation, official_clip_sources[animation])
                if source_clip is not None and source_clip.is_file():
                    source_bytes = source_clip.read_bytes()
                    target_clip = CLIP_OUTPUT / source_clip.name
                    target_meta = Path(str(target_clip) + ".meta")
                    write_verified(target_clip, source_bytes)
                    write_verified(target_meta, animation_meta(target_clip))
                    clips.setdefault(animation, {
                        "source": source_clip.relative_to(ROOT).as_posix(),
                        "source_sha256": sha256_bytes(source_bytes),
                        "official_clip_source": official_clip_sources.get(animation),
                        "output": target_clip.relative_to(ROOT).as_posix(),
                        "output_sha256": sha256_file(target_clip),
                        "meta": target_meta.relative_to(ROOT).as_posix(),
                        "guid": guid_for(target_clip),
                    })
                else:
                    missing_clips.add(animation)
                    clip_resource = ""

            cue_counts[kind] += 1
            cues.append({
                "kind": 1 if kind == "override" else 0,
                "time": float(row.get("time", 0.0)),
                "speed": float(row.get("speed", 1.0)),
                "animation": animation,
                "clip_resource": clip_resource,
                "origin_clip_name": fields.get("OriginClipName", ""),
                "trigger_param": fields.get("myTriggerParam", ""),
                "pre_state_name_allow_list": fields.get("PreStateNameAllowList", []),
                "my_bool_param": fields.get("myBoolParam", ""),
                "my_bool_value": boolean(fields.get("myBoolValue", False)),
                "my_float_param": fields.get("myFloatParam", ""),
                "my_float_value": float(fields.get("myFloatValue", 0.0)),
                "int_param": fields.get("IntParam", ""),
                "int_value": int(fields.get("IntValue", 0)),
                "my_duration": float(fields.get("myDuration", 0.0)),
                "layer_name": fields.get("LayerName", ""),
                "layer_weight": float(fields.get("LayerWeight", 0.0)),
                "end_resume_param": boolean(fields.get("myEndResumeParam", False)),
            })
        if cues:
            records.append({"skill_entity_id": int(raw_id), "playables": cues})

    payload = {"manifest": MANIFEST, "skills": records}
    output_bytes = (json.dumps(payload, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
    previous_manifest = None
    previous_manifest_bytes = None
    if OUTPUT_MANIFEST.is_file():
        previous_manifest_bytes = OUTPUT_MANIFEST.read_bytes()
        previous_manifest = json.loads(previous_manifest_bytes)
        if previous_manifest.get("manifest") != MANIFEST \
                or previous_manifest.get("catalog_sha256") != sha256_bytes(catalog_bytes):
            raise ValueError("Refusing to regenerate a skill Playable catalog from an unpinned prior manifest")
        if OUTPUT_DATA.is_file() and sha256_file(OUTPUT_DATA) != previous_manifest.get("output_sha256"):
            raise ValueError("Refusing to replace a modified generated skill Playable catalog")
    if OUTPUT_DATA.exists() and OUTPUT_DATA.read_bytes() != output_bytes \
            and previous_manifest is None:
        raise ValueError("Refusing to overwrite an untracked skill Playable catalog")
    OUTPUT_DATA.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_DATA.write_bytes(output_bytes)
    output_meta = Path(str(OUTPUT_DATA) + ".meta")
    write_verified(output_meta, text_meta(OUTPUT_DATA))
    write_verified(CLIP_OUTPUT_META, folder_meta(CLIP_OUTPUT))

    all_guids: dict[str, str] = {}
    for meta_path in (ROOT / "unity/Assets").rglob("*.meta"):
        text = meta_path.read_text(encoding="utf-8", errors="replace")
        match = re.search(r"^guid: ([0-9a-f]{32})$", text, re.MULTILINE)
        if not match:
            continue
        guid = match.group(1)
        previous = all_guids.get(guid)
        if previous is not None and previous != str(meta_path):
            raise ValueError("Duplicate Unity GUID already exists: " + guid)
        all_guids[guid] = str(meta_path)

    outputs = [OUTPUT_DATA, output_meta, CLIP_OUTPUT, CLIP_OUTPUT_META]
    for clip in clips.values():
        outputs.append(ROOT / clip["output"])
        outputs.append(ROOT / clip["meta"])
    for output in outputs:
        meta_path = Path(str(output) + ".meta") if output.suffix != ".meta" else output
        text = meta_path.read_text(encoding="utf-8")
        guid_match = re.search(r"^guid: ([0-9a-f]{32})$", text, re.MULTILINE)
        if not guid_match:
            raise ValueError("Generated Unity asset has no stable GUID: " + str(meta_path))
        guid = guid_match.group(1)
        collisions = [path for value, path in all_guids.items() if value == guid and path != str(meta_path)]
        if collisions:
            raise ValueError("Generated Unity asset GUID collides with " + collisions[0])

    manifest = {
        "format": 1,
        "manifest": MANIFEST,
        "catalog": SOURCE.relative_to(ROOT).as_posix(),
        "catalog_sha256": sha256_bytes(catalog_bytes),
        "catalog_manifest": SOURCE_MANIFEST.relative_to(ROOT).as_posix(),
        "catalog_manifest_sha256": sha256_bytes(source_manifest_bytes),
        "output": OUTPUT_DATA.relative_to(ROOT).as_posix(),
        "output_sha256": sha256_bytes(output_bytes),
        "output_meta": output_meta.relative_to(ROOT).as_posix(),
        "output_guid": guid_for(OUTPUT_DATA),
        "clip_folder_meta": CLIP_OUTPUT_META.relative_to(ROOT).as_posix(),
        "clip_folder_guid": guid_for(CLIP_OUTPUT),
        "skills": len(records),
        "cues": sum(cue_counts.values()),
        "cue_kinds": cue_counts,
        "clip_assets": clips,
        "unavailable_clip_names": sorted(missing_clips),
        "resolved_clip_source_names": sorted(official_clip_sources),
        "resolved_clip_sources": official_clip_sources,
        "operation": "Pack fixed APK skill Playable animator triggers and clip overrides; copy only byte-identical available official AnimationClips; keep missing clips unmapped",
    }
    manifest_bytes = (json.dumps(manifest, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    if OUTPUT_MANIFEST.exists() and OUTPUT_MANIFEST.read_bytes() != manifest_bytes \
            and previous_manifest_bytes is None:
        raise ValueError("Refusing to overwrite an untracked skill Playable source manifest")
    OUTPUT_MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_MANIFEST.write_bytes(manifest_bytes)
    return payload, manifest


def main() -> None:
    payload, manifest = build()
    print("official skill entities:", len(payload["skills"]))
    print("official Playable cues:", manifest["cues"], manifest["cue_kinds"])
    print("official clip assets:", len(manifest["clip_assets"]))
    print("unavailable clip names:", len(manifest["unavailable_clip_names"]))
    print("output:", OUTPUT_DATA.relative_to(ROOT).as_posix(), sha256_file(OUTPUT_DATA))


if __name__ == "__main__":
    main()
