"""Recover the original shared SystemMsgPanel prefab, component and animation bindings."""
import math
from official_animation import mecanim_tracks
from prepare_cooking_module import CookingConverter
from prepare_character_prop_bindings import node_path


def restore_system_message(reader, text_schemas, city):
    converter = CookingConverter.__new__(CookingConverter)
    converter.apk = reader.apk
    converter.schemas = reader.schemas
    converter.text_mesh_pro_fields = text_schemas.read
    def components(game_object):
        transform, result = None, []
        ui_classes = {"ContentSizeFitter", "VerticalLayoutGroup", "HorizontalLayoutGroup", "LayoutElement", "Image", "TextMeshProUGUI"}
        for entry in game_object.m_Component:
            component = entry.component.deref()
            name, fields = component.type.name, None
            if name in ("RectTransform", "Transform"):
                transform = component.read(check_read=False)
                continue
            if name == "MonoBehaviour":
                name = component.read(check_read=False).m_Script.read().m_ClassName
                if name == "MirrorImage":
                    name, fields = "Image", reader.schemas.read(component)
                elif name in ui_classes:
                    fields = text_schemas.read(component)
            elif name == "CanvasGroup":
                fields = component.read_typetree()
            result.append((name, component, fields))
        return transform, result
    converter.game_object_components = components
    uris = set()
    def sprite(obj):
        value = obj.read(check_read=False)
        provenance = reader.apk.provenance(obj)
        matches = [uri for uri, record in city["resources"].items()
                   if record.get("object") == value.m_Name and record.get("path_id") == str(obj.path_id)
                   and record.get("bundle_hash") == provenance["bundle_hash"]
                   and uri.startswith("res://assets/official/sprites/")]
        if len(matches) != 1:
            raise ValueError(("SystemMsgPanel original sprite is not uniquely resolved", value.m_Name, matches))
        uris.add(matches[0])
        return {"file": matches[0], "border": [getattr(value.m_Border, axis) for axis in "xyzw"]}
    converter.add_sprite = sprite
    bundle = "defaultpackage_assets_prefabs_ui_public.bundle"
    flat, _ = converter.object_tree("SystemMsgPanel", bundle)
    rows = flat["nodes"]
    for row in rows:
        row["rotation"] = math.radians(row["rotation"])
        row["children"] = []
    for row in rows[1:]: rows[row["parentIndex"]]["children"].append(row)
    root = rows[0]
    source_path, prefab = next((path, ptr) for path, ptr in reader.apk.container(bundle)
                              if path.endswith("/SystemMsgPanel.prefab"))
    by_path = {row["path"]: row for row in rows}
    canvas_order = None
    safe_areas = {}
    for node in reader.walk(prefab.read()):
        for entry in node.m_Component:
            component = entry.component.deref()
            if component.type.name == "Canvas": canvas_order = component.read().m_SortingOrder
            if component.type.name == "MonoBehaviour":
                name = component.read(check_read=False).m_Script.read().m_ClassName
                if name == "MirrorImage":
                    fields = reader.schemas.read(component)
                    by_path[node_path(node)]["image"].update(mirror_type=fields["m_mirrorType"], reversed=bool(fields["mReversed"]))
                elif name == "SafeArea":
                    fields = reader.schemas.read(component)
                    safe_areas[node_path(node)] = {"source": reader.apk.provenance(component), "fields": {
                        key: fields[key] for key in ("PaddingTop", "PaddingBottom", "DisableTopAdapt", "DisableBottomAdapt")}}
            if component.type.name != "Animator": continue
            animator = component.read()
            owner = by_path[node_path(node)]
            for pointer in animator.m_Controller.read().m_AnimationClips:
                tracks, duration, loop, unknown = mecanim_tracks(pointer.deref(), owner)
                if unknown: raise ValueError(("Unconverted original SystemMsgPanel animation bindings", unknown))
                owner.setdefault("animations", []).append({"name": pointer.read().m_Name, "owner": owner["path"],
                    "duration": duration, "loop": loop, "tracks": tracks, "source": reader.apk.provenance(pointer.deref())})
    bindings = reader.component_fields(bundle, prefab.path_id, ("SystemMsgPanel",))["SystemMsgPanel"]
    return {"root": root, "source": {"bundle": bundle, "bundle_hash": reader.apk.index[bundle],
            "path_id": str(prefab.path_id), "prefab": source_path}, "bindings": bindings,
            "canvas_sorting_order": canvas_order, "safe_area_bindings": safe_areas}, uris
