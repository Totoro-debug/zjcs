"""Restore the complete original HP subtrees and faction effect dependency closure.

Only application source and serialized asset data are used. The surrounding
character graph remains owned by the native role/attachment modules.
"""
import json
import argparse
import re
import struct
from functools import lru_cache
from pathlib import Path
from Crypto.Hash import MD4

from official_apk import ROOT, Apk
from official_manifest import read_asset_manifest
from official_tables import Constructors
from prepare_battle_performance_camera import documents
from prepare_battle_status_orbit_assets import build, EXPORTED, CODE
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed

BASE = ROOT/'unity/Assets/Official/Combat/PartnerHpBars/Resources/Combat/PartnerHpBars'
DATA = BASE/'bindings.json'
RECEIPT = ROOT/'assets/reference/battle-animation/partner-native-hp-bars-source-manifest.json'
RUNTIME = ROOT/'unity/Assets/Game/Runtime/Official/BattleUnitUI'
BLOCK = re.compile(r'(?ms)^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)')
LOCAL = re.compile(r'\{fileID: (-?\d+)\}')


def record_runtime():
    proof = json.loads(RECEIPT.read_bytes())
    # Asset conversion is not rerun by this operation. Require its recorded
    # payloads to remain identical before refreshing application binding hashes.
    for row in proof['outputs']:
        assert identity(ROOT/row['path'])['sha256'] == row['sha256'], row['path']
    for row in proof['application']:
        target = ROOT/row['migrated']['path']
        row['migrated'] = identity(target)
        row['migrated_meta'] = identity(Path(str(target)+'.meta'))
    proof['runtime_bindings'] = [identity(path) for path in sorted(RUNTIME.glob('*')) if path.is_file()]
    proof['runtime_bindings'] += [identity(ROOT/path) for path in (
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattlePartnerHpView.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattlePartnerHpEffects.cs',
        'unity/Assets/Game/Runtime/Offline/ProfessionUI/PartnerNativeBattleAttachments.cs',
        'unity/Assets/Game/Editor/PartnerBattlePlayables/OfficialPartnerNativeRoleSceneProbe.cs',
        'unity/Assets/Game/Editor/HomeBattleHostPlayModeAcceptance.Assistance.cs',
        'scripts/prepare_partner_native_role_scene_validation.py')]
    proof['shared_scene_integration'] = dict(
        path='unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSceneView.cs',
        entrypoints=['BindPartnerHp', 'Refresh', 'Tick', 'RemoveUnit', 'Dispose'],
        operation='Bind the original HP view when a native partner is created; refresh health before death, tick after native transforms, and release views before the shared effect pools.')
    proof['application_adaptations'][0] = 'HpSliderEffect: restore the original async application method from its compiled state machine, retaining UniTask.NextFrame, immediate first sample, interpolation, frame/delta ordering, cancellation and slider/effect transitions.'
    proof['generator'] = identity(Path(__file__).resolve())
    proof['graph_asset_adapter'] = identity(ROOT/'scripts/prepare_battle_status_orbit_assets.py')
    write_changed(RECEIPT, json.dumps(proof, ensure_ascii=False, indent=2)+'\n')


@lru_cache(maxsize=64)
def extract_hp_subtree(text, path):
    if path.parent.name != 'FightNpc':
        return text
    print('Extract original HP branch:', path.stem, flush=True)
    docs = documents(path)
    transforms = {key: row['Transform'] for key, row in docs.items() if 'Transform' in row}
    roots = [key for key, row in transforms.items()
             if docs[row['m_GameObject']['fileID']]['GameObject']['m_Name'] == 'HpBarGame']
    assert len(roots) == 1, (path, roots)
    selected = set()

    def walk(key):
        row = transforms[key]
        go = row['m_GameObject']['fileID']
        selected.update((key, go))
        selected.update(item['component']['fileID'] for item in docs[go]['GameObject']['m_Component'])
        for child in row['m_Children']:
            walk(child['fileID'])

    walk(roots[0])
    chunks = []
    for match in BLOCK.finditer(text):
        if int(match[2]) not in selected:
            continue
        chunk = match[0]
        if int(match[2]) == roots[0]:
            chunk, count = re.subn(r'(?m)^  m_Father: \{fileID: \d+\}$',
                                  '  m_Father: {fileID: 0}', chunk)
            assert count == 1
        dangling = {int(value) for value in LOCAL.findall(chunk)} - selected - {0}
        assert not dangling, (path, match[2], dangling)
        chunks.append(chunk)
    assert len(chunks) == len(selected)
    return '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n' + ''.join(chunks)


