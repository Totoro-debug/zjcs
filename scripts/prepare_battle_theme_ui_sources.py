"""Port original legacy animation sequences used by the battle theme NPC."""
import hashlib
import json
from pathlib import Path
import uuid
from official_source import OfficialSource
from conversion_history import preserve

ROOT = Path(__file__).resolve().parents[1]
SELECTED = OfficialSource()
SOURCE = SELECTED.prepared_path / 'official-code/client-source'
CODE = ROOT / 'unity/Assets/Game/Runtime/Official/BattleThemeUI'
RECEIPT = ROOT / 'assets/reference/battle-animation/theme-ui-runtime-source-manifest.json'


def identity(path):
    raw = path.read_bytes()
    return dict(path=path.relative_to(ROOT).as_posix(), bytes=len(raw),
                sha256=hashlib.sha256(raw).hexdigest())


def main():
    previous = json.loads(RECEIPT.read_bytes()) if RECEIPT.is_file() else None
    if previous:
        for row in previous['outputs']:
            raw = (ROOT / row['path']).read_bytes()
            if identity(ROOT / row['path'])['sha256'] != row['sha256'] and hashlib.sha256(raw.replace(b'\r\n', b'\n')).hexdigest() != row['sha256']:
                raise ValueError('Preserve a modified theme UI runtime output: ' + row['path'])
    code_manifest = SOURCE.parent.parent / 'code-manifest.json'
    code_source = json.loads(code_manifest.read_bytes())
    assembly = SOURCE.parent / 'managed/decoded/Assembly-CSharp.dll'
    metadata = SOURCE.parent / 'global-metadata.dat'
    if (code_source['manifest'] != SELECTED.version or code_source['apk_sha256'] != SELECTED.apk_sha256
            or identity(assembly)['sha256'] != code_source['assemblies'][assembly.name]['decoded_sha256']
            or identity(metadata)['sha256'] != code_source['metadata_sha256']):
        raise ValueError('Verify the selected original application assembly and metadata first')
    outputs = []
    inputs = []

    def write(path, text):
        path.parent.mkdir(parents=True, exist_ok=True)
        raw = text.replace('\r\n', '\n').replace('\n', '\r\n').encode('utf-8')
        if not path.is_file() or path.read_bytes() != raw:
            path.write_bytes(raw)
        outputs.append(identity(path))

    def meta(path, folder=False):
        key = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs:battle-theme-ui:' + path.relative_to(ROOT).as_posix()).hex
        kind = 'DefaultImporter' if folder else ('AssemblyDefinitionImporter' if path.suffix == '.asmdef' else 'MonoImporter')
        write(Path(str(path) + '.meta'), 'fileFormatVersion: 2\nguid: ' + key + '\n'
              + ('folderAsset: yes\n' if folder else '') + kind + ':\n  externalObjects: {}\n')

    CODE.mkdir(parents=True, exist_ok=True)
    meta(CODE, True)
    for name in ('AnimationSequence', 'AnimationClipData'):
        source = SOURCE / (name + '.cs')
        inputs.append(identity(source))
        text = source.read_text(encoding='utf-8-sig')
        target = CODE / source.name
        write(target, 'namespace Zjcs.Official.BattleThemeUI\n{\nusing Object = UnityEngine.Object;\n'
              + text + '\n}\n')
        meta(target)
    source = SOURCE / 'FightMainPanelThemeNpcModule.cs'
    inputs.append(identity(source))
    text = source.read_text(encoding='utf-8-sig').replace('using Framework;', 'using System;')
    replacements = {
        ': FightPanelModuleBase': ': MonoBehaviour',
        '\tpublic override EVisibilityOwner VisibilityOwner => EVisibilityOwner.Self;': '''    private string themePrefabPath;
    private Action<Transform> destroyChildren;
    private Action<string, Transform> instantiate;

    public void Configure(Transform slot, string prefabPath, Action<Transform> clear,
        Action<string, Transform> create)
    {
        mThemeNpcSlot = slot;
        themePrefabPath = prefabPath;
        destroyChildren = clear ?? throw new ArgumentNullException(nameof(clear));
        instantiate = create ?? throw new ArgumentNullException(nameof(create));
    }''',
        'public override void OnActivate()': 'public void OnActivate()',
        'mThemeNpcSlot.DestroyChildren();': 'destroyChildren(mThemeNpcSlot);',
        'mFightRoot.FightThemeInfo != null': 'themePrefabPath != null',
        'UIUtils.InstantiateAsync(mFightRoot.FightThemeInfo.UIPrefabPath, mThemeNpcSlot, null);':
            'instantiate(themePrefabPath, mThemeNpcSlot);',
    }
    for original, replacement in replacements.items():
        assert text.count(original) == 1, original
        text = text.replace(original, replacement)
    target = CODE / source.name
    write(target, 'namespace Zjcs.Official.BattleThemeUI\n{\nusing Object = UnityEngine.Object;\n'
          + text + '\n}\n')
    meta(target)
    assembly = CODE / 'Zjcs.Official.BattleThemeUI.asmdef'
    write(assembly, json.dumps(dict(name='Zjcs.Official.BattleThemeUI',
          rootNamespace='Zjcs.Official.BattleThemeUI', references=[], autoReferenced=True), indent=2) + '\n')
    meta(assembly)
    old_inputs = {Path(row['path']).name: row for row in previous['inputs']} if previous else {}
    comparisons = [dict(current=row, previous=old_inputs[Path(row['path']).name],
        entire_source_identical=row['sha256'] == old_inputs[Path(row['path']).name]['sha256']) for row in inputs if Path(row['path']).name in old_inputs]
    receipt = dict(manifest=SELECTED.version, inputs=inputs, outputs=outputs,
        code_source=[identity(path) for path in (code_manifest, assembly, metadata)],
        previous=preserve(RECEIPT) if previous else None, comparisons=comparisons,
        adaptations=['AnimationSequence and AnimationClipData: namespace isolation and an explicit UnityEngine.Object alias only; original serialized fields, coroutine sequencing, loop counts, callbacks and activation/disable order retained.',
            'FightMainPanelThemeNpcModule: replace unneeded fight-root/base-module services with the consumed immutable prefab path and two local resource operations. Preserve activation, missing-slot check, destroy-children-before-load order and null-theme branch.'],
        tool=identity(Path(__file__)))
    RECEIPT.write_bytes((json.dumps(receipt, ensure_ascii=False, indent=2) + '\n').replace('\n', '\r\n').encode('utf-8'))
    print(json.dumps(dict(inputs=len(inputs), outputs=len(outputs))))


def verify():
    proof = json.loads(RECEIPT.read_bytes())
    if proof['manifest'] != SELECTED.version:
        raise ValueError('The theme UI source differs from the selected source')
    for row in [*proof['inputs'], *proof['outputs'], *proof['code_source'], proof['tool']]:
        if identity(ROOT / row['path']) != row:
            raise ValueError('A selected theme UI input or output changed: ' + row['path'])
    print('Current theme UI application source and original runtime conversion verified.')


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--verify', action='store_true')
    verify() if parser.parse_args().verify else main()
