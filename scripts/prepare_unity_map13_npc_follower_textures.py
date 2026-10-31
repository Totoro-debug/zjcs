"""Restore the exact official atlas pages used by Map13 story followers."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / ".tools/map13-entity-native-cache-871_156598"
python = CACHE / "python"
if (python / "UnityPy/__init__.py").is_file():
    sys.path.insert(0, str(python))

from prepare_unity_map13_entity_inputs import CACHE as ENTITY_CACHE, RECEIPT as INPUT_RECEIPT
from prepare_unity_map13_entity_textures import decode
from prepare_unity_map13_native_scene import native_texture
from prepare_unity_map13_sources import encode, sha, source_path, write
from prepare_unity_bundle_inputs import serialized_files
from prepare_unity_map13_water_components import import_meta
from official_apk import ROOT as REPOSITORY_ROOT
import UnityPy


DESTINATION = REPOSITORY_ROOT / "unity/Assets/Official/World/Map/13/NativeNpcFollowerTextures"
CATALOG = REPOSITORY_ROOT / "unity/Assets/Game/Resources/Map13/Native/npc-follower-texture-catalog.asset"
RECEIPT = REPOSITORY_ROOT / "assets/reference/official-map/map13-npc-follower-textures-source-manifest.json"
VALIDATION = REPOSITORY_ROOT / "unity/Assets/Game/Editor/Map13/OfficialMap13NpcFollowerAcceptance.cs"
TEXTURES = (
    (8402, "Assets/AppearanceAssets/Npc/npc_middle_model/Atlas/High/npc_8402.png"),
    (8440, "Assets/AppearanceAssets/Npc/npc_big_model/Atlas/High/npc_8440.png"),
    (8440, "Assets/AppearanceAssets/Npc/npc_big_model/Atlas/High/pWeapon_100_8440.png"),
    (8441, "Assets/AppearanceAssets/Npc/npc_middle_model/Atlas/High/npc_8441.png"),
    (8442, "Assets/AppearanceAssets/Npc/npc_middle_model/Atlas/High/npc_8442.png"),
)


def asset_meta(path: Path, guid: str) -> None:
    body = (f"fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n"
            "  externalObjects: {}\n  mainObjectFileID: 2800000\n  userData:\n"
            "  assetBundleName:\n  assetBundleVariant:\n").encode()
    write(Path(str(path) + ".meta"), body)


def clean_yaml(data: bytes) -> bytes:
    return b"\n".join(line.rstrip() for line in data.splitlines()) + b"\n"


def build() -> None:
    input_bytes = subprocess.run(["git", "show", "HEAD:" + INPUT_RECEIPT.relative_to(ROOT).as_posix()],
                                 cwd=ROOT, check=True, stdout=subprocess.PIPE).stdout
    committed_inputs = json.loads(input_bytes)
    inputs = json.loads(INPUT_RECEIPT.read_bytes())
    if inputs["manifest"] != "871_156598" or inputs["mapClassId"] != 13:
        raise ValueError("Wrong original Map13 entity source version")
    committed_dynamic = committed_inputs["selection"]["dynamic_textures"]
    working_dynamic = inputs["selection"]["dynamic_textures"]
    dynamic = committed_dynamic
    bindings = dynamic["bindings"]
    working_bindings = working_dynamic["bindings"]
    input_by_name = {row["name"]: row for row in inputs["inputs"]}
    template_path = REPOSITORY_ROOT / "unity/Assets/Official/World/Map/13/NativeEntityTextures/008cecc391c151a8b89279210ee46fcf/194505744627608095.texture.asset"
    template = template_path.read_text(encoding="utf8").replace("--- !u!28 &2800000\n", "--- !u!28 &1\n")
    if template.count("--- !u!28 &1\n") != 1 or "  m_TextureFormat: 4\n" not in template:
        raise ValueError("Existing native RGBA32 texture serialization reference changed")
    output_records, catalog_bindings = [], []
    for class_id, official_path in TEXTURES:
        matches = [row for row in bindings if row["asset"]["path"] == official_path]
        if len(matches) != 1 or matches[0]["role"] != "targetTextureReference":
            raise ValueError("Original follower atlas page does not have one exact dynamic binding: " + official_path)
        binding = matches[0]
        working_matches = [row for row in working_bindings if row["asset"]["path"] == official_path]
        if len(working_matches) != 1 or working_matches[0] != binding:
            raise ValueError("Working source receipt differs from its committed official page binding: " + official_path)
        source = binding["source"]
        input_row = input_by_name.get(source["bundle"])
        if input_row is None or input_row["output"] != source["bundle"]:
            raise ValueError("Original follower atlas bundle is absent from the converted input closure")
        input_path = ENTITY_CACHE / "inputs" / input_row["output"]
        if sha(input_path.read_bytes()) != input_row["converter_input_sha256"]:
            raise ValueError("Original follower atlas bundle changed after conversion")
        environment = UnityPy.load(input_path.read_bytes())
        found = []
        wanted_id = int(source["path_id"])
        for file in (file for bundle in environment.files.values() for file in serialized_files(bundle)):
            for obj in file.objects.values():
                if obj.path_id == wanted_id:
                    found.append((file.name, obj))
        if len(found) != 1:
            raise ValueError("Original follower atlas object path ID is ambiguous: " + official_path)
        cab, obj = found[0]
        if obj.type.name != "Texture2D" or sha(obj.get_raw_data()) != binding["payload_sha256"]:
            raise ValueError("Original follower atlas object identity or payload differs: " + official_path)
        texture = obj.read(check_read=True)
        rgba, mips, profile, channels = decode(texture)
        if channels != 4 or len(mips) != texture.m_MipCount:
            raise ValueError("Original follower atlas mip layout is incomplete")
        serialized = clean_yaml(native_texture(template, texture, rgba))
        texture_name = Path(official_path).stem
        target_name = str(class_id) + ("" if texture_name == "npc_" + str(class_id)
                                       else "-" + texture_name)
        target = DESTINATION / (target_name + ".texture.asset")
        write(target, serialized)
        guid = hashlib.md5(("zjcs-map13-official-follower-texture:" + source["bundle"] + ":" + str(wanted_id)).encode()).hexdigest()
        asset_meta(target, guid)
        output_records.append(dict(classId=class_id, officialPath=official_path, source=binding,
            cab=cab, pathId=str(wanted_id), sourceObjectSha256=sha(obj.get_raw_data()),
            decodedRgbaSha256=sha(rgba), decodeProfile=profile, channelCount=channels, mips=mips,
            output=dict(path=source_path(target), sha256=sha(target.read_bytes()),
                        metaSha256=sha(Path(str(target) + ".meta").read_bytes()))))
        catalog_bindings.append((official_path, guid))
    for folder in [DESTINATION, *sorted(path for path in DESTINATION.rglob("*") if path.is_dir())]:
        import_meta(folder, "DefaultImporter", folder=True)
    script_meta = REPOSITORY_ROOT / "unity/Assets/Game/Runtime/Offline/World/NativeMap13/NativeMap13AssetCatalog.cs.meta"
    script_guid = re.search(r"(?m)^guid: ([0-9a-f]{32})$", script_meta.read_text(encoding="utf8"))[1]
    rows = "\n".join(f"  - OfficialPath: {path}\n    Asset: {{fileID: 2800000, guid: {guid}, type: 2}}"
                      for path, guid in catalog_bindings)
    catalog_text = ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n"
        "MonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
        "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n"
        "  m_Name: npc-follower-texture-catalog\n  m_EditorClassIdentifier: \n  Bindings:\n" + rows + "\n").encode()
    write(CATALOG, clean_yaml(catalog_text))
    catalog_guid = hashlib.md5(b"zjcs-map13-npc-follower-texture-catalog").hexdigest()
    write(Path(str(CATALOG) + ".meta"),
        (f"fileFormatVersion: 2\nguid: {catalog_guid}\nNativeFormatImporter:\n"
         "  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData:\n"
         "  assetBundleName:\n  assetBundleVariant:\n").encode())
    write(RECEIPT, encode(dict(format=1, mapClassId=13, manifest=inputs["manifest"],
        inputReceipt=dict(path=source_path(INPUT_RECEIPT), sha256=sha(input_bytes)),
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        validation=dict(path=source_path(VALIDATION), sha256=sha(VALIDATION.read_bytes())),
        serializationReference=dict(path=source_path(template_path), sha256=sha(template_path.read_bytes())),
        catalog=dict(path=source_path(CATALOG), sha256=sha(CATALOG.read_bytes()),
                     metaSha256=sha(Path(str(CATALOG) + ".meta").read_bytes())),
        outputs=output_records,
        operation="Resolve each official follower page through YooAssetTextureLoader target GUIDs, the selected AssetTable and exact original bundle/path IDs. Verify source payload hashes, decode all original ASTC mips with the source color-space profile, and write native RGBA32 Texture2D assets preserving dimensions, mip count, sampler settings and row order. Bind each page by original official asset path in a Resources catalog.",
        limitations=["This receipt records original atlas inputs and outputs. Current rendering, constraints and story validation are tracked in docs/maps/map-13.md."])))
    print("Restored official Map13 follower atlas pages:", len(output_records), "textures",
          "and", sum(len(row["mips"]) for row in output_records), "mips", flush=True)


if __name__ == "__main__":
    build()
