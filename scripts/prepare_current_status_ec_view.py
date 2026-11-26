"""Port the complete current official character-status EC view through existing state ports."""
import json
from pathlib import Path
import re

from official_source import OfficialSource, ROOT
from prepare_current_battle_ec_views import identity, replace
from prepare_battle_scene_appearance_runtime import meta, write_changed


def build():
    source = OfficialSource()
    code = source.prepared_path / 'official-code'
    proof = json.loads((code.parent / 'code-manifest.json').read_bytes())
    assembly = code / 'managed/decoded/Assembly-CSharp.dll'
    assert identity(assembly)['sha256'] == proof['assemblies'][assembly.name]['decoded_sha256']
    original = code / 'client-source/ECFightStatusPlayableView.cs'
    text = original.read_text('utf-8-sig')
    text = re.sub(r'(?m)^using (?:Common|Framework.DependencyInjection|GameFramework);\n', '', text)
    text = replace(text, 'ECViewBase, IFightGetUsePlayable', 'ECViewBase, IFightGetUsePlayable, ICurrentFightStatusPlayableView')
    text = replace(text, '[Inject]\n\tprivate IMyPlayableManager MyPlayableManager { get; set; }',
                   'public IMyPlayableManager MyPlayableManager { get; set; }')
    text = text.replace('FightStatusComponent', 'IFightStatusPlayableState')
    text = replace(text, 'private MyFightStatusShareStack mShareStack;', 'private int? mShareStack;')
    text = text.replace('(Object)(object)mShareStack == (Object)null', '!mShareStack.HasValue')
    text = text.replace('(Object)(object)mShareStack != (Object)null', 'mShareStack.HasValue')
    text = text.replace('mShareStack.Info.ShareId', 'mShareStack.Value')
    text = text.replace('mStatusComponent.ActionComp == null', 'mStatusShell?.Context == null')
    text = replace(text, 'return mStatusRoot.myContainer.GetEntity(mStatusComponent.ActionComp.ApplyId);',
                   'return mStatusShell.Context.Owner ? mStatusShell.Context.Owner.GetComponent<MyEntity>() : null;')
    begin = text.index('\t\tFightRoleCharacterComponent component =')
    end = text.index('\n\t}\n\n\tpublic void StopMasterCastPlayable()', begin)
    text = text[:begin] + '''\t\tGameObject master = mStatusShell.Context.Master;
\t\tif (master)
\t\t{
\t\t\tmMasterCastPlayableHandle = MyPlayableManager.Play(mMasterCastPlayable, master, ((Component)applyEntity).gameObject);
\t\t}''' + text[end:]
    text = replace(text, 'mOnStatusForbiddenChangedDisposable = mStatusComponent.OnForbiddenChanged.Subscribe(OnForbiddenChanged);',
                   'mStatusComponent.OnForbiddenChanged += OnForbiddenChanged;\n'
                   '\t\t\tmOnStatusForbiddenChangedDisposable = new CurrentStatusViewSubscription(() => mStatusComponent.OnForbiddenChanged -= OnForbiddenChanged);')
    text = replace(text, 'MyFightStatusShareStack component = ((Component)myStatus).GetComponent<MyFightStatusShareStack>();',
                   'int? component = myStatus.Context.ShareId;')
    text = replace(text, '!((Object)(object)component == (Object)null) && component.Info.ShareId == shareId',
                   'component.HasValue && component.Value == shareId')
    text = replace(text, 'mStatusAgent.Component.StatusToShareStack', 'mStatusAgent.StatusToShareStack')
    text = replace(text, 'myFightStatus.myEntity.GetECView<ECFightStatusPlayableView>()', 'CurrentStatusViewLookup.Get(myFightStatus)')
    text = replace(text, 'oldDisplayStatus.myEntity.GetECView<ECFightStatusPlayableView>()', 'CurrentStatusViewLookup.Get(oldDisplayStatus)')
    text = replace(text, 'mShareStack = ((Component)mStatusRoot).GetComponent<MyFightStatusShareStack>();',
                   'mShareStack = mStatusShell.Context.ShareId;')
    text = replace(text, 'mStatusComponent.ActionComp != null', 'mStatusShell?.Context != null')
    text = replace(text, 'MyEntity entity = mStatusRoot.myContainer.GetEntity(mStatusComponent.ActionComp.ApplyId);',
                   'MyEntity entity = mStatusShell.Context.Owner ? mStatusShell.Context.Owner.GetComponent<MyEntity>() : null;')
    text = replace(text, 'mStatusComponent != null && mStatusComponent.Info != null', 'mStatusComponent != null')
    text = replace(text, 'return mStatusComponent.Info.CanStackCount();', 'return mStatusComponent.CanStackCount;')
    start = text.index('public class ECFightStatusPlayableView')
    text = text[:start] + 'namespace Zjcs.Official.BattleStatusPlayables\n{\n[System.Serializable]\n' + text[start:] + '\n}\n'
    imports = ('// Complete current official status EC view; see its source receipt.\n'
               'using Object = UnityEngine.Object;\nusing Zjcs.Official.EntityViews;\n'
               'using Zjcs.Official.EntityLifecycle;\nusing Zjcs.Official.BattleSkillPlayables;\n'
               'using Zjcs.Official.StoryPlayables;\nusing Zjcs.Official.StoryPlayableManager;\n')
    folder = ROOT / 'unity/Assets/Game/Runtime/Official/BattleStatusPlayables'
    target = folder / 'ECFightStatusPlayableView.cs'
    write_changed(target, re.sub(r'(?m)[ \t]+$', '', imports + text)); meta(target)
    boundaries = [folder / (name + '.cs') for name in ('CurrentStatusViewLookup', 'IFightStatusPlayableContext', 'MyFightStatus')]
    boundaries += [folder / 'Zjcs.Official.BattleStatusPlayables.asmdef',
        ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleStatusPlayback.cs']
    receipt = ROOT / 'assets/reference/battle-animation/current-status-ec-view-source-manifest.json'
    write_changed(receipt, json.dumps(dict(manifest=source.version, assembly=identity(assembly),
        original=identity(original), runtime=identity(target), metadata=identity(Path(str(target) + '.meta')),
        boundaries=[identity(path) for path in boundaries], generator=identity(Path(__file__)),
        adaptation='Preserve all current playable, stack, shared-display, forbidden, lifecycle and stop branches. '
                   'Forward the existing actual status state events symmetrically; use its owner, master, share ID and shared counts. '
                   'Current serialized status views own visuals, while the original shell retains apply/end agent notifications.'),
        ensure_ascii=False, indent=2) + '\n')
    print('Current official character-status EC view migrated.')


if __name__ == '__main__':
    build()
