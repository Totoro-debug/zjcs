"""Restore complete native 11170 cast/projectile/hit graphs and original audio input.

The decoded sample is a verification input. It does not substitute for Wwise DSP.
"""
import json
import os
from pathlib import Path
import re
import subprocess
import wave

from official_apk import ROOT
from prepare_battle_status_orbit_assets import build, SCRIPTS
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed
from prepare_battle_movement_effects import method_body
from prepare_unity_story_playable_runtime import logging_adapter
from map14_audio_source import AudioSource, sha
from prepare_unity_map14_audio import fields
from prepare_unity_map14_playable_audio import action_contract, audio_meta
from prepare_character_creation_opening_audio import read_field


RECEIPT = ROOT/'assets/reference/battle-animation/cast-assets-source-manifest.json'
CODE = ROOT/'.tools/source-packages/current/884_156692/official-code'
EVENT_ASSET = 'Wwise/ScriptableObjects/Event/C89F1758-B148-4FE4-BA3A-B6CBD3273D74.asset'


def main():
    mapped = dict(SCRIPTS)
    mapped.update({-822603460: ('StoryPlayables', 'ApplyBySource'),
        -94696243: ('StoryAnimation', 'MyPlayableOverrideAnimator'),
        1640720687: ('StorySounds', 'MyPlayableSound'),
        -741389653: ('BattleTrackPlayables', 'MyPlayableTrack'),
        602813528: ('BattleTrackPlayables', 'MyTrack'),
        1801709843: ('BattleTrackPlayables', 'MyCurveTrack'),
        180732079: ('BattleTrackPlayables', 'AnimationTrack'),
        116049708: ('BattleTrackPlayables', 'ParabolicTrack')})
    # Reusing a historical port requires current selected implementation evidence.
    compatibility = []
    for folder, name, methods in (
        ('StorySounds', 'MyPlayableSound', ('InitImpl', 'PlayImpl', 'ProcessFrameImpl', 'Stop', 'StopImpl', 'GetDuration', 'GetEventGO')),
        ('StoryAnimation', 'MyPlayableOverrideAnimator', ('PlayImpl', 'SetOverrideClip'))):
        source = CODE/'client-source'/(name+'.cs')
        target = ROOT/'unity/Assets/Game/Runtime/Official'/folder/(name+'.cs')
        original, _ = logging_adapter(source.read_text(encoding='utf-8-sig'))
        migrated = target.read_text(encoding='utf-8-sig')
        checks = {}
        for method in methods:
            bodies = [re.sub(r'\s+', '', re.sub(r'//[^\n]*', '', method_body(text, method))) for text in (original, migrated)]
            assert bodies[0] == bodies[1], (name, method)
            checks[method] = sha(bodies[0].encode())
        compatibility.append(dict(original=identity(source), migrated=identity(target), methods=checks))
    build(DEST=ROOT/'unity/Assets/Official/Combat/Casts/Resources/Combat/Casts',
        DATA=ROOT/'unity/Assets/Game/Resources/Combat/casts.json', RECEIPT=RECEIPT,
        ROOTS={}, graph_roots={key: 'Prefabs/EcEntity/Fight/FightSkill/11170/'+name+'.prefab'
            for key, name in ((11170, 'playable11170'), (11171, 'playable11171'), (11172, 'playable11172'))},
        MAPPED=mapped, family='Casts', shader_prefix='Cast_',
        runtime_receipts=('track-runtime-source-manifest.json',), generator=Path(__file__),
        reference_scripts={-210918009: dict(name='WwiseEventReference', native_asset=EVENT_ASSET,
            runtime='unity/Assets/Game/Runtime/Official/ProfessionUI/WwiseEventReference.cs',
            receipt='assets/reference/profession-ui/audio-source-manifest.json')},
        limits=['Three complete original 11170-family graphs. Formal battle skill lifecycle selection remains pending.',
            'Sound event reference and exact original PCM are retained; production Wwise bus/RTPC/filter/FX backend remains pending.'])
    proof = json.loads(RECEIPT.read_bytes())
    proof['current_runtime_compatibility'] = compatibility
    work = Path(os.environ['ZJCS_VALIDATION_DIR']).resolve()
    audio = AudioSource(expected_version='884_156692', work_directory=work)
    try:
        event_id, _ = audio.load_event('Skill_10032')
        assert event_id == 3777790779
        action_ids = [v for n, v, _ in fields(audio.by_id[event_id][0]) if n == 'ulActionID']
        assert len(action_ids) == 1, action_ids
        contract = action_contract(audio.by_id[action_ids[0]][0])
        assert contract['actionType'] == 1027 and contract['delayMs'] == 0, contract
        sound = audio.by_id[contract['target']][0]
        assert read_field(sound, 'eHircType') == 2 and read_field(sound, 'ulPluginID') == 262145
        assert read_field(sound, 'StreamType') == 0
        media_id = read_field(sound, 'sourceID')
        raw, media_sources = audio.media(media_id)
        wem, wav = work/(str(media_id)+'.wem'), work/(str(media_id)+'.wav')
        wem.write_bytes(raw)
        decoder = ROOT/'.tools/vgmstream/vgmstream-cli.exe'
        subprocess.run([str(decoder), '-o', str(wav), str(wem)], check=True, capture_output=True)
        with wave.open(str(wav), 'rb') as pcm:
            assert pcm.getcomptype() == 'NONE' and pcm.getsampwidth() == 2
            info = dict(channels=pcm.getnchannels(), sample_rate=pcm.getframerate(), frames=pcm.getnframes(),
                sample_width=2, pcm_sha256=sha(pcm.readframes(pcm.getnframes())))
        clip = ROOT/'unity/Assets/Official/Combat/Casts/Resources/Combat/Casts/Media'/(str(media_id)+'.wav')
        clip.parent.mkdir(parents=True, exist_ok=True)
        if clip.exists(): assert clip.read_bytes() == wav.read_bytes()
        else: clip.write_bytes(wav.read_bytes())
        audio_meta(clip)
        media_meta = Path(str(clip.parent)+'.meta'); meta(clip.parent, folder=True)
        data = ROOT/'unity/Assets/Game/Resources/Combat/cast-audio.json'
        row = dict(id=event_id, name='Skill_10032', action_id=contract['id'], sound_id=contract['target'],
            media_id=media_id, clip_resource='Combat/Casts/Media/'+str(media_id), **info)
        write_changed(data, json.dumps(dict(manifest='884_156692', events=[row]), indent=2)+'\n'); meta(data)
        routes = {}
        for key in (event_id, contract['id'], contract['target']): routes.update(audio.routing(key))
        proof['audio'] = dict(event=row, contract=contract, wem_sha256=sha(raw), sources=media_sources,
            packages=audio.receipts(), routing_objects=routes, decoder=identity(decoder),
            extraction_adapter=identity(ROOT/'scripts/map14_audio_source.py'))
        proof['outputs'] += [identity(path) for path in (clip, Path(str(clip)+'.meta'), media_meta, data, Path(str(data)+'.meta'))]
    finally:
        audio.close()
    # Normalize empty importer values without changing any serialized values.
    canonical = []
    for output in proof['outputs']:
        path = ROOT/output['path']
        if path.suffix != '.meta': continue
        raw = path.read_bytes(); normalized = b'\n'.join(line.rstrip() for line in raw.splitlines())+b'\n'
        if raw == normalized: continue
        assert re.search(rb'(?m)^guid: (\w+)\r?$', raw)[1] == re.search(rb'(?m)^guid: (\w+)$', normalized)[1]
        # write_changed intentionally treats CRLF as equivalent; here the
        # receipt records canonical bytes, so write those exact bytes.
        path.write_bytes(normalized)
        canonical.append(dict(path=output['path'], before_sha256=output['sha256']))
        output['sha256'] = sha(normalized)
    proof['metadata_canonicalization'] = dict(operation='LF and trailing whitespace only; field values/GUIDs unchanged', outputs=canonical)
    runtime = ROOT/'unity/Assets/Game/Runtime/Offline/BattleNativePlayables'
    editor = ROOT/'unity/Assets/Game/Editor/Combat/AnimatorPlayback'
    bound = [*runtime.glob('*.cs'), *runtime.glob('*.asmdef'), Path(str(runtime)+'.meta')]
    bound += [Path(str(path)+'.meta') for path in list(bound) if path.suffix != '.meta']
    bound += [editor/name for name in ('OfficialBattleAnimatorPlaybackAcceptance.Casts.cs',
        'OfficialBattleAnimatorPlaybackAcceptance.Casts.cs.meta',
        'OfficialBattleAnimatorPlaybackAcceptance.cs', 'OfficialBattleAnimatorPlaybackAcceptance.ScenePlayerAppearance.cs',
        'Zjcs.Editor.Combat.AnimatorPlayback.asmdef')]
    proof['bindings'] = [identity(path) for path in sorted(bound)]
    proof['binding_contract'] = 'Inject original full graphs before activation, retain original manager/center/FIFO pools, use an explicit sound backend only during synchronous callbacks, restore the previous global backend in finally, isolate ownership and release active graphs before pools. No global instantiation resolver replacement.'
    write_changed(RECEIPT, json.dumps(proof, ensure_ascii=False, indent=2)+'\n')
    print('Complete native cast graphs and original sound media:', len(proof['outputs']), 'outputs')


if __name__ == '__main__':
    main()
