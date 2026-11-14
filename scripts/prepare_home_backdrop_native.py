"""Recover the original default Back_Ground hierarchy, particles and native day clip."""
import argparse
import hashlib
import json
from pathlib import Path
import yaml

from official_apk import ROOT
from prepare_current_partner_native import documents, identity, pointer
from prepare_home_environment_native import HomeEnvironmentBinding, metadata
from prepare_partner_card_native import Dumper, guid, script_id, sha
from home_native_assets import install_staged_assets, write_staged_assets
from validation_workspace import validation_directory


BUNDLE = 'defaultpackage_assets_prefabs_home_dynamic_theme_background.bundle'
NATIVE = 'Prefabs/Home/Dynamic/Theme/Background/Background.prefab'


class HomeBackdropBinding(HomeEnvironmentBinding):
    def register_scripts(self):
        super().register_scripts()
        path = ROOT/'unity/Assets/Game/Runtime/Official/World/NativeEnvironment/CullingGroupWrapper.cs'
        ref = dict(fileID=11500000,guid=guid(Path(str(path)+'.meta')),type=3)
        self.scripts['CullingGroupWrapper'] = ref
        self.class_ids[script_id('','CullingGroupWrapper')] = ref
        self.script_guids.add(ref['guid'])
        self.script_sources['CullingGroupWrapper'] = dict(path=path.relative_to(ROOT).as_posix(),pointer=ref,
            sha256=sha(path.read_bytes()),meta_sha256=sha(Path(str(path)+'.meta').read_bytes()))

    def build(self):
        root, = [o for o in self.recovery.apk.objects(BUNDLE) if o.type.name == 'GameObject'
            and o.read().m_Name == 'Background' and o.read().m_Component[0].component.read().m_Father.path_id == 0]
        binder, = [entry.component.deref() for entry in root.read().m_Component
            if entry.component.type.name == 'MonoBehaviour']
        script = binder.read(check_read=False).m_Script.read()
        if script.m_ClassName != 'HomePlayableClipBind': raise ValueError('Restore original background binder')
        source_rows = self.recovery.fields(binder)['ClipDatas']
        selected, = [row for row in source_rows if pointer(row['Animator'],binder).read().m_GameObject.read().m_Name == 'Back_Ground']
        animator = pointer(selected['Animator'],binder)
        source_root = animator.read().m_GameObject.deref()
        clip = pointer(selected['Clip'],binder)
        native_path = self.native/NATIVE
        native_docs = documents(native_path.read_text('utf8'))
        native_root, = [key for key,value in native_docs.items() if value.get('GameObject',{}).get('m_Name') == 'Back_Ground']
        native_transform, = [value['Transform'] for value in native_docs.values()
            if value.get('Transform',{}).get('m_GameObject',{}).get('fileID') == native_root]
        parent = native_docs[native_transform['m_Father']['fileID']]['Transform']
        if parent['m_Father']['fileID'] != 0 or parent['m_LocalPosition'] != dict(x=0,y=0,z=0) \
            or parent['m_LocalRotation'] != dict(x=0,y=0,z=0,w=1) or parent['m_LocalScale'] != dict(x=1,y=1,z=1):
            raise ValueError('Preserve nonidentity original background ancestor before detaching the subtree')
        self.recovery.recover('Back_Ground',self.work/'home-backdrop-native',bundle=BUNDLE,
            native_relative=NATIVE,source_root=source_root,native_root_id=native_root)
        self.recovery.finalize()
        panel = self.recovery.outputs[0][2]
        native_binder, = [v['MonoBehaviour'] for v in native_docs.values() if 'ClipDatas' in v.get('MonoBehaviour',{})]
        self.learn(self.recovery.fields(binder),native_binder,binder)
        original_index = source_rows.index(selected)
        native_clip_ref = native_binder['ClipDatas'][original_index]['Clip']
        self.objects[native_clip_ref['guid']] = clip
        clip_ref = self.load(native_clip_ref['guid'])
        for key, ref in panel['external'].items(): self.objects[ref['guid']] = self.original(key)
        for key,file_id in panel['objectMap'].items():
            owner = self.original(key)
            self.learn(self.recovery.fields(owner),next(iter(panel['documents'][file_id].values())),owner)
        for script in panel['scripts']:
            full = '.'.join(filter(None,(script['namespace'],script['type'])))
            if full not in ('CameraFollower','CullingGroupWrapper'):
                raise ValueError('Restore additional original backdrop component: '+full)
            fields = panel['documents'][script['nativeFileID']]['MonoBehaviour']
            fields['m_Script'] = self.scripts[full]
            if full == 'CullingGroupWrapper' and fields['SkeletonAnimations']:
                raise ValueError('Bind original skeleton culling before using this backdrop')
        resolved = self.replace(panel['documents'])
        target_guid = hashlib.md5(b'zjcs:home-native-backdrop:Back_Ground').hexdigest()
        self.queue.append((Path('BackGround.prefab'),target_guid,resolved))
        for _,_,data in self.queue:
            if isinstance(data,dict):
                replaced = self.replace(data);data.clear();data.update(replaced)
        outputs = write_staged_assets(self.queue,self.work/'home-backdrop-native-assets')
        source = ROOT/'.tools/source-packages/current'/self.recovery.apk.version/'official-code/client-source'
        for path in (native_path,Path(str(native_path)+'.meta'),Path(__file__),ROOT/'scripts/home_native_assets.py',
            ROOT/'scripts/prepare_home_environment_native.py',source/'HomePlayable/HomePlayableClipBind.cs',
            source/'HomePlayable/HomePlayableClipData.cs',source/'HomePlayable/TimelinePlayable.cs',
            source/'CullingGroupWrapper.cs',source/'HomeEntityManagerMono.cs'):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        for obj in (root,binder,source_root,animator,clip):
            self.proof.setdefault(identity(obj),{}).update(source=self.recovery.apk.provenance(obj),original_sha256=sha(obj.get_raw_data()))
        receipt = dict(manifest=self.recovery.apk.version,inputs=self.inputs,objects=self.proof,outputs=outputs,
            root=dict(fileID=panel['root'],guid=target_guid,type=3),clip=clip_ref,
            scripts=self.script_sources,recovery=self.recovery.proof,
            selection=dict(original_binder=identity(binder),original_clip_row=original_index,
                original_clip=identity(clip),original_animator=identity(animator),source_root=identity(source_root),
                detached_native_parent=parent),
            stage='Complete Back_Ground subtree and native clip staged; runtime installation, native scene sorting and graphical validation pending')
        path = self.work/'home-backdrop-native-source-manifest.json'
        path.write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        print('Staged original native backdrop',len(outputs),'files',flush=True)


