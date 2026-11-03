"""Record the common visibility extraction without changing original source receipts."""
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
PREDECESSOR = "f827eab75e6404731d5fb73a97e8fbb427e46797"
OLD = "unity/Assets/Game/Runtime/Offline/World/NativeMap13GridVisibility/OfflineMap13GridVisibility.cs"
NEW = "unity/Assets/Game/Runtime/Offline/World/MapGridVisibility"


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def record(relative):
    return {"path": relative, "sha256": sha((ROOT / relative).read_bytes().replace(b"\r\n", b"\n"))}


def map_binding(map_id, visibility, bodies):
    definition = f"unity/Assets/Official/World/Map/{map_id}/map{map_id}.json"
    ground = json.loads((ROOT / definition).read_bytes())
    config = json.loads((ROOT / visibility).read_bytes())
    body = json.loads((ROOT / bodies).read_bytes())
    nav = ground["Navigation"]
    if config["mapClassId"] != map_id or config["size"] != [nav["Width"], nav["Height"]]:
        raise ValueError("Visibility dimensions differ from active map: " + str(map_id))
    placed = {row["Id"]: row for row in ground["Entities"]}
    if len(placed) != len(body["entities"]):
        raise ValueError("Visibility placement count differs from active map: " + str(map_id))
    for row in body["entities"]:
        target = placed[row["placementId"]]
        pos = row["transform"]["Pos"]
        if target["EntityId"] != row["classId"] or target["Grid"] != [pos["X"], pos["Y"]]:
            raise ValueError("Visibility placement differs from active map: " + str(row["placementId"]))
    return dict(mapClassId=map_id, definition=record(definition), visibility=record(visibility), bodies=record(bodies),
        checked_placements=len(placed), method="Compare every original placement id, class and grid coordinate, and active ground dimensions. Input versions retain their separate receipts; no blanket version equivalence is asserted.")


