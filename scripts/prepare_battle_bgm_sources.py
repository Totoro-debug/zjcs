"""Migrate the original BGM stack, reference counts and deferred operations."""
from pathlib import Path
import hashlib
import json
import re
import uuid

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / '.tools/source-packages/official-code'
CODE = ROOT / 'unity/Assets/Game/Runtime/Official/BattleBgm'
RECEIPT = ROOT / 'assets/reference/battle-animation/bgm-runtime-source-manifest.json'


def identity(path):
    raw = path.read_bytes()
    return dict(path=path.relative_to(ROOT).as_posix(), bytes=len(raw), sha256=hashlib.sha256(raw).hexdigest())


def main():
    previous = json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {}
    previous_hashes = {r['path']: r['sha256'] for r in previous.get('outputs', [])}
    inputs, outputs = [], []

    def write(path, text):
        raw = text.encode('utf-8')
        key = path.relative_to(ROOT).as_posix()
        if path.exists() and path.read_bytes() != raw:
            assert hashlib.sha256(path.read_bytes()).hexdigest() == previous_hashes.get(key), key
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists() or path.read_bytes() != raw:
            path.write_bytes(raw)
        outputs.append(identity(path))

    def meta(path):
        guid = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs:battle-bgm:' + path.relative_to(ROOT).as_posix()).hex
        write(Path(str(path) + '.meta'), 'fileFormatVersion: 2\nguid: ' + guid + '\n')

    folder_guid = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs:battle-bgm:folder').hex
    write(Path(str(CODE) + '.meta'), 'fileFormatVersion: 2\nguid: ' + folder_guid
          + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n')

    for relative in ('client-source/BgmManager.cs', 'client-source/BgmEventKeyConfig.cs',
                     'client-source/Sound.Bgm/IBgmManager.cs', 'client-source/Sound.Bgm/PlayBgmHandle.cs',
                     'launch-source/IBgmGameObjectProvider.cs', 'launch-source/BgmGameObjectProvider.cs',
                     'launch-source/BgmGameObject.cs', 'client-source/BgmEventConfig.cs',
                     'client-source/GameBgm.cs', 'client-source/GameBgmConfig.cs',
                     'client-source/IBgmManagerExtensions.cs'):
        source = SOURCE / relative
        inputs.append(identity(source))
        text = source.read_text(encoding='utf-8-sig')
        text = re.sub(r'^namespace Sound\.Bgm;\n', '', text, flags=re.M)
        text = text.replace('using Framework.DependencyInjection;\n', '').replace('using Framework;\n', '')
        text = text.replace('using Sound.Bgm;\n', '')
        text = text.replace('\t[Inject]\n', '').replace('[ScriptableObjectConfig]\n', '')
        if source.name == 'GameBgm.cs':
            # The local prefab loader injects this before the original OnEnable.
            text = text.replace('private IBgmGameObjectProvider mBgmGameObjectProvider',
                                'public IBgmGameObjectProvider mBgmGameObjectProvider')
        if source.name == 'BgmManager.cs':
            for using in ('Common', 'GFrameCore.Extensions.ZLogger', 'Launch',
                          'Microsoft.Extensions.Logging', 'Sound.Bgm', 'ZLogger'):
                text = text.replace('using ' + using + ';\n', '')
            text, removed = re.subn(r'\t\t//IL_.*?\n(?=\t\tBgmData)', '', text, flags=re.S)
            assert removed == 2
            text = text.replace('private readonly IWwiseProxy mWwiseProxy;', 'private readonly IBgmPlayback mPlayback;')
            text = text.replace('\n\tprivate readonly ILogger<BgmManager> mLogger;\n', '')
            text = text.replace('IWwiseProxy wwiseProxy, ILogger<BgmManager> logger', 'IBgmPlayback playback')
            text = text.replace('mWwiseProxy = wwiseProxy;\n\t\tmLogger = logger;', 'mPlayback = playback;')
            text = text.replace('mWwiseProxy.Wwise.', 'mPlayback.')
            assert text.count('TryDequeue(ref bgmDeferData)') == 1
            text = text.replace('TryDequeue(ref bgmDeferData)', 'TryDequeue(out bgmDeferData)')
        # Unity 2022 C# does not support file-scoped namespaces.
        usings = re.match(r'(?:using [^\n]+;\n)*', text)[0]
        body = text[len(usings):].strip()
        text = usings + '\nnamespace Zjcs.Official.BattleBgm\n{\n' + body + '\n}\n'
        target = CODE / source.name
        write(target, text); meta(target)

    seam = CODE / 'IBgmPlayback.cs'
    write(seam, '''using UnityEngine;

namespace Zjcs.Official.BattleBgm
{
    // The two Wwise operations consumed by the original manager. Native playback
    // and the recording acceptance adapter use the same interface.
    public interface IBgmPlayback
    {
        uint PostEvent(string eventName, GameObject emitter);
        void SetSwitch(string group, string value, GameObject emitter);
    }
}
'''); meta(seam)
    assembly = CODE / 'Zjcs.Official.BattleBgm.asmdef'
    write(assembly, json.dumps(dict(name='Zjcs.Official.BattleBgm',
          rootNamespace='Zjcs.Official.BattleBgm', references=['AK.Wwise.Unity.API.WwiseTypes'], autoReferenced=True), indent=2) + '\n')
    meta(assembly)
    # Retain the original ordered selector and short-circuiting. Its condition
    # and random dependencies are supplied by the local configuration adapter.
    source = SOURCE / 'common-source/Common/FightThemeInfoParser.cs'
    inputs.append(identity(source))
    text = source.read_text(encoding='utf-8-sig')
    start = text.index('\tpublic FightThemeInfo GetThemeIdByStageInfo(')
    end = text.index('\n\tprotected override', start)
    method = text[start:end]
    method = method.replace('StageInfo stageInfo, ICondition.DataHub dataHub', 'int stageType')
    method = method.replace('\t\tConditionInfoParser conditionInfoParser = Parsers.Instance.ConditionInfoParser;\n', '')
    method = method.replace('stageInfo.Type', 'stageType')
    method = method.replace('ICondition condition = conditionInfoParser.GetCondition(value.ConditionId);',
                            'bool? condition = conditionIsMet(value.ConditionId);')
    method = method.replace('condition != null && condition.IsMet(dataHub) && RandomUtils.Next(10000)',
                            'condition == true && next(10000)')
    assert 'Parsers.' not in method and 'RandomUtils.' not in method and 'dataHub' not in method
    target = CODE / 'FightThemeInfoParser.cs'
    write(target, '''using System;
using System.Collections.Generic;

namespace Zjcs.Official.BattleBgm
{
public sealed class FightThemeInfoParser
{
    private readonly IReadOnlyDictionary<int, FightThemeInfo> configs;
    private readonly Func<int, bool?> conditionIsMet;
    private readonly Func<int, int> next;

    public FightThemeInfoParser(IReadOnlyDictionary<int, FightThemeInfo> configurations,
        Func<int, bool?> evaluateCondition, Func<int, int> nextRandom)
    {
        configs = configurations ?? throw new ArgumentNullException(nameof(configurations));
        conditionIsMet = evaluateCondition ?? throw new ArgumentNullException(nameof(evaluateCondition));
        next = nextRandom ?? throw new ArgumentNullException(nameof(nextRandom));
    }

''' + method + '\n}\n}\n'); meta(target)
    source = SOURCE / 'common-source/Common/FightThemeInfo.cs'
    inputs.append(identity(source))
    target = CODE / 'FightThemeInfo.cs'
    write(target, '''using System;
using System.Collections.Generic;

namespace Zjcs.Official.BattleBgm
{
    // The original selector consumes these four fields. Audio overrides and
    // presentation paths retain their separate, original resource bindings.
    [Serializable] public sealed class FightThemeInfo
    {
        public int FightThemeId, ConditionId, TriggerPercent;
        public List<int> StageTypes;
    }
}
'''); meta(target)
    source = SOURCE / 'common-source/Common/DomainTypeCondition.cs'
    condition_source = source.read_text(encoding='utf-8-sig')
    start = condition_source.index('\tpublic override bool IsMet(')
    end = condition_source.index('\n\tpublic DomainTypeCondition(', start)
    method = condition_source[start:end].replace('public override bool IsMet(DataHub dataHub)',
                                                'public bool IsMet(int domainType)')
    method = method.replace('PlanesInfoUtils.DomainType', 'domainType')
    target = CODE / 'DomainTypeCondition.cs'
    write(target, '''using System;

namespace Zjcs.Official.BattleBgm
{
    [Serializable] public sealed class DomainTypeCondition
    {
        public int DomainType;
        public bool Not;

''' + method + '\n    }\n}\n'); meta(target)
    inputs += [identity(SOURCE / p) for p in (
        'client-source/FightThemeGenerator.cs', 'common-source/Common/DomainTypeCondition.cs',
        'common-source/Common/DomainType.cs', 'common-source/Common/StageType.cs',
        'common-source/Common/RandomUtils.cs', 'framework-firstpass-source/Framework/GRandom.cs',
        'framework-firstpass-source/Framework/RandomFactory.cs',
        'framework-firstpass-source/Framework/SystemRandom.cs', 'launch-source/LaunchContainerInstaller.cs')]
    inputs += [identity(SOURCE / p) for p in ('client-source/FightBgmOverrideHandler.cs',
        'client-source/FightSceneRoot.cs')]
    export = ROOT / '.tools/source-packages/current/884_156692/native-conversion/native-export-all'
    export_receipt = export.with_name(export.name + '-source-manifest.json')
    exported = {r['path']: r for r in json.loads(export_receipt.read_bytes())['outputs']}
    for relative in (
        'ScriptableConfig/Modules/Audio/GameBgmConfig/GameBgmConfig.asset',
        'Wwise/ScriptableObjects/Event/77C68772-614B-403E-A056-39C6439E8659.asset',
        'Wwise/ScriptableObjects/Event/206D318C-6846-4E8C-96AF-D7B5748151F3.asset',
        'Wwise/ScriptableObjects/Event/340938C5-EE40-437E-B62D-259B7E976E26.asset',
        'Wwise/ScriptableObjects/Event/EF47AD8A-37F8-4561-B7D2-8732DBBDC7BB.asset',
        'Wwise/ScriptableObjects/Switch/8D7FD78C-6384-4235-8FDA-6295AD85A024.asset',
        'Wwise/ScriptableObjects/SwitchGroup/0B67A831-34D7-4D1C-8DDB-6DAF92440040.asset',
        'Prefabs/EcEntity/Fight/FightStage/1000/game_1000.prefab',
    ):
        path = export / 'ExportedProject/Assets' / relative
        record = identity(path)
        assert record['sha256'] == exported[path.relative_to(export).as_posix()]['sha256'], relative
        inputs.append(record)
    receipt = dict(manifest='884_156692', inputs=inputs, outputs=outputs,
        adaptations=['Namespace isolation and block namespaces for Unity 2022.',
            'Replace DI attributes with pre-activation provider injection; expose the original GameBgm provider setter.',
            'Remove only the framework ScriptableObjectConfig registration attribute; original asset fields remain.',
            'Remove only the two trace logging blocks and logger dependency.',
            'Replace the broad Wwise proxy with the two consumed playback operations.',
            'Retain the original theme selector iteration, stage filter, condition/null short-circuit and strict Next(10000) threshold; inject only its consumed condition and random operations.',
            'Project the four original theme fields consumed by the selector; keep original audio asset bindings separately.',
            'Retain DomainTypeCondition.IsMet with the same equality/Not branches; supply the current local domain instead of the online PlanesInfo singleton.',
            'Repair decompiled Queue.TryDequeue ref to out.'], tool=identity(Path(__file__)))
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print(json.dumps(dict(inputs=len(inputs), outputs=len(outputs))))


if __name__ == '__main__':
    main()
