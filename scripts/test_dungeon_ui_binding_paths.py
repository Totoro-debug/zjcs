"""Replay dungeon prefab paths through the actual C# resolver without starting Unity."""
import json
from pathlib import Path
import re
import shutil
import subprocess
from validation_workspace import temporary_validation_directory
from test_profession_achievements import managed

ROOT = Path(__file__).resolve().parents[1]


def main():
    source = (ROOT / "unity/Assets/Game/Runtime/Presentation/World/OfficialDungeonUiBindingRuntime.cs").read_text(encoding="utf8")
    start = source.index("        private static Transform Find(")
    end = source.index("        private static Sprite LoadSprite(", start)
    fixture = (ROOT / "scripts/fixtures/dungeon_ui_binding_paths_harness.cs").read_text(encoding="utf8")
    dotnet = shutil.which("dotnet")
    if not dotnet:
        raise FileNotFoundError("The installed .NET runtime was not found.")
    runtimes = subprocess.check_output([dotnet, "--list-runtimes"], text=True)
    versions = re.findall(r"^Microsoft.NETCore.App (\S+) \[(.*?)\]$", runtimes, re.M)
    version, parent = max(versions, key=lambda pair: tuple(map(int, pair[0].split("."))))
    runtime = Path(parent) / version
    editor = Path("D:/Tool/Unity/Editor/Data")
    with temporary_validation_directory("dungeon-ui-path-tests-") as temporary:
        work = Path(temporary).resolve()
        harness = work / "Paths.cs"
        harness.write_text(fixture.replace("/* ACTUAL_FIND_METHOD */", source[start:end]), encoding="utf8")
        output = work / "Paths.dll"
        arguments = [str(editor / "NetCoreRuntime/dotnet.exe"), str(editor / "DotNetSdkRoslyn/csc.dll"),
            "/nologo", "/noconfig", "/nostdlib+", "/target:exe", "/out:" + str(output)]
        arguments.extend("/r:" + str(path) for path in sorted(runtime.glob("*.dll")) if managed(path))
        subprocess.run(arguments + [str(harness)], cwd=ROOT, check=True)
        output.with_suffix(".runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {
            "tfm": "net" + version.split(".")[0] + ".0", "framework": {"name": "Microsoft.NETCore.App", "version": version}}}), encoding="utf8")
        subprocess.run([dotnet, str(output), str(ROOT / "unity/Assets/StreamingAssets/Dungeons/UI/official-bindings.json")], cwd=ROOT, check=True)


if __name__ == "__main__":
    main()
