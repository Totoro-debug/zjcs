"""Recover mountain UI from the selected APK/cloud objects, preserving native bindings."""
import argparse
import json

from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from map11_support_ui import load_support_ui
from prepare_unity_map12_sources import encode, meta, sha, source_path, write
from home_particle_schema import load as load_particle_schema


def prepare_context():
    apk = Apk.current()
    code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    schemas = ScriptSchemas(code_root=code)
    load_particle_schema(schemas)
    support = load_support_ui(apk, schemas, 12, code)
    extra_trees, extra_sources, extra_resources, _, _, _, _, exporter = support
    source, prefabs = dict(extra_trees), dict(extra_sources)
    for key in ("GoddessTouchDialog", "LostGoddessTouchDialog", "6010_12_DialogIcon",
                "6011_12_DialogIcon", "6620_12_DialogIcon", "WorldCanvas",
                "WorldFantasyLadderDialog", "6103_DialogIcon", "6302_DialogIcon",
                "BattleSteleWorldStageDialog", "6650_DialogIcon", "6655_DialogIcon", "ConfirmDialog"):
        bundle = ("prefabs_ui_map_world" if key == "WorldCanvas" else
                  "prefabs_ui_confirmdialog" if key == "ConfirmDialog" else "prefabs_ui_worldmap")
        native, _ = exporter.object_tree(key, bundle)
        nodes = native["nodes"]
        for node in nodes:
            node["children"] = []
        for node in nodes[1:]:
            nodes[node["parentIndex"]]["children"].append(node)
        source[key] = nodes[0]
        prefabs[key] = dict(native["source"], path=native["source_path"])
        if key in ("WorldFantasyLadderDialog", "6103_DialogIcon", "6302_DialogIcon",
                   "BattleSteleWorldStageDialog", "6650_DialogIcon", "6655_DialogIcon", "ConfirmDialog"):
            extra_trees[key] = nodes[0]
            extra_sources[key] = prefabs[key]
    base = ROOT / "unity/Assets/Official/World/Map/12/Source"
    path = base / "ui-prefabs.json"
    receipt_path = base / "ui-prefabs-source-manifest.json"
    raw = encode(source)
    manifest = dict(manifest=apk.version, operation="Decode original UI prefab hierarchy from selected native objects.",
        prefabs=prefabs, resources=dict(extra_resources, **exporter.resources), output_sha256=sha(raw),
        assemblies={name: dict(path=source_path(code / "managed/decoded" / name),
            sha256=sha((code / "managed/decoded" / name).read_bytes()))
            for name in ("Assembly-CSharp.dll", "Framework.dll", "Console.dll")})
    write(path, raw)
    write(receipt_path, encode(manifest))
    meta(path)
    meta(receipt_path)
    print(f"Map12 native UI source: {len(source)} prefab hierarchies", flush=True)
    return path, receipt_path, raw, manifest, source, apk, schemas, support


def build():
    from prepare_unity_map11_ui import build as restore
    restore(12)
    record_stele_ui_sources()


