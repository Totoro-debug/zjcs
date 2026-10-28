"""Restore original item configuration assets and exact quality Sprite references."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from prepare_unity_profession_native_inputs import ROOT, CACHE
from official_apk import Apk
import yaml
from UnityPy.classes import PPtr
from official_manifest import read_asset_manifest
from map13_native_schemas import NativeSchemas
from prepare_unity_profession_native_bindings import guid, sha
from prepare_unity_profession_reward_sprites import Dumper, source_header
from official_tables import Constructors


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path))


def build(unity_project=None):
    apk = Apk.current()
    if apk.version != "871_156598":
        raise ValueError("Wrong original item configuration version")
    code = ROOT / ".tools/source-packages/current/871_156598/official-code"
    schemas = NativeSchemas(code, unity_project=unity_project)
    manifest = read_asset_manifest(apk.source.manifest_bytes("DefaultPackage", apk.archive))
    exports = CACHE / "typed/export/ExportedProject/Assets"
    folder = CACHE / "typed/item-configs"
    outputs, originals, bindings = [], [], []
    for name in ("ItemIconConfig", "ItemIconAttachmentConfig"):
        path = "Assets/ScriptableConfig/Modules/ItemIcon/" + name + ".asset"
        entry, = [row for row in manifest if row["path"] == path]
        obj = apk.asset(entry["bundle"], "/" + name + ".asset")
        fields = obj.read_typetree(nodes=schemas.default.tree("Assembly-CSharp", name))
        script = exports / "Scripts/Assembly-CSharp" / (name + ".cs")
        if not script.exists():
            raise ValueError("Original config script is not in the verified export")
        native = source_header()
        native.update(m_GameObject={"fileID": 0}, m_Enabled=fields["m_Enabled"], m_EditorHideFlags=0,
            m_Script={"fileID": 11500000, "guid": guid(script), "type": 3},
            m_Name=fields["m_Name"], m_EditorClassIdentifier="")
        if fields["m_GameObject"]["m_PathID"]:
            raise ValueError("Original configuration is attached to a scene object")
        if name == "ItemIconConfig":
            native["QualityBgImages"] = []
            for pointer in fields["QualityBgImages"]:
                sprite = PPtr(**pointer, assetsfile=obj.assets_file).deref()
                sprite_name = sprite.read().m_Name
                candidates = list((exports / "Image/UI/ItemQuality").rglob(sprite_name + ".asset"))
                if len(candidates) != 1:
                    raise ValueError("Original quality Sprite export is ambiguous: " + sprite_name)
                target, = candidates
                identity = re.search(r"(?m)^--- !u!213 &(\d+)$", target.read_text(encoding="utf-8"))
                if not identity:
                    raise ValueError("Original quality Sprite has no native identity")
                reference = dict(fileID=int(identity[1]), guid=guid(target), type=2)
                native["QualityBgImages"].append(reference)
                bindings.append(dict(name=sprite_name, source=apk.provenance(sprite),
                    payload_sha256=hashlib.sha256(sprite.get_raw_data()).hexdigest(),
                    native_relative_path=target.relative_to(exports).as_posix(),
                    export=record(target), meta=record(target.with_name(target.name + ".meta")), reference=reference))
        else:
            native["Templates"] = fields["Templates"]
        relative = path.removeprefix("Assets/")
        target = folder / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        body = yaml.dump({"MonoBehaviour": native}, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
        target.write_text("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n" + body, encoding="utf-8", newline="\n")
        meta = target.with_name(target.name + ".meta")
        meta.write_text("fileFormatVersion: 2\nguid: " + entry["guid"] + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n", encoding="utf-8", newline="\n")
        outputs.append(dict(path=target.relative_to(ROOT).as_posix(), sha256=sha(target),
            native_relative_path=relative, meta_sha256=sha(meta), guid=entry["guid"]))
        originals.append(dict(source=apk.provenance(obj), payload_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(),
            fields=fields, asset_table=entry, schema_source=record(code / "client-source" / (name + ".cs"))))
    dynamic_path = ROOT / "assets/reference/profession-ui/dynamic-resources-source-manifest.json"
    dynamic = json.loads(dynamic_path.read_bytes())
    reward_ids = {row["item_id"] for row in dynamic["resources"] if row["kind"] == "reward_sprite"}
    constructors = Constructors(code)
    tables = {match[1]: match[2] for match in re.finditer(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source("Tables"))}
    table_inputs, values = [], {}
    for table_type in ("TbItemInfo", "TbChestInfo", "TbAppendAppearanceInfo"):
        name = tables[table_type]
        obj = apk.asset("config_luban", "/" + name + ".g.bytes")
        raw = obj.read().m_Script.encode("utf-8", "surrogateescape")
        rows = json.loads(json.dumps(constructors.table(raw, table_type)))
        values[table_type] = rows
        table_inputs.append(dict(name=name, type=table_type, source=apk.provenance(obj),
            sha256=hashlib.sha256(raw).hexdigest(), bytes=len(raw), rows=len(rows)))
    for table_type in ("TbChestInfo", "TbAppendAppearanceInfo"):
        if any(row["ClassId"] in reward_ids for row in values[table_type]):
            raise ValueError("Profession reward has a custom GetIconPath part; follow that source")
    reward_items = [row for row in values["TbItemInfo"] if row["ClassId"] in reward_ids]
    if len(reward_items) != 4 or any(row["IconEffect"] for row in reward_items):
        raise ValueError("Inspect original profession item scope and icon effects")
    colors = apk.asset("scriptableconfig_modules_systemsetting", "/GlobalColors.asset")
    color_fields, _ = schemas.read(colors)
    result = dict(format=1, manifest=apk.version, originals=originals, native_assets=outputs,
        quality_sprites=bindings, generator=record(Path(__file__)),
        schema_reader=record(ROOT / "scripts/map13_native_schemas.py"),
        dynamic_sources=record(dynamic_path), reward_items=reward_items, table_inputs=table_inputs,
        icon_path_sources=[record(code / "common-source/Common" / (name + ".cs")) for name in
            ("ItemInfo", "ItemPartInfo", "ChestInfoParser", "AppendAppearanceInfoParser")],
        global_colors=dict(source=apk.provenance(colors), payload_sha256=hashlib.sha256(colors.get_raw_data()).hexdigest(), fields=color_fields),
        operation="Recover every original item config field, retain all 42 attachment entries/GUID strings and all eight ordered quality references, including the repeated white Sprite. Resolve native quality pointers by original source object and verified native export identity. Add only Unity editor asset headers and explicit original script references.")
    proof = ROOT / "assets/reference/profession-ui/item-configs-source-manifest.json"
    proof.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Recovered two original item configs and", len(bindings), "ordered quality Sprite references")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity-project", type=Path)
    args = parser.parse_args()
    build(args.unity_project)
