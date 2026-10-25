"""Extract the pinned opening story-fight data and story UI for the offline flow.

Inputs come from the fixed APK (854_156383) and the already converted official
story catalog. Outputs:
  assets/official/character-creation/opening-story.json
  assets/official/character-creation/opening-stage.json
  unity/Assets/StreamingAssets/Opening/*.json|*.bytes
  unity/Assets/StreamingAssets/Opening/ui/*.png + opening-ui.json
  assets/reference/character-creation/opening-source-manifest.json
"""
from __future__ import annotations

import gzip
import hashlib
import io
import json
import math
import re
import struct
import sys
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))

VERSION = "854_156383"
STORY_PATH = ROOT / "assets/official/adventure.json"
STORY_MANIFEST = ROOT / "assets/reference/official-map/story-source-manifest.json"
BATTLE_STAGES = ROOT / "assets/official/battle-stages.json"
MODEL_IDENTITIES = ROOT / "unity/Assets/Official/Combat/Presentation/model-identities.json"
OFFICIAL_DATA = ROOT / "assets/official/character-creation"
PAYLOAD = ROOT / "unity/Assets/StreamingAssets/Opening"
REFERENCE = ROOT / "assets/reference/character-creation/opening-source-manifest.json"

STORIES = ("BeforeFight", "AfterFight", "FirstStory")
RECORD_BUNDLE = "defaultpackage_assets_config_story.bundle"
RECORD_PATH = "Assets/Config/Story/CreatePlayerFightRecord.bytes"
STORY_BUNDLE = "prefabs_ui_story"
GLOBAL_BUNDLE = "prefabs_ui_global"
FIGHT_HUD_BUNDLE = "prefabs_ui_fight_fightmainpanel_extmodel_fightmainpanelrecordfight"
UI_PANELS = {
    "talk_dialog": ("Assets/Prefabs/UI/Story/OptionTalkDialog.prefab", STORY_BUNDLE),
    "story_mask": ("Assets/Prefabs/UI/Story/StoryMask.prefab", STORY_BUNDLE),
    "story_mask_graphic": ("Assets/Prefabs/UI/Story/Mask.prefab", STORY_BUNDLE),
    "exit_fight_transition": ("Assets/Prefabs/UI/Story/ExitFightTransitionPanel.prefab", STORY_BUNDLE),
    "enter_game_transition": ("Assets/Prefabs/UI/Story/EnterGameStoryTransitionPanel.prefab", STORY_BUNDLE),
    "first_story": ("Assets/Prefabs/UI/Global/FirstStoryPanel.prefab", GLOBAL_BUNDLE),
}
FIGHT_HUD_PREFAB = "FightMainPanelRecordFight"


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def json_bytes(value) -> bytes:
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")


def write_with_meta(path: Path, data: bytes) -> dict:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)
    meta = path.with_name(path.name + ".meta")
    if not meta.exists():
        guid = uuid.uuid5(uuid.NAMESPACE_URL, "zjcs-opening:" + path.as_posix()).hex
        meta.write_text(
            "fileFormatVersion: 2\nguid: %s\nDefaultImporter:\n  externalObjects: {}\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid,
            encoding="utf-8")
    return {"path": path.relative_to(ROOT).as_posix(), "bytes": len(data), "sha256": sha256(data)}


# ---------------------------------------------------------------- story graphs
def story_payload(apk, manifest):
    raw = STORY_PATH.read_bytes()
    digest = sha256(raw)
    if digest != manifest["adventure_sha256"]:
        raise ValueError("Converted story catalog does not match its pinned manifest")
    catalog = json.loads(raw.decode("utf-8"))
    graphs = {}
    for name in STORIES:
        if name not in catalog["stories"]:
            raise ValueError("Official story graph is missing: " + name)
        graph = catalog["stories"][name]
        nodes = []
        for node_id, node in graph["nodes"].items():
            ports = node.get("ports") or {}
            entry = []
            exit_ = []
            for port_name, port in ports.items():
                targets = [int(c["node"]) for c in port.get("connections") or []]
                if port_name == "Entry":
                    entry.extend(int(c["node"]) for c in port.get("connections") or [])
                elif port_name == "Exit":
                    exit_.extend(int(c["node"]) for c in port.get("connections") or [])
            item = {
                "id": int(node_id),
                "type": node["_type"],
                "name": node.get("m_Name", ""),
                "enter": sorted(set(entry)),
                "exit": sorted(set(exit_)),
            }
            for key, target in (("Seconds", "seconds"), ("NeedMask", "need_mask"),
                                ("DialogId", "dialog_id"), ("AutoCloseDialog", "auto_close"),
                                ("Immediately", "immediately"), ("IsHideOtherUI", "hide_other_ui"),
                                ("IsNeedMoveCamera", "move_camera"), ("mIsKeepBlackScreen", "keep_black"),
                                ("Instant", "instant"), ("LifeType", "life_type"),
                                ("IsWaitPlayableStop", "wait_playable"), ("PlayableDura", "playable_dura"),
                                ("BehaviourType", "behaviour"), ("HideMainUI", "hide_main_ui"),
                                ("InStoryMode", "in_story_mode")):
                if key in node:
                    item[target] = node[key]
            if "DialogItems" in node and node["DialogItems"]:
                item["dialog_items"] = [int(entry["Order"]) for entry in node["DialogItems"]]
            auto = node.get("AutoPlayConfig")
            if isinstance(auto, dict):
                item["auto_play"] = bool(auto.get("IsAutoPlay"))
                item["auto_wait"] = float(auto.get("AutoPlayWaitTime", 0.0))
                item["auto_dura"] = float(auto.get("AutoPlayDura", 0.0))
            window = node.get("WindowPrefab")
            if isinstance(window, dict) and window.get("mWindowPath"):
                item["window_path"] = window["mWindowPath"]
            playables = node.get("PlayablesProxy") or []
            if playables:
                item["playables"] = [entry.get("mAssetGuid", "") for entry in playables if entry.get("mAssetGuid")]
            nodes.append(item)
        nodes.sort(key=lambda entry: entry["id"])
        graphs[name] = {"nodes": nodes, "order": None}

    story_table = {row["StoryName"]: row for row in catalog["tables"].get("story", [])
                   if row.get("StoryName") in STORIES}
    for name, graph in graphs.items():
        row = story_table.get(name)
        if row is None:
            raise ValueError("Official story table row is missing: " + name)
        graph["order"] = int(row.get("Order", 0))
        graph["show_skip_button"] = bool(row.get("IsShowSkipBtn"))
        graph["res_path"] = row.get("ResPath")

    dialogues = []
    for name in STORIES:
        rows = catalog["dialogues"].get(name) or {}
        for dialog_id, lines in rows.items():
            for line in lines:
                dialogues.append({
                    "story": name,
                    "id": int(dialog_id),
                    "order": int(line["Order"]),
                    "text": line.get("Text") or "",
                    "dialog_type": line.get("DialogType") or "",
                    "voice": line.get("VoiceEventKey") or "",
                    "role_id": int(line.get("RoleId") or 0),
                    "role_face": line.get("RoleFaceType") or "",
                })
    dialogues.sort(key=lambda entry: (entry["story"], entry["id"], entry["order"]))
    if not dialogues:
        raise ValueError("Official opening dialogues are missing")

    roles = {}
    role_rows = catalog["tables"].get("Dialog/dialog_role", [])
    for line in dialogues:
        role_id = line["role_id"]
        if not role_id or role_id in roles:
            continue
        row = next((entry for entry in role_rows if entry.get("RoleId") == role_id), None)
        if row is None:
            raise ValueError("Official dialog role is missing: " + str(role_id))
        roles[str(role_id)] = {"name": row.get("RoleName") or "", "portrait": row.get("PortraitPath") or ""}

    provenance = manifest["graphs"]
    return {
        "manifest": VERSION,
        "source_sha256": digest,
        "graphs": graphs,
        "dialogues": dialogues,
        "roles": roles,
        "provenance": {name: provenance[name] for name in STORIES},
    }


