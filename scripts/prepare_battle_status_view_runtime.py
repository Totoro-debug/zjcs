"""Port the official status-view lifecycle across explicit local entity boundaries."""
import json
import re
import struct
from pathlib import Path
from hashlib import sha256
from Crypto.Hash import MD4

from prepare_battle_status_playable_runtime import CODE, OUTPUT, NAMESPACE, identity, write
from prepare_unity_story_sound_runtime import runtime_meta
from prepare_unity_story_playable_runtime import logging_adapter
from official_apk import ROOT

RECEIPT = ROOT / 'assets/reference/battle-animation/status-view-runtime-source-manifest.json'


def replace_method(text, name, replacement):
    match = re.search(r'(?m)^\t(?:public|private|protected)[^\n;=]*\b' + name
                      + r'\([^\n]*\)\n\t\{', text)
    assert match, name
    start = text.index('{', match.start())
    depth, end = 1, start + 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[:match.start()] + replacement.rstrip() + text[end:]


def source(name):
    if name == 'GAction3':
        original = (CODE / 'framework-firstpass-source/Framework/GAction.cs').read_text(encoding='utf-8-sig')
        start = original.index('public sealed class GAction<T1, T2, T3>')
        end, depth = original.index('{', start) + 1, 1
        while depth:
            depth += (original[end] == '{') - (original[end] == '}')
            end += 1
        return ('using System;\nusing System.Runtime.CompilerServices;\nusing System.Threading;\n'
                + original[start:end] + '\n')
    return (CODE / 'client-source' / (name + '.cs')).read_text(encoding='utf-8-sig')


def wrap(text):
    text = re.sub(r'(?m)^using (?:Common|GameFramework|Framework(?:\.[\w.]+)*);\n', '', text)
    start = re.search(r'(?m)^public (?:(?:abstract|sealed) )?(?:class|interface)', text).start()
    return ('// Migrated official status application source; see status view runtime receipt.\n'
            'using Object = UnityEngine.Object;\n'
            'using Zjcs.Official.StoryPlayables;\n'
            'using Zjcs.Official.StoryPlayableManager;\n' + text[:start]
            + 'namespace ' + NAMESPACE + '\n{\n' + text[start:] + '\n}\n')


