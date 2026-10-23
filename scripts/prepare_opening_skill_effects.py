"""Prepare verified battle-presentation particle effects used by the opening record.

This first native runtime slice includes only cast effects whose class id is
actually present in the pinned CreatePlayerFightRecord. Effect JSON and sprite
hashes are checked against battle-effects-source-manifest.json before copying.
"""
from __future__ import annotations

import gzip
import hashlib
import json
import re
from pathlib import Path, PurePosixPath

ROOT = Path(__file__).resolve().parents[1]
VERSION = "854_156383"
RECORD = ROOT / "unity/Assets/StreamingAssets/Opening/opening-record.bytes"
EFFECTS = ROOT / "assets/official/battle-presentation/effects.json"
EFFECT_ROOT = ROOT / "assets/official/battle-presentation/effects"
MANIFEST = ROOT / "assets/reference/official-map/battle-effects-source-manifest.json"
SPRITES = ROOT / "assets/official/sprites"
TARGET = ROOT / "unity/Assets/StreamingAssets/Opening/skill-effects.json"
TEXTURES = ROOT / "unity/Assets/Official/Opening/SkillEffects/Resources/OpeningSkillEffects/Textures"
REFERENCE = ROOT / "assets/reference/character-creation/opening-skill-effects-source-manifest.json"

TEXTURE_META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  textureType: 0
  textureShape: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    wrapU: 0
    wrapV: 0
    wrapW: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def guid(path: str) -> str:
    return hashlib.md5(("zjcs-official:" + path).encode("utf-8")).hexdigest()


def ensure_folder_meta(directory: Path) -> None:
    relative = directory.relative_to(ROOT / "unity").as_posix()
    meta = directory.with_name(directory.name + ".meta")
    if meta.exists():
        return
    meta.write_text(
        "fileFormatVersion: 2\nguid: " + guid(relative) + "\nfolderAsset: yes\nDefaultImporter:\n"
        "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n",
        encoding="utf-8")


def ensure_payload_meta(path: Path) -> None:
    relative = path.relative_to(ROOT / "unity").as_posix()
    meta = path.with_name(path.name + ".meta")
    if meta.exists():
        return
    meta.write_text(
        "fileFormatVersion: 2\nguid: " + guid(relative) + "\nDefaultImporter:\n"
        "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n",
        encoding="utf-8")