# -------------------------------------------------------------------- record
def record_payload(apk):
    obj = apk.asset(RECORD_BUNDLE, "/Story/CreatePlayerFightRecord.bytes")
    raw = obj.read().m_Script.encode("utf-8", "surrogateescape")
    if raw[:2] != b"\x1f\x8b":
        raise ValueError("Official opening record is not GZip compressed")
    decoded = gzip.decompress(raw)
    record = json.loads(decoded.decode("utf-8"))
    if record.get("StageClassId") is None or not record.get("GameStartMsgs") or not record.get("RoundMsgList"):
        raise ValueError("Official opening record is incomplete")
    steps = sum(len(round_["StepList"]) for round_ in record["RoundMsgList"])
    entity_list = next((msg for msg in record["GameStartMsgs"] if msg["Item1"] == "GAME_ENTITY_LIST"), None)
    if entity_list is None:
        raise ValueError("Official opening record has no entity list")
    return {
        "bytes": raw,
        "decoded_sha256": sha256(decoded),
        "source": apk.provenance(obj),
        "summary": {
            "game_id": record["GameId"],
            "stage_class_id": record["StageClassId"],
            "rounds": len(record["RoundMsgList"]),
            "steps": steps,
            "entities": len(entity_list["Item2"]["EntityList"]),
            "has_result": record.get("GameResultMsg") is not None,
        },
    }


# --------------------------------------------------------------------- stage
def stage_payload(apk):
    from official_tables import Constructors
    c = Constructors()
    stage_obj = apk.asset("config_luban", "/stage.g.bytes")
    rows = c.table(stage_obj.read().m_Script.encode("utf-8", "surrogateescape"), "TbStageInfo")
    stage = next((row for row in rows if row.get("Id") == 20), None)
    if stage is None:
        raise ValueError("Official stage 20 is missing")
    battle = json.loads(BATTLE_STAGES.read_text(encoding="utf-8"))
    game_key = str(stage["GameId"])
    grid = (battle["stages"].get(game_key) or {}).get("FightGridGroundComponent")
    if not grid:
        raise ValueError("Official grid ground for game " + game_key + " is missing")
    cells = []
    for info in grid.get("PosInfos") or []:
        cells.append([int(info["Pos"]["X"]), int(info["Pos"]["Y"]),
                      int(info.get("GroundType", 0)), int(info.get("Height", 0))])
    sizes = grid["Size"]
    origin = grid.get("OriginPos") or [0.0, 0.0, 0.0]
    monsters = [int(value) for value in (stage.get("WaveMonsterIds") or [[]])[0]]
    identities = json.loads(MODEL_IDENTITIES.read_text(encoding="utf-8"))
    model_keys = {row["monster_id"]: row["key"] for row in identities["monster_models"]}
    models = {str(monster): model_keys.get(monster, "") for monster in monsters}
    return {
        "manifest": VERSION,
        "stage_id": int(stage["Id"]),
        "type": int(stage.get("Type", 0)),
        "game_id": int(stage["GameId"]),
        "scene": "game_" + game_key,
        "grid": {
            "origin": [float(origin[0]), float(origin[1])],
            "size": [int(sizes["X"]), int(sizes["Y"])],
            "cells": cells,
        },
        "monsters": monsters,
        "models": models,
        "source": apk.provenance(stage_obj),
    }


