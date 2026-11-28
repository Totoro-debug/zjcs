"""Migrate current native round resources and the current official presentation code."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil

from official_apk import ROOT
from official_source import OfficialSource
from prepare_current_battle_hud import identity, verify_cache, windows_references, work_directory
from prepare_current_damage_ui import CurrentDamageRecovery, normalize_checkout
from prepare_battle_round_scene_native import RoundSceneBinding, DESTINATION, RECEIPT, NAMES, main as install
from prepare_battle_round_manager_runtime import render


RUNTIME_RECEIPT = ROOT/'assets/reference/battle-hud/round-manager-runtime-source-manifest.json'
RUNTIME = ROOT/'unity/Assets/Game/Runtime/Official/BattleRoundUI'


def scene_source(path):
    text = path.read_text('utf-8-sig')
    text = re.sub(r'^\s*//IL_[^\n]*\n', '', text, flags=re.M)
    text = text.replace('Camera.main', 'SceneCamera')
    text = text.replace('private Vector3 mLastPosition',
        'private Camera mSceneCamera;\n\n\tprivate Camera SceneCamera => mSceneCamera != null ? mSceneCamera : Camera.main;\n\n\tprivate Vector3 mLastPosition')
    text = text.replace('SetAttachUI(RectTransform uiTrans)', 'SetAttachUI(RectTransform uiTrans, Camera sceneCamera = null)')
    text = text.replace('mAttachUITrans = uiTrans;', 'mAttachUITrans = uiTrans;\n\t\tmSceneCamera = sceneCamera;')
    text = text.replace('mUICamera = null;', 'mUICamera = null;\n\t\tmSceneCamera = null;')
    guard = ' && !((Object)(object)mUICamera == (Object)null)'
    if text.count(guard) != 1:
        raise ValueError('Review current scene camera guard')
    # Overlay UI has a null UI camera by definition. The selected battle camera
    # remains explicit, while the official position/scale/cache code is intact.
    text = text.replace(guard, '')
    text = text.replace('\tprivate void OnDestroy()',
        '\tprivate void OnEnable() => Canvas.willRenderCanvases += FixedPosAndScale;\n\n'
        '\tprivate void OnDisable() => Canvas.willRenderCanvases -= FixedPosAndScale;\n\n\tprivate void OnDestroy()')
    return '// Current official position, scale and cache implementation; Windows overlay/camera binding is explicit.\nnamespace Zjcs.Official.BattleRoundUI\n{\n'+text+'\n}\n'


def snapshot(work, previous):
    rows = [identity(ROOT/name) for name in previous['installed']]
    for row in rows:
        if row['sha256'] != previous['installed'][row['path']]:
            raise ValueError('Previous round resource changed: '+row['path'])
    rows += [identity(RECEIPT), identity(RUNTIME_RECEIPT)]
    rows += [identity(RUNTIME/(name+'.cs')) for name in ('GameRoundUI', 'GameRoundScene')]
    destination = work/'previous-installation'
    if destination.exists():
        saved = json.loads((destination/'snapshot.json').read_bytes())
        if not saved['completed'] or saved['files'] != rows:
            raise ValueError('Retain the different completed round snapshot')
    else:
        destination.mkdir()
        for row in rows:
            target = destination/row['path']
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT/row['path'], target)
        (destination/'snapshot.json').write_text(json.dumps(dict(completed=True, files=rows), indent=2)+'\n', 'utf-8')
    for row in rows:
        if identity(destination/row['path'])['sha256'] != row['sha256']:
            raise ValueError('Previous round snapshot differs: '+row['path'])


def prepare(work):
    work = work_directory(work)
    source = OfficialSource()
    cache = source.prepared_path/'battle-round-ui-native'
    managed, windows = verify_cache(cache), windows_references(cache)
    previous = json.loads(RECEIPT.read_bytes())
    snapshot(work, previous)
    current_code = source.prepared_path/'official-code'
    original = {Path(path).name: value for path, value in previous['inputs'].items() if '/official-code/' in path}
    compatible = []
    for name in NAMES:
        path = current_code/'client-source'/(name+'.cs')
        row = identity(path)
        if name not in ('GameRoundScene', 'GameRoundUI') and row['sha256'] != original[path.name]:
            raise ValueError('Migrate changed serialized round behaviour first: '+name)
        compatible.append(row)
    old_runtime = json.loads(RUNTIME_RECEIPT.read_bytes())
    for path, expected in old_runtime['outputs'].items():
        if identity(ROOT/path)['sha256'] != expected:
            raise ValueError('Existing round manager port changed: '+path)
    for name, content in [('GameRoundUI', render(current_code/'client-source/GameRoundUI.cs')),
                          ('GameRoundScene', scene_source(current_code/'client-source/GameRoundScene.cs'))]:
        (RUNTIME/(name+'.cs')).write_bytes(content.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))
    runtime_inputs = {}
    for path in old_runtime['inputs']:
        selected = current_code/path.split('/official-code/',1)[1]
        row = identity(selected); runtime_inputs[row['path']] = row['sha256']
    row = identity(current_code/'client-source/GameRoundScene.cs'); runtime_inputs[row['path']] = row['sha256']
    outputs = {name: identity(ROOT/name)['sha256'] for name in old_runtime['outputs']}
    for path in (RUNTIME/'GameRoundScene.cs', RUNTIME/'GameRoundScene.cs.meta'):
        row = identity(path); outputs[row['path']] = row['sha256']
    runtime_proof = dict(manifest=source.version, inputs=runtime_inputs, outputs=outputs,
        generator=identity(Path(__file__).resolve()), manager_generator=identity(ROOT/'scripts/prepare_battle_round_manager_runtime.py'),
        adaptations=list(dict.fromkeys(old_runtime['adaptations']+[
            'Current original round updates run without an additional play-phase filter.',
            'Current scene position/scale and cached reference-height/orthographic-size/canvas-height branches are preserved.',
            'Explicit Windows battle camera and overlay UI camera boundary; update at canvas render as well as original LateUpdate.'])))
    RUNTIME_RECEIPT.write_bytes((json.dumps(runtime_proof, ensure_ascii=False, indent=2)+'\n').replace('\n','\r\n').encode('utf-8'))
    recovery = CurrentDamageRecovery(native_cache=cache, export_name='native-export',
        bundle='defaultpackage_assets_prefabs_ui_fight_gameroundscene.bundle')
    try:
        binding = RoundSceneBinding(work, recovery=recovery, resource_cache=source.prepared_path/'native-conversion')
        for row in managed+compatible+[identity(RUNTIME_RECEIPT)]:
            binding.inputs[row['path']] = row['sha256']
        for name in ('prepare_current_round_ui.py', 'prepare_current_damage_ui.py',
                     'prepare_current_battle_hud.py', 'prepare_battle_round_manager_runtime.py',
                     'prepare_partner_panel_native.py', 'partner_panel_animator_binding.py'):
            row = identity(ROOT/'scripts'/name); binding.inputs[row['path']] = row['sha256']
        install(binding)
        proof = json.loads(RECEIPT.read_bytes())
        # Retire only the preceding module-owned resources after its verified
        # snapshot exists. Shared resources are never removed here.
        for name, expected in previous['installed'].items():
            path = ROOT/name
            if name in proof['installed'] or path.suffix == '.meta' and Path(str(path)[:-5]).is_dir():
                continue
            if not path.resolve().is_relative_to(DESTINATION.resolve()) or identity(path)['sha256'] != expected:
                raise ValueError('Refusing changed obsolete round resource: '+name)
            path.unlink()
        normalize_checkout(proof)
        proof.update(apk_sha256=source.apk_sha256, input_manifest=identity(cache/'source-manifest.json'),
            native_export=identity(cache/'native-export-source-manifest.json'), windows=windows,
            current_behaviour_sources=compatible, generator=identity(Path(__file__).resolve()))
        RECEIPT.write_bytes((json.dumps(proof, ensure_ascii=False, indent=2)+'\n').replace('\n','\r\n').encode('utf-8'))
        verify()
    finally:
        recovery.apk.close()


def verify():
    proof = json.loads(RECEIPT.read_bytes()); source = OfficialSource()
    if proof['manifest'] != source.version or proof['apk_sha256'] != source.apk_sha256:
        raise ValueError('Installed round source differs')
    rows = [proof[key] for key in ('input_manifest','native_export','generator')]
    rows += proof['windows']['files']
    rows += [proof['windows'][key] for key in ('inventory','original_inputs','shared_inventory')]
    rows += [row['source'] for row in proof['windows']['catalogs']]
    rows += [dict(path=path,sha256=value) for key in ('inputs','installed','runtime_ports') for path,value in proof[key].items()]
    for path, row in proof['shared_resources'].items():
        rows.extend([dict(path=path,sha256=row['sha256']),dict(path=path+'.meta',sha256=row['meta_sha256'])])
    for row in rows:
        if identity(ROOT/row['path'])['sha256'] != row['sha256']:
            raise ValueError('Round input or installed payload changed: '+row['path'])
    print('Verified current round installation:',len(proof['installed']),'files.',flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work',type=Path)
    parser.add_argument('--verify',action='store_true')
    args = parser.parse_args()
    if args.verify: verify()
    else:
        if args.work is None: parser.error('--work is required for replacement')
        prepare(args.work)
