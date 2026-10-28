"""Record exact official inputs and scoped C# profession ports."""
import hashlib
import json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
CODE = ROOT / ".tools/source-packages/official-code"
PORTS = {
    "ImageLocator": ("ImageLocator", "Sprite native size and pivot correction."),
    "ProfessionOverviewPanel": ("ProfessionOverviewPanel", "Original tab state order, source image selection, property row pool and alternating backgrounds; offline data/resource/window services are injected."),
    "ProfessionPropRadar": ("ProfessionPropRadar", "Original radar slot order, nonlinear function, property row cache and settings color."),
    "ProfessionPropRadarDrawer": ("ProfessionPropRadarDrawer", "Original Graphic mesh vertices, colors and triangles."),
    "PlayerPropItem": ("ProfessionPlayerPropItem", "String value presentation lifecycle, preview visibility, delayed text effect and background/line visibility. General typed/colorful values and blessing paths remain required."),
    "ProfessionTreePanel": ("ProfessionTreePanel", "Original item initialization, total profession level and highest-rank state; player events are supplied by the local adapter."),
    "ProfessionTreePanelItem": ("ProfessionTreePanelItem", "Original five statuses, update order, native condition-prefab creation, grouping and tutor routing; appearance, map, tip and detail services are injected."),
    "RuleDialog": ("RuleDialog", "All original overloads, ordered row reuse/activation, source UIUtils transform reset; original RuleTipInfoParser data is injected."),
    "TipInfo": ("TipInfo", "Original image visibility and localized key/arguments order."),
    "PanelTitle": ("PanelTitle", "Original title key setter and serialized text reference."),
    "AchievementPanel": ("AchievementPanel", "Original panel entry scoped to ProfessionTotalLevel type 56 and empty parameter; other achievement entry paths are not ported here."),
    "AchievementPanelBase": ("AchievementPanelBase", "Group 10, original state filters, score/index sorting, title and original five row instances; other ranking/achievement types are not ported here."),
    "AchievementPanelItem": ("AchievementPanelItem", "Original refresh order, progress, state, reward-icon borrowing and group claim checks; local icon/function/player/RPC services are injected."),
    "FreedomChangeProfessionBtn": ("FreedomChangeProfessionBtn", "Original function lock key, timer/state branches, tutor image offset and click validation order; local clock/function/map services are injected."),
    "ShowRemainTimeInfo": ("ShowRemainTimeInfo", "Original timer comparisons, refresh interval, callback and text visibility; local clock/culture services replace original singletons and UIBase initialization."),
    "CloseWindowBtn": ("CloseWindowBtn", "Original parent/specified/last close target and destroyed-window subscription; local window service is injected."),
    "HyperLinkClickListener": ("HyperLinkClickListener", "Original TMP link hit, numeric provider selection, parameter parsing and click dispatch. Provider supplies original navigation context; optional UITouchPass component routing is not installed here."),
    "StoryNodeRegister": ("StoryNodeRegister", "Original registration order, missing-node reporting and unregister loop; local story registry is injected."),
    "UIBloomElement": ("UIBloomElement", "Original serialized renderer metadata without lifecycle methods."),
    "UIBloomElementOrder": ("UIBloomElementOrder", "Original serialized renderer order metadata."),
    "UIBloomGraphicElement": ("UIBloomGraphicElement", "Original graphic/material/stencil references; bloom renderer is not ported by this metadata holder."),
    "UIBloomPlayerElement": ("UIBloomPlayerElement", "Original appearance/weapon references retained as Components pending native appearance type binding; bloom renderer is not ported here."),
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def file_record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path))


