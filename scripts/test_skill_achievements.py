"""Compile the skill-gift port against the pinned runtime data and test its official boundaries."""
import json
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
from test_profession_achievements import managed

ROOT = Path(__file__).resolve().parents[1]


def main():
    with tempfile.TemporaryDirectory(prefix="skill-achievement-tests-", dir=ROOT / ".tools") as temporary:
        work = Path(temporary)
        dotnet = shutil.which("dotnet")
        if not dotnet:
            raise FileNotFoundError("The installed .NET runtime was not found.")
        runtimes = subprocess.check_output([dotnet, "--list-runtimes"], text=True)
        candidates = re.findall(r"^Microsoft.NETCore.App (\S+) \[(.*?)\]$", runtimes, re.M)
        version, root = max(candidates, key=lambda pair: tuple(map(int, pair[0].split("."))))
        runtime = Path(root) / version
        editor = Path("D:/Tool/Unity/Editor/Data")
        output = work / "SkillAchievements.dll"
        arguments = [str(editor / "NetCoreRuntime/dotnet.exe"), str(editor / "DotNetSdkRoslyn/csc.dll"),
                     "/nologo", "/noconfig", "/nostdlib+", "/target:exe", "/out:" + str(output)]
        arguments.extend("/r:" + str(path) for path in sorted(runtime.glob("*.dll")) if managed(path))
        arguments.extend(str(ROOT / path) for path in [
            "unity/Assets/Game/Runtime/Offline/Skills/OfflineSkillProfile.cs",
            "unity/Assets/Game/Runtime/Offline/Skills/OfficialSkillAchievementRules.cs",
            "scripts/fixtures/skill_achievements_harness.cs"])
        subprocess.run(arguments, cwd=ROOT, check=True)
        output.with_suffix(".runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {
            "tfm": "net" + version.split(".")[0] + ".0", "framework": {"name": "Microsoft.NETCore.App", "version": version}}}), encoding="utf-8")
        subprocess.run([dotnet, str(output), str(ROOT / "unity/Assets/Official/Skills/skill-runtime-rules.json")], cwd=ROOT, check=True)


if __name__ == "__main__":
    main()
