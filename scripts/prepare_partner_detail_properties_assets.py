"""Package the original detail-property region with current language and shared fonts."""
import hashlib
import json
from pathlib import Path
import re

from npc_travel_language import language
from official_apk import Apk, ROOT
from validation_workspace import validation_directory


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    work = validation_directory()
    native_path = ROOT/'assets/reference/partner-detail-properties-native-source-manifest.json'
    native = json.loads(native_path.read_bytes())
    inputs = {native_path.relative_to(ROOT).as_posix(): sha(native_path.read_bytes())}
    for relative, expected in native['installed_output_sha256'].items():
        if sha((ROOT/relative).read_bytes()) != expected:
            raise ValueError('Detail property installed payload changed: '+relative)
    prefab = ROOT/'unity/Assets/Official/Partners/UI/DetailProperties/NpcDetailPropContent.prefab'
    keys = set(re.findall(r'(?m)^  myKey: (ui_\S+)$', prefab.read_text('utf8')))
    keys.update(('ui_had_collect_npc', 'ui_had_not_collect_npc', 'ui_had_collect_stargod_npc', 'ui_had_not_collect_stargod_npc'))
    texts, sources = language(keys)
    if set(texts) != keys:
        raise ValueError('Selected original detail language missing: '+repr(keys-texts.keys()))
    apk = Apk.current()
    records = {row['name']: row for row in apk.source.manifest('WwiseBnk',apk.archive)['bundles']}
    for relative, expected in sources.items():
        raw = (ROOT/relative).read_bytes(); record = records[Path(relative).name]
        if sha(raw) != expected or len(raw) != record['size'] or hashlib.md5(raw).hexdigest() != record['hash']:
            raise ValueError('Selected detail language bytes differ: '+relative)
        inputs[relative] = expected
    output = {}
    text_path = Path('unity/Assets/Official/Partners/UI/DetailProperties/partner-detail-property-texts.json')
    output[text_path.as_posix()] = (json.dumps(dict(manifest=apk.version,texts=[dict(key=k,text=v) for k,v in sorted(texts.items())]),ensure_ascii=False,indent=2)+'\n').encode()
    output[str(text_path)+'.meta'] = ('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:partner-detail-property-texts').hexdigest()
        +'\nTextScriptImporter:\n  externalObjects: {}\n').encode()

    def guid(relative):
        return re.search(rb'^guid: ([a-f0-9]{32})$', (ROOT/(str(relative)+'.meta')).read_bytes(),re.M)[1].decode()

    script = 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfficialPartnerDetailPropertyAssetCatalog.cs'
    common = 'unity/Assets/Game/Resources/Partners/OfficialPartnerGiftPageAssetCatalog.asset'
    for relative in (script,script+'.meta',common,common+'.meta'):
        inputs[relative] = sha((ROOT/relative).read_bytes())
    asset = 'unity/Assets/Game/Resources/Partners/OfficialPartnerDetailPropertyAssetCatalog.asset'
    root = native['roots']['NpcDetailPropContent']
    output[asset] = ('''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
''' + f'  m_Script: {{fileID: 11500000, guid: {guid(script)}, type: 3}}\n'
        +'  m_Name: OfficialPartnerDetailPropertyAssetCatalog\n  m_EditorClassIdentifier:\n'
        +f'  propertyPrefab: {{fileID: {root["fileID"]}, guid: {root["guid"]}, type: 3}}\n'
        +f'  texts: {{fileID: 4900000, guid: {hashlib.md5(b"zjcs:partner-detail-property-texts").hexdigest()}, type: 3}}\n'
        +f'  common: {{fileID: 11400000, guid: {guid(common)}, type: 2}}\n').encode()
    output[asset+'.meta'] = ('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:partner-detail-property-catalog').hexdigest()
        +'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()
    proof_path=ROOT/'assets/reference/partner-detail-properties-assets-source-manifest.json'
    previous=json.loads(proof_path.read_bytes()).get('outputs',{}) if proof_path.is_file() else {}
    for relative,raw in output.items():
        path=ROOT/relative
        if path.is_file() and path.read_bytes()!=raw and sha(path.read_bytes())!=previous.get(relative):
            raise ValueError('Refusing to replace an unrecorded detail binding: '+relative)
    for relative,raw in output.items():
        (ROOT/relative).write_bytes(raw)
    proof=dict(manifest=apk.version,generator_sha256=sha(Path(__file__).read_bytes()),inputs=inputs,
        outputs={relative:sha(raw) for relative,raw in output.items()},language_keys=sorted(keys),
        scope='Explicit original property-region Resources binding; complete detail window and Player validation pending.')
    proof_path.write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Packaged native detail-property region and',len(texts),'original texts.')


if __name__=='__main__':
    main()
