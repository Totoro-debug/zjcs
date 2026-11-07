"""Restore the selected official leaderboard fields alongside its native prefabs."""
from pathlib import Path
import hashlib
import json
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def build():
    from official_apk import Apk
    from current_component_schemas import CurrentSchemas
    from prepare_character_creation_opening import SpriteCache
    from prepare_dungeon_ui_bindings import prefab_bindings, relative_transform_path, route_texts
    from prepare_dungeon_matching_panels import state_records
    from official_tables import Constructors
    import re

    apk = Apk.current()
    if apk.version != "884_156692":
        raise ValueError("Review leaderboard source version before reproduction")
    schemas, sprites = CurrentSchemas(), SpriteCache(apk)
    from UnityPy.classes import PPtr
    gray_record_path = ROOT / "unity/Assets/Official/World/Mainline/HandIn/handin-ui.json"
    gray_record = json.loads(gray_record_path.read_text(encoding="utf-8-sig"))["grayMaterial"]
    reused_materials = []
    code = ROOT / ".tools/source-packages/current/884_156692/official-code"
    table_name = "daily_dungeon_battle"
    table_source = (code / "common-source/Common/Tables.cs").read_text(encoding="utf-8-sig")
    table_type, = re.findall(r'new (Tb\w+)\(loader\("' + table_name + r'"\)\)', table_source)
    table_asset = apk.asset("config_luban", "/" + table_name + ".g.bytes")
    raw_table = table_asset.read().m_Script.encode("utf8", "surrogateescape")
    rows = Constructors(code).table(raw_table, table_type)
    levels = [dict(dungeonId=row["DungeonBattleId"], difficulty=int(difficulty), grades=grades)
              for row in rows for difficulty, grades in (row["TopRecordGradeTypeDict"] or {}).items()]
    config_path = ROOT / "unity/Assets/Game/Resources/Dungeons/daily-dungeon-leaderboards.json"
    config_path.write_text(json.dumps(dict(manifest=apk.version, levels=levels), indent=2) + "\n",
                           encoding="utf8", newline="\n")
    config_meta = config_path.with_name(config_path.name + ".meta")
    if not config_meta.exists():
        config_meta.write_text("fileFormatVersion: 2\nguid: " + digest(b"zjcs-dungeon-leaderboards-884")[:32]
                               + "\nTextScriptImporter:\n  externalObjects: {}\n", encoding="ascii")
    prefabs, inputs, keys = [], [], set()
    for name, bundle in (("DailyDungeonTopPanel", "prefabs_ui_dailydungeon"),
                         ("TopRankTeamItem", "prefabs_ui_top")):
        binding, source = prefab_bindings(apk, schemas, sprites, name, bundle, "/" + name + ".prefab")
        root = apk.asset(bundle, "/" + name + ".prefab")
        nodes = list(relative_transform_path(root.read()))
        by_id = {go.object_reader.path_id: (path, names) for go, path, names, _ in nodes}

        def reference(pointer):
            if not pointer["m_PathID"]:
                return None
            if pointer["m_FileID"]:
                raise ValueError("External leaderboard hierarchy reference")
            target = root.assets_file.objects[pointer["m_PathID"]].read(check_read=False)
            identity = target.m_GameObject.path_id if hasattr(target, "m_GameObject") else pointer["m_PathID"]
            return "/".join(map(str, by_id[identity][0]))

        binding["localizations"] = []
        for go, path, names, _ in nodes:
            for pair in go.m_Component:
                obj = pair.component.deref()
                if obj.type.name != "MonoBehaviour":
                    continue
                cls = obj.read(check_read=False).m_Script.read().m_ClassName
                if cls == "NoDrawingRayCast":
                    fields = schemas.read(obj)
                    binding.setdefault("raycastGraphics", []).append(dict(
                        path="/".join(map(str, path)), objects=names,
                        enabled=bool(fields["m_Enabled"]),
                        color=[fields["m_Color"][key] for key in ("r", "g", "b", "a")],
                        raycastTarget=bool(fields["m_RaycastTarget"]),
                        raycastPadding=[fields["m_RaycastPadding"][key] for key in ("x", "y", "z", "w")]))
                    inputs.append(dict(prefab=name, objects=names, type=cls, source=apk.provenance(obj),
                                       sha256=digest(obj.get_raw_data())))
                    continue
                if cls == "Image":
                    fields = schemas.read(obj)
                    pointer = fields["m_Material"]
                    if pointer["m_PathID"]:
                        material = PPtr(**pointer, assetsfile=obj.assets_file).deref()
                        if material.read().m_Name == "UIGray":
                            material_source = apk.provenance(material)
                            shader_source = apk.provenance(material.read().m_Shader.deref())
                            if material_source != gray_record["source"] or shader_source != gray_record["shaderSource"]:
                                raise ValueError("Dungeon UI/Gray differs from the restored official material")
                            image, = [value for value in binding["images"] if value["path"] == "/".join(map(str, path))]
                            image["materialResource"] = "World/Mainline/HandIn/UIGray"
                            reused_materials.append(dict(prefab=name, objects=names, source=material_source,
                                                         shader_source=shader_source))
                    continue
                if cls not in ("TeamTopContent", "TopRankTeamItem", "MultiLanguageText", "AchievementPanelItem"):
                    continue
                fields = schemas.read(obj)
                inputs.append(dict(prefab=name, objects=names, type=cls, source=apk.provenance(obj),
                                   sha256=digest(obj.get_raw_data())))
                if cls == "AchievementPanelItem":
                    refs = {value["rid"]: value for value in fields["references"]["RefIds"]}
                    configs = fields["StateSwitcher"]["mStateConfigs"]
                    if {refs[value["rid"]]["type"]["class"] for value in configs} != {"StateGameObjectConfig", "StateUIColorConfig"}:
                        raise ValueError("Review original achievement state configurations")
                    object_fields = dict(fields, StateSwitcher=dict(mStateConfigs=[value for value in configs
                        if refs[value["rid"]]["type"]["class"] == "StateGameObjectConfig"]))
                    states = state_records(object_fields, "StateSwitcher", by_id)
                    for state in states:
                        state["state"] = int(state["state"])
                    colors = []
                    for value in configs:
                        config = refs[value["rid"]]
                        if config["type"]["class"] != "StateUIColorConfig":
                            continue
                        for graphic in config["data"]["mStateColorData"]["GraphicConfigs"]:
                            for color in graphic["Color"]["mSerializeBuffer"]:
                                colors.append(dict(state=int(color["Key"]["mValue"]),
                                    objects=[reference(pointer) for pointer in graphic["Graphics"]],
                                    color=[color["Value"][key] for key in ("r", "g", "b", "a")]))
                    binding.setdefault("achievementRows", []).append(
                        dict(path="/".join(map(str, path)), states=states, colors=colors))
                    continue
                if cls == "MultiLanguageText":
                    key = fields["myKey"]
                    if key:
                        keys.add(key)
                        binding["localizations"].append(dict(path="/".join(map(str, path)), key=key))
                    continue
                fields_binding = {field: reference(fields[field]) for field in
                                  (("mScrollRect", "mMeTopItem", "mContent") if cls == "TeamTopContent"
                                   else ("mRankText",))}
                state_fields = (("mEmptyStateSwitcher", "mMyTopStateSwitcher") if cls == "TeamTopContent"
                                else ("mMeStateSwitcher", "mTopStateSwticher"))
                for field in state_fields:
                    states = state_records(fields, field, by_id)
                    for state in states:
                        state["state"] = int(state["state"])
                    fields_binding[field] = states
                if cls == "TeamTopContent" or name == "TopRankTeamItem":
                    binding["leaderboard"] = fields_binding
        prefabs.append(binding)
        inputs.append(dict(prefab=name, source=source, sha256=digest(root.get_raw_data())))
    texts, language_inputs, missing = route_texts(apk, sorted(keys), [])
    translations = {row["key"]: row["value"] for row in texts}
    for prefab in prefabs:
        localized = {row["path"]: translations[row["key"]] for row in prefab["localizations"]}
        for text in prefab["texts"]:
            if text["path"] in localized:
                text["text"] = localized[text["path"]]
    target = ROOT / "unity/Assets/StreamingAssets/Dungeons/UI/leaderboard-bindings.json"
    payload = (json.dumps(dict(manifest=apk.version, prefabs=prefabs, texts=texts),
                          ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode()
    target.write_bytes(payload)
    meta = target.with_name(target.name + ".meta")
    if not meta.exists():
        meta.write_text("fileFormatVersion: 2\nguid: " + digest(b"zjcs-dungeon-leaderboard-bindings-884")[:32]
                        + "\nTextScriptImporter:\n  externalObjects: {}\n", encoding="ascii")
    outputs = [target, meta, config_path, config_meta]
    shared_assets = [gray_record_path, ROOT / gray_record["shaderPath"].replace("Assets/", "unity/Assets/", 1),
                    ROOT / "unity/Assets/Game/Resources/World/Mainline/HandIn/UIGray.mat"]
    sprite_files = {image["sprite"]["file"] for prefab in prefabs for image in prefab["images"] if image["sprite"]}
    for relative in sorted(sprite_files):
        path = target.parent / relative
        outputs.extend((path, path.with_name(path.name + ".meta")))
    code_paths = [code / "client-source" / (name + ".cs") for name in
                  ("DailyDungeonTopPanel", "TeamTopContent", "TopRankTeamItem", "DailyDungeonTabItem", "AchievementPanelItem",
                   "TeamTopContentDataDailyDungeon", "LayoutElementClamper",
                   "UnityEngine.UI.Ext/TabItem", "UnityEngine.UI.Ext/TabGroup", "UnityEngine.UI.Ext/TabAnimatorTransition")]
    code_paths.append(code / "framework-source/UGUIJszx/HorizontalLayoutGroupJSZX.cs")
    code_paths.extend(code / "common-source/Common" / (name + ".cs") for name in
                      ("Tables", table_type, "DailyDungeonBattleInfo", "TbItemInfo", "ItemInfo"))
    code_paths.append(code / "framework-firstpass-source/NoDrawingRayCast.cs")
    code_paths.extend(code / "framework-source" / (name + ".cs") for name in
                      ("StateGameObjectConfig", "StateGameObjectData"))
    receipt = dict(manifest=apk.version, apk_manifest=apk.source.apk_version, apk_sha256=apk.source.apk_sha256,
        operation="Read original Image/TMP/Transform, localization and leaderboard object-state fields; native hierarchy, layout and loop-scroll are imported by prepare_daily_dungeon_result_ui.py.",
        inputs=inputs, language_inputs=language_inputs, missing_translation_keys=missing,
        reused_materials=reused_materials,
        shared_assets=[dict(path=p.relative_to(ROOT).as_posix(), sha256=digest(p.read_bytes())) for p in shared_assets],
        leaderboard_config=dict(table=table_name, source=apk.provenance(table_asset), sha256=digest(raw_table),
                                levels=len(levels), field="TopRecordGradeTypeDict"),
        tools=[dict(path=p.relative_to(ROOT).as_posix(), sha256=digest(p.read_bytes())) for p in
               (Path(__file__), ROOT / "scripts/prepare_dungeon_ui_bindings.py", ROOT / "scripts/current_component_schemas.py")],
        code_sources=[dict(path=p.relative_to(ROOT).as_posix(), sha256=digest(p.read_bytes())) for p in code_paths],
        outputs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=digest(p.read_bytes())) for p in outputs])
    receipt_path = ROOT / "assets/reference/dungeon-ui/daily-dungeon-leaderboard-source-manifest.json"
    receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf8", newline="\n")
    print(json.dumps(dict(prefabs=len(prefabs), sprites=len(sprite_files), missing_translation_keys=missing)))


if __name__ == "__main__":
    build()
