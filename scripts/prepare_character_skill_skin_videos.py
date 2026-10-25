#!/usr/bin/env python3
"""Extract the pinned official skill appearance preview videos."""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
BUNDLE_ROOT = ROOT / ".tools" / "source-packages" / "cloud" / "854_156383" / "DefaultPackage"
BUNDLE_INVENTORY = ROOT / ".tools" / "source-packages" / "cloud-research" / "bundle-inventory.json"
RULES_MANIFEST_PATH = ROOT / "assets" / "reference" / "character-appearance" / "rules-source-manifest.json"
OUTPUT_ROOT = ROOT / "unity" / "Assets" / "StreamingAssets" / "Character" / "SkillSkinVideos"
RUNTIME_INDEX = ROOT / "unity" / "Assets" / "StreamingAssets" / "Character" / "skill-skin-videos.json"
SOURCE_MANIFEST = ROOT / "assets" / "reference" / "character-appearance" / "skill-skin-videos.json"


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def load_official_rows() -> list[dict]:
    manifest = json.loads(RULES_MANIFEST_PATH.read_text(encoding="utf-8"))
    if manifest.get("version") != "854_156383":
        raise ValueError("Skill skin table manifest is not pinned to 854_156383.")
    table = manifest.get("tables", {}).get("skill_skin", {})
    table_path = ROOT / table.get("file", "")
    raw = table_path.read_bytes()
    if sha256(raw) != table.get("sha256"):
        raise ValueError("The pinned skill_skin table bytes differ from their source manifest.")

    constructor_name = "TbSkillSkinInfo"
    constructor_path = ROOT / ".tools" / "source-packages" / "official-code" / "common-source" / "Common" / (constructor_name + ".cs")
    constructor_hash = sha256(constructor_path.read_bytes())
    if constructor_hash != manifest.get("constructors", {}).get(constructor_name):
        raise ValueError("The official skill skin constructor differs from its source manifest.")
    sys.path.insert(0, str(ROOT / "scripts"))
    from official_tables import Constructors

    rows = Constructors().table(raw, constructor_name)
    if not isinstance(rows, list) or not rows:
        raise ValueError("The official skill_skin table is missing.")
    for row in rows:
        if (int(row.get("ClassId", 0)) <= 0 or int(row.get("SkillId", 0)) <= 0
                or int(row.get("EffectSkillId", 0)) <= 0):
            raise ValueError("The official skill_skin table contains an invalid row.")
    return rows


def load_sources() -> dict:
    inventory = json.loads(BUNDLE_INVENTORY.read_text(encoding="utf-8"))
    sources = {}
    for row in inventory.get("bundles", []):
        name = row.get("name", "")
        if "skilldisplayvedio_" not in name or row.get("manifest") != "854_156383":
            continue
        path = BUNDLE_ROOT / row["file"]
        raw = path.read_bytes()
        if len(raw) != int(row["size"]) or hashlib.md5(raw).hexdigest() != row["hash"]:
            raise ValueError(f"The cached original video bundle differs from its manifest: {name}")
        sources[name] = {
            "manifest": "854_156383",
            "origin": "official_cdn",
            "url": "https://zhangjcsdl.leiting.com/patch/202609271456857/Android/DefaultPackage/" + row["file"],
            "apk_entry": None,
            "md5": row["hash"],
            "sha256": sha256(raw),
        }
    return sources


