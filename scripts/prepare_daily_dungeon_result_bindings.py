"""Restore current official daily-dungeon result fields and Windows UI materials."""
from pathlib import Path
from functools import lru_cache
import hashlib
import json
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


@lru_cache(maxsize=1)
def load_native_export_receipt(path):
    raw = path.read_bytes()
    return json.loads(raw), digest(raw)


def write_meta(path, importer="DefaultImporter"):
    meta = Path(str(path) + ".meta")
    if not meta.exists():
        guid = digest(("zjcs-dungeon-result-binding:" + path.relative_to(ROOT).as_posix()).encode())[:32]
        meta.write_text("fileFormatVersion: 2\nguid: " + guid + "\n" + importer
                        + ":\n  externalObjects: {}\n", encoding="ascii")
    return meta


def restore_additive(apk, material):
    """Reuse a verified conversion of this exact current shader, including every variant."""
    from prepare_daily_dungeon_result_ui import EXPORT, write_folder_meta
    shader = material.read().m_Shader.deref()
    cache = ROOT / ".tools/source-packages/current/884_156692/native-conversion/all-assets/windows-shaders"
    receipt_path = cache / "source-manifest.json"
    receipt = json.loads(receipt_path.read_bytes())
    if receipt["manifest"] != apk.version:
        raise ValueError("Result shader conversion uses another official version")
    row, = [value for value in receipt["shaders"]
            if value["source"]["cab"].lower() == shader.assets_file.name.lower()
            and value["source"]["path_id"] == str(shader.path_id)]
    if row["source"]["object_sha256"] != digest(shader.get_raw_data()):
        raise ValueError("Result additive shader differs from its Windows conversion")
    source = cache / row["path"]
    if digest(source.read_bytes()) != row["sha256"]:
        raise ValueError("Converted additive shader changed")
    for program in row["programs"]:
        if digest((cache / program["path"]).read_bytes()) != program["sha256"]:
            raise ValueError("Original converted additive shader program changed")
    shader_path = ROOT / "unity/Assets/Game/Runtime/Official/Dungeons/UI/EffectAdditive.shader"
    for folder in (shader_path.parent.parent, shader_path.parent):
        write_folder_meta(folder)
    shader_path.write_bytes(source.read_bytes())
    shader_meta = write_meta(shader_path, "ShaderImporter")
    guid = re.search(r"^guid: (\w+)$", shader_meta.read_text(), re.M).group(1)
    export = EXPORT / "UIEffect/Material/UIImgAdd.mat"
    text = export.read_text(encoding="utf-8-sig")
    if "m_Name: UIImgAdd\n" not in text:
        raise ValueError("Original result material export changed")
    # The native export preserves all serialized material parameters and texture transforms.
    text, count = re.subn(r"  m_Shader: \{[^\n]+\}",
                         "  m_Shader: {fileID: 4800000, guid: " + guid + ", type: 3}", text)
    if count != 1 or any(value.m_Texture.path_id for _, value in material.read().m_SavedProperties.m_TexEnvs):
        raise ValueError("Review result additive material texture bindings")
    properties = material.read().m_SavedProperties
    saved = ["    serializedVersion: 3", "    m_TexEnvs:"]
    for name, value in properties.m_TexEnvs:
        saved += ["    - " + name + ":", "        m_Texture: {fileID: 0}",
                  "        m_Scale: {x: " + repr(value.m_Scale.x) + ", y: " + repr(value.m_Scale.y) + "}",
                  "        m_Offset: {x: " + repr(value.m_Offset.x) + ", y: " + repr(value.m_Offset.y) + "}"]
    for field in ("m_Ints", "m_Floats", "m_Colors"):
        values = getattr(properties, field)
        saved.append("    " + field + (":" if values else ": []"))
        for name, value in values:
            if field == "m_Colors":
                value = "{" + ", ".join(key + ": " + repr(getattr(value, key)) for key in ("r", "g", "b", "a")) + "}"
            else:
                value = repr(value)
            saved.append("    - " + name + ": " + value)
    # AssetRipper exports property maps; Unity's native YAML requires pair arrays.
    text, count = re.subn(r"  m_SavedProperties:\n.*?(?=  m_BuildTextureStacks:)",
                         "  m_SavedProperties:\n" + "\n".join(saved) + "\n", text, flags=re.S)
    if count != 1:
        raise ValueError("Original result material serialized property block changed")
    material_path = ROOT / "unity/Assets/Game/Resources/Dungeons/UI/884_156692/Materials/UIImgAdd.mat"
    write_folder_meta(material_path.parent)
    material_path.write_text(text, encoding="utf8", newline="\n")
    material_meta = write_meta(material_path, "NativeFormatImporter")
    return dict(source=apk.provenance(material), object_sha256=digest(material.get_raw_data()),
                shader_source=apk.provenance(shader), shader_object_sha256=digest(shader.get_raw_data()),
                conversion=row, conversion_receipt_sha256=digest(receipt_path.read_bytes()),
                export=dict(path=export.relative_to(ROOT).as_posix(), sha256=digest(export.read_bytes()))), [
                    shader_path, shader_meta, material_path, material_meta,
                    shader_path.parent.with_name(shader_path.parent.name + ".meta"),
                    shader_path.parent.parent.with_name(shader_path.parent.parent.name + ".meta"),
                    material_path.parent.with_name(material_path.parent.name + ".meta")]


