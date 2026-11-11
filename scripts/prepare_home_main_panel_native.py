"""Recover the original HomeUI Animator and both complete native clips."""
import json
from pathlib import Path

import yaml
from UnityPy.enums import ClassIDType

from prepare_home_bottom_bar_native import HomeBottomBarBinding
from prepare_partner_card_native import ROOT, Dumper, sha
from prepare_current_partner_native import pointer
from validation_workspace import validation_directory


class HomeMainPanelBinding(HomeBottomBarBinding):
    def build(self):
        recovery=self.recovery
        recovery.recover('MainCanvas',self.work/'main-panel-native',
            bundle='defaultpackage_assets_prefabs_ui_main.bundle',
            native_relative='Prefabs/UI/Main/MainCanvas.prefab')
        recovery.finalize()
        native,_,panel,_=recovery.outputs[0]
        home,=[row for row in panel['scripts'] if row['type']=='HomeUI']
        fields=panel['documents'][home['nativeFileID']]['MonoBehaviour']
        animator_id=fields['mAnimator']['fileID']
        animator=panel['documents'][animator_id]['Animator']
        owner_key,=[key for key,value in panel['objectMap'].items() if value==animator_id]
        owner=self.original(owner_key)
        original=pointer(recovery.fields(owner)['m_Controller'],owner)
        self.objects[animator['m_Controller']['guid']]=original
        controller=self.load(animator['m_Controller']['guid'])
        for _,_,data in self.queue:
            if isinstance(data,dict):
                resolved=self.replace(data);data.clear();data.update(resolved)
        assert len(self.queue)==3,'The original controller references exactly two clips.'
        outputs={}
        destination=self.work/'main-panel-assets'
        for relative,key,data in self.queue:
            if relative.suffix=='.controller':
                relative=Path('Resources/Home/MainPanel/HomeUI.controller')
            elif relative.suffix=='.anim':
                relative=Path('Animations')/relative.name
            else:raise ValueError(relative)
            path=destination/relative;path.parent.mkdir(parents=True,exist_ok=True)
            text='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
            for file_id,doc in data.items():
                kind,=doc
                text+='--- !u!'+str(int(ClassIDType[kind]))+' &'+str(file_id)+'\n'
                text+=yaml.dump(doc,Dumper=Dumper,sort_keys=False,allow_unicode=True,width=1000000)
            path.write_text(text,'utf8',newline='\n')
            meta=Path(str(path)+'.meta')
            meta.write_text('fileFormatVersion: 2\nguid: '+key+'\nNativeFormatImporter:\n  externalObjects: {}\n',
                'utf8',newline='\n')
            for item in (path,meta):outputs[item.relative_to(destination).as_posix()]=sha(item.read_bytes())
        for path in (native,Path(str(native)+'.meta')):
            self.inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
        cache=recovery.proof['component_cache'];self.inputs[cache['path']]=cache['sha256']
        receipt=dict(manifest=recovery.apk.version,generator_sha256=sha(Path(__file__).read_bytes()),
            inputs=self.inputs,objects=self.proof,outputs=outputs,native_recovery=recovery.proof,
            controller=controller,animator_fields=animator,home_fields=fields,
            resource='Home/MainPanel/HomeUI',
            scope='Complete original HomeUI Animator/controller/clips only. Full HomeUI hierarchy, tab/scene lifecycle and production HUD integration remain separate.')
        (self.work/'home-main-panel-native-source-manifest.json').write_text(
            json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        print('Original HomeUI animation:',len(self.queue),'assets;',len(outputs),'outputs',flush=True)


if __name__=='__main__':HomeMainPanelBinding(validation_directory()).build()
