"""Recover the complete official FunctionEntryDialog and native dependencies."""
import hashlib
import json
from pathlib import Path
import re

from prepare_home_bottom_bar_native import HomeBottomBarBinding
from prepare_partner_gift_native import PartnerGiftBinding
from prepare_partner_card_native import ROOT,guid,sha,script_id
from prepare_current_partner_native import identity
from npc_travel_language import language
from prepare_home_function_menu_runtime import NAMES
from validation_workspace import validation_directory


class HomeFunctionMenuBinding(HomeBottomBarBinding):
    def register_scripts(self):
        super().register_scripts()
        runtime=ROOT/'unity/Assets/Game/Runtime/Official'
        paths={name:runtime/'HomeFunctionMenuUI'/(name+'.cs') for name in (*NAMES,
            'FunctionEntryDialog','ActivityRecommendItemView','ActivityRecommendItemCell','ActivityRecommendEventUpItem','WikiEntry','PredownloadEntry','TimeInfoUI')}
        paths.update({'FancyScrollView.Scroller':runtime/'SkillsUI/Scroller.cs',
            'UGUIJszx.GridLayoutGroupJSZX':runtime/'WorldUI/GridLayoutGroupJSZX.cs'})
        for name,path in paths.items():
            meta=Path(str(path)+'.meta');ref=dict(fileID=11500000,guid=guid(meta),type=3)
            self.scripts[name]=ref;namespace,_,short=name.rpartition('.')
            self.class_ids[script_id(namespace,short)]=ref;self.script_guids.add(ref['guid'])
            self.script_sources[name]=dict(path=path.relative_to(ROOT).as_posix(),pointer=ref,
                sha256=sha(path.read_bytes()),meta_sha256=sha(meta.read_bytes()))

    def tip_address(self,obj,original_type):
        key=identity(obj)
        if key in self.original_refs:return self.original_refs[key]
        result=super().tip_address(obj,original_type)
        previous=result['guid'];key_guid=hashlib.md5(('zjcs:home-menu-tip-address:'+key).encode()).hexdigest()
        result['guid']=key_guid;self.script_guids.add(key_guid)
        payload_guid=hashlib.md5(b'zjcs:home-menu-tip-source-objects').hexdigest();self.script_guids.add(payload_guid)
        for index,(path,asset_guid,data) in enumerate(self.queue):
            if asset_guid==previous:
                self.queue[index]=(path,key_guid,data)
                for doc in data.values():doc['MonoBehaviour']['sourceGraphData']['guid']=payload_guid
        self.proof[key]['guid']=key_guid
        return result

    def friendship_templates(self):
        self.recovery.recover('FunctionEntryDialog',self.work/'menu-dialog-native',
            bundle='defaultpackage_assets_prefabs_ui_functionentry.bundle',
            native_relative='Prefabs/UI/FunctionEntry/FunctionEntryDialog/FunctionEntryDialog.prefab')
        panel=self.recovery.outputs[0][2]
        # The carousel references independent cell/dot prefabs. Recover their
        # actual CAB objects and native GUIDs before resolving the graph.
        for key,ref in panel['external'].items():
            obj=self.original(key)
            if obj.type.name!='GameObject':continue
            native=self.native_path(ref['guid'],obj)
            self.recovery.recover(obj.read(check_read=False).m_Name,self.work/'menu-dialog-native',
                bundle=self.recovery.apk.provenance(obj)['bundle'],native_relative=native.relative_to(self.native).as_posix())
        self.recovery.finalize()
        native,_,panel,record=self.recovery.outputs[0]
        self.home_template_pointers={}
        for native,_,data,_ in self.recovery.outputs:
            template_guid=hashlib.md5(('zjcs:partner-current:'+native.stem+'.prefab').encode()).hexdigest()
            self.home_template_pointers.update({key:dict(fileID=file_id,guid=template_guid,type=3) for key,file_id in data['objectMap'].items()})
            self.script_guids.add(template_guid)
            for path in (native,Path(str(native)+'.meta')):self.inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
        self.prefab_object_pointers.update(self.home_template_pointers)
        cache=self.recovery.proof['component_cache'];self.inputs[cache['path']]=cache['sha256']
        missing={'.'.join(filter(None,(s['namespace'],s['type']))) for s in panel['scripts']}-self.scripts.keys()
        assert not missing,missing
        self.empty_strings=[]
        def restore(original,converted,address,owner):
            if isinstance(original,dict) and isinstance(converted,dict):
                for key in original.keys() & converted.keys():
                    if converted[key] is None and original[key] in ('',[],{}):
                        converted[key]=original[key];self.empty_strings.append(dict(original=owner,field=address+'/'+key,value=original[key]))
                    else:restore(original[key],converted[key],address+'/'+key,owner)
            elif isinstance(original,list) and isinstance(converted,list) and len(original)==len(converted):
                for i,(left,right) in enumerate(zip(original,converted)):restore(left,right,address+'/'+str(i),owner)
        for script in panel['scripts']:
            owner=self.original(script['original']);original=self.recovery.fields(owner)
            fields=panel['documents'][script['nativeFileID']]['MonoBehaviour']
            restore(original,fields,'',script['original'])
            if script['type']=='PopupWindowSound':
                for field in ('ShowSound','HideSound'):fields[field]=self.sound(original[field],owner)
        self.entry_text_keys={doc['MonoBehaviour']['myKey'] for doc in panel['documents'].values() if doc.get('MonoBehaviour',{}).get('myKey')}
        self.template_selection=dict(source=record['originalRoot'],mapped_objects=len(panel['objectMap']),
            scope='Complete original FunctionEntryDialog, all components and hierarchy retained.')
        return [(native.stem,data) for native,_,data,_ in self.recovery.outputs]

    def build(self):
        PartnerGiftBinding.build(self)
        receipt=json.loads((self.work/'partner-gift-native-source-manifest.json').read_bytes());folder=self.work/'gift-assets'
        payload=dict(manifest=self.recovery.apk.version,scope='Complete original dependency payload; selector address binding does not evaluate conditions.',objects=self.tip_source_objects)
        texts,sources=language(self.entry_text_keys);missing=self.entry_text_keys-texts.keys()
        assert not missing-{'{0}'},missing
        receipt['inputs'].update(sources);receipt['texts']=texts;receipt['literal_format_keys']=sorted(missing)
        for name,key,data in [('home-menu-tip-source-objects.json',b'zjcs:home-menu-tip-source-objects',payload),
            ('home-function-menu-texts.json',b'zjcs:home-function-menu-texts',dict(manifest=self.recovery.apk.version,texts=[dict(key=k,text=v) for k,v in sorted(texts.items())]))]:
            path=folder/name;path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
            meta=Path(str(path)+'.meta');meta.write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(key).hexdigest()+'\nTextScriptImporter:\n  externalObjects: {}\n','utf8',newline='\n')
            for item in (path,meta):receipt['outputs'][item.name]=sha(item.read_bytes())
        for name in ('partner-gift-texts.json','partner-gift-texts.json.meta'):
            receipt['outputs'].pop(name);(folder/name).unlink()
        used=set()
        for name in receipt['outputs']:
            path=folder/name
            if path.suffix!='.meta' and path.read_bytes().startswith(b'%YAML'):
                used.update(re.findall(r'm_Script: \{[^\n}]*guid: ([0-9a-f]{32})',path.read_text('utf8')))
        receipt['script_bindings']={name:row for name,row in receipt['script_bindings'].items() if row['pointer']['guid'] in used}
        for name in ('prepare_home_bottom_bar_native.py','prepare_home_repair_native.py','prepare_partner_gift_native.py'):
            path=ROOT/'scripts'/name;receipt['inputs'][path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
        receipt.update(generator_sha256=sha(Path(__file__).read_bytes()),tip_source_object_count=len(self.tip_source_objects),
            original_empty_value_restorations=self.empty_strings,
            scope='Complete original function menu native hierarchy and dependency staging. Local hosts, runtime validation and formal HUD integration remain required.')
        (self.work/'home-function-menu-native-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        print('Original function menu:',len(receipt['outputs']),'outputs;',len(self.tip_source_objects),'tip source objects',flush=True)


if __name__=='__main__':HomeFunctionMenuBinding(validation_directory()).build()
