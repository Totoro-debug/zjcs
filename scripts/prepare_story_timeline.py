"""Recover the original standalone story Timeline and camera dependency bindings."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from official_manifest import read_asset_manifest
from prepare_current_partner_native import identity, pointer
from prepare_partner_card_native import guid, script_id, sha
from prepare_partner_roster_native import PartnerRosterBinding
from prepare_activity_battle_selector_native import encode
from prepare_unity_story_sound_runtime import runtime_meta
from validation_workspace import validation_directory

RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Official/StoryTimeline'
DEST = ROOT / 'unity/Assets/Official/StoryUI/Timeline'
CLIENT = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source'


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes()))


def write(path, raw):
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and path.read_bytes() == raw: return
    temp = validation_directory() / ('write-' + hashlib.md5(str(path).encode()).hexdigest())
    temp.write_bytes(raw)
    temp.replace(path)


def runtime():
    for name in ('CinemachineTrackStoryBinder', 'VirtualCameraFollowBinder'):
        text = (CLIENT / (name + '.cs')).read_text('utf8')
        text = re.sub(r'^using (Framework[^;]*|Microsoft.Extensions.DependencyInjection);\n', '', text, flags=re.M)
        text = re.sub(r'^\s*//IL_[^\n]*\n', '\n', text, flags=re.M)
        text = re.sub(r'\s*\[Inject\]\s*private IStoryNodeRegisterMgr NodeRegisterManager \{ get; set; \}',
            '\n\tprivate IStoryTimelineBindings Bindings;\n\tpublic void Bind(IStoryTimelineBindings bindings) => Bindings = bindings;', text)
        text = text.replace('NodeRegisterManager.TryGetNode(MainCameraKey, out var node)',
            'TryGetNode(MainCameraKey, out var node)')
        if name == 'CinemachineTrackStoryBinder':
            pos = text.rfind('}')
            text = text[:pos] + '''private bool TryGetNode(string key, out GameObject node)
    { node = Bindings.ResolveNode(key); return node != null; }
''' + text[pos:]
        else:
            text = re.sub(r'\s*\[Inject\]\s*private IServiceProvider ServiceProvider \{ get; set; \}',
                '\n\tprivate IStoryTimelineBindings Bindings;\n\tpublic void Bind(IStoryTimelineBindings bindings) { Bindings = bindings; EntityConfig?.Bind(bindings); }', text)
            text = re.sub(r'\s*\[Inject\]\s*private IScriptableObjectConfig<ZSettings> mSettings \{ get; set; \}', '', text)
            text = text.replace('public CameraStage CameraStage;', 'public int CameraStage;')
            text = text.replace('ServiceProviderExtensions.Inject(ServiceProvider, (object)EntityConfig);', '')
            text = text.replace('MyEntity myEntity = EntityConfig.GetMyEntity();', 'Transform myEntity = EntityConfig.GetMyEntity();')
            text = text.replace('mSettings.Value.GetCameraZ(CameraStage)', 'Bindings.GetCameraZ(CameraStage)')
        pos = text.index('[RequireComponent')
        text = text[:pos] + 'namespace Zjcs.Official.StoryTimeline\n{\nusing Object = UnityEngine.Object;\n' + text[pos:] + '\n}\n'
        write(RUNTIME / (name + '.cs'), ('// Migrated original source; see story-timeline-source-manifest.json.\n' + text).encode())
    # Preserve the original player selector; replace its application-container
    # lookup with a per-instance binding before the original Awake executes.
    for name in ('EntityConfigBase', 'EntityConfigPlayer', 'EntityConfigType'):
        text = (CLIENT / (name + '.cs')).read_text('utf8')
        text = re.sub(r'^using (Framework[^;]*|Microsoft.Extensions.DependencyInjection);\n', '', text, flags=re.M)
        if name == 'EntityConfigBase':
            text = text.replace('MyEntity GetMyEntity();', 'Transform GetMyEntity();\n\tvoid Bind(IStoryTimelineBindings bindings);')
        if name == 'EntityConfigPlayer':
            start = text.index('\tpublic MyEntity GetMyEntity()')
            text = text[:start] + '''private IStoryTimelineBindings bindings;
    public void Bind(IStoryTimelineBindings value) => bindings = value;
    public Transform GetMyEntity() => bindings.PlayerTransform;
}
'''
        pos = min(v for v in (text.find('[Serializable]'), text.find('public ')) if v >= 0)
        text = 'using UnityEngine;\n' + text[:pos] + 'namespace Zjcs.Official.StoryTimeline\n{\n' + text[pos:] + '\n}\n'
        write(RUNTIME / (name + '.cs'), text.encode())
    write(RUNTIME / 'Zjcs.Official.StoryTimeline.asmdef', (json.dumps(dict(name='Zjcs.Official.StoryTimeline',
        references=['Cinemachine', 'Unity.Timeline', 'Zjcs.Official.StoryMasks'], autoReferenced=True), indent=2)+'\n').encode())
    for path in RUNTIME.iterdir():
        if path.is_file() and path.suffix != '.meta': runtime_meta(path)
    runtime_meta(RUNTIME)


class TimelineBinding(PartnerRosterBinding):
    def register_scripts(self):
        super().register_scripts()
        self.native_guids = set()
        paths = {name: RUNTIME / (name + '.cs') for name in ('CinemachineTrackStoryBinder', 'VirtualCameraFollowBinder')}
        for root, namespace in ((ROOT / 'unity/Packages/com.unity.cinemachine', 'Cinemachine'),
                                (ROOT / 'unity/Packages/com.unity.timeline/Runtime', 'UnityEngine.Timeline')):
            for path in root.rglob('*.cs'):
                if Path(str(path) + '.meta').exists():
                    match = re.search(r'^namespace\s+([\w.]+)', path.read_text('utf-8-sig'), re.M)
                    paths['.'.join(filter(None, [match[1] if match else '', path.stem]))] = path
        for name, path in paths.items():
            ns, _, typ = name.rpartition('.')
            value = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
            self.scripts[name] = value
            self.class_ids[script_id(ns, typ)] = value
            self.script_guids.add(value['guid'])
            self.script_sources[name] = dict(**record(path), pointer=value, meta_sha256=sha(Path(str(path)+'.meta').read_bytes()))

    def original_asset(self, obj):
        key = identity(obj)
        if key in self.original_refs: return self.original_refs[key]
        if obj.type.name != 'MonoBehaviour': raise ValueError('Unbound original Timeline dependency: ' + key)
        script = obj.read(check_read=False).m_Script.read()
        typ = '.'.join(filter(None, [script.m_Namespace, script.m_ClassName]))
        target_guid = hashlib.md5(('zjcs:story-timeline:' + key).encode()).hexdigest()
        result = dict(fileID=11400000, guid=target_guid, type=2)
        self.original_refs[key] = result
        self.script_guids.add(target_guid)
        fields = self.recovery.fields(obj)
        def convert(value):
            if isinstance(value, dict):
                if 'm_FileID' in value and 'm_PathID' in value:
                    return self.original_asset(pointer(value, obj)) if value['m_PathID'] else dict(fileID=0)
                if set(value) == {'id'}: return value['id']
                return {k: self.scripts[typ] if k == 'm_Script' else convert(v) for k,v in value.items()}
            if isinstance(value, list): return [convert(v) for v in value]
            return value
        relative = Path('Graph') / (sha(key.encode())[:16] + '.asset')
        self.queue.append((relative, target_guid, {11400000: dict(MonoBehaviour=convert(fields))}))
        self.proof[key] = dict(source=self.recovery.apk.provenance(obj), type=typ,
            original_sha256=sha(obj.get_raw_data()), fields=fields, output=relative.as_posix())
        return result

    def replace(self, value):
        if isinstance(value, dict) and value.get('guid') in self.native_guids:
            return value
        if isinstance(value, dict) and value.get('class') == 'EntityConfigPlayer' and value.get('ns') in ('','Zjcs.Official.StoryTimeline'):
            return dict(**{'class':'EntityConfigPlayer', 'ns':'Zjcs.Official.StoryTimeline', 'asm':'Zjcs.Official.StoryTimeline'})
        return super().replace(value)


def main():
    runtime()
    work = validation_directory()
    binding = TimelineBinding(work)
    recovery = binding.recovery
    bundle = 'defaultpackage_assets_prefabs_playables_displaypackage_story_timeline_map12_earthquake1.bundle'
    recovery.recover('Earthquake1', work / 'native', bundle=bundle,
        native_relative='Prefabs/Playables/DisplayPackage/Story/Timeline/Map12/Earthquake1.prefab')
    recovery.finalize()
    native, _, prefab, proof = recovery.outputs[-1]
    def collect_guids(value):
        if isinstance(value, dict):
            if value.get('guid'): binding.native_guids.add(value['guid'])
            for child in value.values(): collect_guids(child)
        elif isinstance(value, list):
            for child in value: collect_guids(child)
    collect_guids(prefab)
    for path in (native, Path(str(native)+'.meta')):
        key = path.relative_to(binding.cache / 'native-export-all').as_posix()
        if sha(path.read_bytes()) != binding.checked[key]: raise ValueError('Original native Timeline cache changed: '+key)
        binding.inputs[path.relative_to(ROOT).as_posix()] = binding.checked[key]
    for key, value in prefab['external'].items(): binding.objects[value['guid']] = binding.original(key)
    for key, file_id in prefab['objectMap'].items():
        owner = binding.original(key)
        fields = recovery.fields(owner)
        native_fields = next(iter(prefab['documents'][file_id].values()))
        binding.learn(fields, native_fields, owner)
        if owner.type.name == 'PlayableDirector':
            native_fields['m_PlayableAsset'] = binding.original_asset(pointer(fields['m_PlayableAsset'], owner))
            for old, new in zip(fields['m_SceneBindings'], native_fields['m_SceneBindings']):
                if old['key']['m_PathID']: new['key'] = binding.original_asset(pointer(old['key'], owner))
    for row in prefab['scripts']:
        prefab['documents'][row['nativeFileID']]['MonoBehaviour']['m_Script'] = binding.scripts['.'.join(filter(None,[row['namespace'],row['type']]))]
    data = binding.replace(prefab['documents'])
    noise_source = binding.cache / 'native-export-all/ExportedProject/Assets/Packages/com.unity.cinemachine/Presets/Noise/6D Wobble.asset'
    noise_meta = Path(str(noise_source) + '.meta')
    noise_guid = re.search(rb'(?m)^guid: ([0-9a-f]{32})', noise_meta.read_bytes())[1].decode()
    binding.native_guids.add(noise_guid)
    noise_bytes = noise_source.read_bytes().replace(b'248a19188c2dffaa949fefc5fd6f973a',
        guid(ROOT / 'unity/Packages/com.unity.cinemachine/Runtime/Core/NoiseSettings.cs.meta').encode())
    def remap_noise(value):
        if isinstance(value, dict):
            return {key: (dict(child, guid=noise_guid) if key == 'm_NoiseProfile' and isinstance(child, dict)
                and child.get('guid') else remap_noise(child)) for key, child in value.items()}
        if isinstance(value, list): return [remap_noise(child) for child in value]
        return value
    data = remap_noise(data)
    target_guid = hashlib.md5(b'zjcs:story-timeline:Earthquake1:prefab').hexdigest()
    binding.queue.append((Path('Resources/StoryUI/Timeline/Earthquake1.prefab'),target_guid,data))
    binding.queue.append((Path('Resources/StoryUI/Timeline/6D Wobble.asset'), noise_guid, noise_bytes))
    for _, _, queued in binding.queue:
        if isinstance(queued, dict):
            converted = binding.replace(queued)
            queued.clear(); queued.update(converted)
    records = read_asset_manifest(recovery.apk.source.manifest_bytes('DefaultPackage', recovery.apk.archive))
    settings_row, = [row for row in records if row['path'].lower() == 'assets/scriptableconfig/modules/render/zsettings.asset']
    settings, = [obj for obj in recovery.apk.objects(settings_row['bundle']) if obj.type.name == 'MonoBehaviour'
        and obj.read(check_read=False).m_Name == 'ZSettings']
    settings_fields = recovery.fields(settings)
    configuration = dict(manifest=recovery.apk.version, assets=[dict(guid='75dbb924ad6de1f46a6696666cc083ae',
        path='StoryUI/Timeline/Earthquake1')], defaultCameraZ=settings_fields['mDefaultCameraZ'],
        cameraZList=settings_fields['mCameraZList'])
    # Chapter data remains pinned to its recorded historical source. Prove the
    # exact Timeline request/ports agree before binding this current prefab;
    # this does not declare the rest of the historical chapter equivalent.
    chapter = ROOT / 'unity/Assets/Official/World/Map/12/Source/stories.json'
    original_request = json.loads(chapter.read_bytes())['FirstMeetMecna']['nodes']['16']
    graph, = [obj for obj in recovery.apk.objects('defaultpackage_assets_scriptableconfig_story_map12story.bundle')
        if obj.type.name == 'MonoBehaviour' and obj.read(check_read=False).m_Name == 'FirstMeetMecna']
    graph_fields = recovery.fields(graph)
    graph_nodes = [pointer(value, graph) for value in graph_fields['nodes']]
    decoded = {recovery.fields(obj)['NodeId']: (obj, recovery.fields(obj)) for obj in graph_nodes}
    node, selected_request = decoded[16]
    fields = ('NodeId', 'NeedMask', 'PlayableScene', 'OptionTime', 'TimeOutSafeEnd', 'StoryPrefabGuid')
    if any(original_request[field] != selected_request[field] for field in fields):
        raise ValueError('Historical chapter Timeline parameters differ from the current native binding')
    current_ids = {obj.path_id: str(key) for key, (obj, _) in decoded.items()}
    current_ports = {port['_fieldName']: dict(direction=port['_direction'], flow=port.get('_flowConnectionType',0),
        connections=[dict(node=current_ids[value['node']['m_PathID']],port=value['fieldName'])
            for value in port['connections'] if value['node']['m_PathID']]) for port in selected_request['ports']['values']}
    old_ports = {name: dict(value,connections=[v for v in value['connections'] if v['node']])
        for name,value in original_request['ports'].items()}
    if current_ports != old_ports: raise ValueError('Historical chapter Timeline ports differ from the current native binding')
    compatibility = dict(input=record(chapter), graphSource=recovery.apk.provenance(graph),
        nodeSource=recovery.apk.provenance(node), node_sha256=sha(node.get_raw_data()),
        comparedFields={field:selected_request[field] for field in fields}, comparedPorts=current_ports,
        scope='Only FirstMeetMecna node 16 Timeline request and ports; other chapter content retains its historical provenance.')
    config = DEST / 'Resources/StoryUI/Timeline/bindings.json'
    write(config, (json.dumps(configuration, indent=2)+'\n').encode())
    write(Path(str(config)+'.meta'), ('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:story-timeline:bindings').hexdigest()+'\nTextScriptImporter:\n  externalObjects: {}\n').encode())
    installed = []
    for relative, asset_guid, data in binding.queue:
        path = DEST / relative
        write(path, encode(data) if isinstance(data,dict) else data)
        importer = 'PrefabImporter:\n  externalObjects: {}\n' if path.suffix=='.prefab' else 'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n'
        write(Path(str(path)+'.meta'), ('fileFormatVersion: 2\nguid: '+asset_guid+'\n'+importer).encode())
        installed += [record(path),record(Path(str(path)+'.meta'))]
    for folder in sorted({DEST} | {p for p in DEST.rglob('*') if p.is_dir()}): runtime_meta(folder)
    installed += [record(Path(str(folder)+'.meta')) for folder in sorted({DEST}|{p for p in DEST.rglob('*') if p.is_dir()})]
    installed += [record(config),record(Path(str(config)+'.meta'))]
    write(ROOT / 'assets/reference/official-map/story-timeline-source-manifest.json', (json.dumps(dict(
        manifest=recovery.apk.version, prefab=proof, dependencies=binding.proof,
        nativeInputs=binding.inputs,
        cameraSettings=dict(source=recovery.apk.provenance(settings), original_sha256=sha(settings.get_raw_data()), fields=settings_fields),
        chapterRequestCompatibility=compatibility,
        inputs=[record(CLIENT/(name+'.cs')) for name in ('PlayStoryTimelineNode','CinemachineTrackStoryBinder','VirtualCameraFollowBinder','EntityConfigBase','EntityConfigPlayer','EntityConfigType')],
        scripts={name:binding.script_sources[name] for name in
            {'.'.join(filter(None,[row['namespace'],row['type']])) for row in prefab['scripts']}
            | {row['type'] for row in binding.proof.values() if 'type' in row and row['type'] in binding.script_sources}
            | {'UnityEngine.Timeline.TimelineAsset','CinemachineTrack','CinemachineShot','Cinemachine.NoiseSettings'}}, installed=installed,
        runtime=[record(path) for path in sorted(RUNTIME.iterdir()) if path.is_file()],
        generator=record(Path(__file__)), operation='Restore original standalone Timeline, shots, original exposed camera references, native camera hierarchy and per-instance player dependency injection; preserve original playback and lifetime ordering.'
    ),ensure_ascii=False,indent=2)+'\n').encode())
    print('Original standalone story Timeline installed:',len(installed),flush=True)


if __name__ == '__main__': main()
