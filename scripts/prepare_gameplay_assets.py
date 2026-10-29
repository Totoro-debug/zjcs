"""Extract gameplay icons from the pinned APK; extend the existing resource manifest."""
import hashlib
import json
from pathlib import Path
from prepare_official_city import Exporter, ART, REFERENCE, ROOT


def build():
    exporter = Exporter()  # Validates the pinned APK before reading any resource.
    rules = json.loads((ART / "rules.json").read_text(encoding="utf8"))
    manifest = json.loads((REFERENCE / "source-manifest.json").read_text(encoding="utf8"))
    icons = json.loads((ART / "icons.json").read_text(encoding="utf8"))
    items = {r["ClassId"]: r for r in rules["item"]}
    selected = {56, 57, 106, 107, 118, 120, 1500, 1501, 1502, 350, 8050, 8101, 43003}
    # Every obtainable bag item can appear in a loaded save or a choice chest.
    selected.update(r['ClassId'] for r in rules['item'] if r['ShowInBag'])
    for profession in rules["profession_base"]:
        selected.update(profession["Skills"])
    for skill in rules["skill"]:
        if skill["ClassId"] in selected:
            selected.add(skill["ClassId"] + 2000)
    for pet in rules["pet"]:
        selected.update([pet["ClassId"], pet["PetPieceId"]])
    selected.update(r["ClassId"] for r in rules["equip"])
    selected.update(r['ItemId'] for r in rules['shop_goods'])
    selected.update(r['ItemId'] for r in rules.get('home_visit_shop_goods',[]))
    for table in ['direct_pay','periodic_card_pay','daily_pay_gift','weekly_pay_gift','activity_sign_days']:
        for row in rules.get(table,[]):
            for field in ['FixAward','FirstExtraAward','NotFirstExtraAward','PeriodicAward','Rewards','Award']:
                if isinstance(row.get(field),dict):selected.update(map(int,row[field]))
    for pool in rules["treasure_gacha"]:
        if pool["ChestGachaId"] == 11:
            selected.update(pool["ChestGachaItems"])
    for treasure in rules["treasure"]:
        selected.update([treasure["ClassId"],treasure["TreasurePieceId"]])
    for recipe in rules["cooking"]:
        selected.add(recipe["Id"])
        if isinstance(recipe["Materials"],dict):selected.update(map(int, recipe["Materials"]))
    # Higher level or unbundled icons remain text-only and are recorded, never substituted.
    packaged = set(exporter.apk.archive.namelist())
    bundles = {}
    missing = []
    requests = {str(ident):items[ident]['RawIcon'] for ident in sorted(selected) if ident in items}
    for row in rules.get('direct_pay',[]):requests['pay:'+str(row['Id'])]=row['Icon']
    # LanguageUtils.GetItemDetailQualityBg uses this exact source naming.
    for quality, name in enumerate(['White','Blue','Purple','Orange','Gold','Red','Rainbow'],1):
        requests[f'quality_header:{quality}']=f'UI/ItemDetailDialog/Img{name}_PopUps'
        requests[f'quality_header_small:{quality}']=f'UI/ItemDetailDialog/Img{name}_PopUps_Small'
    for cfg in rules['profession_base']:
        for field in ['Icon1','Icon2','Icon3','ProfessionBgImg']:
            requests[f'profession:{cfg["Profession"]}:{field}'] = cfg[field]
    for cfg in rules.get('npc',[]):
        for field in ['Icon','FigureImage']:
            requests[f'npc:{cfg["Id"]}:{field}']=cfg[field]
    for ident, icon in requests.items():
        if ident in icons:
            continue
        if not icon:
            continue
        prefix = "defaultpackage_assets_image_" + icon.rsplit("/", 1)[0].replace("/", "_").lower()
        asset_key = "defaultpackage_assets_image_" + icon.replace("/", "_").lower()
        found = None
        for bundle, digest in exporter.apk.index.items():
            if not (bundle.startswith(prefix) or asset_key.startswith(bundle.removesuffix('.bundle')+'_')) or f"assets/yoo/DefaultPackage/{digest}.bundle" not in packaged:
                continue
            if bundle not in bundles:
                bundles[bundle] = {
                    path.rsplit("/", 1)[-1].rsplit(".", 1)[0].lower(): ptr
                    for path, ptr in exporter.apk.container(bundle) if ptr.type.name == "Sprite"
                }
            found = bundles[bundle].get(icon.rsplit("/", 1)[-1].lower())
            if found:
                break
        if found:
            icons[ident] = exporter.sprite(found)
        else:
            missing.append(icon)
    # Resolve quality frames from the client config's actual ordered Sprite list.
    import struct
    from UnityPy.classes.PPtr import PPtr
    for name in exporter.apk.index:
        if 'scriptableconfig' not in name or 'itemicon' not in name:
            continue
        for obj in exporter.apk.objects(name):
            if obj.type.name!='MonoBehaviour':continue
            cls,raw,_=exporter.mono(obj)
            if cls!='ItemIconConfig':continue
            end=(32+struct.unpack_from('<i',raw,28)[0]+3)&~3
            count=struct.unpack_from('<i',raw,end)[0]
            for quality in range(count):
                file_id,path_id=struct.unpack_from('<iq',raw,end+4+quality*12)
                if path_id:
                    icons['quality:'+str(quality)]=exporter.sprite(PPtr(m_FileID=file_id,m_PathID=path_id,assetsfile=obj.assets_file))
    exporter.save('icons.json',icons)
    manifest["resources"].update(exporter.records)
    manifest["data_sha256"]["icons.json"] = hashlib.sha256((ART / "icons.json").read_bytes()).hexdigest()
    manifest["unsupported"]["gameplay_icons_not_packaged"] = sorted(set(missing))
    (REFERENCE / "source-manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf8")
    entries = sorted(manifest["resources"])
    __import__("official_resource_manifest").write_resources(entries)
    print(f"Gameplay icon catalog: {len(icons)}; added resources: {len(exporter.records)}; unavailable: {len(missing)}")


if __name__ == "__main__":
    build()