def restore_popup_clip(apk, component, fields):
    from UnityPy.classes import PPtr
    from prepare_daily_dungeon_result_ui import EXPORT, PACKAGE_SCRIPT_GUIDS, write_folder_meta
    clip_bindings = {}
    receipts = []
    outputs = []
    native_export_receipt = ROOT / ".tools/source-packages/current/884_156692/native-conversion/native-export-all-source-manifest.json"
    native_export, native_export_hash = load_native_export_receipt(native_export_receipt)
    if native_export.get("manifest") != apk.version:
        raise ValueError("Original shared item-detail animations use another official version")

    for field in ("mShowClip", "mHideClip", "mLoopClip"):
        pointer = fields[field]
        if not pointer["m_PathID"]:
            clip_bindings[field] = None
            continue
        clip = PPtr(**pointer, assetsfile=component.assets_file).deref()
        clip_name = clip.read().m_Name
        if clip_name == "GameEndPanelBase_Show" and field == "mShowClip":
            source = EXPORT / "Prefabs/UI/Fight/GameEndPanelBase_Show.anim"
            native_receipt = ROOT / "assets/reference/dungeon-ui" / apk.version / "daily-dungeon-result-ui-source-manifest.json"
            if not native_receipt.exists():
                native_receipt = ROOT / "assets/reference/dungeon-ui/daily-dungeon-result-ui-source-manifest.json"
            native_source = json.loads(native_receipt.read_bytes())
            if native_source["manifest"] != apk.version:
                raise ValueError("Original shared result animation receipt uses another official version")
            record, = [row for row in native_source["shared_assets"]
                       if row["source"] == "Prefabs/UI/Fight/GameEndPanelBase_Show.anim"]
            if digest(source.read_bytes()) != record["sha256"]:
                raise ValueError("Original shared result animation export changed")
            text = source.read_text(encoding="utf-8-sig")

            def remap(match):
                guid = PACKAGE_SCRIPT_GUIDS.get((match.group(2), int(match.group(1))))
                if not guid:
                    raise ValueError("Unbound result animation component script")
                return "script: {fileID: 11500000, guid: " + guid + ", type: 3}"

            text = re.sub(r"script: \{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: 3\}", remap, text)
            target = ROOT / "unity/Assets/Game/Resources/Dungeons/UI/884_156692/Animations/GameEndPanelBase_Show.anim"
            write_folder_meta(target.parent)
            target.write_text(text, encoding="utf8", newline="\n")
            outputs.extend((target, write_meta(target, "NativeFormatImporter"), Path(str(target.parent) + ".meta")))
            receipts.append(dict(source=apk.provenance(clip), object_sha256=digest(clip.get_raw_data()),
                export=dict(path=source.relative_to(ROOT).as_posix(), sha256=digest(source.read_bytes())),
                source_receipt=dict(path=native_receipt.relative_to(ROOT).as_posix(),
                                    sha256=digest(native_receipt.read_bytes()))))
            clip_bindings[field] = dict(resource="Dungeons/UI/884_156692/Animations/GameEndPanelBase_Show",
                                        name=clip_name)
            continue

        # The four detail prefabs use different original popup animation sets.
        # Resolve the actual serialized clip, including its unique export path.
        records = [row for row in native_export.get("outputs", [])
                   if Path(row.get("path", "")).name == clip_name + ".anim"]
        if len(records) != 1:
            raise ValueError("Original popup animation export is ambiguous: " + field + "/" + clip_name)
        record, = records
        export_relative = record["path"]
        source = EXPORT / Path(export_relative).relative_to("ExportedProject/Assets")
        if not source.is_file() or digest(source.read_bytes()) != record.get("sha256"):
            raise ValueError("Original 884 item-detail popup animation export changed: " + clip_name)
        target = ROOT / "unity/Assets/Game/Resources/Dungeons/UI/884_156692/Animations" / (clip_name + ".anim")
        write_folder_meta(target.parent)
        target.write_bytes(source.read_bytes())
        outputs.extend((target, write_meta(target, "NativeFormatImporter"), Path(str(target.parent) + ".meta")))
        receipts.append(dict(source=apk.provenance(clip), object_sha256=digest(clip.get_raw_data()),
            export=dict(path=source.relative_to(ROOT).as_posix(), sha256=digest(source.read_bytes())),
            source_receipt=dict(path=native_export_receipt.relative_to(ROOT).as_posix(),
                                sha256=native_export_hash)))
        clip_bindings[field] = dict(resource="Dungeons/UI/884_156692/Animations/" + clip_name,
                                    name=clip_name)

    show, hide, loop = (clip_bindings.get(field) or {} for field in ("mShowClip", "mHideClip", "mLoopClip"))
    if not show.get("resource") or not show.get("name"):
        raise ValueError("Original daily dungeon popup has no supported show clip")
    binding = dict(showClipResource=show.get("resource"), showClipName=show.get("name"),
                   hideClipResource=hide.get("resource"), hideClipName=hide.get("name"),
                   loopClipResource=loop.get("resource"), loopClipName=loop.get("name"),
                   closeByClickMask=bool(fields["mCloseByClickMask"]), maskType=fields["mMaskType"])
    return binding, receipts, outputs


