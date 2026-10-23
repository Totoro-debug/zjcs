"""Stage verified official Pet Gacha performance audio and timing data for Unity."""
import hashlib
import json
import sys
from pathlib import Path, PurePosixPath


ROOT = Path(__file__).resolve().parents[1]
PERFORMANCE = ROOT / "assets/official/pet-gacha-performance/performance.json"
SOURCE_MANIFEST = ROOT / "assets/reference/pet-gacha-performance/source-manifest.json"
OUTPUT_ROOT = ROOT / "unity/Assets/StreamingAssets/PetGacha/PerformanceAudio"
SPRITE_ROOT = ROOT / "unity/Assets/StreamingAssets/PetGacha/sprites/performance"
CATALOG_PATH = ROOT / "unity/Assets/StreamingAssets/PetGacha/performance.json"
PANEL_PATH = ROOT / "unity/Assets/StreamingAssets/PetGacha/performance-ui.json"
EFFECTS_PATH = ROOT / "unity/Assets/StreamingAssets/PetGacha/performance-effects.json"
RUNTIME_MANIFEST = ROOT / "assets/reference/pet-gacha-performance/runtime-assets-manifest.json"
EFFECT_TEXTURE_ROOT = ROOT / "unity/Assets/StreamingAssets/PetGacha/sprites/effects"
EXTRACTED_ROOT = ROOT / ".tools/source-packages/extracted"
UNITYPY_ROOT = ROOT / ".tools/character-art-python"
VERSION = "854_156383"
PET_GACHA_PARTICLE_SHADER = "-1464726073327024352"


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def stable_guid(relative):
    return hashlib.md5(("zjcs:" + relative.replace("\\", "/").lower()).encode("utf-8")).hexdigest()


def verify_source(relative, expected, label):
    path = ROOT / relative
    if not path.is_file():
        raise FileNotFoundError(label + " is missing: " + relative)
    payload = path.read_bytes()
    if sha256(payload) != expected:
        raise ValueError(label + " hash differs from the pinned manifest: " + relative)
    return path, payload


def fields_for(panel, component):
    components = panel.get("bindings", {}).get(component, [])
    if len(components) != 1 or not isinstance(components[0].get("fields"), dict):
        raise ValueError("The official Pet Gacha performance component is missing: " + component)
    return components[0]["fields"]


def animation_config(config):
    actions = []
    for action in config.get("ActionAnims", []):
        event = action.get("AkEvent", {}).get("WwiseObjectReference", {}).get("event", "")
        animations = [
            {"name": row.get("AnimName", ""), "loop_times": row.get("LoopTimes", 0),
             "exit_time": row.get("ExitTime", 0.0), "mix_duration": row.get("NextMixDuration", 0.0)}
            for row in action.get("AnimDatas", [])
        ]
        if not event or not animations or any(not row["name"] for row in animations):
            raise ValueError("The official Pet Gacha action animation binding is incomplete.")
        actions.append({"sound": event, "animations": animations})
    if not actions:
        raise ValueError("The official Pet Gacha action animation list is empty.")
    return {
        "protected_time": config.get("ProtectedTime", 0.0),
        "interval_time": config.get("IntervalTime", 0.0),
        "scene_animation": config.get("SceneAnimName", ""),
        "actions": actions,
    }


def effect_bindings(rows):
    result = []
    for row in rows or []:
        resource = row.get("Item")
        quality = row.get("Quality")
        if not isinstance(resource, str) or quality is None:
            raise ValueError("An official Pet Gacha quality effect binding is incomplete.")
        result.append({"quality": quality, "resource": resource})
    return result


def quality_sounds(fields):
    buffer = fields.get("mItemShowSoundDict", {}).get("mSerializeBuffer", [])
    result = []
    for row in buffer:
        event = row.get("Value", {}).get("WwiseObjectReference", {}).get("event", "")
        quality = row.get("Key")
        if not event or quality is None:
            raise ValueError("An official Pet Gacha rank sound binding is incomplete.")
        result.append({"quality": quality, "event": event})
    return result