# ------------------------------------------------------------------------ UI
class SpriteCache:
    def __init__(self, apk):
        self.apk = apk
        self.images = {}
        self.failures = {}
        self.sources = {}
        self.texture_paths = {}
        self.texture_outputs = {}

    def record_source(self, obj, kind, object_path=""):
        key = obj.assets_file.name + ":" + str(obj.path_id)
        if key not in self.sources:
            record = self.apk.provenance(obj)
            record.update(kind=kind, raw_sha256=sha256(obj.get_raw_data()))
            if object_path:
                record["object"] = object_path
            self.sources[key] = record

    def sprite_image(self, assets_file, file_id, path_id):
        key = (file_id, path_id)
        if key in self.images:
            return self.images[key]
        sprite = self.lookup(assets_file, file_id, path_id)
        if sprite is None or sprite.type.name != "Sprite":
            self.failures[str(key)] = "not a sprite object"
            self.images[key] = None
            return None
        self.record_source(sprite, "Sprite")
        data = sprite.read()
        rd = data.m_RD
        if data.m_SpriteAtlas is not None and data.m_SpriteAtlas.path_id:
            atlas_object = data.m_SpriteAtlas.deref()
            self.record_source(atlas_object, "SpriteAtlas")
            atlas = atlas_object.read()
            render_data = dict(atlas.m_RenderDataMap).get(data.m_RenderDataKey)
            if render_data is None:
                self.failures[str(key)] = "sprite atlas has no matching render data"
                self.images[key] = None
                return None
            rd = render_data
        if rd.texture.path_id == 0:
            self.failures[str(key)] = "sprite has no texture"
            self.images[key] = None
            return None
        texture_object = rd.texture.deref()
        self.record_source(texture_object, "Texture2D")
        texture = texture_object.read()
        image = texture.image
        rect = rd.textureRect
        left = int(rect.x)
        top = int(rect.y)
        width = int(rect.width)
        height = int(rect.height)
        if width <= 0 or height <= 0:
            self.failures[str(key)] = "sprite has no area"
            self.images[key] = None
            return None
        # Unity texture space is bottom-up; m_Y is measured from the bottom.
        top = image.height - top - height
        cropped = image.crop((left, top, left + width, top + height)).convert("RGBA")
        settings = rd.settingsRaw
        if getattr(settings, "flipHorizontal", False) or getattr(settings, "flipVertical", False):
            cropped = cropped.transpose(0)
        result = {
            "name": data.m_Name,
            "key": "%d:%d:%s" % (file_id, path_id, data.m_Name),
            "image": cropped,
            "pivot": [float(data.m_Pivot.x), float(data.m_Pivot.y)],
            "border": [float(data.m_Border.x), float(data.m_Border.y),
                       float(data.m_Border.z), float(data.m_Border.w)],
            "ppu": float(data.m_PixelsToUnits or 100.0),
            "rect": [width, height],
        }
        self.images[key] = result
        return result

    def texture_file(self, pointer, assets_file, target_dir, object_path):
        if not pointer.path_id:
            return None
        texture = self.lookup(assets_file, pointer.file_id, pointer.path_id)
        if texture is None or texture.type.name != "Texture2D":
            self.failures[str((pointer.file_id, pointer.path_id))] = "material texture is unavailable"
            return None
        key = texture.assets_file.name + ":" + str(texture.path_id)
        if key in self.texture_paths:
            return self.texture_paths[key]

        self.record_source(texture, "Texture2D", object_path)
        from PIL import Image

        texture_data = texture.read()
        settings = texture_data.m_TextureSettings
        image = texture_data.image.convert("RGBA")
        safe_name = re.sub(r"[^A-Za-z0-9._-]+", "_", texture_data.m_Name).strip("._") or "texture"
        filename = safe_name + "-" + hashlib.sha256(key.encode("utf-8")).hexdigest()[:12] + ".png"
        relative = "textures/" + filename
        target = target_dir / filename
        buffer = io.BytesIO()
        image.save(buffer, format="PNG", optimize=False)
        output = write_with_meta(target, buffer.getvalue())
        texture_record = {
            "file": relative,
            "filter_mode": int(settings.m_FilterMode),
            "aniso_level": int(settings.m_Aniso),
            "wrap_u": int(settings.m_WrapU),
            "wrap_v": int(settings.m_WrapV),
            "wrap_w": int(settings.m_WrapW),
        }
        self.texture_paths[key] = texture_record
        self.texture_outputs[relative] = output
        return texture_record

    def lookup(self, assets_file, file_id, path_id):
        from UnityPy.classes.PPtr import PPtr

        try:
            return PPtr(m_FileID=file_id, m_PathID=path_id, assetsfile=assets_file).deref()
        except Exception as error:  # dependency absent
            self.failures[str((file_id, path_id))] = type(error).__name__ + ": " + str(error)
            return None


def decode_image(fields):
    color = fields["m_Color"]
    pointer = fields["m_Sprite"]
    return {
        "color": [round(float(color[channel]), 4) for channel in ("r", "g", "b", "a")],
        "sprite": [int(pointer["m_FileID"]), int(pointer["m_PathID"])],
        "type": int(fields["m_Type"]),
        "preserve": bool(fields["m_PreserveAspect"]),
    }


TMP_ALIGNMENT_CONVERTED = 65535
TMP_HORIZONTAL = {1, 2, 4, 8, 16, 32}
TMP_VERTICAL = {256, 512, 1024, 2048, 4096, 8192, 16384}


def tmp_text_base(raw, name):
    """Return the official TMP text plus the offset where its fixed field block starts.

    The pinned bundles carry no TMP type tree, so the layout is read from the
    known TMP 3.x serialization order. The text string is length-prefixed and
    four-byte aligned, then two material arrays precede the fixed block whose
    absolute position therefore depends only on the string and array counts.
    """
    if len(raw) < 92:
        raise ValueError("TMP component is too small: " + name)
    length = struct.unpack_from("<i", raw, 88)[0]
    if length < 0 or 92 + length > len(raw):
        raise ValueError("Unexpected TMP text string: " + name)
    text = raw[92:92 + length].decode("utf-8")
    aligned = (92 + length + 3) & ~3
    shared = struct.unpack_from("<i", raw, aligned + 28)[0]
    if shared < 0 or aligned + 44 + 12 * shared > len(raw):
        raise ValueError("Unexpected TMP shared material count: " + name)
    materials = struct.unpack_from("<i", raw, aligned + 44 + 12 * shared)[0]
    if materials < 0:
        raise ValueError("Unexpected TMP material count: " + name)
    base = aligned + 12 * (shared + materials)
    if base + 368 > len(raw):
        raise ValueError("TMP component has no official layout block: " + name)
    return text, base


