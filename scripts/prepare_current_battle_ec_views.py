"""Port the selected current official skill/damage EC presentation classes."""
import hashlib
import json
from pathlib import Path
import re

from official_source import OfficialSource, ROOT
from prepare_battle_scene_appearance_runtime import meta, write_changed


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def replace(text, before, after):
    assert text.count(before) == 1, ('Current client boundary changed', before)
    return text.replace(before, after)


def build():
    source = OfficialSource()
    code = source.prepared_path / 'official-code'
    proof = json.loads((code.parent / 'code-manifest.json').read_bytes())
    assembly = code / 'managed/decoded/Assembly-CSharp.dll'
    assert identity(assembly)['sha256'] == proof['assemblies'][assembly.name]['decoded_sha256']
    folder = ROOT / 'unity/Assets/Game/Runtime/Official/BattleSkillPlayables'
    records = []
    for name in ('ECFightSkillView', 'ECFightDamageView', 'ECFightHitView'):
        original = code / 'client-source' / (name + '.cs')
        text = original.read_text(encoding='utf-8-sig')
        text = re.sub(r'(?m)^using (?:Common|Framework.DependencyInjection|GameFramework);\n', '', text)
        text = replace(text, '[Inject]\n\tprivate IMyPlayableManager MyPlayableManager { get; set; }',
                       'public IMyPlayableManager MyPlayableManager { get; set; }\n'
                       '\tpublic IFightSkillViewServices ViewServices { get; set; }')
        text = text.replace('ECPlayableOverrideView', 'BattlePlayableOverrideView')
        text = text.replace('mSkillRoot.myContainer.GetEntity(', 'ViewServices.GetEntity(')
        text = text.replace('myContainer.GetEntity(', 'ViewServices.GetEntity(')
        if name != 'ECFightHitView':
            root_field = 'mSkillRoot' if name == 'ECFightSkillView' else 'mDamageEntity'
            text = replace(text, root_field + '.GetECView<BattlePlayableOverrideView>()',
                           'CurrentFightViewLookup.GetOverride(' + root_field + ')')
        if name == 'ECFightSkillView':
            text = replace(text, 'mInterruptSubscription = mSkillComponent.OnInterruped.Subscribe(OnSkillInterrupted);',
                           'mSkillComponent.OnInterruped += OnSkillInterrupted;\n'
                           '\t\t\t\tmInterruptSubscription = new CurrentFightViewSubscription(() => mSkillComponent.OnInterruped -= OnSkillInterrupted);')
            text = replace(text, 'GridTransformComponent component = GetComponent<GridTransformComponent>();',
                           'ECGridTransformData component = GetComponent<ECGridTransformData>();')
            text = replace(text, 'GridGroundComponent component = mSkillRoot.myContainer.MyRootEntity.Entity.GetComponent<GridGroundComponent>();',
                           'IFightSkillViewServices component = ViewServices;')
        elif name == 'ECFightDamageView':
            text = replace(text, 'public EcEntity Entity => mEntity;', 'public ECViewEntityData Entity => mEntity;')
            text = replace(text, '((Component)entity).GetComponent<MyFightRoleFight>().FireDamageEvent(this);',
                           'var carrier = mDamageEntity.GetComponent<MyFightDamage>();\n'
                           '\t\tcarrier.myShowZeroDamage = mShowZeroDamage; carrier.myHideDamage = mHideDamage;\n'
                           '\t\tcarrier.myCommonPlayable = mCommonPlayable; carrier.myDamagePlayable = mDamagePlayable;\n'
                           '\t\tcarrier.myBlockPlayable = mBlockPlayable; carrier.myCritPlayable = mCritPlayable;\n'
                           '\t\tcarrier.myTriggerStatusPlayable = mTriggerStatusPlayable;\n'
                           '\t\t((Component)entity).GetComponent<MyFightRoleFight>().FireDamageEvent(carrier);')
            text = replace(text, 'FightSkillComponent skill = mDamageComponent.Skill;',
                           'MyEntity skillEntity = ViewServices.GetEntity(mDamageComponent.SkillId);\n'
                           '\t\tFightSkillComponent skill = skillEntity ? skillEntity.GetComponent<MyFightSkill>()?.Component : null;')
            text = replace(text, '((Component)entity2).GetComponent<MyFightRoleFight>().FireHitEvent(this);',
                           '((Component)entity2).GetComponent<MyFightRoleFight>().FireHitEvent(carrier);')
        else:
            text = text.replace('IFightSkillViewServices ViewServices', 'IFightHitViewServices ViewServices')
            text = text.replace('IFightHitDelayInfoProvider', 'IFightHitViewBinding')
            text = text.replace('IBattleFormulaHandler.SkillHitBaseDelayInfoProvider', 'FightHitViewFormula.SkillHitBaseDelayInfoProvider')
            text = text.replace('BattleFormula.Instance.', 'FightHitViewFormula.')
            text = text.replace('GridGroundComponent gridGround = mGridTransform.GridGround;',
                                'IFightSkillViewServices gridGround = ViewServices;')
            text = replace(text, 'mHitComponent.GridTransform.GridGround.GridToPos(grid)', 'ViewServices.GridToPos(grid)')
            text = replace(text, 'skillEntity.GetECView<ECFightSkillView>()', 'CurrentFightViewLookup.Get<ECFightSkillView>(skillEntity)')
            text = replace(text, 'mDelayInfoProvider?.GetDelayInfoProvider()', 'mDelayInfoProvider?.GetCurrentDelayInfoProvider()')
            text = replace(text, '\t\t\t\tmDelayCoroutine = StartCoroutine(DelayPlay());',
                           '\t\t\t\tmDelayInfoProvider?.GetCurrentDelayInfoProvider();\n\t\t\t\tmDelayCoroutine = StartCoroutine(DelayPlay());')
            text = replace(text, '\t\t\tmDelayCoroutine = 0L;\n\t\t\tDoAfterDelay();',
                           '\t\t\tmDelayCoroutine = 0L;\n'
                           '\t\t\tif (ViewServices is IFightHitViewArrivalDeferrer deferrer\n'
                           '\t\t\t\t&& deferrer.DeferHitView(mHitComponent.SkillId, mHitComponent.Entity.Id, DoAfterDelay)) yield break;\n'
                           '\t\t\tDoAfterDelay();')
            text = replace(text, '\t\tif ((Object)(object)mScopePlayable != (Object)null)\n\t\t{\n\t\t\tmScopeCoroutine',
                           '\t\t(ViewServices as IFightHitViewArrivalObserver)?.HitViewArrived(\n'
                           '\t\t\tmHitComponent.SkillId, mHitComponent.Entity.Id, mHitComponent.GridTransform.Pos, -1);\n'
                           '\t\tif ((Object)(object)mScopePlayable != (Object)null)\n\t\t{\n\t\t\tmScopeCoroutine')
            text = replace(text, '\t\tfloat num = 0f;', '\t\tfloat num = 0f;\n\t\tint scopeIndex = 0;')
            text = replace(text, '\t\t\tMyPlayableManager.Play(mScopePlayable, val, val);',
                           '\t\t\t(ViewServices as IFightHitScopeOriginObserver)?.HitScopeCreated(mHitComponent.Entity.Id, val);\n'
                           '\t\t\tMyPlayableManager.Play(mScopePlayable, val, val);\n'
                           '\t\t\t(ViewServices as IFightHitViewArrivalObserver)?.HitViewArrived(\n'
                           '\t\t\t\tmHitComponent.SkillId, mHitComponent.Entity.Id, grid, scopeIndex++);')
        start = text.index('public class ' + name)
        text = (text[:start] + 'namespace Zjcs.Official.BattleSkillPlayables\n{\n[System.Serializable]\n'
                + text[start:] + '\n}\n')
        imports = ('// Current official EC view; explicit entity/DI boundaries are recorded in its source receipt.\n'
                   'using Object = UnityEngine.Object;\nusing Zjcs.Official.EntityViews;\n'
                   'using Zjcs.Official.EntityLifecycle;\nusing Zjcs.Official.EntityTransformViews;\n'
                   'using Zjcs.Official.StoryPlayables;\nusing Zjcs.Official.StoryPlayableManager;\n'
                   'using Zjcs.Official.World.NativeTerrain.Common;\n')
        target = folder / (name + '.cs')
        write_changed(target, re.sub(r'(?m)[ \t]+$', '', imports + text))
        meta(target)
        records.append(dict(original=identity(original), runtime=identity(target), metadata=identity(Path(str(target) + '.meta'))))
    receipt = ROOT / 'assets/reference/battle-animation/current-battle-ec-views-source-manifest.json'
    result = dict(manifest=source.version, assembly=identity(assembly), sources=records,
                  boundaries=[identity(folder / (name + '.cs')) for name in
                              ('CurrentFightViewLookup', 'FightHitViewBindings', 'MyFightSkill', 'MyFightHitBase', 'MyFightDamage')]
                             + [identity(ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat' / (name + '.cs')) for name in
                                ('OfficialBattleCurrentViewBinding', 'OfficialBattleSkillEntityPlayback',
                                 'OfficialBattleHitEntityPlayback', 'OfficialBattleDamageEntityPlayback')],
                  generator=identity(Path(__file__)),
                  adaptation='Retain the complete current official EC view branches and order. Inject the existing entity/grid/playable ports; '
                             'adapt interruption subscription symmetrically and forward damage notifications through the existing typed event carrier. '
                             'Only explicitly restored current serialized EC views select this path; unknown managed view types remain rejected.')
    write_changed(receipt, json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    print('Current official skill, hit and damage EC views migrated.')


if __name__ == '__main__':
    build()
