"""Verify original UI particle hierarchy and every native parameter after conversion."""
from collections import Counter
import json
import re
from pathlib import Path
import struct
import yaml
from official_apk import ROOT
from official_native_yaml_verification import documents
from prepare_unity_map13_sources import encode, sha, source_path, write
from prepare_unity_map13_ui_particle_inputs import CACHE, SOURCE, RECEIPT

EXPORT = CACHE / "export/ExportedProject/Assets"
PROOF = ROOT / "assets/reference/official-map/map13-native-ui-particle-export-source-manifest.json"


def build():
    receipt = json.loads(RECEIPT.read_bytes())
    source = json.loads(SOURCE.read_bytes())
    if receipt["sources"][0]["sha256"] != sha(SOURCE.read_bytes()):
        raise ValueError("Verified native UI particle source changed")
    objects = {o["identity"]: o for o in source["objects"]}
    selected = {p["key"]: p for p in source["prefabs"] if p["key"] in {p["key"] for p in receipt["selection"]}}
    prefabs = {p["key"]: next(EXPORT.rglob(p["key"] + ".prefab")) for p in receipt["selection"]}
    counts = Counter()
    bindings, output_files = [], []
    for key, path in prefabs.items():
        text = path.read_text(encoding="utf8")
        docs = documents(text)
        transforms = {i: f for i, (t, f) in docs.items() if t in ("Transform", "RectTransform")}
        roots = [i for i, f in transforms.items() if f["m_Father"]["fileID"] == 0]
        if len(roots) != 1:
            raise ValueError("Original particle UI prefab root differs: " + key)
        paired, node_bindings = {}, []
        def pair(node, transform_id, relative):
            native = objects[node["object"]]
            transform = transforms[transform_id]
            go_id = transform["m_GameObject"]["fileID"]
            go = docs[go_id][1]
            if ("" if go["m_Name"] is None else str(go["m_Name"])) != native["fields"]["m_Name"]:
                raise ValueError("Original UI particle sibling name differs: " + relative)
            source_transforms = [objects[c] for c in node["components"] if objects[c]["type"] in ("Transform", "RectTransform")]
            if len(source_transforms) != 1 or docs[transform_id][0] != source_transforms[0]["type"]:
                raise ValueError("Original UI particle Transform type differs: " + relative)
            paired[node["object"]] = go_id
            paired[source_transforms[0]["identity"]] = transform_id
            components = {docs[r["component"]["fileID"]][0]: r["component"]["fileID"] for r in go["m_Component"]}
            for identity in node["components"]:
                record = objects[identity]
                if record["type"] not in ("ParticleSystem", "ParticleSystemRenderer"):
                    continue
                if record["type"] not in components:
                    raise ValueError("Missing original native particle component: " + relative)
                file_id = components[record["type"]]
                paired[identity] = file_id
                node_bindings.append(dict(path=relative, original=record, file_id=file_id))
            children = transform["m_Children"]
            if len(children) != len(node["children"]):
                raise ValueError("Original UI particle hierarchy differs: " + relative)
            for index, (child, exported) in enumerate(zip(node["children"], children)):
                pair(child, exported["fileID"], relative + "/" + str(index) + ":" + objects[child["object"]]["fields"]["m_Name"])
        pair(selected[key]["hierarchy"], roots[0], key)
        references, signed_zeros = [], {}
        current_file_id = None
        def compare(expected, actual, field, cab, tokens=()):
            if isinstance(expected, dict):
                if set(expected) == {"m_FileID", "m_PathID"}:
                    if expected["m_PathID"] == 0:
                        if actual != {"fileID": 0}:
                            raise ValueError("Original null particle reference differs: " + field)
                    elif expected["m_FileID"] == 0 and cab + ":" + str(expected["m_PathID"]) in paired:
                        if actual != {"fileID": paired[cab + ":" + str(expected["m_PathID"])]}:
                            raise ValueError("Original internal particle reference differs: " + field)
                    else:
                        if not actual.get("fileID") or "guid" not in actual:
                            raise ValueError("Original external particle reference is absent: " + field)
                        references.append(dict(field=field, original=expected, exported=actual))
                    counts["references"] += 1
                    return
                if not isinstance(actual, dict) or set(expected) - set(actual):
                    raise ValueError("Original particle field names differ: " + field + " " + str(set(expected) - set(actual) if isinstance(actual, dict) else type(actual)))
                for name, value in expected.items():
                    compare(value, actual[name], field + "." + name, cab, tokens + (name,))
            elif isinstance(expected, list):
                if field.endswith((".m_VertexStreams", ".m_TrailVertexStreams")) and not isinstance(actual, list):
                    scalar = str(getattr(actual, "scalar", actual))
                    if len(scalar) != len(expected) * 2:
                        raise ValueError("Original packed vertex stream extent differs: " + field)
                    actual = list(bytes.fromhex(scalar))
                    counts["packed_vertex_streams"] += 1
                if actual is None and not expected:
                    actual = []
                if not isinstance(actual, list) or len(expected) != len(actual):
                    raise ValueError("Original particle array differs: " + field)
                for index, (left, right) in enumerate(zip(expected, actual)):
                    compare(left, right, field + "[" + str(index) + "]", cab, tokens + (index,))
            elif isinstance(expected, float):
                if struct.pack("<f", expected) != struct.pack("<f", actual):
                    if expected == 0.0 and actual == 0.0 and struct.pack("<f", expected) == bytes.fromhex("00000080"):
                        signed_zeros.setdefault(current_file_id, []).append(tokens)
                    else:
                        raise ValueError("Original particle float bits differ: " + field + " " + repr((expected, actual)))
            elif expected != actual:
                raise ValueError("Original particle scalar differs: " + field + " " + str((expected, actual)))
            counts["values"] += 1
        for binding in node_bindings:
            record = binding["original"]
            current_file_id = binding["file_id"]
            actual = docs[binding["file_id"]][1]
            omitted = {"m_LightmapIndex", "m_LightmapIndexDynamic", "m_LightmapTilingOffset", "m_LightmapTilingOffsetDynamic"}
            if record["type"] == "ParticleSystemRenderer" and set(record["fields"]) - set(actual) == omitted:
                if any(record["fields"][f] != 65535 for f in ("m_LightmapIndex", "m_LightmapIndexDynamic")) or any(
                    record["fields"][f] != dict(x=1.0, y=1.0, z=0.0, w=0.0) for f in ("m_LightmapTilingOffset", "m_LightmapTilingOffsetDynamic")):
                    raise ValueError("Inspect original particle lightmap data before restoration")
                lines = "  m_LightmapIndex: 65535\n  m_LightmapIndexDynamic: 65535\n  m_LightmapTilingOffset: {x: 1, y: 1, z: 0, w: 0}\n  m_LightmapTilingOffsetDynamic: {x: 1, y: 1, z: 0, w: 0}\n"
                pattern = r"(?m)(^--- !u!199 &" + str(binding["file_id"]) + r"\nParticleSystemRenderer:\n)"
                text, changed = re.subn(pattern, lambda m: m[1] + lines, text)
                if changed != 1:
                    raise ValueError("Cannot restore original lightmap fields")
                actual = dict(actual, **{f: record["fields"][f] for f in omitted})
                counts["restored_original_lightmap_fields"] += len(omitted)
            compare(record["fields"], actual, binding["path"] + ":" + record["type"], record["identity"].split(":")[0])
            counts[record["type"]] += 1
            bindings.append(dict(panel=key, hierarchy_path=binding["path"], original_identity=record["identity"],
                original_sha256=record["object_sha256"], export_path=source_path(path), file_id=binding["file_id"]))
        for file_id, paths in signed_zeros.items():
            pattern = r"(?ms)(^--- !u!\d+ &" + str(file_id) + r"\n)(.*?)(?=^--- !u!|\Z)"
            match = re.search(pattern, text)
            if match is None:
                raise ValueError("Cannot locate original signed-zero component")
            body = match[2]
            root_node = yaml.compose(body).value[0][1]
            replacements = []
            for tokens in paths:
                target = root_node
                for token in tokens:
                    target = target.value[token] if isinstance(token, int) else next(v for k, v in target.value if k.value == token)
                if target.value not in ("0", "-0", "0.0"):
                    raise ValueError("Inspect signed-zero export spelling before repair")
                replacements.append((target.start_mark.index, target.end_mark.index))
            for start, end in sorted(replacements, reverse=True):
                body = body[:start] + "-0.0" + body[end:]
            text = text[:match.start(2)] + body + text[match.end(2):]
            reread = documents(match[1] + body)[file_id][1]
            for tokens in paths:
                target = reread
                for token in tokens:
                    target = target[token]
                if struct.pack("<f", target) != bytes.fromhex("00000080"):
                    raise ValueError("Restored original negative zero did not survive YAML reading")
            counts["restored_signed_zero_scalars"] += len(paths)
        restored = CACHE / "restored" / path.relative_to(EXPORT)
        write(restored, text.encode("utf8"))
        output_files.append(dict(path=source_path(path), sha256=sha(path.read_bytes()),
            restored=dict(path=source_path(restored), sha256=sha(restored.read_bytes())), external_references=references))
        print("Verified original UI particle fields:", key, len(node_bindings), flush=True)
    if counts["ParticleSystem"] != 80 or counts["ParticleSystemRenderer"] != 80:
        raise ValueError("Original UI particle coverage differs: " + str(counts))
    result = dict(manifest="871_156598", mapClassId=13, counts=dict(counts), bindings=bindings, exports=output_files,
        inputs=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in (SOURCE, RECEIPT)],
        tools=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in (Path(__file__).resolve(), ROOT / "scripts/official_native_yaml_verification.py")],
        operation="Restore original negative-zero Float32 curve scalar spellings lost by AssetRipper, then reread and check their bits. Restore original renderer lightmap indices and tiling fields omitted by AssetRipper (all 80 have the original 65535 sentinel and identity scale/offset). Compare every original native ParticleSystem/Renderer field, exact Float32 values, array order, source hierarchy, node types and local object references. External material/mesh pointers are recorded for separate exact dependency restoration; this does not establish Windows shader or UI particle playback.")
    write(PROOF, encode(result))
    print("Verified native Map13 UI particle exports:", result["counts"], flush=True)
    return result


if __name__ == "__main__":
    build()
