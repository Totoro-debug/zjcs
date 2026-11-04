"""Restore the original world popup blur mask for the shared native minimap."""
import copy
import hashlib
import json
from pathlib import Path
import re

import yaml
from prepare_current_partner_native import PartnerNativeRecovery, documents, identity, script_id
from prepare_dungeon_ui_bindings import relative_transform_path
from prepare_unity_map13_minimap_mark_core import method
from prepare_unity_map13_native_sprite_yaml import Dumper
from prepare_unity_map13_sources import ROOT, meta, write
from validation_workspace import validation_directory

CODE = ROOT / '.tools/source-packages/current/884_156692/official-code/framework-firstpass-source'
RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Official/World/Map13MiniMapWindows'
DEST = ROOT / 'unity/Assets/Official/World/Shared/NativeMiniMapWindows/Resources/MainlineMiniMap'
RECEIPT = ROOT / 'assets/reference/official-map/mainline-minimap-window-masks-source-manifest.json'
NS = 'Zjcs.Official.World.Map13MiniMapWindows'


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def build():
    if not (validation_directory() / '.zjcs-validation.json').is_file():
        raise ValueError('Use a named owned validation workspace for original mask recovery')
    inputs, outputs = [], []

    def read(name):
        path = CODE / (name + '.cs')
        inputs.append(record(path))
        return path.read_text('utf8').replace('\r\n', '\n')

    def emit(name, text):
        # Only injection/logging infrastructure and the explicit offline access
        # seam change. Original fields, component binding and operations remain.
        text = re.sub(r'^using (Framework(?:\.[\w.]+)?|GFrameCore[^;]*|Microsoft.Extensions.Logging|ZLogger);\n', '', text, flags=re.M)
        at = re.search(r'(?m)^(?:\[|public )', text).start()
        text = text[:at] + 'namespace ' + NS + '\n{\nusing Object=UnityEngine.Object;\nusing Debug=UnityEngine.Debug;\n' + text[at:] + '\n}\n'
        path = RUNTIME / (name + '.cs')
        write(path, text.encode())
        meta(path, 'MonoImporter')
        outputs.extend([record(path), record(Path(str(path) + '.meta'))])

    text = read('WindowMaskBase')
    text = re.sub(r'\t\[Inject\]\n\tprivate ILogger<WindowMaskBase> Logger \{ get; set; \}\n', '', text)
    text = re.sub(r'\t\t\tILogger logger =.*?GZLoggerExtensions.Error\([^\n]*\);', '\t\t\tDebug.LogError("AttachToWindow -> window is null");', text, flags=re.S)
    text = text.replace('internal abstract UniTask Prepare', 'public abstract UniTask Prepare')
    for name in ('AttachToWindow', 'UnattachToWindow'):
        text = text.replace('internal void ' + name, 'public void ' + name)
    text = text.replace('internal virtual void Refresh', 'public virtual void Refresh')
    emit('WindowMaskBase', text)
    for name in ('IBlurManager', 'WindowMaskEffectBase', 'WindowMaskEffectColor'):
        emit(name, read(name))
    text = read('WindowBlurMask')
    text = text[:text.index('\t[StructLayout')] + text[text.index('\t[SerializeField]'):]
    text = text.replace('\t[Inject]\n\tprivate IBlurManager BlurManager { get; set; }', '\tpublic IBlurManager BlurManager { private get; set; }')
    text = re.sub(r'\n\t\[AsyncStateMachine\([^\n]+\)\]\n', '\n', text)
    text = text.replace(method(text, 'internal override UniTask Prepare(PopupWindow window, bool reopenWindowInTop, CancellationToken token)'), '''public override async UniTask Prepare(PopupWindow window, bool reopenWindowInTop, CancellationToken token)
    {
        if (!mWindow2RTDLookup.TryGetValue(window, out var value))
        {
            value = BlurManager.GetRenderTexture();
            mWindow2RTDLookup.Add(window, value);
        }
        if (!reopenWindowInTop)
        {
            BlurManager.TakeAShot(value);
            await UniTask.NextFrame(token, false);
        }
        else Graphics.Blit(mImage.texture, value);
    }''')
    start = method(text, 'private void ExpandImageToFullCanvas()')
    replacement = re.sub(r'\t\t\tGLoggerErrorInterpolatedStringHandler message =.*?Log.Error\([^\n]*\);', '\t\t\tDebug.LogError("WindowBlurMask ExpandToFullScreen failed! Canvas or rootCanvas is null! Canvas == null:" + (Canvas == null));', start, flags=re.S)
    text = text.replace(start, replacement).replace('Remove(mExpiredWindow, ref rt)', 'Remove(mExpiredWindow, out rt)')
    emit('WindowBlurMask', text)

    recovery = PartnerNativeRecovery()
    if recovery.apk.version != '884_156692':
        raise ValueError('The current original world mask source version changed')
    bundle = 'defaultpackage_assets_prefabs_ui_map_world.bundle'
    original_prefab = recovery.apk.asset('prefabs_ui_map_world', '/WorldCanvas.prefab')
    native = recovery.native / 'Prefabs/UI/Map/World/WorldCanvas.prefab'
    native_docs = documents(native.read_text('utf8'))
    choices = []
    for go, _, names, _ in relative_transform_path(original_prefab.read()):
        if '/'.join(names[1:]) != 'Content/Popup/BlurMask':
            continue
        for pair in go.m_Component:
            obj = pair.component.deref()
            if obj.type.name != 'MonoBehaviour':
                continue
            script = obj.read(check_read=False).m_Script.read()
            if script.m_ClassName != 'WindowBlurMask':
                continue
            fields = recovery.fields(obj)
            matches = [doc['MonoBehaviour'] for doc in native_docs.values() if 'MonoBehaviour' in doc
                       and doc['MonoBehaviour']['m_Script']['fileID'] == script_id(script)
                       and doc['MonoBehaviour'].get('mMaskType') == fields['mMaskType']]
            if len(matches) != 1 or fields['mMaskType'] != 3:
                raise ValueError('Original world blur mask identity is ambiguous')
            recovery.recover('BlurMask', validation_directory() / 'world-mask-recovery', bundle=bundle,
                             native_relative='Prefabs/UI/Map/World/WorldCanvas.prefab', source_root=go.object_reader,
                             native_root_id=matches[0]['m_GameObject']['fileID'])
            choices.append(dict(source=recovery.apk.provenance(obj), original=identity(obj), fields=fields))
    if len(choices) != 1:
        raise ValueError('Exactly one original world popup blur mask is required')
    recovery.finalize()
    _, _, panel, source = recovery.outputs[0]
    if panel['external'] or panel.get('unresolved') or len(panel['documents']) != 8:
        raise ValueError('The complete original blur mask must have eight closed native objects')
    docs = copy.deepcopy(panel['documents'])
    bindings = []
    for script in panel['scripts']:
        name = script['type'].rsplit('.', 1)[-1]
        if name == 'WindowBlurMask':
            path = RUNTIME / 'WindowBlurMask.cs'
        else:
            if name not in ('Button', 'RawImage', 'GraphicRaycaster'):
                raise ValueError('Unbound original blur mask script: ' + name)
            relative = 'Runtime/UI/Core/' + name + '.cs'
            candidates = list((ROOT / 'unity/Packages').glob('com.unity.ugui*/' + relative))
            if not candidates:
                candidates = list((ROOT / 'unity/Library/PackageCache').glob('com.unity.ugui*/' + relative))
            path, = candidates
        metadata = Path(str(path) + '.meta')
        guid = re.search(r'(?m)^guid: ([a-f0-9]{32})$', metadata.read_text('utf8'))[1]
        fields = docs[script['nativeFileID']]['MonoBehaviour']
        fields['m_Script'] = dict(fileID=11500000, guid=guid, type=3)
        # Unity's Rect YAML needs its serialization version to retain width/height.
        if name == 'RawImage':
            fields['m_UVRect']['serializedVersion'] = 2
        bindings.append(dict(original=script['original'], type=script['type'], script=record(path), metadata=record(metadata)))
    class_ids = {'GameObject': 1, 'RectTransform': 224, 'Canvas': 223, 'CanvasRenderer': 222, 'MonoBehaviour': 114}
    text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
    for file_id, doc in docs.items():
        text += '--- !u!' + str(class_ids[next(iter(doc))]) + ' &' + str(file_id) + '\n'
        text += yaml.dump(doc, Dumper=Dumper, sort_keys=False, allow_unicode=True)
    target = DEST / 'BlurMask.prefab'
    write(target, text.encode())
    meta(target, 'PrefabImporter')
    for folder in (DEST, DEST.parent, DEST.parent.parent):
        meta(folder, folder=True)
        outputs.append(record(Path(str(folder) + '.meta')))
    outputs.extend([record(target), record(Path(str(target) + '.meta'))])
    cache = ROOT / recovery.proof['component_cache']['path']
    inputs.extend([record(native), record(cache), record(ROOT / 'scripts/prepare_current_partner_native.py')])
    blur_receipt = ROOT / 'assets/reference/partner-friendship-window-source-manifest.json'
    blur = json.loads(blur_receipt.read_bytes())
    recovery.apk.load_builtin('assets/bin/Data/data.unity3d')
    shader = recovery.apk.env.get_cab('unity_builtin_extra').objects[4800000]
    if hashlib.sha256(shader.get_raw_data()).hexdigest() != blur['screen_blur']['original_sha256']:
        raise ValueError('The reused current native blur shader has a different original source')
    previous = json.loads((ROOT / 'assets/reference/official-map/popup-effects-source-manifest.json').read_bytes())
    for name in ('ScreenBlurPass', 'BlurManager'):
        current = record(CODE / (name + '.cs'))
        expected = next(row['sha256'] for row in previous['officialCode'] if row['path'].endswith('/' + name + '.cs'))
        if current['sha256'] != expected:
            raise ValueError('The shared capture pass must be updated for the current original ' + name)
        inputs.append(current)
    result = dict(format=1, manifest=recovery.apk.version, official_code=inputs, outputs=outputs,
                  selection=choices[0], prefab_source=recovery.apk.provenance(original_prefab),
                  original_objects=panel['objectMap'], original_components=panel['scripts'], bindings=bindings,
                  root_file_id=next(file_id for file_id, doc in docs.items() if 'GameObject' in doc), resource='MainlineMiniMap/BlurMask',
                  blur_capture=dict(shader_source=recovery.apk.provenance(shader), object_sha256=blur['screen_blur']['original_sha256'],
                                    reused_shader_receipt=dict(path=blur_receipt.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(blur_receipt.read_bytes().replace(b'\r\n', b'\n')).hexdigest(), hashLineEndings='LF'),
                                    pass_equivalence='Current original ScreenBlurPass and BlurManager decompiled files match the shared desktop capture implementation source receipt byte for byte. The current shader is independently restored; its full original object differs from the historical shader.'),
                  generator=record(Path(__file__).resolve()),
                  runtime=[dict(path=path.relative_to(ROOT).as_posix(), hashLineEndings='LF', sha256=hashlib.sha256(path.read_bytes().replace(b'\r\n', b'\n')).hexdigest()) for path in (
                      ROOT / 'unity/Assets/Game/Runtime/Offline/World/MainlineMiniMap/MainlineMiniMapMarkerCatalog.cs',
                      ROOT / 'unity/Assets/Game/Editor/Map13MiniMap/OfficialMap13MiniMapMarkDialogsAcceptance.cs',
                      ROOT / 'unity/Assets/Game/Editor/Map13MiniMap/Zjcs.Editor.Map13.MiniMap.asmdef',
                      ROOT / 'unity/Assets/Game/Runtime/Presentation/World/OfficialDungeonPopupMask.cs')],
                  operation='Restore the complete original world popup BlurMask subtree. Preserve original hierarchy, field values and local object IDs; detach its parent, bind actual package scripts and the migrated original mask. Recover the exact NextFrame/capture/reopen state machine; expose original injection and internal host methods explicitly. Replace logging backend and repair decompiled Dictionary.Remove out parameter only.',
                  limitations=['The original popup host and capture adapter still need runtime integration and validation.'])
    write(RECEIPT, (json.dumps(result, ensure_ascii=False, indent=2) + '\n').encode())
    recovery.apk.archive.close()
    print('Original shared minimap BlurMask: eight native objects and original mask lifecycle')


if __name__ == '__main__':
    build()
