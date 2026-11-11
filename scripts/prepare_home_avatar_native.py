"""Recover selected PublicPlayerIconHome and original experience-ring code."""
import hashlib
import json
import re
from pathlib import Path

from official_apk import ROOT
from prepare_current_partner_native import identity, documents
from prepare_home_bottom_bar_native import HomeBottomBarBinding
from prepare_home_experience_runtime import build as experience_runtime
from prepare_partner_card_native import guid, script_id, sha
from prepare_partner_gift_native import PartnerGiftBinding
from validation_workspace import validation_directory


class HomeAvatarBinding(HomeBottomBarBinding):
    def gradient_evidence(self):
        return []

    def register_scripts(self):
        super().register_scripts()
        base=ROOT/'unity/Assets/Game/Runtime/Official'
        aliases={
            'PlayerIcon':'TeamUI/TeamPlayerIcon',
            'PlayerIconOther':'TeamUI/TeamPlayerIconOther',
            'PlayerIconMe':'ProfessionUI/PlayerIconMe',
            'PlayerInfoUI':'ProfessionUI/PlayerInfoUI',
            'HeadCharacterPortrait':'TeamUI/TeamHeadCharacterPortrait',
            'PlayerExpSlider':'HomeHudUI/HomePlayerExpSlider',
            'NoDrawingRayCast':'WorldUI/NoDrawingRayCast',
            'GraphicCapsuleMeshClipper':'WorldUI/GraphicCapsuleMeshClipper',
        }
        for name,relative in aliases.items():
            path=base/(relative+'.cs');meta=Path(str(path)+'.meta')
            ref=dict(fileID=11500000,guid=guid(meta),type=3)
            self.scripts[name]=ref
            self.script_sources[name]=dict(path=path.relative_to(ROOT).as_posix(),pointer=ref,
                sha256=sha(path.read_bytes()),meta_sha256=sha(meta.read_bytes()))
            self.class_ids[script_id('',name)]=ref;self.script_guids.add(ref['guid'])

    def friendship_templates(self):
        recovery=self.recovery
        recovery.recover('PublicPlayerIconHome',self.work/'avatar-native',
            bundle='defaultpackage_assets_prefabs_ui_public.bundle',
            native_relative='Prefabs/UI/Public/PublicPlayerIconHome.prefab')
        recovery.finalize()
        self.empty_values=[];self.home_template_pointers={};self.serialization_headers=[]

        def restore(source,converted,owner,address=''):
            if isinstance(source,dict) and isinstance(converted,dict):
                for key in source.keys()&converted.keys():
                    if converted[key] is None and (source[key]=='' or source[key]==[] or source[key]=={}):
                        converted[key]=source[key]
                        self.empty_values.append(dict(original=owner,field=address+'/'+key,value=source[key]))
                    else:restore(source[key],converted[key],owner,address+'/'+key)
            elif isinstance(source,list) and isinstance(converted,list) and len(source)==len(converted):
                for index,(left,right) in enumerate(zip(source,converted)):
                    restore(left,right,owner,address+'/'+str(index))

        result=[]
        for native,output,panel,record in recovery.outputs:
            native_docs=documents(native.read_text('utf8'))
            name=output.stem;target_guid=hashlib.md5(('zjcs:partner-current:'+name+'.prefab').encode()).hexdigest()
            self.home_template_pointers.update({key:dict(fileID=file_id,guid=target_guid,type=3)
                for key,file_id in panel['objectMap'].items()})
            self.script_guids.add(target_guid)
            for file in (native,Path(str(native)+'.meta')):
                self.inputs[file.relative_to(ROOT).as_posix()]=sha(file.read_bytes())
            for key,ref in panel['external'].items():self.objects[ref['guid']]=self.original(key)
            for key,file_id in panel['objectMap'].items():
                owner=self.original(key)
                self.learn(recovery.fields(owner),next(iter(panel['documents'][file_id].values())),owner)
            for script in panel['scripts']:
                owner=self.original(script['original']);fields=recovery.fields(owner)
                restore(fields,panel['documents'][script['nativeFileID']]['MonoBehaviour'],script['original'])
                if script['namespace']=='UnityEngine.UI' and script['type']=='RawImage':
                    # Binary Rect has four coordinates; Unity text Rect v2 also
                    # needs its serialization header. Without it Unity reads a
                    # zero UV rectangle and SetNativeSize collapses the head.
                    original=fields['m_UVRect'];template=native_docs[script['nativeFileID']]['MonoBehaviour']['m_UVRect']
                    assert set(original)=={'x','y','width','height'} and template['serializedVersion']==2
                    assert all(original[key]==template[key] for key in original)
                    panel['documents'][script['nativeFileID']]['MonoBehaviour']['m_UVRect']=dict(serializedVersion=2,**original)
                    self.serialization_headers.append(dict(original=script['original'],field='m_UVRect',
                        original_value=original,native_serialized_version=2,native_path=native.relative_to(ROOT).as_posix()))
                if script['type']=='UIClickSound':self.sound(fields['AkEvent'],owner)
            missing={'.'.join(filter(None,(s['namespace'],s['type']))) for s in panel['scripts']}-self.scripts.keys()
            if missing:raise ValueError('Migrate original home avatar scripts: '+repr(sorted(missing)))
            result.append((name,panel))
        cache=recovery.proof['component_cache'];self.inputs[cache['path']]=cache['sha256']
        self.template_selection=dict(scope='Complete selected PublicPlayerIconHome, original player controllers, head requestor and experience slider.')
        return result

    def build(self):
        PartnerGiftBinding.build(self)
        receipt=json.loads((self.work/'partner-gift-native-source-manifest.json').read_bytes())
        used=set()
        for relative in receipt['outputs']:
            file=self.work/'gift-assets'/relative
            if file.suffix!='.meta' and file.read_bytes().startswith(b'%YAML'):
                used.update(re.findall(r'm_Script: \{[^\n}]*guid: ([0-9a-f]{32})',file.read_text('utf8')))
        receipt['script_bindings']={key:row for key,row in receipt['script_bindings'].items() if row['pointer']['guid'] in used}
        code=ROOT/'.tools/source-packages/current'/self.recovery.apk.version/'official-code'
        for name in ('PlayerIcon','PlayerIconMe','PlayerIconOther','PlayerInfoUI','HeadCharacterPortrait'):
            current=code/'client-source'/(name+'.cs')
            previous=ROOT/'.tools/source-packages/official-code/client-source'/(name+'.cs')
            if current.read_bytes()!=previous.read_bytes():raise ValueError('Recheck shared avatar source: '+name)
            for file in (current,previous):receipt['inputs'][file.relative_to(ROOT).as_posix()]=sha(file.read_bytes())
        evidence=experience_runtime()
        receipt['inputs'].update(evidence['sources']);receipt['runtime_code']=evidence['outputs']
        for name in ('Official/HomeHudUI/OfficialHomeAvatarAssetCatalog','Offline/HomeUI/OfflineHomeAvatar'):
            for suffix in ('.cs','.cs.meta'):
                file=ROOT/'unity/Assets/Game/Runtime'/(name+suffix)
                receipt['runtime_code'][file.relative_to(ROOT).as_posix()]=sha(file.read_bytes())
        assembly=ROOT/'unity/Assets/Game/Runtime/Offline/HomeUI/Zjcs.Offline.HomeUI.asmdef'
        receipt['runtime_code'][assembly.relative_to(ROOT).as_posix()]=sha(assembly.read_bytes())
        for name in ('prepare_home_experience_runtime.py','prepare_home_avatar_validation.py','fixtures/home_avatar_native_validation.cs',
                     'prepare_home_bottom_bar_native.py','prepare_home_repair_native.py'):
            file=ROOT/'scripts'/name;receipt['inputs'][file.relative_to(ROOT).as_posix()]=sha(file.read_bytes())
        receipt.update(generator_sha256=sha(Path(__file__).read_bytes()),original_empty_value_restorations=self.empty_values,
            native_serialization_headers=self.serialization_headers,
            scope='Selected original native home avatar and experience widget. Local player/reward binding and actual rendering remain to be validated.')
        (self.work/'home-avatar-native-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        print('Prepared original home avatar',len(receipt['outputs']),'files',flush=True)


if __name__=='__main__':
    experience_runtime()
    HomeAvatarBinding(validation_directory()).build()
