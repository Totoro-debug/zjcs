"""Bind complete selected original scene motion clips to the native pose consumer.

Copies verified native-conversion AnimationClip payloads; only metadata GUIDs
change. Original compiled clip fields and source skeleton identities are checked.
No animation SDK implementation, DLL method or old npm runtime is inspected.
"""
from __future__ import annotations

import gzip
import hashlib
import json
from pathlib import Path
import re
import struct

from official_apk import Apk, ROOT
from prepare_battle_game_animators import documents, GUID
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed
from prepare_unity_profession_character_resources import asset, guid

DEST = ROOT / 'unity/Assets/Official/Combat/SceneMotionSources'
RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSceneMotionCatalog.cs'
RECEIPT = ROOT / 'assets/reference/battle-animation/scene-motions-source-manifest.json'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def skeleton_motions(path):
    raw = path.read_bytes()
    assert raw[:12] == b'ZJCSPROF\x01\x00\x00\x00'
    offset, strings = 12, []
    for _ in range(2):
        size, = struct.unpack_from('<I', raw, offset)
        strings.append(raw[offset + 4:offset + 4 + size].decode('utf-8'))
        offset += 4 + size
    assert strings[0] == '884_156692'
    offset += 4
    size, = struct.unpack_from('<I', raw, offset)
    compressed, = struct.unpack_from('<I', raw, offset + 36)
    decoded = gzip.decompress(raw[offset + 40:offset + 40 + compressed])
    assert len(decoded) == size and sha(decoded) == raw[offset + 4:offset + 36].hex()
    return json.loads(decoded)['animations']


def source_duration(value):
    if isinstance(value, dict):
        return max([float(value.get('time', 0))] + [source_duration(row) for row in value.values() if isinstance(row, (dict, list))])
    if isinstance(value, list):
        return max([0] + [source_duration(row) for row in value if isinstance(row, (dict, list))])
    return 0


