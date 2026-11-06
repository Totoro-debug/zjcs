"""Migrate selected MonsterUI without changing its prefab/handle lifetime semantics."""
import hashlib
import json
import re
from pathlib import Path
from official_apk import ROOT
from prepare_partner_input_dialog import metadata


def main():
    base = ROOT / '.tools/source-packages/current/884_156692/official-code'
    source = base / 'client-source/MonsterUI.cs'
    text = source.read_text('utf8')
    text = text[text.index('public class MonsterUI'):]
    def replace(old, new):
        nonlocal text
        if text.count(old) != 1: raise ValueError('Reinspect original MonsterUI: ' + old[:90])
        text = text.replace(old, new)
    replace('private DisposableBag mDisposableBag;', 'private readonly List<IDisposable> mOperations = new List<IDisposable>();')
    replace('''[Inject]
\tprivate IServiceProvider ServiceProvider { get; set; }

\t[Inject]
\tprivate IResourceLoader ResourceLoader { get; set; }''', '''// The original scene container supplies parser, loader and injected instantiation.
\t[NonSerialized] public Func<int, int?> MonsterIconId;
\t[NonSerialized] public IProfessionImageResourceLoader ResourceLoader;
\t[NonSerialized] public Func<GameObject, Transform, GameObject> InstantiatePrefab;''')
    replace('\tprivate void Awake()\n\t{\n\t\t//IL_0006: Unknown result type (might be due to invalid IL or missing references)\n\t\tmDisposableBag = default;\n\t}\n\n', '')
    replace('mDisposableBag.Dispose();', 'for (int i = 0; i < mOperations.Count; i++) mOperations[i].Dispose();\n\t\tmOperations.Clear();')
    replace('Slot.DestroyChildren();', '''// Original TransformExtensions.DestroyChildren defaults: include inactive, no detach.
\t\tfor (int i = Slot.childCount - 1; i >= 0; i--)
\t\t\tUnityEngine.Object.Destroy(Slot.GetChild(i).gameObject);''')
    start = text.index('\t\tbool enabled = default;')
    end = text.index('\n\t\tstring monsterSpineIcon', start)
    text = text[:start] + '''\t\tif (MonsterIconId == null || ResourceLoader == null || InstantiatePrefab == null)
\t\t\tthrow new InvalidOperationException("Original MonsterUI scene services are unbound.");
\t\tint? iconId = MonsterIconId(monsterId);
\t\tif (!iconId.HasValue)
\t\t{
\t\t\tDebug.LogError("不存在怪物配置，id:" + monsterId, this);
\t\t\treturn;
\t\t}''' + text[end:]
    replace('LanguageUtils.GetMonsterSpineIcon(config.IconId)', '$"Appearance/Monster/{iconId.Value}/MonsterIcon_{iconId.Value}"')
    replace('GameObject result = Disposable.AddTo<AssetOperationHandle<GameObject>>(ResourceLoader.LoadPrefab(monsterSpineIcon), ref mDisposableBag).Result;', '''var operation = ResourceLoader.LoadAsset<GameObject>(monsterSpineIcon);
\t\tmOperations.Add(operation);
\t\tGameObject result = operation.Result;''')
    start = text.index('\t\t\tILogger role =')
    end = text.index('\n\t\t}\n\t\telse', start)
    text = text[:start] + '\t\t\tDebug.LogError("不存在Icon:" + monsterSpineIcon, this);' + text[end:]
    replace('ServiceProviderExtensions.Instantiate(ServiceProvider, result, Slot, false)', 'InstantiatePrefab(result, Slot)')
    text = re.sub(r'\bObject\b', 'UnityEngine.Object', text).replace('UnityEngine.UnityEngine.Object', 'UnityEngine.Object')
    text = '\n'.join(l for l in text.splitlines() if '//IL_' not in l) + '\n'
    text = ('// Migrated original MonsterUI; see activity-monster-ui-runtime-source-manifest.json.\n'
            'using System;\nusing System.Collections.Generic;\nusing UnityEngine;\n'
            'using Zjcs.Official.ProfessionUI;\nnamespace Zjcs.Official.ActivityBattleUI\n{\n' + text + '}\n')
    output = ROOT / 'unity/Assets/Game/Runtime/Official/ActivityBattleUI/MonsterUI.cs'
    output.write_text(text, 'utf8', newline='\n')
    digest = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
    inputs = [source, base / 'common-source/Common/LanguageUtils.cs',
        base / 'framework-source/Framework/TransformExtensions.cs', Path(__file__),
        ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI/ProfessionImageResourceBridge.cs']
    receipt = dict(manifest='884_156692', inputs={p.relative_to(ROOT).as_posix(): digest(p) for p in inputs},
        outputs={p.relative_to(ROOT).as_posix(): digest(p) for p in (output, metadata(output))},
        adaptation='Original Slot, same-ID early return, descending inactive-child destruction without detach, '
            'MonsterInfo.IconId address, retained load handles until destruction and instantiated local position zero. '
            'Existing migrated resource operation contract; explicit parser and injected prefab factory delegates. '
            'Original missing config/icon logging uses Unity. Native prefab bindings are recorded separately; '
            'the production activity page remains pending.')
    (ROOT / 'assets/reference/daily-challenges/activity-monster-ui-runtime-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


if __name__ == '__main__': main()
