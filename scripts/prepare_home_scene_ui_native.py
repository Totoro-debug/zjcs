"""Recover the original HomeUI Guider subtree without rebuilding its layout."""
import copy
import json
from pathlib import Path
import re

from prepare_home_bottom_bar_native import HomeBottomBarBinding
from prepare_partner_gift_native import PartnerGiftBinding
from prepare_partner_card_native import ROOT,guid,sha,script_id
from validation_workspace import validation_directory


class HomeSceneUiBinding(HomeBottomBarBinding):
    def register_scripts(self):
        super().register_scripts()
        for name in ('HomeHangUpDirectionGuider','AwardRetrieveNpcDirectionGuider','HomeScreenShootActive','Home.HomeFadeProxy'):
            path=ROOT/'unity/Assets/Game/Runtime/Official/HomeSceneUiControls'/(name.rsplit('.',1)[-1]+'.cs')
            ref=dict(fileID=11500000,guid=guid(Path(str(path)+'.meta')),type=3)
            namespace,_,short=name.rpartition('.')
            self.scripts[name]=ref;self.class_ids[script_id(namespace,short)]=ref;self.script_guids.add(ref['guid'])
            self.script_sources[name]=dict(path=path.relative_to(ROOT).as_posix(),pointer=ref,
                sha256=sha(path.read_bytes()),meta_sha256=sha(Path(str(path)+'.meta').read_bytes()))

    def friendship_templates(self):
        self.recovery.recover('MainCanvas',self.work/'scene-ui-native',
            bundle='defaultpackage_assets_prefabs_ui_main.bundle',native_relative='Prefabs/UI/Main/MainCanvas.prefab')
        self.recovery.finalize();native,_,original,_=self.recovery.outputs[0]
        panel=copy.deepcopy(original);docs=panel['documents'];root=1177930474251431;selected=set()
        assert docs[root]['GameObject']['m_Name']=='Guider'
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
            adaptation='Complete original Guider subtree; detach only its original parent. Original null NPC guide mask retained.')
        for path in (native,Path(str(native)+'.meta')):self.inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
        cache=self.recovery.proof['component_cache'];self.inputs[cache['path']]=cache['sha256']
        missing={'.'.join(filter(None,(s['namespace'],s['type']))) for s in panel['scripts']}-self.scripts.keys()
        assert not missing,missing
        for script in panel['scripts']:
            if script['type']=='UIClickSound':
                owner=self.original(script['original'])
                self.sound(self.recovery.fields(owner)['AkEvent'],owner)
        return [('HomeSceneUiGuider',panel)]

    def build(self):
        PartnerGiftBinding.build(self)
        receipt=json.loads((self.work/'partner-gift-native-source-manifest.json').read_bytes());folder=self.work/'gift-assets'
        for name in ('partner-gift-texts.json','partner-gift-texts.json.meta'):
            receipt['outputs'].pop(name);(folder/name).unlink()
        used=set()
        for name in receipt['outputs']:
            path=folder/name
            if path.suffix!='.meta' and path.read_bytes().startswith(b'%YAML'):
                used.update(re.findall(r'm_Script: \{[^\n}]*guid: ([0-9a-f]{32})',path.read_text('utf8')))
        receipt['script_bindings']={name:row for name,row in receipt['script_bindings'].items() if row['pointer']['guid'] in used}
        receipt.update(generator_sha256=sha(Path(__file__).read_bytes()),
            scope='Complete original Guider subtree and dependencies. Formal HomeUI parent, camera/furniture hosts and runtime validation remain required.')
        (self.work/'home-scene-ui-native-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        print('Original home scene UI:',len(receipt['outputs']),'outputs',flush=True)


if __name__=='__main__':HomeSceneUiBinding(validation_directory()).build()
