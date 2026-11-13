"""Migrate the selected original story mask implementation; keep its geometry and ordering."""
import hashlib
import json
import re
from pathlib import Path

from official_apk import ROOT

DEST = ROOT / 'unity/Assets/Game/Runtime/Official/StoryMasks'


def body_span(text, signature):
    start = text.index(signature)
    opening = text.index('{', start)
    depth = 1
    end = opening + 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return start, opening, end


def replace_body(text, signature, body):
    _, opening, end = body_span(text, signature)
    return text[:opening] + '{\n' + body + '\n\t}' + text[end:]


def strip_logs(text):
    text = re.sub(r'^\s*//IL_[^\n]*\n', '\n', text, flags=re.M)
    # Logging interpolated handlers have no gameplay effects. Remove only these
    # exact generated declarations, their enabled blocks, and final log calls.
    text = re.sub(r'^\s*GLogger\w+InterpolatedStringHandler \w+ = [^\n]+;\n', '\n', text, flags=re.M)
    while re.search(r'\bif \(enabled\)', text):
        start, _, end = body_span(text, 'if (enabled)')
        text = text[:start] + text[end:]
    text = re.sub(r'^\s*Log\.(Debug|Error|Warn)\([^\n]+;\n', '\n', text, flags=re.M)
    text = re.sub(r'^using (Framework(?:\.[\w.]+)?|Common);\n', '', text, flags=re.M)
    text = re.sub(r'^\s*\[Inject\]\n', '\n', text, flags=re.M)
    text = text.replace('GAction', 'Action')
    text = text.replace('Vector2.op_Implicit(mUIScale)', '(Vector2)mUIScale')
    text = text.replace('Vector2.op_Implicit(setting.Scale)', '(Vector2)setting.Scale')
    text = text.replace('Vector2.op_Implicit(mTmpScale)', '(Vector2)mTmpScale')
    text = re.sub(r'\b(Vector2|Vector3|Vector4|Color32|Object)\.op_Implicit', '', text)
    return text


