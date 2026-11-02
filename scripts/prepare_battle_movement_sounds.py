"""Recover selected original movement sound cues, event routes and unmodified PCM.

No Wwise DSP approximation is installed by this extraction. The public playable
adapter requires an explicit event backend; decoded samples alone are not one.
"""
import json
import os
from pathlib import Path
import subprocess
import wave

from current_component_schemas import CurrentSchemas
from map14_audio_source import AudioSource, sha
from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed
from prepare_character_creation_opening_audio import read_field
from prepare_unity_map14_audio import fields
from prepare_unity_map14_playable_audio import action_contract, audio_meta
from UnityPy.classes import PPtr

DATA = ROOT / 'unity/Assets/Game/Resources/Combat/Audio/movement.json'
MEDIA = ROOT / 'unity/Assets/Official/Combat/Audio/Resources/Combat/Audio/Media'
RECEIPT = ROOT / 'assets/reference/battle-animation/movement-sounds-source-manifest.json'
DECODER = ROOT / '.tools/vgmstream/vgmstream-cli.exe'


def build():
    work = Path(os.environ['ZJCS_VALIDATION_DIR']).resolve()
    assert work.parent == (ROOT / 'output/validation').resolve() and (work / '.zjcs-validation.json').is_file()
    apk, schemas = Apk.current(), CurrentSchemas()
    assert apk.version == '884_156692', 'Inspect changed original movement/audio source version'
    audio = AudioSource(expected_version=apk.version, work_directory=work)
    rules = ROOT / 'unity/Assets/Official/Combat/battle-rules.json'
    entities = json.loads(rules.read_bytes())['entities']
    skills = {int(v['FightRoleCharacterComponent'].get(k, 0))
              for v in entities.values() if 'FightRoleCharacterComponent' in v
              for k in ('MoveSkillId', 'BackMoveSkillId', 'FixSpeedMoveSkillId')} - {0}
    actions = {int(row['MoveBehaviourId']) for key in skills
               for row in entities[str(key)]['FightSkillComponent']['SourceMoveList']}
    assets = read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))
    inputs, omitted, references, routes, decoded, outputs = [], [], {}, {}, {}, []
    previous = {}
    if RECEIPT.exists():
        for row in json.loads(RECEIPT.read_bytes())['outputs']:
            path = ROOT / row['path']
            if path.suffix in ('.cs', '.asmdef') or path.name.endswith('.cs.meta'):
                continue  # Reviewed bindings are inputs to the next extraction.
            assert sha(path.read_bytes()) == row['sha256'], 'Review changed sound output: ' + row['path']
            previous[path] = row['sha256']

    def write(path, raw):
        path.parent.mkdir(parents=True, exist_ok=True)
        if path.exists() and path not in previous and path.read_bytes() != raw:
            raise ValueError('Unowned sound output: ' + str(path))
        if not path.exists() or path.read_bytes() != raw:
            path.write_bytes(raw)

    def pointer(owner, value):
        return PPtr(**value, assetsfile=owner.assets_file).deref() if value['m_PathID'] else None

    def component(go, name):
        for ref in go.read().m_Component:
            obj = ref.component.deref()
            if obj.type.name == 'MonoBehaviour' and obj.read(check_read=False).m_Script.read().m_ClassName == name:
                return obj
        raise ValueError('Missing original component: ' + name)

    def read(obj):
        value = schemas.read(obj)
        inputs.append(dict(source=apk.provenance(obj), fields=value))
        return value

    def event(owner, value):
        obj = pointer(owner, value['WwiseObjectReference'])
        if obj is None:
            return 0
        value = read(obj)
        old = references.setdefault(value['id'], value)
        assert old == value
        return value['id']

    def walk(obj, time, cues, owners, parent=None):
        name = obj.read(check_read=False).m_Script.read().m_ClassName
        value = read(obj)
        assert value['mySpeed'] == 1 and value['myFadeInDuration'] == 0 and value['myFadeOutDuration'] == 0
        assert not value['myTriggerList'] and not value['myFadeoutTrigerList'] and not value['myEndTrigerList']
        if name in ('MyPlayableSequence', 'MyPlayableSound'):
            assert all(not value[k]['m_PathID'] for k in ('mySource', 'myTarget', 'myApplyComponent', 'Prefab')), (name, value)
        if name == 'MyPlayableSequence':
            before = len(cues)
            owner = (apk.provenance(obj)['bundle'], obj.path_id)
            for row in value['myPlayables']:
                assert row['myTime'] >= 0
                walk(pointer(obj, row['myPlayable']), time + row['myTime'], cues, owners, owner)
            if len(cues) != before:
                assert time == 0, 'Preserve delayed nested sound sequence frame boundaries before flattening'
        elif name == 'MyPlayableSound':
            owners.add(parent)
            cues.append(dict(time=time, speed=value['mySpeed'], duration=value['Duration'],
                loop_interval=value['LoopInterval'], event_id=event(obj, value['SoundEvent']),
                stop_event_id=event(obj, value['StopSoundEvent'])))
        else:
            assert name in ('MyPlayableTrack', 'MyPlayableFollowBone', 'MyPlayableAnimator'), name
            omitted.append(dict(type=name, time=time, source=apk.provenance(obj)))

    result = dict(manifest=apk.version, skills=[], actions=[], events=[])
    try:
        for kind, ids, cls in (('skills', skills, 'MyFightSkill'), ('actions', actions, 'MyFightActionSkillMove')):
            for key in sorted(ids):
                asset = next(row for row in assets if row['path'].endswith('/' + str(key) + '.prefab'))
                owner = component(apk.asset(asset['bundle'], '/' + str(key) + '.prefab'), cls)
                value = read(owner)
                if kind == 'skills':
                    assert all(not value[k]['m_PathID'] for k in
                               ('UpPlayablePrefab', 'DownPlayablePrefab', 'LeftPlayablePrefab', 'RightPlayablePrefab'))
                root = pointer(owner, value['myPlayablePrefab'])
                cues, sound_owners = [], set()
                if root:
                    walk(component(root, 'MyPlayableSequence'), 0., cues, sound_owners)
                assert len(sound_owners) <= 1, 'Preserve interleaved original sound sequences before flattening'
                result[kind].append(dict(id=key, cues=cues))
        for key, reference in sorted(references.items()):
            event_id, banks = audio.load_event(reference['objectName'])
            assert event_id == key
            action_ids = [v for n, v, _ in fields(audio.by_id[key][0]) if n == 'ulActionID']
            assert len(action_ids) == 1, 'Inspect original ordered event actions'
            contract = action_contract(audio.by_id[action_ids[0]][0])
            assert contract['actionType'] == 1027 and contract['delayMs'] == 0
            routes.update(audio.routing(event_id))
            routes.update(audio.routing(contract['id']))
            sound = audio.by_id[contract['target']][0]
            assert read_field(sound, 'eHircType') == 2 and read_field(sound, 'ulPluginID') == 262145
            assert read_field(sound, 'StreamType') == 0
            media_id = read_field(sound, 'sourceID')
            routes.update(audio.routing(contract['target']))
            if media_id not in decoded:
                raw, sources = audio.media(media_id)
                wem, wav = work / (str(media_id) + '.wem'), work / (str(media_id) + '.wav')
                wem.write_bytes(raw)
                subprocess.run([str(DECODER), '-o', str(wav), str(wem)], check=True, capture_output=True)
                with wave.open(str(wav), 'rb') as pcm:
                    assert pcm.getcomptype() == 'NONE' and pcm.getsampwidth() == 2
                    info = dict(channels=pcm.getnchannels(), sample_rate=pcm.getframerate(),
                        frames=pcm.getnframes(), sample_width=2, pcm_sha256=sha(pcm.readframes(pcm.getnframes())))
                target = MEDIA / (str(media_id) + '.wav')
                write(target, wav.read_bytes())
                importer = audio_meta(target)
                # Match repository LF bytes before recording the output hash;
                # the reused Map14 importer writer uses platform newlines.
                importer.write_bytes(importer.read_bytes().replace(b'\r\n', b'\n'))
                outputs.extend((identity(target), identity(target.with_name(target.name + '.meta'))))
                decoded[media_id] = dict(media_id=media_id, wem_sha256=sha(raw), sources=sources,
                    clip_resource='Combat/Audio/Media/' + str(media_id), **info)
            result['events'].append(dict(id=key, name=reference['objectName'], guid=reference['guid'],
                action_id=contract['id'], sound_id=contract['target'], **{k: v for k, v in decoded[media_id].items()
                    if k not in ('sources', 'wem_sha256')}))
        write(DATA, (json.dumps(result, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
        meta(DATA)
        for path in (DATA.parent, MEDIA):
            while not path.with_name(path.name + '.meta').exists():
                meta(path, folder=True)
                path = path.parent
        outputs.extend((identity(DATA), identity(DATA.with_name(DATA.name + '.meta'))))
        for path in (DATA.parent, MEDIA, MEDIA.parent, MEDIA.parent.parent, MEDIA.parent.parent.parent, MEDIA.parent.parent.parent.parent):
            if path.with_name(path.name + '.meta').exists():
                outputs.append(identity(path.with_name(path.name + '.meta')))
        original = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
        runtime = ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat'
        bound = [runtime / name for name in ('OfficialBattleSoundCatalog.cs', 'OfficialBattleSoundPlayback.cs', 'OfficialBattleSoundSequence.cs')]
        bound += [ROOT / 'unity/Assets/Game/Editor/BattleSounds/OfficialBattleSoundAcceptance.cs']
        for path in bound:
            meta(path)
        editor_folder = bound[-1].parent
        meta(editor_folder, folder=True)
        editor_assembly = editor_folder / 'Zjcs.Editor.BattleSounds.asmdef'
        meta(editor_assembly)
        outputs.extend(identity(path) for path in
            (editor_folder.with_name(editor_folder.name + '.meta'), editor_assembly,
             editor_assembly.with_name(editor_assembly.name + '.meta'), runtime / 'Zjcs.Presentation.Combat.asmdef'))
        proof = dict(manifest=apk.version, selection=identity(rules), objects=inputs,
            omitted_non_sound_nodes=omitted, decoded=list(decoded.values()), packages=audio.receipts(),
            routing_objects=routes, events=result['events'],
            application=[identity(original / 'client-source' / name) for name in
                         ('MyPlayableSound.cs', 'GameSoundSpeedController.cs', 'MyFightSkill.cs', 'MyFightActionSkillMove.cs')],
            framework=[identity(original / 'game-framework-source/GameFramework' / name) for name in
                       ('MyPlayable.cs', 'MyPlayableSequence.cs', 'MyPlayableCenter.cs')],
            tools=[identity(Path(__file__)), identity(ROOT / 'scripts/map14_audio_source.py'), identity(DECODER)],
            outputs=sorted({x['path']: x for x in outputs + [identity(p) for f in bound
                           for p in (f, f.with_name(f.name + '.meta'))]}.values(), key=lambda r: r['path']),
            binding='Shared sound scheduler instantiates migrated MyPlayableSound and MyPlayableSequence with an explicit IProfessionSoundHost. Skill and per-cell action owners are independent; cancellation is explicit. Extracted samples are unnormalized PCM, not an implementation of the original Wwise mixer.',
            limitations=['Production battle host wiring awaits the original Wwise bus/filter/EQ/compressor/limiter/TimeStretch backend.',
                'Game-speed RTPC 414249091 drives the original non-reciprocal eight-point TimeStretch curve; AudioSource.pitch is not a substitute.',
                'Only the selected 21 role movement skills and 21 movement actions are included. Other cast, hit and follow-bone sound/effect nodes remain pending.',
                'Sound cues retain their original relative times; non-sound siblings and parent end effects are owned by the separate visual scheduler.'])
        write_changed(RECEIPT, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
        print('Original movement sounds:', {k: len(result[k]) for k in ('skills', 'actions', 'events')},
              'cues=', sum(len(r['cues']) for k in ('skills', 'actions') for r in result[k]))
    finally:
        apk.archive.close()
        audio.close()


if __name__ == '__main__':
    build()