def main() -> int:
    for directory in (TEXTURES, TEXTURES.parent, TEXTURES.parent.parent, TEXTURES.parent.parent.parent):
        directory.mkdir(parents=True, exist_ok=True)
        ensure_folder_meta(directory)
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    effects_bytes = EFFECTS.read_bytes()
    expected = manifest["outputs"].get("battle-presentation/effects.json")
    if manifest.get("manifest") != VERSION or not expected or sha(effects_bytes) != expected:
        raise ValueError("Battle effect catalog differs from its pinned source manifest")
    catalog = json.loads(effects_bytes.decode("utf-8"))
    record = json.loads(gzip.decompress(RECORD.read_bytes()).decode("utf-8", "replace"))
    used_classes = set()
    damage_classes = set()
    for round_data in record.get("RoundMsgList", []):
        for step in round_data.get("StepList", []):
            if step.get("MsgType") != "GAME_ENTITY_ADD": continue
            entity = step.get("Data", {}).get("Entity", {})
            class_id = entity.get("ClassId")
            if class_id is None: continue
            class_id = int(class_id)
            used_classes.add(class_id)
            if any(c.get("$type", "").split(".")[-1].split(",")[0] == "FightDamageComponent"
                   for c in entity.get("ComponentList", [])):
                damage_classes.add(class_id)
    if 11150 not in used_classes:
        raise ValueError("Opening record does not contain cast entity ClassId 11150")
    # First migration slice: one frequent cast effect plus the damage effects
    # that are explicitly present in the pinned opening record.
    selected = {}
    for key in ["cast:11150"] + ["damage:" + str(value) for value in sorted(damage_classes)]:
        if key in catalog: selected[key] = catalog[key]
    if "cast:11150" not in selected:
        raise ValueError("Pinned battle effect catalog has no cast:11150 entry")

    resources = manifest["resources"]
    output_textures = {}
    output_effects = []
    effect_inputs = {}
    for key, events in selected.items():
        class_id = int(key.split(":", 1)[1])
        for event in events:
            if event.get("kind") != "particles":
                continue
            uri = event.get("file", "")
            prefix = "res://assets/official/battle-presentation/effects/"
            if not uri.startswith(prefix):
                raise ValueError("Unexpected effect payload URI: " + uri)
            relative = uri[len("res://"):]
            source = ROOT / PurePosixPath(relative)
            manifest_path = relative.removeprefix("assets/official/")
            source_hash = manifest["outputs"].get(manifest_path)
            data = json.loads(source.read_text(encoding="utf-8"))
            if not source_hash or sha(source.read_bytes()) != source_hash:
                raise ValueError("Effect payload hash differs from manifest: " + manifest_path)
            effect_inputs[manifest_path] = {
                "sha256": source_hash,
                "systems": [{"path": particle.get("path", ""),
                             "source": particle.get("source", {})} for particle in data],
            }
            particle_records = []
            for particle in data:
                material = particle.get("material") or {}
                main = (material.get("textures") or {}).get("_MainTex") or {}
                texture_uri = main.get("file", "")
                texture_name = ""
                if texture_uri:
                    resource = resources.get(texture_uri)
                    if resource is None:
                        raise ValueError("Effect texture has no verified resource record: " + texture_uri)
                    sprite_name = PurePosixPath(texture_uri).name
                    sprite_source = SPRITES / sprite_name
                    payload = sprite_source.read_bytes()
                    if sha(payload) != resource.get("sha256"):
                        raise ValueError("Effect texture hash differs from source manifest: " + sprite_name)
                    texture_name = sprite_source.stem
                    relative_target = "Assets/Official/Opening/SkillEffects/Resources/OpeningSkillEffects/Textures/" + sprite_source.name
                    target = TEXTURES / sprite_source.name
                    target.parent.mkdir(parents=True, exist_ok=True)
                    if not target.exists() or sha(target.read_bytes()) != resource["sha256"]:
                        target.write_bytes(payload)
                    meta = target.with_name(target.name + ".meta")
                    if not meta.exists():
                        meta.write_text(TEXTURE_META.format(guid=guid(relative_target)), encoding="utf-8")
                    output_textures[sprite_name] = {"path": relative_target, "sha256": resource["sha256"],
                                                    "source": texture_uri, "bundle": resource["bundle"],
                                                    "bundle_hash": resource.get("bundle_hash", ""),
                                                    "path_id": resource.get("path_id", ""),
                                                    "object": resource.get("object", "")}
                particle_copy = dict(particle)
                particle_copy["textureResource"] = texture_name
                particle_records.append(particle_copy)
            output_effects.append({
                "classId": class_id,
                "effectType": key.split(":", 1)[0],
                "key": key,
                "eventTime": float(event.get("time", 0.0)),
                "eventSpeed": float(event.get("speed", 1.0)),
                "prefab": relative,
                "prefab_sha256": source_hash,
                "playable_field": event.get("field", "myPlayablePrefab"),
                "particles": particle_records,
            })
    output = {
        "manifest": VERSION,
        "record_sha256": sha(RECORD.read_bytes()),
        "effects_catalog_sha256": expected,
        "effect_manifest_sha256": sha(MANIFEST.read_bytes()),
        "effects": output_effects,
        "textures": output_textures,
    }
    data = (json.dumps(output, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
    TARGET.parent.mkdir(parents=True, exist_ok=True)
    TARGET.write_bytes(data)
    ensure_payload_meta(TARGET)
    output_hashes = {TARGET.relative_to(ROOT).as_posix(): sha(data)}
    for entry in output_textures.values():
        output_hashes[entry["path"]] = entry["sha256"]
    reference = {
        "manifest": VERSION,
        "operation": "Pinned battle-presentation cast/damage particle payloads for the opening record; "
                     "native ParticleSystem runtime adapter preserves source transforms, main/emission/shape, "
                     "bursts, main texture bindings and verified source hashes",
        "inputs": {
            "record": {"path": RECORD.relative_to(ROOT).as_posix(), "sha256": sha(RECORD.read_bytes())},
            "effect_catalog": {"path": EFFECTS.relative_to(ROOT).as_posix(), "sha256": expected},
            "effect_source_manifest": {"path": MANIFEST.relative_to(ROOT).as_posix(),
                                        "sha256": sha(MANIFEST.read_bytes())},
            "effect_payloads": effect_inputs,
        },
        "effects": [{"key": entry["key"], "effect_type": entry["effectType"],
                     "class_id": entry["classId"], "particle_systems": len(entry["particles"]),
                     "prefab": entry["prefab"], "prefab_sha256": entry["prefab_sha256"]}
                    for entry in output_effects],
        "textures": output_textures,
        "outputs": output_hashes,
    }
    reference_data = (json.dumps(reference, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8")
    REFERENCE.parent.mkdir(parents=True, exist_ok=True)
    REFERENCE.write_bytes(reference_data)
    print(json.dumps({"effects": len(output_effects), "particleSystems": sum(len(e["particles"]) for e in output_effects),
                      "textures": len(output_textures), "payloadBytes": len(data),
                      "payloadSha256": sha(data), "manifest": REFERENCE.relative_to(ROOT).as_posix(),
                      "selected": [e["key"] for e in output_effects]}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