def extract_video(effect_skill_id: int, sources: dict) -> tuple[bytes, dict]:
    sys.path.insert(0, str(ROOT / ".tools" / "character-art-python"))
    import UnityPy
    from UnityPy.helpers.ResourceReader import get_resource_data

    suffix = f"skilldisplayvedio_{effect_skill_id}.bundle"
    candidates = [(name, value) for name, value in sources.items() if name.endswith(suffix)]
    if len(candidates) != 1:
        raise FileNotFoundError(f"Expected one original official bundle for effect skill {effect_skill_id}; found {len(candidates)}.")
    bundle_name, source = candidates[0]
    bundle_path = BUNDLE_ROOT / next(row["file"] for row in json.loads(BUNDLE_INVENTORY.read_text(encoding="utf-8"))["bundles"]
                                     if row.get("name") == bundle_name and row.get("manifest") == "854_156383")
    if source.get("manifest") != "854_156383":
        raise ValueError(f"The bundle source is not pinned to 854_156383: {bundle_name}")
    bundle_bytes = bundle_path.read_bytes()
    bundle_hash = sha256(bundle_bytes)
    if bundle_hash != source.get("sha256"):
        raise ValueError(f"The cached original bundle differs from its verified inventory: {bundle_name}")

    environment = UnityPy.load(str(bundle_path))
    clips = [obj for obj in environment.objects if obj.type.name == "VideoClip"]
    if len(clips) != 1:
        raise ValueError(f"Expected one VideoClip in {bundle_path.name}; found {len(clips)}.")
    clip_object = clips[0]
    clip = clip_object.read()
    expected_name = f"SkillDisplayVedio_{effect_skill_id}"
    if clip.m_Name != expected_name or not clip.m_OriginalPath.endswith(expected_name + ".mp4"):
        raise ValueError(f"The VideoClip identity does not match effect skill {effect_skill_id}.")
    resource = clip.m_ExternalResources
    payload = get_resource_data(resource.m_Source, clip_object.assets_file,
                                resource.m_Offset, resource.m_Size)
    if not payload.startswith(b"\x00\x00\x00") or b"ftyp" not in payload[:16]:
        raise ValueError(f"The extracted VideoClip payload is not an MP4: {bundle_path.name}")
    return payload, {
        "bundle": bundle_name,
        "bundle_sha256": bundle_hash,
        "origin": source.get("origin"),
        "url": source.get("url"),
        "manifest": source.get("manifest"),
        "apk_entry": source.get("apk_entry"),
        "clip_name": clip.m_Name,
        "clip_path": clip.m_OriginalPath,
        "clip_path_id": str(clip_object.path_id),
        "width": int(clip.Width),
        "height": int(clip.Height),
        "frame_count": int(clip.m_FrameCount),
        "frame_rate": float(clip.m_FrameRate),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Verify all extracted videos and manifests without writing.")
    args = parser.parse_args()
    rows = load_official_rows()
    sources = load_sources()
    effects = sorted({int(row["EffectSkillId"]) for row in rows})
    payloads: dict[int, tuple[bytes, dict]] = {}
    for effect_skill_id in effects:
        payloads[effect_skill_id] = extract_video(effect_skill_id, sources)

    source_rows = []
    for row in rows:
        effect_skill_id = int(row["EffectSkillId"])
        payload, source = payloads[effect_skill_id]
        output = f"Character/SkillSkinVideos/{effect_skill_id}.mp4"
        output_path = OUTPUT_ROOT / f"{effect_skill_id}.mp4"
        if args.check:
            if not output_path.is_file() or sha256(output_path.read_bytes()) != sha256(payload):
                raise ValueError(f"The runtime skill appearance video is missing or changed: {output}")
        else:
            output_path.parent.mkdir(parents=True, exist_ok=True)
            output_path.write_bytes(payload)
        source_rows.append({
            "skill_skin_id": int(row["ClassId"]),
            "skill_id": int(row["SkillId"]),
            "effect_skill_id": effect_skill_id,
            "skin_type": int(row["SkinType"]),
            "path": output,
            "sha256": sha256(payload),
            "source": source,
        })

    runtime = {
        "manifest": "854_156383",
        "videos": [
            {"effect_skill_id": effect_id, "path": f"Character/SkillSkinVideos/{effect_id}.mp4",
             "sha256": sha256(payloads[effect_id][0])}
            for effect_id in effects
        ],
    }
    provenance = {
        "manifest": "854_156383",
        "source_table": ".tools/source-packages/official-data/skill_skin.g.bytes",
        "source_table_sha256": sha256((ROOT / ".tools/source-packages/official-data/skill_skin.g.bytes").read_bytes()),
        "constructor": ".tools/source-packages/official-code/common-source/Common/TbSkillSkinInfo.cs",
        "constructor_sha256": sha256((ROOT / ".tools/source-packages/official-code/common-source/Common/TbSkillSkinInfo.cs").read_bytes()),
        "runtime_index": "unity/Assets/StreamingAssets/Character/skill-skin-videos.json",
        "bundles": len(effects),
        "rows": source_rows,
    }
    if args.check:
        if json.loads(RUNTIME_INDEX.read_text(encoding="utf-8")) != runtime:
            raise ValueError("The runtime skill skin video index differs from the official rules.")
        if json.loads(SOURCE_MANIFEST.read_text(encoding="utf-8")) != provenance:
            raise ValueError("The skill skin video provenance differs from the extracted inputs.")
    else:
        RUNTIME_INDEX.parent.mkdir(parents=True, exist_ok=True)
        SOURCE_MANIFEST.parent.mkdir(parents=True, exist_ok=True)
        RUNTIME_INDEX.write_text(json.dumps(runtime, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        SOURCE_MANIFEST.write_text(json.dumps(provenance, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Verified manifest 854_156383: {len(effects)} skill videos, {len(rows)} skill skin rows.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
