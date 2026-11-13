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
            'Repair decompiled Queue.TryDequeue ref to out.'], tool=identity(Path(__file__)))
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print(json.dumps(dict(inputs=len(inputs), outputs=len(outputs))))


if __name__ == '__main__':
    main()
