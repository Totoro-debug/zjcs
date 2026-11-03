"""Recover the selected original renderer shared by the mainline minimap slot."""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from prepare_mainline_map15_native_ground import GroundSource
from prepare_mainline_minimap_native_hud import MiniMapHudBinding
from prepare_mainline_minimap_native_root import integration_sources
from prepare_partner_card_native import guid, script_id, sha
from prepare_current_partner_native import identity
from UnityPy.classes.PPtr import PPtr
from validation_workspace import validation_directory


class MiniMapRendererBinding(MiniMapHudBinding):
    def __init__(self, work):
        super().__init__(work)
        self.source=GroundSource(13,'Assets/Prefabs/Map/Common/MiniMap/MinimapNonMaskable.prefab',
            'defaultpackage_assets_prefabs_map_common_minimap.bundle')
        self.destination=ROOT/'unity/Assets/Official/World/Shared/NativeMiniMapRendererCurrent'
        self.receipt_path=ROOT/'assets/reference/official-map/mainline-current-minimap-renderer-source-manifest.json'
        self.builtins={}

    def native_guid(self,name):
        return hashlib.md5(('zjcs:mainline-minimap-renderer:'+name).encode()).hexdigest()

    def register_scripts(self):
        super().register_scripts()
        name='MiniMapNonMaskableManager'
        path=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapNonMaskable'/(name+'.cs')
        ref=dict(fileID=11500000,guid=guid(Path(str(path)+'.meta')),type=3)
        self.scripts[name]=ref;self.class_ids[script_id('',name)]=ref;self.script_guids.add(ref['guid'])
        self.script_sources[name]=dict(path=path.relative_to(ROOT).as_posix(),pointer=ref,
            sha256=sha(path.read_bytes()),meta_sha256=sha(Path(str(path)+'.meta').read_bytes()))

    def learn(self,source,native,owner):
        if isinstance(source,dict) and source.get('m_PathID') and isinstance(native,dict) and native.get('guid') in (
                '0000000000000000e000000000000000','0000000000000000f000000000000000'):
            obj=PPtr(**source,assetsfile=owner.assets_file).deref()
            if native.get('type')!=0 or native.get('fileID',0)<=0 or obj.type.name not in ('Mesh','Material','Shader','Texture2D'):
                raise ValueError('Review additional built-in renderer reference: '+str(native))
            key=native['guid']+':'+str(native['fileID'])
            record=dict(source=self.recovery.apk.provenance(obj),original=identity(obj),type=obj.type.name,
                name=obj.read(check_read=False).m_Name,original_sha256=sha(obj.get_raw_data()),native=native)
            if key in self.builtins and self.builtins[key]['original_sha256']!=record['original_sha256']:
                raise ValueError('One built-in selector maps to different original payloads: '+key)
            self.builtins[key]=record;self.script_guids.add(native['guid'])
            return
        return super().learn(source,native,owner)

    def build_renderer(self):
        pointer,record=self.bind_prefab(self.source,Path('Resources/World/Native/mini-map-nonmaskable.prefab'))
        count=len(self.recovery.outputs[-1][2]['documents'])
        index=0
        while index<len(self.queue):
            _,_,data=self.queue[index]
            if isinstance(data,dict):
                resolved=self.replace(data);data.clear();data.update(resolved)
            index+=1
        selected=self.cache.parent/'official-code/client-source/MiniMapNonMaskableManager.cs'
        previous=ROOT/'.tools/source-packages/official-code/client-source/MiniMapNonMaskableManager.cs'
        if selected.read_bytes()!=previous.read_bytes():raise ValueError('Review changed original shared renderer code')
        host=self.cache.parent/'official-code/client-source/MapRootSwitchMapSupport.cs'
        if 'component.NonMaskableManager = ((Component)mMinimapSlot).GetComponentInChildren<MiniMapNonMaskableManager>();' not in host.read_text('utf-8-sig'):
            raise ValueError('Review changed original shared slot binding')
        for path in (selected,previous,host,Path(__file__)):
            self.inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
        self.write_outputs(record,pointer,serialized_object_count=count,integration=integration_sources(),builtin_references=self.builtins,
            scope='Complete original shared minimap-slot renderer. Original CreateMiniMap binds this slot to the current map; the fullscreen panel controls showing and hiding it.')
        receipt=json.loads(self.receipt_path.read_bytes());receipt.pop('mapClassId')
        self.receipt_path.write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n',encoding='utf8',newline='\n')


if __name__=='__main__':
    MiniMapRendererBinding(validation_directory()).build_renderer()
