"""Recover official combat HUD Graphic and CanvasGroup input flags from pinned bundles."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from refresh_current_ui_graphics import object_path


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def build():
    source_path = ROOT / "assets/reference/battle-hud/source-manifest.json"
    source = json.loads(source_path.read_bytes())
    runtime = json.loads((ROOT / "unity/Assets/Official/Combat/HUD/panels-runtime.json").read_bytes())
    nodes = {p["key"]: {n["path"] for n in p["nodes"]} for p in runtime["prefabs"]}
    apk, schemas = Apk(), ScriptSchemas()
    rows, evidence = [], []
    for key, entry in source["prefabs"].items():
        if key not in nodes:
            continue
        for obj in apk.objects(entry["bundle"]):
            if obj.type.name not in ("MonoBehaviour", "CanvasGroup"):
                continue
            basic = obj.read(check_read=False)
            if not basic.m_GameObject.path_id:
                continue
            path, root = object_path(basic.m_GameObject.deref())
            if root != str(entry["path_id"]) or path not in nodes[key]:
                continue
            if obj.type.name == "CanvasGroup":
                row = {"prefab": key, "path": path, "canvas_group": True,
                       "interactable": bool(basic.m_Interactable),
                       "blocks_raycasts": bool(basic.m_BlocksRaycasts)}
            else:
                if not basic.m_Script.path_id:
                    continue
                name = basic.m_Script.read().m_ClassName
                if name in ("ChatQuickMsgEntryPanel", "GameMatchMiniSwitchPanel"):
                    data = schemas.read(obj)
                    # These are the official OnEnable/Refresh branches for a local
                    # participant with no quick-message cooldown and no active match.
                    fields = ("EmojiPanel", "OpenBtnCDMask") if name == "ChatQuickMsgEntryPanel" else ("mContentGo",)
                    for field in fields:
                        reference = data[field]
                        target = obj.assets_file.objects[reference["m_PathID"]]
                        target_basic = target.read(check_read=False)
                        game_object = target if target.type.name == "GameObject" else target_basic.m_GameObject.deref()
                        target_path, target_root = object_path(game_object)
                        if target_root != str(entry["path_id"]) or target_path not in nodes[key]:
                            raise ValueError("HUD controller reference is outside the extracted prefab: " + field)
                        initial = {"prefab": key, "path": target_path, "initial_visibility": True, "active": False}
                        rows.append(initial)
                        evidence.append({**initial, "controller": name, "field": field,
                                         "source": apk.provenance(obj), "object_sha256": sha(obj.get_raw_data())})
                    continue
                if name not in ("Image", "DamagePartialImage", "DamageSequenceImage", "NoDrawingRayCast"):
                    continue
                data = schemas.read(obj)
                row = {"prefab": key, "path": path,
                       "raycast_target": bool(data["m_RaycastTarget"]),
                       "maskable": bool(data.get("m_Maskable", True))}
            rows.append(row)
            evidence.append({**row, "source": apk.provenance(obj), "object_sha256": sha(obj.get_raw_data())})
        print("Read battle HUD input:", key, flush=True)
    payload = (json.dumps({"manifest": "854_156383", "bindings": rows}, separators=(",", ":")) + "\n").encode()
    output = ROOT / "unity/Assets/Game/Resources/Combat/battle-hud-graphic-bindings.json"
    receipt = ROOT / "assets/reference/battle-hud/graphic-bindings-source-manifest.json"
    if output.exists():
        prior = json.loads(receipt.read_bytes())
        if sha(output.read_bytes()) != prior["output_sha256"]:
            raise ValueError("Refusing to overwrite modified combat HUD input bindings")
    output.write_bytes(payload)
    metadata = {"manifest": "854_156383", "output_sha256": sha(payload),
                "source_manifest": source_path.relative_to(ROOT).as_posix(),
                "source_manifest_sha256": sha(source_path.read_bytes()), "bindings": evidence,
                "output": output.relative_to(ROOT).as_posix(), "generator_sha256": sha(Path(__file__).read_bytes())}
    receipt.write_text(json.dumps(metadata, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    sidecar = output.with_name("battle-hud-graphic-bindings-source-manifest.json")
    sidecar.write_text(json.dumps({"manifest": "854_156383", "output_sha256": sha(payload)}, indent=2) + "\n", encoding="utf-8")
    return len(rows)


if __name__ == "__main__":
    print("Recovered HUD input bindings:", build())
