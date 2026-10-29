"""Restore verified 854_156383 equipment rule payloads for Unity."""
import argparse
import hashlib
import json
import os
import tempfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MANIFEST_VERSION = "854_156383"
PAYLOAD = "Assets/Official/Equipment"
SOURCES = (
    ("assets/official/rules.json", "assets/reference/official-city/source-manifest.json",
     "data_sha256.rules.json"),
    ("assets/official/equipment-rules.json", "assets/reference/equipment-fidelity/source-manifest.json",
     "outputs.equipment-rules.json"),
    ("assets/official/fidelity-rules.json", "assets/reference/equipment-completion/source-manifest.json",
     "output_sha256.fidelity-rules.json"),
)
RUNTIME_RULES = "Assets/Official/Equipment/Runtime/equipment-runtime-rules.json"
RUNTIME_MANIFEST = "Assets/Official/Equipment/Runtime/source-manifest.json"
UI_CATALOG = "Assets/Official/Equipment/UI/equipment-ui.json"
UI_MANIFEST = "Assets/Official/Equipment/UI/source-manifest.json"
UI_SOURCE = "assets/official/ui.json"
UI_PANELS = (
    "equipment", "equip_detail", "equip_compare", "item_equip", "item_gem", "equip_row",
    "gem_merge_row", "equip_promote", "equip_promote_cell", "equip_promote_bar",
    "equip_promote_bar_row", "equip_promote_prop", "equip_gem_row", "equip_inherit",
    "equip_inherit_content", "equip_inherit_rule", "equip_select", "equip_select_row",
    "equip_resonance", "equip_resonance_content", "equip_resonance_row", "equip_gem_slot",
    "equip_select_detail", "equip_inherit_prop", "equip_inherit_select_prop", "gem_detail",
    "equip_term", "equip_piece_detail", "equip_choose", "equip_choose_row", "equip_bless_tip",
    "equip_suit_tip", "equip_piece_confirm", "equip_term_skill_detail", "equip_choose_icon",
    "equip_level_tip", "equip_piece_owned_hint", "equip_level_tip_row", "equip_detail_gem",
)
RUNTIME_TABLES = (
    "item", "item_piece_merge", "profession_base", "prop_cfg", "conditions",
    "function_limit", "game_settings", "equip", "equip_type", "equip_rank",
    "equip_upgrade_material", "equip_upgrade_bless", "equip_piece_merge",
    "equip_attributes", "equip_attributes_props", "level_prop_default_value", "gem", "gem_merge",
)
SOURCE_MANIFESTS = (
    "assets/reference/official-city/source-manifest.json",
    "assets/reference/equipment-fidelity/source-manifest.json",
    "assets/reference/equipment-completion/source-manifest.json",
)


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while block := stream.read(1024 * 1024):
            digest.update(block)
    return digest.hexdigest()


def source_hash(manifest, field):
    section, _, key = field.partition(".")
    value = manifest.get(section)
    if isinstance(value, dict):
        value = value.get(key)
    if not isinstance(value, str) or len(value) != 64:
        raise ValueError("Equipment source manifest hash is invalid: " + field)
    return value


def safe_target(unity_root, relative):
    root = unity_root.resolve()
    target = root.joinpath(*Path(relative).parts)
    if target.is_symlink() or not target.resolve().is_relative_to(root):
        raise ValueError("Equipment payload path escapes Unity project: " + relative)
    for parent in target.parents:
        if parent == root:
            break
        if parent.is_symlink() or (hasattr(parent, "is_junction") and parent.is_junction()):
            raise ValueError("Equipment payload path uses a link or junction: " + relative)
    return target


def copy_verified(content, target, expected_hash):
    if target.exists():
        if sha256(target) != expected_hash:
            raise ValueError("Refusing to overwrite changed Unity equipment payload: " + str(target))
        return False
    target.parent.mkdir(parents=True, exist_ok=True)
    handle, temporary_name = tempfile.mkstemp(prefix=".equipment-payload-", dir=target.parent)
    temporary = Path(temporary_name)
    try:
        with os.fdopen(handle, "wb") as stream:
            stream.write(content)
        if sha256(temporary) != expected_hash:
            raise ValueError("Unity equipment payload hash differs: " + str(target))
        temporary.replace(target)
    finally:
        temporary.unlink(missing_ok=True)
    return True


