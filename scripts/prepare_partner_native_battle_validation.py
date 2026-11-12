"""Create an owned Unity project for original partner battle data and textures."""
import json
from pathlib import Path
import re
import shutil

from official_apk import ROOT
from validation_assemblies import stage_runtime_modules
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    if not (work / ".zjcs-validation.json").is_file():
        raise ValueError("Owned partner validation workspace required")
    project = work / "native-battle-unity"
    if project.exists():
        raise ValueError("Preserve the existing validation project")
    runtime = ROOT / "unity/Assets/Game/Runtime"
    pending = ['Zjcs.Offline.ProfessionUI']
    allowed = {"UniTask", "Unity.ugui", "UnityEngine.UI", "Unity.TextMeshPro", "Unity.Newtonsoft.Json",
               "Unity.RenderPipelines.Universal.Runtime"}
    selected = stage_runtime_modules(project, runtime, pending, allowed_external=allowed)
    editor = project / "Assets/Editor"
    editor.mkdir(parents=True)
    shutil.copy2(ROOT / "unity/Assets/Game/Editor/ProfessionUI/OfficialPartnerNativeBattleDataValidation.cs", editor)
    gpu_validation = ROOT / "unity/Assets/Game/Editor/ProfessionUI/OfficialPartnerNativeBattlePoseAcceptance.cs"
    if gpu_validation.is_file():
        shutil.copy2(gpu_validation, editor)
    animator_validation = ROOT / "unity/Assets/Game/Editor/ProfessionUI/OfficialPartnerNativeBattleAnimatorAcceptance.cs"
    if animator_validation.is_file():
        shutil.copy2(animator_validation, editor)
    runtime_validation = ROOT / "unity/Assets/Game/Editor/ProfessionUI/OfficialPartnerNativeBattleRuntimeAcceptance.cs"
    if runtime_validation.is_file():
        shutil.copy2(runtime_validation, editor)
    (editor / "Validation.asmdef").write_text(json.dumps(dict(name="PartnerNativeBattleDataValidation",
        includePlatforms=["Editor"], references=["Zjcs.Offline.ProfessionUI", "Zjcs.Official.ProfessionUI",
            "Zjcs.Offline.Equipment", "Unity.RenderPipelines.Universal.Runtime"])), encoding="utf-8", newline="\n")
    shutil.copytree(ROOT / "unity/Assets/Official/Combat/NativePartners",
                    project / "Assets/Official/Combat/NativePartners")
    animators = ROOT / "unity/Assets/Official/Combat/NativePartnerAnimators"
    if animators.is_dir():
        shutil.copytree(animators, project / "Assets/Official/Combat/NativePartnerAnimators")
    native_assets = ROOT / "unity/Assets/Official/Combat/NativePartnerAssets"
    if native_assets.is_dir():
        shutil.copytree(native_assets, project / "Assets/Official/Combat/NativePartnerAssets")
    shutil.copytree(ROOT / "unity/Assets/Official/Combat/NativePartnerAppearance",
                    project / "Assets/Official/Combat/NativePartnerAppearance")
    for relative in (
        "Assets/Official/Combat/SceneCharacterAppearance/Resources/Combat/SceneAppearance/AppearanceConfig.asset",
        "Assets/Official/ProfessionUI/WeaponTextureLoader/appearance-asset-manager-config.asset",
    ):
        target = project / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        for suffix in ("", ".meta"):
            shutil.copy2(ROOT / "unity" / (relative + suffix), Path(str(target) + suffix))
    shader_cache = ROOT / ".tools/source-packages/current/884_156692/native-conversion/all-assets/windows-shaders"
    shader_receipt = json.loads((shader_cache / "source-manifest.json").read_bytes())
    shader, = [row for row in shader_receipt["shaders"] if row["source"]["object_sha256"] ==
               "4aa96ea56babfdc788805e559c0c1d94e1d64b7ed36719d12e61f6a08bb850ae"]
    from hashlib import sha256
    shader_path = shader_cache / shader["path"]
    if sha256(shader_path.read_bytes()).hexdigest() != shader["sha256"] or sha256(
            Path(str(shader_path) + ".meta").read_bytes()).hexdigest() != shader["meta_sha256"]:
        raise ValueError("Current original partner battle shader conversion changed")
    shader_destination = project / "Assets/PartnerNativeBattleValidation/OriginalSkeletonEffect.shader"
    shader_destination.parent.mkdir()
    shutil.copy2(shader_path, shader_destination)
    # The installed catalog keeps the exact converted shader GUID. Give this
    # path-loaded fixture copy its own identity so Unity never rewrites either
    # reference due to a duplicate GUID in the isolated project.
    metadata = Path(str(shader_path) + ".meta").read_text("utf-8")
    fixture_guid = sha256(b"zjcs:partner-native-battle:fixture-shader").hexdigest()[:32]
    Path(str(shader_destination) + ".meta").write_text(re.sub(r"(?m)^guid: [0-9a-f]{32}$",
        "guid: " + fixture_guid, metadata), encoding="utf-8", newline="\n")
    packages = project / "Packages"
    packages.mkdir()
    shutil.copytree(ROOT / "unity/Packages/com.cysharp.unitask", packages / "com.cysharp.unitask")
    dependencies = {"com.cysharp.unitask": "file:com.cysharp.unitask", "com.unity.ugui": "1.0.0",
        "com.unity.textmeshpro": "3.0.6", "com.unity.nuget.newtonsoft-json": "3.2.1"}
    shutil.copytree(ROOT / "unity/Packages/com.unity.render-pipelines.universal", packages / "com.unity.render-pipelines.universal")
    dependencies["com.unity.render-pipelines.universal"] = "file:com.unity.render-pipelines.universal"
    for name in ("ui", "imgui", "animation", "audio", "jsonserialize", "imageconversion", "screencapture",
                 "director", "assetbundle", "physics", "physics2d", "particlesystem"):
        dependencies["com.unity.modules." + name] = "1.0.0"
    (packages / "manifest.json").write_text(json.dumps(dict(dependencies=dependencies)), encoding="utf-8", newline="\n")
    settings = project / "ProjectSettings"
    settings.mkdir()
    shutil.copy2(ROOT / "unity/ProjectSettings/ProjectVersion.txt", settings)
    shutil.copy2(ROOT / "unity/ProjectSettings/ProjectSettings.asset", settings)
    shutil.copy2(ROOT / "unity/ProjectSettings/TagManager.asset", settings)
    print("Prepared original partner battle data with", len(selected), "runtime assemblies:", project)


if __name__ == "__main__":
    main()
