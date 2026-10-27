"""Record the restored Map13 native UI outputs after editor verification."""
import json
from pathlib import Path
from prepare_unity_map13_sources import ROOT, encode, sha, source_path, write
from official_native_yaml_verification import documents


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
    particle_receipt = ROOT / "assets/reference/official-map/map13-native-ui-particle-assets-source-manifest.json"
    particles = json.loads(particle_receipt.read_bytes())
    native_counts = {kind: sum(1 for path in outputs if path.suffix == ".prefab"
        for actual, _ in documents(path.read_text(encoding="utf8")).values() if actual == kind)
        for kind in ("ParticleSystem", "ParticleSystemRenderer")}
    if native_counts != {"ParticleSystem": 80, "ParticleSystemRenderer": 80}:
        raise ValueError("Original native UI particle components are not fully bound: " + repr(native_counts))
    result = dict(manifest="871_156598", mapClassId=13,
        inputs=[record(p) for p in (source, ROOT / "unity/Assets/Official/World/Map/13/UI/source-manifest.json", particle_receipt)],
        tools=[record(p) for p in (Path(__file__).resolve(), ROOT / "unity/Assets/Game/Editor/OfficialMap11UiTools.cs",
            ROOT / "unity/Assets/Game/Editor/OfficialMap11UiTools.Support.cs",
            ROOT / "unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudRenderer.cs",
            ROOT / "unity/Assets/Game/Editor/Map13UiParticles/OfficialMap13UiParticleTools.cs",
            ROOT / "unity/Assets/Game/Editor/Map13UiParticles/OfficialMap13UiParticleTools.Playback.cs")],
        outputs=[dict(record(p), meta_sha256=sha(Path(str(p) + ".meta").read_bytes())) for p in outputs],
        counts=dict(source_prefabs=len(data["panels"]), nodes=sum(len(p["nodes"]) for p in data["panels"]), sprites=len(data["sprites"]), native_particle_systems=native_counts["ParticleSystem"], coffee_components=len(particles["coffee"]), native_materials=len(particles["materials"]), native_textures=len(particles["textures"])),
        operation="Restore original Map13 UI layout and supported components through the existing native importer. Preserve original Transform vs RectTransform, quaternion and position, and Image enable state. Bind all 80 original ParticleSystem/Renderer pairs, 32 Coffee components with exact source parameters and ordered references, 16 original materials, 17 complete-mip textures and the original mesh through the verified Windows shader. UI audio and business bindings remain separate.",
        validation="Core UI import/compile and ProjectTools.Validate previously passed in the main project. This particle batch passed import/compile, ProjectTools.Validate and the same static checker in the isolated dependency snapshot (25,799 GUIDs); the shared main-project static run was blocked by another map's in-progress missing metadata. Two portrait sizes passed actual PlayMode pointer raycast/click, goddess states, popup/close animation, hierarchy and runtime-error checks; actual screenshots inspected. Separate component PlayMode playback passed all eight particle panels at both portrait sizes in the original URP pipeline, with actual screenshots and error-shader detection. The component fixture stops UI animations and explicitly activates effect states; production state timing remains separate. GPU readback verified all 112 original texture mip payloads/rows. Full world HUD state, remaining business interactions, audio and Windows Player are pending.")
    write(ROOT / "assets/reference/official-map/map13-ui-binding-source-manifest.json", encode(result))
    print("Map13 UI native bindings:", result["counts"], len(outputs), "output assets")


if __name__ == "__main__":
    build()
