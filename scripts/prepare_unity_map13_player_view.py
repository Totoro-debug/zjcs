"""Recover the selected original Map13 player model and shared world camera view."""
from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from prepare_unity_map13_sources import encode, meta, sha, source_path, write


def build():
    apk = Apk.current()
    if apk.version != "871_156598":
        raise ValueError("Unexpected Map13 player/camera source")
    code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    schemas = ScriptSchemas(code_root=code)

    def child(root, name):
        transform = next(c.component.read() for c in root.read().m_Component if c.component.type.name == "Transform")
        return next(ref.read().m_GameObject.deref() for ref in transform.m_Children
                    if ref.read().m_GameObject.read().m_Name == name)

    def component(root, kind, script=None):
        return next(c.component.deref() for c in root.read().m_Component if c.component.type.name == kind
                    and (script is None or c.component.deref().read(check_read=False).m_Script.read().m_ClassName == script))

    player = apk.asset("prefabs_ecentity_mapworld_worldmapcharacter", "/3001.prefab")
    model = player
    for name in ("3001_view", "Model", "Offset", "Player_Model_Map"):
        model = child(model, name)
    transform = component(model, "Transform")
    offset = transform.read().m_Father.deref()
    renderer = component(model, "MeshRenderer")
    camera_root = apk.asset("prefabs_map_common_entityui", "/WorldCamera.prefab")
    controller = child(camera_root, "controller")
    adaptor = component(controller, "MonoBehaviour", "OrthographicCameraResolutionAdaptor")
    camera = component(child(camera_root, "Main Camera"), "Camera")
    follow = child(controller, "followVCam")
    follow_controller = component(follow, "MonoBehaviour", "FollowCameraController")
    transposer = component(child(follow, "cm"), "MonoBehaviour", "CinemachineTransposer")
    camera_fields, follow_fields = schemas.read(adaptor), schemas.read(follow_controller)
    transposer_fields = schemas.read(transposer)
    vector = lambda value: {axis: getattr(value, axis) for axis in "xyz"}
    view = dict(modelScale=vector(transform.read().m_LocalScale), modelOffset=vector(offset.read().m_LocalPosition),
        sortingOrder=renderer.read().m_SortingLayer * 1000 + renderer.read().m_SortingOrder,
        sortingLayerId=renderer.read().m_SortingLayerID, nativeSortingOrder=renderer.read().m_SortingOrder,
        cameraSafeExtents=camera_fields["mySafeArea"]["m_Extent"], cameraAdaptPriority=camera_fields["mAdapterPriority"],
        cameraDefaultSize=camera.read().orthographic_size, cameraNear=camera.read().near_clip_plane,
        cameraFar=camera.read().far_clip_plane, cameraZ=follow_fields["mZOffset"],
        cameraXDamping=transposer_fields["m_XDamping"], cameraYDamping=transposer_fields["m_YDamping"],
        cameraZDamping=transposer_fields["m_ZDamping"])
    output = ROOT / "unity/Assets/Game/Resources/Map13/BaseGround/player-view.json"
    raw = encode(view)
    receipt = dict(manifest=apk.version, mapClassId=13,
        operation="Read unchanged original player model transform/sorting and WorldCamera adaptor/follow configuration; no base-ground geometry or camera runtime migration is claimed.",
        objects={name: dict(apk.provenance(obj), object_sha256=sha(obj.get_raw_data())) for name, obj in
            dict(player=player, model=model, transform=transform, offset=offset, renderer=renderer,
                 camera_root=camera_root, camera=camera, adaptor=adaptor, follow=follow_controller, transposer=transposer).items()},
        code=[dict(path=source_path(code / "client-source" / (name + ".cs")),
            sha256=sha((code / "client-source" / (name + ".cs")).read_bytes())) for name in
            ("FollowCameraController", "OrthographicCameraResolutionAdaptor", "WorldCameraBaseController")],
        output=dict(path=source_path(output), sha256=sha(raw)))
    write(output, raw)
    if not output.parent.with_name(output.parent.name + ".meta").exists():
        meta(output.parent, folder=True)
    meta(output)
    write(ROOT / "assets/reference/official-map/map13-player-view-source-manifest.json", encode(receipt))
    print("Map13 original player/camera view:", view)


if __name__ == "__main__":
    build()
