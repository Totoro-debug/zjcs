"""Stage/install current original portrait payloads with explicit lazy Resources bindings."""
import argparse
import hashlib
import json
from pathlib import Path
import re

import yaml
from official_apk import ROOT
from prepare_unity_profession_reward_sprites import Dumper
from validation_workspace import validation_directory


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def guid(path):
    return re.search(rb'^guid: ([0-9a-f]{32})$',path.read_bytes(),re.M)[1].decode()


def asset_guid(relative):
    return hashlib.md5(('zjcs:current-partner-portrait:'+relative).encode()).hexdigest()


def build(work):
    source_path=work/'partner-native-portraits-source-manifest.json'
    proof=json.loads(source_path.read_bytes())
    if proof['manifest']!='884_156692' or len(proof['portraits'])!=54:
        raise ValueError('Review the selected available partner portrait set')
    for name,expected in proof['generator_inputs'].items():
        if sha((ROOT/name).read_bytes())!=expected:raise ValueError('Rebuild the portrait source staging: '+name)
    if sha((ROOT/proof['rule_source']['path']).read_bytes())!=proof['rule_source']['sha256']:
        raise ValueError('Selected partner configuration changed')
    source=work/'native-portraits'
    staged=work/'portrait-assets'
    staged.mkdir(exist_ok=True)
    outputs,inputs,portraits={}, {}, []

    def write(relative,raw):
        path=staged/relative
        path.parent.mkdir(parents=True,exist_ok=True)
        path.write_bytes(raw)
        outputs[relative]=sha(raw)

    def meta(relative,importer,file_id=None):
        text='fileFormatVersion: 2\nguid: '+asset_guid(relative)+'\n'+importer+':\n  externalObjects: {}\n'
        if file_id is not None:text+='  mainObjectFileID: '+str(file_id)+'\n'
        text+='  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
        write(relative+'.meta',text.encode())

    def checked(relative):
        raw=(source/relative).read_bytes()
        if sha(raw)!=proof['outputs'][relative]:raise ValueError('Staged portrait changed: '+relative)
        return raw

    cache=ROOT/'.tools/source-packages/current/884_156692/native-conversion/all-assets/windows-textures'
    script=ROOT/'unity/Assets/Game/Runtime/Offline/PartnersUI/OfficialPartnerPortraitAsset.cs'
    script_guid=guid(Path(str(script)+'.meta'))
    for row in proof['portraits']:
        if 'sharedWithNpc' in row:raise ValueError('Bind the explicit original shared portrait identity before installing')
        npc=row['npcId']
        base='unity/Assets/Official/Partners/Portraits/'+str(npc)
        binary=base+'/portrait.bytes'
        write(binary,checked(row['nativePackage']));meta(binary,'TextScriptImporter')
        configuration=base+'/original-configuration.json'
        atlases=json.loads(checked(row['atlases']))
        write(configuration,(json.dumps(dict(portrait=row,atlases=atlases),ensure_ascii=False,separators=(',',':'),allow_nan=False)+'\n').encode())
        meta(configuration,'TextScriptImporter')
        pages=[]
        for atlas in atlases:
            names=sorted({region['page'] for region in atlas['regions']})
            textures=[texture for material in atlas['materials'] for texture in material['textures'] if texture['property']=='_MainTex']
            if len(names)!=1 or len(textures)!=1:raise ValueError('Bind the original multipage portrait atlas order explicitly')
            texture=textures[0]
            key=sha(texture['original'].encode())
            record_path=cache/'Records'/(key+'.json')
            record=json.loads(record_path.read_bytes())
            if record['source']['object_sha256']!=proof['objects'][texture['original']]['payload_sha256']:
                raise ValueError('Native portrait texture is from another original object')
            if record['original_fields']['m_MipCount']!=texture['mip_count'] or record['original_fields']['m_ColorSpace']!=texture['color_space']:
                raise ValueError('Native portrait mip/color descriptor differs')
            inputs[record_path.relative_to(ROOT).as_posix()]=sha(record_path.read_bytes())
            target='unity/Assets/Official/Partners/Portraits/Textures/'+key+'.asset'
            for output in record['outputs']:
                path=cache/output['path'];raw=path.read_bytes()
                if sha(raw)!=output['sha256']:raise ValueError('Native portrait texture cache changed')
                inputs[path.relative_to(ROOT).as_posix()]=sha(raw)
                write(target+('.meta' if output['path'].endswith('.meta') else ''),raw)
            pages.append(dict(Name=names[0],Texture=dict(fileID=record['file_id'],guid=record['guid'],type=2)))
        resource='unity/Assets/Game/Resources/Partners/Portraits/Npc'+str(npc)+'.asset'
        def vector(value):return {axis.lower():value[axis] for axis in 'XYZ'}
        value=dict(m_ObjectHideFlags=0,m_CorrespondingSourceObject=dict(fileID=0),m_PrefabInstance=dict(fileID=0),
            m_PrefabAsset=dict(fileID=0),m_GameObject=dict(fileID=0),m_Enabled=1,m_EditorHideFlags=0,
            m_Script=dict(fileID=11500000,guid=script_guid,type=3),m_Name='Npc'+str(npc),m_EditorClassIdentifier='',
            manifest='884_156692',npcId=npc,data=dict(fileID=4900000,guid=asset_guid(binary),type=3),
            originalConfiguration=dict(fileID=4900000,guid=asset_guid(configuration),type=3),
            liveOffset=vector(row['liveOffset']),liveScale=vector(row['liveScale']),defaultMix=row['fields']['defaultMix'],pages=pages)
        write(resource,('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'+
            yaml.dump(dict(MonoBehaviour=value),Dumper=Dumper,sort_keys=False,allow_unicode=True,width=1000000)).encode())
        meta(resource,'NativeFormatImporter',11400000)
        portraits.append(dict(npcId=npc,resource='Partners/Portraits/Npc'+str(npc),binary=binary,pages=pages))
    folders=set()
    for relative in list(outputs):
        parent=Path(relative).parent
        while parent.as_posix() not in ('unity/Assets/Official/Partners','unity/Assets/Game/Resources/Partners'):
            folders.add(parent.as_posix());parent=parent.parent
    for folder in sorted(folders):
        write(folder+'.meta',('fileFormatVersion: 2\nguid: '+asset_guid(folder)+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode())
    result=dict(manifest='884_156692',generator_sha256=sha(Path(__file__).read_bytes()),
        portrait_source=proof,inputs=inputs,outputs=outputs,portraits=portraits,
        script_bindings={path.relative_to(ROOT).as_posix():sha(path.read_bytes()) for path in (script,Path(str(script)+'.meta'))},
        runtime_source_sha256={path.relative_to(ROOT).as_posix():sha(path.read_bytes()) for path in (
            ROOT/'unity/Assets/Game/Runtime/Offline/PartnersUI/PartnerNativePortraitPose.cs',
            ROOT/'unity/Assets/Game/Runtime/Offline/PartnersUI/PartnerNativePortraitIk.cs',
            *[ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI'/name for name in (
                'ProfessionNativeSkeletonData.PartnerPortrait.cs','ProfessionNativeSkeletonData.cs',
                'ProfessionNativeBonePose.cs','ProfessionNativeIdleIk.cs','ProfessionNativeIdleTransform.cs',
                'ProfessionNativeAnimationChannels.cs','ProfessionNativeDeformAnimation.cs')])},
        validation_source_sha256={path.relative_to(ROOT).as_posix():sha(path.read_bytes()) for path in (
            ROOT/'scripts/prepare_partner_native_portraits_validation.py',ROOT/'scripts/prepare_partner_portrait_graphics_validation.py',
            ROOT/'scripts/fixtures/partner_native_portraits_validation.cs',ROOT/'scripts/fixtures/partner_portrait_graphics_validation.cs')},
        scope='Current original portrait packages and all native texture mip/sampler fields staged; full live UI and complete constraint/mixing verification remain pending.')
    (work/'partner-portrait-assets-source-manifest.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Staged',len(portraits),'lazy native portraits and',len(outputs),'files')


