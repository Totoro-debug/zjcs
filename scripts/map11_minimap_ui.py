"""Restore Map11 minimap mark prefabs and their official entity bindings."""
import json
from pathlib import Path

from UnityPy.classes.PPtr import PPtr
from official_apk import ROOT


def extract_marks(apk, schemas, exporter, animation_record, sprites, map_id=11):
    definition_path = ROOT / f"unity/Assets/Official/World/Map/{map_id}/map{map_id}.json"
    templates_path = ROOT / f"unity/Assets/Official/World/Map/{map_id}/Resources/Map{map_id}/stories.json"
    definition = json.loads(definition_path.read_text("utf-8"))
    templates = {row["EntityClassId"]: {c["Type"]: json.loads(c["Json"]) for c in row["Components"]}
                 for row in json.loads(templates_path.read_text("utf-8"))["entityTemplates"]}
    rows = lambda table: [json.loads(row["Json"]) for row in definition["Configs"] if row["TableName"] == table]
    extends = {row["EntityClassId"]: row for row in rows("TbEntityExtendParameterInfo")}
    areas = {row["Area"]: row["AreaTag"] for row in rows("TbMapAreaInfo")}
    ui = {}
    for row in rows("TbEntityMapUiDisplayInfo"):
        ui.setdefault(row["EntityClassId"], {}).setdefault(row["GroupId"], {}).setdefault(row["MapClassId"], {}) \
            .setdefault(row["AreaTag"], {})[row["Tag"]] = row
    placements = {row["EntityId"]: json.loads(row["Json"]) for row in definition["EntityComponents"]}
    bindings = []
    for entity in definition["Entities"]:
        cls, placement = entity["EntityId"], entity["Id"]
        components = templates[cls]
        if "WorldStageComponent" not in components:
            continue
        extend = extends.get(cls)
        if extend is None:
            continue
        value = ui.get(cls, {})
        value = value.get(definition["MapGroupId"], value.get(0, {}))
        value = value.get(map_id, value.get(0, {}))
        value = value.get(areas[entity["Area"]], value.get("IgnoreKey", {}))
        tag = placements[placement].get("LoEntityComponent", {}).get("Tag") or ""
        value = value.get(tag, value.get("", {}))
        key = value.get("MiniMapMark") or extend.get("MiniMapMark")
        if not key:
            continue
        grid = components["GridTransformComponent"]
        positions = [row["Pos"] for row in grid["BodyRangeInfos"]]
        center = grid["RealCenter"]
        if not positions:
            raise ValueError("MiniMapEntityMark body range is empty: " + str(cls))
        # MiniMapEntityMark.GetEntityMarkPos, including the actual body centroid.
        offset = [sum(row[axis] for row in positions) / len(positions) - center[index]
                  for index, axis in enumerate(("X", "Y"))]
        bindings.append({"placementId": placement, "key": key, "offset": {"x": offset[0], "y": offset[1]},
                         "hideInEnd": extend["MiniMapMarkHideInEnd"], "completeState": extend["CompleteState"]})

    prefabs, components_out, sources = [], [], {}
    for key in sorted({row["key"] for row in bindings}):
        root = apk.asset("defaultpackage_assets_prefabs_ui_map_minimap_minimapmark.bundle", "/" + key + ".prefab")
        paths, nodes, behaviours, animations = {}, [], [], []
        def visit(obj, parent=-1, prefix=""):
            game_object = obj.read()
            index = len(nodes)
            path = prefix + "/" + game_object.m_Name
            node = {"name": game_object.m_Name, "path": path, "parentIndex": parent, "active": bool(game_object.m_IsActive)}
            nodes.append(node)
            paths[(obj.assets_file.name, obj.path_id)] = path
            children = []
            for entry in game_object.m_Component:
                component = entry.component.deref()
                paths[(component.assets_file.name, component.path_id)] = path
                if component.type.name in ("Transform", "RectTransform"):
                    transform = component.read()
                    node.update(position={k: getattr(transform.m_LocalPosition, k) for k in "xyz"},
                                rotation={k: getattr(transform.m_LocalRotation, k) for k in "xyzw"},
                                scale={k: getattr(transform.m_LocalScale, k) for k in "xyz"})
                    children = transform.m_Children
                elif component.type.name == "SpriteRenderer":
                    renderer = component.read()
                    if not renderer.m_Sprite.m_PathID:
                        continue
                    for material in renderer.m_Materials:
                        if material.read().m_Shader.read().m_ParsedForm.m_Name != "Sprites/Default":
                            raise ValueError("Unconverted minimap mark shader: " + key)
                    sprite = exporter.add_sprite(renderer.m_Sprite.deref())
                    sprites[sprite["file"]] = sprite
                    node.update(sprite=sprite["file"], color={k: getattr(renderer.m_Color, k) for k in "rgba"},
                                sortingOrder=renderer.m_SortingLayer * 1000 + renderer.m_SortingOrder,
                                flipX=bool(renderer.m_FlipX), flipY=bool(renderer.m_FlipY))
                elif component.type.name == "Animation":
                    animations.append((path, component))
                elif component.type.name == "MonoBehaviour":
                    behaviours.append(component)
            for child in children:
                visit(child.read().m_GameObject.deref(), index, path)
        visit(root)
        def resolve(value, owner):
            if isinstance(value, dict):
                if "m_FileID" in value and "m_PathID" in value:
                    if not value["m_PathID"]:
                        return None
                    obj = PPtr(**value, assetsfile=owner.assets_file).deref()
                    return paths.get((obj.assets_file.name, obj.path_id), {"source": apk.provenance(obj), "type": obj.type.name})
                return {k: resolve(v, owner) for k, v in value.items()}
            if isinstance(value, list):
                return [resolve(v, owner) for v in value]
            return value
        for obj in behaviours:
            kind = obj.read(check_read=False).m_Script.read().m_ClassName
            if kind not in ("MiniMapActiveSwitcherSupport", "MiniMapWorldStageSwitcherSupport", "MinimapMarkKeepSizeObjSupport",
                            "MiniMapActiveByNPCVisibleSupporter", "MapEntityTipDotNodeLinker"):
                continue
            fields = schemas.read(obj)
            # Script is an external asset; it is recorded by provenance, not a scene reference.
            fields.pop("m_Script", None)
            components_out.append({"panel": key, "path": paths[(obj.assets_file.name, obj.path_id)], "type": kind,
                                   "fields": resolve(fields, obj), "source": apk.provenance(obj)})
        for path, obj in animations:
            native = obj.read()
            components_out.append({"panel": key, "path": path, "type": "NativeAnimation",
                "fields": {"clips": [animation_record(ref.deref(), {"path": path}) for ref in native.m_Animations if ref.m_PathID],
                           "defaultClip": native.m_Animation.read().m_Name if native.m_Animation.m_PathID else "",
                           "playAutomatically": bool(native.m_PlayAutomatically)}, "source": apk.provenance(obj)})
        prefabs.append({"key": key, "nodes": nodes})
        sources[key] = apk.provenance(root)
    return prefabs, bindings, components_out, sources, [definition_path, templates_path]