def result_claim_binding(root, fields, nodes):
    paths = {go.object_reader.path_id: "/".join(map(str, path)) for go, path, _, _ in nodes}
    references = {row["rid"]: row for row in fields["references"]["RefIds"]}

    def pointer_path(pointer):
        if pointer["m_FileID"] or not pointer["m_PathID"]:
            raise ValueError("Result claim state must reference its original local hierarchy")
        target = root.assets_file.objects[pointer["m_PathID"]].read(check_read=False)
        target_id = target.m_GameObject.path_id if hasattr(target, "m_GameObject") else pointer["m_PathID"]
        return paths[target_id]

    def configs(name):
        result = []
        for pointer in fields[name]["mStateConfigs"]:
            reference = references[pointer["rid"]]
            kind, data = reference["type"]["class"], reference["data"]
            if kind == "StateGameObjectConfig":
                result.append(dict(objects=[dict(state=row["State"]["mValue"],
                    paths=[pointer_path(value) for value in row["GameObjects"]])
                    for row in data["mStateGameObjectData"]["GoConfig"]]))
            elif kind == "StateMultiLanguageConfig":
                language = data["mStateMultiLanguageTextData"]
                result.append(dict(textPath=pointer_path(language["Text"]), texts=[
                    dict(state=row["Key"]["mValue"], key=row["Value"])
                    for row in language["Keys"]["mSerializeBuffer"]]))
            else:
                raise ValueError("Unbound original result claim state config: " + kind)
        return result
    return dict(freeStates=configs("FreeStateSwitcher"), awardTimesStates=configs("mAwardTimesSwitcher"))