def ensure_metadata(unity_root, relative, folder=False):
    guid = hashlib.sha256(("zjcs-equipment:" + relative).encode("ascii")).hexdigest()[:32]
    if folder:
        body = ("fileFormatVersion: 2\nguid: " + guid +
                "\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
                "  userData:\n  assetBundleName:\n  assetBundleVariant:\n")
    else:
        body = ("fileFormatVersion: 2\nguid: " + guid +
                "\nTextScriptImporter:\n  externalObjects: {}\n  userData:\n"
                "  assetBundleName:\n  assetBundleVariant:\n")
    content = body.encode("ascii")
    target = safe_target(unity_root, relative + ".meta")
    digest = hashlib.sha256(content).hexdigest()
    created = copy_verified(content, target, digest)
    return {"path": relative + ".meta", "bytes": target.stat().st_size,
            "sha256": digest, "restored": created}


def ensure_sprite_metadata(unity_root, relative, sprite):
    guid = hashlib.sha256(("zjcs-equipment:" + relative).encode("ascii")).hexdigest()[:32]
    sprite_id = hashlib.sha256(("zjcs-equipment-sprite:" + relative).encode("ascii")).hexdigest()[:32]
    border = sprite.get("border", [0, 0, 0, 0])
    pivot = sprite.get("pivot", [sprite.get("width", 0) / 2, sprite.get("height", 0) / 2])
    width = max(1, int(sprite.get("width", 1)))
    height = max(1, int(sprite.get("height", 1)))
    pivot_x = float(pivot[0]) / width
    pivot_y = float(pivot[1]) / height
    body = (
        "fileFormatVersion: 2\n"
        "guid: " + guid + "\n"
        "TextureImporter:\n"
        "  internalIDToNameTable: []\n"
        "  externalObjects: {}\n"
        "  serializedVersion: 12\n"
        "  mipmaps:\n"
        "    mipMapMode: 0\n"
        "    enableMipMap: 0\n"
        "    sRGBTexture: 1\n"
        "  isReadable: 0\n"
        "  streamingMipmaps: 0\n"
        "  textureFormat: 1\n"
        "  maxTextureSize: 2048\n"
        "  textureSettings:\n"
        "    serializedVersion: 2\n"
        "    filterMode: 1\n"
        "    aniso: 1\n"
        "    mipBias: 0\n"
        "    wrapU: 1\n"
        "    wrapV: 1\n"
        "    wrapW: 1\n"
        "  nPOTScale: 0\n"
        "  lightmap: 0\n"
        "  compressionQuality: 50\n"
        "  spriteMode: 1\n"
        "  spriteExtrude: 1\n"
        "  spriteMeshType: 1\n"
        "  alignment: 9\n"
        "  spritePivot: {x: " + format(pivot_x, ".9g") + ", y: " + format(pivot_y, ".9g") + "}\n"
        "  spritePixelsToUnits: " + format(float(sprite.get("ppu", 100)), ".9g") + "\n"
        "  spriteBorder: {x: " + format(float(border[0]), ".9g") + ", y: "
        + format(float(border[1]), ".9g") + ", z: " + format(float(border[2]), ".9g")
        + ", w: " + format(float(border[3]), ".9g") + "}\n"
        "  spriteGenerateFallbackPhysicsShape: 0\n"
        "  alphaUsage: 1\n"
        "  alphaIsTransparency: 1\n"
        "  textureType: 8\n"
        "  textureShape: 1\n"
        "  spriteSheet:\n"
        "    serializedVersion: 2\n"
        "    sprites: []\n"
        "    outline: []\n"
        "    physicsShape: []\n"
        "    bones: []\n"
        "    spriteID: " + sprite_id + "\n"
        "    internalID: 21300000\n"
        "    vertices: []\n"
        "    indices: \n"
        "    edges: []\n"
        "    weights: []\n"
        "    secondaryTextures: []\n"
        "  platformSettings: []\n"
        "  spritePackingTag: \n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    ).encode("ascii")
    target = safe_target(unity_root, relative + ".meta")
    digest = hashlib.sha256(body).hexdigest()
    copied = copy_verified(body, target, digest)
    return {"path": relative + ".meta", "bytes": target.stat().st_size,
            "sha256": digest, "restored": copied}


def restore_ui(source_root, unity_root, manifests, restored):
    ui_path = source_root / UI_SOURCE
    completion_path, completion, completion_digest = manifests[
        "assets/reference/equipment-completion/source-manifest.json"]
    expected_ui = completion.get("output_sha256", {}).get("ui.json")
    if not isinstance(expected_ui, str):
        raise ValueError("Official equipment UI catalog source hash is missing from its manifest.")
    actual_ui = sha256(ui_path)
    city_path, city, city_digest = manifests["assets/reference/official-city/source-manifest.json"]
    fidelity_path, fidelity, fidelity_digest = manifests[
        "assets/reference/equipment-fidelity/source-manifest.json"]
    feedback_path = source_root / "assets/reference/equipment-feedback/source-manifest.json"
    feedback = json.loads(feedback_path.read_text(encoding="utf-8"))
    feedback_digest = sha256(feedback_path)
    if feedback.get("manifest") != MANIFEST_VERSION:
        raise ValueError("Equipment feedback prefab source manifest is not pinned to " + MANIFEST_VERSION)
    original_ui = json.loads(ui_path.read_text(encoding="utf-8"))
    missing = [name for name in UI_PANELS if name not in original_ui]
    if missing:
        raise ValueError("Official equipment UI panels are missing: " + ", ".join(missing))

    declarations = {}
    declarations.update(city.get("panel_prefabs", {}))
    declarations.update(fidelity.get("prefabs", {}))
    declarations.update(completion.get("templates", {}))
    declarations["equip_inherit_prop"] = feedback.get("inheritance_prefab", {})
    for name in UI_PANELS:
        actual = original_ui[name].get("source", {})
        expected = declarations.get(name, {})
        if not expected or any(actual.get(key) != expected.get(key)
                               for key in ("bundle", "bundle_hash", "path_id", "prefab")):
            raise ValueError("Official equipment prefab source differs from its pinned manifest: " + name)

    sprites = {}

    def visit(value):
        if isinstance(value, dict):
            sprite = value.get("sprite")
            if isinstance(sprite, dict) and isinstance(sprite.get("file"), str):
                resource = sprite["file"]
                if resource not in sprites:
                    source_relative = resource.removeprefix("res://")
                    metadata = city.get("resources", {}).get(resource)
                    if not isinstance(metadata, dict) or not metadata.get("sha256"):
                        raise ValueError("Official equipment sprite source is unrecorded: " + resource)
                    source = source_root / Path(source_relative)
                    if sha256(source) != metadata["sha256"]:
                        raise ValueError("Official equipment sprite differs from its source manifest: " + resource)
                    sprite_name = Path(source_relative).name
                    target_relative = "Assets/Official/Equipment/UI/Sprites/" + sprite_name
                    target = safe_target(unity_root, target_relative)
                    copied = copy_verified(source.read_bytes(), target, metadata["sha256"])
                    sprites[resource] = {
                        "path": target_relative,
                        "source": source_relative,
                        "sha256": metadata["sha256"],
                        "sprite": {key: sprite[key] for key in ("name", "width", "height", "border", "ppu", "pivot")
                                   if key in sprite},
                        "restored": copied,
                    }
                value["sprite"]["file"] = "Sprites/" + Path(sprites[resource]["path"]).name
            for child in value.values():
                visit(child)
        elif isinstance(value, list):
            for child in value:
                visit(child)

    panels = {name: original_ui[name] for name in UI_PANELS}
    visit(panels)
    output_bytes = (json.dumps(panels, ensure_ascii=False, sort_keys=True,
                               separators=(",", ":"), allow_nan=False) + "\n").encode("utf-8")
    output_hash = hashlib.sha256(output_bytes).hexdigest()
    target = safe_target(unity_root, UI_CATALOG)
    copied = copy_verified(output_bytes, target, output_hash)
    restored.append({"path": UI_CATALOG, "bytes": target.stat().st_size,
                     "sha256": output_hash, "restored": copied})

    for resource, record in sprites.items():
        restored.append({"path": record["path"], "bytes": safe_target(unity_root, record["path"]).stat().st_size,
                         "sha256": record["sha256"], "restored": record["restored"]})
        restored.append(ensure_sprite_metadata(unity_root, record["path"], record["sprite"]))

    ui_manifest = {
        "manifest": MANIFEST_VERSION,
        "source": UI_SOURCE,
        "source_sha256": actual_ui,
        "aggregate_manifest_sha256": expected_ui,
        "source_manifest": "assets/reference/equipment-completion/source-manifest.json",
        "source_manifest_sha256": completion_digest,
        "resource_manifest": "assets/reference/official-city/source-manifest.json",
        "resource_manifest_sha256": city_digest,
        "prefab_manifest_sha256": fidelity_digest,
        "feedback_manifest_sha256": feedback_digest,
        "output": {"path": UI_CATALOG, "bytes": len(output_bytes), "sha256": output_hash},
        "panels": list(UI_PANELS),
        "sprites": [{key: value for key, value in record.items() if key != "restored"}
                    for record in sorted(sprites.values(), key=lambda value: value["path"])],
    }
    manifest_bytes = (json.dumps(ui_manifest, ensure_ascii=False, sort_keys=True,
                                 indent=2) + "\n").encode("utf-8")
    manifest_hash = hashlib.sha256(manifest_bytes).hexdigest()
    manifest_path = safe_target(unity_root, UI_MANIFEST)
    manifest_copied = copy_verified(manifest_bytes, manifest_path, manifest_hash)
    restored.append({"path": UI_MANIFEST, "bytes": manifest_path.stat().st_size,
                     "sha256": manifest_hash, "restored": manifest_copied})
    restored.append(ensure_metadata(unity_root, "Assets/Official/Equipment/UI", folder=True))
    restored.append(ensure_metadata(unity_root, "Assets/Official/Equipment/UI/Sprites", folder=True))
    restored.append(ensure_metadata(unity_root, UI_CATALOG))
    restored.append(ensure_metadata(unity_root, UI_MANIFEST))


def restore(source_root=ROOT, unity_root=ROOT / "unity"):
    source_root = Path(source_root).resolve()
    unity_root = Path(unity_root).resolve()
    manifests = {}
    for relative in SOURCE_MANIFESTS:
        path = source_root / Path(relative)
        manifest = json.loads(path.read_text(encoding="utf-8"))
        if manifest.get("manifest") != MANIFEST_VERSION:
            raise ValueError("Equipment source manifest is not pinned to " + MANIFEST_VERSION + ": " + relative)
        manifests[relative] = (path, manifest, sha256(path))

    restored = []
    source_payloads = {}
    input_hashes = {}
    for source_name, manifest_name, field in SOURCES:
        source = source_root / Path(source_name)
        _, manifest, manifest_digest = manifests[manifest_name]
        expected = source_hash(manifest, field)
        actual = sha256(source)
        if actual != expected:
            raise ValueError("Equipment output differs from its source manifest: " + source_name)
        source_payloads[source_name] = json.loads(source.read_text(encoding="utf-8"))
        input_hashes[source_name] = {
            "sha256": actual,
            "source_manifest": manifest_name,
            "source_manifest_sha256": manifest_digest,
            "source_field": field,
        }

    rules = source_payloads["assets/official/rules.json"]
    extra = source_payloads["assets/official/equipment-rules.json"]
    fidelity = source_payloads["assets/official/fidelity-rules.json"]
    runtime = {
        "manifest": MANIFEST_VERSION,
        "rules": {name: rules.get(name, []) for name in RUNTIME_TABLES},
        "tables": {
            **extra["tables"],
            "system_level_limit": fidelity["system_level_limit"],
            "system_level_gap_limit": fidelity["system_level_gap_limit"],
        },
        "props": extra["props"],
        "input_sha256": {name: value["sha256"] for name, value in input_hashes.items()},
    }
    runtime_bytes = (json.dumps(runtime, ensure_ascii=False, sort_keys=True,
                                separators=(",", ":"), allow_nan=False) + "\n").encode("utf-8")
    runtime_digest = hashlib.sha256(runtime_bytes).hexdigest()
    runtime_path = safe_target(unity_root, RUNTIME_RULES)
    runtime_copied = copy_verified(runtime_bytes, runtime_path, runtime_digest)
    restored.append({"path": RUNTIME_RULES, "bytes": runtime_path.stat().st_size,
                     "sha256": runtime_digest, "restored": runtime_copied})

    runtime_manifest = {
        "manifest": MANIFEST_VERSION,
        "output": {"path": RUNTIME_RULES, "bytes": len(runtime_bytes), "sha256": runtime_digest},
        "inputs": [{"path": name, "sha256": digest}
                   for name, digest in runtime["input_sha256"].items()],
        "tables": sorted(runtime["tables"]),
    }
    runtime_manifest_bytes = (json.dumps(runtime_manifest, ensure_ascii=False, sort_keys=True,
                                         indent=2) + "\n").encode("utf-8")
    runtime_manifest_digest = hashlib.sha256(runtime_manifest_bytes).hexdigest()
    runtime_manifest_path = safe_target(unity_root, RUNTIME_MANIFEST)
    runtime_manifest_copied = copy_verified(runtime_manifest_bytes, runtime_manifest_path,
                                            runtime_manifest_digest)
    restored.append({"path": RUNTIME_MANIFEST, "bytes": runtime_manifest_path.stat().st_size,
                     "sha256": runtime_manifest_digest, "restored": runtime_manifest_copied})

    for relative, (source, manifest, digest) in manifests.items():
        name = Path(relative).parent.name + "-source-manifest.json"
        target_name = PAYLOAD + "/" + name
        target = safe_target(unity_root, target_name)
        records = []
        for payload_name, manifest_name, field in SOURCES:
            if manifest_name != relative:
                continue
            records.append({"path": payload_name, "source_field": field,
                            "output_sha256": source_hash(manifest, field)})
        content = (json.dumps({"manifest": MANIFEST_VERSION,
                               "source_manifest": relative,
                               "source_manifest_sha256": digest,
                               "outputs": records}, ensure_ascii=False,
                   sort_keys=True, indent=2) + "\n").encode("utf-8")
        content_digest = hashlib.sha256(content).hexdigest()
        copied = copy_verified(content, target, content_digest)
        restored.append({"path": target_name, "bytes": target.stat().st_size,
                         "sha256": content_digest, "restored": copied})

    module = {
        "manifest": MANIFEST_VERSION,
        "runtime_rules": {"path": RUNTIME_RULES, "sha256": runtime_digest},
        "inputs": [{"path": name, **value} for name, value in input_hashes.items()],
        "source_manifests": [
            {"path": relative, "sha256": digest}
            for relative, (_, _, digest) in manifests.items()
        ],
    }
    content = (json.dumps(module, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
    module_path = safe_target(unity_root, PAYLOAD + "/source-manifest.json")
    digest = hashlib.sha256(content).hexdigest()
    copied = copy_verified(content, module_path, digest)
    restored.append({"path": PAYLOAD + "/source-manifest.json", "bytes": module_path.stat().st_size,
                     "sha256": digest, "restored": copied})
    restored.append(ensure_metadata(unity_root, "Assets/Official/Equipment", folder=True))
    restored.append(ensure_metadata(unity_root, "Assets/Official/Equipment/Runtime", folder=True))
    for name in [RUNTIME_RULES,
            RUNTIME_MANIFEST, PAYLOAD + "/official-city-source-manifest.json",
            PAYLOAD + "/equipment-fidelity-source-manifest.json",
            PAYLOAD + "/equipment-completion-source-manifest.json",
            PAYLOAD + "/source-manifest.json"]:
        restored.append(ensure_metadata(unity_root, name))
    restore_ui(source_root, unity_root, manifests, restored)
    return restored


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", type=Path, default=ROOT)
    parser.add_argument("--unity-root", type=Path, default=ROOT / "unity")
    args = parser.parse_args()
    print(json.dumps(restore(args.source_root, args.unity_root), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
