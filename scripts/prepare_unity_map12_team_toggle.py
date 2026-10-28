"""Bind the six original invitation-permission nodes without redesigning their UI."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import struct

from PIL import Image
import xxhash
from official_apk import Apk, ROOT
from prepare_unity_bundle_inputs import collect, serialized_files
from prepare_unity_map12_sources import encode, sha, source_path
from prepare_unity_map12_team_sources import BUNDLE, OUTPUT as ORIGINAL
from official_native_yaml_verification import documents
from map13_native_schemas import NativeSchemas
from map12_team_native_schemas import install

NATIVE = ROOT / 'unity/Assets/Official/World/Map/12/Source/native-team-ui-prefabs.json'
DEST = ROOT / 'unity/Assets/Game/Resources/Map12/Team'
RECEIPT = ROOT / 'assets/reference/official-map/map12-team-toggle-source-manifest.json'


def guid(path):
    return re.search(r'(?m)^guid: ([a-f0-9]{32})$', path.read_text(encoding='utf8'))[1]


def build(write=False):
    original, native = json.loads(ORIGINAL.read_bytes()), json.loads(NATIVE.read_bytes())
    if original['manifest'] != '871_156598' or native['manifest'] != original['manifest']:
        raise ValueError('Original team permission source version differs')
    rows = {r['identity']: r for r in original['objects']}
    binding = {int(r['fileId']): rows[r['original']] for r in native['bindings'] if r['panel'] == 'TeamPanel'}
    source, = [r for r in original['objects'] if r.get('scriptType') == 'AllowMemberInviteToggle']
    component, = [k for k, r in binding.items() if r['identity'] == source['identity']]
    panel, = [p for p in native['panels'] if p['name'] == 'TeamPanel']
    docs = documents(panel['yaml'])
    root = docs[component][1]['m_GameObject']['fileID']
    selected, transforms = set(), {}

    def walk(go):
        selected.add(go)
        transform = None
        for item in docs[go][1]['m_Component']:
            key = item['component']['fileID']
            selected.add(key)
            if docs[key][0] in ('Transform', 'RectTransform'):
                transform = key
        transforms[go] = transform
        for child in docs[transform][1]['m_Children']:
            walk(docs[child['fileID']][1]['m_GameObject']['fileID'])

    walk(root)
    if len(transforms) != 6 or len(selected) != 28:
        raise ValueError('Recheck the exact original invitation permission subtree')
    ugui = ROOT / 'unity/Library/PackageCache/com.unity.ugui@1.0.0/Runtime/UI/Core'
    scripts = {
        'AllowMemberInviteToggle': ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/WorldAllowMemberInviteToggle.cs',
        'MultiLanguageText': ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/WorldMultiLanguageText.cs',
        'UIClickSound': ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/WorldUiClickSound.cs',
        'NoDrawingRayCast': ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/NoDrawingRayCast.cs',
        'UIToggleObject': ROOT / 'unity/Assets/Game/Runtime/Official/SkillsUI/UIToggleObject.cs',
        'UnityEngine.UI.Toggle': ugui / 'Toggle.cs',
        'UnityEngine.UI.Image': ugui / 'Image.cs',
        'UnityEngine.UI.LayoutElement': ugui / 'Layout/LayoutElement.cs',
        'TMPro.TextMeshProUGUI': ROOT / 'unity/Library/PackageCache/com.unity.textmeshpro@3.0.6/Scripts/Runtime/TextMeshProUGUI.cs',
    }
    apk = Apk.current()
    _, missing = collect(apk, [BUNDLE])
    if missing:
        raise ValueError('Resolve original permission widget dependencies: ' + str(missing))
    objects = {f.name + ':' + str(o.path_id): o for b in apk.loaded.values()
               for f in serialized_files(b) for o in f.objects.values()}
    references = {r['identity']: r for r in native['externalReferences']}

    def external(pointer):
        candidates = [r for r in references if r.rsplit(':', 1)[1] == str(pointer['m_PathID'])]
        if len(candidates) != 1:
            raise ValueError('Original permission pointer identity is ambiguous')
        key = candidates[0]
        obj = objects[key]
        if sha(obj.get_raw_data()) != references[key]['payload_sha256']:
            raise ValueError('Original permission dependency object changed: ' + key)
        return obj, references[key]

    ui_path = ROOT / 'unity/Assets/Official/World/Map/12/UI/map12-ui.json'
    ui = json.loads(ui_path.read_bytes())
    geometry_path = ROOT / 'unity/Assets/Official/World/Map/12/Source/team-target-sprite-geometry.json'
    geometry = {row['identity']: row for row in json.loads(geometry_path.read_bytes())['sprites']}
    font_receipt_path = ROOT / 'assets/reference/official-map/map12-team-chinese-font-source-manifest.json'
    font_receipt = json.loads(font_receipt_path.read_bytes())
    for r in font_receipt['outputs']:
        if sha((ROOT / r['path']).read_bytes()) != r['sha256']:
            raise ValueError('Original team font binding differs')
    font_path = ROOT / 'unity/Assets/Official/World/Map/12/UI/Team/FZY4K-Dynamic.asset'
    mat_path = font_path.with_suffix('.mat')
    redirects, dependencies, audio = {}, [], None
    for key in sorted(selected):
        if docs[key][0] != 'MonoBehaviour':
            continue
        row, fields = binding[key], docs[key][1]
        script = scripts[row['scriptType']]
        redirects[fields['m_Script']['guid']] = guid(script.with_name(script.name + '.meta'))
        if row['scriptType'] == 'UnityEngine.UI.Image':
            obj, reference = external(row['fields']['m_Sprite'])
            sprite = obj.read()
            name = reference['convertedPath'].rsplit('/', 1)[-1].removesuffix('.asset')
            entry, = [r for r in ui['sprites'] if r['file'].startswith(name + '-')]
            path = ROOT / 'unity' / entry['unityAssetPath']
            actual = Image.open(path).convert('RGBA')
            expected = sprite.image.convert('RGBA')
            if sha(path.read_bytes()) != entry['sha256'] or actual.size != expected.size or actual.tobytes() != expected.tobytes():
                raise ValueError('Original permission sprite pixels differ')
            border = [sprite.m_Border.x, sprite.m_Border.y, sprite.m_Border.z, sprite.m_Border.w]
            if entry['pixelsPerUnit'] != sprite.m_PixelsToUnits or entry['border'] != border:
                raise ValueError('Original permission sprite dimensions/settings differ')
            if entry['pivot'] != [sprite.m_Pivot.x * expected.width, sprite.m_Pivot.y * expected.height]:
                raise ValueError('Original permission sprite pivot differs')
            native_path = ROOT / 'unity' / geometry[reference['identity']]['path']
            native_guid = guid(native_path.with_name(native_path.name + '.meta'))
            if native_guid != geometry[reference['identity']]['guid']:
                raise ValueError('Original permission Sprite mesh binding differs')
            redirects[fields['m_Sprite']['guid']] = native_guid
            dependencies.append(dict(identity=reference['identity'], path=source_path(path), sha256=entry['sha256']))
            dependencies.append(dict(identity=reference['identity'], path=source_path(native_path),
                                     sha256=sha(native_path.read_bytes()), nativeGeometry=True))
        elif row['scriptType'] == 'TMPro.TextMeshProUGUI':
            for field, target, kind in (('m_fontAsset', font_path, 'font'), ('m_sharedMaterial', mat_path, 'material')):
                obj, reference = external(row['fields'][field])
                index = 0 if kind == 'font' else 1
                if font_receipt['inputs'][index]['object_sha256'] != sha(obj.get_raw_data()):
                    raise ValueError('Original serialized permission font/material differs')
                redirects[fields[field]['guid']] = guid(target.with_name(target.name + '.meta'))
        elif row['scriptType'] == 'UIClickSound':
            obj, reference = external(row['fields']['AkEvent']['WwiseObjectReference'])
            schemas = NativeSchemas(ROOT / '.tools/source-packages/current/871_156598/official-code')
            install(schemas, ROOT / '.tools/source-packages/current/871_156598/official-code')
            event, _ = schemas.read(obj)
            receipt = json.loads((ROOT / 'assets/reference/official-map/map12-ui-audio-source-manifest.json').read_bytes())
            audio, = [r for r in receipt['events'] if r['id'] == event['id'] and r['name'] == event['objectName']]
            audio_path = ROOT / audio['path']
            if sha(audio_path.read_bytes()) != audio['sha256']:
                raise ValueError('Original permission click PCM differs')
            dependencies.append(dict(identity=reference['identity'], path=source_path(audio_path), sha256=audio['sha256']))
    blocks = re.split(r'(?m)(?=^--- !u!\d+ &-?\d+\n)', panel['yaml'])
    header, result = blocks[0], []
    for block in blocks[1:]:
        match = re.match(r'--- !u!\d+ &(-?\d+)\n', block)
        key = int(match[1])
        if key not in selected:
            continue
        block = re.sub(r'guid: ([a-f0-9]{32})', lambda m: 'guid: ' + redirects.get(m[1], m[1]), block)
        if key == transforms[root]:
            block, count = re.subn(r'(?m)^  m_Father: \{fileID: -?\d+\}$', '  m_Father: {fileID: 0}', block)
            if count != 1:
                raise ValueError('Original permission root parent binding differs')
        if key in binding and binding[key].get('scriptType') == 'UIClickSound':
            if audio is None:
                raise ValueError('Original permission click binding is missing')
            audio_path = ROOT / audio['path']
            block, count = re.subn(r'(?m)^  AkEvent:\n(?:    .*\n)+',
                '  AkEvent: {fileID: 8300000, guid: ' + guid(audio_path.with_name(audio_path.name + '.meta')) + ', type: 3}\n', block)
            if count != 1:
                raise ValueError('Original permission click field shape differs')
        result.append(block)
    prefab = (header + ''.join(result)).encode()
    restored = documents(prefab.decode())
    if set(restored) != selected:
        raise ValueError('Original permission node/component coverage differs after binding')
    known = set(redirects.values())
    known.add(guid((ROOT / audio['path']).with_name(Path(audio['path']).name + '.meta')))
    for match in re.finditer(r'guid: ([a-f0-9]{32})', prefab.decode()):
        if match[1] not in known:
            raise ValueError('Permission widget still contains an unbound native dependency: ' + match[1])
    key = 'ui_team_allowmemberinvite_text'
    language_path = ROOT / 'assets/reference/official-map/localization-source-manifest.json'
    language_inputs = json.loads(language_path.read_bytes())['inputs']
    language, text = [], None
    wanted = xxhash.xxh64_intdigest(key.encode())
    for r in language_inputs.values():
        raw = apk.archive.read(r['source']['path'])
        if sha(raw) != r['sha256']:
            raise ValueError('Original selected permission language input differs')
        language.append(r)
        count, = struct.unpack_from('<i', raw)
        start = 4 + count * 12
        for i in range(count):
            identity, offset = struct.unpack_from('<Qi', raw, 4 + i * 12)
            if identity == wanted and text is None:
                size, = struct.unpack_from('<H', raw, start + offset)
                text = raw[start + offset + 2:start + offset + 2 + size].decode('utf8')
    if text is None:
        raise ValueError('Original permission localization is missing')
    # Keep the real parent's two layout stages for the isolated widget fixture.
    # This is source data for tests, not a replacement TeamPanel prefab.
    layout_nodes = []
    parent = docs[transforms[root]][1]['m_Father']['fileID']
    allowed_layouts = {'UGUIJszx.VerticalLayoutGroupJSZX', 'UnityEngine.UI.VerticalLayoutGroup',
                       'UnityEngine.UI.ContentSizeFitter', 'UnityEngine.UI.LayoutElement'}
    headers = {'m_ObjectHideFlags', 'm_CorrespondingSourceObject', 'm_PrefabInstance', 'm_PrefabAsset',
               'm_GameObject', 'm_Script', 'm_EditorHideFlags', 'm_Name', 'm_EditorClassIdentifier'}
    for _ in range(2):
        transform = docs[parent][1]
        go = docs[transform['m_GameObject']['fileID']][1]
        rect = {k: v for k, v in transform.items() if k not in headers | {'m_Children', 'm_Father', 'm_RootOrder'}}
        components = []
        for item in go['m_Component']:
            layout_key = item['component']['fileID']
            script = binding.get(layout_key, {}).get('scriptType')
            if script in allowed_layouts:
                fields = {k: v for k, v in docs[layout_key][1].items() if k not in headers}
                components.append(dict(type=script, fields=json.dumps(fields, ensure_ascii=False)))
        layout_nodes.append(dict(name=go['m_Name'], rect=json.dumps(rect), components=components))
        parent = transform['m_Father']['fileID']
    if [r['name'] for r in layout_nodes] != ['Content', 'BgDialog1']:
        raise ValueError('Original permission parent layout chain differs')
    layout_path = ROOT / 'unity/Assets/Official/World/Map/12/Source/team-toggle-layout-context.json'
    outputs = {DEST / 'AllowMemberInviteToggle.prefab': prefab,
               DEST / 'texts.json': encode(dict(manifest=apk.version, texts=[dict(key=key, value=text)])),
               layout_path: encode(dict(manifest=apk.version, nodes=list(reversed(layout_nodes))))}
    for path in list(outputs):
        native_guid = hashlib.md5(('zjcs-map12-original-team-toggle:' + source_path(path)).encode()).hexdigest()
        importer = 'PrefabImporter' if path.suffix == '.prefab' else 'TextScriptImporter'
        outputs[path.with_name(path.name + '.meta')] = ('fileFormatVersion: 2\nguid: ' + native_guid + '\n' + importer +
            ':\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()
    outputs[DEST.with_name(DEST.name + '.meta')] = ('fileFormatVersion: 2\nguid: ' +
        hashlib.md5(('zjcs-map12-original-team-toggle:' + source_path(DEST)).encode()).hexdigest() +
        '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()

    def record(path):
        return dict(path=source_path(path), sha256=sha(path.read_bytes()))

    proof = dict(format=1, manifest=apk.version, mapClassId=12,
        inputs=[record(p) for p in (ORIGINAL, NATIVE, font_receipt_path, ui_path, language_path, geometry_path)],
        dependencies=dependencies, languageInputs=language,
        scriptBindings=[dict(original=name, path=source_path(path), sha256=sha(path.read_bytes()),
                             meta_sha256=sha(path.with_name(path.name + '.meta').read_bytes())) for name, path in scripts.items()],
        outputs=[dict(path=source_path(path), sha256=sha(raw)) for path, raw in outputs.items()],
        adapters=[record(Path(__file__).resolve())],
        validation=[record(ROOT / 'unity/Assets/Game/Editor' / name) for name in (
            'OfficialMap12PlayModeAcceptance.TeamControls.cs', 'OfficialMap12PlayModeAcceptance.TeamControls.cs.meta')],
        originalCode=[record(ROOT / '.tools/source-packages/current/871_156598/official-code/framework-firstpass-source' / (name + '.cs'))
                      for name in ('UIClickSound', 'UIToggleObject', 'UIToggleStatusBase', 'MultiLanguageText')],
        nodes=6, serializedObjects=28,
        operation='Extract the exact original TeamPanel permission subtree. Retain original object IDs, component '
                  'order, Transform/RectTransform fields and typography. Detach only its root parent for prefab '
                  'installation; explicitly bind actual Windows script ports, the exact original serialized font, '
                  'original pixel-identical sprites and decoded click PCM. Retain original language key and text.',
        limitations=['This binds the original permission widget only; the complete TeamPanel, invitation list, '
                     'NPC roster services, battle flow and persistence remain pending. Runtime validation is separate.'])
    outputs[RECEIPT] = encode(proof)
    for path, raw in outputs.items():
        if write:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
        elif not path.is_file() or path.read_bytes() != raw:
            raise ValueError('Original team permission native/source drift: ' + str(path))
    print('Original team permission subtree bound: six nodes, 28 objects, original sprites/font/text/click.', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true')
    build(parser.parse_args().write)
