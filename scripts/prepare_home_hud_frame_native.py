"""Recover the original shared HomeUI ancestors and player stencil mask."""
import copy
import hashlib
import json
from pathlib import Path
import re
import yaml
from UnityPy.enums import ClassIDType
from prepare_home_screen_native import HomeScreenBinding
from prepare_partner_card_native import ROOT,Dumper,guid,sha,script_id
from validation_workspace import validation_directory

class HomeHudFrameBinding(HomeScreenBinding):
    def register_scripts(self):
        super().register_scripts()
        name='StencilMask';path=ROOT/'unity/Assets/Game/Runtime/Official/HomeScreenUI/StencilMask.cs'
        meta=Path(str(path)+'.meta');ref=dict(fileID=11500000,guid=guid(meta),type=3)
        self.scripts[name]=ref;self.class_ids[script_id('',name)]=ref;self.script_guids.add(ref['guid'])
        self.script_sources[name]=dict(path=path.relative_to(ROOT).as_posix(),pointer=ref,sha256=sha(path.read_bytes()),meta_sha256=sha(meta.read_bytes()))
    def build(self):
        recovery = self.recovery
        recovery.recover('MainCanvas',self.work/'screen-native',
            bundle='defaultpackage_assets_prefabs_ui_main.bundle',
            native_relative='Prefabs/UI/Main/MainCanvas.prefab')
        recovery.finalize()
        native, _, original, _ = recovery.outputs[0]
        panel = copy.deepcopy(original)
        # Exact original ancestors; feature prefabs retain their own local geometry.
        documents=panel['documents'];selected=set();root=panel['root'];by_name={}
        frame_ids={1709254077376921,1730242686860875,1724804207875695,
            1618682377417806,1557543343976191,1695280048154070,
            1466628476471728,1816378901845403,1730984962074643,1254416370816575}
        allowed={'CanvasWidthAdjuster','CanvasScaler','GraphicRaycaster','RectMask2D','Image','SafeArea','StencilMask'}
        for go in frame_ids:
            obj=documents[go]['GameObject'];by_name[str(go)]=obj['m_Name'];selected.add(go)
            for entry in obj['m_Component']:
                key=entry['component']['fileID'];fields=documents[key]
                if 'MonoBehaviour' in fields:
                    kind,=[s['type'] for s in panel['scripts'] if s['nativeFileID']==key]
                    if kind not in allowed:continue
                selected.add(key)
        panel['documents'] = {key:doc for key,doc in documents.items() if key in selected}
        for doc in panel['documents'].values():
            if 'GameObject' in doc:
                obj=doc['GameObject']
                obj['m_Component']=[c for c in obj['m_Component'] if c['component']['fileID'] in selected]
            if 'RectTransform' in doc:
                tr=doc['RectTransform']
                tr['m_Children']=[c for c in tr['m_Children'] if c['fileID'] in selected]
        panel['scripts']=[s for s in panel['scripts'] if s['nativeFileID'] in selected]
        used=set()
        def check(value):
            if isinstance(value,dict):
                if set(value)=={'fileID'} and value['fileID']:
                    assert value['fileID'] in selected, value
                if 'guid' in value and value.get('fileID'): used.add(value['guid'])
                for item in value.values():check(item)
            elif isinstance(value,list):
                for item in value:check(item)
        check(panel['documents'])
        for key,ref in panel['external'].items():
            if ref['guid'] in used:self.objects[ref['guid']]=self.original(key)
        for key,file_id in panel['objectMap'].items():
            if file_id in selected:
                owner=self.original(key)
                self.learn(recovery.fields(owner),next(iter(panel['documents'][file_id].values())),owner)
        used_scripts=set()
        for row in panel['scripts']:
            name='.'.join(filter(None,(row['namespace'],row['type'])))
            panel['documents'][row['nativeFileID']]['MonoBehaviour']['m_Script']=self.scripts[name]
            used_scripts.add(name)
        resolved=self.replace(panel['documents'])
        for _,_,data in self.queue:
            if isinstance(data,dict):
                value=self.replace(data);data.clear();data.update(value)
        target_guid=hashlib.md5(b'zjcs:home-hud-original-frame').hexdigest()
        self.queue.append((Path('HomeHudFrame.prefab'),target_guid,resolved))
        outputs={}
        destination=self.work/'frame-assets'
        for relative,asset_guid,data in self.queue:
            path=destination/relative;path.parent.mkdir(parents=True,exist_ok=True)
            if isinstance(data,dict):
                text='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
                for file_id,doc in data.items():
                    kind,=doc
                    text+='--- !u!'+str(int(ClassIDType[kind]))+' &'+str(file_id)+'\n'
                    text+=yaml.dump(doc,Dumper=Dumper,sort_keys=False,allow_unicode=True,width=1000000)
                data=text.encode('utf8')
            path.write_bytes(data)
            importer='PrefabImporter' if path.suffix=='.prefab' else 'ShaderImporter' if path.suffix=='.shader' else 'NativeFormatImporter'
            meta=Path(str(path)+'.meta')
            meta.write_text('fileFormatVersion: 2\nguid: '+asset_guid+'\n'+importer+':\n  externalObjects: {}\n','utf8',newline='\n')
            for item in (path,meta):outputs[item.relative_to(destination).as_posix()]=sha(item.read_bytes())
        for path in (native,Path(str(native)+'.meta')):
            self.inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
        cache=recovery.proof['component_cache'];self.inputs[cache['path']]=cache['sha256']
        receipt=dict(manifest=recovery.apk.version,generator_sha256=sha(Path(__file__).read_bytes()),
            inputs=self.inputs,objects=self.proof,outputs=outputs,native_recovery=recovery.proof,
            root=dict(fileID=root,guid=target_guid,type=3),
            script_bindings={name:self.script_sources[name] for name in sorted(used_scripts)},
            selection=dict(nodes=by_name,objects=sorted(selected),
                omitted_objects=len(original['documents'])-len(selected),
                adaptation='Exact HomeUI and public-HUD ancestor projection, including original player stencil. Other feature panels and global MainUIRoot handlers omitted; geometry retained.'),
            scope='Original HomeUI/public ancestors and stencil only. Attach recovered HomeUI, PublicPlayerInfo and PublicBottomBar to their original slots; production local services remain required.')
        (self.work/'home-hud-frame-native-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        print('Original screen frame:',len(selected),'objects;',len(outputs),'outputs',flush=True)


if __name__=='__main__':HomeHudFrameBinding(validation_directory()).build()
