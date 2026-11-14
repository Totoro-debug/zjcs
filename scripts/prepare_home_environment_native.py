"""Bind the selected original Day/Night scene subtrees and native animation clips."""
import hashlib
import json
from pathlib import Path
import re

import yaml
from UnityPy.enums import ClassIDType

from official_apk import ROOT
from official_manifest import read_asset_manifest
from prepare_current_partner_native import documents, identity, pointer
from prepare_partner_card_native import PartnerCardBinding, Dumper, guid, script_id, sha
from validation_workspace import validation_directory


DESTINATION = ROOT/'unity/Assets/Official/Home/EnvironmentNative'
RECEIPT = ROOT/'assets/reference/home-environment-native-source-manifest.json'


def metadata(path, importer, folder=False):
    target = Path(str(path)+'.meta')
    value = hashlib.md5(('zjcs:home-native-environment:'+path.relative_to(ROOT).as_posix()).encode()).hexdigest()
    text = 'fileFormatVersion: 2\nguid: '+value+'\n'
    if folder: text += 'folderAsset: yes\n'
    text += importer+':\n  externalObjects: {}\n'
    if importer == 'MonoImporter': text += '  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n'
    if target.exists():
        if not re.search(r'(?m)^guid: [0-9a-f]{32}$',target.read_text('utf-8-sig')):
            raise ValueError('Existing metadata has no Unity GUID: '+str(target))
        return target
    target.write_text(text, 'utf8', newline='\n')
    return target


