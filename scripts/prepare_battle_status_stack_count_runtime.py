"""Port the original empty stack threshold wrapper through the existing view-data boundary."""
import json
import re
from pathlib import Path

from official_source import OfficialSource, ROOT
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed


def main():
    source = OfficialSource()
    code = source.prepared_path / 'official-code'
    original = code / 'client-source/MyStatusStackCount.cs'
    text = original.read_text('utf-8-sig')
    assert re.fullmatch(r'using Common;\s*public class MyStatusStackCount\s*'
        r': MyComponent<StatusStackCountComponent, StatusStackCountComponentInfo>\s*\{\s*\}\s*', text)
    text = text.replace('using Common;', 'using Zjcs.Official.EntityLifecycle;\nusing Zjcs.Official.EntityViews;')
    text = text.replace('MyComponent<StatusStackCountComponent, StatusStackCountComponentInfo>',
        'MyComponent<IBattleGridComponentViewData>')
    path = ROOT / 'unity/Assets/Game/Runtime/Official/BattleGridItemEntities/MyStatusStackCount.cs'
    text = '// Original empty client wrapper; see status stack count runtime source receipt.\n' \
        + 'namespace Zjcs.Official.BattleGridItemEntities\n{\n' + text.strip() + '\n}\n'
    write_changed(path, text); meta(path)
    write_changed(ROOT / 'assets/reference/battle-animation/status-stack-count-runtime-source-manifest.json',
        json.dumps(dict(manifest=source.version, original=identity(original),
            assembly=identity(code / 'managed/decoded/Assembly-CSharp.dll'),
            outputs=[identity(path), identity(path.with_name(path.name+'.meta'))],
            generator=identity(Path(__file__)),
            adaptation='The original empty MyComponent wrapper remains empty; its unavailable server component types use the existing shared view-data boundary.'),
            ensure_ascii=False, indent=2) + '\n')
    print('Current original empty stack-count wrapper verified and migrated.')


if __name__ == '__main__':
    main()