def decode_text(raw, name):
    """Decode the official TMP_Text serialized fields of one prefab text node."""
    text, base = tmp_text_base(raw, name)
    font_size = struct.unpack_from("<f", raw, base + 192)[0]
    font_weight = struct.unpack_from("<i", raw, base + 200)[0]
    auto_sizing = raw[base + 204] != 0
    font_size_min, font_size_max = struct.unpack_from("<2f", raw, base + 208)
    font_style = struct.unpack_from("<i", raw, base + 216)[0]
    horizontal, vertical, alignment = struct.unpack_from("<3i", raw, base + 220)
    word_wrap = raw[base + 256] != 0
    overflow = struct.unpack_from("<i", raw, base + 264)[0]
    margin = list(struct.unpack_from("<4f", raw, base + 352))
    color = [round(float(value), 6) for value in struct.unpack_from("<4f", raw, base + 52)]
    if not 0.0 < font_size <= 400.0:
        raise ValueError("Unexpected TMP font size %s: %s" % (font_size, name))
    if not all(math.isfinite(value) and abs(value) <= 1000.0 for value in margin):
        raise ValueError("Unexpected TMP margin: " + name)
    if overflow not in range(7):
        raise ValueError("Unexpected TMP overflow mode: " + name)
    if not all(0.0 <= value <= 1.0 for value in color):
        raise ValueError("Unexpected TMP font color: " + name)
    if alignment != TMP_ALIGNMENT_CONVERTED:
        if alignment not in {h | v for h in TMP_HORIZONTAL for v in TMP_VERTICAL}:
            raise ValueError("Unexpected TMP alignment: " + name)
    else:
        if horizontal not in TMP_HORIZONTAL or vertical not in TMP_VERTICAL:
            raise ValueError("Unexpected TMP axis alignment: " + name)
        alignment = horizontal | vertical
    return {
        "text": text,
        "font_size": float(font_size),
        "font_weight": int(font_weight),
        "font_style": int(font_style),
        "auto_sizing": bool(auto_sizing),
        "font_size_range": [float(font_size_min), float(font_size_max)],
        "alignment": int(alignment),
        "word_wrap": bool(word_wrap),
        "overflow": int(overflow),
        "margin": [float(value) for value in margin],
        "color": color,
    }


def prefab_walk(go):
    rect = None
    transform = None
    for component in go.m_Component:
        kind = component.component.type.name
        if kind in ("RectTransform", "Transform"):
            transform = component.component.read()
            if kind == "RectTransform":
                rect = transform
    yield go, rect
    if transform is None:
        return
    for child in transform.m_Children:
        for entry in prefab_walk(child.read().m_GameObject.read()):
            yield entry


def decode_layout_component(cls, raw, node):
    """Read the fixed APK's UGUI fields, including its JSZX spacing-list extension."""
    if cls == "ContentSizeFitter":
        if len(raw) != 40:
            raise ValueError("Unexpected official ContentSizeFitter: " + node["path"])
        node["size_fitter"] = list(struct.unpack_from("<2i", raw, 32))
    elif cls == "LayoutElement":
        if len(raw) != 64:
            raise ValueError("Unexpected official LayoutElement: " + node["path"])
        values = struct.unpack_from("<i6fi", raw, 32)
        node["layout_element"] = dict(ignore=bool(values[0]), minimum=list(values[1:3]),
            preferred=list(values[3:5]), flexible=list(values[5:7]), priority=values[7])
    elif cls == "HorizontalLayoutGroupJSZX":
        left, right, top, bottom, alignment = struct.unpack_from("<5i", raw, 32)
        spacing, count = struct.unpack_from("<fi", raw, 52)
        offset = 60 + 4 * count
        if count < 0 or offset + 28 != len(raw):
            raise ValueError("Unexpected official JSZX layout: " + node["path"])
        node["horizontal_layout"] = dict(padding=[left, right, top, bottom], alignment=alignment,
            spacing=spacing, spacing_list=list(struct.unpack_from("<" + "f" * count, raw, 60)),
            flags=list(struct.unpack_from("<7i", raw, offset)))
    else:
        return False
    return True