class HomeEnvironmentBinding(PartnerCardBinding):
    def load(self, source_guid):
        result = super().load(source_guid)
        self.script_guids.add(result['guid'])
        return result

    def register_scripts(self):
        super().register_scripts()
        paths = {
            'CameraFollower': ROOT/'unity/Assets/Game/Runtime/Official/HomeSceneUiControls/HomeCameraFollower.cs',
            'Home.HomeFadeProxy': ROOT/'unity/Assets/Game/Runtime/Official/HomeSceneUiControls/HomeFadeProxy.cs',
            'UnityEngine.Rendering.Universal.Light2D': ROOT/'unity/Packages/com.unity.render-pipelines.universal/Runtime/2D/Light2D.cs',
        }
        for name, path in paths.items():
            ref = dict(fileID=11500000, guid=guid(Path(str(path)+'.meta')), type=3)
            self.scripts[name] = ref
            ns, _, leaf = name.rpartition('.')
            self.class_ids[script_id(ns,leaf)] = ref
            self.script_guids.add(ref['guid'])
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=ref,
                sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path)+'.meta').read_bytes()))

    def build(self):
        bundle = 'defaultpackage_assets_scenes_main.bundle'
        objects = self.recovery.apk.objects(bundle)
        manager, = [o for o in objects if o.type.name == 'MonoBehaviour' and
            o.read(check_read=False).m_Script.deref().read(check_read=False).m_ClassName == 'HomeEnvironmentManagerMono']
        playable = pointer(self.recovery.fields(manager)['HomeTimeDriver']['Playable'],manager)
        binding = pointer(self.recovery.fields(playable)['PlayableBinding'],playable)
        timeline = pointer(self.recovery.fields(playable)['DefaultTimelineAsset'],playable)
        native_path = self.native/'Scenes/Main.unity'
        native_docs = documents(native_path.read_text('utf8'))
        native_playable, = [v['MonoBehaviour'] for v in native_docs.values()
            if 'DefaultTimelineAsset' in v.get('MonoBehaviour',{})]
        self.learn(self.recovery.fields(playable),native_playable,playable)

        def native_fields(obj, ref, local=None):
            if ref.get('guid'):
                path = self.native_path(ref['guid'],obj)
                key = path.relative_to(self.cache/'native-export-all').as_posix()
                if sha(path.read_bytes()) != self.checked[key] or sha(Path(str(path)+'.meta').read_bytes()) != self.checked[key+'.meta']:
                    raise ValueError('Native timeline dependency changed: '+key)
                self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
                self.inputs[path.relative_to(ROOT).as_posix()+'.meta'] = sha(Path(str(path)+'.meta').read_bytes())
                local = documents(path.read_text('utf8'))
            value = next(iter(local[ref['fileID']].values()))
            self.learn(self.recovery.fields(obj),value,obj)
            return value,local

        native_timeline,timeline_docs = native_fields(timeline,native_playable['DefaultTimelineAsset'])
        clips = {}; roots = {}; selections = {}
        for row in self.recovery.fields(binding)['AnimatorBindings']:
            name = row['Key']
            if name not in ('Day','Night'): raise ValueError('Bind additional original timeline output: '+name)
            animator = pointer(row['Animator'],binding)
            original_root = animator.read().m_GameObject.deref()
            native_root, = [key for key, value in native_docs.items() if
                value.get('GameObject',{}).get('m_Name') == original_root.read().m_Name]
            self.recovery.recover(original_root.read().m_Name,self.work/'home-environment-native',bundle=bundle,
                native_relative='Scenes/Main.unity',source_root=original_root,native_root_id=native_root)
            selections[name] = dict(source_root=identity(original_root),source_animator=identity(animator),native_root=native_root)
        self.recovery.finalize()
        for name, output in zip(('Day','Night'),self.recovery.outputs):
            _,_,panel,record = output
            for key, ref in panel['external'].items(): self.objects[ref['guid']] = self.original(key)
            for key, file_id in panel['objectMap'].items():
                owner = self.original(key)
                self.learn(self.recovery.fields(owner),next(iter(panel['documents'][file_id].values())),owner)
            for script in panel['scripts']:
                fields = panel['documents'][script['nativeFileID']]['MonoBehaviour']
                full = '.'.join(filter(None,(script['namespace'],script['type'])))
                fields['m_Script'] = self.scripts[full]
                if full == 'CameraFollower':
                    selections[name].setdefault('camera_follow_targets',[]).append(dict(
                        source=script['original'],original_target=fields['Target']))
                    fields['Target'] = dict(fileID=0)
            resolved = self.replace(panel['documents'])
            target_guid = hashlib.md5(('zjcs:home-native-environment:'+name).encode()).hexdigest()
            self.queue.append((Path(name+'.prefab'),target_guid,resolved))
            roots[name] = dict(fileID=panel['root'],guid=target_guid,type=3)
        for track_ptr,native_track_ptr in zip(self.recovery.fields(timeline)['m_Tracks'],native_timeline['m_Tracks'],strict=True):
            track = pointer(track_ptr,timeline); fields = self.recovery.fields(track)
            native_track,track_docs = native_fields(track,native_track_ptr,timeline_docs)
            name = fields['m_Name']
            if name not in roots: raise ValueError('Bind original track '+name)
            records = fields['m_Clips']
            if len(records) != 1: raise ValueError('Restore original track blending before adding multiple clips')
            row = records[0]
            if row['m_Start'] != 0 or row['m_ClipIn'] != 0 or row['m_TimeScale'] != 1:
                raise ValueError('Restore authored timeline offsets')
            playable_clip = pointer(row['m_Asset'],track)
            native_playable_clip,_ = native_fields(playable_clip,native_track['m_Clips'][0]['m_Asset'],track_docs)
            clip = pointer(self.recovery.fields(playable_clip)['m_Clip'],playable_clip)
            for obj in (track,playable_clip,clip):
                self.proof[identity(obj)] = dict(source=self.recovery.apk.provenance(obj),original_sha256=sha(obj.get_raw_data()))
            # Follow the paired original CAB/PathID and native GUID chain all the
            # way from Main -> HomeTimeline -> track -> playable asset -> clip.
            original_guid = native_playable_clip['m_Clip']['guid']
            self.objects[original_guid] = clip
            clips[name] = self.load(original_guid)
            selections[name]['clip'] = dict(source=identity(clip),timeline_record=row)
        assets = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage',self.recovery.apk.archive))
        config, = [o for o in self.recovery.apk.objects('defaultpackage_assets_scriptableconfig_modules_home.bundle')
            if o.type.name == 'MonoBehaviour' and o.read(check_read=False).m_Name == 'HomeEnvironmentConfig']
        moon_config = self.recovery.fields(config)['MoonConfig']
        moons = []
        for phase in range(1,moon_config['DayCycle']//moon_config['DayDuration']+1):
            row = dict(Index=phase)
            for field,suffix in (('Moon',''),('Outline','_Outline'),('Glow','_Glow')):
                address = 'Assets/Image/Home/Dynamic/Moon/Moon_'+str(phase)+suffix+'.png'
                record, = [a for a in assets if a['path'] == address]
                obj, = [p.deref() for path,p in self.recovery.apk.container(record['bundle'])
                    if path.casefold() == record['path'].casefold() and p.deref().type.name == 'Sprite']
                source_key,source_id = self.recovery.fields(obj)['m_RenderDataKey']
                render_guid = ''.join(format(source_key['data['+str(i)+']'],'08x')[::-1] for i in range(4))
                if render_guid != record['guid']: raise ValueError('Selected moon render key differs')
                name = self.name_key(obj.read(check_read=False).m_Name)
                matches = []
                for key in [name]+[name+str(i) for i in range(100)]:
                    for candidate in self.by_name.get(key,[]):
                        if candidate.suffix != '.asset': continue
                        docs = documents(candidate.read_text('utf8'))
                        if len(docs) == 1 and next(iter(docs.values())).get('Sprite',{}).get('m_RenderDataKey') == {render_guid:source_id}:
                            matches.append(candidate)
                native_sprite, = matches
                source_guid = guid(Path(str(native_sprite)+'.meta'))
                self.objects[source_guid] = obj;row[field] = self.load(source_guid)
                self.proof[identity(obj)]['selected_asset'] = record
            moons.append(row)
        # Dependencies discovered during binding can append to this queue.
        for target,_,data in self.queue:
            if isinstance(data,dict):
                resolved = self.replace(data);data.clear();data.update(resolved)
        folder = self.work/'home-environment-native-assets'; outputs = {}
        for target,target_guid,data in self.queue:
            path = folder/target;path.parent.mkdir(parents=True,exist_ok=True)
            if isinstance(data,dict):
                text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
                for file_id,content in data.items():
                    kind, = content
                    text += '--- !u!'+str(int(ClassIDType[kind]))+' &'+str(file_id)+'\n'
                    text += yaml.dump(content,Dumper=Dumper,sort_keys=False,allow_unicode=True,width=1000000)
                raw = text.encode('utf8')
            else: raw = data
            path.write_bytes(raw)
            importer = 'PrefabImporter' if path.suffix == '.prefab' else 'ShaderImporter' if path.suffix == '.shader' else 'NativeFormatImporter'
            meta = Path(str(path)+'.meta')
            meta.write_text('fileFormatVersion: 2\nguid: '+target_guid+'\n'+importer+':\n  externalObjects: {}\n','utf8',newline='\n')
            outputs[target.as_posix()] = sha(raw);outputs[target.as_posix()+'.meta'] = sha(meta.read_bytes())
        for path in (native_path,Path(str(native_path)+'.meta')):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        for obj in (manager,playable,binding,timeline):
            self.proof[identity(obj)] = dict(source=self.recovery.apk.provenance(obj),original_sha256=sha(obj.get_raw_data()))
        cache = self.recovery.proof['component_cache']
        self.inputs[cache['path']] = cache['sha256']
        source = ROOT/'.tools/source-packages/current'/self.recovery.apk.version/'official-code/client-source'
        for relative in ('CameraFollower.cs','HomeMoon.cs','HomeTimeDriver.cs','HomePlayable/TimelinePlayable.cs'):
            path = source/relative
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        self.inputs[Path(__file__).relative_to(ROOT).as_posix()] = sha(Path(__file__).read_bytes())
        receipt = dict(manifest=self.recovery.apk.version,inputs=self.inputs,objects=self.proof,
            outputs=outputs,roots=roots,clips=clips,moons=moons,selection=selections,scripts=self.script_sources,
            recovery=self.recovery.proof)
        (self.work/'home-environment-native-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8')
        print('Staged native home environment',len(outputs),'files',flush=True)


def install(work):
    receipt = json.loads((work/'home-environment-native-source-manifest.json').read_bytes())
    for path,expected in receipt['inputs'].items():
        if sha((ROOT/path).read_bytes()) != expected: raise ValueError('Changed input '+path)
    folder = work/'home-environment-native-assets'
    for path,expected in receipt['outputs'].items():
        if sha((folder/path).read_bytes()) != expected: raise ValueError('Changed staged output '+path)
    # Reuse the exact GUID and bytes if another migrated feature already owns it.
    import subprocess
    wanted = work/'home-environment-native-guids.txt'
    wanted.write_text(''.join('guid: '+guid(folder/path)+'\n' for path in receipt['outputs'] if path.endswith('.meta')),'utf8')
    found = subprocess.run(['rg','-l','-F','-f',str(wanted),'unity/Assets','-g','*.meta'],cwd=ROOT,capture_output=True,text=True)
    if found.returncode not in (0,1): raise ValueError(found.stderr)
    existing = {guid(ROOT/path):ROOT/path[:-5] for path in found.stdout.splitlines()}
    installed = {}
    for relative in receipt['outputs']:
        if relative.endswith('.meta'): continue
        source = folder/relative;ref = guid(Path(str(source)+'.meta'))
        target = existing.get(ref,DESTINATION/relative)
        if target.exists() and target.read_bytes() != source.read_bytes():
            raise ValueError('Preserve different existing GUID payload '+str(target))
        target.parent.mkdir(parents=True,exist_ok=True)
        if not target.exists():
            target.write_bytes(source.read_bytes());Path(str(target)+'.meta').write_bytes(Path(str(source)+'.meta').read_bytes())
        for path in (target,Path(str(target)+'.meta')):installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        parent = target.parent
        while parent.is_relative_to(DESTINATION):
            meta = metadata(parent,'DefaultImporter',True)
            installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes());parent = parent.parent
    catalog_script = ROOT/'unity/Assets/Game/Runtime/Presentation/Home/HomeNativeEnvironmentAssets.cs'
    durations = {row['clip']['timeline_record']['m_Duration'] for row in receipt['selection'].values()}
    if len(durations) != 1: raise ValueError('Restore differing original track durations')
    duration, = durations
    data = dict(MonoBehaviour=dict(m_ObjectHideFlags=0,m_CorrespondingSourceObject=dict(fileID=0),
        m_PrefabInstance=dict(fileID=0),m_PrefabAsset=dict(fileID=0),m_GameObject=dict(fileID=0),m_Enabled=1,
        m_EditorHideFlags=0,m_Script=dict(fileID=11500000,guid=guid(Path(str(catalog_script)+'.meta')),type=3),
        m_Name='HomeNativeEnvironmentAssets',m_EditorClassIdentifier='',Day=receipt['roots']['Day'],Night=receipt['roots']['Night'],
        DayClip=receipt['clips']['Day'],NightClip=receipt['clips']['Night'],Duration=duration,Moons=receipt['moons']))
    path = ROOT/'unity/Assets/Game/Resources/Home/HomeNativeEnvironmentAssets.asset'
    path.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'+yaml.dump(data,Dumper=Dumper,sort_keys=False),'utf8',newline='\n')
    for value in (path,metadata(path,'NativeFormatImporter')):installed[value.relative_to(ROOT).as_posix()] = sha(value.read_bytes())
    receipt['installed_output_sha256'] = installed
    paths = [catalog_script,
        ROOT/'unity/Assets/Game/Runtime/Presentation/Home/HomeNativeEnvironmentView.cs',
        ROOT/'unity/Assets/Game/Runtime/Presentation/Home/HomeEnvironmentView.cs',
        ROOT/'unity/Assets/Game/Runtime/Official/HomeSceneUiControls/HomeCameraFollower.cs',
        ROOT/'scripts/fixtures/home_fast_environment_validation.cs']
    receipt['runtime_bindings'] = {p.relative_to(ROOT).as_posix():sha(p.read_bytes()) for p in paths}
    receipt['adaptations'] = [
        'Extract the complete original Day_Common and Night_Common scene subtrees; detach root parents only.',
        'Bind original Light2D fields, cookie sprites, sprite geometry, native materials and Windows shader programs.',
        'Bind the serialized CameraFollower.Target to the local home camera-follow transform before Start.',
        'Use the original TimelinePlayable clip/mixer construction, explicit stage time and graph evaluation for the default two tracks.',
        'Retain the original fifteen moon sprite/outline/glow resource addresses and the existing natural-calendar phase selection.',
        'Environment skin transitions, dynamic background clips and remaining scene material/sorting bindings are pending.']
    RECEIPT.write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Installed native environment',len(installed),'files')


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__);parser.add_argument('--install',action='store_true')
    args = parser.parse_args()
    metadata(ROOT/'unity/Assets/Game/Runtime/Official/HomeSceneUiControls/HomeCameraFollower.cs','MonoImporter')
    metadata(ROOT/'unity/Assets/Game/Runtime/Presentation/Home/HomeNativeEnvironmentAssets.cs','MonoImporter')
    if args.install: install(validation_directory())
    else: HomeEnvironmentBinding(validation_directory()).build()
