"""Read Map11's dynamic confirmation and item prefabs from the pinned APK."""
import hashlib
import io
import json
from pathlib import Path

from UnityPy.classes.PPtr import PPtr
from prepare_cooking_module import CookingConverter, SCHEMA_CLASSES, vec, safe_name
from official_unity_schema import ScriptSchemas
from official_tables import Constructors
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
from prepare_unity_map10_entities import ROOT, write_generated


class Map11NativeUiSchemas(ScriptSchemas):
    def __init__(self, assemblies):
        # UGUI layout enums and RectOffset require the actual editor Core and
        # TextRendering assemblies. Read original bytes with strict consumption;
        # no official game scripts are replaced by these schema dependencies.
        self.generator = TypeTreeGenerator("2022.3.57f1", generator="AssetsTools")
        paths = {path.name: path for path in (ROOT / ".tools/source-packages/official-code/managed/decoded").glob("*.dll")}
        paths.update({name + ".dll": path for name, path in assemblies.items()})
        for path in paths.values():
            self.generator.load_dll(path.read_bytes())
        self.cache, self.records = {}, {}


class Map11SupportUi(CookingConverter):
    def __init__(self, apk, schemas, sprite_root=None):
        self.apk, self.schemas = apk, schemas
        self.sprite_root = sprite_root or ROOT / "unity/Assets/Official/World/Map/11/UI/Sprites"
        self.sprite_outputs = {}
        self.resources = {}
        editor = Path("D:/Tool/Unity/Editor/Data")
        self.ui_assemblies = {
            "UnityEngine.UI": ROOT / "unity/Library/ScriptAssemblies/UnityEngine.UI.dll",
            "UnityEngine.CoreModule": editor / "Managed/UnityEngine/UnityEngine.CoreModule.dll",
            "UnityEngine.TextRenderingModule": editor / "Managed/UnityEngine/UnityEngine.TextRenderingModule.dll",
            "netstandard": editor / "NetStandard/ref/2.1.0/netstandard.dll",
            "Framework": ROOT / ".tools/source-packages/official-code/managed/decoded/Framework.dll",
            "mscorlib": Path("C:/Windows/Microsoft.NET/Framework64/v4.0.30319/mscorlib.dll"),
        }
        self.ui_schemas = Map11NativeUiSchemas(self.ui_assemblies)
        self.native_components = []

    def game_object_components(self, game_object):
        components, transform = [], None
        for entry in game_object.m_Component:
            obj = entry.component.deref()
            kind = obj.type.name
            if kind in ("Transform", "RectTransform"):
                transform = obj.read()
                continue
            fields = None
            if kind == "MonoBehaviour":
                script = obj.read(check_read=False).m_Script.read()
                kind = script.m_ClassName
                if kind == "TextMeshProUGUI":
                    fields = self.text_mesh_pro_fields(obj)
                elif kind in SCHEMA_CLASSES or kind in ("MirrorImage", "ClampingScrollRect"):
                    schema = self.ui_schemas if script.m_AssemblyName.removesuffix(".dll") == "UnityEngine.UI" or kind == "ClampingScrollRect" else self.schemas
                    fields = schema.read(obj)
                    if schema is self.ui_schemas:
                        self.native_components.append((kind, game_object.object_reader.path_id, fields))
            elif kind == "CanvasGroup":
                fields = obj.read_typetree()
            components.append(("Image" if kind == "MirrorImage" else "ScrollRect" if kind == "ClampingScrollRect" else kind, obj, fields))
        return transform, components

    def add_sprite(self, obj):
        sprite = obj.read()
        key = (obj.assets_file.name, obj.path_id)
        if key in self.sprite_outputs:
            return self.sprite_outputs[key]
        image = sprite.image
        stream = io.BytesIO()
        image.save(stream, format="PNG")
        raw = stream.getvalue()
        digest = hashlib.sha256(raw).hexdigest()
        name = safe_name(sprite.m_Name) + "-" + digest[:10] + ".png"
        path = self.sprite_root / name
        write_generated(path, raw)
        uri = "res://" + path.relative_to(ROOT).as_posix()
        record = {"file": uri, "width": image.width, "height": image.height,
                  "border": vec(sprite.m_Border, 4),
                  "pivot": [sprite.m_Pivot.x * image.width, sprite.m_Pivot.y * image.height],
                  "ppu": sprite.m_PixelsToUnits}
        self.sprite_outputs[key] = record
        self.resources[uri] = dict(self.apk.provenance(obj), sha256=digest)
        return record


