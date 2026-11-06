"""Recover the original pet skill widgets used by skill hyperlinks."""
import hashlib
import io
import json
import os
import re
import sys
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PINNED_VERSION = "884_156692"
MODULE = ROOT / "unity/Assets/Official/PetSkillUI"
LAYOUT = MODULE / "pet-skill-ui.json"
MODULE_MANIFEST = MODULE / "source-manifest.json"
RECEIPT = ROOT / "assets/reference/pet-skill-ui/source-manifest.json"
UNITY_EDITOR_DATA = Path("D:/Tool/Unity/Editor/Data")
CODE = ROOT / ".tools/source-packages/current/884_156692/official-code"
sys.path.insert(0, str(ROOT / ".tools/python-packages"))
sys.path.insert(0, str(ROOT / "scripts"))
sys.path.insert(0, str(ROOT / ".tools/character-art-python/Lib/site-packages"))
dotnet_bin = ROOT / ".tools/dotnet"
if (dotnet_bin / "dotnet.exe").is_file():
    os.environ["PATH"] = str(dotnet_bin) + os.pathsep + os.environ.get("PATH", "")

from official_apk import Apk
from official_manifest import read_asset_manifest
from official_unity_schema import ScriptSchemas
from prepare_character_prop_bindings import BindingReader
from prepare_skill_extra_ui import PET_SKILL_TEMPLATES, recover_prefab
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
from UnityPy.classes.PPtr import PPtr


PET_SKILL_CODE = (
    "client-source/MonsterBriefInfoPanel.cs",
    "client-source/SkillWithOuterTagItem.cs",
    "client-source/ItemIcon.cs",
    "client-source/ItemIconPetSkill.cs",
    "client-source/PetSkillDetailDialog.cs",
    "client-source/SkillDetailDialog.cs",
    "client-source/PetSkillPropCalculator.cs",
    "client-source/SkillPanelPropPanel.cs",
    "common-source/Common/PropConfigUtils.cs",
    "common-source/Common/PetEvolutionInfoParser.cs",
)
GENERATOR_PATH = "scripts/prepare_skill_pet_ui.py"
PET_OPERATION = "Recover original SkillWithOuterTagItem, ItemIconPetSkill, and PetSkillDetailDialog prefabs from the pinned APK."


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def source_record(path):
    return {"source": path.relative_to(ROOT).as_posix(), "sha256": sha(path.read_bytes())}


def load_json(path):
    return json.loads(path.read_text(encoding="utf-8"))


def make_schemas(apk):
    reader = BindingReader.__new__(BindingReader)
    reader.apk = apk
    reader.inputs = {}
    reader.schemas = ScriptSchemas(assembly_overrides={
        "UnityEngine.UI": ROOT / "unity/Library/ScriptAssemblies/UnityEngine.UI.dll"})
    reader.schemas.generator.load_dll(Path(
        "D:/Tool/Unity/Editor/Data/NetStandard/ref/2.1.0/netstandard.dll").read_bytes())
    reader.schemas.generator.load_dll(Path(
        "D:/Tool/Unity/Editor/Data/Managed/UnityEngine/UnityEngine.VideoModule.dll").read_bytes())

    text_schemas = ScriptSchemas()
    text_schemas.generator = TypeTreeGenerator("2022.3.57f1", generator="AssetsTools")
    text_schemas.generator.load_local_dll_folder(str(UNITY_EDITOR_DATA / "Managed/UnityEngine"))
    ui_schema_folder = UNITY_EDITOR_DATA / "Resources/PackageManager/ProjectTemplates/libcache/com.unity.template.2d-7.0.3/ScriptAssemblies"
    for name in ("UnityEngine.UI.dll", "Unity.TextMeshPro.dll"):
        candidate = ROOT / "unity/Library/ScriptAssemblies" / name
        if not candidate.is_file():
            candidate = ui_schema_folder / name
        text_schemas.generator.load_dll(candidate.read_bytes())
    for path in (
        UNITY_EDITOR_DATA / "NetStandard/ref/2.1.0/netstandard.dll",
        UNITY_EDITOR_DATA / "MonoBleedingEdge/lib/mono/4.5/mscorlib.dll",
        CODE / "managed/decoded/LoopScrollRect.Runtime.dll",
        CODE / "managed/decoded/Framework.dll",
    ):
        text_schemas.generator.load_dll(path.read_bytes())
    return reader, text_schemas