def main():
    for path in (RUNTIME, *RUNTIME.iterdir()):
        if path.suffix != '.meta' and not Path(str(path)+'.meta').exists():
            meta(path, folder=path.is_dir())
    inventory = ROOT/'unity/Assets/Official/Combat/NativePartners/catalog.json'
    catalog = json.loads(inventory.read_bytes())
    assert catalog['manifest'] == '884_156692'
    roots = {str(row['entityId']): f"Prefabs/EcEntity/Fight/FightNpc/{row['entityId']}.prefab"
             for row in catalog['fighters']}
    roots['config'] = 'ScriptableConfig/Modules/Fight/GamePlayManagerConfig.asset'
    scripts = {}
    for name, target in (
        ('HpSliderEffect', RUNTIME/'HpSliderEffect.cs'),
        ('BattleHpSpriteSlider', RUNTIME/'BattleHpSpriteSlider.cs'),
        ('BattleHpBloodEffect', RUNTIME/'BattleHpBloodEffect.cs'),
        ('GamePlayManagerConfig', RUNTIME/'GamePlayManagerConfig.cs'),
        ('SlicedFilledRender', ROOT/'unity/Assets/Game/Runtime/Official/World/Map13EntityRendering/SlicedFilledRender.cs')):
        original = CODE/'client-source'/(name+'.cs')
        source_text = original.read_text(encoding='utf-8-sig')
        ns = re.search(r'(?m)^namespace ([\w.]+)', source_text)
        namespace = ns[1] if ns else ''
        file_id = struct.unpack('<i', MD4.new(('s\0\0\0'+namespace+name).encode()).digest()[:4])[0]
        target_text = target.read_text(encoding='utf-8-sig')
        scripts[file_id] = dict(source=name+'.cs', name=name, namespace=namespace,
            runtime=target.relative_to(ROOT).as_posix(),
            target_namespace=re.search(r'(?m)^namespace ([\w.]+)', target_text)[1])
    build(DEST=BASE, DATA=DATA, RECEIPT=RECEIPT, ROOTS={}, graph_roots=roots,
        family='PartnerHpBars', shader_prefix='PartnerHp_', MAPPED={},
        source_scripts=scripts, native_preprocess=extract_hp_subtree,
        runtime_receipts=(), generator=Path(__file__),
        operation='Extract each entire original HpBarGame subtree with all local objects, native IDs, positions, sizes, sliced fill components and references. Retain the entire original faction config and effect/animation dependency closure. Rebind the detached root to its original ui parent in the restored actor graph. Preserve full Windows shaders and verified original texture mips.',
        limits=['Partner HP only; surrounding original HUD name, faction mark and status branches are owned by their respective modules.'])
    proof = json.loads(RECEIPT.read_bytes())
    records = []
    for partner in catalog['fighters']:
        path = EXPORTED/roots[str(partner['entityId'])]
        docs = documents(path)
        transforms = {key: value['Transform'] for key, value in docs.items() if 'Transform' in value}
        hp = next(value for value in transforms.values()
                  if docs[value['m_GameObject']['fileID']]['GameObject']['m_Name'] == 'HpBarGame')
        ui = transforms[hp['m_Father']['fileID']]
        components = docs[ui['m_GameObject']['fileID']]['GameObject']['m_Component']
        locker = [docs[entry['component']['fileID']]['MonoBehaviour'] for entry in components
                  if 'MonoBehaviour' in docs[entry['component']['fileID']]
                  and docs[entry['component']['fileID']]['MonoBehaviour']['m_Script']['fileID'] == -1546634475]
        assert len(locker) == 1, path
        key = locker[0]['mParentTransform']['fileID'] or hp['m_Father']['fileID']
        names = []
        while key:
            node = transforms[key]
            names.insert(0, str(docs[node['m_GameObject']['fileID']]['GameObject']['m_Name']))
            key = node['m_Father']['fileID']
        records.append(dict(entityId=partner['entityId'], lockParentPath='/'.join(names)))
    apk = Apk.current()
    try:
        manifest = read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))
        entries = [row for row in manifest if row['path'].endswith('/game_settings.g.bytes')]
        assert len(entries) == 1, entries
        obj = apk.asset(entries[0]['bundle'], '/game_settings.g.bytes')
        raw = obj.read().m_Script
        if isinstance(raw, str): raw = raw.encode('utf-8', 'surrogateescape')
        local = CODE.parent/'official-data/game_settings.g.bytes'
        assert bytes(raw) == local.read_bytes()
        settings = Constructors(CODE).table(bytes(raw), 'TbGameSettings')
        battle = settings['Battle']
        enum_source = CODE/'common-source/Common/StageType.cs'
        colors = dict(legionStageType=int(re.search(r'LegionPlunder = (\d+)', enum_source.read_text(encoding='utf-8-sig'))[1]),
            onlyFactionStages=battle['SetHpColorOnlyOnFactionStageType'],
            multipleGroupStages=battle['MultipleGroupIncludeRealPlayerStage'])
        proof['faction_rules'] = dict(source=apk.provenance(obj), payload=identity(local),
            original_fields=colors, enum=identity(enum_source),
            sources=[identity(CODE/'common-source/Common'/name) for name in (
                'FactionColorResolver.cs', 'DefaultFactionColorStrategy.cs', 'DirectFactionColorStrategy.cs',
                'LegionPlunderFactionColorStrategy.cs', 'JustWatchFactionColorStrategy.cs')])
    finally:
        apk.archive.close()
    bindings = BASE/'partner-bindings.json'
    write_changed(bindings, json.dumps(dict(manifest=catalog['manifest'], partners=records, colors=colors),
        ensure_ascii=False, separators=(',', ':'))+'\n')
    meta(bindings)
    proof['outputs'].extend(identity(path) for path in (bindings, Path(str(bindings)+'.meta')))
    proof.update(partner_selection=identity(inventory),
        original_application_sources=[identity(CODE/'client-source'/name) for name in (
            'HpSliderEffect.cs', 'BattleHpSpriteSlider.cs', 'BattleHpAndShieldHandle.cs',
            'GameEffectData.cs', 'GamePlayManagerConfig.cs', 'GameFactionEffectAssignerHandler.cs', 'TransformLocker.cs')],
        application_adaptations=[
            'HpSliderEffect: restore the original async application method from its compiled state machine, retaining UniTask.NextFrame, immediate first sample, interpolation, frame/delta ordering, cancellation and slider/effect transitions.',
            'BattleHpSpriteSlider: preserve initialization, subscriptions, faction colors, borrowed effects and update ordering through an explicit local health/effect/spectator port.',
            'GamePlayManagerConfig: preserve every serialized field; StageType list uses the same serialized Int32 representation.',
            'BattleHpBloodEffect: preserve the two empty original pool callbacks and original shared object pool interface.'])
    proof['runtime_bindings'] = [identity(path) for path in sorted(RUNTIME.glob('*')) if path.is_file()]
    write_changed(RECEIPT, json.dumps(proof, ensure_ascii=False, indent=2)+'\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--record-runtime', action='store_true')
    if parser.parse_args().record_runtime: record_runtime()
    else:
        main()
        record_runtime()