def load_support_ui(apk, schemas):
    exporter = Map11SupportUi(apk, schemas)
    config = apk.asset("scriptableconfig_modules_itemicon", "/ItemIconManagerConfig.asset")
    fields = schemas.read(config)
    icon = PPtr(**fields["DefPrefab"], assetsfile=config.assets_file).deref()
    item_object = apk.asset("config_luban", "Assets/Config/Luban/item.g.bytes")
    item_payload = item_object.read().m_Script.encode("utf-8", "surrogateescape")
    item_ids = (1, 2, 3, 56, 57, 110, 115, 118, 325, 2011, 2012, 2013, 2014, 2015, 2016, 41300, 41400, 42200)
    all_items = Constructors().table(item_payload, "TbItemInfo")
    item_rows = [row for row in all_items if row["ClassId"] in item_ids]
    if len(item_rows) != len(item_ids) or any(row["ItemType"] in fields["TypeList"]
                                or row["ClassId"] in fields["IdList"] for row in item_rows):
        raise ValueError("Map11 quick-buy and witch rewards must use their pinned default item icon.")
    exploration = json.loads((ROOT / "unity/Assets/Game/Resources/World/Mainline/exploration.json").read_text(encoding="utf-8"))
    exploration_ids = {row["ClassId"] for row in exploration["items"]}
    region_obj = apk.asset("config_luban_map", "/map_region.g.bytes")
    regions = Constructors().table(region_obj.read().m_Script.encode("utf-8", "surrogateescape"), "TbMapRegionInfo")
    drop_items = {item for row in regions if row["ClassId"] == 11 for item in row["DropItems"]}
    # Treasure-gacha tables span every mainland. Their selector intersects the
    # active region's DropItems; retain all common rewards and this map's union.
    item_rows = [row for row in all_items if row["ClassId"] in item_ids or row["ItemType"] in (100, 101) or row["ClassId"] in exploration_ids
                 and (row["ItemType"] != 305 or row["ClassId"] in drop_items)]
    def template(item):
        if item["ClassId"] in fields["IdList"]:
            pointer = fields["IdPrefabList"][fields["IdList"].index(item["ClassId"])]
        elif item["ItemType"] in fields["TypeList"]:
            pointer = fields["PrefabList"][fields["TypeList"].index(item["ItemType"])]
        else:
            pointer = fields["DefPrefab"]
        return PPtr(**pointer, assetsfile=config.assets_file).deref()
    award_config = apk.asset("scriptableconfig_modules_itemicon", "/ItemAwardIconManagerConfig.asset")
    award_fields = schemas.read(award_config)
    if award_fields["TypeAwardList"] or award_fields["PrefabAwardList"]:
        raise ValueError("Expected the pinned common award icon wrapper.")
    award_icon = PPtr(**award_fields["DefAwardPrefab"], assetsfile=award_config.assets_file).deref()
    icon_name = icon.read().m_Name
    prefabs = {"CostItemConfirmDialog": "prefabs_ui_confirmdialog",
               "PayMoneyChangeDialog": "prefabs_ui_pay",
               icon_name: apk.provenance(icon)["bundle"],
               "WorldWitchTouchDialog": "defaultpackage_assets_prefabs_ui_worldmap.bundle",
               "WorldStageInteractionPreviewDialogWitch": "defaultpackage_assets_prefabs_ui_worldmap.bundle",
               "WorldStageInteractionPreparePlayableDialog": "defaultpackage_assets_prefabs_ui_worldmap.bundle",
               "WorldTouchDialogWitchPeriodicityInteraction": "defaultpackage_assets_prefabs_ui_worldmap.bundle",
               "WitchStoryCollectContent": "defaultpackage_assets_prefabs_ui_worldmap.bundle",
               "WorldStageDialogWithWitchStoryCollect": "defaultpackage_assets_prefabs_ui_worldmap.bundle",
               "StoryWitchBookDialog": "defaultpackage_assets_prefabs_ui_storybook.bundle",
               "StoryBookDialog": "defaultpackage_assets_prefabs_ui_storybook.bundle",
               "OpenStoryBookItem": "defaultpackage_assets_prefabs_ui_storybook.bundle"}
    for item in item_rows:
        prefab = template(item)
        prefabs[prefab.read().m_Name] = apk.provenance(prefab)["bundle"]
    map_ui_manifest = json.loads((ROOT / "assets/reference/official-map/ui-source-manifest.json").read_text(encoding="utf-8"))
    for name in ("TalkDialog", "OptionTalkDialog", "MiniMapMaskedUI", "MiniMapFullScreenPanel"):
        prefabs[name] = map_ui_manifest["prefabs"][name]["bundle"]
    topbar_config = apk.asset("scriptableconfig_modules_topbar", "/TopBarManagerConfig.asset")
    topbar_fields = schemas.read(topbar_config)
    for field in ("TopBarPrefab", "CurrencyPrefab"):
        obj = PPtr(**topbar_fields[field], assetsfile=topbar_config.assets_file).deref()
        prefabs[obj.read().m_Name] = apk.provenance(obj)["bundle"]
    prefabs["TaskPanelListItemProgress"] = "defaultpackage_assets_prefabs_ui_task.bundle"
    prefabs["TaskListItem"] = "defaultpackage_assets_prefabs_ui_task.bundle"
    prefabs["FunctionEntryDialog"] = "defaultpackage_assets_prefabs_ui_functionentry.bundle"
    prefabs["FirstPayBtn"] = "defaultpackage_assets_prefabs_ui_activityentry.bundle"
    for name in ("MarkUIFilterEntryItem", "MiniMapMarkFilterItem"):
        prefabs[name] = "defaultpackage_assets_prefabs_ui_map_minimap_filter.bundle"
    prefabs[award_icon.read().m_Name] = apk.provenance(award_icon)["bundle"]
    for name in ("ExploreConfirmDialog", "ExploreDetailDialog", "ExploreHistoryItem", "ExploreProbabilityItem",
                 "ExploreRewardItem", "ExploreRewardPreviewItemDetailDialog", "ExploreSelectTokenDialog", "ExploreSelectTokenDialogItem"):
        prefabs[name] = "defaultpackage_assets_prefabs_ui_map_explore.bundle"
    for name in ("AwardListPanel", "AwardListPanelItem"):
        prefabs[name] = "defaultpackage_assets_prefabs_ui_award.bundle"
    for name in ("WorldTouchDialogLostGoddess", "LostGoddessResultSkillRankDialog",
                 "LostGoddessResultCharacterLevelDialog", "LostGoddessResultPlayerPropDialog",
                 "LostGoddessResultPlayerPropDialogItem"):
        prefabs[name] = "defaultpackage_assets_prefabs_ui_map_upgraderesult.bundle"
    for name in ("PreviewContentGroup", "WorldStageInteractionAwardPreview",
                 "WorldTouchDialogWithPreview", "WorldTouchDialogPeriodicityInteraction",
                 "WorldTouchDialogNegativePeriodicityInteraction", "WorldStageInteractionPreviewDialogHeroStatue",
                 "WorldStageInteractionResultAwardExtraPropPreview", "WorldStageInteractionResultAwardExtraPropPreviewItem",
                 "6570_11_DialogIcon", "6571_11_DialogIcon", "6572_11_DialogIcon", "6573_11_DialogIcon",
                 "WorldStageInteractionResultAwardStatusEffectPreview", "WorldStageInteractionResultAwardStatusEffectPreviewItem",
                 "WorldStageInteractionResultModifyHpPercentPreview", "WorldStageInteractionResultSpawnEntityPreview"):
        prefabs[name] = "defaultpackage_assets_prefabs_ui_worldmap.bundle"
    trees, sources = {}, {}
    for key, bundle in prefabs.items():
        data, _ = exporter.object_tree(key, bundle)
        nodes = data["nodes"]
        for node in nodes:
            node["children"] = []
        for node in nodes[1:]:
            nodes[node["parentIndex"]]["children"].append(node)
        trees[key] = nodes[0]
        sources[key] = dict(data["source"], path=data["source_path"])
    items = []
    quality_object = apk.asset("scriptableconfig_modules_itemicon", "/ItemIconConfig.asset")
    quality = schemas.read(quality_object)
    quality_sprite = exporter.add_sprite(PPtr(**quality["QualityBgImages"][3], assetsfile=quality_object.assets_file).deref())
    for item in sorted(item_rows, key=lambda value: value["ClassId"]):
        item_id, raw_icon = item["ClassId"], item["RawIcon"]
        asset_key = "defaultpackage_assets_image_" + raw_icon.rsplit("/", 1)[0].replace("/", "_").lower()
        candidates = sorted(bundle for bundle in apk.index
                            if bundle == asset_key + ".bundle" or bundle.startswith(asset_key + "_"))
        matches = [(path, ptr) for bundle in candidates for path, ptr in apk.container(bundle)
                   if path.rsplit("/", 1)[-1].rsplit(".", 1)[0].lower() == raw_icon.rsplit("/", 1)[-1].lower()
                   and ptr.type.name == "Sprite"]
        if len(matches) != 1:
            raise ValueError("Expected one pinned puzzle quick-buy item icon: " + raw_icon)
        sprite = exporter.add_sprite(matches[0][1].deref())
        item_quality = exporter.add_sprite(PPtr(**quality["QualityBgImages"][item["Quality"]], assetsfile=quality_object.assets_file).deref())
        items.append({"itemId": item_id, "template": template(item).read().m_Name, "sprite": sprite["file"], "metadata": sprite,
                      "qualitySprite": item_quality["file"], "qualityMetadata": item_quality})
    ui_sources = {"managerConfig": apk.provenance(config), "qualityConfig": apk.provenance(quality_object),
                  "items": dict(apk.provenance(item_object), payloadSha256=hashlib.sha256(item_payload).hexdigest(),
                                rows=[{key: row[key] for key in ("ClassId", "ItemType", "Quality", "RawIcon")} for row in item_rows]),
                  "awardManagerConfig": apk.provenance(award_config),
                  "regionDropItems": dict(apk.provenance(region_obj), mapClassId=11, items=sorted(drop_items)),
                  "topBarManagerConfig": apk.provenance(topbar_config),
                  "schemas": exporter.ui_schemas.records,
                  "schemaAssemblies": {name: {"path": str(path), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
                                       for name, path in exporter.ui_assemblies.items()}}
    return trees, sources, exporter.resources, items, ui_sources, quality_sprite, exporter.ui_schemas, exporter