def status_view():
    text = source('MyFightStatus')
    text = text.replace('MyComponent<FightStatusComponent, FightStatusComponentInfo>, IFightGetUsePlayable',
                        'MonoBehaviour')
    text = text.replace('\tprivate FightStatusInfo mStatusInfo;\n\n', '')
    text = text.replace('\tprivate MyFightStatusShareStack mFightStatusShareStack;',
                        '\tprivate int? mShareId;')
    text = text.replace('\tprivate IDisposable mOnStatusForbiddenChangedDisposable;\n\n', '')
    text = text.replace('\tpublic FightStatusInfo StatusInfo => mStatusInfo;', '''
	public IFightStatusPlayableContext Context { get; private set; }

	public IFightStatusPlayableState Component => Context.State;'''.strip('\n'))
    text = text.replace('public MyEntity ApplyEntity => myContainer.GetEntity(Component.ActionComp.ApplyId);',
                        'public GameObject ApplyEntity => Context.Owner;')
    text = text.replace('[Inject]\n\tprivate IMyPlayableManager mMyPlayableManager { get; set; }', '''
private IMyPlayableManager mMyPlayableManager;

	public void Bind(IFightStatusPlayableContext context, IMyPlayableManager manager)
	{
		Context = context ?? throw new ArgumentNullException(nameof(context));
		mMyPlayableManager = manager ?? throw new ArgumentNullException(nameof(manager));
	}'''.strip('\n'))
    text = replace_method(text, 'OnStart', '''	public void OnStart(bool newEntity)
	{
		if (ApplyEntity != null)
		{
			mStatusAgent = Context.Agent;
			mShareId = Context.ShareId;
			Component.OnStatusStackUpdate += OnStatusStackUpdate;
			Component.OnInvoked += OnStatusInvoked;
			Component.OnForbiddenChanged += OnForbiddenChanged;
			OnForbiddenChanged(Component, Component.Forbidden);
			NotifyStartEvent();
		}
	}''')
    text = replace_method(text, 'OnEnd', '''	public void OnEnd()
	{
		StopAllPlayable();
		if (Component != null)
		{
			Component.OnStatusStackUpdate -= OnStatusStackUpdate;
			Component.OnInvoked -= OnStatusInvoked;
			Component.OnForbiddenChanged -= OnForbiddenChanged;
		}
		NotifyEndEvent();
		mStatusAgent = null;
		mShowView = false;
	}''')
    text = text.replace('((Component)ApplyEntity).GetComponent<MyFightStatusAgent>()', 'Context.Agent')
    text = text.replace('MyEntity applyEntity = ApplyEntity;', 'GameObject applyEntity = ApplyEntity;')
    text = text.replace('((Component)applyEntity).GetComponent<MyFightStatusAgent>()', 'Context.Agent')
    text = text.replace('FightStatusComponent status', 'IFightStatusPlayableState status')
    text = text.replace('Component.Info.CanStackCount()', 'Component.CanStackCount')
    text = text.replace('(Object)(object)mFightStatusShareStack == (Object)null', '!mShareId.HasValue')
    text = text.replace('mFightStatusShareStack.Info.ShareId', 'mShareId.Value')
    text = text.replace('MyFightStatusShareStack component = ((Component)myStatus).GetComponent<MyFightStatusShareStack>();',
                        'int? component = myStatus.Context.ShareId;')
    text = text.replace('(Object)(object)component == (Object)null', '!component.HasValue')
    text = text.replace('component.Info.ShareId', 'component.Value')
    text = text.replace('mStatusAgent.Component.StatusToShareStack', 'mStatusAgent.StatusToShareStack')
    text = text.replace('((Component)ApplyEntity).gameObject', 'ApplyEntity')
    text = replace_method(text, 'PlayMasterCastPlayable', '''	private void PlayMasterCastPlayable()
	{
		if ((Object)(object)MasterCastPlayable == (Object)null) return;
		GameObject master = Context.Master;
		if (master != null)
			mMasterCastPlayable = mMyPlayableManager.Play(MasterCastPlayable, master, ApplyEntity);
	}''')
    text = replace_method(text, 'CanShowSpriteIcon', '''	public bool CanShowSpriteIcon()
	{
		if (!ShowSpriteAbovePlayer) return false;
		return Context.HasIcon;
	}''')
    return wrap(text)


def agent():
    text = source('MyFightStatusAgent')
    text = text.replace('MyComponent<FightStatusAgentComponent, FightStatusAgentComponentInfo>', 'MonoBehaviour')
    text = text.replace('\tpublic HashSet<MyFightStatus> MyStatuses', '''	// Supplied by the actual shared-stack component, never computed by the view.
	public Dictionary<int, int> StatusToShareStack = new Dictionary<int, int>();

	public HashSet<MyFightStatus> MyStatuses''')
    text = text.replace('public override void OnEnd(MySerializable userData)', 'public void OnEnd()')
    text = text.replace('\n\t\tbase.OnEnd(userData);', '')
    return wrap(text)


def apply_stack():
    text = source('ApplyByStatusStackCount')
    start = text.index('\t\tGLoggerWarnInterpolatedStringHandler message;')
    end = text.index('\n\t}\n', start)
    text = text[:start] + '''		if (mChangePlayableStatusStackCount == null)
		{
			Debug.LogWarning("ApplyByStatusStackCount requires IChangePlayableStatusStackCount.");
			return;
		}
		MyFightStatus myFightStatus = playable.TryGetParam("Status") as MyFightStatus;
		if ((Object)(object)myFightStatus == (Object)null)
		{
			Debug.LogWarning("ApplyByStatusStackCount requires the original Status playable parameter.");
		}
		else
		{
			mStatusComponent = myFightStatus.Component;
			playable.OnStop += new Action<MyPlayable>(Playable_OnStop);
			mStatusComponent.OnStatusStackUpdate += new Action<IFightStatusPlayableState, int, int>(Component_OnStatusStackUpdate);
			Component_OnStatusStackUpdate(myFightStatus.Component, -1, myFightStatus.Component.StackedCount);
		}''' + text[end:]
    text = text.replace('FightStatusComponent', 'IFightStatusPlayableState')
    return wrap(text)


