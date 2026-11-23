"""Migrate the original shared home overlap and light-layer relation components."""
import hashlib
import inspect
import json
from pathlib import Path
import re

from official_apk import ROOT
from official_source import OfficialSource
from prepare_home_entity_bindings import remove_logging


DEST = ROOT / 'unity/Assets/Game/Runtime/Official/HomeRelations'
RECEIPT = ROOT / 'assets/reference/home-relations-source-manifest.json'
NAMES = ('EHomeRelationGroup', 'HomeRelation', 'HomeMutex', 'IHomeRelationManager',
         'HomeRelationManager', 'HomeMutexLightLayer', 'SortingLayerMask')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def prepare():
    source = OfficialSource()
    code = ROOT / '.tools/source-packages/current' / source.version / 'official-code'
    identity_path = code.parent / 'code-manifest.json'
    identity = json.loads(identity_path.read_bytes())
    if identity['manifest'] != source.version:
        raise ValueError('Selected home relation source differs')
    assembly = code / 'managed/decoded/Assembly-CSharp.dll'
    if sha(assembly.read_bytes()) != identity['assemblies']['Assembly-CSharp.dll']['decoded_sha256']:
        raise ValueError('Selected home relation assembly differs')
    inputs = {p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in (identity_path, assembly)}
    previous = json.loads(RECEIPT.read_bytes())['outputs'] if RECEIPT.exists() else {}
    outputs = {}

    def write(path, raw):
        key = path.relative_to(ROOT).as_posix()
        if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get(key):
            raise ValueError('Preserve another home relation edit: ' + key)
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists() or path.read_bytes() != raw:
            path.write_bytes(raw)
        outputs[key] = sha(raw)

    def metadata(path, folder=False):
        guid = hashlib.md5(('zjcs:home-relations:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        importer = 'DefaultImporter' if folder else 'AssemblyDefinitionImporter' if path.suffix == '.asmdef' else 'MonoImporter'
        text = 'fileFormatVersion: 2\nguid: ' + guid + '\n' + ('folderAsset: yes\n' if folder else '')
        write(Path(str(path) + '.meta'), (text + importer + ':\n  externalObjects: {}\n').encode())

    # Install the owned folder identity before emitting scripts, so background
    # Unity imports cannot assign a second identity midway through generation.
    metadata(DEST, True)
    for name in NAMES:
        path = code / 'client-source' / (name + '.cs')
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        text = path.read_text('utf-8-sig').replace('\r\n', '\n')
        text = re.sub(r'(?m)^using [^\n]+;\n|^\s*//IL_[^\n]*\n|^\s*\[Inject\]\n', '', text)
        text, diagnostics = re.subn(r'Log\.Error\(ref \w+, (\(\(Component\)(?:this|homeMutex)\)\.gameObject), "(\w+)", "[^"]+", \d+\);',
            lambda match: 'Debug.LogError(' + json.dumps('Official ' + name + '.' + match[2]) + ', ' + match[1] + ');', text)
        if diagnostics != {'HomeRelationManager': 2, 'HomeMutexLightLayer': 1}.get(name, 0):
            raise ValueError('Verify the original relation diagnostic context: ' + name)
        text = remove_logging(text, name)
        text = text.replace('Vector2.op_Implicit(position)', '(Vector2)position').replace('Vector2.op_Implicit(position2)', '(Vector2)position2')
        if any(token in text for token in ('Framework.', 'GLogger', 'Log.', '[Inject]', '.op_Implicit')):
            raise ValueError('Unbound original relation dependency: ' + name)
        header = '// Selected original home overlap/light relation implementation; see its source receipt.\n'
        header += 'using System;\nusing System.Collections.Generic;\nusing System.Reflection;\nusing UnityEngine;\nusing UnityEngine.Rendering.Universal;\n'
        header += 'namespace Zjcs.Official.HomeRelations\n{\nusing Object = UnityEngine.Object;\n'
        raw = '\n'.join(line.rstrip() for line in (header + text.strip() + '\n}\n').split('\n')).encode()
        target = DEST / (name + '.cs'); write(target, raw); metadata(target)
    definition = DEST / 'Zjcs.Official.HomeRelations.asmdef'
    write(definition, (json.dumps(dict(name='Zjcs.Official.HomeRelations',
        references=['Unity.RenderPipelines.Universal.Runtime']), indent=2) + '\n').encode())
    metadata(definition)
    for path in (Path(__file__), Path(inspect.getsourcefile(remove_logging))):
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    validation = [ROOT / p for p in ('scripts/fixtures/home_relation_validation.cs',
        'scripts/fixtures/home_relation_probe.cs', 'scripts/test_home_relations.ps1')]
    receipt = dict(manifest=source.version, inputs=inputs, outputs=outputs,
        validation_inputs={p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in validation if p.is_file()},
        adaptations=[
            'Original relation groups, role filtering, translated world rectangles, ordered registration/removal callbacks and ForceCheck are retained.',
            'Original URP light-layer capture, RemoveAll filtering, changed guard and restoration are retained.',
            'Original ExecuteAlways and component requirements are retained; dependency injection is an explicit owner-assigned property.',
            'Decompiler Vector3-to-Vector2 casts are restored and diagnostics use Unity logging.'
        ], scope='Shared original overlap/light-layer components. Native house binding, environment light/clock controllers and main-scene play verification remain separate.')
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Restored original home relation components:', len(outputs), 'outputs')


if __name__ == '__main__':
    prepare()
