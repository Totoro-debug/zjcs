"""Read Map11's dynamic confirmation and item prefabs from the pinned APK."""
import hashlib
import io
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
        for path in assemblies.values():
            self.generator.load_dll(path.read_bytes())
        self.cache, self.records = {}, {}


class Map11SupportUi(CookingConverter):
    def __init__(self, apk, schemas):
        self.apk, self.schemas = apk, schemas
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
                elif kind in SCHEMA_CLASSES or kind == "MirrorImage":
                    schema = self.ui_schemas if script.m_AssemblyName.removesuffix(".dll") == "UnityEngine.UI" else self.schemas
                    fields = schema.read(obj)
                    if schema is self.ui_schemas:
                        self.native_components.append((kind, game_object.object_reader.path_id, fields))
            elif kind == "CanvasGroup":
                fields = obj.read_typetree()
            components.append(("Image" if kind == "MirrorImage" else kind, obj, fields))
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
        path = ROOT / "unity/Assets/Official/World/Map/11/UI/Sprites" / name
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
    item_rows = [row for row in Constructors().table(item_payload, "TbItemInfo") if row["ClassId"] in (2, 3, 325)]
    if len(item_rows) != 3 or any(row["Quality"] != 3 or row["ItemType"] in fields["TypeList"]
                                or row["ClassId"] in fields["IdList"] for row in item_rows):
        raise ValueError("Map11 quick-buy currencies and ticket must use the pinned quality and default item icon.")
    icon_name = icon.read().m_Name
    prefabs = {"CostItemConfirmDialog": "prefabs_ui_confirmdialog",
               "PayMoneyChangeDialog": "prefabs_ui_pay",
               icon_name: apk.provenance(icon)["bundle"]}
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
        items.append({"itemId": item_id, "sprite": sprite["file"], "metadata": sprite})
    ui_sources = {"managerConfig": apk.provenance(config), "qualityConfig": apk.provenance(quality_object),
                  "items": dict(apk.provenance(item_object), payloadSha256=hashlib.sha256(item_payload).hexdigest(),
                                rows=[{key: row[key] for key in ("ClassId", "ItemType", "Quality", "RawIcon")} for row in item_rows]),
                  "schemas": exporter.ui_schemas.records,
                  "schemaAssemblies": {name: {"path": str(path), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
                                       for name, path in exporter.ui_assemblies.items()}}
    return trees, sources, exporter.resources, items, ui_sources, quality_sprite, exporter.ui_schemas
