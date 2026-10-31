"""Stage verified converted payloads in an isolated validation workspace."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil

import UnityPy
from UnityPy.export.Texture2DConverter import parse_image_data


def sha(data):
    return hashlib.sha256(data).hexdigest()


def textures(cache, work):
    output = cache / 'all-assets/windows-textures'
    receipt = json.loads((output / 'source-manifest.json').read_bytes())
    groups = {}
    for row in receipt['textures']:
        source, fields = row['source'], row['original_fields']
        key = (source['type'], source['format'], source['mips'], fields['m_ColorSpace'])
        if key not in groups or row['pixel_bytes'] < groups[key]['pixel_bytes']:
            groups[key] = row
    project = work / 'texture-unity'
    (project / 'Packages').mkdir(parents=True, exist_ok=True)
    (project / 'Packages/manifest.json').write_text('{"dependencies":{}}\n')
    (project / 'ProjectSettings').mkdir(exist_ok=True)
    (project / 'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 2022.3.57f1c2\n')
    (project / 'Assets/Editor').mkdir(parents=True, exist_ok=True)
    shutil.copyfile(Path(__file__).with_name('current_resource_texture_validation.cs'), project / 'Assets/Editor/CurrentResourceTextureValidation.cs')
    (work / 'expected-mips').mkdir(exist_ok=True)
    cases = []
    for row in groups.values():
        asset = row['outputs'][0]
        payload = (output / asset['path']).read_bytes()
        if sha(payload) != asset['sha256']:
            raise ValueError('Native payload changed')
        target = project / asset['path']
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(payload)
        shutil.copyfile(output / row['outputs'][1]['path'], Path(str(target) + '.meta'))
        pixels = bytes.fromhex(re.search(rb'_typelessdata: ([0-9a-f]*)', payload)[1].decode()) if row['pixel_bytes'] else b''
        if sha(pixels) != row['pixels_sha256']:
            raise ValueError('Native pixel bytes changed')
        mips = []
        for mip in row['mips']:
            size = mip.get('native_size', mip.get('rgba_size'))
            data = pixels[mip['native_offset']:mip['native_offset'] + size]
            width, height = mip['width'], mip['height']
            if row['native_format'] != 4:
                data = parse_image_data(data, width, height, row['native_format'], (2022, 3, 57, 1), 19, flip=False).convert('RGBA').tobytes()
            name = row['guid'] + '-' + str(mip['face']) + '-' + str(mip['level']) + '.rgba'
            (work / 'expected-mips' / name).write_bytes(data)
            mips.append(dict(face=mip['face'], level=mip['level'], expected='expected-mips/' + name, sha256=sha(data)))
        cases.append(dict(path=asset['path'], sha256=asset['sha256'], name=row['source']['name'],
                          width=row['source']['width'], height=row['source']['height'],
                          mip_count=row['source']['mips'], native_format=row['native_format'],
                          cubemap=row['source']['type'] == 'Cubemap', mips=mips))
    (work / 'texture-catalog.json').write_text(json.dumps(dict(textures=cases), indent=2) + '\n')
    print('Texture cases', len(cases), 'mip faces', sum(len(r['mips']) for r in cases), flush=True)


def shaders(cache, work):
    output = cache / 'all-assets/windows-shaders'
    receipt = json.loads((output / 'source-manifest.json').read_bytes())
    project = work / 'unity'
    (project / 'Packages').mkdir(parents=True, exist_ok=True)
    (project / 'Packages/manifest.json').write_text('{"dependencies":{}}\n')
    (project / 'ProjectSettings').mkdir(exist_ok=True)
    (project / 'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 2022.3.57f1c2\n')
    (project / 'Assets/Editor').mkdir(parents=True, exist_ok=True)
    shutil.copyfile(Path(__file__).with_name('current_resource_windows_validation.cs'), project / 'Assets/Editor/CurrentResourceWindowsValidation.cs')
    cases = []
    for row in receipt['shaders']:
        target = project / row['path']
        payload = (output / row['path']).read_bytes()
        if sha(payload) != row['sha256']:
            raise ValueError('Shader payload changed')
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(payload)
        shutil.copyfile(Path(str(output / row['path']) + '.meta'), Path(str(target) + '.meta'))
        if row['programs']:
            cases.append(dict(path=row['path'], sha256=row['sha256'], source_name=row['source']['name'], programs=row['programs']))
    (work / 'shader-catalog.json').write_text(json.dumps(dict(shaders=sorted(cases, key=lambda x:len(x['programs']))), indent=2) + '\n')
    print('Shader cases', len(cases), 'programs', sum(len(r['programs']) for r in cases), flush=True)


def engine(cache, work):
    source = cache / 'windows-engine'
    receipt = json.loads((source / 'source-manifest.json').read_bytes())
    raw = (source / receipt['inputs'][0]['output']).read_bytes()
    if sha(raw) != receipt['inputs'][0]['decoded_sha256']:
        raise ValueError('Windows engine payload changed')
    env = UnityPy.load(raw)
    file = next(iter(env.files.values()))
    project = work / 'texture-unity'
    guid = 'e5085b6d31f342d2a0a6aa607ef496ef'
    script = project / 'Assets/Editor/CurrentResourceEngineProbe.cs'
    shutil.copyfile(Path(__file__).with_name('current_resource_engine_probe.cs'), script)
    Path(str(script) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + guid + '\n')
    text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_Script: {fileID: 11500000, guid: ' + guid + ', type: 3}\n  m_Name: EngineProbe\n'
    cases = {}
    for kind, field in [('Shader', 'shaders'), ('ComputeShader', 'computes')]:
        text += '  ' + field + ':\n'
        cases[field] = []
        for row in receipt['counterparts']:
            if row['type'] != kind:
                continue
            obj = file.objects[row['windows_file_id']]
            if sha(obj.get_raw_data()) != row['windows_object_sha256']:
                raise ValueError('Native engine object changed')
            text += '  - {fileID: ' + str(row['windows_file_id']) + ', guid: 0000000000000000e000000000000000, type: 0}\n'
            case = dict(name=row['name'], windows_d3d=True, kernels=[])
            if kind == 'ComputeShader':
                variant = next((v for v in obj.read_typetree()['variants'] if v['targetRenderer'] == 2), None)
                case['windows_d3d'] = variant is not None
                if variant:
                    named = {}
                    for kernel in variant['kernels']:
                        size = kernel['uniqueVariants'][0]['threadGroupSize']
                        if kernel['name'] in named and named[kernel['name']] != size:
                            raise ValueError('Kernel keyword variants have different thread groups')
                        named[kernel['name']] = size
                    case['kernels'] = [dict(name=name, x=size[0], y=size[1], z=size[2]) for name, size in named.items()]
            cases[field].append(case)
    (project / 'Assets/EngineProbe.asset').write_text(text)
    (work / 'engine-catalog.json').write_text(json.dumps(cases, indent=2) + '\n')
    print('Native engine cases', len(cases['shaders']), len(cases['computes']), flush=True)


def render(cache, work):
    source = cache / 'all-assets/windows-shaders'
    receipt = json.loads((source / 'source-manifest.json').read_bytes())
    row = next(r for r in receipt['shaders'] if r['source']['name'] == 'UI/Default')
    payload = (source / row['path']).read_bytes()
    if sha(payload) != row['sha256']:
        raise ValueError('Render shader payload changed')
    target = work / 'texture-unity' / row['path']
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(payload)
    shutil.copyfile(Path(str(source / row['path']) + '.meta'), Path(str(target) + '.meta'))
    shutil.copyfile(Path(__file__).with_name('current_resource_shader_render_validation.cs'), work / 'texture-unity/Assets/Editor/CurrentResourceShaderRenderValidation.cs')
    (work / 'render-shader-path.txt').write_text(row['path'] + '\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cache', type=Path, required=True)
    parser.add_argument('--work', type=Path, required=True)
    parser.add_argument('--kind', choices=['shaders', 'textures', 'engine', 'render'], required=True)
    args = parser.parse_args()
    globals()[args.kind](args.cache.resolve(), args.work.resolve())
