"""Recover original quantity-control subtrees from the official quick-buy dialog."""
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


class HomeCountControlsBinding(HomeBottomBarBinding):
    def register_scripts(self):
        super().register_scripts()
        runtime=ROOT/'unity/Assets/Game/Runtime/Official/HomeCountControlsUI'
        paths={name:runtime/(name+'.cs') for name in ('SetCountSlider','SetCountButtonGroup','LongTouch')}
        paths['SlicedFilledImage']=ROOT/'unity/Assets/Game/Runtime/Official/WorldUI/SlicedFilledImage.cs'
        original=ROOT/'.tools/source-packages/current/884_156692/official-code/framework-firstpass-source/SlicedFilledImage.cs'
        self.inputs[original.relative_to(ROOT).as_posix()]=sha(original.read_bytes())
        for name,path in paths.items():
            meta=Path(str(path)+'.meta');ref=dict(fileID=11500000,guid=guid(meta),type=3)
            self.scripts[name]=ref;self.class_ids[script_id('',name)]=ref;self.script_guids.add(ref['guid'])
            self.script_sources[name]=dict(path=path.relative_to(ROOT).as_posix(),pointer=ref,
                sha256=sha(path.read_bytes()),meta_sha256=sha(meta.read_bytes()))

    def friendship_templates(self):
        self.recovery.recover('ShopQuickBuyDialog',self.work/'count-controls-native',
            bundle='defaultpackage_assets_prefabs_ui_shop.bundle',native_relative='Prefabs/UI/Shop/ShopQuickBuyDialog.prefab')
        self.recovery.finalize();native,_,original,_=self.recovery.outputs[0]
        result=[];self.template_selection={};self.entry_text_keys={'ui_common_contrast_1'}
        for name,kind in [('HomeCountSlider','SetCountSlider'),('HomeCountButtons','SetCountButtonGroup')]:
            panel=copy.deepcopy(original);docs=panel['documents']
            component,=[row for row in panel['scripts'] if row['type']==kind]
            root=docs[component['nativeFileID']]['MonoBehaviour']['m_GameObject']['fileID']
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
                    if set(value)=={'fileID'} and value['fileID']:assert value['fileID'] in selected,(kind,value)
                    for child in value.values():check(child)
                elif isinstance(value,list):
                    for child in value:check(child)
            check(panel['documents'])
            parents=[];ancestor=previous['fileID']
            while ancestor:
                rect=docs[ancestor]['RectTransform'];ancestor_go=rect['m_GameObject']['fileID']
                parents.append(dict(name=docs[ancestor_go]['GameObject']['m_Name'],transform=rect))
                ancestor=rect['m_Father']['fileID']
            self.template_selection[name]=dict(original_parent_chain=parents,source_root=next(key for key,value in panel['objectMap'].items() if value==root),
                original_parent=previous,detached_parent=dict(fileID=0),mapped_objects=len(selected),
                adaptation='Complete original quantity-control subtree; detach only its original parent.')
            missing={'.'.join(filter(None,(s['namespace'],s['type']))) for s in panel['scripts']}-self.scripts.keys()
            assert not missing,missing
            self.entry_text_keys.update(doc['MonoBehaviour']['myKey'] for doc in panel['documents'].values() if doc.get('MonoBehaviour',{}).get('myKey'))
            result.append((name,panel))
        for path in (native,Path(str(native)+'.meta')):self.inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
        cache=self.recovery.proof['component_cache'];self.inputs[cache['path']]=cache['sha256']
        return result

    def build(self):
        PartnerGiftBinding.build(self)
        receipt=json.loads((self.work/'partner-gift-native-source-manifest.json').read_bytes());folder=self.work/'gift-assets'
        texts,sources=language(self.entry_text_keys)
        missing=self.entry_text_keys-texts.keys()
        assert not missing-{'{0}'},missing
        receipt['inputs'].update(sources);receipt['texts']=texts
        receipt['literal_format_keys']=sorted(missing)
        for item in ('partner-gift-texts.json','partner-gift-texts.json.meta'):
            receipt['outputs'].pop(item);(folder/item).unlink()
        text_path=folder/'home-count-controls-texts.json'
        text_path.write_text(json.dumps(dict(manifest=self.recovery.apk.version,
            texts=[dict(key=key,text=value) for key,value in sorted(texts.items())]),
            ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        text_meta=Path(str(text_path)+'.meta')
        text_meta.write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:home-count-controls-texts').hexdigest()+
            '\nTextScriptImporter:\n  externalObjects: {}\n','utf8',newline='\n')
        for item in (text_path,text_meta):receipt['outputs'][item.name]=sha(item.read_bytes())
        used=set()
        for name in receipt['outputs']:
            path=folder/name
            if path.suffix!='.meta' and path.read_bytes().startswith(b'%YAML'):
                used.update(re.findall(r'm_Script: \{[^\n}]*guid: ([0-9a-f]{32})',path.read_text('utf8')))
        receipt['script_bindings']={name:row for name,row in receipt['script_bindings'].items() if row['pointer']['guid'] in used}
        receipt.update(generator_sha256=sha(Path(__file__).read_bytes()),
            scope='Original quick-buy quantity controls and dependencies. Complete currency windows, production service assembly and runtime validation remain required.')
        (self.work/'home-count-controls-native-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        print('Original quantity controls:',len(receipt['outputs']),'outputs',flush=True)


if __name__=='__main__':HomeCountControlsBinding(validation_directory()).build()