def record_stele_ui_sources():
    """Verify the converted source closure; this does not import or run Unity."""
    from prepare_unity_map10_entities import write_texture_meta
    ui_root = ROOT / "unity/Assets/Official/World/Map/12/UI"
    ui_path = ui_root / "map12-ui.json"
    data = json.loads(ui_path.read_bytes())
    ui_manifest = json.loads((ui_root / "source-manifest.json").read_bytes())
    keys = ("BattleSteleWorldStageDialog", "6650_DialogIcon", "6655_DialogIcon", "ConfirmDialog")
    if data["manifest"] != "871_156598" or data["mapClassId"] != 12 or len(data["panels"]) != 72:
        raise ValueError("Unexpected selected stele UI source closure")
    if sha(ui_path.read_bytes()) != ui_manifest["output"]["sha256"]:
        raise ValueError("Converted stele UI source differs from its receipt")
    if any(len([p for p in data["panels"] if p["key"] == key]) != 1 for key in keys):
        raise ValueError("An original stele prefab is missing or duplicated")
    steles_path = ROOT / "unity/Assets/Official/World/Map/12/Resources/Map12/steles.json"
    steles = json.loads(steles_path.read_bytes())
    if len(steles["Items"]) != 50 or not {r["ClassId"] for r in steles["Items"]}.issubset(r["itemId"] for r in data["itemIcons"]):
        raise ValueError("Stele preview item icon closure is incomplete")
    component = next(c for c in data["components"] if c["type"] == keys[0])
    fields = component["fields"]
    refs = {r["rid"]: r for r in fields["references"]["RefIds"]}
    if len(refs) != 3 or any(not (2**53 < rid <= 2**63-1) for rid in refs) or len(fields["mTeamStateSwitcher"]["mStateConfigs"]) != 2 or len(fields["mRankSuppressionSwitcher"]["mStateConfigs"]) != 1:
        raise ValueError("Original stele managed state closure changed")
    for switch in ("mTeamStateSwitcher", "mRankSuppressionSwitcher"):
        if any(r["rid"] not in refs for r in fields[switch]["mStateConfigs"]):
            raise ValueError("Original stele managed state ID is unbound")
    confirmation = [c for c in data["components"] if c["panel"] == "ConfirmDialog"]
    required = ("ConfirmDialog", "AutoAlignForSingleMultiLineText", "LockMaskWithTime", "ShowRemainTimeInfo")
    if any(sum(c["type"] == kind for c in confirmation) != 1 for kind in required):
        raise ValueError("Original confirmation component closure is incomplete")
    align = next(c["fields"] for c in confirmation if c["type"] == required[1])
    if (align["mSingleLineAlignment"], align["mMultiLineAlignment"]) != (514, 513):
        raise ValueError("Original confirmation text alignment changed")
    locked = next(c["fields"] for c in confirmation if c["type"] == required[2])
    timer = next(c["fields"] for c in confirmation if c["type"] == required[3])
    if locked["mLockTimeDefault"] != 10 or not timer["mRemainTimeText"]:
        raise ValueError("Original confirmation timer binding changed")
    lock_refs = {r["rid"]: r for r in locked["references"]["RefIds"]}
    lock_ids = locked["mLockedStateSwitcher"]["mStateConfigs"]
    if len(lock_ids) != 1 or lock_ids[0]["rid"] not in lock_refs:
        raise ValueError("Original confirmation lock state reference is unbound")
    sprites = []
    for row in data["sprites"]:
        path = ROOT / "unity" / row["unityAssetPath"]
        if sha(path.read_bytes()) != row["sha256"]:
            raise ValueError("Converted stele sprite differs: " + str(path))
        metadata = path.with_name(path.name + ".meta")
        if not metadata.exists():
            write_texture_meta(path)
            # Final native Sprite settings are applied by ImportSprites.
            # Keep stable source metadata until explicit native import.
            metadata.write_text("\n".join(line.rstrip() for line in metadata.read_text(encoding="utf8").splitlines()) + "\n", encoding="utf8", newline="\n")
        reward_ids = {r["ClassId"] for r in steles["Items"]}
        reward_sprites = {item[field] for item in data["itemIcons"] if item["itemId"] in reward_ids
                          for field in ("sprite", "qualitySprite")}
        if row["uri"] in reward_sprites or any(node.get("image_sprite_file") == row["uri"]
                for p in data["panels"] if p["key"] in keys for node in p["nodes"]):
            sprites.extend((path, metadata))
    record = lambda p: dict(path=source_path(p), sha256=sha(p.read_bytes()), bytes=p.stat().st_size)
    code = ROOT / ".tools/source-packages/current/871_156598/official-code"
    originals = [code / "client-source" / (name + ".cs") for name in
        ("BattleSteleWorldStageDialog", "WorldSteleStageInput", "ConfirmDialog", "AutoAlignForSingleMultiLineText")]
    originals += [code / "framework-source" / (name + ".cs") for name in ("StateMultiLanguagesConfig", "StateMultiLanguageData")]
    originals += [code / "client-source" / (name + ".cs") for name in ("LockMaskWithTime", "ShowRemainTimeInfo")]
    originals += [code / "framework-firstpass-source" / "UIBase.cs",
                  code / "framework-firstpass-source/Framework/CultureTimeSpanArg.cs",
                  code / "framework-firstpass-source/Framework/ETimeSpanFormatType.cs",
                  code / "common-source/Common/TimeManager.cs"]
    adapters = [ROOT / p for p in (
        "scripts/prepare_unity_map12_ui.py", "scripts/prepare_unity_map11_ui.py", "scripts/map11_support_ui.py",
        "unity/Assets/Game/Runtime/Official/WorldUI/StageDialog.cs", "unity/Assets/Game/Runtime/Official/WorldUI/WorldStateSwitcher.cs",
        "unity/Assets/Game/Runtime/Official/WorldUI/WorldBattleSteleStageUI.cs", "unity/Assets/Game/Runtime/Official/WorldUI/WorldStateMultiLanguagesConfig.cs",
        "unity/Assets/Game/Runtime/Presentation/World/OfficialMap11UiData.cs", "unity/Assets/Game/Editor/OfficialMap11UiTools.cs",
        "unity/Assets/Game/Editor/OfficialMap11UiTools.Stage.cs", "unity/Assets/Game/Editor/OfficialMap11UiTools.Steles.cs",
        "unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.SteleUi.cs",
        "unity/Assets/Game/Runtime/Presentation/World/OfficialMap12SteleRewardView.cs",
        "unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.ConfirmUi.cs",
        "unity/Assets/Game/Editor/OfficialMap11UiTools.Confirm.cs",
        "unity/Assets/Game/Runtime/Official/WorldUI/WorldInitializedUI.cs",
        "unity/Assets/Game/Runtime/Official/WorldUI/WorldAutoAlignForSingleMultiLineText.cs",
        "unity/Assets/Game/Runtime/Official/WorldUI/WorldConfirmDialog.cs",
        "unity/Assets/Game/Runtime/Official/WorldUI/WorldLockMaskWithTime.cs",
        "unity/Assets/Game/Runtime/Official/WorldUI/WorldShowRemainTimeInfo.cs",
        "unity/Assets/Game/Runtime/Official/WorldUI/WorldCultureTimeSpanArg.cs")]
    inputs = [steles_path, ROOT / "assets/reference/official-map/map12-steles-source-manifest.json",
              ui_root / "source-manifest.json", ROOT / "unity/Assets/Official/World/Map/12/Source/ui-prefabs-source-manifest.json"]
    receipt = dict(manifest=data["manifest"], mapClassId=12,
        operation="Decode four original prefab dependencies, preserve signed Int64 managed references and original UI methods, and retain all 50 original reward item icons. Native Windows assets are recorded separately.",
        inputs=[record(p) for p in inputs], originalCode=[record(p) for p in originals], adapters=[record(p) for p in adapters],
        prefabs={key: ui_manifest["source"]["prefabs"][key] for key in keys},
        outputs=[record(ui_path)] + [record(p) for p in sorted(set(sprites))],
        counts=dict(panels=len(data["panels"]), sprites=len(data["sprites"]), rewardItems=50, stateReferences=len(refs)),
        limitations=["This source receipt alone does not prove native Windows import or runtime behavior; native assets are recorded separately.",
            "NPC team binding, map window entry, item detail windows, hyperlink handling and UI audio/text effects remain pending.",
            "The two-size seven-placement simulated-input fixture verifies the original window, fixed reward icons and confirmation flow; actual map teams and battles require separate verification."])
    target = ROOT / "assets/reference/official-map/map12-stele-ui-source-manifest.json"
    write(target, encode(receipt))
    print("Map12 stele UI source closure verified: 72 panels, 50 reward item icons, three exact Int64 managed references.", flush=True)


