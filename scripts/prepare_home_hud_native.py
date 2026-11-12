"""Assemble original HomeUI while applying the confirmed offline team/chat scope."""
import copy
import hashlib
import json
from pathlib import Path
import re

from prepare_home_bottom_bar_native import HomeBottomBarBinding
from prepare_partner_gift_native import PartnerGiftBinding
from prepare_partner_card_native import ROOT,guid,sha,script_id
from prepare_current_partner_native import identity
from npc_travel_language import language
from validation_workspace import validation_directory
from official_manifest import read_asset_manifest


class HomeHudBinding(HomeBottomBarBinding):
    def replace(self,value):
        if isinstance(value,dict) and value==dict(fileID=114107629493147155,guid='11d8c212077eef141ac6e6de03ef463e',type=2):
            return dict(value,guid=hashlib.md5(b'zjcs:partner-current:TaskListItem.prefab').hexdigest(),type=3)
        return super().replace(value)

    def register_scripts(self):
        super().register_scripts()
        names=('home-avatar','home-bottom-bar','home-function-entry','home-function-menu',
               'home-hud-infrastructure','home-player-info','home-player-panel','home-result-ui',
               'home-scene-ui','home-screen','home-social-ui','home-task-ui','home-top-bar')
        for name in names:
            receipt_path=ROOT/'assets/reference'/(name+'-native-source-manifest.json')
            receipt=json.loads(receipt_path.read_bytes());assert receipt['manifest']=='884_156692'
            self.inputs[receipt_path.relative_to(ROOT).as_posix()]=sha(receipt_path.read_bytes())
            for name,row in receipt['script_bindings'].items():
                path=ROOT/row['path'];meta=Path(str(path)+'.meta')
                assert sha(path.read_bytes())==row['sha256'] and sha(meta.read_bytes())==row['meta_sha256'],path
                ref=row['pointer'];namespace,_,short=name.rpartition('.')
                previous=self.scripts.get(name)
                if previous is not None and previous!=ref and name in ('PreviewTool','SafeArea','ShowRemainTimeInfo'):
                    # Both PreviewTool ports are the original empty runtime
                    # class. Bind SafeArea explicitly to the complete home port below.
                    continue
                assert previous is None or previous==ref,(name,previous,ref)
                self.scripts[name]=ref;self.class_ids[script_id(namespace,short)]=ref
                self.script_guids.add(ref['guid']);self.script_sources[name]=row
        paths={
            'HomeUI':'HomeMainPanelUI/HomeUI.cs',
            'GameEndUIHandlerInHome':'HomeResultUI/GameEndUIHandlerInHome.cs',
            'HomeUIActivityEntryController':'HomeHudInfrastructureUI/HomeUIActivityEntryController.cs',
            'ChatQuickMsgEntryController':'HomeMainUiControls/ChatQuickMsgEntryController.cs',
            'MainTaskFunctionLimit':'HomeMainUiControls/MainTaskFunctionLimit.cs'}
        paths['SafeArea']='HomeScreenUI/SafeArea.cs'
        paths['ShowRemainTimeInfo']='HomeTaskUI/ShowRemainTimeInfo.cs'
        for name,relative in paths.items():
            path=ROOT/'unity/Assets/Game/Runtime/Official'/relative;meta=Path(str(path)+'.meta')
            ref=dict(fileID=11500000,guid=guid(meta),type=3);self.scripts[name]=ref
            self.class_ids[script_id('',name)]=ref;self.script_guids.add(ref['guid'])
            self.script_sources[name]=dict(path=path.relative_to(ROOT).as_posix(),pointer=ref,
                sha256=sha(path.read_bytes()),meta_sha256=sha(meta.read_bytes()))

    def tip_address(self,obj,original_type):
        key=identity(obj)
        if key in self.original_refs:return self.original_refs[key]
        result=super().tip_address(obj,original_type)
        previous=result['guid'];key_guid=hashlib.md5(('zjcs:home-hud-tip-address:'+key).encode()).hexdigest()
        result['guid']=key_guid;self.script_guids.add(key_guid)
        payload_guid=hashlib.md5(b'zjcs:home-hud-tip-source-objects').hexdigest();self.script_guids.add(payload_guid)
        for index,(path,asset_guid,data) in enumerate(self.queue):
            if asset_guid==previous:
                self.queue[index]=(path,key_guid,data)
                for doc in data.values():doc['MonoBehaviour']['sourceGraphData']['guid']=payload_guid
        self.proof[key]['guid']=key_guid
        return result

    def friendship_templates(self):
        self.recovery.recover('MainCanvas',self.work/'hud-native',
            bundle='defaultpackage_assets_prefabs_ui_main.bundle',native_relative='Prefabs/UI/Main/MainCanvas.prefab')
        self.recovery.recover('TaskListItem',self.work/'hud-native',
            bundle='defaultpackage_assets_prefabs_ui_task.bundle',native_relative='Prefabs/UI/Task/Common/TaskListItem.prefab')
        self.recovery.finalize();native,_,original,_=self.recovery.outputs[0]
        panel=copy.deepcopy(original);docs=panel['documents']
        component,=[row for row in panel['scripts'] if row['type']=='HomeUI']
        root=docs[component['nativeFileID']]['MonoBehaviour']['m_GameObject']['fileID']
        selected,removed=set(),set();closures=[]
        closed={'TeamMiniPanel','ApplyConfirmBtn','ChatMiniPanel','FunctionEntryChatQuick'}
        def visit(go,path='',remove=False):
            obj=docs[go]['GameObject'];path+='/'+obj['m_Name']
            if not remove and obj['m_Name'] in closed:
                closures.append(dict(native_game_object=go,path=path,policy='Confirmed removal of real-player teams and chat; docs/features/offline-access.md and settings-and-save.md.'))
                remove=True
            target=removed if remove else selected
            target.add(go);target.update(c['component']['fileID'] for c in obj['m_Component'])
            tr=next(iter(docs[obj['m_Component'][0]['component']['fileID']].values()))
            for child in tr['m_Children']:visit(next(iter(docs[child['fileID']].values()))['m_GameObject']['fileID'],path,remove)
        visit(root)
        assert len(selected)==651 and len(closures)==4,(len(selected),closures)
        panel['root']=root;panel['documents']={key:value for key,value in docs.items() if key in selected}
        panel['scripts']=[row for row in panel['scripts'] if row['nativeFileID'] in selected]
        panel['objectMap']={key:value for key,value in panel['objectMap'].items() if value in selected}
        transform=next(iter(docs[docs[root]['GameObject']['m_Component'][0]['component']['fileID']].values()))
        previous=transform['m_Father'];transform['m_Father']=dict(fileID=0)
        pruned_references=[]
        def prune(value,path):
            if isinstance(value,dict):
                if set(value)=={'fileID'} and value['fileID']:
                    if value['fileID'] in removed:
                        pruned_references.append(dict(path=path,original=value['fileID']));return dict(fileID=0)
                    assert value['fileID'] in selected,(path,value)
                for key,child in value.items():
                    if key=='m_Children':
                        value[key]=[item for item in child if item['fileID'] not in removed]
                    else:value[key]=prune(child,path+'/'+str(key))
            elif isinstance(value,list):
                for index,child in enumerate(value):value[index]=prune(child,path+'/'+str(index))
            return value
        prune(panel['documents'],'HomeUI')
        self.template_selection=dict(source_root=next(key for key,value in panel['objectMap'].items() if value==root),
            original_parent=previous,detached_parent=dict(fileID=0),mapped_objects=len(selected),
            removed_objects=len(removed),closed_subtrees=closures,pruned_references=pruned_references,
            adaptation='Retain original HomeUI geometry/components/animations; remove only confirmed offline team/chat subtrees. External task template retained in full.')
        for source,_,_,_ in self.recovery.outputs:
            for path in (source,Path(str(source)+'.meta')):self.inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
        for name in ('offline-access.md','settings-and-save.md'):
            path=ROOT/'docs/features'/name;self.inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
        cache=self.recovery.proof['component_cache'];self.inputs[cache['path']]=cache['sha256']
        task=self.recovery.outputs[1][2]
        missing={'.'.join(filter(None,(s['namespace'],s['type']))) for item in (panel,task) for s in item['scripts']}-self.scripts.keys()
        assert not missing,missing
        self.tip_source_objects=getattr(self,'tip_source_objects',{})
        self.entry_text_keys={doc['MonoBehaviour']['myKey'] for item in (panel,task) for doc in item['documents'].values()
            if doc.get('MonoBehaviour',{}).get('myKey')}
        return [('HomeHud',panel),('TaskListItem',task)]

    def build(self):
        PartnerGiftBinding.build(self)
        receipt=json.loads((self.work/'partner-gift-native-source-manifest.json').read_bytes());folder=self.work/'gift-assets'
        records=read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage',self.recovery.apk.archive))
        config=self.recovery.apk.asset('textmesh_pro_rawfonts_fontassetfontconfig','/FontAssetFontConfig.json')
        definition=json.loads(config.read().m_Script)['FontAssetInfoDict']['MPLUSRounded1c-Medium SDF']
        row,=[row for row in records if row['path']==definition['FontPath'] or row['address']==definition['FontPath']]
        obj,=[pointer.deref() for name,pointer in self.recovery.apk.container(row['bundle'])
              if name.casefold()==row['path'].casefold() and pointer.deref().type.name=='Font']
        raw=bytes(obj.read(check_read=False).m_FontData)
        assert len(raw)==definition['Length'] and hashlib.md5(raw).hexdigest()==definition['Hash']
        key=hashlib.md5(('zjcs:partner-source-font:'+identity(obj)).encode()).hexdigest()
        font=folder/'Fonts/MPLUSRounded1c-Medium.ttf';font.parent.mkdir(parents=True,exist_ok=True);font.write_bytes(raw)
        meta=Path(str(font)+'.meta');meta.write_text('fileFormatVersion: 2\nguid: '+key+'\nTrueTypeFontImporter:\n  externalObjects: {}\n  serializedVersion: 4\n  fontSize: 16\n  includeFontData: 1\n','utf8',newline='\n')
        for item in (font,meta):receipt['outputs'][item.relative_to(folder).as_posix()]=sha(item.read_bytes())
        receipt['source_font']=dict(pointer=dict(fileID=12800000,guid=key,type=3),definition=definition,
            source=self.recovery.apk.provenance(obj),original=identity(obj),original_sha256=sha(obj.get_raw_data()),font_data_sha256=sha(raw),
            config_source=self.recovery.apk.provenance(config),config_sha256=sha(config.get_raw_data()))
        for name,payload,key in [('home-hud-tip-source-objects.json',dict(manifest=self.recovery.apk.version,
            scope='Complete original dependency payload; selector address binding does not evaluate conditions.',objects=self.tip_source_objects),
            hashlib.md5(b'zjcs:home-hud-tip-source-objects').hexdigest())]:
            path=folder/name;path.write_text(json.dumps(payload,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
            meta=Path(str(path)+'.meta');meta.write_text('fileFormatVersion: 2\nguid: '+key+'\nTextScriptImporter:\n  externalObjects: {}\n','utf8',newline='\n')
            for item in (path,meta):receipt['outputs'][item.name]=sha(item.read_bytes())
        texts,sources=language(self.entry_text_keys)
        missing=self.entry_text_keys-texts.keys()
        assert not missing-{'{0}','{0}/{1}'},missing
        receipt['inputs'].update(sources);receipt['texts']=texts
        receipt['literal_format_keys']=sorted(missing)
        for item in ('partner-gift-texts.json','partner-gift-texts.json.meta'):
            receipt['outputs'].pop(item);(folder/item).unlink()
        text_path=folder/'home-hud-texts.json'
        text_path.write_text(json.dumps(dict(manifest=self.recovery.apk.version,
            texts=[dict(key=key,text=value) for key,value in sorted(texts.items())]),
            ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        text_meta=Path(str(text_path)+'.meta')
        text_meta.write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:home-hud-texts').hexdigest()+
            '\nTextScriptImporter:\n  externalObjects: {}\n','utf8',newline='\n')
        for item in (text_path,text_meta):receipt['outputs'][item.name]=sha(item.read_bytes())
        used=set()
        for name in receipt['outputs']:
            path=folder/name
            if path.suffix!='.meta' and path.read_bytes().startswith(b'%YAML'):
                used.update(re.findall(r'm_Script: \{[^\n}]*guid: ([0-9a-f]{32})',path.read_text('utf8')))
        receipt['script_bindings']={name:row for name,row in receipt['script_bindings'].items() if row['pointer']['guid'] in used}
        receipt.update(generator_sha256=sha(Path(__file__).read_bytes()),
            tip_source_object_count=len(self.tip_source_objects),
            scope='Original HomeUI with confirmed offline team/chat removals and full external task template. Production local services and runtime verification remain required.')
        (self.work/'home-hud-native-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        print('Original home HUD:',len(receipt['outputs']),'outputs;',len(self.tip_source_objects),'tip source objects',flush=True)


if __name__=='__main__':HomeHudBinding(validation_directory()).build()
