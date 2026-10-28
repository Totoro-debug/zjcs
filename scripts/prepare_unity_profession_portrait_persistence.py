"""Migrate the original portrait disk cache, without selecting a user save path."""
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
CODE = ROOT / '.tools/source-packages/current/871_156598/official-code'
TARGET = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI'


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def build():
    ports = []
    names = ['CharacterPortraitDetailRequestData', 'CharacterPortraitPersisterState',
             'ICharacterPortraitPersistentCollection', 'ICharacterPortraitPersister',
             'CharacterPortraitPersistentCollection', 'CharacterPortraitPersister']
    for name in names + ['IStorage', 'IPersistentState']:
        source = CODE / ('framework-firstpass-source/Framework.Storage' if name in ('IStorage', 'IPersistentState') else 'client-source') / (name + '.cs')
        text = source.read_text(encoding='utf-8')
        text = re.sub(r'^using (?:Framework[^;]*|GFrameCore[^;]*|Microsoft.Extensions[^;]*|ZLogger);\n', '', text, flags=re.M)
        text = text.replace('namespace Framework.Storage;\n', '')
        text = re.sub(r'^\s*//IL_[^\n]*\n', '\n', text, flags=re.M)
        if name == 'CharacterPortraitPersistentCollection':
            text = text.replace('public CharacterPortraitPersistentCollection()', 'public CharacterPortraitPersistentCollection(string sandboxRoot)')
            text = text.replace('PathUtils.GetFileSandboxPath("CharacterPortrait")', 'Path.Combine(sandboxRoot, "CharacterPortrait")')
        if name == 'CharacterPortraitPersister':
            text = text.replace('\tprivate ILogger<CharacterPortraitManager> mLogger;\n', '')
            text = text.replace('ILogger<CharacterPortraitManager> logger, ', '')
            text = text.replace('IPersistentState<CharacterPortraitPersisterState> persistentState)', 'IPersistentState<CharacterPortraitPersisterState> persistentState, string sandboxRoot)')
            text = text.replace('\t\tmLogger = logger;\n', '').replace('new CharacterPortraitPersistentCollection()', 'new CharacterPortraitPersistentCollection(sandboxRoot)')
            pattern = r'(?m)^([ \t]*)ILogger val = [^\n]+;\n.*?^[ \t]*GZLoggerExtensions\.(?:Error|Debug)\([^\n]*;'
            messages = iter(['UnityEngine.Debug.LogException(ex);',
                'UnityEngine.Debug.LogError("SetPersistentTexture texture.BsPngData 是null");',
                'UnityEngine.Debug.LogException(ex);',
                'UnityEngine.Debug.Log("ClearAllExpirePersistentStorage, 删除" + num + "个过期的肖像数据");',
                'UnityEngine.Debug.Log("ClearAllStorage， 删除所有肖像数据");'])
            count = 0
            def diagnostic(match):
                nonlocal count
                count += 1
                return match[1] + next(messages)
            text = re.sub(pattern, diagnostic, text, flags=re.S)
            assert count == 5
            text = re.sub(r'^\s*(?:ZLoggerErrorInterpolatedStringHandler val3;|bool flag = default;)\n', '\n', text, flags=re.M)
        if any(x in text for x in ('ILogger', 'GZLogger', 'PathUtils', 'ZLoggerError')):
            raise ValueError('Unresolved original persistence dependency: ' + name)
        text = re.sub(r'\n(?:[ \t]*\n){2,}', '\n\n', text)
        target = TARGET / (name + '.cs')
        target.write_text('namespace Zjcs.Official.ProfessionUI\n{\n' + text + '\n}\n', encoding='utf-8', newline='\n')
        meta = target.with_name(target.name + '.meta')
        meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs-profession-ui-' + name).encode()).hexdigest() + '\n', encoding='utf-8', newline='\n')
        ports.append(dict(source=record(source), runtime=record(target), meta=record(meta)))
    assembly = TARGET / 'Zjcs.Official.ProfessionUI.asmdef'
    editor = ROOT / 'unity/Assets/Game/Editor/ProfessionUI/Zjcs.Editor.ProfessionUI.asmdef'
    for path in (assembly, editor):
        data = json.loads(path.read_bytes())
        if 'UniTask' not in data['references']:
            data['references'].append('UniTask')
            path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    proof = dict(format=1, manifest='871_156598', ports=ports,
        assemblies=[record(assembly), record(editor)], generator=record(Path(__file__)),
        validation=record(ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionPortraitPersistenceValidation.cs'),
        validation_meta=record(ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionPortraitPersistenceValidation.cs.meta'),
        operation='Copy original disk collection, persister, state, request data and storage interfaces. Retain raw file bytes, request/hash filenames (not VersionId), three-day LastAccessTimeUtc expiry, code-version 3 invalidation and write-before-expiry order, empty-data and I/O failure behavior. Replace PathUtils sandbox lookup with an explicit caller-owned root and diagnostics with Unity logging. Keep original UniTask storage contract.',
        validation_scope='Actual isolated Unity Play Mode renders GPU RGBA pixels, persists and recovers identical bytes after reconstructing the state provider and persister, then reloads a real texture. Verifies version-3 invalidation and write order, Id preservation, VersionId-independent filenames, empty inputs/entries, real FileShare.None read/write failures, three-day LastAccessTimeUtc expiry independent of write time, and clear/recreate. State provider and pixels are fixtures, not production game saves or character art.',
        limitations=['Production save-state provider, photographer/manager and full native profession UI remain unbound. No player save path is selected by this module.'])
    path = ROOT / 'assets/reference/profession-ui/portrait-persistence-source-manifest.json'
    path.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Ported original portrait persistence and storage contracts.')


if __name__ == '__main__':
    build()