def build():
    from official_apk import Apk
    from current_component_schemas import CurrentSchemas
    from prepare_character_creation_opening import SpriteCache
    from prepare_dungeon_ui_bindings import prefab_bindings, relative_transform_path, route_texts
    from prepare_daily_dungeon_result_ui import ROOTS
    from UnityPy.classes import PPtr

    apk = Apk.current()
    if apk.version != "884_156692":
        raise ValueError("Review result binding source selection")
    schemas, sprites = CurrentSchemas(), SpriteCache(apk)
    code = ROOT / ".tools/source-packages/current/884_156692/official-code"
    gray_path = ROOT / "unity/Assets/Official/World/Mainline/HandIn/handin-ui.json"
    gray = json.loads(gray_path.read_text(encoding="utf-8-sig"))["grayMaterial"]
    source_receipts, outputs, inputs, prefabs, keys = [], [], [], [], set()
    material_cache = {}
    roots = list(ROOTS.items()) + [("Assets/Prefabs/UI/Item/Attachments/ProbabilityGo.prefab",
                                    "defaultpackage_assets_prefabs_ui_item.bundle")]
    for asset, bundle in roots:
        name = Path(asset).stem
        if name in ("DailyDungeonTopPanel", "TopRankTeamItem"):
            continue
        binding, source = prefab_bindings(apk, schemas, sprites, name, bundle, "/" + name + ".prefab")
        root = apk.asset(bundle, "/" + name + ".prefab")
        localizations = {}
        nodes = list(relative_transform_path(root.read()))
        for go, path, names, _ in nodes:
            path_key = "/".join(map(str, path))
            for pair in go.m_Component:
                obj = pair.component.deref()
                if obj.type.name != "MonoBehaviour":
                    continue
                cls = obj.read(check_read=False).m_Script.read().m_ClassName
                if cls not in ("Image", "MultiLanguageText", "NoDrawingRayCast", "SafeArea", "AspectRatioFitter", "PopupWindow", "DailyDungeonCostMoneyContent", "AwardContent", "AwardPanelLayoutElement", "AwardLoopScrollRect"):
                    continue
                fields = schemas.read(obj)
                inputs.append(dict(prefab=name, objects=names, type=cls, source=apk.provenance(obj),
                                   sha256=digest(obj.get_raw_data())))
                if cls == "AwardLoopScrollRect":
                    def local_path(pointer):
                        if not pointer["m_PathID"]: return None
                        if pointer["m_FileID"]: raise ValueError("External award scroll reference")
                        target = root.assets_file.objects[pointer["m_PathID"]].read(check_read=False).m_GameObject.path_id
                        return next("/".join(map(str, index)) for go2, index, _, _ in nodes
                                    if go2.object_reader.path_id == target)
                    binding["awardScroll"] = dict(path=path_key, content=local_path(fields["m_Content"]),
                        viewport=local_path(fields["m_Viewport"]), reverseDirection=bool(fields["reverseDirection"]),
                        reverseIndex=bool(fields["reverseIndex"]), horizontal=bool(fields["m_Horizontal"]), vertical=bool(fields["m_Vertical"]),
                        movementType=fields["m_MovementType"], elasticity=fields["m_Elasticity"],
                        inertia=bool(fields["m_Inertia"]), decelerationRate=fields["m_DecelerationRate"],
                        scrollSensitivity=fields["m_ScrollSensitivity"])
                elif cls == "AwardContent":
                    binding["awardContent"] = dict(path=path_key, interval=fields["mPlayIntervalSeconds"],
                        showDelay=fields["mShowItemIconTime"])
                elif cls == "AwardPanelLayoutElement":
                    pointer = fields["mContentLayoutGroup"]
                    target = root.assets_file.objects[pointer["m_PathID"]].read(check_read=False).m_GameObject.path_id
                    grid_path, = ["/".join(map(str, index)) for go2, index, _, _ in nodes
                                   if go2.object_reader.path_id == target]
                    binding["awardLayout"] = dict(path=path_key, grid=grid_path,
                        minRow=fields["mMinRow"], maxHeight=fields["mMaxHeight"],
                        priority=fields["mLayoutPriority"], currentRow=fields["mCurrentRow"])
                elif cls == "DailyDungeonCostMoneyContent":
                    binding["resultClaim"] = result_claim_binding(root, fields, nodes)
                    for configs in binding["resultClaim"].values():
                        keys.update(row["key"] for config in configs for row in config.get("texts", []))
                elif cls == "PopupWindow":
                    binding["resultPopup"], records, files = restore_popup_clip(apk, obj, fields)
                    source_receipts.extend(records)
                    outputs.extend(files)
                elif cls == "MultiLanguageText" and fields["myKey"]:
                    localizations[path_key] = fields["myKey"]
                    keys.add(fields["myKey"])
                elif cls == "NoDrawingRayCast":
                    continue  # Restored by the shared native prefab binding extractor.
                elif cls == "SafeArea":
                    binding["safeAreas"].append(dict(path=path_key, objects=names, enabled=bool(fields["m_Enabled"]),
                        paddingTop=fields["PaddingTop"], paddingBottom=fields["PaddingBottom"],
                        disableTopAdapt=bool(fields["DisableTopAdapt"]), disableBottomAdapt=bool(fields["DisableBottomAdapt"])))
                elif cls == "AspectRatioFitter":
                    binding.setdefault("aspectRatioFitters", []).append(dict(path=path_key, objects=names,
                        enabled=bool(fields["m_Enabled"]), aspectMode=fields["m_AspectMode"], aspectRatio=fields["m_AspectRatio"]))
                elif cls == "Image" and fields["m_Material"]["m_PathID"]:
                    material = PPtr(**fields["m_Material"], assetsfile=obj.assets_file).deref()
                    material_name = material.read().m_Name
                    image, = [value for value in binding["images"] if value["path"] == path_key]
                    if material_name == "UIGray":
                        if apk.provenance(material) != gray["source"] or apk.provenance(material.read().m_Shader.deref()) != gray["shaderSource"]:
                            raise ValueError("Result gray material differs from the shared Windows material")
                        image["materialResource"] = "World/Mainline/HandIn/UIGray"
                    elif material_name == "UIImgAdd":
                        if material_name not in material_cache:
                            record, files = restore_additive(apk, material)
                            material_cache[material_name] = record
                            source_receipts.append(record)
                            outputs.extend(files)
                        image["materialResource"] = "Dungeons/UI/884_156692/Materials/UIImgAdd"
                    else:
                        raise ValueError("Unconverted original result Image material: " + material_name)
        binding["localizations"] = [dict(path=path, key=key) for path, key in localizations.items()]
        binding["layoutJson"] = json.dumps(dict(layouts=binding["layouts"], scrolls=[], buttons=[]))
        prefabs.append(binding)
        inputs.append(dict(prefab=name, source=source, sha256=digest(root.get_raw_data())))
    # Original LanguageUtils.GetDailyDungeonNameKey is used for the result difficulty caption.
    from official_tables import Constructors
    tables = (code / "common-source/Common/Tables.cs").read_text(encoding="utf-8-sig")
    table_type, = re.findall(r'new (Tb\w+)\(loader\("daily_dungeon_battle"\)\)', tables)
    table = apk.asset("config_luban", "/daily_dungeon_battle.g.bytes")
    raw_table = table.read().m_Script.encode("utf8", "surrogateescape")
    battle_rows = Constructors(code).table(raw_table, table_type)
    levels = [dict(dungeonId=row["DungeonBattleId"], difficulty=difficulty)
              for row in battle_rows for difficulty in row["DifficultyList"] or []]
    inputs.append(dict(table="daily_dungeon_battle", source=apk.provenance(table), sha256=digest(raw_table)))
    enum = (code / "common-source/Common/DailyDungeonDifficulty.cs").read_text(encoding="utf-8-sig")
    names = {int(value): name for name, value in re.findall(r"(\w+)\s*=\s*(\d+)", enum)}
    difficulties = [dict(dungeonId=row["dungeonId"], difficulty=row["difficulty"],
        key="ui_daily_dungeon_item_title_" + str(row["dungeonId"]) + "_" + names[row["difficulty"]]) for row in levels]
    keys.update(row["key"] for row in difficulties)
    texts, languages, missing = route_texts(apk, sorted(keys), [])
    translated = {row["key"]: row["value"] for row in texts}
    for prefab in prefabs:
        local = {row["path"]: translated[row["key"]] for row in prefab["localizations"]}
        for text in prefab["texts"]:
            if text["path"] in local:
                text["text"] = local[text["path"]]
    for row in difficulties:
        row["title"] = translated[row["key"]]
    target = ROOT / "unity/Assets/StreamingAssets/Dungeons/UI/884_156692/result-bindings.json"
    target.write_text(json.dumps(dict(manifest=apk.version, prefabs=prefabs, difficulties=difficulties),
                                ensure_ascii=False, indent=2) + "\n", encoding="utf8", newline="\n")
    outputs += [target, write_meta(target, "TextScriptImporter")]
    sprite_files = {image["sprite"]["file"] for prefab in prefabs for image in prefab["images"] if image["sprite"]}
    for relative in sorted(sprite_files):
        path = target.parent / relative
        outputs.extend((path, Path(str(path) + ".meta")))
    code_paths = [code / "client-source" / (name + ".cs") for name in
                  ("GameEndPanelDailyDungeon", "DailyDungeonCostMoneyContent", "DailyDungeonEndPanelItem", "AwardPanelItem", "ItemAwardIcon", "ItemIcon", "AwardContent", "AwardPanelLayoutElement")]
    code_paths.append(code / "common-source/Common/LanguageUtils.cs")
    code_paths.extend(code / "framework-source" / (name + ".cs") for name in
                      ("StateSwitcher", "StateGameObjectConfig", "StateMultiLanguageConfig", "StateConfigValue"))
    receipt = dict(manifest=apk.version, apk_manifest=apk.source.apk_version, apk_sha256=apk.source.apk_sha256,
        operation="Restore original result prefab Transform/Image/TMP/localization/raycast fields and current Windows material conversion.",
        inputs=inputs, material_conversions=source_receipts, language_inputs=languages, missing_translation_keys=missing,
        shared_material_receipt=dict(path=gray_path.relative_to(ROOT).as_posix(), sha256=digest(gray_path.read_bytes())),
        tools=[dict(path=p.relative_to(ROOT).as_posix(), sha256=digest(p.read_bytes())) for p in
               (Path(__file__), ROOT / "scripts/prepare_dungeon_ui_bindings.py", ROOT / "scripts/current_component_schemas.py")],
        code_sources=[dict(path=p.relative_to(ROOT).as_posix(), sha256=digest(p.read_bytes())) for p in code_paths],
        outputs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=digest(p.read_bytes())) for p in dict.fromkeys(outputs)])
    receipt_path = ROOT / "assets/reference/dungeon-ui/884_156692/daily-dungeon-result-bindings-source-manifest.json"
    receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf8", newline="\n")
    apk.close()
    print(json.dumps(dict(prefabs=len(prefabs), sprites=len(sprite_files), missing_translation_keys=missing)))


if __name__ == "__main__":
    build()
