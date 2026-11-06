"""Restore Whale Island's exact 884 NPC disappearance playable prefabs."""
from __future__ import annotations

import hashlib
import json
import re
import sys
from collections import Counter
from pathlib import Path

from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from prepare_unity_bundle_inputs import serialized_files
from prepare_unity_map13_sources import encode
from prepare_unity_map14_entity_materials import pointers
from prepare_unity_map14_entity_materials_native import clean, comparable, parse_material
from prepare_unity_map14_suimo_performance import same_scalars

VERSION = "884_156692"
CACHE = ROOT / ".tools/source-packages/current" / VERSION / "native-conversion"
EXPORTED = CACHE / "native-export-all/ExportedProject/Assets"
EXPORT_RECEIPT = CACHE / "native-export-all-source-manifest.json"
TEXTURE_CACHE = CACHE / "all-assets/windows-textures"
TEXTURE_RECEIPT = TEXTURE_CACHE / "source-manifest.json"
SHADER_CACHE = CACHE / "all-assets/windows-shaders"
SHADER_RECEIPT = SHADER_CACHE / "source-manifest.json"
MAP_SOURCE = ROOT / "unity/Assets/Official/World/Map/16/Source/story-source.json"
MAP_SOURCE_RECEIPT = ROOT / "assets/reference/official-map/map16-story-source-manifest.json"
CODE_ROOT = ROOT / ".tools/source-packages/current" / VERSION / "official-code"
CODE_RECEIPT = CODE_ROOT.parent / "code-manifest.json"
DESTINATION = ROOT / "unity/Assets/Official/World/Map/16/NativeStoryPlayables"
RESOURCE_DIR = ROOT / "unity/Assets/Game/Resources/Map16/Native"
CATALOG_PATH = RESOURCE_DIR / "native-story-playables-catalog.asset"
RESOURCE_CONFIG = RESOURCE_DIR / "story-playables.json"
RECEIPT = ROOT / "assets/reference/official-map/map16-native-story-playables-source-manifest.json"

GUID = re.compile(r"(?m)^guid: ([a-f0-9]{32})$")
REFERENCE = re.compile(r"guid: ([a-f0-9]{32})")
SCRIPT_REFERENCE = re.compile(
    r"(?m)^(\s*m_Script:\s*\{fileID:\s*)(-?\d+)(,\s*guid:\s*)([a-f0-9]{32})(,\s*type:\s*\d+\})$"
)

ROOTS = (
    {
        "official_guid": "9b248cde91299ee4287533b25b7dd468",
        "bundle": "defaultpackage_assets_prefabs_playables_displaypackage_story_npcanimator_playable_npc_disappear.bundle",
        "path": "Assets/Prefabs/Playables/DisplayPackage/Story/NPCAnimator/playable_npc_disappear.prefab",
        "hierarchy_counts": {"GameObject": 15, "Transform": 15, "MonoBehaviour": 11,
                             "ParticleSystem": 8, "ParticleSystemRenderer": 9},
        "event_export_guid": "0e5455f0fdd83124cab6b1388ff3293f",
        "event_guid": "6B99DC6E-8F0E-4DFE-B122-2FBEEE314EEA",
        "event_name": "Map_Action_BlueFlash_2",
        "event_id": 1758751830,
    },
    {
        "official_guid": "5fd5bec6c4f91f54799a4b823888f49a",
        "bundle": "defaultpackage_assets_prefabs_playables_displaypackage_story_npcanimator_playable_feile_disappear.bundle",
        "path": "Assets/Prefabs/Playables/DisplayPackage/Story/NPCAnimator/playable_FeiLe_disappear.prefab",
        "hierarchy_counts": {"GameObject": 19, "Transform": 19, "MonoBehaviour": 11,
                             "ParticleSystem": 11, "ParticleSystemRenderer": 13},
        "event_export_guid": "17e979a887fcf8b448d9c98f8da887a1",
        "event_guid": "C79A391D-164F-4B89-87BA-7EC819D33FFB",
        "event_name": "Map_Action_PinkFlash_2",
        "event_id": 2315213594,
    },
)

ORIGINAL_SCRIPTS = {
    ("055811b6aa1e2185dc556870e6de0c64", "-253978515"): "MyPlayableSequence",
    ("06dd3d5416bf43ea368cc39ff96e6d5a", "205640859"): "MyPlayableAnimator",
    ("055811b6aa1e2185dc556870e6de0c64", "-822603460"): "ApplyBySource",
    ("06dd3d5416bf43ea368cc39ff96e6d5a", "-1107144664"): "MyPlayableFollowBone",
    ("055811b6aa1e2185dc556870e6de0c64", "-1247969075"): "ApplyEnableOnPlay",
    ("06dd3d5416bf43ea368cc39ff96e6d5a", "1640720687"): "MyPlayableSound",
    ("06dd3d5416bf43ea368cc39ff96e6d5a", "768753278"): "ApplyOnStopFadeOut",
    ("06dd3d5416bf43ea368cc39ff96e6d5a", "-1858338532"): "MyPlayableActiveBone",
}
WWISE_SCRIPT = ("a9e21bc7bb762a5cf3086c00d123625f", "-210918009")
EXPECTED_SHADER = ("cab-014000ed2538cb87d1aa4612be612002", "-1464726073327024352")


