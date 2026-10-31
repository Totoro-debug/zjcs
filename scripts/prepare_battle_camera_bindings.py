"""Recover the camera initializer and Cinemachine framing omitted by the scene export."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import urllib.request

from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

OUTPUT = ROOT / "unity/Assets/Game/Resources/Combat/battle-camera-bindings.json"
RECEIPT = ROOT / "assets/reference/battle-scenes/camera-source-manifest.json"
LAYOUT = ROOT / "assets/reference/battle-scenes/layout-source-manifest.json"


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    layout = json.loads(LAYOUT.read_bytes())
    scene_path = ROOT / layout["output"]
    if sha(scene_path.read_bytes()) != layout["output_sha256"]:
        raise ValueError("Original battle scene binding identity changed")
    scenes = json.loads(scene_path.read_bytes())["cameras"]
    editor = Path("D:/Tool/Unity/Editor/Data")
    schemas = ScriptSchemas.__new__(ScriptSchemas)
    schemas.generator = TypeTreeGenerator("2022.3.57f1", generator="AssetsTools")
    schemas.cache, schemas.records = {}, {}
    schemas.generator.load_local_dll_folder(str(ROOT / ".tools/source-packages/official-code/managed/decoded"))
    schemas.generator.load_local_dll_folder(str(editor / "Managed/UnityEngine"))
    cinemachine = ROOT / "unity/Library/ScriptAssemblies/Cinemachine.dll"
    schemas.generator.load_dll(cinemachine.read_bytes())
    schemas.generator.load_dll((editor / "NetStandard/ref/2.1.0/netstandard.dll").read_bytes())
    for name in ("mscorlib.dll", "System.Core.dll"):
        schemas.generator.load_dll((Path("C:/Windows/Microsoft.NET/Framework64/v4.0.30319") / name).read_bytes())
    apk = Apk.current()
    records, resolved = [], {}
    fight = apk.asset("scriptableconfig_modules_fight", "/FightSettings.asset")
    fight_receipt = ROOT / "assets/reference/battle-rules/report-score-settings-source-manifest.json"
    if sha(fight.get_raw_data()) != json.loads(fight_receipt.read_bytes())["object_sha256"]:
        raise ValueError("FightSettings object differs from the existing battle source")
    fight_settings = ScriptSchemas().read(fight)
    progression_path = ROOT / "unity/Assets/Official/Character/character-progression.json"
    progression = json.loads(progression_path.read_bytes())
    def setting(value, key):
        if isinstance(value, dict):
            if key in value:
                return value[key]
            for child in value.values():
                found = setting(child, key)
                if found is not None:
                    return found
        elif isinstance(value, list):
            for child in value:
                found = setting(child, key)
                if found is not None:
                    return found
        return None
    default_follow = setting(progression, "GameCameraDefautLock")
    if not isinstance(default_follow, bool):
        raise ValueError("Original camera default lock setting is absent")

    def record(obj, bundle):
        records.append(dict(bundle=bundle, path_id=str(obj.path_id), raw_sha256=sha(obj.get_raw_data())))

    for scene in sorted({entry["sceneName"] for entry in scenes}):
        bundle = "defaultpackage_assets_prefabs_ecentity_fight_fightstage_" + scene.removeprefix("game_") + ".bundle"
        sources = [item for item in layout["inputs"] if item["bundle"] == bundle]
        if not sources:
            raise ValueError("Scene has no original input receipt: " + scene)
        source = sources[0]
        objects = apk.objects(bundle)
        current = apk.provenance(objects[0])
        if current["bundle_hash"] != source["bundle_hash"]:
            if not source.get("url") or not source.get("bundle_sha256"):
                raise ValueError("Changed bundle has no recoverable historical input: " + bundle)
            cached = ROOT / ".tools/source-packages/historical/battle-camera" / bundle
            if not cached.exists():
                data = urllib.request.urlopen(source["url"], timeout=60).read()
                if len(data) != source["bundle_size"] or sha(data) != source["bundle_sha256"]:
                    raise ValueError("Historical source hash differs: " + bundle)
                cached.parent.mkdir(parents=True, exist_ok=True)
                cached.write_bytes(data)
            data = cached.read_bytes()
            if len(data) != source["bundle_size"] or sha(data) != source["bundle_sha256"]:
                raise ValueError("Historical cache hash differs: " + bundle)
            loaded = apk.env.load_file(apk.decode(data), name="battle-camera-original-" + bundle)
            objects = [obj for asset in loaded.files.values() if hasattr(asset, "objects") for obj in asset.objects.values()]
            for asset in loaded.files.values():
                if hasattr(asset, "objects"):
                    apk.env.register_cab(asset.name, asset)
        elif source.get("bundle_sha256") and current.get("bundle_sha256") != source["bundle_sha256"]:
            raise ValueError("Equal bundle name has different bytes: " + bundle)
        by_id = {obj.path_id: obj for obj in objects}
        scripts = {}
        for obj in objects:
            if obj.type.name == "MonoBehaviour":
                basic = obj.read(check_read=False)
                scripts[obj.path_id] = (basic.m_Script.read().m_ClassName, obj)
        initializers = [obj for name, obj in scripts.values() if name == "GameCameraIniter"]
        if len(initializers) != 1:
            raise ValueError("Expected one original camera initializer: " + scene)
        initializer = initializers[0]
        settings = schemas.read(initializer)
        controller = by_id[settings["GameCameraController"]["m_PathID"]]
        controller_settings = schemas.read(controller)
        adapter = settings["Adaptor"]
        virtual = by_id[adapter["mCamera"]["m_PathID"]]
        virtual_settings = schemas.read(virtual)
        group = by_id[virtual_settings["m_Follow"]["m_PathID"]]
        # Follow is the target group's Transform, whose GameObject owns the group script.
        group_go = group.read().m_GameObject.path_id
        groups = [obj for name, obj in scripts.values() if name == "CinemachineTargetGroup"
                  and obj.read(check_read=False).m_GameObject.path_id == group_go]
        if len(groups) != 1:
            raise ValueError("Original follow target is not a unique target group: " + scene)
        pipeline = virtual_settings["m_ComponentOwner"]["m_PathID"]
        pipeline_go = by_id[pipeline].read().m_GameObject.path_id
        framings = [obj for name, obj in scripts.values() if name == "CinemachineFramingTransposer"
                    and obj.read(check_read=False).m_GameObject.path_id == pipeline_go]
        if len(framings) != 1:
            raise ValueError("Original camera has no unique framing transposer: " + scene)
        framing = schemas.read(framings[0])
        for key in ("m_LookaheadIgnoreY", "m_TargetMovementOnly", "m_UnlimitedSoftZone", "m_CenterOnActivate"):
            framing[key] = bool(framing[key])
        group_settings = schemas.read(groups[0])
        for obj in (initializer, controller, virtual, framings[0], groups[0]):
            record(obj, bundle)
        clean = lambda value: {key: item for key, item in value.items()
                               if key not in ("m_GameObject", "m_Script", "m_Name", "m_Enabled")}
        resolved[scene] = dict(sceneName=scene,
            baseAspect=[adapter["mBaseAspectRatio"]["x"], adapter["mBaseAspectRatio"]["y"]],
            baseSize=adapter["mBaseSize"], lens=virtual_settings["m_Lens"],
            priority=virtual_settings["m_Priority"], yOffset=controller_settings["myYOffset"],
            includeDead=bool(controller_settings["AutoRegisterCharacterDie"]),
            framingJson=json.dumps(clean(framing), separators=(",", ":")),
            groupJson=json.dumps(clean(group_settings), separators=(",", ":")))
    output = dict(defaultFollow=default_follow,
        viewBound=[fight_settings["CameraViewBound"][key] for key in ("x", "y")],
        cameras=[dict(resolved[item["sceneName"]], gameId=item["gameId"]) for item in scenes])
    payload = (json.dumps(output, ensure_ascii=False, separators=(",", ":")) + "\n").encode()
    if args.check:
        if OUTPUT.read_bytes() != payload:
            raise ValueError("Recovered battle camera bindings differ")
    else:
        OUTPUT.write_bytes(payload)
        source_code = ROOT / ".tools/source-packages/official-code/client-source"
        receipt = dict(operation="Read original GameCameraIniter.Adaptor, referenced virtual camera, target group and framing transposer; preserve original aspect adaptation and Cinemachine group framing. Historical bundle identities remain fixed.",
            layout_sha256=sha(LAYOUT.read_bytes()), original_objects=records,
            fight_settings_source=dict(apk.provenance(fight), object_sha256=sha(fight.get_raw_data())),
            progression_input=dict(path=str(progression_path.relative_to(ROOT)), sha256=sha(progression_path.read_bytes())),
            source_code={name: sha((source_code / name).read_bytes()) for name in
                ("GameCameraIniter.cs", "OrthographicCameraExtendAdaptor.cs", "GameCameraController.cs")},
            schema_cinemachine_sha256=sha(cinemachine.read_bytes()),
            output_sha256=sha(payload), generator_sha256=sha(Path(__file__).read_bytes()))
        RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Original battle camera bindings verified: {len(scenes)} game IDs, {len(resolved)} scenes")


if __name__ == "__main__":
    main()
