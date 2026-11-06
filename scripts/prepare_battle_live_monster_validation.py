"""Create an owned project for the full current monster skeleton/geometry path."""
import argparse
import json
from pathlib import Path
import shutil

from official_apk import ROOT
from validation_assemblies import stage_runtime_modules
from validation_workspace import validation_directory


def main(refresh=False):
    work = validation_directory()
    if not (work / ".zjcs-validation.json").is_file():
        raise ValueError("Owned monster validation workspace required")
    project = work / "live-monster-unity"
    if refresh:
        if not (project / "ProjectSettings/ProjectVersion.txt").is_file() or (project / "Temp/UnityLockfile").exists():
            raise ValueError("Refresh only an existing stopped owned monster project")
        for name in ("BattleNativeMonsterAssets", "BattleNativeMonsterRenderer", "BattleNativeMonsterRenderer.Animator", "ProfessionNativeSkeletonData.BattleMonster",
                     "ProfessionNativeSkeletonData", "ProfessionNativeOrderedConstraintPose"):
            source = ROOT / "unity/Assets/Game/Runtime/Offline/ProfessionUI" / (name + ".cs")
            for path in (source, Path(str(source) + ".meta")):
                shutil.copy2(path, project / "Assets/Game/Runtime/Offline/ProfessionUI" / path.name)
        for name in ("OfficialBattleMonsterDataValidation", "OfficialBattleMonsterRuntimeValidation", "OfficialBattleMonsterRuntimeValidation.Animator"):
            source = ROOT / "unity/Assets/Game/Editor/ProfessionUI" / (name + ".cs")
            for path in (source, Path(str(source) + ".meta")):
                shutil.copy2(path, project / "Assets/Editor" / path.name)
        shutil.copytree(ROOT / "unity/Assets/Official/Combat/NativeMonsters", project / "Assets/Official/Combat/NativeMonsters", dirs_exist_ok=True)
        print("Refreshed stopped native monster validation project:", project)
        return
    if project.exists():
        raise ValueError("Preserve the existing validation project")
    runtime = ROOT / "unity/Assets/Game/Runtime"
    pending = ['Zjcs.Offline.ProfessionUI']
    allowed = {"UniTask", "Unity.ugui", "UnityEngine.UI", "Unity.TextMeshPro", "Unity.Newtonsoft.Json", "Unity.RenderPipelines.Universal.Runtime"}
    selected = stage_runtime_modules(project, runtime, pending, allowed_external=allowed)
    shutil.copytree(ROOT / "unity/Assets/Official/Combat/NativeMonsters", project / "Assets/Official/Combat/NativeMonsters")
    editor = project / "Assets/Editor"
    editor.mkdir(parents=True)
    for name in ("OfficialBattleMonsterDataValidation", "OfficialBattleMonsterRuntimeValidation", "OfficialBattleMonsterRuntimeValidation.Animator"):
        shutil.copy2(ROOT / "unity/Assets/Game/Editor/ProfessionUI" / (name + ".cs"), editor)
    (editor / "Validation.asmdef").write_text(json.dumps(dict(name="MonsterNativeDataValidation", includePlatforms=["Editor"],
        references=["Zjcs.Offline.ProfessionUI", "Zjcs.Official.ProfessionUI", "Zjcs.Offline.Equipment",
                    "Unity.RenderPipelines.Core.Runtime", "Unity.RenderPipelines.Universal.Runtime"])), encoding="utf-8", newline="\n")
    packages = project / "Packages"
    packages.mkdir()
    shutil.copytree(ROOT / "unity/Packages/com.cysharp.unitask", packages / "com.cysharp.unitask")
    dependencies = {"com.cysharp.unitask": "file:com.cysharp.unitask", "com.unity.ugui": "1.0.0", "com.unity.textmeshpro": "3.0.6", "com.unity.nuget.newtonsoft-json": "3.2.1"}
    shutil.copytree(ROOT / "unity/Packages/com.unity.render-pipelines.universal", packages / "com.unity.render-pipelines.universal")
    dependencies["com.unity.render-pipelines.universal"] = "file:com.unity.render-pipelines.universal"
    for name in ("ui", "imgui", "animation", "audio", "jsonserialize", "imageconversion", "screencapture", "director", "assetbundle", "physics", "physics2d", "particlesystem"):
        dependencies["com.unity.modules." + name] = "1.0.0"
    (packages / "manifest.json").write_text(json.dumps(dict(dependencies=dependencies)), encoding="utf-8", newline="\n")
    settings = project / "ProjectSettings"
    settings.mkdir()
    for name in ("ProjectVersion.txt", "ProjectSettings.asset", "TagManager.asset"):
        shutil.copy2(ROOT / "unity/ProjectSettings" / name, settings / name)
    print("Prepared native monster validation:", project)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument("--refresh", action="store_true")
    main(parser.parse_args().refresh)
