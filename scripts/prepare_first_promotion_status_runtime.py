"""Port original first-promotion status client components, retaining authored callbacks."""
import json
import re
from pathlib import Path

from official_apk import ROOT
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed

CODE = ROOT / '.tools/source-packages/current/884_156692/official-code'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/BattleGridItemEntities'
RECEIPT = ROOT / 'assets/reference/battle-animation/first-promotion-status-runtime-source-manifest.json'
EMPTY = '''MyFightStatusActionEnd MyFightStatusActionExist MyFightStatusActionInvisible
MyFightStatusBackToLife MyFightStatusCureReduce MyFightStatusDamageFalloff
MyFightStatusDamageProcess MyFightStatusDamageRemove MyFightStatusDamageSkill
MyFightStatusDie MyFightStatusDoDamageHandle MyFightStatusHitApplyDamage
MyFightStatusHitCustomCure MyFightStatusHitDmgAddPer MyFightStatusHitScope
MyFightStatusHitSkill MyFightStatusHitSummon MyFightStatusHpDecreaseUnit
MyFightStatusHpIncreaseUnit MyFightStatusHpLessThan MyFightStatusHpLimit
MyFightStatusKillSkill MyFightStatusLogic MyFightStatusMoveNear MyFightStatusMoveScope
MyFightStatusProp MyFightStatusPropBySkill MyFightStatusReduceStatusLife
MyFightStatusRoleDie MyFightStatusRoundInterval MyFightStatusSkillEnd
MyFightStatusSkillStopCd MyFightStatusStackChange MyFightStatusStart'''.split()


def wrap(text):
    text = re.sub(r'(?m)^using (Common|Framework.DependencyInjection|GameFramework);\n', '', text)
    text = text.replace('MySerializable userData', 'object userData')
    text = text.replace('((Component)ApplyEntity).gameObject', 'ApplyEntity')
    text = text.replace('protected MyEntity ApplyEntity => myContainer.GetEntity(Component.Status.ApplyEntity.Id);',
                        'protected GameObject ApplyEntity => Component.ApplyObject;')
    text = text.replace('[Inject]\n\tprivate IMyPlayableManager', 'public IMyPlayableManager')
    return ('// Original client component; see first-promotion status runtime source receipt.\n'
            'using Zjcs.Official.EntityLifecycle;\n'
            'using Zjcs.Official.EntityViews;\n'
            'using Zjcs.Official.StoryPlayables;\n'
            'using Zjcs.Official.StoryPlayableManager;\n'
            'using Zjcs.Official.BattleSkillPlayables;\n'
            'using Zjcs.Official.BattleStatusPlayables;\n'
            'namespace Zjcs.Official.BattleGridItemEntities\n{\n'
            'using Object = UnityEngine.Object;\n' + text.strip() + '\n}\n')


def main():
    proof = json.loads((CODE.parent / 'code-manifest.json').read_bytes())
    assembly = identity(CODE / 'managed/decoded/Assembly-CSharp.dll')
    assert proof['manifest'] == '884_156692'
    assert assembly['sha256'] == proof['assemblies']['Assembly-CSharp.dll']['decoded_sha256']
    inputs, outputs, reused = [], [], []

    def install(name, text):
        path = DEST / (name + '.cs')
        write_changed(path, text)
        meta(path)
        outputs.extend((identity(path), identity(Path(str(path) + '.meta'))))

    for name in EMPTY:
        source = CODE / 'client-source' / (name + '.cs')
        text = source.read_text(encoding='utf-8-sig')
        match = re.fullmatch(r'using Common;\s*public class ' + name
                            + r' : (MyFightStatusBase|MyComponent)<\w+(?:, \w+)?>\s*\{\s*\}\s*', text)
        assert match, ('Original client wrapper is not empty', name)
        inputs.append(identity(source))
        target = DEST / (name + '.cs')
        if target.is_file() and name in ('MyFightStatusDamageRemove', 'MyFightStatusDamageSkill', 'MyFightStatusMoveNear'):
            # Existing airborne/move-near ports retain their own source receipts.
            reused.append(identity(target))
            continue
        text = text.replace('using Common;\n', '')
        text = re.sub(r'(MyFightStatusBase|MyComponent)<\w+(?:, \w+)?>',
                      lambda m: m[1] + '<IBattleGridComponentViewData>', text)
        install(name, wrap(text))

    name = 'MyFightStatusShieldBase'
    source = CODE / 'client-source' / (name + '.cs')
    text = source.read_text(encoding='utf-8-sig')
    text = text.replace('where T : FightStatusShieldBaseComponent where TInfo : FightStatusShieldBaseComponentInfo',
                        'where T : BattleStatusShieldViewData')
    install(name, wrap(text))
    inputs.append(identity(source))

    name = 'MyFightStatusShieldPersistent'
    source = CODE / 'client-source' / (name + '.cs')
    text = source.read_text(encoding='utf-8-sig')
    text = text.replace('MyFightStatusShieldBase<FightStatusShieldPersistentComponent, FightStatusShieldPersistentComponentInfo>',
                        'MyFightStatusShieldBase<BattleStatusShieldViewData, object>')
    install(name, wrap(text))
    inputs.append(identity(source))

    name = 'MyFightStatusDamageReduceAbsolute'
    source = CODE / 'client-source' / (name + '.cs')
    text = source.read_text(encoding='utf-8-sig')
    text = text.replace('MyFightStatusBase<FightStatusDamageReduceAbsoluteComponent, FightStatusDamageReduceAbsoluteComponentInfo>',
                        'MyFightStatusBase<BattleStatusDamageReduceAbsoluteViewData>')
    text = text.replace('Info.Count', 'Component.MaximumCount')
    install(name, wrap(text))
    inputs.append(identity(source))

    install('BattleStatusShieldViewData', '''using System;
using UnityEngine;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
namespace Zjcs.Official.BattleGridItemEntities
{
    // Captured local settlement values; views never derive gameplay from frames.
    public abstract class BattleStatusShieldViewData
    {
        public abstract GameObject ApplyObject { get; }
        public event Action<EcEntityIdentifier, long, long> OnUpdateRemainData;
        public void NotifyRemainData(EcEntityIdentifier id, long maximum, long remaining)
            => OnUpdateRemainData?.Invoke(id, maximum, remaining);
    }
    public abstract class BattleStatusDamageReduceAbsoluteViewData
    {
        public abstract GameObject ApplyObject { get; }
        public abstract int MaximumCount { get; }
        public event Action<int> OnUpdateTriggerCount;
        public void NotifyTriggerCount(int count) => OnUpdateTriggerCount?.Invoke(count);
    }
}
''')
    for row in outputs:
        if row['path'].endswith('.cs'):
            text = (ROOT / row['path']).read_text(encoding='utf-8-sig')
            assert not any(value in text for value in ('using Common;', '[Inject]', 'MySerializable', 'Component.Status.ApplyEntity'))
    write_changed(RECEIPT, json.dumps(dict(manifest='884_156692', original_assembly=assembly,
        original_code=inputs, outputs=outputs, existing_runtime=reused, generator=identity(Path(__file__)),
        operation='Retain literal empty official client wrappers and complete persistent shield / absolute '
        'reduction view callbacks, subscription order, playable selection, stop and cleanup. Adapt server '
        'component generics to typed local presentation data; bind original ApplyEntity to its existing '
        'character GameObject and inject the scene-owned original playable manager. Original Count comes '
        'from captured component metadata. Gameplay settlement and event capture remain in the local executor.'),
        ensure_ascii=False, indent=2) + '\n')


if __name__ == '__main__':
    main()
