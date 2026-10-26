"""Restore exact APK tags, layer slots and signed-compatible sorting IDs."""
import json
from pathlib import Path
from official_apk import Apk, ROOT
from prepare_unity_bundle_inputs import serialized_files
from prepare_unity_map13_sources import encode, sha, source_path, write, meta


def build():
    apk = Apk.current(); apk.load_builtin("assets/bin/Data/data.unity3d")
    file = next(f for b in apk.loaded.values() for f in serialized_files(b) if f.name == "globalgamemanagers")
    obj = file.objects[3]
    if obj.type.name != "TagManager": raise ValueError("Original layer configuration identity changed")
    fields = obj.read_typetree()
    if len(fields["layers"]) != 32 or len(fields["m_SortingLayers"]) != 23 or fields["layers"][13] != "WaterMask":
        raise ValueError("Original map rendering layer closure changed")
    source = ROOT / "unity/Assets/Official/World/Map/13/Source/native-layers.json"
    write(source, encode(dict(manifest=apk.version, fields=fields))); meta(source)
    text = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!78 &1\nTagManager:\n  serializedVersion: 2\n  tags:\n"
    text += "".join("  - " + json.dumps(v, ensure_ascii=False) + "\n" for v in fields["tags"])
    text += "  layers:\n" + "".join("  - " + json.dumps(v, ensure_ascii=False) + "\n" for v in fields["layers"])
    text += "  m_SortingLayers:\n"
    text += "".join("  - name: " + json.dumps(v["name"], ensure_ascii=False) + "\n    uniqueID: " + str(v["uniqueID"]) + "\n    locked: 0\n" for v in fields["m_SortingLayers"])
    output = ROOT / "unity/ProjectSettings/TagManager.asset"
    # Refuse to erase additional local configuration from concurrent work.
    if output.exists():
        import yaml
        old = yaml.safe_load("\n".join(l for l in output.read_text(encoding="utf8").splitlines() if not l.startswith(("%", "---"))))["TagManager"]
        if set(old.get("tags", [])) - set(fields["tags"]): raise ValueError("Local tags require reconciliation")
        if any(v and v != fields["layers"][i] for i, v in enumerate(old["layers"])): raise ValueError("Local layer slots require reconciliation")
        allowed = {(v["name"], v["uniqueID"]) for v in fields["m_SortingLayers"]}
        if any((v["name"], v["uniqueID"]) not in allowed for v in old["m_SortingLayers"]): raise ValueError("Local sorting layers require reconciliation")
    write(output, text.encode("utf8"))
    validator = ROOT / "unity/Assets/Game/Editor/Map13/OfficialMap13LayerTools.cs"
    receipt = dict(format=1, mapClassId=13, manifest=apk.version,
        input=dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data())),
        outputs=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [source, output]],
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        operation="Strictly decode the original APK TagManager and restore every tag, all 32 physical layer slots and all 23 ordered sorting layers with original unsigned IDs. Rendering components consume the same IDs as signed 32-bit values.")
    if validator.exists(): receipt["validation"] = dict(path=source_path(validator), sha256=sha(validator.read_bytes()))
    write(ROOT / "assets/reference/official-map/map13-layers-source-manifest.json", encode(receipt))
    print("Restored original APK tags/layers: 19 tags, 32 slots, 23 sorting layers")


if __name__ == "__main__": build()
