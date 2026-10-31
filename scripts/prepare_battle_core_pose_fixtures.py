"""Choose combat pose boundaries directly from verified original player data."""
import json
from pathlib import Path

from prepare_battle_constraint_channel_fixtures import ROOT, sha, skeleton
from validation_workspace import validation_directory


def times(value):
    if isinstance(value, dict):
        for key, child in value.items():
            if key == "time":
                yield child
            else:
                yield from times(child)
    elif isinstance(value, list):
        for child in value:
            yield from times(child)


def build():
    receipt = ROOT / "assets/reference/profession-ui/skeleton-data-source-manifest.json"
    original = json.loads(receipt.read_bytes())
    if original["manifest"] != "871_156598":
        raise ValueError("Original player source version differs")
    bodies, inputs = [], []
    for output in original["outputs"]:
        path = ROOT / output["file"]["path"]
        raw = path.read_bytes()
        if sha(raw) != output["file"]["sha256"]:
            raise ValueError("Original lossless source changed")
        inputs.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(raw)))
        data = skeleton(raw)
        for prefix, profession, capture in (("100", 1, ("100/skill_06", "100/skill_50")), ("200", 10, ("200/skill_06", "200/skill_13"))):
            cases = []
            for name, fields in data["animations"].items():
                if not name.startswith(prefix + "/"):
                    continue
                duration = max(times(fields), default=0)
                selected = {0, duration * .37, duration * .73, duration}
                for kind in ("ik", "transform", "physics"):
                    selected.update(times(fields.get(kind, {})))
                cases.append(dict(animation=name, duration=duration, times=sorted(selected)))
            assert all(name in data["animations"] for name in capture)
            bodies.append(dict(sex=output["sex"], prefix=prefix, profession=profession, cases=cases, capture=list(capture)))
    payload = (json.dumps(dict(bodies=bodies), ensure_ascii=False, separators=(",", ":")) + "\n").encode()
    (validation_directory() / "core-pose-cases.json").write_bytes(payload)
    dependencies = (
        "assets/reference/profession-ui/character-resources-source-manifest.json",
        "assets/reference/profession-ui/appearance-spine-source-manifest.json",
        "assets/reference/profession-ui/skin-defaults-source-manifest.json",
        "assets/reference/profession-ui/character-textures-source-manifest.json",
        "unity/Assets/Game/Resources/ProfessionUI/character-resources.asset",
        "unity/Assets/Game/Resources/ProfessionUI/character-textures.asset",
        "unity/Assets/Game/Resources/ProfessionUI/appearance-tables.json",
        "unity/Assets/Game/Resources/ProfessionUI/skin-defaults.json",
        "unity/Assets/Official/ProfessionUI/Portrait/AppearanceConfig.asset",
        "unity/Assets/Official/ProfessionUI/Portrait/AppearanceAnimNameCombineRule.asset")
    implementation = (
        "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeConstraintPose.cs",
        "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeAppearanceSkeleton.cs",
        "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeAnimationChannels.cs",
        "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeBonePose.cs",
        "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeIdleIk.cs",
        "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeIdleTransform.cs",
        "unity/Assets/Game/Runtime/Official/World/NativeMap13Entities/Shaders/SpineSkeletonGraphicDye_a2f0973c.shader",
        "unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.CorePoses.cs",
        "unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.CorePoses.cs.meta",
        "unity/Assets/Game/Editor/Combat/AnimatorPlayback/Zjcs.Editor.Combat.AnimatorPlayback.asmdef")
    proof = dict(manifest=original["manifest"], inputs=inputs,
        source_receipt=dict(path=receipt.relative_to(ROOT).as_posix(), sha256=sha(receipt.read_bytes())),
        dependencies=[dict(path=path, sha256=sha((ROOT / path).read_bytes())) for path in dependencies],
        implementation=[dict(path=path, sha256=sha((ROOT / path).read_bytes())) for path in implementation],
        generator_sha256=sha(Path(__file__).read_bytes()), fixture_sha256=sha(payload),
        profiles=[dict(sex=body["sex"], prefix=body["prefix"], profession=body["profession"], animations=len(body["cases"]), samples=sum(len(row["times"]) for row in body["cases"])) for body in bodies],
        operation="Choose all original 100/ and 200/ combat actions, original IK/transform/physics key boundaries and three interior/end samples. The Play Mode consumer resolves the original complete core appearance via BuildDefaultAppearanceData, ProfessionAppearancePreview with Game append skins, AppearanceCharacter and AppearanceSpine using packaged character resources; no guessed skin names/colors or animation SDK.",
        limitations=["Pre-physics, unmixed core appearance diagnostics; no physical simulation, separate weapon/part rendering, production actor binding, game input/save flow or Windows Player release."])
    (ROOT / "assets/reference/battle-animation/core-pose-source-manifest.json").write_text(json.dumps(proof, ensure_ascii=False, indent=2) + "\n", "utf8", newline="\n")
    print(proof["profiles"])


if __name__ == "__main__":
    build()
