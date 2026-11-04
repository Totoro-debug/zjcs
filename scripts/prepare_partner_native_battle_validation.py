"""Create an owned Unity project for original partner battle data and textures."""
import json
import shutil

from official_apk import ROOT
from prepare_partner_star_god_validation import copy_modules
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    if not (work / ".zjcs-validation.json").is_file():
        raise ValueError("Owned partner validation workspace required")
    project = work / "native-battle-unity"
    if project.exists():
        raise ValueError("Preserve the existing validation project")
    runtime = ROOT / "unity/Assets/Game/Runtime"
    modules = {json.loads(path.read_bytes())["name"]: path for path in runtime.rglob("*.asmdef")}
    pending, selected, external = ["Zjcs.Offline.ProfessionUI"], set(), set()
    while pending:
        name = pending.pop()
        if name in selected:
            continue
        if name not in modules:
            external.add(name)
            continue
        selected.add(name)
        pending.extend(json.loads(modules[name].read_bytes()).get("references", []))
    allowed = {"UniTask", "Unity.ugui", "UnityEngine.UI", "Unity.TextMeshPro", "Unity.Newtonsoft.Json",
               "Unity.RenderPipelines.Universal.Runtime"}
    if external - allowed:
        raise ValueError("Review partner battle validation dependencies: " + repr(external))
    copy_modules(project, runtime, modules, selected)
    editor = project / "Assets/Editor"
    editor.mkdir(parents=True)
    shutil.copy2(ROOT / "unity/Assets/Game/Editor/ProfessionUI/OfficialPartnerNativeBattleDataValidation.cs", editor)
    (editor / "Validation.asmdef").write_text(json.dumps(dict(name="PartnerNativeBattleDataValidation",
        includePlatforms=["Editor"], references=["Zjcs.Offline.ProfessionUI", "Zjcs.Official.ProfessionUI"])), encoding="utf-8", newline="\n")
    shutil.copytree(ROOT / "unity/Assets/Official/Combat/NativePartners",
                    project / "Assets/Official/Combat/NativePartners")
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
    print("Prepared original partner battle data with", len(selected), "runtime assemblies:", project)


if __name__ == "__main__":
    main()