def convert_prefab(apk, sprites, schemas, path, bundle, target_dir):
    pointer = next((ptr for entry, val in apk.container(bundle) if entry == path
                    for ptr in [val]), None)
    if pointer is None:
        pointer = next(ptr for entry, ptr in apk.container(bundle) if entry.endswith("/" + path.rsplit("/", 1)[-1]))
    sprites.record_source(pointer.deref(), "GameObjectPrefab", path)
    root_go = pointer.read()
    assets_file = pointer.assetsfile

    def animation_tree(go, parent_path=""):
        node_path = parent_path + "/" + go.m_Name
        transform = next((component.component.read() for component in go.m_Component
                          if component.component.type.name in ("RectTransform", "Transform")), None)
        return {
            "name": go.m_Name,
            "path": node_path,
            "children": [] if transform is None else [
                animation_tree(child.read().m_GameObject.read(), node_path)
                for child in transform.m_Children
            ],
        }

    clip_tree = animation_tree(root_go)
    material_float_names = set()
    material_vector_names = set()
    for candidate, _ in prefab_walk(root_go):
        for component in candidate.m_Component:
            if component.component.type.name != "MonoBehaviour":
                continue
            reader = component.component.deref()
            basic = reader.read(check_read=False)
            script = basic.m_Script.read()
            if script.m_ClassName != "UIMesh":
                continue
            fields = schemas.read(reader)
            material_pointer = fields["m_Material"]
            material_object = sprites.lookup(assets_file, material_pointer["m_FileID"],
                                             material_pointer["m_PathID"])
            if material_object is None or material_object.type.name != "Material":
                continue
            saved = material_object.read().m_SavedProperties
            material_float_names.update(name for name, _ in saved.m_Floats)
            material_vector_names.update(name for name, _ in saved.m_Colors)

    from official_animation import mecanim_tracks

    def build(go, parent_path):
        node_path = parent_path + "/" + go.m_Name
        node = {
            "name": go.m_Name,
            "path": node_path,
            "active": bool(go.m_IsActive),
            "children": [],
            "button": False,
            "mask": False,
            "has_image": False,
            "has_text": False,
            "has_mesh": False,
            "ui_particle": False,
            "is_rect_transform": False,
            "alpha": 1.0,
        }
        rect = None
        transform = None
        for component in go.m_Component:
            kind = component.component.type.name
            if kind in ("RectTransform", "Transform"):
                transform = component.component.read()
                if kind == "RectTransform":
                    rect = transform
                continue
            if kind == "ParticleSystem":
                particle = component.component.deref()
                node["particle_system"] = particle.read_typetree()
                sprites.record_source(particle, "ParticleSystem", node_path)
                continue
            if kind == "ParticleSystemRenderer":
                renderer = component.component.deref()
                renderer_data = renderer.read_typetree()
                node["particle_renderer"] = {
                    key: renderer_data[key] for key in (
                        "m_Enabled", "m_RenderMode", "m_SortingLayerID", "m_SortingOrder",
                        "m_SortMode", "m_Materials", "m_MinParticleSize", "m_MaxParticleSize",
                        "m_LengthScale", "m_NormalDirection", "m_Pivot", "m_Flip",
                        "m_MaskInteraction",
                    ) if key in renderer_data
                }
                sprites.record_source(renderer, "ParticleSystemRenderer", node_path)
                material_pointers = renderer_data.get("m_Materials") or []
                for material_pointer in material_pointers:
                    if not material_pointer.get("m_PathID"):
                        continue
                    material_object = sprites.lookup(
                        renderer.assets_file, material_pointer.get("m_FileID", 0),
                        material_pointer["m_PathID"])
                    if material_object is None or material_object.type.name != "Material":
                        raise ValueError("official particle renderer has no source Material")
                    sprites.record_source(material_object, "Material", node_path)
                    material = material_object.read()
                    saved = material.m_SavedProperties
                    textures = {}
                    for property_name, environment in saved.m_TexEnvs:
                        texture_file = sprites.texture_file(
                            environment.m_Texture, material_object.assets_file,
                            target_dir / "textures", node_path)
                        if texture_file is not None:
                            textures[property_name] = {
                                **texture_file,
                                "scale": [float(environment.m_Scale.x), float(environment.m_Scale.y)],
                                "offset": [float(environment.m_Offset.x), float(environment.m_Offset.y)],
                            }
                    node["particle_material"] = {
                        "name": material.m_Name,
                        "shader_path_id": str(material.m_Shader.path_id),
                        "render_queue": int(material.m_CustomRenderQueue),
                        "floats": {key: float(value) for key, value in saved.m_Floats},
                        "vectors": {key: [float(value.r), float(value.g), float(value.b), float(value.a)]
                                    for key, value in saved.m_Colors},
                        "textures": textures,
                    }
                continue
            if kind == "CanvasGroup":
                group = component.component.read()
                node["canvas_group"] = True
                node["alpha"] = float(group.m_Alpha)
                node["canvas_group_interactable"] = bool(group.m_Interactable)
                node["canvas_group_blocks_raycasts"] = bool(group.m_BlocksRaycasts)
                node["canvas_group_ignore_parent_groups"] = bool(group.m_IgnoreParentGroups)
                continue
            if kind != "MonoBehaviour":
                continue
            reader = component.component.deref()
            raw = reader.get_raw_data()
            try:
                script = reader.read(check_read=False).m_Script.read()
                cls = script.m_ClassName
            except Exception:
                continue
            try:
                if decode_layout_component(cls, raw, node):
                    sprites.record_source(reader, cls, node_path)
                elif cls == "Image":
                    fields = schemas.read(reader)
                    payload = decode_image(fields)
                    node["has_image"] = True
                    node["image_color"] = payload["color"]
                    node["image_type"] = payload["type"]
                    node["image_preserve_aspect"] = payload["preserve"]
                    file_id, path_id = payload["sprite"]
                    if path_id:
                        sprite = sprites.sprite_image(assets_file, file_id, path_id)
                        if sprite is not None:
                            node["sprite"] = sprite["key"]
                elif cls in ("TextMeshProUGUI", "TextMeshPro"):
                    node["has_text"] = True
                    decoded = decode_text(raw, node_path)
                    node["text_value"] = decoded["text"]
                    node["text_font_size"] = decoded["font_size"]
                    node["text_font_weight"] = decoded["font_weight"]
                    node["text_font_style"] = decoded["font_style"]
                    node["text_color"] = decoded["color"]
                    node["text_alignment"] = decoded["alignment"]
                    node["text_margin"] = decoded["margin"]
                    node["text_word_wrap"] = decoded["word_wrap"]
                    node["text_overflow"] = decoded["overflow"]
                    node["text_auto_sizing"] = decoded["auto_sizing"]
                    node["text_font_size_range"] = decoded["font_size_range"]
                elif cls in ("Button", "UIButton"):
                    node["button"] = True
                elif cls in ("Mask", "RectMask2D"):
                    node["mask"] = True
                elif cls == "CanvasGroup":
                    node["canvas_group"] = True
                elif cls == "PopupWindow":
                    fields = schemas.read(reader)
                    window = {"show_clip": "", "hide_clip": "", "loop_clip": "", "clips": {}}
                    for field, target in (("mShowClip", "show_clip"), ("mHideClip", "hide_clip"),
                                          ("mLoopClip", "loop_clip")):
                        reference = fields.get(field) or {}
                        path_id = int(reference.get("m_PathID", 0))
                        if not path_id:
                            continue
                        animation = sprites.lookup(assets_file, int(reference.get("m_FileID", 0)), path_id)
                        if animation is None or animation.type.name != "AnimationClip":
                            node.setdefault("errors", []).append(cls + ": " + field + " is not an AnimationClip")
                            continue
                        clip = animation.read()
                        tracks, duration, loop, unknown = mecanim_tracks(
                            animation, clip_tree, material_float_names, material_vector_names)
                        sprites.record_source(animation, "AnimationClip", node_path)
                        name = clip.m_Name
                        window[target] = name
                        window["clips"][name] = {
                            "duration": float(duration),
                            "loop": bool(loop),
                            "tracks": tracks,
                            "unconverted_bindings": unknown,
                        }
                    node["window"] = window
                elif cls == "UIMesh":
                    from UnityPy.helpers.MeshHelper import MeshHandler

                    payload = schemas.read(reader)
                    mesh_pointer = payload["Mesh"]
                    mesh_object = sprites.lookup(assets_file, mesh_pointer["m_FileID"], mesh_pointer["m_PathID"])
                    if mesh_object is None or mesh_object.type.name != "Mesh":
                        raise ValueError("official UIMesh has no source Mesh")
                    sprites.record_source(mesh_object, "Mesh", node_path)
                    mesh = MeshHandler(mesh_object.read())
                    mesh.process()
                    triangles = [int(index) for submesh in mesh.get_triangles()
                                 for triangle in submesh for index in triangle]
                    colors = getattr(mesh, "m_Colors", None) or []
                    node["has_mesh"] = True
                    node["mesh"] = {
                        "vertices": [[float(value) for value in vertex[:3]] for vertex in mesh.m_Vertices],
                        "uv": [[float(value) for value in uv[:2]] for uv in (mesh.m_UV0 or [])],
                        "colors": [[float(value) for value in color[:4]] for color in colors],
                        "triangles": triangles,
                    }
                    node["mesh_color"] = [float(payload["m_Color"][channel])
                                           for channel in ("r", "g", "b", "a")]
                    node["mesh_raycast_target"] = bool(payload.get("m_RaycastTarget", 0))
                    node["mesh_maskable"] = bool(payload.get("m_Maskable", 1))
                    material_pointer = payload["m_Material"]
                    material_object = sprites.lookup(assets_file, material_pointer["m_FileID"],
                                                     material_pointer["m_PathID"])
                    if material_object is None or material_object.type.name != "Material":
                        raise ValueError("official UIMesh has no source Material")
                    sprites.record_source(material_object, "Material", node_path)
                    material = material_object.read()
                    saved = material.m_SavedProperties
                    textures = {}
                    for property_name, environment in saved.m_TexEnvs:
                        pointer = environment.m_Texture
                        texture_file = sprites.texture_file(
                            pointer, material_object.assets_file, target_dir / "textures", node_path)
                        if texture_file is not None:
                            textures[property_name] = {
                                **texture_file,
                                "scale": [float(environment.m_Scale.x), float(environment.m_Scale.y)],
                                "offset": [float(environment.m_Offset.x), float(environment.m_Offset.y)],
                            }
                    node["mesh_material"] = {
                        "name": material.m_Name,
                        "shader_path_id": str(material.m_Shader.path_id),
                        "render_queue": int(material.m_CustomRenderQueue),
                        "floats": {key: float(value) for key, value in saved.m_Floats},
                        "vectors": {key: [float(value.r), float(value.g), float(value.b), float(value.a)]
                                    for key, value in saved.m_Colors},
                        "textures": textures,
                    }
                elif cls == "UIParticle":
                    node["ui_particle"] = True
                    sprites.record_source(reader, "UIParticle", node_path)
            except Exception as error:
                node.setdefault("errors", []).append(cls + ": " + type(error).__name__ + ": " + str(error))
        if transform is not None:
            position = transform.m_LocalPosition
            rotation = transform.m_LocalRotation
            scale = transform.m_LocalScale
            node["local_position"] = [float(position.x), float(position.y), float(position.z)]
            node["local_rotation"] = [float(rotation.x), float(rotation.y), float(rotation.z), float(rotation.w)]
            node["local_scale"] = [float(scale.x), float(scale.y), float(scale.z)]
        if rect is not None:
            node["is_rect_transform"] = True
            node["anchor_min"] = [float(rect.m_AnchorMin.x), float(rect.m_AnchorMin.y)]
            node["anchor_max"] = [float(rect.m_AnchorMax.x), float(rect.m_AnchorMax.y)]
            node["anchored_position"] = [float(rect.m_AnchoredPosition.x), float(rect.m_AnchoredPosition.y)]
            node["size_delta"] = [float(rect.m_SizeDelta.x), float(rect.m_SizeDelta.y)]
            node["pivot"] = [float(rect.m_Pivot.x), float(rect.m_Pivot.y)]
            node["scale"] = [float(rect.m_LocalScale.x), float(rect.m_LocalScale.y)]
            for child in rect.m_Children:
                node["children"].append(build(child.read().m_GameObject.read(), node_path))
        elif transform is not None:
            for child in transform.m_Children:
                node["children"].append(build(child.read().m_GameObject.read(), node_path))
        return node

    return build(root_go, "")