def install(work):
    proof=json.loads((work/'partner-portrait-assets-source-manifest.json').read_bytes())
    if proof['generator_sha256']!=sha(Path(__file__).read_bytes()):raise ValueError('Rebuild native portrait assets before installing')
    for relative,expected in dict(proof['inputs'],**proof['script_bindings'],**proof['portrait_source']['generator_inputs'],
                                  **proof['runtime_source_sha256'],**proof['validation_source_sha256']).items():
        if sha((ROOT/relative).read_bytes())!=expected:raise ValueError('Portrait input changed: '+relative)
    plan=[]
    for relative,expected in proof['outputs'].items():
        source=work/'portrait-assets'/relative;target=ROOT/relative;raw=source.read_bytes()
        if sha(raw)!=expected:raise ValueError('Portrait output changed: '+relative)
        if target.is_file() and target.read_bytes()!=raw:raise ValueError('Refusing to overwrite native portrait asset: '+relative)
        plan.append((target,raw))
    for target,raw in plan:
        if not target.is_file():target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(raw)
    proof['installed_output_sha256']=proof['outputs']
    proof['scope']='Current native portrait data, complete native texture payloads and lazy Resources bindings installed. Full UI, other solver branches, mixing and Windows Player remain pending.'
    (ROOT/'assets/reference/partner-portrait-assets-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Installed',len(plan),'native portrait asset files')


if __name__=='__main__':
    parser=argparse.ArgumentParser(__doc__);parser.add_argument('--install-staged',action='store_true');args=parser.parse_args()
    (install if args.install_staged else build)(validation_directory())
