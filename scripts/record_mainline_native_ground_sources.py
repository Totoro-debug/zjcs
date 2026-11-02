"""Record extraction of the original bound labels/environment into a shared module."""
import json
import subprocess
from pathlib import Path
from prepare_mainline_map15_configurations import ROOT, PREDECESSOR, record, sha
from prepare_unity_map13_sources import encode, write


def build():
    previous_path = "unity/Assets/Game/Runtime/Presentation/World/NativeMap14/OfficialMap14NativeWorldView.cs"
    previous = subprocess.check_output(["git", "show", PREDECESSOR + ":" + previous_path], cwd=ROOT)
    module = ROOT / "unity/Assets/Game/Runtime/Presentation/World/MainlineNativeGround"
    outputs = sorted(p for p in module.iterdir() if p.is_file()) + [Path(str(module) + ".meta")]
    outputs += [ROOT / p for p in [previous_path,
        "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineStatuesModule.Goddess.cs",
        "scripts/prepare_mainline_ground_validation.py",
        "unity/Assets/Game/Runtime/Presentation/World/NativeMap14/Zjcs.Presentation.World.NativeMap14.asmdef",
        "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.NativeMap14.cs",
        "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.NativeGround.cs",
        "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.NativeGround.cs.meta",
        "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.PlayerView.cs",
        "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.cs",
        "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapProfile.cs",
        "unity/Assets/Game/Runtime/Presentation/World/Zjcs.Presentation.World.asmdef",
        "unity/Assets/Game/Runtime/Offline/World/NativeMap14Labels/Map14LabelBinding.cs",
        "unity/Assets/Game/Runtime/Official/World/NativeMap14Labels/Map14LabelData.cs",
        "unity/Assets/Game/Runtime/Official/World/NativeMap14Labels/Map14LabelServices.cs",
        "unity/Assets/Game/Editor/Map14/WorldHost/OfficialMap14NativeHostAcceptance.cs",
        "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSkeletonData.World.cs",
        "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSkeletonData.World.cs.meta",
        "unity/Assets/Game/Editor/Map14/WorldHost/Zjcs.Editor.Map14.WorldHost.asmdef"]]
    fixture = ROOT / "unity/Assets/Game/Editor/MainlineNativeGround"
    outputs += sorted(p for p in fixture.iterdir() if p.is_file()) + [Path(str(fixture) + ".meta")]
    water_receipt = ROOT / "assets/reference/official-map/map13-native-water-code-source-manifest.json"
    water = json.loads(water_receipt.read_bytes())
    current_code = ROOT / ".tools/source-packages/current/884_156692/code-manifest.json"
    selected = json.loads(current_code.read_bytes())
    assembly = ROOT / ".tools/source-packages/current/884_156692/official-code/managed/decoded/CustomWater.dll"
    selected_water = selected["assemblies"]["CustomWater.dll"]
    if selected["manifest"] != "884_156692" or sha(assembly.read_bytes()) != water["assembly"]["sha256"]:
        raise ValueError("Current water assembly differs from the original migrated assembly")
    if selected_water["decoded_sha256"] != water["assembly"]["sha256"]:
        raise ValueError("Current prepared water assembly differs from its extraction receipt")
    for row in water["outputs"]:
        if sha((ROOT / row["path"]).read_bytes().replace(b"\r\n", b"\n")) != row["sha256"]:
            raise ValueError("Installed original water component changed: " + row["path"])
    official_path = ROOT / ".tools/source-packages/official-code/common-source/Common/MapRegionInfo.cs"
    proof = dict(format=1, operation="Extract the existing original-component binding into one shared labels/environment module. Preserve bind-before-Awake, original status/progress events, region data and renderer pipeline ownership. Partitioned Map14 reuses the same services; non-partitioned prefabs use MainlineNativeGround. Map15's confirmed local unlocked-map state supplies the time-reached port; the original time-before-level branch is retained. Unbound timed/rank scopes fail closed; no server dates are invented.",
        predecessor=dict(commit=PREDECESSOR, path=previous_path, sha256=sha(previous)),
        original_label_receipt=record(ROOT / "assets/reference/official-map/map14-label-components-code-source-manifest.json"),
        original_region_limit_code=record(official_path),
        original_view_receipt=record(ROOT / "assets/reference/official-map/map15-world-view-source-manifest.json"),
        current_water_equivalence=dict(manifest=selected["manifest"], assembly=record(assembly),
            code_manifest=record(current_code), original_migration_receipt=record(water_receipt),
            extraction=selected_water, installed_outputs=water["outputs"],
            scope="CustomWater.dll is byte-identical across these two recorded inputs; the six installed migration outputs still match. This is not whole-client version equivalence."),
        hashing="UTF-8 bytes normalized to LF for installed code; original input receipts retain their own hash conventions.",
        outputs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p.read_bytes().replace(b"\r\n", b"\n"))) for p in outputs],
        tool=record(Path(__file__)))
    write(ROOT / "assets/reference/official-map/mainline-native-ground-source-manifest.json", encode(proof))
    print("Recorded shared original labels/environment:", len(outputs), "outputs")


if __name__ == "__main__":
    build()
