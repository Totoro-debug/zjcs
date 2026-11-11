"""Recover the exact MainCanvas FunctionEntryFunc subtree and dependencies."""
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


class HomeFunctionEntryBinding(HomeBottomBarBinding):
    def register_scripts(self):
        super().register_scripts()
        for name in ('FunctionEntrysPanel','MainTaskFunctionLimit','ChatQuickMsgEntryController','TipDotObject'):
            path=ROOT/'unity/Assets/Game/Runtime/Official/HomeMainUiControls'/(name+'.cs')
            meta=Path(str(path)+'.meta');ref=dict(fileID=11500000,guid=guid(meta),type=3)
            self.scripts[name]=ref;self.class_ids[script_id('',name)]=ref;self.script_guids.add(ref['guid'])
            self.script_sources[name]=dict(path=path.relative_to(ROOT).as_posix(),pointer=ref,
                sha256=sha(path.read_bytes()),meta_sha256=sha(meta.read_bytes()))

    def tip_address(self,obj,original_type):
        key=identity(obj)
        if key in self.original_refs:return self.original_refs[key]
        result=super().tip_address(obj,original_type)
        previous=result['guid'];key_guid=hashlib.md5(('zjcs:home-function-tip-address:'+key).encode()).hexdigest()
        result['guid']=key_guid;self.script_guids.add(key_guid)
        payload_guid=hashlib.md5(b'zjcs:home-function-tip-source-objects').hexdigest();self.script_guids.add(payload_guid)
        for index,(path,asset_guid,data) in enumerate(self.queue):
            if asset_guid==previous:
                self.queue[index]=(path,key_guid,data)
                for doc in data.values():doc['MonoBehaviour']['sourceGraphData']['guid']=payload_guid
        self.proof[key]['guid']=key_guid
        return result

    def friendship_templates(self):
        self.recovery.recover('MainCanvas',self.work/'function-entry-native',
            bundle='defaultpackage_assets_prefabs_ui_main.bundle',native_relative='Prefabs/UI/Main/MainCanvas.prefab')
        self.recovery.finalize();native,_,original,_=self.recovery.outputs[0]
        panel=copy.deepcopy(original);docs=panel['documents']
        root,=[key for key,doc in docs.items() if doc.get('GameObject',{}).get('m_Name')=='FunctionEntryFunc']
        selected=set()
        def visit(go):
            selected.add(go);obj=docs[go]['GameObject'];selected.update(c['component']['fileID'] for c in obj['m_Component'])
            tr=next(iter(docs[obj['m_Component'][0]['component']['fileID']].values()))
            for child in tr['m_Children']:visit(next(iter(docs[child['fileID']].values()))['m_GameObject']['fileID'])
        visit(root)
        panel['root']=root;panel['documents']={key:value for key,value in docs.items() if key in selected}
        panel['scripts']=[row for row in panel['scripts'] if row['nativeFileID'] in selected]
        panel['objectMap']={key:value for key,value in panel['objectMap'].items() if value in selected}
        transform=next(iter(docs[docs[root]['GameObject']['m_Component'][0]['component']['fileID']].values()))
        previous=transform['m_Father'];transform['m_Father']=dict(fileID=0)
        def check(value):
            if isinstance(value,dict):
                if set(value)=={'fileID'} and value['fileID']:assert value['fileID'] in selected,value
                for child in value.values():check(child)
            elif isinstance(value,list):
                for child in value:check(child)
        check(panel['documents'])
        self.template_selection=dict(source_root=next(key for key,value in panel['objectMap'].items() if value==root),
            original_parent=previous,detached_parent=dict(fileID=0),mapped_objects=len(selected),
            adaptation='Complete original FunctionEntryFunc subtree; detach only its original parent.')
        for path in (native,Path(str(native)+'.meta')):self.inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
        cache=self.recovery.proof['component_cache'];self.inputs[cache['path']]=cache['sha256']
        missing={'.'.join(filter(None,(s['namespace'],s['type']))) for s in panel['scripts']}-self.scripts.keys()
        assert not missing,missing
        self.entry_text_keys={doc['MonoBehaviour']['myKey'] for doc in panel['documents'].values()
            if doc.get('MonoBehaviour',{}).get('myKey')}
        return [('HomeFunctionEntry',panel)]

    def build(self):
        PartnerGiftBinding.build(self)
        receipt=json.loads((self.work/'partner-gift-native-source-manifest.json').read_bytes());folder=self.work/'gift-assets'
        for name,payload,key in [('home-function-tip-source-objects.json',dict(manifest=self.recovery.apk.version,
            scope='Complete original dependency payload; selector address binding does not evaluate conditions.',objects=self.tip_source_objects),
            hashlib.md5(b'zjcs:home-function-tip-source-objects').hexdigest())]:
            path=folder/name;path.write_text(json.dumps(payload,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
            meta=Path(str(path)+'.meta');meta.write_text('fileFormatVersion: 2\nguid: '+key+'\nTextScriptImporter:\n  externalObjects: {}\n','utf8',newline='\n')
            for item in (path,meta):receipt['outputs'][item.name]=sha(item.read_bytes())
        texts,sources=language(self.entry_text_keys)
        missing=self.entry_text_keys-texts.keys()
        assert not missing-{'{0}'},missing
        receipt['inputs'].update(sources);receipt['texts']=texts
        receipt['literal_format_keys']=sorted(missing)
        for item in ('partner-gift-texts.json','partner-gift-texts.json.meta'):
            receipt['outputs'].pop(item);(folder/item).unlink()
        text_path=folder/'home-function-entry-texts.json'
        text_path.write_text(json.dumps(dict(manifest=self.recovery.apk.version,
            texts=[dict(key=key,text=value) for key,value in sorted(texts.items())]),
            ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        text_meta=Path(str(text_path)+'.meta')
        text_meta.write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:home-function-entry-texts').hexdigest()+
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
            scope='Complete original function-entry subtree and dependencies. Full FunctionEntryDialog, local services and formal HUD integration remain required.')
        (self.work/'home-function-entry-native-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        print('Original function entry:',len(receipt['outputs']),'outputs;',len(self.tip_source_objects),'tip source objects',flush=True)


if __name__=='__main__':HomeFunctionEntryBinding(validation_directory()).build()