def build():
    ports = []
    for official, (runtime, scope) in PORTS.items():
        source = CODE / "client-source" / (official + ".cs")
        target = ROOT / "unity/Assets/Game/Runtime/Official/ProfessionUI" / (runtime + ".cs")
        ports.append(dict(official=source.relative_to(CODE).as_posix(), official_sha256=sha(source),
            runtime=target.relative_to(ROOT).as_posix(), runtime_sha256=sha(target),
            meta_sha256=sha(target.with_name(target.name + ".meta")), scope=scope))
    achievement_sources = ["common-source/Common/" + name + ".cs" for name in (
        "AchievementRawInfoParser", "CycleTemplateInfo", "AchievementInfo", "AchievementUtils", "Player")]
    achievement_sources += ["client-source/AchievementPanelItem.cs", "common-source/PlayerServiceProxyImpl.cs"]
    result = dict(format=1, subject="profession UI and total-level achievement", official_code_root=CODE.relative_to(ROOT).as_posix(),
        ports=ports,
        achievement=dict(source_code={name: sha(CODE / name) for name in achievement_sources},
            rules=file_record(ROOT / "assets/official/rules.json"),
            runtime=file_record(ROOT / "unity/Assets/Game/Runtime/Offline/Progression/OfficialProfessionAchievementRules.cs"),
            fixtures=file_record(ROOT / "scripts/fixtures/profession_achievements_harness.cs")),
        local_service=dict(runtime=file_record(ROOT / "unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterProgressionService.cs"),
            scope="Profession level reads, group-10 reward transaction and source-based non-mutating profession appearance preview only.",
            appearance_sources={name: sha(CODE / "client-source" / name) for name in (
                "ProfessionUtils.cs", "AppearanceSystem.Runtime/AppearanceUtils.cs")}),
        tipdot=file_record(ROOT / "assets/reference/profession-ui/tipdot-source-manifest.json"),
        shaders=file_record(ROOT / "assets/reference/profession-ui/shader-bindings-source-manifest.json"),
        dynamic_resources=file_record(ROOT / "assets/reference/profession-ui/dynamic-resources-source-manifest.json"),
        audio=file_record(ROOT / "assets/reference/profession-ui/audio-source-manifest.json"),
        windows_audio=file_record(ROOT / "assets/reference/profession-ui/windows-audio-source-manifest.json"),
        image_resources=file_record(ROOT / "assets/reference/profession-ui/image-resource-source-manifest.json"),
        item_components=file_record(ROOT / "assets/reference/profession-ui/item-components-source-manifest.json"),
        item_pools=file_record(ROOT / "assets/reference/profession-ui/item-pools-source-manifest.json"),
        auxiliary=file_record(ROOT / "assets/reference/profession-ui/auxiliary-source-manifest.json"),
        wiki=file_record(ROOT / "assets/reference/profession-ui/wiki-source-manifest.json"),
        portrait_lifecycle=file_record(ROOT / "assets/reference/profession-ui/portrait-lifecycle-source-manifest.json"),
        portrait_persistence=file_record(ROOT / "assets/reference/profession-ui/portrait-persistence-source-manifest.json"),
        portrait_settings=file_record(ROOT / "assets/reference/profession-ui/portrait-settings-source-manifest.json"),
        portrait_manager=file_record(ROOT / "assets/reference/profession-ui/portrait-manager-source-manifest.json"),
        appearance_config=file_record(ROOT / "assets/reference/profession-ui/appearance-config-source-manifest.json"),
        appearance_materials=file_record(ROOT / "assets/reference/profession-ui/appearance-materials-source-manifest.json"),
        texture_loader=file_record(ROOT / "assets/reference/profession-ui/texture-loader-source-manifest.json"),
        skin_clip=file_record(ROOT / "assets/reference/profession-ui/skin-clip-source-manifest.json"),
        slot_pose=file_record(ROOT / "assets/reference/profession-ui/slot-pose-source-manifest.json"),
        vertex_geometry=file_record(ROOT / "assets/reference/profession-ui/vertex-geometry-source-manifest.json"),
        atlas_geometry=file_record(ROOT / "assets/reference/profession-ui/atlas-geometry-source-manifest.json"),
        appearance_character=file_record(ROOT / "assets/reference/profession-ui/appearance-character-source-manifest.json"),
        appearance_spine=file_record(ROOT / "assets/reference/profession-ui/appearance-spine-source-manifest.json"),
        native_skins=file_record(ROOT / "assets/reference/profession-ui/native-skins-source-manifest.json"),
        appearance_ui=file_record(ROOT / "assets/reference/profession-ui/appearance-ui-source-manifest.json"),
        offscreen_player=file_record(ROOT / "assets/reference/profession-ui/offscreen-player-source-manifest.json"),
        native_curves=file_record(ROOT / "assets/reference/profession-ui/native-curves-source-manifest.json"),
        bone_pose=file_record(ROOT / "assets/reference/profession-ui/bone-pose-source-manifest.json"),
        native_ik=file_record(ROOT / "assets/reference/profession-ui/native-ik-source-manifest.json"),
        idle_ik=file_record(ROOT / "assets/reference/profession-ui/idle-ik-source-manifest.json"),
        photographer=file_record(ROOT / "assets/reference/profession-ui/photographer-source-manifest.json"),
        portrait_studio=file_record(ROOT / "assets/reference/profession-ui/portrait-studio-source-manifest.json"),
        item_resources=file_record(ROOT / "assets/reference/profession-ui/item-resources-source-manifest.json"),
        item_icon=file_record(ROOT / "assets/reference/profession-ui/item-icon-source-manifest.json"),
        item_configs=file_record(ROOT / "assets/reference/profession-ui/item-configs-source-manifest.json"),
        item_catalog=file_record(ROOT / "assets/reference/profession-ui/item-catalog-source-manifest.json"),
        native_textures=file_record(ROOT / "assets/reference/profession-ui/native-textures-source-manifest.json"),
        font_service=file_record(ROOT / "assets/reference/profession-ui/font-service-source-manifest.json"),
        rule_adapter=file_record(ROOT / "unity/Assets/Game/Runtime/Official/ProfessionUI/IProfessionRuleTipData.cs"),
        rule_instantiation_source=file_record(CODE / "client-source/UIUtils.cs"),
        validation=file_record(ROOT / "unity/Assets/Game/Editor/ProfessionUI/ProfessionProgressionValidation.cs"),
        recorder=file_record(Path(__file__)),
        limitations=["Native prefab runtime scripts and dynamic resource bindings remain incomplete; these ports are not exercised in the profession Play Mode UI flow.",
            "Data/resource/window/appearance/map/tip services must be connected to the original native prefabs before runtime completion.",
            "PlayerPropItem remains scoped to overview/radar string values; full typed preview and blessing interactions remain required where used.",
            "No full asset snapshot or Windows Player release is produced by this source restoration."])
    target = ROOT / "assets/reference/profession-ui/runtime-source-manifest.json"
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Recorded", len(ports), "scoped official profession component ports and local transaction validation")
    return result


if __name__ == "__main__":
    build()
