"""Recover the exact complete PublicPlayerInfo and its two status-icon prefabs."""
import copy
import hashlib
import json
from pathlib import Path
import re
import shutil

from official_apk import ROOT
from official_manifest import read_asset_manifest
from prepare_current_partner_native import documents
from prepare_home_avatar_native import HomeAvatarBinding
from prepare_home_player_panel_runtime import build as runtime
from prepare_partner_card_native import guid, script_id, sha
from prepare_partner_gift_native import PartnerGiftBinding
from validation_workspace import validation_directory
from npc_travel_language import language


class HomePlayerPanelBinding(HomeAvatarBinding):
    def bind_dynamic_sprite(self, asset_guid):
        if not hasattr(self,'dynamic_sprites_by_guid'):
            self.dynamic_sprites_by_guid={}
            self.asset_manifest=read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage',self.recovery.apk.archive))
        if asset_guid in self.dynamic_sprites_by_guid:return
        record,=[row for row in self.asset_manifest if row['guid']==asset_guid]
        obj,=[p.deref() for path,p in self.recovery.apk.container(record['bundle'])
            if path.casefold()==record['path'].casefold() and p.deref().type.name=='Sprite']
        key,source_id=self.recovery.fields(obj)['m_RenderDataKey']
        render_guid=''.join(format(key['data['+str(i)+']'],'08x')[::-1] for i in range(4))
        if render_guid!=asset_guid:raise ValueError('Original dynamic sprite GUID differs')
        name=self.name_key(obj.read(check_read=False).m_Name)
        matches=set()
        for key in [name]+[name+str(i) for i in range(100)]:
            for candidate in self.by_name.get(key,[]):
                if candidate.suffix=='.asset':
                    docs=documents(candidate.read_text('utf8'))
                    if len(docs)==1 and next(iter(docs.values())).get('Sprite',{}).get('m_RenderDataKey')=={render_guid:source_id}:
                        matches.add(candidate)
        from prepare_current_partner_native import identity
        # Several CAB exports can carry the same original render key. Use the
        # export already referenced by this recovered native hierarchy.
        referenced=[p for p in matches if guid(Path(str(p)+'.meta')) in self.objects
            and identity(self.objects[guid(Path(str(p)+'.meta'))])==identity(obj)]
        if not referenced:
            canonical=self.recovery.native/Path(record['path'].split('Assets/',1)[-1]).with_suffix('.asset')
            if canonical in matches:referenced=[canonical]
        if len(referenced)!=1:raise ValueError('Resolve original referenced sprite export: '+repr((record,sorted(str(p) for p in matches),referenced)))
        candidate,=referenced;source_guid=guid(Path(str(candidate)+'.meta'))
        previous=self.objects.get(source_guid)
        if previous is not None and identity(previous)!=identity(obj):raise ValueError('Original sprite identity collision')
        self.objects[source_guid]=obj;pointer=self.load(source_guid)
        self.dynamic_sprites_by_guid[asset_guid]=dict(pointer=pointer,manifest_asset=record,original=identity(obj),
            original_sha256=sha(obj.get_raw_data()),source=self.recovery.apk.provenance(obj))

    def register_scripts(self):
        super().register_scripts()
        base=ROOT/'unity/Assets/Game/Runtime/Official'
        aliases={name:'HomePlayerPanelUI/'+name for name in ('PublicPlayerInfoUI','MainPlayerHpImage','PlayerHpRedPoint',
            'PlayerBlessLevelViewController','PlayerBuffContentUIController','PlayerBuffContentUI',
            'BuffItemUI','FightStatusUIItem','BuffItemDurationDecorator','PreviewTool')}
        aliases.update({name:'HomePlayerInfoUI/Home'+name for name in ('PlayerNameText','PlayerLevelText','PlayerCombatRatingText')})
        aliases['BlessPropBtn']='ProfessionUI/ProfessionBlessPropBtn'
        aliases['SlicedFilledImage']='WorldUI/SlicedFilledImage'
        aliases['UGUIJszx.HorizontalLayoutGroupJSZX']='WorldUI/HorizontalLayoutGroupJSZX'
        for name,relative in aliases.items():
            path=base/(relative+'.cs');meta=Path(str(path)+'.meta')
            ref=dict(fileID=11500000,guid=guid(meta),type=3)
            self.scripts[name]=ref
            self.script_sources[name]=dict(path=path.relative_to(ROOT).as_posix(),pointer=ref,
                sha256=sha(path.read_bytes()),meta_sha256=sha(meta.read_bytes()))
            namespace,_,short_name=name.rpartition('.')
            self.class_ids[script_id(namespace,short_name)]=ref;self.script_guids.add(ref['guid'])

    def friendship_templates(self):
        recovery=self.recovery;bundle='defaultpackage_assets_prefabs_ui_main.bundle'
        native=recovery.native/'Prefabs/UI/Main/MainCanvas.prefab'
        docs=documents(native.read_text('utf8'));main=recovery.apk.asset(bundle,'/MainCanvas.prefab')
        def find(go):
            if go.read().m_Name=='PublicPlayerInfo':return go
            for child in go.read().m_Component[0].component.read().m_Children:
                value=find(child.read().m_GameObject.deref())
                if value is not None:return value
            return None
        root,=[key for key,value in docs.items() if value.get('GameObject',{}).get('m_Name')=='PublicPlayerInfo']
        source=find(main)
        if source is None:raise ValueError('Original MainCanvas player panel not found')
        recovery.recover('PublicPlayerInfo',self.work/'player-panel-native',bundle=bundle,
            native_relative='Prefabs/UI/Main/MainCanvas.prefab',source_root=source,native_root_id=root)
        child=docs[root]['GameObject']['m_Component'][0]['component']['fileID'];self.parent_context=[]
        while child:
            rect=docs[child]['RectTransform']
            self.parent_context.append(dict(native_file_id=child,name=docs[rect['m_GameObject']['fileID']]['GameObject']['m_Name'],rect=copy.deepcopy(rect)))
            child=rect['m_Father']['fileID']
        manifest=read_asset_manifest(recovery.apk.source.manifest_bytes('DefaultPackage',recovery.apk.archive))
        self.status_prefabs={}
        for key in ('6906661e5401e28438d9fb3a60ba1a8f','e36fb3e085e775f48a98294acd208971'):
            row,=[r for r in manifest if r['guid']==key]
            relative=row['path'].split('Assets/',1)[-1]
            name=Path(relative).stem
            recovery.recover(name,self.work/'player-panel-native',bundle=row['bundle'],native_relative=relative)
            self.status_prefabs[key]=dict(name=name,manifest_asset=row)
        recovery.finalize()
        self.empty_values=[];self.home_template_pointers={};self.serialization_headers=[]
        def restore(left,right,owner,address=''):
            if isinstance(left,dict) and isinstance(right,dict):
                for key in left.keys()&right.keys():
                    if right[key] is None and (left[key]=='' or left[key]==[] or left[key]=={}):
                        right[key]=left[key];self.empty_values.append(dict(original=owner,field=address+'/'+key,value=left[key]))
                    else:restore(left[key],right[key],owner,address+'/'+key)
            elif isinstance(left,list) and isinstance(right,list) and len(left)==len(right):
                for i,(a,b) in enumerate(zip(left,right)):restore(a,b,owner,address+'/'+str(i))
        result=[]
        for native,output,panel,record in recovery.outputs:
            native_docs=documents(native.read_text('utf8'));name=output.stem
            target_guid=hashlib.md5(('zjcs:partner-current:'+name+'.prefab').encode()).hexdigest()
            self.home_template_pointers.update({key:dict(fileID=file_id,guid=target_guid,type=3) for key,file_id in panel['objectMap'].items()})
            self.script_guids.add(target_guid)
            for file in (native,Path(str(native)+'.meta')):self.inputs[file.relative_to(ROOT).as_posix()]=sha(file.read_bytes())
            for key,ref in panel['external'].items():self.objects[ref['guid']]=self.original(key)
            for script in panel['scripts']:
                owner=self.original(script['original']);fields=recovery.fields(owner)
                converted=panel['documents'][script['nativeFileID']]['MonoBehaviour']
                restore(fields,converted,script['original'])
                if script['namespace']=='UnityEngine.UI' and script['type']=='RawImage':
                    original=fields['m_UVRect'];template=native_docs[script['nativeFileID']]['MonoBehaviour']['m_UVRect']
                    assert template['serializedVersion']==2 and all(original[k]==template[k] for k in original)
                    converted['m_UVRect']=dict(serializedVersion=2,**original)
                    self.serialization_headers.append(dict(original=script['original'],field='m_UVRect',original_value=original,native_serialized_version=2))
                if script['type']=='UIClickSound':self.sound(fields['AkEvent'],owner)
            missing={'.'.join(filter(None,(s['namespace'],s['type']))) for s in panel['scripts']}-self.scripts.keys()
            if missing:raise ValueError('Migrate original complete player-panel scripts: '+repr(sorted(missing)))
            result.append((name,panel))
        cache=recovery.proof['component_cache'];self.inputs[cache['path']]=cache['sha256']
        self.template_selection=dict(scope='Complete PublicPlayerInfo subtree and two original status-icon prefab dependencies; detach root parent only.')
        return result

    def build(self):
        evidence=runtime();PartnerGiftBinding.build(self)
        receipt=json.loads((self.work/'partner-gift-native-source-manifest.json').read_bytes())
        keys={'ui_common_combat_rating_style_empty','ui_common_level_style_3','player_exp_max_desc1','player_exp_max_desc2','player_exp_max_title',
              'ui_bless_raw_power_desc','ui_bless_bless_power_desc','ui_equip_attribuate_kNormalWithBlessKey','ui_equip_attribuate_kUnEffectKey',
              'ui_cooking_buff_remain_times_continuous','ui_cooking_buff_remain_times'}
        texts,sources=language(keys)
        if set(texts)!=keys:raise ValueError('Original player-panel language entries missing: '+repr(keys-texts.keys()))
        receipt['inputs'].update(sources)
        for relative in ('partner-gift-texts.json','partner-gift-texts.json.meta'):receipt['outputs'].pop(relative)
        path=self.work/'gift-assets/home-player-panel-texts.json'
        path.write_text(json.dumps(dict(manifest=self.recovery.apk.version,texts=[dict(key=k,text=texts[k]) for k in sorted(texts)]),ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        meta=Path(str(path)+'.meta');meta.write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:home-player-panel-texts').hexdigest()+'\nTextScriptImporter:\n  externalObjects: {}\n','utf8',newline='\n')
        for file in (path,meta):receipt['outputs'][file.name]=sha(file.read_bytes())
        used=set()
        for relative in receipt['outputs']:
            file=self.work/'gift-assets'/relative
            if file.suffix!='.meta' and file.read_bytes().startswith(b'%YAML'):
                used.update(re.findall(r'm_Script: \{[^\n}]*guid: ([0-9a-f]{32})',file.read_text('utf8')))
            target=self.work/'player-panel-assets'/relative;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(file,target)
        receipt['script_bindings']={key:row for key,row in receipt['script_bindings'].items() if row['pointer']['guid'] in used}
        for row in self.status_prefabs.values():row['pointer']=receipt['roots'][row['name']]
        receipt['inputs'].update(evidence['sources'])
        code=ROOT/'.tools/source-packages/current'/self.recovery.apk.version/'official-code'
        reused_sources=['client-source/'+name+'.cs' for name in ('PlayerIcon','PlayerIconMe','PlayerIconOther','PlayerInfoUI',
            'HeadCharacterPortrait','BlessLevelTextViewChanger','BlessPropBtn')]
        reused_sources+=['framework-firstpass-source/SlicedFilledImage.cs','framework-source/UGUIJszx/HorizontalLayoutGroupJSZX.cs',
                         'framework-source/UGUIJszx/HorizontalOrVerticalLayoutGroupJSZX.cs']
        for relative in reused_sources:
            current=code/relative;previous=ROOT/'.tools/source-packages/official-code'/relative
            if current.read_bytes()!=previous.read_bytes():raise ValueError('Recheck selected shared panel controller: '+relative)
            for file in (current,previous):receipt['inputs'][file.relative_to(ROOT).as_posix()]=sha(file.read_bytes())
        file=ROOT/'assets/reference/home-player-info-native-source-manifest.json'
        receipt['inputs'][file.relative_to(ROOT).as_posix()]=sha(file.read_bytes())
        for name in ('prepare_home_player_panel_runtime.py','prepare_home_player_panel_validation.py','fixtures/home_player_panel_native_validation.cs',
                     'prepare_home_avatar_native.py','prepare_home_bottom_bar_native.py'):
            file=ROOT/'scripts'/name;receipt['inputs'][file.relative_to(ROOT).as_posix()]=sha(file.read_bytes())
        receipt.update(runtime_code=evidence['outputs'],generator_sha256=sha(Path(__file__).read_bytes()),
            original_parent_context=self.parent_context,original_empty_value_restorations=self.empty_values,
            native_serialization_headers=self.serialization_headers,status_prefabs=self.status_prefabs,
            dynamic_sprites=getattr(self,'dynamic_sprites_by_guid',{}),
            scope='Complete original PublicPlayerInfo subtree, original controllers and two status-icon prefabs; typed service interfaces and explicit resource bindings.')
        (self.work/'home-player-panel-native-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        print('Prepared complete original player panel:',len(receipt['outputs']),'files',flush=True)


if __name__=='__main__':
    runtime();HomePlayerPanelBinding(validation_directory()).build()