def find_sprite(apk, sprites, class_id, sprite_name):
    bundle = "image_ui_npc_npc_individual_" + str(class_id)
    suffix = "/" + sprite_name + ".png"
    matches = [(path, pointer) for path, pointer in apk.container(bundle)
               if pointer.type.name == "Sprite" and path.endswith(suffix)]
    if len(matches) != 1:
        raise ValueError("Expected one official portrait Sprite " + bundle + suffix
                         + ", found " + str(len(matches)))
    path, pointer = matches[0]
    sprite = sprites.sprite_image(pointer.assetsfile, pointer.m_FileID, pointer.m_PathID)
    if sprite is None:
        raise ValueError("Official portrait Sprite could not be decoded: " + path)
    return sprite


def set_prefab_image_sprite(root, node_name, sprite_key):
    pending = [root]
    matches = []
    while pending:
        node = pending.pop()
        if node["name"] == node_name and node["has_image"]:
            matches.append(node)
        pending.extend(node["children"])
    if len(matches) != 1:
        raise ValueError("Official portrait prefab has no unique Image node " + node_name)
    matches[0]["sprite"] = sprite_key


def portrait_prefabs(apk, sprites, schemas, story, prefabs, target_dir):
    faces_by_role = {}
    for line in story["dialogues"]:
        if line["role_id"] and line["role_face"]:
            faces_by_role.setdefault(str(line["role_id"]), set()).add(line["role_face"])

    sources = {}
    for role_id, role in story["roles"].items():
        class_id = int(role_id) // 100
        expected_path = "UI/Portrait/Npc_Portrait_" + str(class_id)
        if role["portrait"] != expected_path:
            raise ValueError("Opening story portrait path differs from its role ID: " + role_id)
        bundle = "prefabs_ui_portrait"
        prefab_path = "Assets/Prefabs/UI/Portrait/Npc_Portrait_" + str(class_id) + ".prefab"
        root_obj = next((pointer for path, pointer in apk.container(bundle) if path == prefab_path), None)
        if root_obj is None:
            raise ValueError("Official opening portrait prefab is missing: " + prefab_path)
        portrait_component = None
        portrait_data = None
        for game_object, _ in prefab_walk(root_obj.read()):
            for entry in game_object.m_Component:
                component = entry.component.deref()
                if component.type.name != "MonoBehaviour":
                    continue
                script = component.read(check_read=False).m_Script.read()
                if script.m_ClassName == "PortraitFace":
                    if portrait_component is not None:
                        raise ValueError("Official portrait has multiple PortraitFace components: " + prefab_path)
                    portrait_component = component
                    portrait_data = schemas.read(component)
        if portrait_data is None:
            raise ValueError("Official portrait configuration is missing: " + prefab_path)
        sprites.record_source(portrait_component, "PortraitFace", prefab_path)
        class_faces = {entry["Type"]: entry for entry in portrait_data["mFacesProxy"]}
        requested_faces = faces_by_role.get(role_id, set())
        if not requested_faces.issubset(class_faces):
            raise ValueError("Opening story references a missing official portrait expression: "
                             + role_id + " " + str(sorted(requested_faces - class_faces.keys())))

        key = "portrait_" + str(class_id)
        portrait_root = convert_prefab(apk, sprites, schemas, prefab_path, bundle, target_dir)
        main_name = "npc_portrait_" + str(class_id)
        main_sprite = find_sprite(apk, sprites, class_id, main_name)
        set_prefab_image_sprite(portrait_root, "Main", main_sprite["key"])

        face_sprites = {}
        for face in sorted(requested_faces):
            proxy = class_faces[face]["FaceSpriteProxy"]
            if not proxy["mAssetGuid"]:
                raise ValueError("Official portrait expression has an empty Sprite GUID: " + face)
            face_name = main_name + "_" + face
            face_sprite = find_sprite(apk, sprites, class_id, face_name)
            face_sprites[face] = face_sprite["key"]

        role["portrait_prefab"] = key
        role["portrait_sprites"] = face_sprites
        role["need_flip_on_right"] = bool(portrait_data["mNeedFlipOnRight"])
        common_offset = portrait_data.get("mFaceCommonOffset") or {"x": 0.0, "y": 0.0}
        role["portrait_face_offset"] = [float(common_offset["x"]), float(common_offset["y"])]
        role["portrait_face_offsets"] = {
            entry["Type"]: [float(entry["Offset"]["x"]), float(entry["Offset"]["y"])]
            for entry in portrait_data.get("mFaceOffsets") or []
        }
        prefabs.append({"key": key, "root": portrait_root})
        sources[role_id] = {
            "prefab": dict(apk.provenance(root_obj.deref()), object=prefab_path,
                            raw_sha256=sha256(root_obj.deref().get_raw_data())),
            "portrait_face": dict(apk.provenance(portrait_component),
                                  raw_sha256=sha256(portrait_component.get_raw_data())),
        }
    return sources


