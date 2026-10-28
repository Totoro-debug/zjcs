"""Record the source-selected native item hint and its explicit Windows bindings."""
import hashlib
import json
from pathlib import Path

from prepare_unity_profession_native_inputs import ROOT, CACHE
from official_apk import Apk
from official_manifest import read_asset_manifest
from map13_native_schemas import NativeSchemas


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def build(unity_project=None):
    apk = Apk.current()
    if apk.version != "871_156598":
        raise ValueError("Wrong original item source version")
    code = ROOT / ".tools/source-packages/current/871_156598/official-code"
    schemas = NativeSchemas(code, unity_project=unity_project)
    config = apk.asset("defaultpackage_assets_scriptableconfig_modules_itemicon.bundle", "/ItemIconAttachmentConfig.asset")
    fields = config.read_typetree(nodes=schemas.default.tree("Assembly-CSharp", "ItemIconAttachmentConfig"))
    entry, = [row for row in fields["Templates"] if row["Type"] == 13]
    assets = read_asset_manifest(apk.source.manifest_bytes("DefaultPackage", apk.archive))
    asset, = [row for row in assets if row["guid"] == entry["GuidProxy"]["mAssetGuid"]]
    if asset["path"] != "Assets/Prefabs/UI/Item/Attachments/CenterHintContent.prefab" or asset["bundle"] != "defaultpackage_assets_prefabs_ui_item.bundle":
        raise ValueError("Inspect the original HintTip configuration target")
    native = apk.asset(asset["bundle"], "/CenterHintContent.prefab")
    if native.read().m_IsActive:
        raise ValueError("Original hint prefab activation changed")
    installed_path = CACHE / "typed/installed-item-hint.json"
    installed = json.loads(installed_path.read_bytes())
    for row in installed["outputs"]:
        path = ROOT / "unity" / row["installed_path"]
        if record(path)["sha256"] != row["sha256"] or record(path.with_name(path.name + ".meta"))["sha256"] != row["meta_sha256"]:
            raise ValueError("Installed original item hint changed: " + str(path))
    for row in installed["folder_metadata"]:
        if record(ROOT / "unity" / row["path"])["sha256"] != row["sha256"]:
            raise ValueError("Native item folder metadata changed")
    result = dict(format=1, manifest=apk.version,
        configuration=dict(source=apk.provenance(config), payload_sha256=hashlib.sha256(config.get_raw_data()).hexdigest(), fields=fields),
        original_asset=asset, source=apk.provenance(native), payload_sha256=hashlib.sha256(native.get_raw_data()).hexdigest(),
        original_icon_source=record(code / "client-source/ItemIcon.cs"),
        component_sources=record(ROOT / "assets/reference/profession-ui/item-components-source-manifest.json"),
        pool_sources=record(ROOT / "assets/reference/profession-ui/item-pools-source-manifest.json"),
        native_export=record(ROOT / "assets/reference/profession-ui/native-export-source-manifest.json"),
        native_installation=installed, recorder=record(Path(__file__)),
        validation="Isolated Unity imports the complete original HintTip prefab with all five script types and its original Windows shader binding. Actual Play Mode checks serialized child/text references, inactive lazy creation, cached lookup activation behavior, explicit show/hide and recycling through the original attachment manager/pool. It verifies FIFO reuse, reset/clear parent and activation order, reverse shrink, low-memory release followed by destruction/recreation, and manager event unsubscription. The original prefab and atlas Sprite render on GPU with original source-font glyphs; the runtime frame is inspected. The numeric probe text is test data, not a replacement game UI or a localization claim.",
        limitations=["ItemIcon and its four reward bindings are verified separately by item-catalog-source-manifest.json. Item detail popup contents and full profession panel service wiring remain pending.",
            "This small original prefab validation does not cover profession GUI mouse input, full layout, appearance animation or Windows Player release.",
            "No full asset backup snapshot has been generated for these additions."])
    target = ROOT / "assets/reference/profession-ui/item-resources-source-manifest.json"
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Recorded original configured HintTip,", len(installed["outputs"]), "installed native assets and exact binding operations")


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity-project", type=Path)
    args = parser.parse_args()
    build(args.unity_project)
