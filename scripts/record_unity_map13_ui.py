"""Record the restored Map13 native UI outputs after editor verification."""
import json
from pathlib import Path
from prepare_unity_map13_sources import ROOT, encode, sha, source_path, write


def build():
    source = ROOT / "unity/Assets/Official/World/Map/13/UI/map13-ui.json"
    destination = ROOT / "unity/Assets/Game/Resources/Map13/UI"
    if (destination / "catalog.json").read_bytes() != source.read_bytes():
        raise ValueError("Map13 restored catalog differs from the official converter output")
    data = json.loads(source.read_bytes())
    for panel in data["panels"]:
        if not (destination / (panel["key"] + ".prefab")).is_file():
            raise ValueError("Missing original Map13 UI prefab: " + panel["key"])
    outputs = [p for p in sorted(destination.rglob("*")) if p.is_file() and p.suffix != ".meta"]
    def record(path):
        return dict(path=source_path(path), sha256=sha(path.read_bytes()))
    result = dict(manifest="871_156598", mapClassId=13,
        inputs=[record(p) for p in (source, ROOT / "unity/Assets/Official/World/Map/13/UI/source-manifest.json")],
        tools=[record(p) for p in (Path(__file__).resolve(), ROOT / "unity/Assets/Game/Editor/OfficialMap11UiTools.cs",
            ROOT / "unity/Assets/Game/Editor/OfficialMap11UiTools.Support.cs",
            ROOT / "unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudRenderer.cs")],
        outputs=[dict(record(p), meta_sha256=sha(Path(str(p) + ".meta").read_bytes())) for p in outputs],
        counts=dict(source_prefabs=len(data["panels"]), nodes=sum(len(p["nodes"]) for p in data["panels"]), sprites=len(data["sprites"])),
        operation="Restore original Map13 UI layout and supported components through the existing native importer. Preserve original Transform vs RectTransform, quaternion and position, and Image enable state. Source particles and sounds are captured separately; their full Windows binding is still required.",
        validation="Editor import/compile and ProjectTools.Validate passed in the main project. Two portrait sizes passed actual PlayMode pointer raycast/click, goddess states, popup/close animation, hierarchy and runtime-error checks; actual screenshots inspected. Full world HUD state, remaining interactions, particles/audio and Windows Player are pending.")
    write(ROOT / "assets/reference/official-map/map13-ui-binding-source-manifest.json", encode(result))
    print("Map13 UI native bindings:", result["counts"], len(outputs), "output assets")


if __name__ == "__main__":
    build()