def install(work):
    receipt = json.loads((work/'home-backdrop-native-source-manifest.json').read_bytes())
    for relative, expected in receipt['inputs'].items():
        if sha((ROOT/relative).read_bytes()) != expected:
            raise ValueError('Changed native backdrop input: '+relative)
    installed = install_staged_assets(work,work/'home-backdrop-native-assets',receipt['outputs'],
        ROOT/'unity/Assets/Official/Home/BackdropNative','home-native-backdrop')
    script = ROOT/'unity/Assets/Game/Runtime/Presentation/Home/HomeNativeBackdropAssets.cs'
    metadata(script,'MonoImporter')
    data = dict(MonoBehaviour=dict(m_ObjectHideFlags=0,m_CorrespondingSourceObject=dict(fileID=0),
        m_PrefabInstance=dict(fileID=0),m_PrefabAsset=dict(fileID=0),m_GameObject=dict(fileID=0),m_Enabled=1,
        m_EditorHideFlags=0,m_Script=dict(fileID=11500000,guid=guid(Path(str(script)+'.meta')),type=3),
        m_Name='HomeNativeBackdropAssets',m_EditorClassIdentifier='',Root=receipt['root'],Clip=receipt['clip']))
    catalog = ROOT/'unity/Assets/Game/Resources/Home/HomeNativeBackdropAssets.asset'
    catalog.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'
        +yaml.dump(data,Dumper=Dumper,sort_keys=False),'utf8',newline='\n')
    for path in (catalog,metadata(catalog,'NativeFormatImporter')):
        installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    receipt['installed_output_sha256'] = installed
    receipt['runtime_bindings'] = {path.relative_to(ROOT).as_posix():sha(path.read_bytes()) for path in (
        script,ROOT/'scripts/fixtures/home_native_backdrop_validation.cs',
        ROOT/'scripts/prepare_home_income_formula_validation.py',
        ROOT/'unity/Assets/Game/Resources/Home/HomeNativeEnvironmentAssets.asset')}
    receipt.pop('stage',None)
    receipt['scope'] = 'Original Back_Ground subtree and clip resource closure. Production scene sorting, cloud/mountain/bird roots and theme changes remain pending.'
    (ROOT/'assets/reference/home-backdrop-native-source-manifest.json').write_text(
        json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Installed original native backdrop',len(installed),'files',flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install',action='store_true')
    args = parser.parse_args()
    if args.install: install(validation_directory())
    else: HomeBackdropBinding(validation_directory()).build()
