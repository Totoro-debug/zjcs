"""Recover original entity directors and their scene/exposed reference targets."""
import json
from pathlib import Path
from collections import Counter

from official_apk import Apk, ROOT
from prepare_unity_bundle_inputs import serialized_files
from prepare_unity_map13_sources import encode, meta, sha, source_path, write
from prepare_unity_map14_entity_timeline_inputs import RECEIPT as TIMELINE_RECEIPT, SOURCE

OUTPUT = ROOT / "unity/Assets/Official/World/Map/14/Source/entity-timeline-bindings.json"
RECEIPT = ROOT / "assets/reference/official-map/map14-entity-timeline-bindings-source-manifest.json"


def record(path):
    return dict(path=source_path(path), sha256=sha(path.read_bytes()))


def build():
    entity_receipt = ROOT / "assets/reference/official-map/map14-native-entity-input-source-manifest.json"
    entity_components = ROOT / "unity/Assets/Official/World/Map/14/Source/native-entity-components.json"
    entities = json.loads(entity_receipt.read_bytes())
    timelines = json.loads(TIMELINE_RECEIPT.read_bytes())
    if entities["manifest"] != "871_156598" or timelines["manifest"] != entities["manifest"]:
        raise ValueError("Original binding source versions differ")
    proof = next(r for r in entities["outputs"] if r["path"] == source_path(entity_components))
    if sha(entity_components.read_bytes()) != proof["sha256"]:
        raise ValueError("Verified original entity component source changed")
    components = {(r["cab"].casefold(), int(r["path_id"])): r
                  for r in json.loads(entity_components.read_bytes())["components"]}
    source = json.loads(SOURCE.read_bytes())
    wanted = {(t["asset"]["cab"].casefold(), int(t["asset"]["path_id"])): t["asset"]["fields"]["m_Name"]
              for t in source["timelines"]}
    apk = Apk.current()
    bundles = sorted(set(entities["selection"]["roots"]) | {r["name"] for r in timelines["inputs"] if not r["name"].startswith("assets/bin/Data/")})
    objects, scanned, directors = {}, [], []
    for bundle in bundles:
        rows = list(apk.objects(bundle))
        if rows:
            scanned.append(apk.provenance(rows[0]))
        for obj in rows:
            objects[obj.assets_file.name.casefold(), obj.path_id] = obj

    def resolve(owner, pointer):
        if not pointer["m_PathID"]:
            return None
        cab = owner.assets_file.name.casefold()
        if pointer["m_FileID"]:
            cab = owner.assets_file.externals[pointer["m_FileID"] - 1].path.replace("\\", "/").rsplit("/", 1)[-1].casefold()
        return cab, pointer["m_PathID"]

    def fields(obj):
        key = obj.assets_file.name.casefold(), obj.path_id
        if obj.type.name == "MonoBehaviour":
            row = components[key]
            if sha(obj.get_raw_data()) != row["payload_sha256"]:
                raise ValueError("Original binding component payload changed")
            return row["fields"]
        return obj.read_typetree(check_read=True)

    captured, hierarchy = {}, {}

    def capture(key):
        if key is None:
            return None
        if key not in captured:
            obj = objects[key]
            f = fields(obj)
            captured[key] = dict(cab=key[0], path_id=str(key[1]),
                type=components[key]["type"] if obj.type.name == "MonoBehaviour" else obj.type.name,
                fields=f, source=apk.provenance(obj), payload_sha256=sha(obj.get_raw_data()))
        return captured[key]

    def path_for(key):
        if key is None:
            return None
        if key in hierarchy:
            return hierarchy[key]
        obj = objects[key];f = fields(obj)
        if obj.type.name != "GameObject":
            go = resolve(obj, f["m_GameObject"])
            result = path_for(go)
        else:
            transforms = [resolve(obj, x["component"]) for x in f["m_Component"]]
            transform_key = next(k for k in transforms if objects[k].type.name in ("Transform", "RectTransform"))
            transform = objects[transform_key];tf = fields(transform)
            parent = resolve(transform, tf["m_Father"])
            prefix = path_for(parent) + "/" if parent else ""
            result = prefix + f["m_Name"]
            capture(transform_key);capture(key)
        hierarchy[key] = result
        return result

    for key, obj in objects.items():
        if obj.type.name != "PlayableDirector":
            continue
        f = fields(obj);asset = resolve(obj, f["m_PlayableAsset"])
        if asset not in wanted:
            continue
        capture(key)
        owner = path_for(key)
        bindings = []
        for row in f["m_SceneBindings"]:
            target = resolve(obj, row["value"])
            capture(target)
            bindings.append(dict(track=resolve(obj, row["key"]), target=target, targetPath=path_for(target)))
        exposed = []
        for name, pointer in f["m_ExposedReferences"]["m_References"]:
            target = resolve(obj, pointer)
            capture(target)
            exposed.append(dict(name=name, target=target, targetPath=path_for(target)))
        directors.append(dict(timeline=wanted[asset], timelineIdentity=asset, director=key, ownerPath=owner,
                              sceneBindings=bindings, exposedReferences=exposed))
    if not directors:
        raise ValueError("No original entity Timeline director bindings found")
    write(OUTPUT, encode(dict(manifest=apk.version, mapClassId=14, directors=directors,
        objects=[dict(row, hierarchyPath=hierarchy.get(key)) for key, row in sorted(captured.items())])))
    meta(OUTPUT)
    counts = dict(directors=len(directors), capturedObjects=len(captured),
                  timelines=dict(Counter(r["timeline"] for r in directors)))
    write(RECEIPT, encode(dict(manifest=apk.version, mapClassId=14,
        inputs=[record(entity_receipt), record(entity_components), record(TIMELINE_RECEIPT), record(SOURCE)],
        scannedBundles=scanned, tools=[record(Path(__file__))], counts=counts,
        outputs=[record(OUTPUT), record(Path(str(OUTPUT) + ".meta"))],
        operation="Scan the verified original entity root bundles and five Timeline dependency closures for native PlayableDirectors. Preserve director fields, every scene/exposed reference including null and obsolete entries, exact CAB/PathID targets, original object/component fields, and transform-parent paths. Resolve targets by original pointer identity; never infer bindings from similar names.",
        limitations=["Source binding recovery only. Unreferenced or dynamically selected Timeline directors require their original caller configuration; no absent binding is invented. Native prefab/runtime/actor integration remains required."])))
    apk.archive.close()
    print("Original entity Timeline director bindings:", counts, flush=True)


if __name__ == "__main__":
    build()
