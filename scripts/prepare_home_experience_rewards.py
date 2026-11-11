"""Restore the selected original asynchronous experience reward handler."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
CODE = ROOT / '.tools/source-packages/current/884_156692/official-code'
REFERENCES = CODE.parent / 'native-conversion/inputs/Managed'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/HomeExperienceRewards'
PREFIX = '// Migrated selected official implementation; service ports only.\n'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def replace(text, before, after, count=None):
    actual = text.count(before)
    if not actual or (count is not None and actual != count):
        raise ValueError((before, actual, count))
    return text.replace(before, after)


def resolved_source(dotnet, ilspy):
    """Resolving the original UniTask dependency restores the async syntax."""
    source = CODE / 'client-source-resolved/PlayerExpRewardHandler.cs'
    proof = source.with_suffix('.decompilation.json')
    assembly = CODE / 'managed/decoded/Assembly-CSharp.dll'
    inputs = {p.relative_to(ROOT).as_posix(): digest(p)
              for p in [assembly, ilspy, *sorted(REFERENCES.glob('*.dll'))]}
    if not source.exists() or not proof.exists():
        command = [str(dotnet), str(ilspy), '-t', 'PlayerExpRewardHandler',
                   '-r', str(REFERENCES), str(assembly)]
        result = subprocess.run(command, check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        raw = result.stdout.decode('utf-8-sig').replace('\r\n', '\n')
        if 'private async UniTaskVoid Play(' not in raw or 'IAsyncStateMachine' in raw:
            raise ValueError('Resolve the original async dependencies before migrating.')
        source.parent.mkdir(parents=True, exist_ok=True)
        source.write_text(raw, 'utf8', newline='\n')
        proof.write_text(json.dumps(dict(inputs=inputs, sha256=digest(source),
            command=[Path(p).relative_to(ROOT).as_posix() if Path(p).is_absolute() else p
                     for p in command]), ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
    evidence = json.loads(proof.read_bytes())
    if evidence['inputs'] != inputs or evidence['sha256'] != digest(source):
        raise ValueError('Preserve and recheck changed original decompilation inputs.')
    inputs[source.relative_to(ROOT).as_posix()] = digest(source)
    inputs[proof.relative_to(ROOT).as_posix()] = digest(proof)
    return source.read_text('utf8'), inputs


def write(path, text, outputs):
    if path.exists() and path.suffix == '.cs' and not path.read_text('utf8').startswith(PREFIX):
        raise ValueError('Preserve existing runtime code: '+str(path))
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, 'utf8', newline='\n')
    outputs[path.relative_to(ROOT).as_posix()] = digest(path)
    meta = Path(str(path)+'.meta')
    if not meta.exists():
        key = hashlib.md5(('zjcs:home-experience-rewards:'+path.name).encode()).hexdigest()
        meta.write_text('fileFormatVersion: 2\nguid: '+key+'\n', 'utf8', newline='\n')
    outputs[meta.relative_to(ROOT).as_posix()] = digest(meta)


def build(dotnet, ilspy):
    text, inputs = resolved_source(dotnet, ilspy)
    original_async = text[text.index('\tprivate async UniTaskVoid Play('):text.index('\tprivate void DefUpgradeDisplay(')]
    for line in ('using Common;\n', 'using Framework;\n', 'using GameFramework;\n'):
        text = replace(text, line, '', 1)
    declaration = 'public class PlayerExpRewardHandler : SceneScopeBackgroundServiceBase, IHomeScopeBackgroundService, IMapScopeBackgroundService, IFightScopeBackgroundService, IPlayerExpRewardHandler, ICombatRatingTipBlocker'
    text = replace(text, declaration, 'public sealed class HomeExperienceRewardHandler : IHomeExperienceRewards, IDisposable', 1)
    position = text.index('{')+1
    text = text[:position]+'''
    private readonly IHomeExperienceRewardBindings Bindings;
    private bool active;
    public HomeExperienceRewardHandler(IHomeExperienceRewardBindings bindings)
    {
        Bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
    }
    public void Activate()
    {
        if (active) return;
        active = true;
        OnActivate();
    }
    public void Dispose()
    {
        if (!active) return;
        OnDeactivate();
        active = false;
    }
'''+text[position:]
    substitutions = [
        ('IPlayerExpRewardHandler.RewardExpDisplayDel', 'Func<long, long, IEnumerator>'),
        ('IPlayerExpRewardHandler.UpgradeDisplayDelay', 'Func<bool>'),
        ('IPlayerExpRewardHandler.PlayerLevelUpgradeDisplayDel', 'Action<int, int>'),
        ('SafeAction<long, long>', 'HomeRewardSignal<long, long>'),
        ('ConsoleDataManager.Instance.Inited', 'Bindings.Store.Inited'),
        ('ConsoleDataManager.Instance.Get<long>(ConsoleDataManager.ConsoleDataKey.HandledExp)', 'Bindings.Store.HandledExp'),
        ('ConsoleDataManager.Instance.Set(ConsoleDataManager.ConsoleDataKey.HandledExp, value)', 'Bindings.Store.HandledExp = value'),
        ('ConsoleDataManager.Instance.Get<int>(ConsoleDataManager.ConsoleDataKey.LevelUpDialogHandledLevel)', 'Bindings.Store.HandledLevel'),
        ('ConsoleDataManager.Instance.Set(ConsoleDataManager.ConsoleDataKey.LevelUpDialogHandledLevel, value)', 'Bindings.Store.HandledLevel = value'),
        ('MeManager.MainPlayer', 'Bindings.Player'),
        ('MeManager.Instance.OnPlayerUpdate', 'Bindings.OnPlayerUpdate'),
        ('MeManager.Instance != null', 'Bindings.ManagerAvailable'),
        ('Player mainPlayer', 'IHomeExperienceRewardPlayer mainPlayer'),
        ('protected override void OnActivate()', 'private void OnActivate()'),
        ('protected override void OnDeactivate()', 'private void OnDeactivate()'),
        ('SubRank obj', 'int obj'),
        ('PlayerSeasonRecordDataWrap obj', 'object obj'),
        ('PlayerItemDataWrap itemData', 'HomeExperienceItem itemData'),
        ('AwardSourceData awardSourceData', 'object awardSourceData'),
        ('itemData.Count', 'Bindings.Player.GetItemAmount(itemData.Id)'),
        ('Parsers.GameSettings.PlayerExpItemId', 'Bindings.PlayerExpItemId'),
        ('PlayerCombatRatingHandler.Instance', 'Bindings.CombatRatingBlockers'),
        ('LevelUpNotifyTip.TryPopup(preLevel)', 'Bindings.ShowLevelUpNotification(preLevel)'),
        ('mainPlayer.OnBlessLevelUp = (Action<int, int>)Delegate.Combine(mainPlayer.OnBlessLevelUp, new Action<int, int>(MainPlayer_OnBlessLevelUp));', 'mainPlayer.OnBlessLevelUp += MainPlayer_OnBlessLevelUp;'),
        ('mainPlayer.OnBlessLevelUp = (Action<int, int>)Delegate.Remove(mainPlayer.OnBlessLevelUp, new Action<int, int>(MainPlayer_OnBlessLevelUp));', 'mainPlayer.OnBlessLevelUp -= MainPlayer_OnBlessLevelUp;')]
    for before, after in substitutions:
        text = replace(text, before, after)
    # These five complete async methods keep their awaits, conditions, loops,
    # cancellation tokens and ordering. Only delegate type names change.
    adapted_async = text[text.index('\tprivate async UniTaskVoid Play('):text.index('\tprivate void DefUpgradeDisplay(')]
    expected_async = original_async
    for before, after in substitutions:
        expected_async = expected_async.replace(before, after)
    assert adapted_async == expected_async
    outputs = {}
    write(DEST/'HomeExperienceRewardHandler.cs', PREFIX+'using Zjcs.Official.HomeHudUI;\n'
          +'namespace Zjcs.Official.HomeExperienceRewards\n{\n'+text+'\n}\n', outputs)
    signal = CODE/'framework-firstpass-source/Framework/SafeAction.cs'
    inputs[signal.relative_to(ROOT).as_posix()] = digest(signal)
    raw = signal.read_text('utf-8-sig').replace('\r\n','\n')
    raw = raw[raw.index('public class SafeAction<T1, T2>'):raw.index('public class SafeAction<T1, T2, T3>')]
    start = raw.index('\t\t\t\tGLoggerErrorInterpolatedStringHandler')
    end = raw.index('\n\t\t\t}', start)
    raw = raw[:start]+'\t\t\t\tUnityEngine.Debug.LogException(exception);'+raw[end:]
    raw = raw.replace('SafeAction<T1, T2>', 'HomeRewardSignal<T1, T2>')
    write(DEST/'HomeRewardSignal.cs', PREFIX+'using System;\nusing System.Collections.Generic;\n'
          +'namespace Zjcs.Official.HomeExperienceRewards\n{\n'+raw+'\n}\n', outputs)
    assembly = dict(name='Zjcs.Official.HomeExperienceRewards', references=[
        'Zjcs.Official.HomeHudUI', 'Zjcs.Official.ProfessionUI', 'UniTask'])
    write(DEST/'Zjcs.Official.HomeExperienceRewards.asmdef', json.dumps(assembly, indent=2)+'\n', outputs)
    for path in (DEST/'HomeExperienceRewardBindings.cs',):
        meta = Path(str(path)+'.meta')
        if not meta.exists():
            meta.write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(('zjcs:home-experience-rewards:'+path.name).encode()).hexdigest()+'\n','utf8',newline='\n')
        for file in (path,meta):outputs[file.relative_to(ROOT).as_posix()] = digest(file)
    folder_meta = Path(str(DEST)+'.meta')
    if not folder_meta.exists():
        folder_meta.write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:home-experience-rewards:folder').hexdigest()+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n','utf8',newline='\n')
    outputs[folder_meta.relative_to(ROOT).as_posix()] = digest(folder_meta)
    for path in (CODE/'client-source/PlayerExpRewardHandler.cs', CODE/'client-source/IPlayerExpRewardHandler.cs',
                 CODE/'common-source/ConsoleDataManager.cs', ROOT/'unity/Packages/com.cysharp.unitask/package.json',
                 ROOT/'unity/Packages/com.cysharp.unitask/Runtime/UniTask.asmdef', Path(__file__),
                 ROOT/'scripts/prepare_home_experience_rewards_validation.py',
                 ROOT/'scripts/fixtures/home_experience_rewards_validation.cs'):
        inputs[path.relative_to(ROOT).as_posix()] = digest(path)
    receipt = dict(version='884_156692', inputs=inputs, outputs=outputs,
        preserved_async_methods=['Play','PlayerGetExp','PlayRewardExpDisplay','PlayUpgradeDelay','PlayUpgradeDisplay'],
        adaptations=['Scene scope lifecycle ports', 'Selected player and console-data ports',
                     'Existing UniTask scheduling unchanged', 'Combat-rating blocker and notification ports',
                     'Delegate type aliases', 'Original SafeAction two-argument implementation with Unity exception logging'])
    target = ROOT/'assets/reference/home-experience-rewards-source-manifest.json'
    target.write_text(json.dumps(receipt, ensure_ascii=False, indent=2)+'\n','utf8',newline='\n')
    print('Migrated original asynchronous experience rewards:',len(outputs),'outputs')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--dotnet', type=Path, default=ROOT/'.tools/dotnet/dotnet.exe')
    parser.add_argument('--ilspy', type=Path, default=ROOT/'.tools/ilspycmd/tools/net10.0/any/ilspycmd.dll')
    args = parser.parse_args()
    build(args.dotnet, args.ilspy)
