"""Decode selected official roadblock UI through the shared native UI pipeline."""
from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path

from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from map11_support_ui import Map11SupportUi
from prepare_unity_map11_ui import extract_panels
from prepare_map16_story_source import decode_localization
from prepare_unity_story_sound_runtime import runtime_meta
from prepare_unity_map13_prop_parts import meta
from UnityPy.classes.PPtr import PPtr

VERSION = "884_156692"
KEYS = ("RoadblockTeamPropStageDialog", "RoadblockTeamPropStageDialogItem",
        *(f"{i}_14_DialogIcon" for i in (8625, 8626, 8634, 8637, 8633)), "TipInfo1", "RuleDialog")
BUNDLE = "defaultpackage_assets_prefabs_ui_roadblock.bundle"
OUTPUT = ROOT / "unity/Assets/Official/World/Map/14/UI/Roadblock"


def build():
    with Apk.current() as apk:
        if apk.version != VERSION:
            raise ValueError("Roadblock UI requires the selected 884 source")
        code_root = ROOT / ".tools/source-packages/current" / VERSION / "official-code"
        print("Loading the selected official UI schemas", flush=True)
        schemas = ScriptSchemas(code_root=code_root)
        exporter = Map11SupportUi(apk, schemas, OUTPUT / "Sprites", code_root=code_root)
        source, manifest = {}, {"prefabs": {}}
        for key in KEYS:
            bundle = BUNDLE if key.startswith("Roadblock") else "defaultpackage_assets_prefabs_ui_worldmap.bundle"
            if key == "TipInfo1": bundle = "defaultpackage_assets_prefabs_ui_common.bundle"
            if key == "RuleDialog": bundle = "defaultpackage_assets_prefabs_ui_ruledialog.bundle"
            print("Decoding original prefab: " + key, flush=True)
            native, _ = exporter.object_tree(key, bundle)
            nodes = native["nodes"]
            for node in nodes:
                node["children"] = []
            for node in nodes[1:]:
                nodes[node["parentIndex"]]["children"].append(node)
            source[key] = nodes[0]
            manifest["prefabs"][key] = dict(native["source"], bundle=bundle)
        panels, components, sprites, shaders = extract_panels(
            apk, schemas, source, manifest, KEYS, source, exporter.ui_schemas,
            exporter, b"", output_root=OUTPUT, preserve_native_transforms=True,
            restore_component_image_colors=True)
        # The shared renderer records visual components. Preserve the remaining
        # original controllers and their object references for explicit binding.
        extras, animators = [], []
        hand_in = json.loads((ROOT / "unity/Assets/Official/World/Mainline/HandIn/handin-ui.json").read_text(encoding="utf8"))
        for key in KEYS:
            bundle = manifest["prefabs"][key]["bundle"]
            paths, objects = {}, []
            def visit(obj, prefix=""):
                go = obj.read()
                path = prefix + "/" + go.m_Name
                paths[(obj.assets_file.name, obj.path_id)] = path
                for pair in go.m_Component:
                    child = pair.component.deref()
                    paths[(child.assets_file.name, child.path_id)] = path
                    if child.type.name in ("RectTransform", "Transform"):
                        for ref in child.read().m_Children:
                            visit(ref.read().m_GameObject.deref(), path)
                    elif child.type.name == "Animator":
                        fields = child.read_typetree()
                        controller = child.read().m_Controller
                        if controller.path_id:
                            native = controller.deref()
                            digest = hashlib.sha256(native.get_raw_data()).hexdigest()
                            if digest != hand_in["buttonAnimation"]["source"]["objectSha256"]:
                                raise ValueError("Roadblock button controller differs from the verified original controller")
                        animators.append(dict(panel=key, path=path,
                            settings={k:v for k,v in fields.items() if k not in ("m_GameObject", "m_Avatar", "m_Controller")},
                            controllerSource=apk.provenance(controller.deref()) if controller.path_id else None,
                            source=apk.provenance(child)))
                    elif child.type.name == "MonoBehaviour":
                        objects.append(child)
            visit(apk.asset(bundle, "/" + key + ".prefab"))
            def resolve(value, owner):
                if isinstance(value, dict):
                    if "m_FileID" in value and "m_PathID" in value:
                        if not value["m_PathID"]:
                            return None
                        obj = PPtr(**value, assetsfile=owner.assets_file).deref()
                        path = paths.get((obj.assets_file.name, obj.path_id))
                        if path is not None:
                            return path
                        result = dict(source=apk.provenance(obj), type=obj.type.name)
                        if obj.type.name == "MonoBehaviour" and obj.read(check_read=False).m_Script.read().m_ClassName == "WwiseEventReference":
                            result["event"] = schemas.read(obj)
                        return result
                    return {k: resolve(v, owner) for k, v in value.items()}
                if isinstance(value, list):
                    return [resolve(v, owner) for v in value]
                return value
            known = {(c["path"], c["type"]) for c in components if c["panel"] == key}
            for obj in objects:
                cls = obj.read(check_read=False).m_Script.read().m_ClassName
                path = paths[(obj.assets_file.name, obj.path_id)]
                if (path, cls) not in known:
                    if cls not in ("RoadblockTeamPropStageTouchUI", "RoadblockTeamPropStageTouchUIItem",
                                   "TeamBattleDialogItem", "MaintainGlobalScaleUI", "ButtonSound",
                                   "PopupWindowSound", "UIBloomGroup", "CountRedDotImage", "PreviewTool",
                                   "PlayerIcon", "PlayerIconMe", "PlayerIconOther", "RuleBtn", "RuleDialog", "TipInfo"):
                        continue
                    print("Binding original controller: " + cls, flush=True)
                    extras.append(dict(panel=key, path=path, type=cls,
                                       fields=resolve(schemas.read(obj), obj), source=apk.provenance(obj)))
        wanted = {node["localization_key"] for panel in panels for node in panel["nodes"]
                  if node["localization_key"]}
        for class_id in (8625, 8626, 8634, 8637, 8633):
            wanted.update((f"entity_{class_id}_name", f"entity_{class_id}_desc", f"entity_{class_id}_funcdesc"))
        wanted.add("ui_roadblock_stage_team_prop_describe")
        npc_keys = json.loads((ROOT / "unity/Assets/Game/Resources/Map12/Team/npc-assistance-texts.json").read_text(encoding="utf8"))
        wanted.update(r["key"] for r in npc_keys["texts"])
        from official_tables import Constructors
        constructors = Constructors(code_root)
        npc_table = apk.asset("config_luban", "Assets/Config/Luban/npc.g.bytes")
        npc_raw = npc_table.read().m_Script.encode("utf8", "surrogateescape")
        npc_rows = constructors.table(npc_raw, "TbNpcInfo")
        wanted.update(row["NameKey"] for row in npc_rows
                      if row["NpcType"] != 2 and row["FightEntityId"] > 0)
        manifest["npcNames"] = dict(provenance=apk.provenance(npc_table),
            payloadSha256=hashlib.sha256(npc_raw).hexdigest(),
            constructorSha256=hashlib.sha256((code_root / "common-source/Common/NpcInfo.cs").read_bytes()).hexdigest())
        table = apk.asset("config_luban", "Assets/Config/Luban/rule_tip.g.bytes")
        rows = constructors.table(table.read().m_Script.encode("utf8", "surrogateescape"), "TbRuleTipInfo")
        orders = sorted(r["Order"] for r in rows if r["RuleKey"] == "PropStageDialogRule")
        if orders != [0, 1, 2, 3]: raise ValueError("Original roadblock rule-tip order changed")
        wanted.update(f"rule_tip_PropStageDialogRule_{i}" for i in orders)
        wanted.update("PropType." + name for name in ("WindDamageReduce", "WaterDamageAdd",
            "DarkDamageReduce", "FireDamageReduce", "LightDamageReduce"))
        # Original TipInfo1 carries an unused editor placeholder; SetRule replaces it.
        literals = {"{0}", "+{0}", "0", "1", "NormalMaterial.Info1"}
        texts, language_inputs = decode_localization(apk, wanted, wanted - literals)
        for key in wanted & literals:
            texts.setdefault(key, key)
        tables = json.loads((ROOT / "unity/Assets/Official/World/Map/14/Source/tables.json").read_text(encoding="utf8"))
        backgrounds = []
        for row in tables["Map/map_ui_display"]:
            if row["MapClassId"] != 14:
                continue
            name = row["MapBgPath"].rsplit("/", 1)[1]
            objects = [o for o in apk.objects("defaultpackage_assets_image_ui_itemnotes.bundle")
                       if o.type.name == "Sprite" and o.read().m_Name == name]
            if len(objects) != 1:
                raise ValueError("Original roadblock background is ambiguous: " + name)
            record = exporter.add_sprite(objects[0])
            sprites[record["file"]] = record
            backgrounds.append(dict(areaTag=row["AreaTag"], uri=record["file"]))
        records = []
        for uri, sprite in sorted(sprites.items()):
            path = ROOT / uri.removeprefix("res://")
            records.append(dict(uri=uri, unityAssetPath=path.relative_to(ROOT / "unity").as_posix(),
                                sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                                width=sprite["width"], height=sprite["height"],
                                border=sprite["border"], pivot=sprite["pivot"],
                                pixelsPerUnit=sprite["ppu"]))
            template = ROOT / "unity/Assets/Official/World/Map/14/UI/HandIn/Sprites/ImgBg_PopUps-20ee64ac9f.png.meta"
            raw = template.read_text(encoding="utf8")
            guid = hashlib.md5(("zjcs-roadblock:" + uri).encode()).hexdigest()
            raw = re.sub(r"(?m)^guid: .*", "guid: " + guid, raw)
            raw = re.sub(r"(?m)^  spritePixelsToUnits: .*", f"  spritePixelsToUnits: {sprite['ppu']:g}", raw)
            pivot = [sprite["pivot"][0] / sprite["width"], sprite["pivot"][1] / sprite["height"]]
            raw = re.sub(r"(?m)^  spritePivot: .*", f"  spritePivot: {{x: {pivot[0]}, y: {pivot[1]}}}", raw)
            border = ", ".join(f"{axis}: {value:g}" for axis, value in zip("xyzw", sprite["border"]))
            raw = re.sub(r"(?m)^  spriteBorder: .*", "  spriteBorder: {" + border + "}", raw)
            Path(str(path) + ".meta").write_text(raw, encoding="utf8")
        for panel in panels:
            for node in panel["nodes"]:
                if node["localization_key"]:
                    node["text_value"] = texts[node["localization_key"]]
        for component in components:
            if component["type"] != "PopupWindow":
                continue
            for record in component["fields"]["clips"]:
                native = next(o for o in apk.objects(record["source"]["bundle"])
                              if o.path_id == int(record["source"]["path_id"]))
                record["frameRate"] = native.read().m_SampleRate
                record["objectSha256"] = hashlib.sha256(native.get_raw_data()).hexdigest()
        payload = dict(manifest=VERSION, mapClassId=14, panels=panels, components=components,
                       nativeControllers=extras, animators=animators, backgrounds=backgrounds,
                       areas=[dict(area=r["Area"], areaTag=r["AreaTag"]) for r in tables["Map/map_area"] if r["ClassId"] == 14],
                       sprites=records, texts=[dict(key=k, value=v) for k, v in sorted(texts.items())],
                       shaderPrograms=shaders, source=manifest, languageInputs=language_inputs,
                       resources=exporter.resources, schemas=schemas.records,
                       uiSchemas=exporter.ui_schemas.records)
        colors_asset = apk.asset("scriptableconfig_modules_systemsetting", "/GlobalColors.asset")
        colors = schemas.read(colors_asset)
        payload["conditionColors"] = {name: colors[name] for name in ("EnoughColor", "LackColor")}
        payload["globalColorsSource"] = apk.provenance(colors_asset)
        mask_document = ROOT / "unity/Assets/Official/Skills/skill-ui.json"
        mask = json.loads(mask_document.read_text(encoding="utf8"))["popup_masks"]["3"]
        prefix = "/WorldCanvas/" + mask["prefab_path"]
        def mask_relative(field):
            path = mask["fields"][field]
            if not path.startswith(prefix):
                raise ValueError("The retained original blur mask reference left its root")
            return path[len(prefix):].lstrip("/")
        payload["mask"] = dict(prefabResource=mask["prefab_resource"], prefabPath=mask["prefab_path"],
            blurMaterial=mask["blur_material"], buttonPath=mask_relative("mBtn"),
            imagePath=mask_relative("mImage"), uvRect=mask["raw_image"]["m_UVRect"])
        payload["retainedMaskSource"] = dict(document=mask_document.relative_to(ROOT).as_posix(),
            sha256=hashlib.sha256(mask_document.read_bytes()).hexdigest(), binding=mask)
        OUTPUT.mkdir(parents=True, exist_ok=True)
        target = OUTPUT / "roadblock-ui.json"
        target.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf8")
        meta(target)
        for folder in (OUTPUT / "Sprites", OUTPUT):
            runtime_meta(folder)
        receipt = ROOT / "assets/reference/npc-assistance/roadblock-ui-source-manifest.json"
        receipt.write_text(json.dumps(dict(manifest=VERSION, source=manifest,
            operation="Decode original hierarchy and native component fields through the shared official UI converter; preserve native transforms, colors, sprite geometry and localization. Windows binding and runtime validation remain required.",
            outputs=[dict(path=target.relative_to(ROOT).as_posix(),
                          sha256=hashlib.sha256(target.read_bytes()).hexdigest()),
                     *[dict(path=("unity/" + record["unityAssetPath"]), sha256=record["sha256"])
                       for record in records]],
            tool=dict(path=Path(__file__).relative_to(ROOT).as_posix(),
                      sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest())),
            ensure_ascii=False, indent=2) + "\n", encoding="utf8")
        print(json.dumps(dict(panels={p["key"]: len(p["nodes"]) for p in panels},
                              components=len(components), sprites=len(records),
                              sha256=hashlib.sha256(target.read_bytes()).hexdigest())))


if __name__ == "__main__":
    build()