def apply_source():
    text = source('ApplyOverrideToStatusSource')
    text = replace_method(text, 'TryGetOverrideGameObject', '''	protected override bool TryGetOverrideGameObject(MyPlayable playable, out GameObject overrideObject)
	{
		overrideObject = null;
		MyFightStatus status = playable.TryGetParam("Status") as MyFightStatus;
		if ((Object)(object)status == (Object)null)
		{
			Debug.LogError(playable.name + " is not status's playable");
			return false;
		}
		overrideObject = status.Context.Creator;
		return (Object)(object)overrideObject != (Object)null;
	}''')
    return wrap(text)


def methods(text):
    result = {}
    for match in re.finditer(r'(?m)^\t(?:public|private|protected)[^\n;=]*\b(\w+)\(([^\n]*)\)\n\t\{', text):
        start = text.index('{', match.start())
        depth, end = 1, start + 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        key = match[1] + '(' + match[2] + ')'
        result[key] = text[start:end]
    return result


def visual_boundary(text):
    # Explicit entity-access substitutions only. Validate each remaining original
    # branch/order against the port, rather than accepting regenerated hashes.
    replacements = {
        'FightStatusComponent': 'IFightStatusPlayableState',
        'Component.Info.CanStackCount()': 'Component.CanStackCount',
        '(Object)(object)mFightStatusShareStack == (Object)null': '!mShareId.HasValue',
        'mFightStatusShareStack.Info.ShareId': 'mShareId.Value',
        'MyFightStatusShareStack component = ((Component)myStatus).GetComponent<MyFightStatusShareStack>();':
            'int? component = myStatus.Context.ShareId;',
        '(Object)(object)component == (Object)null': '!component.HasValue',
        'component.Info.ShareId': 'component.Value',
        'mStatusAgent.Component.StatusToShareStack': 'mStatusAgent.StatusToShareStack',
        '((Component)ApplyEntity).gameObject': 'ApplyEntity',
    }
    for old, new in replacements.items(): text = text.replace(old, new)
    return re.sub(r'\s+', '', text)


