"""Stage the public original-container startup path without unrelated map art."""
import json
import argparse
import shutil

from official_apk import ROOT
from validation_assemblies import copy_modules, resolve_runtime_modules
from validation_workspace import validation_directory


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--refresh", action="store_true", help="Update only this owned, stopped project; preserve its cache")
    args = parser.parse_args()
    work = validation_directory()
    if not (work / ".zjcs-validation.json").is_file():
        raise ValueError("An owned validation workspace is required")
    project = work / "startup-unity"
    if project.exists() and not args.refresh:
        raise ValueError("Preserve the existing startup validation project")
    source = ROOT / "unity"
    runtime = source / "Assets/Game/Runtime"
    package_assemblies = {json.loads(p.read_bytes())["name"]
                          for folder in (source / "Packages", source / "Library/PackageCache")
                          for p in folder.rglob("*.asmdef")}
    package_assemblies.add("Unity.ugui")  # Built into this pinned Editor installation.
    fixture = source / "Assets/Game/Editor/MainlineWorldStartup"
    pending = json.loads((fixture / "Zjcs.Editor.MainlineWorldStartup.asmdef").read_bytes())["references"]
    modules, selected, external = resolve_runtime_modules(
        runtime, pending, excluded=package_assemblies | {"Unity.Newtonsoft.Json"})
    if external - {"spine-unity", "spine-csharp"}:
        raise ValueError("Review the original startup dependencies: " + repr(sorted(external)))
    if external:
        shutil.copytree(source / "Assets/Spine/Runtime", project / "Assets/Spine/Runtime", dirs_exist_ok=True)
    copy_modules(project, runtime, modules, selected)
    for relative in (
        "Assets/Official/World/Map/14/SharedConfig",
        "Assets/Official/World/Map/14/SharedRoot",
        "Assets/Official/World/Map/14/SharedRuntime",
        "Assets/Official/World/Map/14/SharedSerialization",
        "Assets/Official/World/Shared/SharedWorldDataCurrent",
        "Assets/Official/World/Shared/SharedWorldConfiguration",
        "Assets/Official/World/Shared/SharedClientConfiguration",
        "Assets/Official/World/Shared/SharedMapConfiguration",
        "Assets/Game/Editor/MainlineWorldStartup",
    ):
        shutil.copytree(source / relative, project / relative, dirs_exist_ok=True)
    relative = "Assets/StreamingAssets/PartnerNpcShared/Config/Binary/level_prop.g.bytes"
    target = project / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    for suffix in ("", ".meta"):
        shutil.copy2(source / (relative + suffix), project / (relative + suffix))
    relative = "Assets/Game/Resources/Map14/Native/shared-assembly-catalog.asset"
    target = project / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    for suffix in ("", ".meta"):
        shutil.copy2(source / (relative + suffix), project / (relative + suffix))
    shutil.copytree(source / "Packages", project / "Packages", dirs_exist_ok=True)
    settings = project / "ProjectSettings"
    settings.mkdir(exist_ok=True)
    for name in ("ProjectVersion.txt", "ProjectSettings.asset", "TagManager.asset"):
        shutil.copy2(source / "ProjectSettings" / name, settings / name)
    print("Prepared original startup:", len(selected), "runtime modules;", project, flush=True)


if __name__ == "__main__":
    main()
