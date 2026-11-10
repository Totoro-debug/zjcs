"""Stage the actual original bottom bar and production local binding for Play Mode."""
import json
from pathlib import Path
import re
import shutil
import hashlib
from official_apk import ROOT
from prepare_partner_card_native import guid, sha, Dumper
from prepare_current_partner_native import documents
from prepare_partner_input_dialog import metadata
from prepare_home_repair_validation import prepare as prepare_project
from validation_assemblies import stage_runtime_modules
from validation_workspace import validation_directory
import yaml


def catalog_bytes(receipt):
    script = ROOT / 'unity/Assets/Game/Runtime/Official/HomeHudUI/OfficialHomeBottomBarAssetCatalog.cs'
    fields = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),
        m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1, m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000, guid=guid(Path(str(script)+'.meta')), type=3),
        m_Name='OfficialHomeBottomBarAssetCatalog', m_EditorClassIdentifier='', prefab=receipt['roots']['PublicBottomBar'],
        texts=dict(fileID=4900000, guid=hashlib.md5(b'zjcs:home-bottom-bar-texts').hexdigest(), type=3),
        tipSourceObjects=dict(fileID=4900000, guid=hashlib.md5(b'zjcs:home-bottom-tip-source-objects').hexdigest(), type=3),
        sprites=[dict(guid=key, sprite=row['pointer']) for key,row in sorted(receipt['dynamic_sprites'].items())],
        sounds=[dict(eventId=row['event']['id'], clip=row['pointer']) for row in receipt['audio'].values()])
    return ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'
        + yaml.dump(dict(MonoBehaviour=fields), Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)).encode('utf8')


def equivalent(left,right):
    if left == right: return True
    if not left.startswith(b'%YAML') or not right.startswith(b'%YAML'): return False
    def normalize(value):
        if isinstance(value,dict):
            return {key: '' if key=='m_Name' and child is None else normalize(child) for key,child in value.items()}
        if isinstance(value,list): return [normalize(child) for child in value]
        return value
    return normalize(documents(left.decode('utf8'))) == normalize(documents(right.decode('utf8')))


def stage(work):
    project = work/'unity'
    if not (project/'ProjectSettings/ProjectVersion.txt').exists(): prepare_project(work)
    stage_runtime_modules(project, ROOT/'unity/Assets/Game/Runtime', ['Zjcs.Offline.HomeUI'])
    # Remove only five abandoned files created earlier in this owned fixture.
    for name in ('TipDotGraphOutLinkNode','TipDotActivityEntryBinderSO','TipDotActivityBinderSO','ActivityType','ActivityEntryType'):
        for suffix in ('.cs','.cs.meta'):
            (project/'Assets/Game/Runtime/Official/HomeHudUI'/(name+suffix)).unlink(missing_ok=True)
    receipt = json.loads((work/'home-bottom-bar-native-source-manifest.json').read_bytes())
    by_guid={guid(path):Path(str(path)[:-5]) for path in (project/'Assets').rglob('*.meta') if Path(str(path)[:-5]).is_file()}
    folder=work/'gift-assets'
    for relative,expected in receipt['outputs'].items():
        source=folder/relative
        if sha(source.read_bytes())!=expected: raise ValueError('Staged native source changed: '+relative)
        if relative.endswith('.meta'):continue
        key=guid(Path(str(source)+'.meta'))
        target=by_guid.get(key,project/'Assets/Official/Home/BottomBar'/relative)
        owned=target.is_relative_to(project/'Assets/Official/Home/BottomBar')
        if target.exists() and not owned and not equivalent(source.read_bytes(),target.read_bytes()):
            raise ValueError('Different native assets use the same GUID: '+str(target))
        if not target.exists() or owned:
            target.parent.mkdir(parents=True,exist_ok=True)
            shutil.copy2(source,target);shutil.copy2(Path(str(source)+'.meta'),Path(str(target)+'.meta'))
        by_guid[key]=target
    catalog=project/'Assets/Game/Resources/Home/OfficialHomeBottomBarAssetCatalog.asset'
    catalog.parent.mkdir(parents=True,exist_ok=True);catalog.write_bytes(catalog_bytes(receipt))
    Path(str(catalog)+'.meta').write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:home-bottom-bar-catalog').hexdigest()
        +'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n','utf8',newline='\n')
    fixture=ROOT/'scripts/fixtures/home_bottom_bar_native_validation.cs'
    if fixture.exists():shutil.copy2(fixture,project/'Assets/Editor/HomeBottomBarNativeValidation.cs')
    assembly=project/'Assets/Editor/Validation.asmdef';data=json.loads(assembly.read_bytes())
    data['references']=sorted(set(data['references'])|{'Zjcs.Official.HomeHudUI','Zjcs.Offline.HomeUI'})
    assembly.write_text(json.dumps(data,indent=2),'utf8')
    print('Staged original bottom bar:',len(receipt['outputs']),'files',flush=True)
    return project

if __name__=='__main__':stage(validation_directory())