def build():
    manifest = json.loads((CODE.parent / 'code-manifest.json').read_bytes())
    assembly = CODE / 'managed/decoded/Assembly-CSharp.dll'
    framework = CODE / 'managed/decoded/Framework.FirstPass.Runtime.dll'
    assert manifest['manifest'] == '884_156692'
    assert identity(assembly)['sha256'] == manifest['assemblies'][assembly.name]['decoded_sha256']
    assert identity(framework)['sha256'] == manifest['assemblies'][framework.name]['decoded_sha256']
    generated = dict(MyFightStatus=status_view(), MyFightStatusAgent=agent(),
                     ApplyByStatusStackCount=apply_stack(), ApplyOverrideToStatusSource=apply_source(),
                     ApplyOverrideSourceOrTarget=wrap(source('ApplyOverrideSourceOrTarget')),
                     IChangePlayableStatusStackCount=wrap(source('IChangePlayableStatusStackCount')))
    action, diagnostics = logging_adapter(source('GAction3'))
    assert diagnostics == 1
    generated['GAction3'] = wrap(action)
    sources, outputs = [], []
    for name, text in generated.items():
        target = OUTPUT / (name + '.cs')
        write(target, text.encode())
        runtime_meta(target)
        outputs += [target, Path(str(target) + '.meta')]
        old = methods(source(name))
        unchanged, adapted, verified = {}, {}, {}
        for key, body in methods(text).items():
            if key not in old: continue
            fingerprint = lambda value: sha256(re.sub(r'\s+', '', value).encode()).hexdigest()
            if fingerprint(body) == fingerprint(old[key]): unchanged[key] = fingerprint(body)
            else: adapted[key] = dict(original=fingerprint(old[key]), migrated=fingerprint(body))
        if name == 'MyFightStatus':
            excluded = {'OnStart', 'OnEnd', 'NotifyStartEvent', 'NotifyEndEvent',
                        'PlayMasterCastPlayable', 'CanShowSpriteIcon'}
            migrated = methods(text)
            for key, body in old.items():
                if key.split('(')[0] in excluded: continue
                mapped_key = key.replace('FightStatusComponent', 'IFightStatusPlayableState')
                assert visual_boundary(body) == visual_boundary(migrated[mapped_key]), key
                verified[key] = sha256(visual_boundary(body).encode()).hexdigest()
            assert len(verified) == 22, verified.keys()
        source_path = CODE / ('framework-firstpass-source/Framework/GAction.cs' if name == 'GAction3'
                              else 'client-source/' + name + '.cs')
        sources.append(dict(original=identity(source_path),
                            migrated=identity(target),
                            unchanged_method_bodies=unchanged, entity_boundary_methods=adapted,
                            verified_visual_bodies_after_explicit_entity_access_substitution=verified))
        if name not in ('GAction3', 'IChangePlayableStatusStackCount'):
            sources[-1]['original_mono_script_file_id'] = struct.unpack('<i', MD4.new(('s\0\0\0' + name).encode()).digest()[:4])[0]
    boundary = OUTPUT / 'IFightStatusPlayableContext.cs'
    runtime_meta(boundary)
    outputs += [boundary, Path(str(boundary) + '.meta')]
    fixture = ROOT / 'unity/Assets/Game/Editor/BattleStatusPlayables/OfficialBattleStatusPlayableAcceptance.StatusView.cs'
    runtime_meta(fixture)
    write(RECEIPT, (json.dumps(dict(manifest='884_156692',
        code_extraction=identity(CODE.parent / 'code-manifest.json'), assemblies=[identity(assembly), identity(framework)],
        sources=sources, generator=identity(Path(__file__)), outputs=[identity(path) for path in outputs],
        entity_boundary_evidence=[identity(CODE / path) for path in (
            'client-source/MyFightStatusShareStack.cs',
            'common-source/Common/FightStatusComponentInfo.cs',
            'common-source/Common/FightStatusAgentComponent.cs',
            'common-source/Common/FightStatusShareStackComponent.cs',
            'common-source/Common/FightStatusShareStackComponentInfo.cs')],
        boundary=identity(boundary), reused_dependencies=[identity(ROOT / path) for path in (
            'assets/reference/battle-animation/status-playable-runtime-source-manifest.json',
            'scripts/prepare_unity_story_sound_runtime.py',
            'scripts/prepare_unity_story_playable_runtime.py')],
        validation=[identity(path) for path in (fixture,
            fixture.with_name('OfficialBattleStatusPlayableAcceptance.cs'),
            fixture.with_name('Zjcs.Editor.BattleStatusPlayables.asmdef'),
            ROOT / 'scripts/prepare_battle_status_playable_validation.py')],
        adaptation='Official MyFightStatus lifecycle and MyFightStatusAgent membership. Original local status/entity metadata, events, shared-stack totals, creator/master GameObjects and icon availability enter through explicit host interfaces; no rule computation is added. Original DI becomes Bind, disposable forbidden subscription becomes a symmetric event subscription, framework logging becomes Unity diagnostics.',
        limits=['Full native status graphs, shared-stack rule producers and formal battle actor/timeline binding are not supplied by this view component.']),
        ensure_ascii=False, indent=2) + '\n').encode())
    print('Original status view lifecycle, shared display selection and stack/source applicators migrated.')


if __name__ == '__main__':
    build()
