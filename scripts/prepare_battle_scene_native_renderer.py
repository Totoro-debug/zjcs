"""Record the independent native drawing endpoint against verified scene inputs.

No animation SDK source or implementation is read. Original art/prefab payloads
are already extracted by the existing source and texture preparation scripts.
"""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def identity(relative):
    return {"path": relative, "sha256": hashlib.sha256((ROOT / relative).read_bytes()).hexdigest()}


def main():
    source = json.loads((ROOT / "assets/reference/battle-animation/scene-core-source-manifest.json").read_bytes())
    assert source["manifest"] == "884_156692"
    hierarchy = "unity/Assets/Official/Combat/SceneCharacterSources/PlayerModel_Game-source.json"
    raw = json.loads((ROOT / hierarchy).read_bytes())
    assert raw["nodes"][0]["game_object"]["source"]["manifest"] == "884_156692"
    proof = {
        "manifest": "884_156692",
        "operation": "Independently bind completed native character poses to original application renderer/part interfaces, preserving current atlas identity, prefab MeshRenderer fields, clipping/PMA, source-slot separators and material callbacks. Never sample or substitute animation, physics or mixing in the drawing endpoint.",
        "inputs": [identity(path) for path in [
            "assets/reference/battle-animation/scene-core-source-manifest.json",
            "assets/reference/battle-animation/scene-texture-source-manifest.json",
            "assets/reference/battle-animation/scene-separator-source-manifest.json",
            hierarchy]],
        "outputs": [identity(path) for path in [
            "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSceneRenderer.cs",
            "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSceneRendererSettings.cs",
            "unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.SceneRenderer.cs"]],
        "generator": identity("scripts/prepare_battle_scene_native_renderer.py"),
        "binding": {
            "catalog": "Combat/SceneCharacters/appearance-resources",
            "models": ["Combat/SceneCharacters/male", "Combat/SceneCharacters/female"],
            "pose": "ProfessionNativeSceneRenderer.State; consume completed poses without changing the caller transform",
            "appearance": "IProfessionAppearanceSceneRenderer / IProfessionAppearancePartRenderer",
            "parts": "Original DynamicSkeletonRenderSeparator with independent ProfessionNativeSceneMeshSeparator"},
        "native_ordering": "Internal SortingGroups retain source draw-slot order while original MeshRenderer fields stay intact. Even keys identify fragment starts; odd after-slot keys are reserved for future native scene part adapters. Current original part renderer layer/order must be uniform.",
        "limits": ["Normal-blend current character slots and uniform original part sorting only; unsupported prefab drawing fields, slot blends, divergent part sorting and unbound follower slot depth fail explicitly.",
            "The pose producer, full physics, mixing, original scene prefab assembly and production battle/Windows Player integration remain required."]}
    target = ROOT / "assets/reference/battle-animation/scene-native-renderer-source-manifest.json"
    content = json.dumps(proof, ensure_ascii=False, indent=2) + "\n"
    if not target.exists() or target.read_text(encoding="utf-8") != content:
        target.write_text(content, encoding="utf-8", newline="\n")
    print("Recorded native scene renderer source identities; no SDK implementation or art conversion performed.")


if __name__ == "__main__":
    main()
