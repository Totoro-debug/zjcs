"""Restore the complete official 24222 status prefab and its native dependency graph."""
import json
import re
from pathlib import Path

from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from prepare_battle_cast_assets import EVENT_ASSET
from prepare_battle_ground_effect_assets import sha
from prepare_battle_grid_entity_assets import script_id
from prepare_battle_movement_effects import SCRIPTS
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed
from prepare_battle_status_orbit_assets import build

CODE = ROOT / '.tools/source-packages/current/884_156692/official-code'
RECEIPT = ROOT / 'assets/reference/battle-animation/dark-status-assets-source-manifest.json'
RUNTIME_RECEIPT = ROOT / 'assets/reference/battle-animation/status-round-start-view-source-manifest.json'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/BattleGridItemEntities'


def prepare_runtime():
    manifest = json.loads((CODE.parent / 'code-manifest.json').read_bytes())
    assembly = identity(CODE / 'managed/decoded/Assembly-CSharp.dll')
    assert manifest['manifest'] == '884_156692'
    assert assembly['sha256'] == manifest['assemblies']['Assembly-CSharp.dll']['decoded_sha256']
    original = CODE / 'client-source/MyFightStatusRoundStart.cs'
    text = original.read_text(encoding='utf-8-sig')
    text = text.replace('using Common;\nusing R3;\n', '')
    text = text.replace('MyFightStatusBase<FightStatusRoundStartComponent, FightStatusRoundStartComponentInfo>',
                        'MyFightStatusBase<BattleStatusRoundStartViewData>')
    text = text.replace('private DisposableBag mEventDisposableBag;',
                        'private Action mUnsubscribe;')
    text = re.sub(r'\s*//IL_[^\n]*', '', text)
    text = text.replace('mEventDisposableBag = default;\n\t\tDisposable.AddTo<IDisposable>(Component.OnRemainCountUpdate.Subscribe(OnRemainCountUpdate), ref mEventDisposableBag);',
                        'Component.OnRemainCountUpdate += OnRemainCountUpdate;\n'
                        '\t\tmUnsubscribe = () => Component.OnRemainCountUpdate -= OnRemainCountUpdate;')
    text = text.replace('mEventDisposableBag.Dispose();', 'mUnsubscribe?.Invoke();\n\t\tmUnsubscribe = null;')
    text = text.replace('MySerializable userData', 'object userData')
    text = text.replace('FightStatusRoundStartComponent component', 'BattleStatusRoundStartViewData component')
    assert not any(value in text for value in ('DisposableBag', 'R3;', 'Common;', 'FightStatusRoundStartComponent'))
    text = ('// Original client source; see status round-start view source receipt.\n'
            + text.replace('public class ', 'namespace Zjcs.Official.BattleGridItemEntities\n{\npublic class ', 1)
            + '\n}\n')
    path = DEST / 'MyFightStatusRoundStart.cs'
    write_changed(path, text); meta(path)
    boundary = DEST / 'BattleStatusRoundStartViewData.cs'
    write_changed(boundary, '''using System;

namespace Zjcs.Official.BattleGridItemEntities
{
    // The actual local component supplies its authored counter and transition event.
    // This client view never consumes a trigger or derives lifetime from rendered frames.
    public abstract class BattleStatusRoundStartViewData
    {
        public event Action<BattleStatusRoundStartViewData> OnRemainCountUpdate;
        public abstract int GetRemainCount();
        public void NotifyRemainCountUpdate() => OnRemainCountUpdate?.Invoke(this);
    }
}
''')
    meta(boundary)
    inputs = [original, CODE / 'client-source/MyFightStatusBase.cs',
              CODE / 'common-source/Common/FightStatusRoundStartComponent.cs']
    outputs = [path, Path(str(path) + '.meta'), boundary, Path(str(boundary) + '.meta')]
    write_changed(RUNTIME_RECEIPT, json.dumps(dict(manifest='884_156692', original_assembly=assembly,
        original_code=[identity(value) for value in inputs], outputs=[identity(value) for value in outputs],
        generator=identity(Path(__file__)),
        operation='Preserve original OnStart/base, subscribe, initial count update, custom-icon flag, '
            'count assignment and unsubscribe/base OnEnd order. Adapt R3 subscription disposal to '
            'the existing C# event view boundary; the host supplies original GetRemainCount values.'),
        ensure_ascii=False, indent=2) + '\n')