def record_native_bindings():
    """Record the imported Windows assets after Unity has restored native bindings."""
    source = ROOT / "unity/Assets/Official/World/Map/12/UI"
    native = ROOT / "unity/Assets/Game/Resources/Map12/UI"
    data = json.loads((source / "map12-ui.json").read_bytes())
    if data["manifest"] != "871_156598" or data["mapClassId"] != 12:
        raise ValueError("Unexpected mountain UI source")
    for panel in data["panels"]:
        if not (native / (panel["key"] + ".prefab")).is_file():
            raise FileNotFoundError("Unity has not restored " + panel["key"])
    if (native / "catalog.json").read_bytes() != (source / "map12-ui.json").read_bytes():
        raise ValueError("Runtime UI catalog differs from selected source")
    inputs = [source / "source-manifest.json", source / "map12-ui.json"]
    inputs.append(ROOT / "unity/Assets/Official/World/Map/12/Resources/Map12/chests.json")
    inputs += [ROOT / name for name in (
        "scripts/prepare_unity_map12_ui.py", "scripts/prepare_unity_map11_ui.py",
        "scripts/map11_support_ui.py", "scripts/map11_exploration_ui.py", "scripts/map11_minimap_ui.py",
        "unity/Assets/Game/Editor/OfficialMap11UiTools.cs",
        "unity/Assets/Game/Editor/OfficialMap11UiTools.Support.cs",
        "unity/Assets/Game/Editor/OfficialMap11UiTools.Stage.cs",
        "unity/Assets/Game/Editor/OfficialMap12BuildingPreviewTools.cs",
        "unity/Assets/Game/Runtime/Presentation/World/NativeMap12BuildingGraphic.cs",
        "unity/Assets/Official/World/Map/12/Source/BuildingPreview/preview.json",
        "unity/Assets/Game/Editor/OfficialMap11UiTools.Animation.cs",
        "unity/Assets/Game/Editor/OfficialMap11UiTools.MiniMap.cs",
        "unity/Assets/Game/Editor/OfficialMap11UiTools.Sprites.cs")]
    outputs = sorted(path for path in native.rglob("*") if path.is_file())
    outputs += sorted(source.rglob("*.meta"))
    record = lambda path: dict(path=source_path(path), sha256=sha(path.read_bytes()), bytes=path.stat().st_size)
    receipt = dict(manifest=data["manifest"], mapClassId=12,
        operation="Restore original hierarchy, serialized UI bindings, JSZX layout, sprite import settings and animation curves as Windows Unity assets.",
        inputs=[record(path) for path in inputs], outputs=[record(path) for path in outputs],
        bindings=dict(resourceRoot="Map12/UI/", panels=[panel["key"] for panel in data["panels"]],
            sprites=len(data["sprites"])),
        limitations=["UI source/import is separate from full map gameplay, payments and save recovery.",
            "Native UIParticle references are retained in source; complete particle simulation and effects remain pending."])
    target = ROOT / "assets/reference/official-map/map12-ui-native-source-manifest.json"
    write(target, encode(receipt))
    print(f"Map12 native UI receipt: {len(outputs)} imported asset files", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--record-native-bindings", action="store_true")
    parser.add_argument("--record-stele-ui-sources", action="store_true")
    args = parser.parse_args()
    record_native_bindings() if args.record_native_bindings else record_stele_ui_sources() if args.record_stele_ui_sources else build()