def main():
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if version != '884_156692':
        raise ValueError('Re-verify story mask sources for the selected version')
    source_root = ROOT / '.tools/source-packages/official-code/client-source'
    inputs, outputs = {}, {}
    DEST.mkdir(parents=True, exist_ok=True)
    for name in ('StoryMask', 'StoryMaskGraphic', 'ProcessGuideHint', 'MaskSetting',
                 'AttachToObjectLocalUI', 'IStoryMaskManager', 'StoryMaskManager',
                 'UIBloomBuiltGroupPriority', 'UIBloomBuiltInGroupRegister'):
        source = source_root / (name + '.cs')
        raw = source.read_bytes()
        selected = ROOT / '.tools/source-packages/current' / version / 'official-code/client-source' / source.name
        if raw != selected.read_bytes():
            raise ValueError('Selected original mask source differs: ' + name)
        inputs[source.relative_to(ROOT).as_posix()] = hashlib.sha256(raw).hexdigest()
        text = strip_logs(raw.decode('utf-8-sig').replace('\r\n', '\n'))
        text = text.replace('UIUtils.GetUiCamera()', 'StoryMaskProjection.GetUiCamera(this)')
        text = text.replace('UIUtils.', 'StoryMaskProjection.')
        text = text.replace('uiCamera, ref val2)', 'uiCamera, out val2)')
        if name == 'StoryMask':
            text = re.sub(r'\s*public static HashSet<WindowType>[^;]+;', '', text)
            text = text.replace('UnsafeAssetPrefabProxy', 'StoryMaskPrefabProxy')
            text = text.replace('LanguageUtils.GetStoryTipKey(setting.TipUIKeyId)', '"story_tip_" + setting.TipUIKeyId')
            text = text.replace('WindowManager.Instance', 'Host')
            text = replace_body(text, 'public bool CanShowMask()', '''\t\tGameObject lastWindow = Host.LastNonTipWindow;
\t\tbool hasWindow = lastWindow != null;
\t\tif (AttachToObjectLocalUI.AttachObject)
\t\t{
\t\t\tif (!hasWindow) return true;
\t\t\treturn AttachToObjectLocalUI.AttachObject.transform.IsChildOf(lastWindow.transform);
\t\t}
\t\tif (AttachToObjUI.AttachObject) return !hasWindow;
\t\treturn true;''')
            text = text.replace('public class StoryMask : MonoBehaviour\n{', '''public class StoryMask : MonoBehaviour
{
    public IStoryMaskHost Host { get; private set; }
    private IStoryMaskManager boundManager;
    public void Bind(IStoryMaskHost host, IStoryMaskManager manager)
    {
        if (gameObject.activeInHierarchy) throw new InvalidOperationException("Bind story mask before enabling it.");
        Host = host;
        boundManager = manager;
        MaskAssetProxy.LoadPrefab = host.LoadPrefab;
        MaskAssetProxy.Initialize = go => go.GetComponent<StoryMaskGraphic>().Bind(manager);
        mAttachToObjectLocalUIRect = AttachToObjectLocalUI.GetComponent<RectTransform>();
    }''')
            # The original destroys only the Graphic component. Its native children
            # and cloned target area otherwise survive cancellation/map unload.
            text = text.replace('Object.DestroyImmediate((Object)(object)mMaskGraphic);',
                                'mMaskGraphic.Close();\n\t\t\tObject.Destroy(mMaskGraphic.gameObject);\n\t\t\tmMaskGraphic = null;')
            text = text.replace('IsPlayingSlotMask = false;', 'mUITargetRect = null;\n\t\tIsPlayingSlotMask = false;')
            text = text.replace('private void OnDisable()\n\t{', 'private void OnDisable()\n\t{\n\t\tboundManager?.Reset();')
        elif name == 'StoryMaskGraphic':
            text = text.replace('private IStoryMaskManager MaskManager { get; set; }',
                                'private IStoryMaskManager MaskManager { get; set; }\n\tpublic void Bind(IStoryMaskManager manager) => MaskManager = manager;')
        elif name == 'StoryMaskManager':
            text = text.replace(' : BackgroundServiceBase, IStoryMaskManager', ' : IStoryMaskManager')
            start = text.index('\n\tprivate IServiceProvider')
            end = text.index('\n\tpublic bool EnableInvisibleMask', start)
            text = text[:start] + text[end:]
            start, _, end = body_span(text, 'protected override void Initialize()')
            text = text[:start] + '''public StoryMaskManager(StoryMask view, IStoryMaskHost host)
    {
        mStoryMask = view;
        view.Bind(host, this);
    }''' + text[end:]
            text = replace_body(text, 'protected override void Dispose()', '\t\tReset();')
            text = text.replace('protected override void Dispose()', 'public void Dispose()')
        elif name == 'UIBloomBuiltInGroupRegister':
            text = text.replace('public IUIBloomManager UIBloomManager { get; set; }',
                                'public IStoryMaskBloomRegistry UIBloomManager { get; set; }')
            text = text.replace('UIBloomManager.RegisterBuiltInGroup', 'UIBloomManager?.RegisterBuiltInGroup')
            text = text.replace('UIBloomManager.UnregisterBuiltInGroup', 'UIBloomManager?.UnregisterBuiltInGroup')
        text = re.sub(r'\n(?:[ \t]*\n){2,}', '\n\n', text)
        text = ('// Migrated selected original; see story-mask-runtime-source-manifest.json.\n'
                'using System;\nusing Zjcs.Official.StoryBubbles;\n'
                'using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;\n'
                'namespace Zjcs.Official.StoryMasks\n{\nusing Object = UnityEngine.Object;\n' + text + '\n}\n')
        target = DEST / source.name
        target.write_text(text, 'utf8', newline='\n')
        meta = Path(str(target) + '.meta')
        if not meta.exists():
            meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs:story-mask:' + name).encode()).hexdigest()
                            + '\nMonoImporter:\n  externalObjects: {}\n', 'utf8')
        for path in (target, meta):
            outputs[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    for relative in ('client-source/UIUtils.cs', 'client-source/EUIInstantiateFlag.cs',
                     'common-source/Common/LanguageUtils.cs', 'framework-firstpass-source/WindowType.cs',
                     'framework-firstpass-source/Framework.UI/WindowContainer.cs',
                     'framework-source/PrefabAssetProxyBase.cs', 'framework-source/UnsafeAssetPrefabProxy.cs'):
        source = source_root.parent / relative
        selected = ROOT / '.tools/source-packages/current' / version / 'official-code' / relative
        if source.read_bytes() != selected.read_bytes():
            raise ValueError('Selected mask support source differs: ' + relative)
        inputs[source.relative_to(ROOT).as_posix()] = hashlib.sha256(source.read_bytes()).hexdigest()
    for path in (DEST / 'StoryMaskHost.cs', DEST / 'Zjcs.Official.StoryMasks.asmdef'):
        outputs[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    receipt = dict(manifest=version, inputs=inputs, outputs=outputs,
        generator_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(), adaptations=[
            'Keep native serialized fields, rounded hole mesh, target copy, hint layout and mask manager execution order.',
            'Replace DI, logging and asset/window services with explicit host bindings; namespace and implicit cast repairs only.',
            'Release the whole instantiated guide graphic and its target clone on disable; clear tracked target on close.',
            'Bloom registration uses an explicit registry; full native bloom rendering remains a separate host dependency.'
        ])
    (ROOT / 'assets/reference/official-map/story-mask-runtime-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8')
    print('Migrated original story mask sources:', len(outputs) // 2)


if __name__ == '__main__':
    main()
