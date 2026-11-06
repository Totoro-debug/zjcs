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
    player_view = json.loads((ROOT / "unity/Assets/Game/Resources/Map13/BaseGround/player-view.json").read_bytes())
    common_receipt = json.loads((ROOT / "assets/reference/official-map/map15-world-view-source-manifest.json").read_bytes())
    common_path = ROOT / "unity/Assets/Game/Resources/Map15/BaseGround/player-view.json"
    expected_common = next(row for row in common_receipt["outputs"] if row["path"] == common_path.relative_to(ROOT).as_posix())
    if common_receipt["manifest"] != "884_156692" or record(common_path)["sha256"] != expected_common["sha256"]:
        raise ValueError("Retained common player/camera projection changed")
    common_view = json.loads(common_path.read_bytes())
    if any(player_view.get(key) != value for key, value in common_view.items()):
        raise ValueError("Shared player/camera runtime fields differ from the verified original common prefab")
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
        inputs=[record(SOURCE / name) for name in ("map14-ui.json", "source-manifest.json")]
            + [record(ROOT / "assets/reference/official-map/map15-world-view-source-manifest.json"),
               record(ROOT / "unity/Assets/Game/Resources/Map13/BaseGround/player-view.json"),
               record(ROOT / "unity/Assets/Game/Resources/Map15/BaseGround/player-view.json")],
        tools=[record(ROOT / name, "LF") for name in tools], outputs=outputs, spriteMetadata=sprite_metadata,
        counts=dict(prefabs=len(source["panels"]), sprites=len(source["sprites"]),
                    nodes=sum(len(panel["nodes"]) for panel in source["panels"]), components=len(source["components"])),
        operation="Restore original Map14 UI hierarchy and implemented components through the shared native importer. Preserve original non-UI Transforms, RectTransforms, quaternions, node order, Image enable bits, Sprite bindings and masks; hand-in UI remains in its own receipt.",
        runtimeBindings=dict(nativeUiRoot="Map14/UI/", playerViewResource="Map13/BaseGround/player-view",
            playerViewSource="Existing shared 3001/WorldCamera projection; runtime-consumed fields verified against the separately recorded 884_156692 common-prefab projection"),
        integration=[record(ROOT / name, "LF") for name in (
            "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapProfile.cs",
            "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.PlayerView.cs",
            "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Data.cs",
            "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.cs",
            "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineHudModule.Functions.cs",
            "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineHudModule.Portrait.cs",
            "unity/Assets/Game/Runtime/Presentation/World/OfficialMap11UiData.cs",
            "unity/Assets/Game/Runtime/Presentation/Character/CharacterNativePortraitCapture.cs",
            "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Dialogue.cs",
            "unity/Assets/Game/Editor/Map13Mainline/OfficialMap13MainlinePlayAcceptance.cs",
            "unity/Assets/Game/Editor/Map13Mainline/Zjcs.Editor.Map13Mainline.asmdef")],
        verificationMethod="Zjcs.Editor.Map13Mainline.OfficialMap13MainlinePlayAcceptance.RunMap14Hud",
        remaining=["remaining HUD and feature interactions", "remaining original component behaviors", "particle/effect and audio bindings", "full Map14 entry and local progression", "Windows Player release"])
    RECEIPT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("Recorded original Map14 UI:", result["counts"], len(outputs), "native outputs")
    return result


if __name__ == "__main__":
    build()