def validate_effect_material(bundle_name, path_id, material_name, expected_shader, cache):
    if str(UNITYPY_ROOT) not in sys.path:
        sys.path.insert(0, str(UNITYPY_ROOT))
    try:
        import UnityPy
    except ImportError as error:
        raise RuntimeError("The pinned UnityPy 1.25.4 runtime is unavailable for effect shader verification.") from error
    if getattr(UnityPy, "__version__", None) != "1.25.4":
        raise RuntimeError("Pet Gacha effect verification requires UnityPy 1.25.4.")
    bundle_path = EXTRACTED_ROOT / bundle_name
    if not bundle_path.is_file():
        raise FileNotFoundError("An official Pet Gacha material bundle is not extracted: " + bundle_name)
    bundle_digest = hashlib.md5(bundle_path.read_bytes()).hexdigest()
    if bundle_digest != expected_shader["bundle_hash"]:
        raise ValueError("An official Pet Gacha material bundle hash differs: " + bundle_name)
    if bundle_name not in cache:
        cache[bundle_name] = UnityPy.load(str(bundle_path))
    material_object = next((row for row in cache[bundle_name].objects
                            if str(row.path_id) == str(path_id)), None)
    if material_object is None or material_object.type.name != "Material":
        raise ValueError("An official Pet Gacha effect material is missing: " + material_name)
    material = material_object.read()
    shader_id = str(material.m_Shader.path_id) if material.m_Shader else ""
    if material.m_Name != material_name or shader_id != PET_GACHA_PARTICLE_SHADER:
        raise ValueError("An official Pet Gacha effect has an unsupported material or shader: " + material_name)
    return bundle_path, bundle_digest, shader_id