def prepare():
    meta(RUNTIME)
    apk = Apk.current()
    assert apk.version == '884_156692'
    cache = ROOT / '.tools/source-packages/current/884_156692/native-conversion'
    conversion = cache / 'native-export-all-source-manifest.json'
    receipt = json.loads(conversion.read_bytes())
    inputs = cache / 'source-manifest.json'
    assert receipt['manifest'] == apk.version and receipt['completed'] and sha(inputs.read_bytes()) == receipt['input_manifest_sha256']
    export = cache / receipt['export_directory']
    checked = {row['path']: row['sha256'] for row in receipt['outputs']}
    source = export / 'ExportedProject/Assets/AppearanceAssets/Character/Model'
    earlier_path = ROOT / 'assets/reference/battle-animation/game-animators-source-manifest.json'
    earlier = json.loads(earlier_path.read_bytes())
    assert earlier['manifest'] == apk.version
    reused = {row['native_source']: row for row in earlier['assets'] if row['original']['type'] == 'AnimationClip'}
    entries, records, folder_outputs, counts, duration_differences = [], [], {}, {}, []
    for sex in ('male', 'female'):
        model = ROOT / ('unity/Assets/Official/Combat/SceneCharacterSources/' + sex + '.bytes')
        motions = skeleton_motions(model)
        native = {}
        for path in sorted((source / (sex.title() + '_1') / 'Anim').glob('*.anim')):
            for item in (path, path.with_name(path.name + '.meta')):
                assert sha(item.read_bytes()) == checked[item.relative_to(export).as_posix()], item
            fields = next(iter(documents(path.read_bytes()).values()))
            assert fields['m_Name'] not in native
            native[fields['m_Name']] = (path, fields)
        assert set(native) == set(motions), 'Current skeleton and original carrier motion closure differ'
        originals = {}
        bundle = 'defaultpackage_assets_appearanceassets_character_model_' + sex + '_1_anim.bundle'
        for container, pointer in apk.container(bundle):
            obj = pointer.deref()
            if obj.type.name != 'AnimationClip':
                continue
            value = obj.read()
            assert value.m_Name not in originals
            originals[value.m_Name] = (container, obj, value)
        assert set(originals) == set(native), 'Original APK clip closure differs from native conversion'
        counts[sex] = dict(all=len(native), battle=sum(name.startswith(('100/', '200/')) for name in native))
        for name, (path, fields) in sorted(native.items()):
            container, original, value = originals[name]
            settings = fields['m_AnimationClipSettings']
            start, stop = value.m_MuscleClip.m_StartTime, value.m_MuscleClip.m_StopTime
            assert abs(settings['m_StartTime'] - start) < .000001 and abs(settings['m_StopTime'] - stop) < .000001
            assert fields['m_SampleRate'] == value.m_SampleRate and bool(fields['m_Legacy']) == bool(value.m_Legacy)
            assert bool(settings['m_LoopTime']) == bool(value.m_MuscleClip.m_LoopTime)
            duration = source_duration(motions[name])
            if abs((stop - start) - duration) > 1 / 30 + .00001:
                duration_differences.append(dict(sex=sex, name=name, clip_duration=stop - start, native_animation_duration=duration))
            assert not GUID.findall(path.read_text('utf-8')), 'Review external clip dependencies before copying'
            relative = path.relative_to(ROOT).as_posix()
            retained = reused.get(relative)
            if retained:
                assert retained['original']['sha256'] == sha(original.get_raw_data())
                for output, expected in retained['outputs'].items():
                    assert sha((ROOT / output).read_bytes()) == expected
                target = next(ROOT / output for output in retained['outputs'] if not output.endswith('.meta'))
            else:
                target = DEST / path.relative_to(source)
                target.parent.mkdir(parents=True, exist_ok=True)
                raw = path.read_bytes()
                if target.exists():
                    assert target.read_bytes() == raw, 'Refuse changed native clip output: ' + str(target)
                else:
                    target.write_bytes(raw)
                original_meta = path.with_name(path.name + '.meta').read_text('utf-8')
                target_guid = hashlib.sha256(('zjcs:scene-motion:' + path.relative_to(source).as_posix()).encode()).hexdigest()[:32]
                metadata = GUID.sub('guid: ' + target_guid, original_meta)
                meta_path = target.with_name(target.name + '.meta')
                if meta_path.exists():
                    assert meta_path.read_bytes() == metadata.encode('utf-8'), 'Review changed native clip metadata'
                else:
                    meta_path.write_bytes(metadata.encode('utf-8'))
                parent = target.parent
                while parent.is_relative_to(DEST):
                    meta(parent, folder=True)
                    folder_outputs[parent.with_name(parent.name + '.meta').relative_to(ROOT).as_posix()] = identity(parent.with_name(parent.name + '.meta'))
                    parent = parent.parent
            entries.append(dict(Sex=sex, Clip=dict(fileID=7400000, guid=guid(target), type=2)))
            records.append(dict(sex=sex, name=name, duration=stop - start, native_animation_duration=duration, loop=bool(settings['m_LoopTime']),
                                original=dict(apk.provenance(original), container=container, path_id=str(original.path_id),
                                              payload_sha256=sha(original.get_raw_data())),
                                native=identity(path), native_meta=identity(path.with_name(path.name + '.meta')),
                                reused=bool(retained), outputs=[identity(target), identity(target.with_name(target.name + '.meta'))]))
    meta(RUNTIME)
    catalog = DEST / 'Resources/Combat/SceneAppearance/player-motions.asset'
    catalog.parent.mkdir(parents=True, exist_ok=True)
    parent = catalog.parent
    while parent.is_relative_to(DEST):
        meta(parent, folder=True)
        folder_outputs[parent.with_name(parent.name + '.meta').relative_to(ROOT).as_posix()] = identity(parent.with_name(parent.name + '.meta'))
        parent = parent.parent
    asset(catalog, 'Complete current original scene motion clips', RUNTIME, dict(entries=entries))
    proof = dict(manifest=apk.version, apk_manifest=apk.source.apk_version, apk_sha256=apk.source.apk_sha256,
                 native_conversion=identity(conversion), native_inputs=identity(inputs),
                 dependencies=[identity(earlier_path), identity(ROOT / 'assets/reference/battle-animation/scene-core-source-manifest.json')],
                 skeletons=[identity(ROOT / ('unity/Assets/Official/Combat/SceneCharacterSources/' + sex + '.bytes')) for sex in ('male', 'female')],
                 counts=counts, duration_differences=duration_differences, clips=records, folders=list(folder_outputs.values()),
                 outputs=[identity(path) for path in (RUNTIME, RUNTIME.with_name(RUNTIME.name + '.meta'), catalog, catalog.with_name(catalog.name + '.meta'))],
                 generator=identity(Path(__file__).resolve()),
                 binding='Combat/SceneAppearance/player-motions; exact source sex and native motion name select original current AnimationClip objects. Preserve original name, duration, loop and marker curves; native skeleton channels produce visible motion. Reuse already imported Game controller clips by their verified native source identity.',
                 limits=['The original APK clip container names denote controller subassets; native file paths are conversion paths, not fabricated official resource addresses.',
                         'This catalog does not select skill rules, alter original controller graphs or replace historical 854_156383 skill configuration with a new version label.',
                         'Original carrier/native duration differences are recorded and preserved. The existing native pose producer still rejects a duration mismatch; no clip is stretched and no missing behavior is inferred from catalog inclusion.',
                         'Full physics, other mixing domains, current skill prefab cue/FX/audio migration and production battle/Windows Player integration remain required.'])
    write_changed(RECEIPT, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Bound complete current scene motions:', counts, 'reused', sum(row['reused'] for row in records))


if __name__ == '__main__':
    prepare()
