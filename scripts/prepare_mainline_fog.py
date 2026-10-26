"""Decode the pinned native MapRegionLight prefabs and custom Light2D fields."""
from pathlib import Path
import json
import struct
import re
import math
import shutil

from official_apk import Apk
from official_unity_schema import ScriptSchemas
from prepare_unity_map10_entities import ROOT, sha256, json_bytes, write_meta
from prepare_unity_map11_ui import write_ui_payload

OUT = ROOT / "unity/Assets/Official/World/Map/11/Resources/Map11/Fog/region-lights.json"
RECEIPT = ROOT / "assets/reference/official-map/map11-region-lights-source-manifest.json"


def decode_light(raw, expected_type=1):
    offset = 32

    def get(fmt):
        nonlocal offset
        values = struct.unpack_from("<" + fmt, raw, offset)
        offset += struct.calcsize("<" + fmt)
        return values[0] if len(values) == 1 else list(values)

    def align():
        nonlocal offset
        offset = (offset + 3) // 4 * 4

    result = {}
    for name, fmt in (("version", "i"), ("type", "i"), ("blend", "i"), ("falloffIntensity", "f"),
                      ("color", "4f"), ("intensity", "f"), ("volume", "f"), ("volumeEnabled", "B")):
        result[name] = get(fmt)
    align()
    result["layers"] = [get("i") for _ in range(get("i"))]
    for name, fmt in (("cookie", "iq"), ("oldCookie", "iq"), ("order", "i"), ("oldOverlap", "B")):
        result[name] = get(fmt)
    align()
    for name, fmt in (("overlap", "i"), ("normalDistance", "f"), ("normalQuality", "i"), ("oldNormal", "B")):
        result[name] = get(fmt)
    align()
    result["shadowEnabled"] = get("B")
    align()
    result["shadow"] = get("f")
    result["volumeShadowEnabled"] = get("B")
    align()
    result["volumeShadow"] = get("f")
    result["bounds"] = get("6f")
    result["useRecession"] = bool(get("B"))
    align()
    result["recessionCenter"] = get("3f")
    for name in ("recessionRadius", "recessionBlurEdge", "innerAngle", "outerAngle", "innerRadius", "outerRadius"):
        result[name] = get("f")
    result["sides"] = get("i")
    result["shapeAngle"] = get("f")
    result["shapeRadius"] = get("f")
    result["falloff"] = get("f")
    result["falloffOffset"] = get("2f")
    result["shape"] = [dict(zip(("x", "y", "z"), get("3f"))) for _ in range(get("i"))]
    if offset != len(raw) or result["type"] != expected_type or (expected_type == 1 and result["overlap"] != 1) or result["cookie"][1] != 0:
        raise ValueError("Unresolved original region shadow layout: " + str((offset, len(raw))))
    return result


def renderer_data(raw):
    # Native stripped metadata confirms no m_LayerMask in this build. Read the
    # ScriptableRendererData base, then every serialized Renderer2DData field.
    offset = 28
    def get(fmt):
        nonlocal offset
        values = struct.unpack_from("<" + fmt, raw, offset)
        offset += struct.calcsize("<" + fmt)
        return values[0] if len(values) == 1 else list(values)
    def string():
        nonlocal offset
        size = get("i")
        text = raw[offset:offset+size].decode("utf-8")
        offset = (offset + size + 3) // 4 * 4
        return text
    name = string()
    debug = [get("iq") for _ in range(2)]
    features = [get("iq") for _ in range(get("i"))]
    feature_map = [get("q") for _ in range(get("i"))]
    native_pass = get("i")
    result = dict(name=name, sortMode=get("i"), sortAxis=get("3f"), hdr=get("f"), textureScale=get("f"))
    result["blendStyles"] = [dict(name=string(), mask=get("i"), blend=get("i")) for _ in range(get("i"))]
    result["useDepthStencil"] = get("i")
    result["cameraSortingLayerTexture"] = get("i")
    result["cameraSortingBound"] = get("i")
    result["cameraDownsampling"] = get("i")
    result["lightTextureLimit"] = get("I")
    result["shadowTextureLimit"] = get("I")
    result["shaders"] = [get("iq") for _ in range(13)]
    result["postProcess"] = get("iq")
    result["falloffLookup"] = get("iq")
    if offset != len(raw) or features or feature_map or native_pass or result["blendStyles"][0]["mask"] != 0 or result["blendStyles"][0]["blend"] != 1:
        raise ValueError("Unconverted original 2D Renderer Data: " + str((offset, len(raw))))
    return result


def finite_curves(value):
    if isinstance(value, dict):
        return {key: finite_curves(item) for key, item in value.items()}
    if isinstance(value, list):
        return [finite_curves(item) for item in value]
    # Unity's native constant tangent is represented as Infinity. Encode its
    # exact semantic explicitly so Unity JSON contains only finite numbers.
    return "Constant" if isinstance(value, float) and not math.isfinite(value) else value


