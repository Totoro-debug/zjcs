"""Bind packaged original item Sprites to their original logical paths and GUIDs."""
import hashlib
import json
from pathlib import Path
import yaml

from prepare_unity_profession_native_inputs import ROOT, CACHE
from official_apk import Apk
from official_manifest import read_asset_manifest
from prepare_unity_profession_native_bindings import CACHE, guid, sha
from prepare_unity_profession_reward_sprites import Dumper, source_header


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path))


def build(unity_project=None):
    project = (unity_project or ROOT / 'unity').resolve()
    if project != (ROOT / 'unity').resolve():
        from validation_workspace import require_temporary_unity_project
        require_temporary_unity_project(project)
    runtime = ROOT / "unity/Assets/Game/Runtime/Offline/ProfessionUI"
    for name in ("OfflineProfessionImageCatalog", "OfflineProfessionImageResourceLoader"):
        path = runtime / (name + ".cs.meta")
        path.write_text("fileFormatVersion: 2\nguid: " + hashlib.md5(("zjcs-offline-profession-ui-" + name).encode()).hexdigest() + "\n", encoding="utf-8")
    apk = Apk.current()
    manifest = read_asset_manifest(apk.source.manifest_bytes("DefaultPackage", apk.archive))
    dynamic_path = ROOT / "assets/reference/profession-ui/dynamic-resources-source-manifest.json"
    dynamic = json.loads(dynamic_path.read_bytes())
    configs_path = ROOT / "assets/reference/profession-ui/item-configs-source-manifest.json"
    configs = json.loads(configs_path.read_bytes())
    native = json.loads((CACHE / "typed/installed-item-icons.json").read_bytes())
    if {apk.version, dynamic["manifest"], configs["manifest"], native["manifest"]} != {"871_156598"}:
        raise ValueError("Mixed original item source versions")
    installed = {row["native_relative_path"]: row for row in native["outputs"]}
    for row in native["outputs"]:
        target = project / row["installed_path"]
        if sha(target) != row["sha256"] or sha(target.with_name(target.name + ".meta")) != row["meta_sha256"]:
            raise ValueError("Installed original ItemIcon resource differs: " + str(target))
    entries, bindings, items = [], [], []
    root = "Assets/Official/ProfessionUI/Native/"
    for item in configs["reward_items"]:
        reward, = [row for row in dynamic["resources"] if row.get("item_id") == item["ClassId"]]
        path = "Assets/Image/" + reward["logical_address"]
        original, = [row for row in manifest if row["address"] == path]
        asset = installed[reward["native_relative_path"]]
        if asset["guid"] != reward["export_guid"]:
            raise ValueError("Original reward Sprite binding changed")
        reference = dict(fileID=21300000, guid=asset["guid"], type=2)
        entries.append(dict(Path=path, AssetGuid=original["guid"], Asset=reference))
        bindings.append(dict(item_id=item["ClassId"], original_asset=original, reference=reference,
            installed_sprite=asset["installed_path"]))
        quality = configs["quality_sprites"][item["Quality"]]
        items.append(dict(info=item, sprite_path=asset["installed_path"], quality_path=root + quality["native_relative_path"]))
    hint, = [row for row in manifest if row["path"] == "Assets/Prefabs/UI/Item/Attachments/CenterHintContent.prefab"]
    hint_asset = installed[hint["path"].removeprefix("Assets/")]
    # Native prefab root local fileID is retained; it is not the APK pathID.
    import re
    raw_prefab = (ROOT / hint_asset["source"]["path"]).read_text(encoding="utf-8")
    root_id = re.search(r"(?m)^--- !u!1 &(\d+)$", raw_prefab)[1]
    entries.append(dict(Path=hint["address"], AssetGuid=hint["guid"],
        Asset=dict(fileID=int(root_id), guid=hint_asset["guid"], type=3)))
    script = runtime / "OfflineProfessionImageCatalog.cs"
    data = source_header()
    data.update(m_Script=dict(fileID=11500000, guid=guid(script), type=3), m_Name="ProfessionItemImages",
        m_EditorClassIdentifier="", Entries=entries)
    target = ROOT / "unity/Assets/Official/ProfessionUI/Catalogs/ProfessionItemImages.asset"
    target.parent.mkdir(parents=True, exist_ok=True)
    folder_meta = target.parent.with_name(target.parent.name + ".meta")
    if not folder_meta.exists():
        folder_meta.write_text("fileFormatVersion: 2\nguid: " + hashlib.md5(b"zjcs:profession:item-image-catalogs").hexdigest() + "\nfolderAsset: yes\n", encoding="utf-8")
    body = yaml.dump({"MonoBehaviour": data}, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
    target.write_text("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n" + body, encoding="utf-8", newline="\n")
    meta = target.with_name(target.name + ".meta")
    meta.write_text("fileFormatVersion: 2\nguid: " + hashlib.md5(b"zjcs:profession:item-image-catalog").hexdigest() + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n", encoding="utf-8")
    result = dict(format=1, manifest=apk.version,
        verified_project_kind='source' if project == (ROOT / 'unity').resolve() else 'isolated-validation',
        dynamic_sources=record(dynamic_path), item_configs=record(configs_path),
        bindings=bindings, catalog=record(target), catalog_meta=record(meta), folder_meta=record(folder_meta),
        runtime=[record(runtime / (name + suffix)) for name in ("OfflineProfessionImageCatalog", "OfflineProfessionImageResourceLoader") for suffix in (".cs", ".cs.meta")],
        native_installation=native,
        validation_sources=[record(ROOT / "unity/Assets/Game/Editor/ProfessionUI" / (name + ".cs")) for name in ("ProfessionItemIconValidation", "ProfessionNativeTextureValidation")],
        generator=record(Path(__file__)),
        operation="Create explicit local lookup entries for four original reward Sprite logical addresses and AssetTable GUIDs, plus the source-configured native HintTip prefab. Keep the actual imported Sprite/prefab local IDs and GUIDs; asynchronous local requests have the original already-completed subscription behavior. No UI hierarchy is generated.",
        validation="Actual isolated Unity Play Mode renders the original ItemIcon prefab for all four current reward definitions, checks the source quality Sprite and numeric text, binds the original TTF via the ported font service, confirms all count glyphs use that font, simulates mouse down/up through StandaloneInputModule to the original ItemIcon detail-popup dispatch, and checks Clear/unsubscription. GPU readback verifies every original mip byte in four UI textures before dynamic glyph generation.",
        limitations=["Detail-popup contents, full profession GUI layout, player provider/state wiring and Windows Player release remain incomplete. Original attachment manager/pools are verified separately by item-pools-source-manifest.json.", "Numeric text 300 is fixture data for all four items; actual gift amounts come from the achievement configuration."])
    proof = ROOT / "assets/reference/profession-ui/item-catalog-source-manifest.json"
    proof.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    fixture = dict(items=items, global_colors=configs["global_colors"]["fields"],
        prefab=root + "Prefabs/UI/Item/ItemIconDefault.prefab",
        config=root + "ScriptableConfig/Modules/ItemIcon/ItemIconConfig.asset",
        catalog=target.relative_to(ROOT / "unity").as_posix())
    (CACHE / "typed/item-icon-fixture.json").write_text(json.dumps(fixture, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Bound four original reward Sprites and native HintTip to explicit local resources")
    return result


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--unity-project', type=Path)
    build(parser.parse_args().unity_project)