def prepare():
    source = load_json(ROOT / "official-source.json")
    if source.get("active") != PINNED_VERSION:
        raise ValueError("Pet skill UI conversion requires the active official source " + PINNED_VERSION)
    apk = Apk()
    if apk.version != PINNED_VERSION:
        apk.archive.close()
        raise ValueError("Official APK selection changed during pet skill UI conversion")
    data = {}
    sprites = {}
    text_sources = {}
    try:
        reader, text_schemas = make_schemas(apk)
        for key, (bundle, prefab) in PET_SKILL_TEMPLATES.items():
            data[key], extracted = recover_prefab(reader, text_schemas, bundle, prefab,
                                                   {"resources": {}})
            sprites.update(extracted)
            def nodes(node):
                yield node
                for child in node.get("children", []):
                    yield from nodes(child)
            by_path = {node["path"]: node for node in nodes(data[key]["root"])}
            prefab_source = data[key]["source"]
            for node in reader.walk(reader.object(prefab_source["bundle"], prefab_source["path_id"]).read()):
                from prepare_character_prop_bindings import node_path
                target = by_path[node_path(node)]
                for entry in node.m_Component:
                    component = entry.component.deref()
                    if component.type.name != "MonoBehaviour" or component.read(check_read=False).m_Script.read().m_ClassName != "TextMeshProUGUI":
                        continue
                    fields = text_schemas.read(component)
                    target["text"].update(native_tmp=True, enabled=bool(fields["m_Enabled"]),
                        font_size=fields["m_fontSizeBase"], auto_size=bool(fields["m_enableAutoSizing"]),
                        font_size_min=fields["m_fontSizeMin"], font_size_max=fields["m_fontSizeMax"],
                        word_wrap=bool(fields["m_enableWordWrapping"]), overflow=fields["m_overflowMode"],
                        horizontal=fields["m_HorizontalAlignment"], vertical=fields["m_VerticalAlignment"],
                        font_style=fields["m_fontStyle"], character_spacing=fields["m_characterSpacing"],
                        word_spacing=fields["m_wordSpacing"], line_spacing=fields["m_lineSpacing"],
                        margin=[fields["m_margin"][axis] for axis in ("x", "y", "z", "w")])
                    gradient = fields["m_fontColorGradient"]
                    preset = fields["m_fontColorGradientPreset"]
                    if preset["m_PathID"]:
                        gradient = text_schemas.read(PPtr(**preset, assetsfile=component.assets_file).deref())
                    target.setdefault("original_bindings", {})["TextMeshProUGUI"] = {
                        "m_enableVertexGradient": fields["m_enableVertexGradient"], "m_fontColorGradient": gradient}
                    text_sources[target["path"]] = {"source": apk.provenance(component),
                        "object_sha256": sha(component.get_raw_data())}
        pet_rules_path = ROOT / "unity/Assets/Game/Resources/Combat/pet-battle-rules.json"
        pet_rules = load_json(pet_rules_path)
        pet_skill_ids = set()
        for row in pet_rules["tables"]["pet_evolution"]:
            for field in ("EntityActiveSkills", "EntityPassiveSkills", "ActiveSkills", "PassiveSkills"):
                pet_skill_ids.update(row.get(field) or [])
            if row.get("EntitySkill"):
                pet_skill_ids.add(row["EntitySkill"])
        manifest = read_asset_manifest(apk.source.manifest_bytes("DefaultPackage", apk.archive))
        data["pet_skill_icons"] = {}
        for item in pet_rules["tables"]["item"]:
            if item["ClassId"] not in pet_skill_ids:
                continue
            address = "Assets/Image/" + item["RawIcon"]
            matches = [row for row in manifest if row["address"] == address
                       or row["path"].removesuffix(".png") == address]
            if len(matches) != 1:
                raise ValueError((item["ClassId"], address, matches))
            record, = matches
            obj, = [pointer.deref() for path, pointer in apk.container(record["bundle"])
                    if path == record["path"] and pointer.type.name == "Sprite"]
            raw = io.BytesIO()
            obj.read().image.save(raw, format="PNG")
            payload = raw.getvalue()
            uri = "res://assets/official/sprites/skill_aux_" + sha(payload) + ".png"
            sprites[uri] = {"bytes": payload, "source": apk.provenance(obj),
                            "sha256": sha(payload), "name": obj.read().m_Name,
                            "address": record, "object_sha256": sha(obj.get_raw_data())}
            data["pet_skill_icons"][str(item["ClassId"])] = uri
        if set(map(int, data["pet_skill_icons"])) != pet_skill_ids:
            raise ValueError("Pet skill icon item configuration is incomplete")
    finally:
        apk.archive.close()

    for value in data.values():
        def rebase_sprites(node):
            if isinstance(node, dict):
                for key, item in list(node.items()):
                    if isinstance(item, str) and item.startswith("res://assets/official/sprites/"):
                        node[key] = item.replace("res://assets/official/sprites/",
                                                 "res://assets/official/pet-skill-ui/sprites/")
                    else:
                        rebase_sprites(item)
            elif isinstance(node, list):
                for item in node:
                    rebase_sprites(item)
        rebase_sprites(value)

    meta_template_path = next((ROOT / "unity/Assets/Official/Skills/Art/sprites").glob("*.png.meta"), None)
    if meta_template_path is None:
        raise FileNotFoundError("Official skill sprite import metadata is absent")
    meta_template = meta_template_path.read_text(encoding="utf-8")
    additions = {}
    for uri, sprite in sprites.items():
        relative = uri.removeprefix("res://assets/official/sprites/")
        if not relative.startswith("skill_aux_") or not relative.endswith(".png"):
            raise ValueError("Unexpected recovered pet skill sprite URI: " + uri)
        target = (MODULE / "Art/sprites" / relative).resolve()
        if not target.is_relative_to((MODULE / "Art/sprites").resolve()):
            raise ValueError("Recovered pet skill sprite escaped the skill asset directory")
        if target.exists() and sha(target.read_bytes()) != sprite["sha256"]:
            raise ValueError("Pet skill sprite path already contains different bytes: " + relative)
        guid = uuid.uuid5(uuid.NAMESPACE_URL, "zjcs-official-pet-skill-ui/" + relative).hex
        meta = re.sub(r"(?m)^guid: .*$", "guid: " + guid, meta_template, count=1)
        meta = re.sub(r"(?m)[ \t]+$", "", meta)
        if not re.search(r"(?m)^guid: .+$", meta_template):
            raise ValueError("Official skill sprite metadata has no GUID")
        meta_path = target.with_suffix(target.suffix + ".meta")
        if meta_path.exists():
            existing_meta = meta_path.read_text(encoding="utf-8")
            if re.sub(r"(?m)[ \t]+$", "", existing_meta) != meta:
                raise ValueError("Pet skill sprite metadata differs from its stable identity: " + relative)
        additions[target] = sprite["bytes"]
        additions[meta_path] = meta.encode("utf-8")

    layout_bytes = json.dumps(data, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    output_files = []
    for target, raw in additions.items():
        output_files.append({"path": target.relative_to(ROOT).as_posix(),
                             "bytes": len(raw), "sha256": sha(raw)})
    output_files.append({"path": LAYOUT.relative_to(ROOT).as_posix(),
                         "bytes": len(layout_bytes), "sha256": sha(layout_bytes)})
    inputs = {
        "apk": {"path": apk.source.apk.relative_to(ROOT).as_posix(), "sha256": apk.source.apk_sha256,
                "manifest": apk.source.apk_version},
        "bundles": {key: data[key]["source"] for key in PET_SKILL_TEMPLATES},
        "official_code": [source_record(CODE / relative) for relative in PET_SKILL_CODE],
        "generator": source_record(ROOT / GENERATOR_PATH),
        "prefab_converter": source_record(ROOT / "scripts/prepare_skill_extra_ui.py"),
        "pet_battle_rules": source_record(pet_rules_path),
        "text_components": text_sources,
    }
    receipt = {"manifest": PINNED_VERSION, "operation": PET_OPERATION,
               "inputs": inputs, "outputs": sorted(output_files, key=lambda row: row["path"]),
               "sprite_sources": {uri.replace("res://assets/official/sprites/",
                                                  "res://assets/official/pet-skill-ui/sprites/"):
                                  {key: value for key, value in sprite.items() if key != "bytes"}
                                  for uri, sprite in sprites.items()}}
    receipt_bytes = (json.dumps(receipt, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    module_manifest = {"manifest": PINNED_VERSION,
                       "reference_receipt": {"path": RECEIPT.relative_to(ROOT).as_posix(),
                                             "sha256": sha(receipt_bytes)},
                       "prefabs": sorted(PET_SKILL_TEMPLATES),
                       "art_files": len(sprites),
                       "files": sorted(({**row, "path": row["path"].removeprefix("unity/")}
                                        for row in output_files), key=lambda row: row["path"])}
    module_manifest_bytes = (json.dumps(module_manifest, ensure_ascii=False, indent=2) + "\n").encode("utf-8")

    for target, raw in additions.items():
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists() or target.read_bytes() != raw:
            target.write_bytes(raw)
    LAYOUT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    LAYOUT.write_bytes(layout_bytes)
    RECEIPT.write_bytes(receipt_bytes)
    MODULE_MANIFEST.write_bytes(module_manifest_bytes)
    for meta_path in [MODULE.with_suffix(".meta"), *MODULE.rglob("*.meta")]:
        if not meta_path.is_file():
            continue
        raw = meta_path.read_bytes()
        normalized = re.sub(rb"(?m)[ \t]+$", b"", raw)
        if normalized != raw:
            meta_path.write_bytes(normalized)
    print(json.dumps({"templates": list(PET_SKILL_TEMPLATES), "sprites": len(sprites),
                      "layout_sha256": sha(layout_bytes), "receipt_sha256": sha(receipt_bytes)},
                     ensure_ascii=False))


if __name__ == "__main__":
    prepare()
