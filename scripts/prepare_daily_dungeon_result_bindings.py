"""Restore current official daily-dungeon result fields and Windows UI materials."""
from pathlib import Path
import hashlib
import json
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


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
    clip = PPtr(**fields["mShowClip"], assetsfile=component.assets_file).deref()
    if clip.read().m_Name != "GameEndPanelBase_Show" or fields["mHideClip"]["m_PathID"] or fields["mLoopClip"]["m_PathID"]:
        raise ValueError("Review original daily dungeon result popup clips")
    source = EXPORT / "Prefabs/UI/Fight/GameEndPanelBase_Show.anim"
    native_receipt = ROOT / "assets/reference/dungeon-ui/daily-dungeon-result-ui-source-manifest.json"
    record, = [row for row in json.loads(native_receipt.read_bytes())["shared_assets"]
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
    path = ROOT / "unity/Assets/Game/Resources/Dungeons/UI/884_156692/Animations/GameEndPanelBase_Show.anim"
    write_folder_meta(path.parent)
    path.write_text(text, encoding="utf8", newline="\n")
    metadata = write_meta(path, "NativeFormatImporter")
    binding = dict(showClipResource="Dungeons/UI/884_156692/Animations/GameEndPanelBase_Show",
                   showClipName=clip.read().m_Name, closeByClickMask=bool(fields["mCloseByClickMask"]),
                   maskType=fields["mMaskType"])
    receipt = dict(source=apk.provenance(clip), object_sha256=digest(clip.get_raw_data()),
                   export=dict(path=source.relative_to(ROOT).as_posix(), sha256=digest(source.read_bytes())),
                   source_receipt=dict(path=native_receipt.relative_to(ROOT).as_posix(), sha256=digest(native_receipt.read_bytes())))
    return binding, receipt, [path, metadata, Path(str(path.parent) + ".meta")]


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
    for asset, bundle in ROOTS.items():
        name = Path(asset).stem
        if name in ("DailyDungeonTopPanel", "TopRankTeamItem"):
            continue
        binding, source = prefab_bindings(apk, schemas, sprites, name, bundle, "/" + name + ".prefab")
        root = apk.asset(bundle, "/" + name + ".prefab")
        localizations = {}
        for go, path, names, _ in relative_transform_path(root.read()):
            path_key = "/".join(map(str, path))
            for pair in go.m_Component:
                obj = pair.component.deref()
                if obj.type.name != "MonoBehaviour":
                    continue
                cls = obj.read(check_read=False).m_Script.read().m_ClassName
                if cls not in ("Image", "MultiLanguageText", "NoDrawingRayCast", "SafeArea", "AspectRatioFitter", "PopupWindow"):
                    continue
                fields = schemas.read(obj)
                inputs.append(dict(prefab=name, objects=names, type=cls, source=apk.provenance(obj),
                                   sha256=digest(obj.get_raw_data())))
                if cls == "PopupWindow":
                    binding["resultPopup"], record, files = restore_popup_clip(apk, obj, fields)
                    source_receipts.append(record)
                    outputs.extend(files)
                elif cls == "MultiLanguageText" and fields["myKey"]:
                    localizations[path_key] = fields["myKey"]
                    keys.add(fields["myKey"])
                elif cls == "NoDrawingRayCast":
                    binding.setdefault("raycastGraphics", []).append(dict(path=path_key, objects=names,
                        enabled=bool(fields["m_Enabled"]), raycastTarget=bool(fields["m_RaycastTarget"]),
                        color=[fields["m_Color"][key] for key in ("r", "g", "b", "a")],
                        raycastPadding=[fields["m_RaycastPadding"][key] for key in ("x", "y", "z", "w")]))
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
    target = ROOT / "unity/Assets/StreamingAssets/Dungeons/UI/result-bindings.json"
    target.write_text(json.dumps(dict(manifest=apk.version, prefabs=prefabs, difficulties=difficulties),
                                ensure_ascii=False, indent=2) + "\n", encoding="utf8", newline="\n")
    outputs += [target, write_meta(target, "TextScriptImporter")]
    sprite_files = {image["sprite"]["file"] for prefab in prefabs for image in prefab["images"] if image["sprite"]}
    for relative in sorted(sprite_files):
        path = target.parent / relative
        outputs.extend((path, Path(str(path) + ".meta")))
    code_paths = [code / "client-source" / (name + ".cs") for name in
                  ("GameEndPanelDailyDungeon", "DailyDungeonEndPanelItem", "AwardPanelItem", "ItemAwardIcon", "ItemIcon")]
    code_paths.append(code / "common-source/Common/LanguageUtils.cs")
    receipt = dict(manifest=apk.version, apk_manifest=apk.source.apk_version, apk_sha256=apk.source.apk_sha256,
        operation="Restore original result prefab Transform/Image/TMP/localization/raycast fields and current Windows material conversion.",
        inputs=inputs, material_conversions=source_receipts, language_inputs=languages, missing_translation_keys=missing,
        shared_material_receipt=dict(path=gray_path.relative_to(ROOT).as_posix(), sha256=digest(gray_path.read_bytes())),
        tools=[dict(path=p.relative_to(ROOT).as_posix(), sha256=digest(p.read_bytes())) for p in
               (Path(__file__), ROOT / "scripts/prepare_dungeon_ui_bindings.py", ROOT / "scripts/current_component_schemas.py")],
        code_sources=[dict(path=p.relative_to(ROOT).as_posix(), sha256=digest(p.read_bytes())) for p in code_paths],
        outputs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=digest(p.read_bytes())) for p in dict.fromkeys(outputs)])
    receipt_path = ROOT / "assets/reference/dungeon-ui/daily-dungeon-result-bindings-source-manifest.json"
    receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf8", newline="\n")
    apk.close()
    print(json.dumps(dict(prefabs=len(prefabs), sprites=len(sprite_files), missing_translation_keys=missing)))


if __name__ == "__main__":
    build()
