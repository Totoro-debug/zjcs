"""Transfer selected native home avatar assets into an existing Unity project."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import yaml

from official_apk import ROOT
from prepare_partner_card_native import guid, sha, Dumper
from prepare_home_bottom_bar_validation import equivalent
from prepare_partner_input_dialog import metadata
from validation_workspace import validation_directory, require_temporary_unity_project
from validation_assemblies import stage_runtime_modules


def catalog_bytes(receipt):
    script=ROOT/'unity/Assets/Game/Runtime/Official/HomeHudUI/OfficialHomeAvatarAssetCatalog.cs'
    fields=dict(m_ObjectHideFlags=0,m_CorrespondingSourceObject=dict(fileID=0),m_PrefabInstance=dict(fileID=0),
        m_PrefabAsset=dict(fileID=0),m_GameObject=dict(fileID=0),m_Enabled=1,m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000,guid=guid(Path(str(script)+'.meta')),type=3),
        m_Name='OfficialHomeAvatarAssetCatalog',m_EditorClassIdentifier='',
        prefab=receipt['roots']['PublicPlayerIconHome'],
        sounds=[dict(eventId=row['event']['id'],clip=row['pointer']) for row in receipt['audio'].values()])
    return ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'
        +yaml.dump(dict(MonoBehaviour=fields),Dumper=Dumper,sort_keys=False,allow_unicode=True,width=1000000)).encode('utf8')


def transfer(work, project, *, install=False):
    if not install:project=require_temporary_unity_project(project)
    receipt = json.loads((work/'home-avatar-native-source-manifest.json').read_bytes())
    generator = ROOT/'scripts/prepare_home_avatar_native.py'
    if sha(generator.read_bytes()) != receipt['generator_sha256']:
        raise ValueError('Regenerate changed original avatar source')
    for relative, expected in {**receipt['inputs'], **receipt['runtime_code']}.items():
        if sha((ROOT/relative).read_bytes()) != expected:
            raise ValueError('Original avatar dependency changed: '+relative)
    for row in receipt['script_bindings'].values():
        path = ROOT/row['path']
        if sha(path.read_bytes()) != row['sha256'] or sha(Path(str(path)+'.meta').read_bytes()) != row['meta_sha256']:
            raise ValueError('Original avatar script changed: '+row['path'])
    destination = project/'Assets/Official/Home/UI/Avatar'
    existing = {}
    for meta in (project/'Assets').rglob('*.meta'):
        source = Path(str(meta)[:-5])
        if not source.is_file(): continue
        key = guid(meta)
        if key in existing: raise ValueError('Existing Unity GUID collision: '+key)
        existing[key] = source
    outputs, reused, plan = {}, [], []
    for relative, expected in receipt['outputs'].items():
        source = work/'gift-assets'/relative
        if sha(source.read_bytes()) != expected: raise ValueError('Staged avatar bytes changed: '+relative)
        if source.suffix == '.meta': continue
        key = guid(Path(str(source)+'.meta'))
        target = existing.get(key, destination/relative)
        if target.exists():
            if not equivalent(source.read_bytes(), target.read_bytes()):
                raise ValueError('Preserve different native resource: '+str(target))
            if not target.is_relative_to(destination):
                reused.append(dict(path=target.relative_to(ROOT).as_posix() if install else target.relative_to(project).as_posix(),
                    guid=key, sha256=sha(target.read_bytes()), staged_sha256=expected))
        else:
            plan.extend(((source.read_bytes(),target),(Path(str(source)+'.meta').read_bytes(),Path(str(target)+'.meta'))))
        existing[key] = target
        if target.is_relative_to(destination):
            for item in (target, Path(str(target)+'.meta')):
                rel = item.relative_to(ROOT).as_posix() if install else item.relative_to(project).as_posix()
                staged = source if item == target else Path(str(source)+'.meta')
                outputs[rel] = sha(item.read_bytes()) if item.exists() else sha(staged.read_bytes())
    catalog = project/'Assets/Game/Resources/Home/OfficialHomeAvatarAssetCatalog.asset'
    raw = catalog_bytes(receipt)
    if catalog.exists() and catalog.read_bytes() != raw: raise ValueError('Preserve different native avatar catalog')
    plan.append((raw,catalog))
    meta = Path(str(catalog)+'.meta')
    raw_meta = ('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:home-avatar-catalog').hexdigest()
        +'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n').encode('utf8')
    if meta.exists() and meta.read_bytes() != raw_meta: raise ValueError('Preserve existing catalog identity')
    plan.append((raw_meta,meta))
    for raw, target in plan:
        target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(raw)
    for folder in (destination,*[p for p in destination.rglob('*') if p.is_dir()]):
        item = metadata(folder,'DefaultImporter',True)
        rel = item.relative_to(ROOT).as_posix() if install else item.relative_to(project).as_posix()
        outputs[rel] = sha(item.read_bytes())
    for item in (catalog,meta):
        rel = item.relative_to(ROOT).as_posix() if install else item.relative_to(project).as_posix()
        outputs[rel] = sha(item.read_bytes())
    if install:
        receipt.update(installed_output_sha256=outputs, reused=reused,
            installed_root=destination.relative_to(ROOT).as_posix())
        (ROOT/'assets/reference/home-avatar-native-source-manifest.json').write_text(
            json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Original avatar transfer:',len(outputs),'files; reused',len(reused),flush=True)
    return receipt


def stage_fixture(project):
    if not (project/'Assets/Game/Resources/ProfessionUI').is_dir():
        raise ValueError('Use a full owned project copy containing the original portrait resource closure.')
    stage_runtime_modules(project,ROOT/'unity/Assets/Game/Runtime',['Zjcs.Offline.HomeUI','Zjcs.Offline.CharacterCreation','Zjcs.Offline.ProfessionUI'])
    folder=project/'Assets/HomeAvatarValidation';folder.mkdir(parents=True,exist_ok=True)
    shutil.copy2(ROOT/'scripts/fixtures/home_avatar_native_validation.cs',folder/'HomeAvatarNativeValidation.cs')
    references=['Zjcs.Offline.HomeUI','Zjcs.Official.HomeHudUI','Zjcs.Offline.PartnersUI','Zjcs.Offline.ProfessionUI',
        'Zjcs.Offline.Progression','Zjcs.Offline.CharacterCreation','Zjcs.Offline.State','Zjcs.Official.Partners',
        'Zjcs.Official.ProfessionUI','Zjcs.Official.TeamUI','Zjcs.Official.WorldUI','UnityEngine.UI',
        'Unity.RenderPipelines.Universal.Runtime','Unity.RenderPipelines.Core.Runtime']
    (folder/'HomeAvatarValidation.asmdef').write_text(json.dumps(dict(name='Zjcs.HomeAvatar.NativeValidation',
        references=references,includePlatforms=['Editor']),indent=2)+'\n','utf8',newline='\n')


if __name__=='__main__':
    parser=argparse.ArgumentParser(__doc__);parser.add_argument('--install',action='store_true');parser.add_argument('--project',type=Path);args=parser.parse_args()
    work=validation_directory()
    if not args.install and args.project is None:parser.error('Supply --install or --project pointing to an owned full-project copy with the original portrait resources.')
    if args.install:transfer(work,ROOT/'unity',install=True)
    else:transfer(work,args.project);stage_fixture(args.project)
