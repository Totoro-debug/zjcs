"""Extract the fixed-version Map10 story portraits and their native layout."""
from __future__ import annotations

from io import BytesIO
import hashlib
import json
from pathlib import Path

from official_apk import Apk
from official_unity_schema import ScriptSchemas


ROOT = Path(__file__).resolve().parents[1]
VERSION = "854_156383"
UNITY_ROOT = ROOT / "unity/Assets/Official/World/Map/10"
RESOURCE_ROOT = UNITY_ROOT / "Resources/Map10/Dialogue"
OUTPUT_DATA = UNITY_ROOT / "Resources/Map10/dialogue-portraits.json"
OUTPUT_MANIFEST = ROOT / "assets/reference/official-map/map10-dialogue-source-manifest.json"
ADVENTURE = ROOT / "assets/official/adventure.json"
STORY_MANIFEST = ROOT / "assets/reference/official-map/story-source-manifest.json"


def sha256(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def json_bytes(value) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8")


def write_generated(path: Path, content: bytes, previous: dict[Path, str]) -> None:
    if path.is_file():
        current = path.read_bytes()
        if current == content:
            return
        expected = previous.get(path.resolve())
        if expected is None or sha256(current) != expected:
            raise ValueError("Refusing to overwrite changed Map10 dialogue asset: " + str(path))
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(content)


def write_meta(path: Path, importer: str, sprite: dict | None = None) -> None:
    relative = path.relative_to(ROOT / "unity").as_posix()
    guid = hashlib.md5(("zjcs-official:" + relative).encode("utf-8")).hexdigest()
    if importer == "DefaultImporter":
        content = (f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n"
                   "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    elif importer == "TextScriptImporter":
        content = (f"fileFormatVersion: 2\nguid: {guid}\nTextScriptImporter:\n"
                   "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    else:
        if sprite is None:
            raise ValueError("Sprite importer metadata needs native Sprite settings")
        pivot = sprite["pivot"]
        content = (f"fileFormatVersion: 2\nguid: {guid}\nTextureImporter:\n"
                   "  internalIDToNameTable: []\n  externalObjects: {}\n  serializedVersion: 13\n"
                   "  mipmaps:\n    mipMapMode: 0\n    enableMipMap: 0\n    sRGBTexture: 1\n"
                   "  isReadable: 0\n  textureSettings:\n    serializedVersion: 2\n"
                   "    filterMode: 1\n    aniso: 1\n    mipBias: 0\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n"
                   "  spriteMode: 1\n  spriteExtrude: 1\n  spriteMeshType: 1\n  alignment: 9\n"
                   f"  spritePivot: {{x: {pivot[0]:.9g}, y: {pivot[1]:.9g}}}\n"
                   f"  spritePixelsToUnits: {sprite['pixelsPerUnit']:.9g}\n"
                   "  spriteGenerateFallbackPhysicsShape: 1\n  alphaUsage: 1\n  alphaIsTransparency: 1\n"
                   "  textureType: 8\n  textureShape: 1\n  maxTextureSize: 2048\n"
                   "  platformSettings:\n  - serializedVersion: 3\n    buildTarget: DefaultTexturePlatform\n"
                   "    maxTextureSize: 2048\n    textureCompression: 0\n    overridden: 0\n"
                   "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    target = Path(str(path) + ".meta")
    if target.exists():
        existing = target.read_text(encoding="utf-8")
        if f"guid: {guid}" not in existing or importer not in existing:
            if importer == "DefaultImporter" and "folderAsset: yes" in existing \
                    and "DefaultImporter:" in existing:
                return
            raise ValueError("Refusing to replace an existing dialogue asset meta file: " + str(target))
    else:
        target.write_text(content, encoding="utf-8", newline="\n")


def verified_previous_outputs() -> dict[Path, str]:
    if not OUTPUT_MANIFEST.is_file():
        return {}
    previous = json.loads(OUTPUT_MANIFEST.read_text(encoding="utf-8"))
    if previous.get("manifest") != VERSION or previous.get("mapClassId") != 10:
        raise ValueError("Existing Map10 dialogue manifest has an unexpected identity")
    if previous.get("inputs", {}).get("adventure", {}).get("sha256") != sha256(ADVENTURE.read_bytes()):
        raise ValueError("Existing Map10 dialogue manifest references another story payload")
    outputs = {}
    for record in previous.get("outputs", []):
        path = ROOT / record["path"]
        if not path.is_file() or sha256(path.read_bytes()) != record["sha256"]:
            raise ValueError("Existing Map10 dialogue output differs from its manifest: " + str(path))
        outputs[path.resolve()] = record["sha256"]
    outputs[OUTPUT_MANIFEST.resolve()] = sha256(OUTPUT_MANIFEST.read_bytes())
    return outputs


def vec(value) -> list[float]:
    if isinstance(value, dict):
        return [float(value["x"]), float(value["y"])]
    return [float(value.x), float(value.y)]


def extract_sprite(apk: Apk, class_id: int, sprite_name: str, output_name: str,
                   previous: dict[Path, str], sprite_object=None, resource_root=None,
                   resource_prefix="Map10") -> tuple[dict, dict]:
    bundle = f"image_ui_npc_npc_individual_{class_id}"
    suffix = f"/npc_portrait_{class_id}{sprite_name}.png"
    matches = [(path, pointer.deref()) for path, pointer in apk.container(bundle)
               if pointer.type.name == "Sprite" and path.endswith(suffix)] if sprite_object is None else [sprite_object]
    if len(matches) != 1:
        raise ValueError(f"Expected one pinned Sprite {bundle}:{suffix}, found {len(matches)}")
    source_path, sprite_obj = matches[0]
    sprite = sprite_obj.read()
    expected_name = f"npc_portrait_{class_id}{sprite_name}"
    if sprite_object is None and sprite.m_Name != expected_name:
        raise ValueError("Map10 dialogue Sprite name differs from its official binding: " + source_path)
    rect_width, rect_height = float(sprite.m_Rect.width), float(sprite.m_Rect.height)
    if rect_width <= 0 or rect_height <= 0 or rect_width % 1 or rect_height % 1:
        raise ValueError("Map10 dialogue Sprite has non-integral native bounds: " + source_path)
    width, height = int(rect_width), int(rect_height)
    image = sprite_obj.read().image.convert("RGBA")
    offset = sprite.m_RD.textureRectOffset
    x, y = round(float(offset.x)), round(float(offset.y))
    if x < 0 or y < 0 or x + image.width > width or y + image.height > height:
        raise ValueError("Map10 dialogue Sprite crop exceeds its native bounds: " + source_path)
    if image.size != (width, height):
        from PIL import Image
        padded = Image.new("RGBA", (width, height), (0, 0, 0, 0))
        padded.alpha_composite(image, (x, y))
        image = padded
    buffer = BytesIO()
    image.save(buffer, format="PNG", optimize=False, compress_level=9)
    output = (resource_root or RESOURCE_ROOT) / (output_name + ".png")
    payload = buffer.getvalue()
    write_generated(output, payload, previous)
    info = {
        "path": output.relative_to(ROOT).as_posix(),
        "resource": resource_prefix + "/Dialogue/" + output_name,
        "sha256": sha256(payload),
        "width": width,
        "height": height,
        "pivot": vec(sprite.m_Pivot),
        "pixelsPerUnit": float(sprite.m_PixelsToUnits),
    }
    source = dict(apk.provenance(sprite_obj), manifest=VERSION, object=source_path,
                  raw_sha256=sha256(sprite_obj.get_raw_data()), output_sha256=sha256(payload))
    write_meta(output, "TextureImporter", info)
    return info, source


def extract_dialogue_icon(apk: Apk, sprite_name: str,
                          previous: dict[Path, str]) -> tuple[dict, dict]:
    matches = [(path, pointer.deref()) for path, pointer in apk.container("image_ui_dialogue")
               if pointer.type.name == "Sprite" and path.endswith("/" + sprite_name + ".png")]
    if len(matches) != 1:
        raise ValueError("Expected one pinned dialogue Sprite " + sprite_name + ", found " + str(len(matches)))
    source_path, sprite_obj = matches[0]
    sprite = sprite_obj.read()
    if sprite.m_Name != sprite_name:
        raise ValueError("Pinned dialogue Sprite name differs from its official binding: " + source_path)
    width, height = int(sprite.m_Rect.width), int(sprite.m_Rect.height)
    if width <= 0 or height <= 0 or sprite.m_Rect.width % 1 or sprite.m_Rect.height % 1:
        raise ValueError("Official dialogue Sprite has non-integral native bounds: " + source_path)
    image = sprite_obj.read().image.convert("RGBA")
    offset = sprite.m_RD.textureRectOffset
    x, y = round(float(offset.x)), round(float(offset.y))
    if x < 0 or y < 0 or x + image.width > width or y + image.height > height:
        raise ValueError("Official dialogue Sprite crop exceeds its native bounds: " + source_path)
    if image.size != (width, height):
        from PIL import Image
        padded = Image.new("RGBA", (width, height), (0, 0, 0, 0))
        padded.alpha_composite(image, (x, y))
        image = padded
    buffer = BytesIO()
    image.save(buffer, format="PNG", optimize=False, compress_level=9)
    output = RESOURCE_ROOT / (sprite_name + ".png")
    payload = buffer.getvalue()
    write_generated(output, payload, previous)
    info = {
        "path": output.relative_to(ROOT).as_posix(),
        "resource": "Map10/Dialogue/" + sprite_name,
        "sha256": sha256(payload),
        "width": width,
        "height": height,
        "pivot": vec(sprite.m_Pivot),
        "pixelsPerUnit": float(sprite.m_PixelsToUnits),
    }
    source = dict(apk.provenance(sprite_obj), manifest=VERSION, object=source_path,
                  raw_sha256=sha256(sprite_obj.get_raw_data()), output_sha256=sha256(payload))
    write_meta(output, "TextureImporter", info)
    return info, source


def extract_portrait(apk: Apk, schemas: ScriptSchemas, class_id: int, face_types: set[str],
                     previous: dict[Path, str], sprite_resolver=None) -> tuple[dict, dict, list[dict]]:
    path = f"Assets/Prefabs/UI/Portrait/Npc_Portrait_{class_id}.prefab"
    root_obj = apk.asset("prefabs_ui_portrait", f"Npc_Portrait_{class_id}.prefab")
    source = dict(apk.provenance(root_obj), manifest=VERSION, object=path,
                  raw_sha256=sha256(root_obj.get_raw_data()))
    root = root_obj.read(check_read=False)
    stack = [(root_obj, "")]
    rects = {}
    portrait_data = None
    portrait_component = None
    while stack:
        game_object, parent_path = stack.pop()
        go = game_object.read(check_read=False)
        object_path = parent_path + "/" + go.m_Name
        transform = None
        components = [entry.component.deref() for entry in go.m_Component]
        for component in components:
            if component.type.name in ("RectTransform", "Transform"):
                transform = component.read(check_read=False)
                rect_name = "Face" if go.m_Name in ("Face", "Brow") else go.m_Name
                if component.type.name == "RectTransform" and rect_name in ("Main", "Face"):
                    rects[rect_name] = {
                        "nodeName": go.m_Name,
                        "position": vec(transform.m_AnchoredPosition),
                        "size": vec(transform.m_SizeDelta),
                        "pivot": vec(transform.m_Pivot),
                    }
            elif component.type.name == "MonoBehaviour":
                basic = component.read(check_read=False)
                if basic.m_Script.read().m_ClassName == "PortraitFace":
                    if portrait_data is not None:
                        raise ValueError("Portrait prefab contains multiple PortraitFace components: " + path)
                    portrait_data = schemas.read(component)
                    portrait_component = component
        if transform is not None:
            for child in reversed(transform.m_Children):
                stack.append((child.deref().read(check_read=False).m_GameObject.deref(), object_path))
    if portrait_data is None or set(rects) != {"Main", "Face"}:
        raise ValueError("Official portrait prefab layout is incomplete: " + path)
    source["portraitFaceComponent"] = dict(apk.provenance(portrait_component), manifest=VERSION,
        raw_sha256=sha256(portrait_component.get_raw_data()))

    expected_main_guid = {8402: "89baf3990fdb9bf44bac21ecb8f28350",
                          8404: "4deff0ebd0638c64287ee74eb2a86481",
                          8417: "e6c72f3fc51e76742b378e67acb2a797"}.get(class_id)
    if sprite_resolver is None and (not expected_main_guid or portrait_data["mPortraitMainProxy"]["mAssetGuid"] != expected_main_guid):
        raise ValueError("Pinned Map10 portrait main proxy GUID differs from the official binding")
    available_faces = {row["Type"]: row for row in portrait_data["mFacesProxy"]}
    if not face_types.issubset(available_faces):
        raise ValueError("Map10 story uses a portrait expression absent from its prefab: " + str(class_id))

    main_name = f"npc_portrait_{class_id}"
    main, main_source = extract_sprite(apk, class_id, "", main_name, previous) if sprite_resolver is None \
        else sprite_resolver(portrait_data["mPortraitMainProxy"]["mAssetGuid"], main_name)
    face_outputs = []
    for face in sorted(face_types):
        proxy_guid = available_faces[face]["FaceSpriteProxy"]["mAssetGuid"]
        if not proxy_guid:
            raise ValueError("Map10 dialogue expression has no official Sprite proxy: " + face)
        name = f"npc_portrait_{class_id}_{face}"
        face_info, face_source = extract_sprite(apk, class_id, "_" + face, name, previous) if sprite_resolver is None \
            else sprite_resolver(proxy_guid, name)
        face_outputs.append({"type": face, **face_info})
        source.setdefault("sprites", {})[name] = face_source
    source.setdefault("sprites", {})[main_name] = main_source
    offsets = {row["Type"]: [float(row["Offset"]["x"]), float(row["Offset"]["y"])]
               for row in portrait_data.get("mFaceOffsets", [])}
    faces = []
    for face in face_outputs:
        faces.append({"type": face["type"], "resource": face["resource"],
                      "position": offsets.get(face["type"], vec(portrait_data["mFaceCommonOffset"])),
                      "nativeSize": [face["width"], face["height"]]})
    layout = {
        "portraitPath": f"UI/Portrait/Npc_Portrait_{class_id}",
        "needFlipOnRight": bool(portrait_data["mNeedFlipOnRight"]),
        "main": {"resource": main["resource"], "nodeName": rects["Main"]["nodeName"],
                 "position": rects["Main"]["position"],
                 "nativeSize": [main["width"], main["height"]], "pivot": rects["Main"]["pivot"]},
        "face": {"nodeName": rects["Face"]["nodeName"], "position": rects["Face"]["position"],
                 "nativeSize": rects["Face"]["size"],
                 "pivot": rects["Face"]["pivot"], "commonOffset": vec(portrait_data["mFaceCommonOffset"]),
                 "expressions": faces},
    }
    return layout, source, [main, *face_outputs]


def extract_speaker_style(apk: Apk, schemas: ScriptSchemas) -> tuple[dict, dict]:
    matches = [(path, pointer.deref()) for path, pointer in apk.container("prefabs_ui_story")
               if pointer.type.name == "GameObject" and path.endswith("/TalkDialog.prefab")]
    if len(matches) != 1:
        raise ValueError("Expected one exact official TalkDialog prefab, found " + str(len(matches)))
    prefab_path, root = matches[0]
    colors = {}
    source = dict(apk.provenance(root), manifest=VERSION, object=prefab_path,
                  raw_sha256=sha256(root.get_raw_data()))
    stack = [(root, "")]
    while stack:
        game_object, parent_path = stack.pop()
        go = game_object.read(check_read=False)
        object_path = parent_path + "/" + go.m_Name
        transform = None
        for entry in go.m_Component:
            component = entry.component.deref()
            if component.type.name in ("RectTransform", "Transform"):
                transform = component.read(check_read=False)
            elif component.type.name == "MonoBehaviour":
                basic = component.read(check_read=False)
                script = basic.m_Script.read()
                if script.m_ClassName != "TalkDialogCharacter":
                    continue
                side = "left" if object_path.endswith("/LeftCharacter") else (
                    "right" if object_path.endswith("/RightCharacter") else "")
                if not side:
                    raise ValueError("TalkDialogCharacter is outside its native speaker slot: " + object_path)
                values = schemas.read(component)
                colors[side] = {
                    "direction": int(values["mTalkPortraitDirectionType"]),
                    "light": [float(values["mCharacterLightColor"][key]) for key in ("r", "g", "b", "a")],
                    "dark": [float(values["mCharacterDarkColor"][key]) for key in ("r", "g", "b", "a")],
                    "colorDuration": float(values["mCharacterChangeColorDuration"]),
                    "moveInAnimation": values["mAniMoveIn"],
                    "moveOutAnimation": values["mAniMoveOut"],
                    "lightAnimation": values["mAniLight"],
                    "darkAnimation": values["mAniDark"],
                    "switchAnimation": values["mAniSwitch"],
                }
                source["speakerComponents"] = source.get("speakerComponents", {})
                source["speakerComponents"][side] = dict(apk.provenance(component), manifest=VERSION,
                    raw_sha256=sha256(component.get_raw_data()))
        if transform is not None:
            for child in reversed(transform.m_Children):
                stack.append((child.deref().read(check_read=False).m_GameObject.deref(), object_path))
    if set(colors) != {"left", "right"}:
        raise ValueError("Official TalkDialog does not contain both speaker slots")
    if colors["left"]["direction"] != 0 or colors["right"]["direction"] != 1:
        raise ValueError("Official TalkDialog speaker directions differ from Left/Right")
    if colors["left"]["light"] != [1.0, 1.0, 1.0, 1.0] \
            or colors["right"]["light"] != [1.0, 1.0, 1.0, 1.0] \
            or colors["left"]["dark"] != colors["right"]["dark"]:
        raise ValueError("Official TalkDialog speaker tint parameters are inconsistent")
    return {"left": colors["left"], "right": colors["right"]}, source


def build() -> dict:
    adventure_bytes = ADVENTURE.read_bytes()
    story_manifest = json.loads(STORY_MANIFEST.read_text(encoding="utf-8"))
    adventure_hash = sha256(adventure_bytes)
    if story_manifest.get("manifest") != VERSION or story_manifest.get("adventure_sha256") != adventure_hash:
        raise ValueError("Pinned Map10 dialogue source differs from the fixed adventure manifest")
    adventure = json.loads(adventure_bytes)
    story_names = {"FirstEnterMap", "FirstBattle", "FirstMeetChaos", "SavingChick",
                   "ChickYell", "ChickYell2", "ChickChat", "FangjianbeiActive", "UnlockFangBei"}
    role_faces: dict[int, set[str]] = {}
    for story_name in story_names:
        graph = adventure.get("stories", {}).get(story_name)
        if not graph or not graph.get("nodes"):
            raise ValueError("Official Map10 story graph is missing: " + story_name)
        for lines in adventure.get("dialogues", {}).get(story_name, {}).values():
            for line in lines:
                role_id = int(line.get("RoleId", 0))
                if role_id > 0 and line.get("RoleFaceType"):
                    role_faces.setdefault(role_id, set()).add(line["RoleFaceType"])
    for row in adventure["tables"].get("Dialog/dialog_node", []):
        if row["DialogId"] in (1, 2, 3) and row.get("RoleId", 0) > 0:
            role_faces.setdefault(int(row["RoleId"]), set())
    if role_faces != {840200: {"angry", "happy", "shock"}, 840401: set(),
                      840402: set(), 841700: {"cry"}}:
        raise ValueError("Map10 dialogue portrait expressions differ from the pinned official stories")

    previous = verified_previous_outputs()
    apk = Apk()
    schemas = ScriptSchemas()
    role_layouts = []
    portrait_sources = {}
    sprite_outputs = []
    class_faces = {8402: role_faces[840200], 8404: role_faces[840401] | role_faces[840402],
                   8417: role_faces[841700]}
    for class_id in sorted(class_faces):
        layout, source, outputs = extract_portrait(apk, schemas, class_id, class_faces[class_id], previous)
        role_layouts.append(layout)
        portrait_sources[str(class_id)] = source
        sprite_outputs.extend(outputs)

    icon_sources = {}
    icons = []
    for sprite_name in ("Iconarrow", "IconExit", "Iconarrow_1"):
        info, source = extract_dialogue_icon(apk, sprite_name, previous)
        icon_sources[sprite_name] = source
        icons.append({"uri": "UI/Dialogue/" + sprite_name, "resource": info["resource"]})
        sprite_outputs.append(info)

    speaker_style, dialogue_ui_source = extract_speaker_style(apk, schemas)
    data = {"manifest": VERSION, "mapClassId": 10, "sourceSha256": adventure_hash,
            "speakerStyle": speaker_style, "portraits": role_layouts, "icons": icons}
    payload = json_bytes(data)
    write_generated(OUTPUT_DATA, payload, previous)
    write_meta(OUTPUT_DATA, "TextScriptImporter")
    output_records = [{"path": item["path"], "sha256": item["sha256"],
                       "width": item["width"], "height": item["height"]}
                      for item in sprite_outputs]
    output_records.append({"path": OUTPUT_DATA.relative_to(ROOT).as_posix(), "sha256": sha256(payload)})
    manifest = {
        "manifest": VERSION,
        "mapClassId": 10,
        "inputs": {
            "adventure": {"path": ADVENTURE.relative_to(ROOT).as_posix(), "sha256": adventure_hash},
            "storySourceManifest": {"path": STORY_MANIFEST.relative_to(ROOT).as_posix(),
                                    "sha256": sha256(STORY_MANIFEST.read_bytes())},
            "portraits": portrait_sources,
            "dialogueIcons": icon_sources,
            "talkDialogPrefab": dialogue_ui_source,
        },
        "outputs": output_records,
        "layoutSha256": sha256(payload),
    }
    write_generated(OUTPUT_MANIFEST, json_bytes(manifest), previous)
    write_meta(RESOURCE_ROOT, "DefaultImporter")
    write_meta(RESOURCE_ROOT.parent, "DefaultImporter")
    print("Map10 dialogue assets: 3 official portrait prefabs, native dialogue icons and fixed bounds.")
    return manifest


if __name__ == "__main__":
    build()