def pack_atlas(sprite_cache, target):
    """Pack official sprites into one or more Unity-uploadable atlas pages.

    A fixed 1024 px shelf grew the sheet past Unity's texture limit once the portrait
    pages were added, which made Texture2D.LoadImage fail for the whole opening UI.
    Sprites are now shelf-packed into 4096 px pages; the page index travels with every
    sprite so the runtime can bind the matching texture.
    """
    from PIL import Image
    limit = 4096
    ordered = [(entry, sprite) for entry, sprite in sprite_cache.images.items() if sprite]
    ordered.sort(key=lambda item: (-item[1]["image"].size[1], str(item[0])))
    pages = []
    current = []
    x = 0
    y = 0
    row_height = 0
    for key, sprite in ordered:
        w, h = sprite["image"].size
        if w > limit or h > limit:
            raise ValueError("Official opening sprite exceeds a 4096 texture: " + str(key))
        if x + w > limit:
            x = 0
            y += row_height + 2
            row_height = 0
        if y + h > limit:
            pages.append(current)
            current = []
            x = 0
            y = 0
            row_height = 0
        current.append((key, sprite, x, y))
        x += w + 2
        row_height = max(row_height, h)
    if current:
        pages.append(current)

    entries = {}
    atlases = []
    target.parent.mkdir(parents=True, exist_ok=True)
    for index, placements in enumerate(pages):
        height = max(1, max(py + sprite["image"].size[1] for _, sprite, _, py in placements))
        height = (height + 3) & ~3
        atlas = Image.new("RGBA", (limit, height), (0, 0, 0, 0))
        for _, sprite, px, py in placements:
            atlas.paste(sprite["image"], (px, py))
        name = "atlas.png" if index == 0 else "atlas-" + str(index) + ".png"
        atlas.save(target.parent / name)
        atlases.append({"file": name, "width": limit, "height": height})
        for _, sprite, px, py in placements:
            entries[str(sprite["key"])] = {
                "x": px, "y": height - py - sprite["image"].size[1],
                "w": sprite["image"].size[0], "h": sprite["image"].size[1],
                "pivot": sprite["pivot"], "border": sprite["border"], "ppu": sprite["ppu"],
                "atlas": index,
            }
    if len(entries) != len(ordered):
        raise ValueError("Opening atlas lost sprites during packing.")
    return entries, atlases, len(entries)


