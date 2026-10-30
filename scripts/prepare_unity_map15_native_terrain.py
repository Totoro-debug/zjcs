"""Install the 871 Map15 terrain prefab dependency closure as Unity-native assets."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import uuid

import yaml

from prepare_unity_map15_native_assets import GUID, GUID_REFERENCE, build_index, close


ROOT = Path(__file__).resolve().parents[1]
RENDERING = ROOT / "assets/official/map15-rendering.json"
DESTINATION = ROOT / "unity/Assets/Official/World/Map/15/NativeTerrain"
RECEIPT = ROOT / "assets/reference/official-map/map15-native-terrain-source-manifest.json"
MASK_SHADER_SOURCE = "defaultpackage_unityshaders.bundle"
MASK_SHADERS = (
    dict(original="Universal Render Pipeline/2D/Sprite-WithTransparentMask", path_id=-5015297347798012165,
         sha256="449927bf12bfd3a373a272714306f0b1905e714ab602ace7200d1faa0cd1db93",
         native_name="SpriteWithTransparentMask", indexes=[2, 3]),
    dict(original="Unlit/TransparentMask", path_id=-4098911902558482847,
         sha256="fb326cb78177780eac674a3e46fbfab98c3299693809f19b167e6cc6b2e9bc91",
         native_name="UnlitTransparentMask", indexes=[1]),
)
MASK_SHADER_DIRECTORY = ROOT / "unity/Assets/Game/Runtime/Official/World/NativeWater/Shaders"
SCRIPT_TARGETS = {
    "CullingGroupWrapper": "Game/Runtime/Official/World/NativeEnvironment/CullingGroupWrapper.cs",
    "FollowSceneActiveSelfController": "Game/Runtime/Official/World/NativeEnvironment/FollowSceneActiveSelfController.cs",
    "MapEffect": "Game/Runtime/Official/World/NativeEnvironment/MapEffect.cs",
    "MapEnvEffect": "Game/Runtime/Official/World/NativeEnvironment/MapEnvEffect.cs",
    "MapGroundRoot": "Game/Runtime/Official/World/NativeEnvironment/MapGroundRoot.cs",
    "MapGroundTile": "Game/Runtime/Official/World/NativeTerrain/MapGroundTile.cs",
    "MapRegionDescItemUI": "Game/Runtime/Official/World/NativeMap14Labels/MapRegionDescItemUI.cs",
    "MapRegionDescSupportUI": "Game/Runtime/Official/World/NativeMap14Labels/MapRegionDescSupportUI.cs",
    "MapRegionStrengthDisplayUI": "Game/Runtime/Official/World/NativeMap14Labels/MapRegionStrengthDisplayUI.cs",
    "PartitionController": "Game/Runtime/Official/World/NativeTerrain/PartitionController.cs",
    "PartitionElement": "Game/Runtime/Official/World/NativeTerrain/PartitionElement.cs",
    "PartitionGroup": "Game/Runtime/Official/World/NativeTerrain/PartitionGroup.cs",
    "PartitionLayerAssigner": "Game/Runtime/Official/World/NativeTerrain/PartitionLayerAssigner.cs",
    "PartitionLoader": "Game/Runtime/Official/World/NativeTerrain/PartitionLoader.cs",
    "ShowRemainTimeInfo": "Game/Runtime/Official/World/NativeMap14Labels/ShowRemainTimeInfo.cs",
    "TransparentMask": "Game/Runtime/Official/World/NativeWater/TransparentMask.cs",
    "UIColorByLevel": "Game/Runtime/Official/World/NativeMap14Labels/UIColorByLevel.cs",
    "WorldMapEditorManager": "Game/Runtime/Official/World/NativeEnvironment/WorldMapEditorManager.cs",
    "PartialSubmersion": "Game/Runtime/Official/World/NativeWater/PartialSubmersion.cs",
    "Pond": "Game/Runtime/Official/World/NativeWater/Pond.cs",
    "WaterController": "Game/Runtime/Official/World/NativeWater/WaterController.cs",
    "WaterMaskGenerator": "Game/Runtime/Official/World/NativeWater/WaterMaskGenerator.cs",
    "WaterMaskRenderer": "Game/Runtime/Official/World/NativeWater/WaterMaskRenderer.cs",
    "MultiLanguageText": "Game/Runtime/Official/World/NativeLabelUI/MultiLanguageText.cs",
    "TextMeshProColorProxy": "Game/Runtime/Official/World/NativeLabelUI/TextMeshProColorProxy.cs",
    "SkeletonAnimation": "Spine/Runtime/spine-unity/Components/SkeletonAnimation.cs",
    "Light2D": "Packages/com.unity.render-pipelines.universal/Runtime/2D/Light2D.cs",
    "UniversalAdditionalCameraData": "Packages/com.unity.render-pipelines.universal/Runtime/UniversalAdditionalCameraData.cs",
    "TextMeshPro": "Library/PackageCache/com.unity.textmeshpro@3.0.6/Scripts/Runtime/TextMeshPro.cs",
    "TMPEffect": "Game/Runtime/Official/World/NativeText/TMPEffect.cs",
    "ContentSizeFitter": "Library/PackageCache/com.unity.ugui@1.0.0/Runtime/UI/Core/Layout/ContentSizeFitter.cs",
}
TEXT_ASSETS = {".anim", ".asset", ".controller", ".mat", ".meta", ".overridecontroller", ".prefab", ".rendertexture", ".shader", ".shadervariants", ".spriteatlas", ".terrainlayer"}


def digest(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def shader_name(path: Path) -> str | None:
    if path.suffix != ".asset":
        return None
    with path.open("rb") as stream:
        header = stream.read(2048).decode("utf-8", errors="strict")
    if "--- !u!48 " not in header:
        return None
    match = re.search(r"(?m)^  m_Name: (.*)$", header)
    if match is None:
        raise ValueError("AssetRipper Shader name is missing: " + str(path))
    return match[1].strip().strip('"')


def new_guid(relative: str) -> str:
    return uuid.uuid5(uuid.NAMESPACE_URL, "zjcs-map15-native-terrain:" + relative).hex


def read_guid(meta_path: Path) -> str:
    match = GUID.search(meta_path.read_text(encoding="utf-8"))
    if match is None:
        raise ValueError("Unity asset has no GUID: " + str(meta_path))
    return match[1]


def validate_imported_meta(meta_path: Path, proposed: str, expected_guid: str) -> str:
    actual = meta_path.read_text(encoding="utf-8")
    if read_guid(meta_path) != expected_guid:
        raise ValueError("Existing Unity metadata has a different GUID: " + str(meta_path))
    expected_data = yaml.safe_load(proposed)
    actual_data = yaml.safe_load(actual)
    expected_importers = {key: value for key, value in expected_data.items() if key.endswith("Importer")}
    actual_importers = {key: value for key, value in actual_data.items() if key.endswith("Importer")}
    if expected_importers.keys() != actual_importers.keys():
        raise ValueError("Existing Unity metadata has a different importer: " + str(meta_path))
    if "TextureImporter" in expected_importers:
        expected_texture = expected_importers["TextureImporter"]
        actual_texture = actual_importers["TextureImporter"]
        for key in ("textureType", "textureShape", "spriteMode", "spritePixelsToUnits", "alphaIsTransparency",
                    "isReadable", "textureSettings"):
            if expected_texture.get(key) != actual_texture.get(key):
                raise ValueError("Unity changed an official Map15 texture setting " + key + ": " + str(meta_path))
        expected_platforms = {item.get("buildTarget"): item for item in expected_texture.get("platformSettings", [])}
        actual_platforms = {item.get("buildTarget"): item for item in actual_texture.get("platformSettings", [])}
        for target, expected_platform in expected_platforms.items():
            if actual_platforms.get(target) != expected_platform:
                raise ValueError("Unity changed an official Map15 texture platform setting " + target + ": " + str(meta_path))
    return actual


def convert_mask_shaders(receipt_path: Path, source_receipt: dict, write: bool) -> list[dict]:
    from prepare_unity_map13_water_shaders import translate
    import UnityPy

    source = next((row for row in source_receipt["inputs"] if row["name"] == MASK_SHADER_SOURCE), None)
    if source is None:
        raise ValueError("The verified 871 shader bundle is absent from the converter receipt")
    bundle_path = receipt_path.parent / source["output"]
    raw = bundle_path.read_bytes()
    if len(raw) != source["decoded_size"] or digest(raw) != source["decoded_sha256"]:
        raise ValueError("The original 871 shader bundle differs from its decoded receipt")
    environment = UnityPy.load(raw)
    records = []
    for spec in MASK_SHADERS:
        matches = [obj for obj in environment.objects if obj.type.name == "Shader" and obj.path_id == spec["path_id"]]
        if len(matches) != 1:
            raise ValueError("The official Map15 mask shader identity is not unique: " + spec["original"])
        original = matches[0]
        if digest(original.get_raw_data()) != spec["sha256"]:
            raise ValueError("The official Map15 mask shader payload changed: " + spec["original"])
        shader = original.read()
        if shader.m_ParsedForm.m_Name != spec["original"]:
            raise ValueError("The official Map15 mask shader name changed: " + spec["original"])
        converted, programs = translate(shader, "Map15" + spec["native_name"])
        converted = converted.replace("Zjcs/Official/World/Map13/Map15" + spec["native_name"],
                                      "Zjcs/Official/World/Map15/" + spec["native_name"])
        if sorted(program["index"] for program in programs) != spec["indexes"]:
            raise ValueError("The original Map15 mask shader GLES variants changed: " + spec["original"])
        if spec["native_name"] == "SpriteWithTransparentMask" and (
                "_TransparentMaskTex" not in converted or "USE_SHAPE_LIGHT_TYPE_0" not in converted):
            raise ValueError("The original Map15 sprite-mask variants were not fully translated")
        if spec["native_name"] == "UnlitTransparentMask" and "\"LIGHTMODE\"=\"TransparentMask\"" not in converted:
            raise ValueError("The original TransparentMask render pass tag was not translated")
        output = converted.encode("utf-8")
        target = MASK_SHADER_DIRECTORY / ("Map15" + spec["native_name"] + ".shader")
        guid = uuid.uuid5(uuid.NAMESPACE_URL, "zjcs-map15-native-terrain:shader:" + spec["original"]).hex
        meta_text = ("fileFormatVersion: 2\n"
                     "guid: " + guid + "\n"
                     "ShaderImporter:\n"
                     "  externalObjects: {}\n"
                     "  userData:\n"
                     "  assetBundleName:\n"
                     "  assetBundleVariant:\n")
        meta_path = Path(str(target) + ".meta")
        actual_meta = meta_text
        if write:
            target.parent.mkdir(parents=True, exist_ok=True)
            if target.exists() and target.read_bytes() != output:
                raise ValueError("Refusing to overwrite a different converted Map15 shader: " + str(target))
            if meta_path.exists():
                actual_meta = validate_imported_meta(meta_path, meta_text, guid)
            else:
                meta_path.write_text(meta_text, encoding="utf-8", newline="\n")
            target.write_bytes(output)
        elif target.exists() or meta_path.exists():
            if not target.is_file() or not meta_path.is_file() or target.read_bytes() != output:
                raise ValueError("An existing converted Map15 shader differs from the reproducible output")
            actual_meta = validate_imported_meta(meta_path, meta_text, guid)
        converter_path = ROOT / "scripts/prepare_unity_map13_water_shaders.py"
        program_reader_path = ROOT / "scripts/official_shader_programs.py"
        records.append(dict(name=spec["original"], source=dict(bundle=MASK_SHADER_SOURCE,
                        path_id=str(spec["path_id"]), payload_sha256=spec["sha256"],
                        bundle_sha256=source["decoded_sha256"], bundle_receipt_sha256=digest(receipt_path.read_bytes())),
                        programs=[dict(index=program["index"], keywords=program["keywords"],
                                       sha256=digest(program["source"].encode("utf-8")), source=program["source"])
                                  for program in programs],
                        converter=dict(path=converter_path.relative_to(ROOT).as_posix(), sha256=digest(converter_path.read_bytes()),
                                       program_reader=program_reader_path.relative_to(ROOT).as_posix(),
                                       program_reader_sha256=digest(program_reader_path.read_bytes())),
                        output=target.relative_to(ROOT).as_posix(),
                        output_sha256=digest(output), proposed_output_meta_sha256=digest(meta_text.encode("utf-8")),
                        output_meta_sha256=digest(actual_meta.encode("utf-8")), guid=guid))
    return records


def unity_script_path(relative: str) -> Path:
    if relative.startswith(("Packages/", "Library/")):
        return ROOT / "unity" / relative
    return ROOT / "unity/Assets" / relative


def terrain_roots(assets: Path) -> list[Path]:
    rendering = json.loads(RENDERING.read_text(encoding="utf-8"))
    roots = []
    for map_id, row in rendering["terrains"].items():
        source = row.get("ground")
        if not source:
            continue
        if not source.startswith("Assets/"):
            raise ValueError("Map15 terrain source path is not rooted at Assets: " + source)
        path = assets.joinpath(*source.split("/")[1:])
        if not path.is_file():
            raise FileNotFoundError("871 Map15 terrain prefab is absent: " + str(path))
        roots.append(path)
    if len(roots) != 14:
        raise ValueError("Map15 terrain catalog must contain the 14 official ground roots")
    return roots


def load_shader_targets() -> dict[str, Path]:
    targets = {}
    receipt_root = ROOT / "assets/reference/official-map"
    preferred_receipts = [
        "map14-entity-shaders-source-manifest.json",
        "map13-entity-shaders-source-manifest.json",
        "map14-scene-shaders-source-manifest.json",
        "map13-scene-shaders-source-manifest.json",
        "map13-water-shaders-source-manifest.json",
    ]
    receipts = [receipt_root / name for name in preferred_receipts]
    receipts.extend(path for path in sorted(receipt_root.glob("*-shader*-source-manifest.json"))
                    if path.name not in preferred_receipts)
    for receipt in receipts:
        try:
            document = json.loads(receipt.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            continue
        if document.get("manifest") != "871_156598":
            continue
        for record in document.get("outputs", []):
            name = record.get("original_shader") or record.get("shader_name")
            output = record.get("path")
            if not name or not output or not output.endswith(".shader"):
                continue
            target = ROOT / "unity" / output.removeprefix("unity/")
            if not target.is_file() or not Path(str(target) + ".meta").is_file():
                continue
            if digest(target.read_bytes()) != record.get("sha256"):
                raise ValueError("Verified Windows shader output changed: " + str(target))
            targets.setdefault(name, target)
    return targets


def close_terrain(assets: Path) -> tuple[list[Path], list[dict], dict[str, Path]]:
    index = build_index(assets)
    roots = terrain_roots(assets)
    files, unresolved = close(assets, roots, index)
    if unresolved:
        raise ValueError("Unresolved GUIDs remain in original Map15 terrain: " + json.dumps(unresolved[:8]))
    return files, [dict(path=path.relative_to(assets).as_posix(), sha256=digest(path.read_bytes()),
                        meta_sha256=digest(Path(str(path) + ".meta").read_bytes())) for path in roots], index


def verify_input_receipt(path: Path) -> dict:
    receipt = json.loads(path.read_text(encoding="utf-8"))
    if receipt.get("manifest") != "871_156598" or not receipt.get("inputs"):
        raise ValueError("Map15 terrain input receipt is not the expanded 871_156598 bundle closure")
    seen = set()
    for item in receipt["inputs"]:
        if item.get("manifest") != "871_156598" or not item.get("decoded_sha256"):
            raise ValueError("Map15 terrain input has no decoded 871 receipt")
        if item["name"] in seen:
            raise ValueError("Duplicate 871 bundle input in terrain receipt: " + item["name"])
        seen.add(item["name"])
        source = path.parent / item["output"]
        if not source.is_file() or source.stat().st_size != item["decoded_size"] or digest(source.read_bytes()) != item["decoded_sha256"]:
            raise ValueError("Map15 terrain converter input differs from its 871 receipt: " + item["name"])
    return receipt


def shader_for(path: Path, targets: dict[str, Path]) -> Path:
    name = shader_name(path)
    if name is None and path.suffix == ".shader":
        match = re.search(r'(?m)^\s*Shader\s+"([^"]+)"', path.read_text(encoding="utf-8", errors="strict"))
        name = match[1] if match else None
    if not name or name not in targets:
        raise ValueError("Map15 terrain shader has no verified Windows binding: " + str(path) + " (" + str(name) + ")")
    return targets[name]


def build(export_assets: Path, input_receipt: Path, destination: Path = DESTINATION,
          receipt_path: Path = RECEIPT, write: bool = True) -> dict:
    assets = export_assets.resolve()
    if assets.name != "Assets" or not assets.is_dir():
        raise ValueError("--export-assets must point to AssetRipper ExportedProject/Assets")
    source_receipt = verify_input_receipt(input_receipt)
    mask_shaders = convert_mask_shaders(input_receipt, source_receipt, write)
    files, roots, index = close_terrain(assets)
    shaders = load_shader_targets()
    mask_by_name = {record["name"]: record for record in mask_shaders}
    shaders.update({name: ROOT / record["output"] for name, record in mask_by_name.items()})
    replacements, script_records, shader_records = {}, [], []
    excluded = set()
    for path in files:
        relative = path.relative_to(assets).as_posix()
        meta_path = Path(str(path) + ".meta")
        old_guid = read_guid(meta_path)
        if path.suffix == ".cs":
            name = path.stem
            target = unity_script_path(SCRIPT_TARGETS.get(name, "__missing__"))
            if name not in SCRIPT_TARGETS or not target.is_file() or not Path(str(target) + ".meta").is_file():
                raise ValueError("Map15 terrain MonoScript has no migrated Unity binding: " + name)
            new = read_guid(Path(str(target) + ".meta"))
            replacements[old_guid] = new
            script_records.append(dict(name=name, source=relative, source_sha256=digest(path.read_bytes()),
                                       original_script_guid=old_guid, unity_script=target.relative_to(ROOT).as_posix(),
                                       unity_script_guid=new, unity_script_sha256=digest(target.read_bytes())))
            excluded.add(path)
        elif path.suffix == ".shader" or shader_name(path) is not None:
            target = shader_for(path, shaders)
            mask_binding = next((record for record in mask_shaders if target.as_posix().endswith(record["output"].removeprefix("unity/"))), None)
            new = mask_binding["guid"] if mask_binding else read_guid(Path(str(target) + ".meta"))
            replacements[old_guid] = new
            shader_records.append(dict(name=shader_name(path) or path.stem, source=relative,
                                       source_sha256=digest(path.read_bytes()), unity_shader=target.relative_to(ROOT).as_posix(),
                                       unity_shader_guid=new, unity_shader_sha256=mask_binding["output_sha256"]
                                       if mask_binding else digest(target.read_bytes())))
            excluded.add(path)
        else:
            replacements[old_guid] = new_guid(relative)
    existing_guids = set()
    for meta in (ROOT / "unity/Assets").rglob("*.meta"):
        if meta.is_relative_to(destination):
            continue
        match = GUID.search(meta.read_text(encoding="utf-8", errors="ignore"))
        if match:
            existing_guids.add(match[1])
    assigned = [value for value in replacements.values() if value not in {record["unity_script_guid"] for record in script_records}
                and value not in {record["unity_shader_guid"] for record in shader_records}]
    if len(assigned) != len(set(assigned)) or set(assigned) & existing_guids:
        raise ValueError("Deterministic Map15 terrain GUID collides with an existing Unity asset")

    def remap(text: str) -> str:
        text = GUID_REFERENCE.sub(lambda match: "guid: " + replacements.get(match[1], match[1]), text)
        shader_guids = {record["unity_shader_guid"] for record in shader_records}
        return re.sub(r"(\{fileID: 4800000, guid: )([a-f0-9]{32})(, type: )2(\})",
                      lambda match: match[1] + match[2] + match[3] + ("3" if match[2] in shader_guids else "2") + match[4], text)

    output_records = []
    for path in files:
        if path in excluded or path.suffix == ".cs":
            continue
        relative = path.relative_to(assets).as_posix()
        if path.suffix not in {".anim", ".asset", ".controller", ".mat", ".overridecontroller", ".png", ".prefab", ".rendertexture", ".shadervariants", ".spriteatlas", ".terrainlayer"}:
            raise ValueError("Unclassified Map15 terrain closure asset: " + relative)
        target = destination / Path(relative)
        raw = path.read_bytes()
        meta_raw = Path(str(path) + ".meta").read_bytes()
        if path.suffix in TEXT_ASSETS:
            raw = remap(raw.decode("utf-8", errors="strict")).encode("utf-8")
        meta_text = remap(meta_raw.decode("utf-8", errors="strict"))
        meta_text = re.sub(r"(?m)^timeCreated: \d+\n", "", meta_text)
        target_meta = Path(str(target) + ".meta")
        actual_meta = meta_text
        if write:
            target.parent.mkdir(parents=True, exist_ok=True)
            if target.exists() and target.read_bytes() != raw:
                raise ValueError("Refusing to overwrite a different Unity terrain asset: " + str(target))
            if target_meta.exists():
                actual_meta = validate_imported_meta(target_meta, meta_text, new_guid(relative))
            else:
                target_meta.write_text(meta_text, encoding="utf-8", newline="\n")
            target.write_bytes(raw)
        elif target.exists() or target_meta.exists():
            if not target.is_file() or not target_meta.is_file() or target.read_bytes() != raw:
                raise ValueError("An existing Map15 terrain output differs from the reproducible output: " + str(target))
            actual_meta = validate_imported_meta(target_meta, meta_text, new_guid(relative))
        output_records.append(dict(source=relative, source_sha256=digest(path.read_bytes()),
                                  source_meta_sha256=digest(Path(str(path) + ".meta").read_bytes()),
                                  output=target.relative_to(ROOT).as_posix() if target.is_relative_to(ROOT) else target.as_posix(), output_sha256=digest(raw),
                                  proposed_output_meta_sha256=digest(meta_text.encode("utf-8")),
                                  output_meta_sha256=digest(actual_meta.encode("utf-8"))))

    folder_records = []
    if write:
        folders = [destination, *sorted(path for path in destination.rglob("*") if path.is_dir())]
        for folder in folders:
            identity = folder.relative_to(destination).as_posix()
            guid = new_guid("folder:" + identity)
            meta_path = Path(str(folder) + ".meta")
            meta_text = ("fileFormatVersion: 2\n"
                         "guid: " + guid + "\n"
                         "folderAsset: yes\n"
                         "DefaultImporter:\n"
                         "  externalObjects: {}\n"
                         "  userData:\n"
                         "  assetBundleName:\n"
                         "  assetBundleVariant:\n")
            if meta_path.exists():
                actual_meta = validate_imported_meta(meta_path, meta_text, guid)
            else:
                meta_path.write_text(meta_text, encoding="utf-8", newline="\n")
            folder_records.append(dict(path=meta_path.relative_to(ROOT).as_posix(), guid=guid,
                                       proposed_sha256=digest(meta_text.encode("utf-8")),
                                       sha256=digest(actual_meta.encode("utf-8"))))

    manifest = dict(format=1, manifest="871_156598", mapClassIds=[int(key) for key in json.loads(RENDERING.read_text())["terrains"]],
                    root_count=len(roots), roots=roots, closure_files=len(files), unresolved_guids=[],
                    input_receipt=dict(format="871_156598 expanded CAB input receipt",
                                       sha256=digest(input_receipt.read_bytes()),
                                       requested_bundles=source_receipt["requested_bundles"],
                                       bundle_inputs=[dict(name=row["name"], url=row.get("url"), sha256=row["raw_sha256"],
                                                           bytes=row["raw_size"]) for row in source_receipt["inputs"]]),
                    generator=dict(path=Path(__file__).resolve().relative_to(ROOT).as_posix(),
                                   sha256=digest(Path(__file__).read_bytes())),
                    scripts=script_records, shaders=shader_records, converted_shaders=mask_shaders, outputs=output_records,
                    folders=folder_records,
                    operation="Copy the exact AssetRipper 871 dependency closure, retain Unity fileIDs and importer settings, deterministically remap asset GUIDs, bind every MonoScript to the existing migrated Unity script or package, and replace Android shader references only with verified Windows shader assets.")
    if write:
        receipt_path.parent.mkdir(parents=True, exist_ok=True)
        receipt_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    return manifest


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--export-assets", type=Path, required=True)
    parser.add_argument("--input-receipt", type=Path, required=True)
    parser.add_argument("--output-root", type=Path, default=DESTINATION)
    parser.add_argument("--receipt", type=Path, default=RECEIPT)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()
    result = build(args.export_assets, args.input_receipt, args.output_root, args.receipt, write=not args.dry_run)
    print("Map15 terrain:", result["root_count"], "roots,", result["closure_files"], "closure files,",
          len(result["outputs"]), "Unity assets,", len(result["scripts"]), "script bindings,",
          len(result["shaders"]), "shader bindings")


if __name__ == "__main__":
    main()