def build():
    apk, schemas, records, regions = Apk(), ScriptSchemas(), [], []

    def record(obj):
        records.append(dict(apk.provenance(obj), objectSha256=sha256(obj.get_raw_data())))
        return obj

    prefabs = sorted((int(match[1]), ptr.deref()) for path, ptr in apk.container("partition_mapground_11_extensions")
                     if (match := re.search(r"/LightRegion(\d+)\.prefab$", path)))
    for region, prefab in prefabs:
        root = record(prefab)
        nodes, lights, clips = [], [], []

        def visit(obj, parent=-1):
            go = obj.read()
            transform = next(c.component.read() for c in go.m_Component if c.component.type.name in ("Transform", "RectTransform"))
            node = dict(name=go.m_Name, parent=parent, active=bool(go.m_IsActive),
                        position={k: getattr(transform.m_LocalPosition, k) for k in "xyz"},
                        rotation={k: getattr(transform.m_LocalRotation, k) for k in "xyzw"},
                        scale={k: getattr(transform.m_LocalScale, k) for k in "xyz"})
            index = len(nodes)
            nodes.append(node)
            for entry in go.m_Component:
                component = entry.component.deref()
                if component.type.name == "MonoBehaviour":
                    kind = component.read(check_read=False).m_Script.read().m_ClassName
                    if kind == "Light2D":
                        light = decode_light(record(component).get_raw_data())
                        light["node"] = index
                        lights.append(light)
                    elif kind == "MapRegionLight":
                        fields = schemas.read(record(component))
                        if fields["Region"] != region:
                            raise ValueError("Original region light identity mismatch")
                elif component.type.name == "Animation":
                    player = record(component).read()
                    for ref in player.m_Animations:
                        clip = record(ref.deref())
                        native = clip.read_typetree()
                        if not native["m_Legacy"] or native["m_PPtrCurves"]:
                            raise ValueError("Unconverted region activation animation")
                        clips.append(dict(name=clip.read().m_Name, curves=native["m_FloatCurves"],
                                          position=native["m_PositionCurves"], scale=native["m_ScaleCurves"],
                                          rotation=native["m_RotationCurves"], events=native["m_Events"]))
            for child in transform.m_Children:
                visit(child.read().m_GameObject.deref(), index)

        visit(root)
        if len(lights) != 1:
            raise ValueError("Expected one native region shadow")
        radius = next(c for c in clips[0]["curves"] if c["attribute"] == "RecessionRadius")
        stop = next(c for c in clips[0]["curves"] if c["attribute"] == "m_IsActive" and c["path"] == "Night")
        regions.append(dict(region=region, nodes=nodes, light=lights[0], clips=clips,
            activationRadius=radius["curve"]["m_Curve"], duration=stop["curve"]["m_Curve"][-1]["time"]))
    apk.load("defaultpackage_assets_partition_mapground_11_mapground_11_bare.bundle")
    global_light = next(o for o in apk.env.objects if o.type.name == "MonoBehaviour"
        and o.read(check_read=False).m_GameObject.m_PathID
        and o.read(check_read=False).m_GameObject.read().m_Name == "Light 2D Global"
        and o.read(check_read=False).m_Script.read().m_ClassName == "Light2D")
    apk.load_builtin("assets/bin/Data/data.unity3d")
    renderer = next(o for o in apk.env.objects if o.type.name == "MonoBehaviour"
        and o.read(check_read=False).m_Name == "2D Renderer Data")
    rendering = renderer_data(record(renderer).get_raw_data())
    lookup = next(o for o in renderer.assets_file.objects.values() if o.path_id == rendering["falloffLookup"][1])
    lookup = record(lookup)
    if lookup.read().m_Name != "FalloffLookupTexture":
        raise ValueError("Unexpected native light falloff texture")
    from prepare_unity_map11_rendering import texture_meta
    texture = OUT.parent / "FalloffLookupTexture.png"
    texture.parent.mkdir(parents=True, exist_ok=True)
    lookup.read().image.save(texture)
    Path(str(texture)+".meta").write_bytes(texture_meta("Resources/Map11/Fog/FalloffLookupTexture.png").replace(b'wrapU: 0',b'wrapU: 1').replace(b'wrapV: 0',b'wrapV: 1').replace(b'wrapW: 0',b'wrapW: 1').replace(b'alphaIsTransparency: 1',b'alphaIsTransparency: 0'))
    payload = finite_curves(dict(manifest="854_156383", mapClassId=11, regions=regions,
        rendering=rendering, globalLight=decode_light(record(global_light).get_raw_data(), 4)))
    known = {}
    if RECEIPT.exists():
        old = json.loads(RECEIPT.read_text(encoding="utf-8"))
        known[OUT.resolve()] = old["output"]["sha256"]
        known[RECEIPT.resolve()] = sha256(RECEIPT.read_bytes())
    OUT.parent.mkdir(parents=True, exist_ok=True)
    raw = json_bytes(payload)
    write_ui_payload(OUT, raw, known)
    write_ui_payload(RECEIPT, json_bytes(dict(manifest=apk.version, inputs=records,
        output=dict(path=OUT.relative_to(ROOT).as_posix(), sha256=sha256(raw)),
        falloffTexture=dict(path=texture.relative_to(ROOT).as_posix(), sha256=sha256(texture.read_bytes())),
        converter=dict(path=Path(__file__).relative_to(ROOT).as_posix(), sha256=sha256(Path(__file__).read_bytes())))), known)
    write_meta(OUT, "TextScriptImporter")
    write_meta(OUT.parent, "DefaultImporter")
    tool = ROOT / ".tools/mainline-completion/fog-mesh-input.json"
    tool.write_bytes(json_bytes(dict(regions=[dict(region=r["region"], shape=r["light"]["shape"], falloff=r["light"]["falloff"])
                                           for r in regions])))
    print("Native region shadows:", len(regions))


if __name__ == "__main__":
    build()
