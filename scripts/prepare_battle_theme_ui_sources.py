"""Port original legacy animation sequences used by the battle theme NPC."""
import hashlib
import json
from pathlib import Path
import uuid

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / '.tools/source-packages/official-code/client-source'
SELECTED = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source'
CODE = ROOT / 'unity/Assets/Game/Runtime/Official/BattleThemeUI'
RECEIPT = ROOT / 'assets/reference/battle-animation/theme-ui-runtime-source-manifest.json'


def identity(path):
    raw = path.read_bytes()
    return dict(path=path.relative_to(ROOT).as_posix(), bytes=len(raw),
                sha256=hashlib.sha256(raw).hexdigest())


def main():
    outputs = []
    inputs = []

    def write(path, text):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding='utf-8', newline='\n')
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
        assert source.read_bytes() == (SELECTED / source.name).read_bytes(), source
        inputs.append(identity(source))
        text = source.read_text(encoding='utf-8-sig')
        target = CODE / source.name
        write(target, 'namespace Zjcs.Official.BattleThemeUI\n{\nusing Object = UnityEngine.Object;\n'
              + text + '\n}\n')
        meta(target)
    source = SOURCE / 'FightMainPanelThemeNpcModule.cs'
    assert source.read_bytes() == (SELECTED / source.name).read_bytes(), source
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
    receipt = dict(manifest='884_156692', inputs=inputs, outputs=outputs,
        adaptations=['AnimationSequence and AnimationClipData: namespace isolation and an explicit UnityEngine.Object alias only; original serialized fields, coroutine sequencing, loop counts, callbacks and activation/disable order retained.',
            'FightMainPanelThemeNpcModule: replace unneeded fight-root/base-module services with the consumed immutable prefab path and two local resource operations. Preserve activation, missing-slot check, destroy-children-before-load order and null-theme branch.'],
        tool=identity(Path(__file__)))
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print(json.dumps(dict(inputs=len(inputs), outputs=len(outputs))))


if __name__ == '__main__':
    main()
