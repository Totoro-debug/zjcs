"""Port the APK's decoded LoopScrollRect assembly with C# and local DI repairs."""
import re
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map10_entities import sha256, json_bytes, write_meta


def calls(source, name, replacement):
    while name+'(' in source:
        start = source.index(name+'(')
        body = start+len(name)+1
        end, depth = body, 1
        while depth:
            depth += (source[end] == '(')-(source[end] == ')')
            end += 1
        source = source[:start]+replacement(source[body:end-1])+source[end:]
    return source


def build():
    source_root = ROOT/'.tools/source-packages/official-code/loop-scroll-full-source'
    target_root = ROOT/'unity/Assets/Game/Runtime/Official/WorldUI/LoopScroll'
    target_root.mkdir(parents=True, exist_ok=True)
    write_meta(target_root, 'DefaultImporter')
    names = ['LoopScrollRectBase','LoopScrollRect','LoopVerticalScrollRect','LoopScrollSinglePrefabSource',
             'LoopScrollPrefabSource','LoopScrollDataSource','LoopScrollSizeHelper','LoopScrollSizeUtils','LoopScrollGridLayoutGroup']
    inputs, outputs = [], []
    for name in names:
        path = source_root/('' if name == 'LoopScrollGridLayoutGroup' else 'UnityEngine.UI')/(name+'.cs')
        raw = path.read_bytes()
        source = raw.decode('utf-8-sig').replace('\r\n', '\n')
        source = re.sub(r'^\s*//IL_[^\n]*\n', '', source, flags=re.M)
        source = source.replace('namespace UnityEngine.UI;', 'namespace UnityEngine.UI\n{')
        if name != 'LoopScrollGridLayoutGroup': source += '\n}\n'
        # The decoded metadata only contains engine declarations, which makes
        # ILSpy spell base calls and C# conversion operators as ordinary calls.
        source = re.sub(r'\(\((?:UIBehaviour|LayoutGroup|GridLayoutGroup)\)this\)\.(Awake|OnEnable|OnDisable|OnDestroy|OnRectTransformDimensionsChange|CalculateLayoutInputHorizontal|CalculateLayoutInputVertical|SetLayoutHorizontal|SetLayoutVertical)\(',r'base.\1(',source)
        # This override uses IL `call UIBehaviour.IsActive`; callers use
        # `callvirt` and must retain the content-aware override.
        source = source.replace('public override bool IsActive()\n\t{\n\t\tif (((UIBehaviour)this).IsActive())',
                                'public override bool IsActive()\n\t{\n\t\tif (base.IsActive())')
        source = calls(source, 'Object.op_Implicit', lambda value:'(bool)('+value+')')
        source = calls(source, 'Vector2.op_Implicit', lambda value:'(Vector2)('+value+')')
        source = source.replace(', ref val)', ', out val)').replace(', ref val2)', ', out val2)')
        source = source.replace('((LayoutGroup)this).rectTransform', 'base.rectTransform')
        source = source.replace('TryGetComponent<HorizontalOrVerticalLayoutGroup>(ref val2)', 'TryGetComponent<HorizontalOrVerticalLayoutGroup>(out val2)')
        source = source.replace('eventData.pressEventCamera, ref m_PointerStartLocalCursor)', 'eventData.pressEventCamera, out m_PointerStartLocalCursor)')
        source = source.replace('reference = ref val2;\n\t\t\t\t\tnum5 = i;\n\t\t\t\t\treference[num5] +=', 'num5 = i;\n\t\t\t\t\tval2[num5] +=')
        if name in ('LoopScrollRect', 'LoopScrollSinglePrefabSource'):
            source = source.replace('using Framework.DependencyInjection;','')
            source = source.replace('\t[Inject]\n\tprivate IServiceProvider ServiceProvider { get; set; }','')
            source = source.replace('DefaultPrefabSource.Init(((Component)this).transform, ServiceProvider);','DefaultPrefabSource.Init(((Component)this).transform);')
            source = source.replace('Init(Transform root, IServiceProvider serviceProvider)','Init(Transform root)')
            source = source.replace('\tprivate IServiceProvider mServiceProvider;','').replace('\t\tmServiceProvider = serviceProvider;','')
            source = source.replace('ServiceProviderExtensions.Instantiate(mServiceProvider, Prefab, mPoolRoot, false)', 'Object.Instantiate(Prefab, mPoolRoot, false)')
        target = target_root/(name+'.cs')
        target.write_text(source, encoding='utf-8', newline='\n')
        write_meta(target, 'MonoImporter')
        inputs.append(dict(path=path.relative_to(ROOT).as_posix(),sha256=sha256(raw)))
        outputs.append(dict(path=target.relative_to(ROOT).as_posix(),sha256=sha256(target.read_bytes())))
    assembly = ROOT/'.tools/source-packages/official-code/managed/decoded/LoopScrollRect.Runtime.dll'
    receipt = dict(manifest='854_156383', assembly=dict(path=assembly.relative_to(ROOT).as_posix(),sha256=sha256(assembly.read_bytes())),
        inputs=inputs, outputs=outputs, operation='Retain original scroll layout, bounds, velocity, drag, virtualization and pool order; repair decompiler base calls/conversions and use Unity Instantiate instead of offline-unavailable service-provider injection.',
        converter=dict(path=Path(__file__).relative_to(ROOT).as_posix(),sha256=sha256(Path(__file__).read_bytes())))
    (ROOT/'assets/reference/official-map/loop-scroll-source-manifest.json').write_bytes(json_bytes(receipt))
    print('Original loop scroll sources:',len(outputs))


if __name__ == '__main__':
    build()