def build():
    # A Git predecessor records our offline implementation's origin. It is not
    # represented as an official client/server source.
    previous = subprocess.check_output(["git", "show", PREDECESSOR + ":" + OLD], cwd=ROOT)
    official = "assets/reference/official-map/map13-grid-visibility-source-manifest.json"
    outputs = sorted(p.relative_to(ROOT).as_posix() for p in (ROOT / NEW).iterdir() if p.is_file())
    outputs += [NEW + ".meta",
        "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.NativeMap13.cs",
        "unity/Assets/Game/Runtime/Presentation/World/Zjcs.Presentation.World.asmdef",
        "unity/Assets/Game/Editor/Map13GridVisibility/OfficialMap13LocalVisibilityAcceptance.cs",
        "unity/Assets/Game/Editor/Map13GridVisibility/Zjcs.Editor.Map13.GridVisibility.asmdef",
        "unity/Assets/Game/Editor/Map13GridVisibility/SharedMapGridVisibilityAcceptance.cs",
        "unity/Assets/Game/Editor/Map13GridVisibility/SharedMapGridVisibilityAcceptance.cs.meta"]
    outputs += ["unity/Assets/Game/Runtime/Presentation/World/Mainline/" + name for name in (
        "OfficialMainlineMapRuntime.GridVisibility.cs", "OfficialMainlineMapRuntime.GridVisibility.cs.meta",
        "OfficialMainlineMapProfile.cs", "OfficialMainlineMapRuntime.cs",
        "OfficialMainlineInteractionCatalog.cs",
        "OfficialMainlineMiniMapModule.HudControls.cs",
        "OfficialMainlineMapRuntime.Persistence.cs", "OfficialMainlineMapRuntime.Puzzles.cs", "OfficialMainlineMapRuntime.Interactions.cs",
        "OfficialMainlineMapRuntime.Map13Trees.cs", "OfficialMainlineStatuesModule.Goddess.cs")]
    outputs += ["unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.NativeMap12.cs",
        "unity/Assets/Game/Runtime/Presentation/World/NativeMap12/OfficialMap12NativeWorldAssets.cs",
        "unity/Assets/Game/Runtime/Presentation/World/NativeMap12/Map12WorldEnvironment.cs"]
    outputs += ["unity/Assets/Game/Runtime/Presentation/World/OfficialMap10RuntimeData.cs",
        "unity/Assets/Game/Editor/Map13Mainline/OfficialMap13MainlinePlayAcceptance.cs",
        "unity/Assets/Game/Editor/Map13Mainline/Zjcs.Editor.Map13Mainline.asmdef"]
    outputs += ["unity/Assets/Game/Editor/Map13MiniMap/" + name for name in (
        "OfficialMap13MiniMapMarkDataAcceptance.cs", "OfficialMap13MiniMapMarkManagersAcceptance.cs",
        "OfficialMap13MiniMapMarkSpawnAcceptance.cs", "OfficialMap13MiniMapMarkSupportsAcceptance.cs",
        "OfficialMap13MiniMapMasksAcceptance.cs", "Zjcs.Editor.Map13.MiniMap.asmdef")]
    outputs += ["unity/Assets/Game/Runtime/Presentation/World/Mainline/" + name for name in (
        "MainlineMapLocalSaveFile.cs", "MainlineMapLocalSaveFile.cs.meta",
        "MainlineMapEntityPresentation.cs", "MainlineMapEntityPresentation.cs.meta",
        "MainlineMapEntityRestore.cs", "MainlineMapEntityRestore.cs.meta",
        "MainlineMapMiniMapSnapshots.cs", "MainlineMapMiniMapSnapshots.cs.meta",
        "OfficialMainlineMapRuntime.Map12Chests.cs", "OfficialMainlineMapRuntime.Map12Steles.cs",
        "OfficialMainlineRegionsModule.Regions.cs", "OfficialMainlineStatuesModule.HeroStatues.cs",
        "OfficialMainlineStatuesModule.Witch.cs", "OfficialMainlineExplorationModule.Exploration.cs")]
    outputs += ["unity/Assets/Game/Runtime/Presentation/World/OfficialMap10EntityView.cs",
        "unity/Assets/Game/Runtime/Offline/Progression/OfflineMapStaminaService.cs"]
    outputs = list(dict.fromkeys(outputs))
    result = {
        "format": 1,
        "operation": "Extract the existing local visibility implementation into one configuration-driven module. Preserve the migrated original component, body/camp queries, event ordering, RLE and legacy Map13 save field. Bind Map11, Map12, Map13 and Map15 through the same placement-checked loader, snapshot lifecycle; Map15 preserves its original absent fog. Map12 forwards the original region-light unlock callback to the same module. Other maps use the generic gridVisibility save field. Restore also rebinds live fog after a rollback. Keep original Map13-specific source assertions in its loader. Validate local snapshot runs before the original recovering decoder.",
        "official_source_receipt": record(official),
        "official_region_unlock": record("unity/Assets/Game/Runtime/Official/World/NativeEnvironment/MapEnvEffect.cs"),
        "offline_predecessor": {"commit": PREDECESSOR, "path": OLD, "sha256": sha(previous)},
        "active_map_bindings": [map_binding(11,
            "unity/Assets/Official/World/Mainline/Visibility/Resources/World/Mainline/Visibility/Map11/visibility.json",
            "unity/Assets/Official/World/Mainline/Visibility/Resources/World/Mainline/Visibility/Map11/bodies.json"),
            *[map_binding(n,
                f"unity/Assets/Official/World/Mainline/Visibility/Resources/World/Mainline/Visibility/Map{n}/visibility.json",
                f"unity/Assets/Official/World/Mainline/Visibility/Resources/World/Mainline/Visibility/Map{n}/bodies.json") for n in (12, 15)],
            map_binding(13, "unity/Assets/Official/World/Map/13/Resources/Map13/grid-visibility.json",
                "unity/Assets/Official/World/Map/13/Resources/Map13/grid-entities.json")],
        "unchanged_official_implementation": [record(p.relative_to(ROOT).as_posix()) for p in sorted(
            (ROOT / "unity/Assets/Game/Runtime/Official/World/Map13GridVisibility").glob("*.cs"))],
        "text_hashing": "UTF-8 file bytes with CRLF normalized to LF; original source receipts retain their own hash conventions.",
        "official_minimap_source_receipt": record("assets/reference/official-map/mainline-minimap-root-source-manifest.json"),
        "local_minimap_snapshot": "Read visited maps through MainlineMapMiniMapSnapshots.Read without mounting scene objects or committing profile/map state. Share the active host's map-file validation, backup recovery, entity catalog parser, puzzle dependency closure, Map12 completion recovery, region point calculation and entity presentation projection. Inspect fresh profile data on every read; preserve original fog bytes, public regions and recorded exploration. Reuse existing hero receipts, Map11 witch state, Map13 water restoration and generated exploration entity templates. Availability and expiry use one injected local time per read. Missing world files or visibility snapshots, unvisited maps and corrupt worlds cannot reveal fallback data. Native fullscreen host binding remains outside this receipt's implementation scope.",
        "local_entity_exploration": "Confirmed offline minimap selection consumes ever-discovered entity placement IDs. The local visibility adapter records active entities at their current revealed anchor cells, retains IDs after movement/inactivation/completion, returns defensive sorted snapshots and rejects duplicate/foreign IDs. Preserve original default positions for camp calculations. Persist IDs in each map's existing local file alongside the unchanged original RLE; absent legacy history is reconstructed only from currently revealed active entities. The formal adapter reads actual world-object activation, rather than the placement default, and captures newly enabled entities before subsequent interactions. Map12 puzzle and Map13 water-entity movement update the original visibility bodies; restore puzzle state before rebuilding visibility on failed persistence.",
        "outputs": [record(p) for p in outputs],
        "tool": record(Path(__file__).relative_to(ROOT).as_posix()),
    }
    path = ROOT / "assets/reference/official-map/mainline-grid-visibility-source-manifest.json"
    path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("Recorded common grid visibility extraction:", len(outputs), "outputs")


if __name__ == "__main__":
    build()