def sha(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def record(path: Path) -> dict:
    return {"path": path.resolve().relative_to(ROOT).as_posix(),
            "sha256": sha(path.read_bytes()), "bytes": path.stat().st_size}


def stable_guid(kind: str, identity: str) -> str:
    return hashlib.md5(("zjcs/map16-story-playables/" + kind + ":" + identity).encode()).hexdigest()


def meta_text(path: Path, guid: str, main_object_id: int | None = None) -> str:
    if path.is_dir():
        return (f"fileFormatVersion: 2\nguid: {guid}\nDefaultImporter:\n"
                "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")
    if path.suffix == ".prefab": importer = "PrefabImporter"
    elif path.suffix == ".mat" or path.suffix == ".asset": importer = "NativeFormatImporter"
    elif path.suffix == ".shader": importer = "ShaderImporter"
    elif path.suffix == ".json": importer = "TextScriptImporter"
    else: raise ValueError("Unsupported migrated asset type: " + str(path))
    text = f"fileFormatVersion: 2\nguid: {guid}\n{importer}:\n  externalObjects: {{}}\n"
    if importer == "NativeFormatImporter":
        if main_object_id is None: raise ValueError("Native asset requires its main object file ID")
        text += f"  mainObjectFileID: {main_object_id}\n"
    return text + "  userData:\n  assetBundleName:\n  assetBundleVariant:\n"


def script_sources() -> tuple[dict[str, tuple[Path, str]], list[dict]]:
    found = {}
    rows = []
    runtime = ROOT / "unity/Assets/Game/Runtime/Official"
    wanted = set(ORIGINAL_SCRIPTS.values()) | {"WwiseEventReference"}
    for path in runtime.rglob("*.cs"):
        text = path.read_text(encoding="utf-8-sig")
        for name in wanted:
            if not re.search(r"\b(?:class|struct)\s+" + re.escape(name) + r"\b", text): continue
            meta = Path(str(path) + ".meta")
            match = GUID.search(meta.read_text(encoding="utf-8-sig")) if meta.is_file() else None
            if match:
                if name in found: raise ValueError("Migrated playable script is ambiguous: " + name)
                found[name] = (path, match[1])
    if found.keys() != wanted: raise ValueError("Migrated playable scripts are missing: " + repr(sorted(wanted - found.keys())))
    for name, (path, script_guid) in sorted(found.items()):
        rows.append({"class": name, "source": record(path), "meta": record(Path(str(path) + ".meta")),
                     "script_guid": script_guid})
    return found, rows


def write_owned(path: Path, raw: bytes, previous: dict[str, str]) -> None:
    rel = path.resolve().relative_to(ROOT).as_posix()
    if path.exists():
        actual = sha(path.read_bytes())
        if rel not in previous:
            if path.read_bytes() != raw: raise FileExistsError("Refusing to replace an unowned Map16 output: " + rel)
        elif actual != previous[rel]:
            raise ValueError("Previously generated Map16 playable output changed: " + rel)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(raw)


def read_previous() -> dict[str, str]:
    if not RECEIPT.is_file(): return {}
    value = json.loads(RECEIPT.read_bytes())
    return {row["path"]: row["sha256"] for row in value.get("outputs", [])}


def source_identity(obj) -> tuple[str, str]:
    return obj.assets_file.name.casefold(), str(obj.path_id)


def object_reader(apk: Apk):
    files = {}
    def read(cab: str, path_id: str):
        key = cab.casefold()
        if key not in files:
            if key not in apk.cabs: raise FileNotFoundError("Official Map16 playable CAB is missing: " + cab)
            bundle = apk.load(apk.cabs[key]["bundle"])
            files[key] = next(item for outer in bundle.files.values() for item in serialized_files(outer)
                              if item.name.casefold() == key)
        return files[key].objects[int(path_id)]
    return read


def pointer_identity(pointer: dict, owner) -> tuple[str, str]:
    file_id = int(pointer["m_FileID"])
    if file_id == 0:
        cab = owner.assets_file.name
    else:
        cab = owner.assets_file.externals[file_id - 1].path.replace("\\", "/").rsplit("/", 1)[-1]
    return cab.casefold(), str(pointer["m_PathID"])


def hierarchy_dependencies(root):
    kinds = Counter()
    materials, meshes = {}, {}
    visited = set()
    def visit(game_object):
        key = source_identity(game_object)
        if key in visited: return
        visited.add(key)
        kinds["GameObject"] += 1
        go = game_object.read()
        children = []
        for pair in go.m_Component:
            component = pair.component.deref()
            kinds[component.type.name] += 1
            if component.type.name in ("Transform", "RectTransform"):
                children.extend(pointer.read().m_GameObject.deref() for pointer in component.read().m_Children)
            elif component.type.name == "ParticleSystemRenderer":
                data = component.read()
                for pointer in data.m_Materials:
                    if pointer.path_id:
                        value = pointer.deref(); materials[source_identity(value)] = value
                pointer = getattr(data, "m_Mesh", None)
                if pointer is not None and pointer.path_id:
                    value = pointer.deref(); meshes[source_identity(value)] = value
        for child in children: visit(child)
    visit(root)
    return kinds, materials, meshes


def export_index(folder: Path, suffix: str) -> dict[str, Path]:
    result = {}
    for sidecar in folder.rglob("*.meta"):
        path = sidecar.with_suffix("")
        if not path.is_file() or path.suffix != suffix: continue
        match = GUID.search(sidecar.read_text(encoding="utf-8-sig"))
        if not match: continue
        if match[1] in result: raise ValueError("Duplicate official export GUID: " + match[1])
        result[match[1]] = path
    return result


def exported_hash(path: Path, outputs: dict[str, dict]) -> dict:
    relative = path.relative_to(CACHE / "native-export-all").as_posix()
    expected = outputs.get(relative)
    if expected is None or sha(path.read_bytes()) != expected["sha256"]:
        raise ValueError("Verified 884 native export changed or missing: " + relative)
    return {"path": relative, "sha256": expected["sha256"], "bytes": expected["bytes"]}


def write_asset(path: Path, raw: bytes, guid: str, main_object_id: int | None,
                outputs: list[dict], previous: dict[str, str]) -> None:
    write_owned(path, raw, previous)
    meta = Path(str(path) + ".meta")
    write_owned(meta, meta_text(path, guid, main_object_id).encode("utf-8"), previous)
    outputs.extend([record(path), record(meta)])


def root_game_object_file_id(raw: str, expected_name: str) -> int:
    documents = re.split(r"(?m)^--- !u!", raw)
    matches = []
    for document in documents:
        header = re.match(r"(\d+) &(-?\d+)", document)
        if not header or int(header[1]) != 1: continue
        if re.search(r"(?m)^  m_Name: " + re.escape(expected_name) + r"\s*$", document):
            matches.append(int(header[2]))
    if len(matches) != 1: raise ValueError("Original playable prefab root object is ambiguous: " + expected_name)
    return matches[0]


def bind_texture_property(raw: str, prop: str, guid: str) -> str:
    section = re.compile(r"(?ms)^(      " + re.escape(prop) + r":\n)(.*?)(?=^      \S|\Z)")
    def replace(match):
        body, count = re.subn(r"(?m)^(        m_Texture: )\{[^\n]+\}$",
            lambda value: value[1] + "{fileID: 2800000, guid: " + guid + ", type: 2}", match[2])
        if count != 1: raise ValueError("Original material texture property is not unique: " + prop)
        return match[1] + body
    result, count = section.subn(replace, raw)
    if count != 1: raise ValueError("Original material texture property is missing: " + prop)
    return result


def build() -> dict:
    if len(ROOTS) != 2: raise ValueError("Map16 NPC playable coverage changed")
    previous = read_previous()
    output_files: list[dict] = []
    source_receipt = json.loads(MAP_SOURCE_RECEIPT.read_bytes())
    source = json.loads(MAP_SOURCE.read_bytes())
    source_outputs = {row["path"]: row["sha256"] for row in source_receipt["outputs"]}
    if source_receipt.get("manifest") != VERSION or source.get("manifest") != VERSION \
            or source_outputs.get(MAP_SOURCE.relative_to(ROOT).as_posix()) != sha(MAP_SOURCE.read_bytes()):
        raise ValueError("Map16 official story selection is not pinned to 884")
    selected_guids = set()
    def collect_guids(value):
        if isinstance(value, dict):
            if value.get("mAssetGuid"): selected_guids.add(value["mAssetGuid"])
            for child in value.values(): collect_guids(child)
        elif isinstance(value, list):
            for child in value: collect_guids(child)
    collect_guids(source.get("graphs", {}))
    expected_guids = {item["official_guid"] for item in ROOTS}
    if selected_guids != expected_guids:
        raise ValueError("Map16 source story playable GUIDs changed: " + repr(sorted(selected_guids)))

    export_proof = json.loads(EXPORT_RECEIPT.read_bytes())
    texture_proof = json.loads(TEXTURE_RECEIPT.read_bytes())
    shader_proof = json.loads(SHADER_RECEIPT.read_bytes())
    if export_proof.get("manifest") != VERSION or texture_proof.get("manifest") != VERSION \
            or shader_proof.get("manifest") != VERSION:
        raise ValueError("Map16 playable asset conversion versions differ")
    export_outputs = {row["path"]: row for row in export_proof["outputs"]}
    texture_rows = {(row["source"]["cab"].casefold(), str(row["source"]["path_id"])): row
                    for row in texture_proof["textures"]}
    shader_rows = {(row["source"]["cab"].casefold(), str(row["source"]["path_id"])): row
                   for row in shader_proof["shaders"]}
    if EXPECTED_SHADER not in shader_rows: raise ValueError("Exact Map16 884 shader source is not converted for Windows")
    shader_row = shader_rows[EXPECTED_SHADER]
    shader_source_file = SHADER_CACHE / shader_row["path"]
    if sha(shader_source_file.read_bytes()) != shader_row["sha256"] \
            or sha(Path(str(shader_source_file) + ".meta").read_bytes()) != shader_row["meta_sha256"]:
        raise ValueError("Verified Windows shader conversion changed")

    apk = Apk.current()
    if apk.version != VERSION: raise ValueError("Selected official APK version differs from Map16 story source")
    selected_assets = {row["guid"]: row for row in read_asset_manifest(
        apk.source.manifest_bytes("DefaultPackage", apk.archive))}
    original_reader = object_reader(apk)
    code_proof = json.loads(CODE_RECEIPT.read_bytes())
    if code_proof.get("manifest") != VERSION or code_proof.get("apk_sha256") != apk.source.apk_sha256:
        raise ValueError("Original playable code does not match the selected 884 APK")
    for assembly in ("Assembly-CSharp.dll", "GameFramework.Runtime.dll"):
        entry = code_proof["assemblies"].get(assembly)
        path = CODE_ROOT / "managed/decoded" / assembly
        if entry is None or sha(path.read_bytes()) != entry["decoded_sha256"]:
            raise ValueError("Original 884 playable assembly changed: " + assembly)
    scripts, script_records = script_sources()
    script_guids = {name: value[1] for name, value in scripts.items()}
    script_pair_to_local = {pair: script_guids[name] for pair, name in ORIGINAL_SCRIPTS.items()}
    wwise_script_path, wwise_script_guid = scripts["WwiseEventReference"]

    used_source_objects = []
    used_export_files = []
    prefab_bindings = []
    material_records = {}
    mesh_records = {}
    texture_records = {}
    event_records = []
    shader_target_guid = stable_guid("shader", EXPECTED_SHADER[0] + ":" + EXPECTED_SHADER[1])
    shader_output = DESTINATION / "Shaders/Noise_Mask_Pro.shader"
    shader_text = shader_source_file.read_text(encoding="utf-8")
    shader_name = re.search(r'(?m)^Shader "([^"]+)"', shader_text)
    if not shader_name or shader_name[1] != "Effect/Noise_Mask_Pro":
        raise ValueError("Original Map16 effect shader name changed")
    shader_runtime_name = "Zjcs/Official/World/Map16/StoryPlayables/Noise_Mask_Pro"
    shader_text, changed = re.subn(r'(?m)^Shader "Effect/Noise_Mask_Pro"',
        'Shader "' + shader_runtime_name + '"', shader_text, count=1)
    if changed != 1: raise ValueError("Cannot namespace original Map16 shader")
    write_asset(shader_output, shader_text.encode("utf-8"), shader_target_guid, None,
                output_files, previous)
    shader_record = {"source": shader_row["source"], "conversion": record(shader_source_file),
                     "conversion_meta_sha256": shader_row["meta_sha256"],
                     "runtime_name": shader_runtime_name, "output": record(shader_output),
                     "output_meta": record(Path(str(shader_output) + ".meta"))}

    material_export_index = export_index(EXPORTED / "Effect/Materials", ".mat")
    mesh_export_index = export_index(EXPORTED / "Effect/models", ".asset")
    root_maps: dict[str, dict] = {}
    for root_spec in ROOTS:
        selected = selected_assets.get(root_spec["official_guid"])
        if selected is None or selected["bundle"] != root_spec["bundle"] or selected["path"] != root_spec["path"]:
            raise ValueError("Map16 official story playable address changed: " + root_spec["official_guid"])
        original_root = apk.asset(selected["bundle"], selected["path"])
        root_provenance = apk.provenance(original_root)
        root_payload_sha = sha(original_root.get_raw_data())
        hierarchy, original_materials, original_meshes = hierarchy_dependencies(original_root)
        if dict(hierarchy) != root_spec["hierarchy_counts"]:
            raise ValueError("Original Map16 playable hierarchy changed: " + repr(dict(hierarchy)))
        export_path = EXPORTED / selected["path"].removeprefix("Assets/")
        exported_root_record = exported_hash(export_path, export_outputs)
        raw = export_path.read_text(encoding="utf-8-sig")
        source_scripts_in_prefab = Counter()
        for match in SCRIPT_REFERENCE.finditer(raw):
            pair = (match[4], match[2])
            if pair not in ORIGINAL_SCRIPTS:
                raise ValueError("Unmapped 884 playable MonoScript pair: " + repr(pair))
            source_scripts_in_prefab[ORIGINAL_SCRIPTS[pair]] += 1
        if source_scripts_in_prefab != Counter({
                "MyPlayableSequence": 1, "MyPlayableAnimator": 1, "ApplyBySource": 2,
                "MyPlayableFollowBone": 2, "ApplyEnableOnPlay": 2, "MyPlayableSound": 1,
                "ApplyOnStopFadeOut": 1, "MyPlayableActiveBone": 1}):
            raise ValueError("Original Map16 playable component script counts changed")

        export_refs = set(REFERENCE.findall(raw))
        source_script_guids = {pair[0] for pair in ORIGINAL_SCRIPTS}
        asset_refs = export_refs - source_script_guids
        material_refs = {guid: material_export_index[guid] for guid in asset_refs if guid in material_export_index}
        mesh_refs = {guid: mesh_export_index[guid] for guid in asset_refs if guid in mesh_export_index}
        expected_event = root_spec["event_export_guid"]
        if expected_event not in asset_refs: raise ValueError("Original Map16 playable sound event reference changed")
        event_path = EXPORTED / "Wwise/ScriptableObjects/Event" / (root_spec["event_guid"] + ".asset")
        event_sidecar = Path(str(event_path) + ".meta")
        if not event_path.is_file() or GUID.search(event_sidecar.read_text(encoding="utf-8-sig"))[1] != expected_event:
            raise ValueError("Original Map16 Wwise event asset is missing or mismatched")
        event_export_record = exported_hash(event_path, export_outputs)
        event_text = event_path.read_text(encoding="utf-8-sig")
        if not all(value in event_text for value in (root_spec["event_name"], root_spec["event_guid"],
                                                     str(root_spec["event_id"]))):
            raise ValueError("Original Map16 Wwise event identity changed")
        other_ref_guids = asset_refs - set(material_refs) - set(mesh_refs) - {expected_event}
        if other_ref_guids:
            raise ValueError("Unresolved original Map16 playable dependencies: " + repr(sorted(other_ref_guids)))
        if len(material_refs) != len(original_materials) or len(mesh_refs) != len(original_meshes):
            raise ValueError("Original Map16 playable renderer dependencies are incomplete")

        prefab_identity = root_spec["official_guid"]
        prefab_guid = stable_guid("prefab", prefab_identity)
        asset_replacements: dict[str, str] = {}
        material_source_rows = []
        for identity, original_material in sorted(original_materials.items()):
            fields = json.loads(encode(original_material.read_typetree(check_read=True)))
            expected_fields = comparable(fields)
            candidates = []
            for export_guid, path in material_refs.items():
                _, converted = parse_material(path)
                if converted["m_Name"] != fields["m_Name"]: continue
                projected = clean({key: value for key, value in converted.items() if key in expected_fields})
                if same_scalars(expected_fields, projected): candidates.append((export_guid, path, converted))
            if len(candidates) != 1:
                raise ValueError("Original Map16 material export is not unique: " + fields["m_Name"])
            export_guid, exported_material, converted = candidates[0]
            shader_pointer = fields["m_Shader"]
            shader_key = pointer_identity(shader_pointer, original_material)
            if shader_key != EXPECTED_SHADER:
                raise ValueError("Unexpected original shader used by Map16 playable material: " + repr(shader_key))
            shader_obj = original_reader(*shader_key)
            if sha(shader_obj.get_raw_data()) != shader_row["source"]["object_sha256"]:
                raise ValueError("Map16 884 shader PPtr differs from the Windows conversion input")
            material_identity = identity[0] + ":" + identity[1]
            material_guid = stable_guid("material", material_identity)
            material_path = DESTINATION / "Materials" / (hashlib.sha256(material_identity.encode()).hexdigest()[:20] + ".mat")
            material_raw = exported_material.read_text(encoding="utf-8-sig")
            shader_ref = converted["m_Shader"]
            if shader_ref.get("guid") != re.search(r"guid: ([a-f0-9]{32})", material_raw).group(1):
                raise ValueError("Original exported material shader pointer differs")
            material_raw, count = re.subn(r"(?m)^(  m_Shader: \{fileID: )[^,]+(, guid: )[a-f0-9]{32}(, type: \d+\})$",
                # The restored .shader uses ShaderImporter (external type 3),
                # rather than the exported native Shader asset's type 2.
                lambda match: match[1] + "4800000" + match[2] + shader_target_guid + ", type: 3}", material_raw)
            if count != 1: raise ValueError("Original Map16 material shader pointer is not unique")

            source_texture_rows = {}
            texture_fields = fields["m_SavedProperties"]["m_TexEnvs"]
            exported_texture_fields = converted["m_SavedProperties"]["m_TexEnvs"]
            if set(row[0] for row in texture_fields) != set(exported_texture_fields):
                raise ValueError("Original Map16 material texture property names changed")
            for prop, env in texture_fields:
                pointer = env["m_Texture"]
                if int(pointer["m_PathID"]) == 0: continue
                texture_key = pointer_identity(pointer, original_material)
                texture_row = texture_rows.get(texture_key)
                if texture_row is None:
                    raise ValueError("Exact Map16 texture source is absent from the Windows texture manifest: " + repr(texture_key))
                texture_obj = original_reader(*texture_key)
                source_record = texture_row["source"]
                if source_record["type"] != "Texture2D" or sha(texture_obj.get_raw_data()) != source_record["object_sha256"]:
                    raise ValueError("Map16 texture CAB/PathID differs from its converted source")
                converted_pointer = exported_texture_fields[prop]["m_Texture"]
                exported_texture_guid = converted_pointer.get("guid")
                if not exported_texture_guid or int(converted_pointer.get("fileID", 0)) == 0:
                    raise ValueError("Original exported material texture binding is missing: " + prop)
                previous_binding = asset_replacements.get(exported_texture_guid)
                texture_identity = texture_key[0] + ":" + texture_key[1]
                target_texture_guid = stable_guid("texture", texture_identity)
                if previous_binding is not None and previous_binding != target_texture_guid:
                    raise ValueError("Exporter texture GUID aliases different source textures")
                asset_replacements[exported_texture_guid] = target_texture_guid
                source_texture_rows[prop] = texture_row
                source_texture_rows[prop + ":export_guid"] = {"guid": exported_texture_guid,
                    "source": source_record, "output_guid": target_texture_guid}
            for prop, texture_row in list(source_texture_rows.items()):
                if prop.endswith(":export_guid"): continue
                relation = source_texture_rows[prop + ":export_guid"]
                material_raw = bind_texture_property(material_raw, prop, relation["output_guid"])

            expected_material_guid = stable_guid("material", material_identity)
            asset_replacements[export_guid] = expected_material_guid
            write_asset(material_path, material_raw.encode("utf-8"), material_guid, 2100000,
                        output_files, previous)
            used_export_files.append(exported_hash(exported_material, export_outputs))
            material_source_rows.append({"identity": material_identity, "name": fields["m_Name"],
                "payload_sha256": sha(original_material.get_raw_data()),
                "export": exported_hash(exported_material, export_outputs), "output": record(material_path),
                "output_meta": record(Path(str(material_path) + ".meta")),
                "shader_source": shader_row["source"],
                "textures": [source_texture_rows[prop + ":export_guid"] for prop in sorted(texture_fields_name
                    for texture_fields_name, _ in texture_fields) if prop + ":export_guid" in source_texture_rows]})
            material_records[identity] = material_source_rows[-1]

            for prop, texture_row in source_texture_rows.items():
                if prop.endswith(":export_guid"): continue
                texture_identity_key = texture_key_for_property(fields, prop, original_material)
                texture_records.setdefault(texture_identity_key, texture_row)

        for identity, original_mesh in sorted(original_meshes.items()):
            source_name = original_mesh.read().m_Name
            candidates = []
            for export_guid, path in mesh_refs.items():
                text = path.read_text(encoding="utf-8-sig")
                if not text.startswith("%YAML") or not re.search(r"(?m)^--- !u!43 &", text): continue
                match = re.search(r"(?m)^  m_Name: (.+)$", text)
                if match and match[1] == source_name: candidates.append((export_guid, path))
            if len(candidates) != 1:
                raise ValueError("Original Map16 mesh export is not unique: " + str(source_name))
            export_guid, exported_mesh = candidates[0]
            identity_text = identity[0] + ":" + identity[1]
            target_guid = stable_guid("mesh", identity_text)
            asset_replacements[export_guid] = target_guid
            target_path = DESTINATION / "Meshes" / (hashlib.sha256(identity_text.encode()).hexdigest()[:20] + ".asset")
            write_asset(target_path, exported_mesh.read_bytes(), target_guid, 4300000, output_files, previous)
            source_row = {"identity": identity_text, "name": source_name,
                          "payload_sha256": sha(original_mesh.get_raw_data()),
                          "export": exported_hash(exported_mesh, export_outputs), "output": record(target_path),
                          "output_meta": record(Path(str(target_path) + ".meta"))}
            mesh_records[identity] = source_row
            used_export_files.append(source_row["export"])

        event_target_guid = stable_guid("wwise-event", root_spec["event_guid"])
        asset_replacements[expected_event] = event_target_guid
        event_export_script = SCRIPT_REFERENCE.search(event_text)
        if not event_export_script or (event_export_script[4], event_export_script[2]) != WWISE_SCRIPT:
            raise ValueError("Original Map16 Wwise event script binding changed")
        event_text, count = SCRIPT_REFERENCE.subn(
            lambda match: match[1] + "11500000" + match[3] + wwise_script_guid + match[5], event_text)
        if count != 1: raise ValueError("Original Map16 Wwise event script was not uniquely rebound")
        event_target = DESTINATION / "Audio" / (root_spec["event_guid"] + ".asset")
        write_asset(event_target, event_text.encode("utf-8"), event_target_guid, 11400000,
                    output_files, previous)
        used_export_files.extend([event_export_record, record(event_sidecar)])
        event_records.append({"source": {"guid": root_spec["event_guid"], "name": root_spec["event_name"],
                                          "id": root_spec["event_id"]},
                              "export": event_export_record, "export_meta": record(event_sidecar),
                              "output": record(event_target), "output_meta": record(Path(str(event_target) + ".meta"))})

        # Bind all authored MonoBehaviours to the exact migrated class GUID and
        # Unity's normal MonoScript file ID, leaving each original component field intact.
        bound_scripts = Counter()
        def bind_script(match):
            pair = (match[4], match[2])
            name = ORIGINAL_SCRIPTS.get(pair)
            if name is None: raise ValueError("Unmapped original Map16 MonoScript: " + repr(pair))
            bound_scripts[name] += 1
            return match[1] + "11500000" + match[3] + script_guids[name] + match[5]
        raw, script_count = SCRIPT_REFERENCE.subn(bind_script, raw)
        if script_count != sum(source_scripts_in_prefab.values()):
            raise ValueError("Original Map16 playable script reference count changed")
        raw = REFERENCE.sub(lambda match: "guid: " + asset_replacements[match[1]]
                            if match[1] in asset_replacements else match[0], raw)
        if set(REFERENCE.findall(raw)) & (set(asset_replacements) | source_script_guids):
            raise ValueError("Original Map16 prefab retained an unbound source asset/script GUID")
        target_path = DESTINATION / "Prefabs" / Path(selected["path"]).name
        write_asset(target_path, raw.encode("utf-8"), prefab_guid, None, output_files, previous)
        used_export_files.extend([exported_root_record, exported_hash(Path(str(export_path) + ".meta"), export_outputs)])
        prefab_bindings.append({"official_guid": root_spec["official_guid"], "official_asset": selected,
            "official_source": root_provenance, "root_payload_sha256": root_payload_sha,
            "hierarchy_counts": dict(hierarchy), "source_scripts": dict(bound_scripts),
            "export": exported_root_record, "export_meta": exported_hash(Path(str(export_path) + ".meta"), export_outputs),
            "output": record(target_path), "output_meta": record(Path(str(target_path) + ".meta")),
            "materials": [material_records[key] for key in sorted(original_materials)],
            "meshes": [mesh_records[key] for key in sorted(original_meshes)],
            "event": event_records[-1]})
        root_maps[root_spec["official_guid"]] = {"guid": prefab_guid, "path": target_path,
                                                  "file_id": root_game_object_file_id(raw, Path(selected["path"]).stem)}

    if len(material_records) != 17 or len(mesh_records) != 3 or len(texture_records) != 25 or len(event_records) != 2:
        raise ValueError("Map16 NPC playable native dependency closure changed: " + repr(
            {"materials": len(material_records), "meshes": len(mesh_records),
             "textures": len(texture_records), "events": len(event_records)}))
    for identity, texture_row in sorted(texture_records.items()):
        outputs = texture_row["outputs"]
        if len(outputs) != 2: raise ValueError("Windows texture output closure changed: " + identity[0] + ":" + identity[1])
        source_path = TEXTURE_CACHE / outputs[0]["path"]
        if sha(source_path.read_bytes()) != outputs[0]["sha256"]:
            raise ValueError("Verified Windows texture changed: " + outputs[0]["path"])
        if sha(Path(str(source_path) + ".meta").read_bytes()) != outputs[1]["sha256"]:
            raise ValueError("Verified Windows texture sidecar changed: " + outputs[1]["path"])
        identity_text = identity[0] + ":" + identity[1]
        target_guid = stable_guid("texture", identity_text)
        target_path = DESTINATION / "Textures" / (hashlib.sha256(identity_text.encode()).hexdigest()[:20] + ".asset")
        write_asset(target_path, source_path.read_bytes(), target_guid, 2800000, output_files, previous)
        texture_row["output"] = record(target_path)
        texture_row["output_meta"] = record(Path(str(target_path) + ".meta"))
        texture_row["windows_conversion"] = {"asset": outputs[0], "meta": outputs[1]}

    # A ScriptableObject catalog is the local ResourceLoader boundary used by
    # every map; the story graph continues to carry the original asset GUIDs.
    catalog_script = ROOT / "unity/Assets/Game/Runtime/Offline/World/NativeMap13/NativeMap13AssetCatalog.cs"
    catalog_script_guid = GUID.search(Path(str(catalog_script) + ".meta").read_text(encoding="utf-8-sig"))[1]
    catalog_guid = stable_guid("resource-catalog", "Map16/native-story-playables")
    catalog_text = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
        "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}", "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1", "  m_EditorHideFlags: 0",
        "  m_Script: {fileID: 11500000, guid: " + catalog_script_guid + ", type: 3}",
        "  m_Name: native-story-playables-catalog", "  m_EditorClassIdentifier: ", "  Bindings:"]
    for root_spec in ROOTS:
        binding = root_maps[root_spec["official_guid"]]
        catalog_text.extend(["  - OfficialPath: " + root_spec["official_guid"],
            "    Asset: {fileID: " + str(binding["file_id"]) + ", guid: " + binding["guid"] + ", type: 3}"])
    catalog_text.append("")
    write_owned(CATALOG_PATH, "\n".join(catalog_text).encode("utf-8"), previous)
    catalog_meta_path = Path(str(CATALOG_PATH) + ".meta")
    write_owned(catalog_meta_path, meta_text(CATALOG_PATH, catalog_guid, 11400000).encode("utf-8"), previous)
    output_files.extend([record(CATALOG_PATH), record(catalog_meta_path)])
    config = {"manifest": VERSION, "mapClassId": 16, "expectedCount": 2,
              "catalogs": ["native-story-playables-catalog"]}
    config_raw = (json.dumps(config, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
    write_owned(RESOURCE_CONFIG, config_raw, previous)
    config_guid = stable_guid("resource-config", "Map16/story-playables")
    write_owned(Path(str(RESOURCE_CONFIG) + ".meta"),
                meta_text(RESOURCE_CONFIG, config_guid).encode("utf-8"), previous)
    output_files.extend([record(RESOURCE_CONFIG), record(Path(str(RESOURCE_CONFIG) + ".meta"))])

    owned_directories = {DESTINATION}
    for row in output_files:
        path = ROOT / row["path"]
        if path.is_relative_to(DESTINATION): owned_directories.add(path.parent)
    resource_directory_existed = RESOURCE_DIR.is_dir()
    if not resource_directory_existed: owned_directories.add(RESOURCE_DIR)
    for folder in sorted(owned_directories, key=lambda item: len(item.parts)):
        if not folder.is_dir(): folder.mkdir(parents=True, exist_ok=True)
        if folder == RESOURCE_DIR and resource_directory_existed: continue
        guid = stable_guid("folder", folder.relative_to(ROOT).as_posix())
        sidecar = Path(str(folder) + ".meta")
        if sidecar.is_file():
            relative = sidecar.resolve().relative_to(ROOT).as_posix()
            raw = sidecar.read_bytes()
            if relative in previous and sha(raw) != previous[relative]:
                raise ValueError("Previously generated Map16 folder metadata changed: " + relative)
            content = raw.decode("utf-8-sig")
            if "folderAsset: yes" not in content or "DefaultImporter:" not in content \
                    or not GUID.search(content):
                raise ValueError("Unexpected existing Map16 folder metadata: " + relative)
        else:
            write_owned(sidecar, meta_text(folder, guid).encode("utf-8"), previous)
        output_files.append(record(sidecar))

    outputs_by_path = {row["path"]: row for row in output_files}
    if len(outputs_by_path) != len(output_files): raise ValueError("Map16 playable output path is duplicated")
    inputs = [record(Path(__file__).resolve()), record(MAP_SOURCE), record(MAP_SOURCE_RECEIPT),
              record(EXPORT_RECEIPT), record(TEXTURE_RECEIPT), record(SHADER_RECEIPT), record(CODE_RECEIPT),
              record(catalog_script), record(Path(str(catalog_script) + ".meta")),
              record(ROOT / "scripts/prepare_unity_map14_entity_materials.py"),
              record(ROOT / "scripts/prepare_unity_map14_entity_materials_native.py"),
              record(ROOT / "scripts/prepare_unity_map14_suimo_performance.py"),
              record(ROOT / "scripts/prepare_unity_map13_sources.py"),
              record(ROOT / "scripts/prepare_unity_bundle_inputs.py"),
              record(CODE_ROOT / "managed/decoded/Assembly-CSharp.dll"),
              record(CODE_ROOT / "managed/decoded/GameFramework.Runtime.dll"),
              *script_records]
    receipt = {"manifest": VERSION, "mapClassId": 16,
        "operation": "Restore original Map16 story disappearance prefabs, remap their verified 884 scripts, bind exact Windows textures and shader, preserve the original Wwise event fields, and publish a local GUID catalog.",
        "story_source": record(MAP_SOURCE), "story_source_receipt": record(MAP_SOURCE_RECEIPT),
        "official_assets": prefab_bindings, "shader": shader_record,
        "textures": [dict(source=row["source"], output=row["output"], output_meta=row["output_meta"],
                          windows_conversion=row["windows_conversion"])
                     for _, row in sorted(texture_records.items())],
        "scripts": script_records, "input_tools": inputs,
        "conversion_inputs": {"native_export_manifest_sha256": sha(EXPORT_RECEIPT.read_bytes()),
            "windows_texture_manifest_sha256": sha(TEXTURE_RECEIPT.read_bytes()),
            "windows_shader_manifest_sha256": sha(SHADER_RECEIPT.read_bytes()),
            "official_code_manifest_sha256": sha(CODE_RECEIPT.read_bytes())},
        "counts": {"prefabs": len(prefab_bindings), "materials": len(material_records),
            "meshes": len(mesh_records), "textures": len(texture_records), "shaders": 1,
            "wwise_events": len(event_records), "playable_components": 22},
        "outputs": [outputs_by_path[key] for key in sorted(outputs_by_path)]}
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, sort_keys=True, indent=2) + "\n", encoding="utf-8", newline="\n")
    return receipt["counts"]


def texture_key_for_property(fields: dict, prop: str, owner) -> tuple[str, str]:
    env = next(value for name, value in fields["m_SavedProperties"]["m_TexEnvs"] if name == prop)
    return pointer_identity(env["m_Texture"], owner)


if __name__ == "__main__":
    print(json.dumps(build(), sort_keys=True), flush=True)