def main():
    prepare_runtime()
    schemas = ScriptSchemas(code_root=CODE)
    objects, assemblies, source_code = [], {}, {}
    manifest = json.loads((CODE.parent / 'code-manifest.json').read_bytes())
    with Apk.current() as apk:
        assert apk.version == '884_156692'
        bundle, = [name for name in apk.index if name.endswith('fightskill_24222.bundle')]
        for obj in apk.objects(bundle):
            if obj.type.name not in ('GameObject', 'Transform', 'MonoBehaviour'): continue
            fields = schemas.read(obj) if obj.type.name == 'MonoBehaviour' else obj.read_typetree()
            row = dict(type=obj.type.name, source=apk.provenance(obj), fields=fields,
                       object_sha256=sha(obj.get_raw_data()))
            if obj.type.name == 'MonoBehaviour':
                script = obj.read(check_read=False).m_Script.deref().read()
                row['script'] = dict(name=script.m_Name, namespace=script.m_Namespace,
                                     assembly=script.m_AssemblyName)
                module = script.m_AssemblyName.removesuffix('.dll') + '.dll'
                assembly = identity(CODE / 'managed/decoded' / module)
                assert assembly['sha256'] == manifest['assemblies'][module]['decoded_sha256']
                assemblies[module] = assembly
                candidates = [CODE / 'client-source' / (script.m_Name + '.cs'),
                              CODE / 'game-framework-source/GameFramework' / (script.m_Name + '.cs')]
                source, = [path for path in candidates if path.is_file()]
                source_code[script.m_Name] = identity(source)
            objects.append(row)
    original, = [row for row in objects if row['type'] == 'GameObject' and row['fields']['m_Name'] == '24222']
    status, = [row for row in objects if row.get('script', {}).get('name') == 'MyFightStatus']
    assert status['fields']['IsIconShowCustomCount'] == 1
    assert status['fields']['ShowSpriteAbovePlayer'] == 1
    follow, = [row for row in objects if row.get('script', {}).get('name') == 'MyPlayableFollowBone']
    assert follow['fields']['myAttach']['myBoneName'] == 'body'
    assert follow['fields']['myDuration'] == -1 and follow['fields']['myFollowDuration'] == -1
    bindings = dict(SCRIPTS)
    for name, folder in [('MyEntity', 'EntityLifecycle'), ('MyAction', 'EntityActions'),
                         ('MyFightStatus', 'BattleStatusPlayables'),
                         ('MyFightStatusRoundStart', 'BattleGridItemEntities'),
                         ('MyFightGameplayTag', 'BattleGridItemEntities'),
                         ('MyPlayableSound', 'StorySounds')]:
        bindings[script_id(name)] = (folder, name)
    build(DEST=ROOT / 'unity/Assets/Official/Combat/DarkStatus/Resources/Combat/DarkStatus',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/dark-status.json', RECEIPT=RECEIPT,
        ROOTS={}, graph_roots={24222: 'Prefabs/EcEntity/Fight/FightSkill/24222/24222.prefab'},
        graph_originals={24222: original}, MAPPED=bindings, family='DarkStatus', shader_prefix='DarkStatus_',
        reference_scripts={-210918009: dict(name='WwiseEventReference',
            native_asset=EVENT_ASSET,
            runtime='unity/Assets/Game/Runtime/Official/ProfessionUI/WwiseEventReference.cs',
            receipt='assets/reference/profession-ui/audio-source-manifest.json')},
        runtime_receipts=['status-view-runtime-source-manifest.json', 'status-round-start-view-source-manifest.json'],
        generator=Path(__file__), limits=[
            'Android prefab objects are restored as explicit Windows-native assets; '
            'the original RoundStart view consumes a typed local counter/event bridge.'])
    result = json.loads(RECEIPT.read_bytes())
    result['original_visuals'] = objects
    result['original_assemblies'] = assemblies
    result['original_code'] = source_code
    result['status_rules_source'] = identity(ROOT / 'assets/reference/battle-rules/dark-hit-source-manifest.json')
    write_changed(RECEIPT, json.dumps(result, ensure_ascii=False, indent=2) + '\n')


if __name__ == '__main__':
    main()