def write_meta(path, folder=False):
    relative = path.relative_to(ROOT / "unity/Assets").as_posix()
    meta = Path(str(path) + ".meta")
    expected = (
        "fileFormatVersion: 2\n"
        "guid: " + stable_guid(relative) + "\n"
        + ("folderAsset: yes\n" if folder else "")
        + "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    )
    if meta.exists() and meta.read_text(encoding="utf-8") != expected:
        raise ValueError("Refusing to overwrite modified Unity metadata: " + str(meta))
    if not meta.exists():
        meta.write_text(expected, encoding="utf-8", newline="\n")
    return meta


def write_generated(path, payload, previous_outputs):
    relative = path.relative_to(ROOT).as_posix()
    if path.exists():
        current = path.read_bytes()
        previous = previous_outputs.get(relative)
        if previous is None or sha256(current) != previous:
            if current != payload:
                raise ValueError("Refusing to overwrite an untracked or modified generated output: " + relative)
        elif current == payload:
            return
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(payload)


def main():
    source_manifest = json.loads(SOURCE_MANIFEST.read_text(encoding="utf-8"))
    if source_manifest.get("manifest") != VERSION or not isinstance(source_manifest.get("outputs"), dict):
        raise ValueError("The Pet Gacha source manifest is not pinned to the supported client build.")
    performance_relative = PERFORMANCE.relative_to(ROOT).as_posix()
    performance_uri = "res://" + performance_relative
    expected_performance_hash = source_manifest["outputs"].get(performance_uri)
    if not expected_performance_hash:
        raise ValueError("The official performance catalog is not listed in its source manifest.")
    _, performance_bytes = verify_source(performance_relative, expected_performance_hash,
                                         "Official Pet Gacha performance catalog")
    data = json.loads(performance_bytes)
    stage = data.get("panels", {}).get("stage", {})
    reward = data.get("panels", {}).get("reward", {})
    stage_fields = fields_for(stage, "PetGachaResultPerformer")
    reward_fields = fields_for(reward, "PetGachaRewardItemPerformer")
    audio = data.get("audio", {})
    settings = data.get("settings", {})

    event_records = []
    staged_audio = {}
    for event_name, row in audio.items():
        files = row.get("files", [])
        if row.get("stop"):
            event_records.append({"event": event_name, "file": "", "stop": True})
            continue
        if len(files) != 1 or not files[0].startswith("res://assets/official/pet-gacha-performance/audio/"):
            raise ValueError("An official Pet Gacha sound event has no unique local OGG: " + event_name)
        source_relative = files[0].removeprefix("res://")
        expected_hash = source_manifest["outputs"].get(files[0])
        if not expected_hash:
            raise ValueError("An official Pet Gacha OGG is not listed in its source manifest: " + files[0])
        source_path, payload = verify_source(source_relative, expected_hash, "Official Pet Gacha OGG")
        filename = PurePosixPath(source_relative).name
        target = OUTPUT_ROOT / filename
        target_relative = target.relative_to(ROOT / "unity/Assets").as_posix()
        if target.exists() and target.read_bytes() != payload:
            raise ValueError("An existing Pet Gacha OGG differs from the pinned official file: " + str(target))
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists():
            target.write_bytes(payload)
        audio_meta = write_meta(target)
        staged_audio[target.relative_to(ROOT).as_posix()] = {
            "bytes": len(payload), "sha256": sha256(payload),
            "meta_sha256": sha256(audio_meta.read_bytes()),
        }
        event_records.append({"event": event_name, "file": "PerformanceAudio/" + filename, "stop": False})

    if not staged_audio:
        raise ValueError("No official Pet Gacha performance audio was staged.")
    write_meta(OUTPUT_ROOT, folder=True)
    stage_root = stage.get("panel", {}).get("root", {})
    staged_sprites = {}
    sprite_sources = {}
    stack = [stage_root]
    while stack:
        node = stack.pop()
        sprite = (node.get("image") or {}).get("sprite") or {}
        sprite_uri = sprite.get("file", "")
        if sprite_uri:
            prefix = "res://assets/official/pet-gacha-performance/sprites/"
            if not sprite_uri.startswith(prefix):
                raise ValueError("The Pet Gacha stage references an unpinned sprite: " + sprite_uri)
            source_relative = sprite_uri.removeprefix("res://")
            expected_hash = source_manifest["outputs"].get(sprite_uri)
            if not expected_hash:
                raise ValueError("A Pet Gacha stage sprite is not listed in its source manifest: " + sprite_uri)
            source_path, payload = verify_source(source_relative, expected_hash, "Official Pet Gacha stage sprite")
            filename = PurePosixPath(source_relative).name
            if filename in sprite_sources and sprite_sources[filename] != sprite_uri:
                raise ValueError("Pet Gacha stage sprite filenames collide: " + filename)
            sprite_sources[filename] = sprite_uri
            target = SPRITE_ROOT / filename
            if target.exists() and target.read_bytes() != payload:
                raise ValueError("An existing Pet Gacha stage sprite differs from the pinned file: " + str(target))
            target.parent.mkdir(parents=True, exist_ok=True)
            if not target.exists():
                target.write_bytes(payload)
            sprite_meta = write_meta(target)
            staged_sprites[target.relative_to(ROOT).as_posix()] = {
                "bytes": len(payload), "sha256": sha256(payload),
                "meta_sha256": sha256(sprite_meta.read_bytes()),
            }
            sprite["file"] = "performance/" + filename
        stack.extend(node.get("children", []))
    if not staged_sprites:
        raise ValueError("The official Pet Gacha stage panel has no locally bound sprites.")
    write_meta(SPRITE_ROOT, folder=True)

    resource_manifest = source_manifest.get("resources", {})
    effect_resources = set()
    for key in ("RewardDropSfxs", "RewardCollectSfxs", "RewardShowSfxs", "RewardQualitySfxs"):
        effect_resources.update(row.get("Item") for row in settings.get(key, []) if row.get("Item"))
    effect_resources.update(value for value in (settings.get("ItemTranslateSfx"),
        settings.get("RewardCollectFeedbackSfx")) if value)
    if not effect_resources:
        raise ValueError("The official Pet Gacha performance effect list is empty.")
    unitypy_cache = {}
    effect_bundles = {}
    staged_effect_textures = {}
    effect_prefabs = {}
    for resource_id in sorted(effect_resources):
        prefab = data.get("prefabs", {}).get(resource_id)
        if prefab is None or not prefab.get("effects"):
            raise ValueError("An official Pet Gacha effect prefab is missing: " + resource_id)
        effect_prefabs[resource_id] = prefab
        for effect in prefab["effects"]:
            for emitter in effect.get("emitters", []):
                material = emitter.get("material", {})
                source = material.get("source", {})
                bundle_name = source.get("bundle", "")
                bundle_path, bundle_digest, shader_id = validate_effect_material(
                    bundle_name, source.get("path_id"), material.get("name", ""), source, unitypy_cache)
                effect_bundles[bundle_name] = {
                    "bundle_hash": bundle_digest,
                    "sha256": sha256(bundle_path.read_bytes()),
                    "shader_path_id": shader_id,
                }
                emitter["shader_path_id"] = shader_id
                for texture in material.get("textures", {}).values():
                    uri = texture.get("file", "")
                    if uri.endswith("/map-particles/white.tres"):
                        texture["file"] = "builtin/white"
                        continue
                    expected_hash = resource_manifest.get(uri, {}).get("sha256")
                    if not expected_hash:
                        raise ValueError("An official Pet Gacha effect texture is not pinned: " + uri)
                    source_relative = uri.removeprefix("res://")
                    _, payload = verify_source(source_relative, expected_hash,
                                               "Official Pet Gacha effect texture")
                    texture_relative = PurePosixPath(source_relative).relative_to(
                        "assets/official/pet-gacha-performance").as_posix()
                    target = EFFECT_TEXTURE_ROOT / Path(*PurePosixPath(texture_relative).parts)
                    if target.exists() and target.read_bytes() != payload:
                        raise ValueError("An existing Pet Gacha effect texture differs from the pinned file: " + str(target))
                    target.parent.mkdir(parents=True, exist_ok=True)
                    if not target.exists():
                        target.write_bytes(payload)
                    texture_meta = write_meta(target)
                    staged_effect_textures[target.relative_to(ROOT).as_posix()] = {
                        "bytes": len(payload), "sha256": sha256(payload),
                        "meta_sha256": sha256(texture_meta.read_bytes()),
                    }
                    texture["file"] = "effects/" + texture_relative
        for trail in prefab.get("trails", []):
            material = trail.get("material", {})
            source = material.get("source", {})
            bundle_name = source.get("bundle", "")
            bundle_path, bundle_digest, shader_id = validate_effect_material(
                bundle_name, source.get("path_id"), material.get("name", ""), source, unitypy_cache)
            effect_bundles[bundle_name] = {
                "bundle_hash": bundle_digest,
                "sha256": sha256(bundle_path.read_bytes()),
                "shader_path_id": shader_id,
            }
            trail["shader_path_id"] = shader_id
            for texture in material.get("textures", {}).values():
                uri = texture.get("file", "")
                if uri.endswith("/map-particles/white.tres"):
                    texture["file"] = "builtin/white"
                    continue
                expected_hash = resource_manifest.get(uri, {}).get("sha256")
                if not expected_hash:
                    raise ValueError("An official Pet Gacha trail texture is not pinned: " + uri)
                source_relative = uri.removeprefix("res://")
                _, payload = verify_source(source_relative, expected_hash,
                                           "Official Pet Gacha trail texture")
                texture_relative = PurePosixPath(source_relative).relative_to(
                    "assets/official/pet-gacha-performance").as_posix()
                target = EFFECT_TEXTURE_ROOT / Path(*PurePosixPath(texture_relative).parts)
                if target.exists() and target.read_bytes() != payload:
                    raise ValueError("An existing Pet Gacha trail texture differs from the pinned file: " + str(target))
                target.parent.mkdir(parents=True, exist_ok=True)
                if not target.exists():
                    target.write_bytes(payload)
                texture_meta = write_meta(target)
                staged_effect_textures[target.relative_to(ROOT).as_posix()] = {
                    "bytes": len(payload), "sha256": sha256(payload),
                    "meta_sha256": sha256(texture_meta.read_bytes()),
                }
                texture["file"] = "effects/" + texture_relative
    write_meta(EFFECT_TEXTURE_ROOT, folder=True)
    for directory in sorted({path.parent for path in EFFECT_TEXTURE_ROOT.rglob("*.png")}):
        write_meta(directory, folder=True)

    drop_range = data.get("polygon")
    if not isinstance(drop_range, list) or len(drop_range) < 3:
        raise ValueError("The official Pet Gacha reward drop polygon is missing.")
    performer = next((row for row in stage_root.get("children", [])
                      if row.get("name") == "PetGachaResultPerformer"), None)
    if performer is None:
        raise ValueError("The official Pet Gacha result performer panel is missing.")
    stage_nodes = {row.get("name"): row for row in performer.get("children", [])}
    actor = stage_nodes.get("PlayerModel_UI")
    collect_target = stage_nodes.get("CollectTarget")
    if actor is None or collect_target is None:
        raise ValueError("The official Pet Gacha actor or reward collection target is missing.")

    catalog = {
        "manifest": VERSION,
        "single": animation_config(stage_fields["mOneGachaConfig"]),
        "ten": animation_config(stage_fields["mTenGachaConfig"]),
        "hundred": {
            "show_time": stage_fields["mHundredShowTime"],
            "show_count": stage_fields["mHundredShowCount"],
            "drop_interval_frames": stage_fields["mHundredPerformerDropFixedIntervalFrame"],
            "drop_interval_count": stage_fields["mHundredPerformerDropFixedIntervalCount"],
        },
        "collect_effect_time": stage_fields["mCollectEffectTime"],
        "award_floating_item_interval": stage_fields["mAwardFloatingItemInterval"],
        "award_collect_sfx_interval": stage_fields["mAwardCollectSfxInterval"],
        "show_item_interval": stage_fields["mShowItemIntervalTime"],
        "reward": {
            "normal_drop_delay": reward_fields["mDelayTimeDropNormal"],
            "special_drop_delay": reward_fields["mDelayTimeDropSpecial"],
            "item_translate_delay": reward_fields["mDelayTimeItemTranslate"],
            "item_show_delay": reward_fields["mDelayTimeItemShow"],
            "special_animation": reward_fields["mSpecialAwardAnimName"],
            "drop_effects": effect_bindings(settings.get("RewardDropSfxs")),
            "collect_effects": effect_bindings(settings.get("RewardCollectSfxs")),
            "show_effects": effect_bindings(settings.get("RewardShowSfxs")),
            "quality_effects": effect_bindings(settings.get("RewardQualitySfxs")),
            "quality_sounds": quality_sounds(reward_fields),
            "translate_effect": settings.get("ItemTranslateSfx", ""),
            "collect_feedback_effect": settings.get("RewardCollectFeedbackSfx", ""),
        },
        "actor": {
            "anchor_position": actor.get("anchored_position", [0.0, 0.0]),
            "scale": actor.get("scale", [1.0, 1.0, 1.0]),
            "collect_target_position": collect_target.get("anchored_position", [0.0, 0.0]),
        },
        "drop_range": drop_range,
        "sounds": event_records,
        "drop_playable": settings.get("RewardDropPlayable", ""),
        "collect_playable": settings.get("RewardCollectPlayable", ""),
    }
    catalog_bytes = (json.dumps(catalog, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    panel_bytes = (json.dumps({
        "version": VERSION,
        "panels": [{"key": "pet_gacha_stage", "root": stage_root}],
    }, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    effects_bytes = (json.dumps({
        "manifest": VERSION,
        "prefabs": [{"key": key, "root": value} for key, value in sorted(effect_prefabs.items())],
    }, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    previous = {}
    if RUNTIME_MANIFEST.exists():
        prior = json.loads(RUNTIME_MANIFEST.read_text(encoding="utf-8"))
        prior_outputs = prior.get("outputs", {})
        if isinstance(prior_outputs, dict):
            previous = {path: row["sha256"] for path, row in prior_outputs.items()
                        if isinstance(row, dict) and "sha256" in row}
        elif isinstance(prior_outputs, list):
            previous = {row["path"]: row["sha256"] for row in prior_outputs
                        if isinstance(row, dict) and "path" in row and "sha256" in row}
    write_generated(CATALOG_PATH, catalog_bytes, previous)
    catalog_meta = write_meta(CATALOG_PATH)
    write_generated(PANEL_PATH, panel_bytes, previous)
    panel_meta = write_meta(PANEL_PATH)
    write_generated(EFFECTS_PATH, effects_bytes, previous)
    effects_meta = write_meta(EFFECTS_PATH)

    outputs = dict(staged_audio)
    outputs.update(staged_sprites)
    outputs.update(staged_effect_textures)
    outputs[CATALOG_PATH.relative_to(ROOT).as_posix()] = {
        "bytes": len(catalog_bytes), "sha256": sha256(catalog_bytes),
        "meta_sha256": sha256(catalog_meta.read_bytes()),
    }
    outputs[PANEL_PATH.relative_to(ROOT).as_posix()] = {
        "bytes": len(panel_bytes), "sha256": sha256(panel_bytes),
        "meta_sha256": sha256(panel_meta.read_bytes()),
    }
    outputs[EFFECTS_PATH.relative_to(ROOT).as_posix()] = {
        "bytes": len(effects_bytes), "sha256": sha256(effects_bytes),
        "meta_sha256": sha256(effects_meta.read_bytes()),
    }
    source_hashes = {
        performance_relative: sha256(performance_bytes),
        SOURCE_MANIFEST.relative_to(ROOT).as_posix(): sha256(SOURCE_MANIFEST.read_bytes()),
    }
    for bundle_name, row in effect_bundles.items():
        source_hashes[(EXTRACTED_ROOT / bundle_name).relative_to(ROOT).as_posix()] = row["sha256"]
    manifest = {
        "manifest": VERSION,
        "generator": "scripts/prepare_pet_gacha_performance.py",
        "source_manifest": SOURCE_MANIFEST.relative_to(ROOT).as_posix(),
        "inputs": source_hashes,
        "effect_bundles": effect_bundles,
        "outputs": outputs,
    }
    RUNTIME_MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
                                encoding="utf-8", newline="\n")
    print("Prepared official Pet Gacha result timings and", len(staged_audio), "local audio files.")


if __name__ == "__main__":
    main()
