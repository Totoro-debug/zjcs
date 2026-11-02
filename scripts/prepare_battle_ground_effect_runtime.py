"""Port original ApplyByGroundType; isolate terrain/resource/injection services only."""
import json
import re
from pathlib import Path

from official_apk import ROOT
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed

SOURCE = ROOT / '.tools/source-packages/current/884_156692/official-code'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/StoryGroundEffects'
RECEIPT = ROOT / 'assets/reference/battle-animation/ground-effects-runtime-source-manifest.json'


def method(text, signature, body):
    start = text.index(signature)
    begin = text.index('{', start)
    end, depth = begin + 1, 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[:begin] + '{\n' + body + '\n\t}' + text[end:]


def build():
    DEST.mkdir(parents=True, exist_ok=True)
    original = SOURCE / 'client-source/ApplyByGroundType.cs'
    text = original.read_text(encoding='utf-8-sig')
    for name in ('Common', 'Framework', 'Framework.DependencyInjection', 'Framework.Logging',
                 'Framework.Logging.InterpolatedStringHandler', 'GameFramework', 'R3'):
        text = text.replace('using ' + name + ';\n', '')
    text = re.sub(r'\s*\[Inject\]\s*private [^\n]+\{ get; set; \}', '', text)
    text = re.sub(r'\t\[Tooltip\([^\n]+\)\]\n', '', text)
    text = re.sub(r'\t\t//IL_[^\n]+\n', '', text)
    text = text.replace('private GridGroundComponent mCurrentGridGroundComponent;',
        'private IPlayableGroundSurface mCurrentGridGroundComponent;\n\n\tpublic IPlayableGroundEffectHost Host { get; set; }')
    text = text.replace('private DisposableBag mDisposableBag;', 'private List<IDisposable> mDisposableBag;')
    text = text.replace('mDisposableBag = default;', 'mDisposableBag = new List<IDisposable>();')
    text = text.replace('mDisposableBag.Dispose();', 'foreach (var handle in mDisposableBag) handle.Dispose();\n\t\tmDisposableBag.Clear();')
    text = method(text, 'private void UpdateGridGroundComponent()',
        '\t\tif (Host == null) throw new InvalidOperationException("ApplyByGroundType requires an explicit ground/effect host.");\n'
        '\t\tmCurrentGridGroundComponent = Host.ResolveGround();\n'
        '\t\tif (mCurrentGridGroundComponent == null) Debug.LogError("[MyPlayable] ApplyByGroundType has no fight/map ground.", this);')
    text = method(text, 'private void UpdateGetAttachGO(',
        '\t\tresult = AttachPosCfg.GetAttachToGo(playable, mAttachType);\n'
        '\t\tif (result == null) Debug.LogError("[MyPlayable] ApplyByGroundType has no attachment object: " + mAttachType, this);')
    text = method(text, 'private GroundType GetGroundType()',
        '\t\treturn mCurrentGridGroundComponent.GetGroundType(mAttachGO.transform.localPosition);')
    text = method(text, 'private AssetGuidAndSlotProxy GetPrefabAsset(',
        '\t\tif (mGroundTypeAssetToApplyDict.TryGetValue(groundType, out var value)) return value;\n'
        '\t\tif (!mGroundTypeAssetToApplyDict.TryGetValue(GroundType.IgnoreKey, out value))\n'
        '\t\t\tDebug.LogWarning("[MyPlayable] No ground effect or IgnoreKey configuration: " + groundType, this);\n'
        '\t\treturn value;')
    text = text.replace('GameObject result = Disposable.AddTo<AssetOperationHandle<GameObject>>(ResourceLoader.LoadAssetByGuid<GameObject>(prefabAsset.AssetGuidProxy.AssetGuid), ref mDisposableBag).Result;',
        'var handle = Host.LoadAssetByGuid(prefabAsset.AssetGuidProxy.AssetGuid);\n\t\tmDisposableBag.Add(handle);\n\t\tGameObject result = handle.Result;')
    text = text.replace('ServiceProviderExtensions.Instantiate(ServiceProvider, result)', 'Host.Instantiate(result)')
    # Only the original diagnostic formatting block changes; its branch and
    # the subsequent native enable/apply operations keep their original order.
    start = text.index('\t\t\tGLoggerErrorInterpolatedStringHandler')
    end = text.index('\n\t\t}', start)
    text = text[:start] + '\t\t\tDebug.LogError("[MyPlayable] Ground effect instantiation returned null: " + groundType, this);' + text[end:]
    assert not any(x in text for x in ('[Inject]', 'GLogger', 'ServiceProviderExtensions', 'private DisposableBag', 'private GridGroundComponent '))
    header = ('// Migrated original ApplyByGroundType. Only service boundaries and diagnostics are adapted.\n'
              '// See ground-effects-runtime-source-manifest.json.\n'
              'using Object = UnityEngine.Object;\nusing System.Collections.Generic;\n'
              'using Zjcs.Official.StoryPlayables;\nusing Zjcs.Official.StoryAttachment;\n'
              'using Zjcs.Official.StoryFollowBone;\n')
    text = header + text[:text.index('public class')] + 'namespace Zjcs.Official.StoryGroundEffects\n{\n' + text[text.index('public class'):] + '\n}\n'
    write_changed(DEST / 'ApplyByGroundType.cs', text)
    copied = [(SOURCE / 'framework-firstpass-source/AssetGuidProxy.cs', 'AssetGuidProxy.cs'),
              (SOURCE / 'common-source/Common/GroundType.cs', 'GroundType.cs')]
    for source, name in copied:
        value = source.read_text(encoding='utf-8-sig').replace('namespace Common;\n', '')
        value = value.replace('where T : Object', 'where T : UnityEngine.Object')
        using = value[:value.index('[Serializable]')] if name == 'AssetGuidProxy.cs' else ''
        value = value[len(using):]
        write_changed(DEST / name, '// Migrated selected original source; see ground effects receipt.\n' + using
                      + 'namespace Zjcs.Official.StoryGroundEffects\n{\n' + value + '\n}\n')
    for p in [DEST, *sorted(DEST.glob('*'))]:
        if p.suffix != '.meta': meta(p, folder=p.is_dir())
    inputs = [original, *[p for p, _ in copied]]
    outputs = [DEST.with_name(DEST.name + '.meta'), *sorted(p for p in DEST.iterdir() if p.is_file())]
    receipt = dict(manifest='884_156692', inputs=[identity(p) for p in inputs],
        outputs=[identity(p) for p in outputs], generator=identity(Path(__file__)),
        binding='Original ApplyByGroundType retains Init/Play/Update/Stop, terrain selection/IgnoreKey fallback, unchanged-terrain suppression, native slot parenting, activation, application and handle lifetime. Explicit host replaces fight/map/loader/service-provider injection; ground lookup receives the original attachment local position.',
        adaptations=['Framework IDisposableBag replaced by equivalent per-instance IDisposable list disposed on destruction.',
            'Original diagnostic formatters replaced by Unity diagnostics; original branches remain.',
            'GroundType and AssetGuidProxy are isolated native ports, avoiding a dependency on map or profession UI assemblies.'],
        limitations=['Runtime port alone does not bind the 17 original movement follow-bone nodes or deliver their complete native effect closure.'])
    write_changed(RECEIPT, json.dumps(receipt, ensure_ascii=False, indent=2) + '\n')


if __name__ == '__main__':
    build()
