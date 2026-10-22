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

    def sprite_image(self, assets_file, file_id, path_id):
        key = (file_id, path_id)
        if key in self.images:
            return self.images[key]
        sprite = self.lookup(assets_file, file_id, path_id)
        if sprite is None or sprite.type.name != "Sprite":
            self.failures[str(key)] = "not a sprite object"
            self.images[key] = None
            return None
        data = sprite.read()
        rd = data.m_RD
        if data.m_SpriteAtlas is not None and data.m_SpriteAtlas.path_id:
            atlas = data.m_SpriteAtlas.read()
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
        texture = rd.texture.read()
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


def decode_text(raw, name):
    """Decode the stable TMP tail fields (color, size, alignment, margins)."""
    if len(raw) < 200:
        return None
    # Walk candidate float fields: pick the largest plausible font size and a
    # color quadruple whose alpha is close to 1.
    best = {"font_size": 0.0, "color": None, "alignment": 0, "margin": [0.0, 0.0, 0.0, 0.0]}
    for offset in range(88, len(raw) - 4, 4):
        value = struct.unpack_from("<f", raw, offset)[0]
        if 8.0 <= value <= 120.0 and abs(value - round(value)) < 0.001 and value > best["font_size"]:
            best["font_size"] = float(value)
    return best


def prefab_walk(go):
    rect = None
    for component in go.m_Component:
        if component.component.type.name == "RectTransform":
            rect = component.component.read()
    yield go, rect
    if rect is None:
        return
    for child in rect.m_Children:
        for entry in prefab_walk(child.read().m_GameObject.read()):
            yield entry


def convert_prefab(apk, sprites, schemas, path, bundle):
    pointer = next((ptr for entry, val in apk.container(bundle) if entry == path
                    for ptr in [val]), None)
    if pointer is None:
        pointer = next(ptr for entry, ptr in apk.container(bundle) if entry.endswith("/" + path.rsplit("/", 1)[-1]))
    root_go = pointer.read()
    assets_file = pointer.assetsfile
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
            "alpha": 1.0,
        }
        rect = None
        for component in go.m_Component:
            kind = component.component.type.name
            if kind == "RectTransform":
                rect = component.component.read()
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
                if cls == "Image":
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
                    decoded = decode_text(raw, go.m_Name) or {}
                    node["text_value"] = ""
                    node["text_font_size"] = decoded.get("font_size") or 32.0
                    node["text_color"] = decoded.get("color") or [1.0, 1.0, 1.0, 1.0]
                    node["text_alignment"] = 0
                    node["text_margin"] = [0.0, 0.0, 0.0, 0.0]
                elif cls in ("Button", "UIButton"):
                    node["button"] = True
                elif cls in ("Mask", "RectMask2D"):
                    node["mask"] = True
                elif cls == "CanvasGroup":
                    node["canvas_group"] = True
            except Exception as error:
                node.setdefault("errors", []).append(cls + ": " + type(error).__name__)
        if rect is not None:
            node["anchor_min"] = [float(rect.m_AnchorMin.x), float(rect.m_AnchorMin.y)]
            node["anchor_max"] = [float(rect.m_AnchorMax.x), float(rect.m_AnchorMax.y)]
            node["anchored_position"] = [float(rect.m_AnchoredPosition.x), float(rect.m_AnchoredPosition.y)]
            node["size_delta"] = [float(rect.m_SizeDelta.x), float(rect.m_SizeDelta.y)]
            node["pivot"] = [float(rect.m_Pivot.x), float(rect.m_Pivot.y)]
            node["scale"] = [float(rect.m_LocalScale.x), float(rect.m_LocalScale.y)]
            node["rotation"] = float(rect.m_LocalRotation is not None and 0.0 or 0.0)
            for child in rect.m_Children:
                node["children"].append(build(child.read().m_GameObject.read(), node_path))
        return node

    return build(root_go, "")


def pack_atlas(sprite_cache, target):
    from PIL import Image
    entries = {}
    ordered = sorted((entry for entry in sprite_cache.images.items() if entry[1]),
                     key=lambda item: str(item[0]))
    width = 1024
    x = 0
    y = 0
    row_height = 0
    placements = []
    for key, sprite in ordered:
        w, h = sprite["image"].size
        if x + w > width:
            x = 0
            y += row_height + 2
            row_height = 0
        placements.append((key, sprite, x, y))
        x += w + 2
        row_height = max(row_height, h)
    height = y + row_height
    height = max(1, (height + 3) & ~3)
    atlas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    for key, sprite, px, py in placements:
        atlas.paste(sprite["image"], (px, py))
        entries[str(sprite["key"])] = {
            "x": px, "y": height - py - sprite["image"].size[1],
            "w": sprite["image"].size[0], "h": sprite["image"].size[1],
            "pivot": sprite["pivot"], "border": sprite["border"], "ppu": sprite["ppu"],
        }
    target.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(target)
    return entries, atlas.size, len(placements)


def ui_payload(apk, target_dir):
    from official_unity_schema import ScriptSchemas

    schemas = ScriptSchemas()
    sprites = SpriteCache(apk)
    prefabs = []
    for key, (path, bundle) in UI_PANELS.items():
        prefabs.append({"key": key, "root": convert_prefab(apk, sprites, schemas, path, bundle)})
    try:
        hud_path = next(entry for entry, _ in apk.container(FIGHT_HUD_BUNDLE)
                        if entry.endswith("/" + FIGHT_HUD_PREFAB + ".prefab"))
        prefabs.append({"key": "record_fight_hud",
                        "root": convert_prefab(apk, sprites, schemas, hud_path, FIGHT_HUD_BUNDLE)})
    except StopIteration:
        pass
    atlas_path = target_dir / "atlas.png"
    entries, size, count = pack_atlas(sprites, atlas_path)
    payload = {"manifest": VERSION, "atlas": {"file": "atlas.png", "width": size[0], "height": size[1]},
               "sprites": entries, "prefabs": prefabs}
    return payload, atlas_path, sprites.failures, count


def main():
    from official_apk import Apk
    apk = Apk()
    story_manifest = json.loads(STORY_MANIFEST.read_text(encoding="utf-8"))
    story = story_payload(apk, story_manifest)
    record = record_payload(apk)
    stage = stage_payload(apk)

    OFFICIAL_DATA.mkdir(parents=True, exist_ok=True)
    PAYLOAD.mkdir(parents=True, exist_ok=True)
    outputs = []
    outputs.append(write_with_meta(OFFICIAL_DATA / "opening-story.json", json_bytes(story)))
    outputs.append(write_with_meta(OFFICIAL_DATA / "opening-stage.json", json_bytes(stage)))
    outputs.append(write_with_meta(PAYLOAD / "opening-story.json", json_bytes(story)))
    outputs.append(write_with_meta(PAYLOAD / "opening-stage.json", json_bytes(stage)))
    outputs.append(write_with_meta(PAYLOAD / "opening-record.bytes", record["bytes"]))

    ui, atlas_path, failures, atlas_sprites = ui_payload(apk, PAYLOAD / "ui")
    outputs.append(write_with_meta(PAYLOAD / "ui" / "opening-ui.json", json_bytes(ui)))
    outputs.append({"path": atlas_path.relative_to(ROOT).as_posix(), "bytes": atlas_path.stat().st_size,
                    "sha256": sha256(atlas_path.read_bytes())})

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
               "atlas": ui["atlas"], "unresolved": failures},
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
