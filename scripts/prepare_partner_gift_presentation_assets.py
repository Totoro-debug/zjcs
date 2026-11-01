"""Recover original dynamic relationship backgrounds and explicit gift-page assets."""
import hashlib
import json
from pathlib import Path
import re
import yaml
from UnityPy.enums import ClassIDType

from official_apk import ROOT
from official_manifest import read_asset_manifest
from npc_travel_language import language
from prepare_current_partner_native import documents, identity
from prepare_partner_card_native import PartnerCardBinding, Dumper, guid, sha
from prepare_partner_ui_helpers import meta
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    proof_path = ROOT/'assets/reference/partner-gift-presentation-assets-source-manifest.json'
    previous = json.loads(proof_path.read_bytes()).get('outputs', {}) if proof_path.is_file() else {}
    binding = PartnerCardBinding(work)
    apk = binding.recovery.apk
    rules_path = ROOT / 'unity/Assets/Official/Partners/partner-rules.json'
    rules = json.loads(rules_path.read_bytes())
    if rules['manifest'] != apk.version:
        raise ValueError('Selected gift presentation rules differ')
    binding.inputs[rules_path.relative_to(ROOT).as_posix()] = sha(rules_path.read_bytes())
    raw_manifest = apk.source.manifest_bytes('DefaultPackage', apk.archive)
    records = read_asset_manifest(raw_manifest)
    tags = rules['tables']['npc_friendship_tag']
    sprites = []
    for address in sorted({row['TagBg'] for row in tags}):
        record, = [row for row in records if row['address'] == 'Assets/Image/' + address
                   or row['path'] == 'Assets/Image/' + address + '.png']
        obj, = [ref.deref() for name, ref in apk.container(record['bundle'])
                if name.casefold() == record['path'].casefold() and ref.deref().type.name == 'Sprite']
        key, file_id = binding.recovery.fields(obj)['m_RenderDataKey']
        render_guid = ''.join(format(key['data[' + str(i) + ']'], '08x')[::-1] for i in range(4))
        if render_guid != record['guid']:
            raise ValueError('Relationship background render key differs')
        name = binding.name_key(obj.read(check_read=False).m_Name)
        candidates = []
        for candidate_key in [name] + [name + str(i) for i in range(100)]:
            for candidate in binding.by_name.get(candidate_key, []):
                if candidate.suffix == '.asset':
                    docs = documents(candidate.read_text('utf8'))
                    if len(docs) == 1 and next(iter(docs.values())).get('Sprite', {}).get('m_RenderDataKey') == {render_guid: file_id}:
                        candidates.append(candidate)
        candidate, = candidates
        source_guid = guid(Path(str(candidate) + '.meta'))
        binding.objects[source_guid] = obj
        sprites.append(dict(address=address, pointer=binding.load(source_guid), manifest_asset=record,
            original=identity(obj), original_sha256=sha(obj.get_raw_data()), source=apk.provenance(obj)))
    # The same verified closure converter supplies original mesh/atlas fields
    # and selected Windows texture payloads; process newly queued dependencies.
    for target, target_guid, data in binding.queue:
        if isinstance(data, dict):
            resolved = binding.replace(data)
            data.clear(); data.update(resolved)
    existing = {}
    for metadata in (ROOT / 'unity/Assets/Official/Partners').rglob('*.meta'):
        asset = Path(str(metadata)[:-5])
        if asset.is_file():
            existing[guid(metadata)] = asset
    destination = ROOT / 'unity/Assets/Official/Partners/UI/GiftPresentation'
    destination.mkdir(exist_ok=True)
    installed, reused = {}, {}

    def write_owned(path, payload):
        relative = path.relative_to(ROOT).as_posix()
        if path.is_file() and path.read_bytes() != payload and sha(path.read_bytes()) != previous.get(relative):
            raise ValueError('Preserve unrecorded gift presentation change: ' + relative)
        path.write_bytes(payload)

    for target, target_guid, data in binding.queue:
        if isinstance(data, dict):
            raw = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
            for file_id, content in data.items():
                kind, = content
                raw += '--- !u!' + str(int(ClassIDType[kind])) + ' &' + str(file_id) + '\n'
                raw += yaml.dump(content, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
            raw = raw.encode()
        else:
            raw = data
        path = existing.get(target_guid, destination / target)
        if path.is_file():
            if path.read_bytes() != raw:
                raise ValueError('Preserve existing relationship asset: ' + str(path))
            reused[path.relative_to(ROOT).as_posix()] = sha(raw)
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
            Path(str(path) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + target_guid +
                '\nNativeFormatImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
        for output in (path, Path(str(path) + '.meta')):
            installed[output.relative_to(ROOT).as_posix()] = sha(output.read_bytes())
    keys = {'npc_friendship_tag_' + str(row['ClassId']) + '_name' for row in tags}
    texts, inputs = language(keys)
    if set(texts) != keys:
        raise ValueError('Selected relationship text missing: ' + repr(keys - texts.keys()))
    language_records = {row['name']: row for row in apk.source.manifest('WwiseBnk', apk.archive)['bundles']}
    for relative, expected in inputs.items():
        raw = (ROOT / relative).read_bytes(); record = language_records[Path(relative).name]
        if sha(raw) != expected or hashlib.md5(raw).hexdigest() != record['hash'] or len(raw) != record['size']:
            raise ValueError('Selected relationship language payload differs')
        binding.inputs[relative] = expected
    text_path = destination / 'partner-relationship-texts.json'
    payload = (json.dumps(dict(manifest=apk.version, texts=[dict(key=k, text=v) for k, v in sorted(texts.items())]), ensure_ascii=False) + '\n').encode()
    write_owned(text_path, payload)
    write_owned(Path(str(text_path) + '.meta'), ('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:partner-relationship-texts').hexdigest()
        + '\nTextScriptImporter:\n  externalObjects: {}\n').encode())
    for p in (text_path, Path(str(text_path) + '.meta')): installed[p.relative_to(ROOT).as_posix()] = sha(p.read_bytes())
    image_script = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionImageCatalog.cs'
    image_path = ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerRelationshipImages.asset'
    content = {11400000: dict(MonoBehaviour=dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0),
        m_PrefabInstance=dict(fileID=0), m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1,
        m_EditorHideFlags=0, m_Script=dict(fileID=11500000, guid=guid(Path(str(image_script)+'.meta')), type=3),
        m_Name='OfficialPartnerRelationshipImages', m_EditorClassIdentifier='', Entries=[
            dict(Path='Assets/Image/' + row['address'], AssetGuid=row['manifest_asset']['guid'], Asset=row['pointer']) for row in sprites]))}
    write_owned(image_path, ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n' +
        yaml.dump(content[11400000], Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)).encode())
    write_owned(Path(str(image_path)+'.meta'), ('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:partner-relationship-images').hexdigest()+
        '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n').encode())
    for p in (image_path, Path(str(image_path)+'.meta')): installed[p.relative_to(ROOT).as_posix()] = sha(p.read_bytes())
    gift_proof_path = ROOT/'assets/reference/partner-gift-controller-source-manifest.json'
    gift = json.loads(gift_proof_path.read_bytes())
    if gift['manifest'] != apk.version:
        raise ValueError('Gift page installed version differs')
    binding.inputs[gift_proof_path.relative_to(ROOT).as_posix()] = sha(gift_proof_path.read_bytes())
    for relative, expected in gift['installed_output_sha256'].items():
        if sha((ROOT / relative).read_bytes()) != expected:
            raise ValueError('Installed native gift page differs: ' + relative)
    for source in (image_script, Path(str(image_script)+'.meta')):
        binding.inputs[source.relative_to(ROOT).as_posix()] = sha(source.read_bytes())
    image_content = content[11400000]['MonoBehaviour']
    image_content['m_Name'] = 'OfficialPartnerGiftPageImages'
    image_content['Entries'] += [dict(Path='Assets/Image/'+row['address'], AssetGuid=row['manifest_asset']['guid'], Asset=row['pointer'])
        for row in gift['dynamic_images']['sprites']]
    image_content['Entries'] += [dict(Path=name, AssetGuid=source_guid, Asset=gift['roots'][name]) for name,source_guid in (
        ('ReduceDot','a1a733aad02670d46814b30ac75aabc8'), ('CenterHintContent','d019ea2d9f143514ebc8c30f545dca16'), ('Upgrade','18dffd1b841a0884bb2131048f3c26a1'))]
    page_images = image_path.with_name('OfficialPartnerGiftPageImages.asset')
    write_owned(page_images, ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'+yaml.dump(content[11400000],Dumper=Dumper,sort_keys=False,allow_unicode=True,width=1000000)).encode())
    write_owned(Path(str(page_images)+'.meta'), ('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:partner-gift-page-images').hexdigest()+
        '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n').encode())
    for p in (page_images,Path(str(page_images)+'.meta')):installed[p.relative_to(ROOT).as_posix()]=sha(p.read_bytes())
    catalog_script = ROOT/'unity/Assets/Game/Runtime/Offline/PartnersUI/OfficialPartnerGiftPageAssetCatalog.cs'
    catalog = page_images.with_name('OfficialPartnerGiftPageAssetCatalog.asset')
    def pointer(path,file_id=4900000,kind=3):return dict(fileID=file_id,guid=guid(Path(str(path)+'.meta')),type=kind)
    mono = dict(image_content)
    mono['m_Script']=pointer(catalog_script,11500000)
    mono['m_Name']='OfficialPartnerGiftPageAssetCatalog';mono.pop('Entries')
    mono.update(giftPrefab=gift['roots']['NpcGiftContent'], giftTexts=pointer(ROOT/'unity/Assets/Official/Partners/UI/GiftController/partner-gift-controller-texts.json'),
        relationshipTexts=pointer(text_path),images=pointer(page_images,11400000,2),iconConfig=gift['icon_config'],attachmentConfig=gift['attachment_config'],
        regionFont=gift['dynamic_images']['font']['font_asset'],sourceFont=gift['dynamic_images']['font']['source_font'])
    write_owned(catalog,('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'+yaml.dump(dict(MonoBehaviour=mono),Dumper=Dumper,sort_keys=False,allow_unicode=True,width=1000000)).encode())
    write_owned(Path(str(catalog)+'.meta'),('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:partner-gift-page-catalog-asset').hexdigest()+
        '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n').encode())
    for p in (catalog,Path(str(catalog)+'.meta')):installed[p.relative_to(ROOT).as_posix()]=sha(p.read_bytes())
    for p in [destination] + [p for p in destination.rglob('*') if p.is_dir()]:
        meta(p, folder=True)
        metadata = Path(str(p)+'.meta')
        metadata.write_bytes(metadata.read_bytes().replace(b'\r\n', b'\n'))
        installed[Path(str(p)+'.meta').relative_to(ROOT).as_posix()] = sha(Path(str(p)+'.meta').read_bytes())
    proof = dict(manifest=apk.version, asset_manifest_sha256=sha(raw_manifest), generator_sha256=sha(Path(__file__).read_bytes()),
        inputs=binding.inputs, objects=binding.proof, sprites=sprites, outputs=installed, reused=reused,
        relationship_tag_ids=[row['ClassId'] for row in tags])
    proof_path.write_text(json.dumps(proof, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
    print('Bound original relationship backgrounds:', len(sprites), '; texts:', len(texts), flush=True)


if __name__ == '__main__':
    main()