def ui_payload(apk, target_dir, story):
    from official_unity_schema import ScriptSchemas

    schemas = ScriptSchemas()
    sprites = SpriteCache(apk)
    prefabs = []
    for key, (path, bundle) in UI_PANELS.items():
        prefabs.append({"key": key, "root": convert_prefab(apk, sprites, schemas, path, bundle, target_dir)})
    portrait_sources = portrait_prefabs(apk, sprites, schemas, story, prefabs, target_dir)
    try:
        hud_path = next(entry for entry, _ in apk.container(FIGHT_HUD_BUNDLE)
                        if entry.endswith("/" + FIGHT_HUD_PREFAB + ".prefab"))
        prefabs.append({"key": "record_fight_hud",
                        "root": convert_prefab(apk, sprites, schemas, hud_path, FIGHT_HUD_BUNDLE, target_dir)})
    except StopIteration:
        pass
    entries, atlases, count = pack_atlas(sprites, target_dir / "atlas.png")
    payload = {"manifest": VERSION, "atlas": atlases[0], "atlases": atlases,
               "sprites": entries, "prefabs": prefabs}
    atlas_paths = [target_dir / entry["file"] for entry in atlases]
    texture_outputs = list(sprites.texture_outputs.values())
    return payload, atlas_paths, texture_outputs, sprites.failures, count, portrait_sources, sprites.sources


def restore_ui_layout(apk):
    """Incrementally restore omitted layout components without repacking art atlases."""
    target = PAYLOAD / "ui" / "opening-ui.json"
    ui = json.loads(target.read_text(encoding="utf-8"))
    receipt = json.loads(REFERENCE.read_text(encoding="utf-8"))
    inputs = []
    for prefab in ui["prefabs"]:
        if prefab["key"] != "talk_dialog":
            continue
        nodes = {}
        def index(node):
            nodes[node["path"]] = node
            for child in node["children"]:
                index(child)
        index(prefab["root"])
        path, bundle = UI_PANELS[prefab["key"]]
        pointer = next(ptr for entry, ptr in apk.container(bundle) if entry == path)
        def visit(go, parent=""):
            node_path = parent + "/" + go.m_Name
            for component in go.m_Component:
                reader = component.component.deref()
                if reader.type.name == "MonoBehaviour":
                    cls = reader.read(check_read=False).m_Script.read().m_ClassName
                    raw = reader.get_raw_data()
                    if decode_layout_component(cls, raw, nodes[node_path]):
                        inputs.append(dict(apk.provenance(reader), script=cls, object_path=node_path,
                                           component_sha256=sha256(raw)))
                elif reader.type.name in ("RectTransform", "Transform"):
                    for child in reader.read().m_Children:
                        visit(child.read().m_GameObject.read(), node_path)
        visit(pointer.read())
    output = write_with_meta(target, json_bytes(ui))
    receipt["outputs"] = [output if row["path"] == output["path"] else row for row in receipt["outputs"]]
    receipt["ui"]["layout_sources"] = inputs
    receipt["ui"]["layout_generator_sha256"] = sha256(Path(__file__).read_bytes())
    REFERENCE.write_bytes((json.dumps(receipt, ensure_ascii=False, indent=2) + "\n").encode("utf-8"))
    print(json.dumps(dict(layout_components=len(inputs), output=output), ensure_ascii=False))


def main():
    from official_apk import Apk
    apk = Apk()
    if "--ui-layout-only" in sys.argv:
        restore_ui_layout(apk)
        return
    story_manifest = json.loads(STORY_MANIFEST.read_text(encoding="utf-8"))
    story = story_payload(apk, story_manifest)
    record = record_payload(apk)
    stage = stage_payload(apk)

    OFFICIAL_DATA.mkdir(parents=True, exist_ok=True)
    PAYLOAD.mkdir(parents=True, exist_ok=True)
    outputs = []
    outputs.append(write_with_meta(OFFICIAL_DATA / "opening-stage.json", json_bytes(stage)))
    outputs.append(write_with_meta(PAYLOAD / "opening-stage.json", json_bytes(stage)))
    outputs.append(write_with_meta(PAYLOAD / "opening-record.bytes", record["bytes"]))

    ui, atlas_paths, texture_outputs, failures, atlas_sprites, portrait_sources, ui_sources = ui_payload(
        apk, PAYLOAD / "ui", story)
    outputs.append(write_with_meta(OFFICIAL_DATA / "opening-story.json", json_bytes(story)))
    outputs.append(write_with_meta(PAYLOAD / "opening-story.json", json_bytes(story)))
    outputs.append(write_with_meta(PAYLOAD / "ui" / "opening-ui.json", json_bytes(ui)))
    for atlas_path in atlas_paths:
        outputs.append({"path": atlas_path.relative_to(ROOT).as_posix(), "bytes": atlas_path.stat().st_size,
                        "sha256": sha256(atlas_path.read_bytes())})
    outputs.extend(texture_outputs)

    manifest = {
        "manifest": VERSION,
        "record": {
            "source": record["source"],
            "path": RECORD_PATH,
            "gzip_bytes": len(record["bytes"]),
            "gzip_sha256": sha256(record["bytes"]),
            "decoded_sha256": record["decoded_sha256"],
            "summary": record["summary"],
        },
        "story": {
            "source_path": STORY_PATH.relative_to(ROOT).as_posix(),
            "source_sha256": story["source_sha256"],
            "graphs": story["provenance"],
            "dialogues": len(story["dialogues"]),
            "roles": story["roles"],
        },
        "stage": {"source": stage["source"], "stage_id": stage["stage_id"], "game_id": stage["game_id"],
                  "scene": stage["scene"], "monsters": stage["monsters"], "cells": len(stage["grid"]["cells"])},
        "ui": {"prefabs": [entry["key"] for entry in ui["prefabs"]], "sprites": atlas_sprites,
               "atlas": ui["atlas"], "atlases": ui["atlases"], "unresolved": failures,
               "portrait_sources": portrait_sources, "sources": ui_sources},
        "outputs": sorted(outputs, key=lambda entry: entry["path"]),
    }
    REFERENCE.parent.mkdir(parents=True, exist_ok=True)
    REFERENCE.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"record": record["summary"], "stories": list(story["graphs"]),
                      "dialogues": len(story["dialogues"]), "stage": stage["scene"],
                      "ui_sprites": atlas_sprites, "ui_unresolved": len(failures),
                      "outputs": len(outputs)}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
