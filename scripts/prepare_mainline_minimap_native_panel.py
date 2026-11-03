"""Bind the selected full original fullscreen minimap window and dependency prefabs."""
import hashlib
import json
import re
import struct
import zlib
from pathlib import Path

from official_apk import ROOT
from official_manifest import read_asset_manifest
from official_tables import Constructors
from prepare_mainline_minimap_native_hud import MiniMapHudBinding
from prepare_mainline_minimap_native_root import integration_sources
from prepare_mainline_map15_native_ground import GroundSource
from prepare_partner_card_native import guid, script_id, sha
from prepare_current_partner_native import identity,documents
from UnityPy.classes.PPtr import PPtr
from validation_workspace import validation_directory


class MiniMapPanelBinding(MiniMapHudBinding):
    def __init__(self, work):
        super().__init__(work)
        self.source = GroundSource(13, 'Assets/Prefabs/UI/Map/MiniMap/MiniMapFullScreenPanel.prefab',
            'defaultpackage_assets_prefabs_ui_map_minimap.bundle')
        self.destination = ROOT / 'unity/Assets/Official/World/Shared/NativeMiniMapPanelCurrent'
        self.receipt_path = ROOT / 'assets/reference/official-map/mainline-current-minimap-panel-source-manifest.json'

    def native_guid(self, name):
        return hashlib.md5(('zjcs:mainline-minimap-panel:' + name).encode()).hexdigest()

    def register_scripts(self):
        super().register_scripts()
        runtime = ROOT / 'unity/Assets/Game/Runtime/Official'
        paths = {name: runtime / f'World/MainlineMiniMapPanel/{name}.cs'
            for name in ('MiniMapFullScreenPanel','MiniMapSwitchTabItem')}
        paths['PanelTitle'] = runtime / 'World/MainlineMiniMapPanel/MiniMapPanelTitle.cs'
        paths.update({name:runtime/'World/MainlineMiniMapPanel'/(name+'.cs')
            for name in ('MiniMapItemDirectionGuider','MiniPlayerDirectionGuider')})
        paths['CloseWindowBtn']=runtime/'World/MainlineMiniMapPanel/MiniMapPanelCloseWindowBtn.cs'
        paths['UIClickSound']=runtime/'World/MainlineMiniMapPanel/MiniMapPanelClickSound.cs'
        paths['SafeArea']=runtime/'WorldUI/WorldSafeArea.cs'
        paths['LayoutElementClamper']=runtime/'SkillsUI/LayoutElementClamper.cs'
        paths['HeadCharacterPortrait']=runtime/'TeamUI/TeamHeadCharacterPortrait.cs'
        paths['UIParticle']=ROOT/'unity/Assets/Game/Runtime/Presentation/Opening/OfficialCoffeeUIParticle/UIParticle.cs'
        for folder,names in {
            'World/Map13MiniMapCamera': ['MiniMapCameraController','UITouchInput'],
            'World/Map13MiniMapMarkFilter': ['MiniMapMarkUIFilter','MiniMapMarkFilterItem','MarkUIFilterEntryItem',
                'MultiTextsControler','CollectSortGroupItem','TabObjectTransition'],
            'World/Map13MiniMapWindows': ['ConditionItem','CountRedDotImage','MaintainGlobalScaleUI',
                'PreviewTool','PopupWindow','PopupWindowSound'],
            'WorldUI': ['HorizontalLayoutGroupJSZX','VerticalLayoutGroupJSZX'],
            'WorldUI/LoopScroll': ['LoopHorizontalScrollRect','LoopVerticalScrollRect'],
            'World/NativeText': ['TMPEffect'],
            'StoryBubbles': ['UIBloomGroup'],
            'ProfessionUI': ['PlayerIconMe','PlayerInfoUI','TopBarHandler'],
        }.items():
            paths.update({name:runtime/folder/(name+'.cs') for name in names})
        paths['TabItem'] = runtime/'WorldUI/WorldTabItem.cs'
        for name,path in paths.items():
            ref=dict(fileID=11500000,guid=guid(Path(str(path)+'.meta')),type=3)
            self.scripts[name]=ref
            self.class_ids[script_id('',name)]=ref
            if name.startswith('Loop'):
                self.class_ids[script_id('UnityEngine.UI',name)]=ref
                self.scripts['UnityEngine.UI.'+name]=ref
            if name.endswith('LayoutGroupJSZX'):
                self.class_ids[script_id('UGUIJszx',name)]=ref
                self.scripts['UGUIJszx.'+name]=ref
            if name=='UIParticle':
                self.class_ids[script_id('Coffee.UIExtensions',name)]=ref
                self.scripts['Coffee.UIExtensions.'+name]=ref
            if name in ('TabItem','TabObjectTransition'):
                self.class_ids[script_id('UnityEngine.UI.Ext',name)]=ref
                self.scripts['UnityEngine.UI.Ext.'+name]=ref
            self.script_guids.add(ref['guid'])
            self.script_sources[name]=dict(path=path.relative_to(ROOT).as_posix(),pointer=ref,
                sha256=sha(path.read_bytes()),meta_sha256=sha(Path(str(path)+'.meta').read_bytes()))

    def original_asset(self,obj):
        if obj.type.name!='GameObject':
            return super().original_asset(obj)
        key=identity(obj)
        if key in self.original_refs:return self.original_refs[key]
        address='Assets/Prefabs/UI/Map/MiniMap/Filter/'+obj.read(check_read=False).m_Name+'.prefab'
        rows=read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage',self.recovery.apk.archive))
        record,=[row for row in rows if row['path']==address]
        source,=[p.deref() for path,p in self.recovery.apk.container(record['bundle']) if path.casefold()==address.casefold()]
        if identity(source)!=key:raise ValueError('Original dependency prefab CAB/PPtr differs: '+address)
        pointer,proof=self.bind_prefab(GroundSource(13,address,record['bundle']),Path('Dependencies')/address.removeprefix('Assets/'))
        self.original_refs[key]=pointer;self.prefab_sources.append(proof)
        return pointer

    def load(self,source_guid):
        try:
            if source_guid not in self.redirects and self.objects[source_guid].type.name=='AnimatorController':
                return self.animator_controller(source_guid,self.objects[source_guid])
            return super().load(source_guid)
        except Exception as error:
            obj=self.objects.get(source_guid)
            raise ValueError('Resolve fullscreen dependency '+source_guid+' '+
                (identity(obj)+' '+obj.type.name if obj else 'unknown')) from error

    def animator_controller(self,source_guid,obj):
        path=self.native_path(source_guid,obj);raw=path.read_bytes()
        key=path.relative_to(self.cache/'native-export-all').as_posix()
        if sha(raw)!=self.checked[key] or sha(Path(str(path)+'.meta').read_bytes())!=self.checked[key+'.meta']:
            raise ValueError('Preserve exact selected controller export')
        docs=documents(raw.decode('utf8'))
        controller_id,=[k for k,v in docs.items() if 'AnimatorController' in v]
        original=self.recovery.fields(obj)
        self.learn(original,docs[controller_id]['AnimatorController'],obj)
        external=set(re.findall(r'guid: ([a-f0-9]{32})',raw.decode('utf8')))
        clips={}
        def canonical(value):
            if isinstance(value,dict):return {k:canonical(v) for k,v in value.items() if k!='serializedVersion'}
            if isinstance(value,list):return [canonical(v) for v in value]
            if isinstance(value,(float,int)):return struct.pack('<f',value).hex()
            return [] if value is None else value
        for pointer in original['m_AnimationClips']:
            clip=PPtr(**pointer,assetsfile=obj.assets_file).deref()
            fields=self.recovery.fields(clip);matches=[]
            for clip_guid in external:
                try:clip_path=self.native_path(clip_guid,clip)
                except ValueError:continue
                clip_docs=documents(clip_path.read_text('utf8'))
                native,=[v['AnimationClip'] for v in clip_docs.values() if 'AnimationClip' in v]
                checks=['m_Name','m_SampleRate','m_WrapMode','m_RotationCurves','m_CompressedRotationCurves',
                    'm_EulerCurves','m_PositionCurves','m_ScaleCurves','m_FloatCurves','m_PPtrCurves']
                comparisons={name:canonical(fields[name]) for name in checks if name in fields and name in native}
                if comparisons!={name:canonical(native[name]) for name in comparisons} and not self.constant_scale_motion(fields,native,canonical):
                    (self.work/('controller-motion-'+clip_guid+'.json')).write_text(json.dumps(dict(
                        source=identity(clip),native=clip_path.as_posix(),original=comparisons,
                        exported={name:canonical(native[name]) for name in comparisons}),indent=2),encoding='utf8')
                    continue
                comparisons['compressed_source']=canonical(dict(bindings=fields.get('m_ClipBindingConstant'),
                    muscle=fields.get('m_MuscleClip')))
                comparisons['native_curves']=canonical({name:native[name] for name in checks if name in native})
                matches.append((clip_guid,clip_path,comparisons))
            clip_guid,clip_path,checks,=matches[0] if len(matches)==1 else (None,None,None)
            if clip_guid is None:raise ValueError('Unique original controller motion/curve correspondence required')
            if clip_guid in clips and clips[clip_guid]['source']!=identity(clip):
                raise ValueError('Different original clips map to one native motion')
            self.objects[clip_guid]=clip
            clips[clip_guid]=dict(source=identity(clip),original_sha256=sha(clip.get_raw_data()),
                native=clip_path.relative_to(ROOT).as_posix(),compared_curve_fields=list(checks),
                compared_curves_sha256=sha(json.dumps(checks,sort_keys=True).encode()))
        if set(clips)!=external:raise ValueError('Bind every original controller external motion')
        target=path.relative_to(self.native);new_guid=self.native_guid(source_guid)
        result=dict(fileID=controller_id,guid=new_guid,type=2)
        self.redirects[source_guid]=result;self.script_guids.add(new_guid)
        self.inputs[path.relative_to(ROOT).as_posix()]=sha(raw)
        self.queue.append((target,new_guid,docs))
        self.proof[identity(obj)]=dict(source=self.recovery.apk.provenance(obj),type='AnimatorController',
            original_sha256=sha(obj.get_raw_data()),native_path=path.relative_to(ROOT).as_posix(),
            output=target.as_posix(),guid=new_guid,subobjects=len(docs),motions=clips)
        return result

    @staticmethod
    def constant_scale_motion(original,native,canonical):
        # Player clips keep constant transform channels inside MuscleClip. The
        # checked native export expands those channels into editable curves.
        # Accept this representation only after checking every original channel.
        if any(canonical(original.get(k))!=canonical(native.get(k)) for k in
            ('m_Name','m_SampleRate','m_WrapMode')):return False
        names=('m_RotationCurves','m_CompressedRotationCurves','m_EulerCurves',
            'm_PositionCurves','m_FloatCurves','m_PPtrCurves')
        if any(original.get(k) or native.get(k) for k in names) or original.get('m_ScaleCurves'):return False
        muscle=original.get('m_MuscleClip',{});data=muscle.get('m_Clip',{}).get('data',{})
        if data.get('m_StreamedClip',{}).get('curveCount') or data.get('m_DenseClip',{}).get('m_CurveCount'):return False
        bindings=original.get('m_ClipBindingConstant',{}).get('genericBindings',[])
        values=data.get('m_ConstantClip',{}).get('data',[])
        curves=native.get('m_ScaleCurves') or []
        if len(bindings)!=len(curves) or len(values)!=len(bindings)*3:return False
        expected={}
        for i,binding in enumerate(bindings):
            if binding['typeID']!=4 or binding['attribute']!=3 or binding['script']['m_PathID'] or any(
                binding.get(k) for k in ('customType','isPPtrCurve','isIntCurve','isSerializeReferenceCurve')):return False
            if binding['path'] in expected:return False
            expected[binding['path']]=canonical(values[i*3:i*3+3])
        actual={}
        for curve in curves:
            path=zlib.crc32(curve['path'].encode());keys=curve['curve']['m_Curve']
            if path in actual or len(keys)!=1:return False
            key=keys[0]
            if key['time']!=0 or any(key[s][a]!=0 for s in ('inSlope','outSlope') for a in 'xyz'):return False
            actual[path]=canonical([key['value'][a] for a in 'xyz'])
        if actual!=expected:return False
        settings=native['m_AnimationClipSettings']
        parameters=('m_StartTime','m_StopTime','m_LoopTime','m_LoopBlend','m_LoopBlendOrientation',
            'm_LoopBlendPositionY','m_LoopBlendPositionXZ','m_KeepOriginalOrientation',
            'm_KeepOriginalPositionY','m_KeepOriginalPositionXZ','m_HeightFromFeet','m_Mirror')
        return all(canonical(muscle[k])==canonical(settings[k]) for k in parameters)

    def replace(self,value):
        if isinstance(value,dict) and value.get('m_Script')==self.scripts.get('PlayerInfoUI') and 'references' in value:
            refs=value['references']
            result={k:self.replace(v) for k,v in value.items() if k!='references'}
            result['references']=dict(refs,RefIds=[dict(row,type=dict(row['type'],**{
                'class':'WorldStateGameObjectConfig','ns':'Zjcs.Official.WorldUI','asm':'Zjcs.Official.WorldUI'}),
                data=self.replace(row['data'])) for row in refs['RefIds']])
            return result
        return super().replace(value)

    def panel_resources(self):
        apk=self.recovery.apk
        code=self.cache.parent/'official-code'
        constructors=Constructors(code)
        tables={};proof={}
        for name,bundle,suffix,kind,fields in (
            ('Maps','config_luban_map','/map.g.bytes','TbMapInfo',
                ('ClassId','MapGroupId','MiniMapSwitchTabGroup','MiniMapIcon')),
            ('Marks','config_luban','/minimap_marks.g.bytes','TbMiniMapMarkInfo',
                ('Name','Image','Description','ShowOnFilterEntry','ShowOnMapGroupId','ShowOnMapClassId'))):
            obj=apk.asset(bundle,suffix);raw=obj.read().m_Script.encode('utf8','surrogateescape')
            rows=constructors.table(raw,kind)
            tables[name]=[{key:row[key] for key in fields} for row in rows]
            proof[name]=dict(source=apk.provenance(obj),original_sha256=sha(obj.get_raw_data()),
                payload_sha256=sha(raw),fields=fields,rows=len(rows))
        manifest=read_asset_manifest(apk.source.manifest_bytes('DefaultPackage',apk.archive))
        by_path={row['path'].casefold():row for row in manifest}
        by_address={row['address'].casefold():row for row in manifest if row['address']}
        supported={10,11,12,13,14,16}
        groups={r['MiniMapSwitchTabGroup'] for r in tables['Maps'] if r['ClassId'] in supported and r['MiniMapSwitchTabGroup']>=0}
        tab_rows=[r for r in tables['Maps'] if r['ClassId'] in supported or r['MiniMapSwitchTabGroup'] in groups]
        icons=sorted({r['MiniMapIcon'] for r in tab_rows if r['MiniMapIcon']}|
            {r['Image'] for r in tables['Marks'] if r['Image'] and (r['ShowOnMapGroupId'] or r['ShowOnMapClassId'])})
        entries=[];sources=[]
        for icon in icons:
            address='Assets/Image/'+icon
            record=by_address.get(address.casefold()) or by_path[(address+'.png').casefold()]
            address=record['path']
            objects=[p.deref() for path,p in apk.container(record['bundle'])
                if path.casefold()==address.casefold() and p.deref().type.name=='Sprite']
            obj,=objects
            pointer=self.sprite_reference(obj)
            entries.append(dict(Key=icon,Value=pointer))
            sources.append(dict(path=address,source=apk.provenance(obj),original_sha256=sha(obj.get_raw_data()),pointer=pointer))
        world=apk.asset('defaultpackage_assets_prefabs_ui_map_world.bundle','/WorldCanvas.prefab')
        scalers=[]
        for component in world.read().m_Components:
            obj=component.deref()
            if obj.type.name=='MonoBehaviour' and obj.read(check_read=False).m_Script.read().m_ClassName=='CanvasScaler':
                scalers.append(obj)
        scaler,=scalers
        fields=self.recovery.fields(scaler)
        scaling={name:fields[key] for name,key in (
            ('ReferenceResolution','m_ReferenceResolution'),('ScaleMode','m_UiScaleMode'),
            ('ScreenMatchMode','m_ScreenMatchMode'),('ReferencePixelsPerUnit','m_ReferencePixelsPerUnit'),
            ('ScaleFactor','m_ScaleFactor'),('MatchWidthOrHeight','m_MatchWidthOrHeight'))}
        proof['CanvasScaler']=dict(source=apk.provenance(scaler),original_sha256=sha(scaler.get_raw_data()),fields=scaling)
        script=ROOT/'unity/Assets/Game/Runtime/Official/World/MainlineMiniMapPanel/MiniMapPanelResources.cs'
        ref=dict(fileID=11500000,guid=guid(Path(str(script)+'.meta')),type=3)
        self.script_guids.add(ref['guid'])
        data=dict(m_ObjectHideFlags=0,m_CorrespondingSourceObject=dict(fileID=0),m_PrefabInstance=dict(fileID=0),
            m_PrefabAsset=dict(fileID=0),m_GameObject=dict(fileID=0),m_Enabled=1,m_EditorHideFlags=0,
            m_Script=ref,m_Name='MiniMapPanelResources',m_EditorClassIdentifier='',**tables,**scaling,
            Icons=dict(mSerializeBuffer=entries))
        target=Path('Resources/World/Native/mini-map-panel-data.asset');target_guid=self.native_guid('configuration')
        self.script_guids.add(target_guid);self.queue.append((target,target_guid,{11400000:dict(MonoBehaviour=data)}))
        paths=[script,Path(str(script)+'.meta'),code/'global-metadata.dat',ROOT/'scripts/official_tables.py']
        for name in constructors.sources:
            paths.append(next(p for p in (code/'common-source'/folder/(name+'.cs')
                for folder in ('Common','Common.LubanGen')) if p.is_file()))
        for path in paths:self.inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
        return dict(tables=proof,icons=sources,tab_icon_groups=sorted(groups),
            resource=dict(fileID=11400000,guid=target_guid,type=2))

    def reused_sources(self):
        records=[]
        for name,folder in (
            ('MiniMapMarkUIFilter','client-source'),('MiniMapMarkFilterItem','client-source'),
            ('MarkUIFilterEntryItem','client-source'),('SafeArea','framework-firstpass-source'),
            ('LayoutElementClamper','framework-firstpass-source'),('HeadCharacterPortrait','client-source'),
            ('PlayerInfoUI','client-source'),('PlayerIconMe','client-source'),('TopBarHandler','client-source'),
            ('MiniMapCameraController','client-source'),('UITouchInput','client-source'),
            ('ConditionItem','client-source'),('CountRedDotImage','client-source'),
            ('MaintainGlobalScaleUI','client-source'),('PreviewTool','framework-source'),
            ('PopupWindow','framework-firstpass-source'),('PopupWindowSound','framework-firstpass-source'),
            ('MultiTextsControler','client-source'),('CollectSortGroupItem','client-source'),
            ('UIBloomGroup','framework-firstpass-source')):
            selected=self.cache.parent/'official-code'/folder/(name+'.cs')
            if not selected.is_file():selected,=(self.cache.parent/'official-code').glob('*/'+name+'.cs')
            previous=ROOT/'.tools/source-packages/official-code'/selected.relative_to(self.cache.parent/'official-code')
            if selected.read_bytes()!=previous.read_bytes():raise ValueError('Review changed original shared window code: '+name)
            for path in (selected,previous):self.inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
            records.append(dict(selected=selected.relative_to(ROOT).as_posix(),previous=previous.relative_to(ROOT).as_posix(),sha256=sha(selected.read_bytes())))
        return records

    def build_panel(self):
        pointer,record=self.bind_prefab(self.source,Path('Resources/World/Native/mini-map-panel.prefab'))
        card=self.recovery.outputs[-1][2]
        if {r['name'] for r in card['unresolved'].values()} != {'MarkUIFilterEntryItem','MiniMapMarkFilterItem'}:
            raise ValueError('Review additional original fullscreen dependencies')
        count=len(card['documents'])
        configuration=self.panel_resources()
        recovered_index=0
        index=0
        while index<len(self.queue):
            while recovered_index<len(self.recovery.outputs):
                _,_,recovered,_=self.recovery.outputs[recovered_index]
                recovered_index+=1
                for script in recovered['scripts']:
                    if script['type'] in ('ButtonSound','UIClickSound'):
                        original=self.original(script['original'])
                        field='ClickEvent' if script['type']=='ButtonSound' else 'AkEvent'
                        recovered['documents'][script['nativeFileID']]['MonoBehaviour'][field]=self.sound(
                            self.recovery.fields(original)[field],original)
            _,_,data=self.queue[index]
            if isinstance(data,dict):
                resolved=self.replace(data);data.clear();data.update(resolved)
            index+=1
        for target,(identity,raw) in self.audio_outputs.items():
            self.queue.append((target,identity,raw))
            meta=Path(str(self.destination/target)+'.meta');meta.parent.mkdir(parents=True,exist_ok=True)
            if not meta.exists():
                meta.write_text('fileFormatVersion: 2\nguid: '+identity+'\nAudioImporter:\n  externalObjects: {}\n'
                    '  serializedVersion: 7\n  defaultSettings:\n    loadType: 0\n    sampleRateSetting: 0\n'
                    '    sampleRateOverride: 0\n    compressionFormat: 0\n    quality: 1\n    conversionMode: 0\n'
                    '  forceToMono: 0\n  normalize: 0\n  preloadAudioData: 1\n  loadInBackground: 0\n'
                    '  ambisonic: 0\n  3D: 0\n',encoding='utf8',newline='\n')
        for p in [Path(__file__),ROOT/'scripts/prepare_mainline_minimap_native_hud.py',
            ROOT/'assets/reference/official-map/mainline-minimap-panel-code-source-manifest.json']:
            self.inputs[p.relative_to(ROOT).as_posix()]=sha(p.read_bytes())
        equivalence=self.reused_sources()
        self.write_outputs(record,pointer,serialized_object_count=count,
            dependency_prefabs=self.prefab_sources,audio=self.audio_proof,configuration=configuration,integration=integration_sources(),
            original_code_equivalence=equivalence,
            scope='Complete selected fullscreen minimap window, switch tabs, filter and dependency hierarchy. Preserve original file IDs, fields and layout. Map-state and window hosting are supplied separately.')
        receipt=json.loads(self.receipt_path.read_bytes());receipt.pop('mapClassId')
        self.receipt_path.write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n',encoding='utf8',newline='\n')


if __name__=='__main__':
    MiniMapPanelBinding(validation_directory()).build_panel()
