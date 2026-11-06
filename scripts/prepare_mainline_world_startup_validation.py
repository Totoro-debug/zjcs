"""Stage the public original-container startup path without unrelated map art."""
import json
import argparse
import shutil
from pathlib import Path

from official_apk import ROOT
from prepare_partner_star_god_validation import copy_modules
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
    modules = {json.loads(p.read_bytes())["name"]: p for p in runtime.rglob("*.asmdef")}
    package_assemblies = {json.loads(p.read_bytes())["name"]
                          for folder in (source / "Packages", source / "Library/PackageCache")
                          for p in folder.rglob("*.asmdef")}
    package_assemblies.add("Unity.ugui")  # Built into this pinned Editor installation.
    fixture = source / "Assets/Game/Editor/MainlineWorldStartup"
    pending = json.loads((fixture / "Zjcs.Editor.MainlineWorldStartup.asmdef").read_bytes())["references"]
    selected = set()
    while pending:
        name = pending.pop()
        if name in selected:
            continue
        if name in package_assemblies or name == "Unity.Newtonsoft.Json":
            continue
        if name in ("spine-unity", "spine-csharp"):
            shutil.copytree(source / "Assets/Spine/Runtime", project / "Assets/Spine/Runtime", dirs_exist_ok=True)
            continue
        if name not in modules:
            raise ValueError("Review the original startup dependency: " + name)
        selected.add(name)
        pending.extend(json.loads(modules[name].read_bytes()).get("references", []))
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
