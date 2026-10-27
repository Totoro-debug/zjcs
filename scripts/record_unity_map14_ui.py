"""Record restored Map14 UI assets while keeping hand-in UI provenance separate."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "unity/Assets/Official/World/Map/14/UI"
DESTINATION = ROOT / "unity/Assets/Game/Resources/Map14/UI"
RECEIPT = ROOT / "assets/reference/official-map/map14-ui-binding-source-manifest.json"


def record(path, line_endings=None):
    raw = path.read_bytes()
    if line_endings == "LF":
        raw = raw.replace(b"\r\n", b"\n")
    value = dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(raw).hexdigest())
    if line_endings:
        value["hashLineEndings"] = line_endings
    return value


def build():
    raw = (SOURCE / "map14-ui.json").read_bytes()
    source = json.loads(raw)
    if source["manifest"] != "871_156598" or source["mapClassId"] != 14 or len(source["panels"]) != 68:
        raise ValueError("Map14 UI source does not match the selected official inputs")
    if (DESTINATION / "catalog.json").read_bytes() != raw:
        raise ValueError("The Map14 runtime UI catalog differs from its original conversion")
    for panel in source["panels"]:
        path = DESTINATION / (panel["key"] + ".prefab")
        if not path.is_file():
            raise ValueError("An original Map14 UI prefab is not bound: " + panel["key"])
    outputs = []
    for path in sorted(DESTINATION.rglob("*")):
        relative = path.relative_to(DESTINATION)
        if not path.is_file() or relative.parts[0] in ("HandIn", "HandIn.meta") or path.suffix == ".meta":
            continue
        value = record(path)
        value["meta_sha256"] = record(Path(str(path) + ".meta"))["sha256"]
        outputs.append(value)
    sprite_metadata = [record(path) for path in sorted((SOURCE / "Sprites").glob("*.png.meta"))]
    tools = ["scripts/prepare_unity_map14_ui.py", "scripts/prepare_unity_map11_ui.py",
             "scripts/map11_support_ui.py", "scripts/map11_minimap_ui.py", "scripts/record_unity_map14_ui.py",
             "unity/Assets/Game/Editor/OfficialMap11UiTools.cs", "unity/Assets/Game/Editor/OfficialMap11UiTools.Support.cs",
             "unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.Map14Ui.cs"]
    result = dict(format=1, manifest="871_156598", mapClassId=14,
        inputs=[record(SOURCE / name) for name in ("map14-ui.json", "source-manifest.json")],
        tools=[record(ROOT / name, "LF") for name in tools], outputs=outputs, spriteMetadata=sprite_metadata,
        counts=dict(prefabs=len(source["panels"]), sprites=len(source["sprites"]),
                    nodes=sum(len(panel["nodes"]) for panel in source["panels"]), components=len(source["components"])),
        operation="Restore original Map14 UI hierarchy and implemented components through the shared native importer. Preserve original non-UI Transforms, RectTransforms, quaternions, node order, Image enable bits, Sprite bindings and masks; hand-in UI remains in its own receipt.",
        verificationMethod="Zjcs.Editor.OfficialMap12PlayModeAcceptance.RunUiMap14Imported",
        remaining=["remaining HUD and feature interactions", "remaining original component behaviors", "particle/effect and audio bindings", "full Map14 entry and local progression", "Windows Player release"])
    RECEIPT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("Recorded original Map14 UI:", result["counts"], len(outputs), "native outputs")
    return result


if __name__ == "__main__":
    build()
