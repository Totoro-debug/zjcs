"""Migrate original home binding caches and navigation auto-connection rules."""
import hashlib
import json
from pathlib import Path
import re
import argparse

from official_apk import ROOT
from official_source import OfficialSource


DEST = ROOT / 'unity/Assets/Game/Runtime/Official/HomeEntityBindings'
RECEIPT = ROOT / 'assets/reference/home-entity-bindings-source-manifest.json'
NAMESPACE = 'Zjcs.Official.HomeEntityBindings'
ACTOR_TYPES = (
    'EHomeActorBindingProfile', 'HomeBindingAuthoring', 'HomeUnitBindingData',
    'HomeUnitDefinitionData', 'HomePointDefinition', 'HomePointBehaviourActionBinding',
    'HomeSortingDefinition', 'HomeActorRenderState', 'HomeUnitPointKey',
    'HomeReferenceSelectorAttribute', 'HomeReferenceKind', 'HomeSpaceBindingData',
    'HomeSpaceRouteDefinition', 'HomeSpaceRouteNodeDefinition', 'HomeSpaceRouteEdgeDefinition',
    'HomeSpaceRouteEndpointDefinition', 'EHomeSpaceRouteEndpointKind', 'HomeChannelBindingData',
    'HomeChannelEndpointDefinition', 'HomeChannelAnchorBinding', 'HomeChannelTraversalActionOverride',
    'HomeRuntimeChannelLink', 'HomeRuntimeBindingSnapshot', 'HomeRuntimeSpaceRoute',
    'HomeRuntimeSpaceRouteNode', 'HomeRuntimeSpaceNavigationGraph', 'HomeRuntimeSpaceNavigationNode',
    'HomeRuntimeSpaceNavigationEdge', 'HomeResolvedLocation', 'EHomeResolvedLocationKind',
    'HomeNavigationAutoConnectNode', 'HomeNavigationAutoConnectEdge', 'HomeNavigationAutoConnectResult',
    'HomeNavigationAutoConnectPlan', 'HomeNavigationAutoConnectPlanner', 'EHomeNavigationAutoConnectTargetKind',
    'HomeUnitSpaceOverrideData', 'HomeUnitSpaceOverrideEntry', 'HomeUnitSpaceOverrideProfileData',
    'HomeBindingCacheBuilder', 'HomeRuntimeBindingCacheService', 'IHomeRuntimeBindingCacheService')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def block_end(text, opening):
    """Return the closing brace, respecting quoted strings and comments."""
    depth, i, quote = 0, opening, None
    while i < len(text):
        c = text[i]
        if quote:
            if c == '\\':
                i += 2
                continue
            if c == quote:
                quote = None
        elif text.startswith('//', i):
            i = text.find('\n', i)
            if i < 0:
                break
            continue
        elif text.startswith('/*', i):
            i = text.index('*/', i + 2) + 2
            continue
        elif c in ('"', "'"):
            quote = c
        elif c == '{':
            depth += 1
        elif c == '}':
            depth -= 1
            if depth == 0:
                return i
        i += 1
    raise ValueError('Original C# block did not close')


def remove_logging(text, owner):
    # The conditional logger is an I/O boundary. Leave all surrounding control
    # flow intact, including invalid-edge branches that only emit a diagnostic.
    flags = set(re.findall(r'out (?:var )?(enabled\w*)\)', text))
    for flag in flags:
        while True:
            m = re.search(r'(?m)^\s*if \(' + flag + r'\)\s*\{', text)
            if not m:
                break
            opening = text.index('{', m.start())
            text = text[:m.start()] + '\n' + text[block_end(text, opening) + 1:]
        text = re.sub(r'(?m)^\s*bool ' + flag + r';\s*\n', '\n', text)
    text = re.sub(r'(?m)^\s*(?:GLogger\w+InterpolatedStringHandler )?\w+ = new GLogger\w+InterpolatedStringHandler\([^\n]*;\s*\n', '\n', text)
    text = re.sub(r'(?m)^\s*GLogger\w+InterpolatedStringHandler \w+;\s*\n', '\n', text)
    text = re.sub(r'Log\.(Error|Warn|Debug)\(ref \w+, null, "([^"]+)", [^\n]*\);',
        lambda m: 'Debug.' + {'Error': 'LogError', 'Warn': 'LogWarning', 'Debug': 'Log'}[m[1]]
        + '(' + json.dumps('Official ' + owner + '.' + m[2]) + ');', text)
    if 'GLogger' in text or 'Log.' in text:
        raise ValueError('Unbound original diagnostic: ' + owner)
    return text


def lower_primary_structs(text):
    pattern = re.compile(r'((?:public|private) readonly struct (\w+))\(([^\n]*)\)([^\n]*)\n([\t ]*)\{')
    while m := pattern.search(text):
        opening = text.index('{', m.start())
        closing = block_end(text, opening)
        body = text[opening + 1:closing]
        params = {part.strip().split()[-1] for part in m[3].split(',')}
        assignments = []

        def field(match):
            if match[2] not in params:
                raise ValueError('Primary struct initializer is not a parameter')
            assignments.append(match[1] + ' = ' + match[2] + ';')
            return match[0].replace(' = ' + match[2], '')

        body = re.sub(r'public readonly [\w<>]+ (\w+) = (\w+);', field, body)
        indent = m[5] + '\t'
        constructor = '\n' + indent + 'public ' + m[2] + '(' + m[3] + ')\n' + indent + '{\n'
        constructor += ''.join(indent + '\t' + row + '\n' for row in assignments)
        constructor += indent + '}\n'
        text = text[:m.start()] + m[1] + m[4] + '\n' + m[5] + '{' + constructor + body + text[closing:]
    return text


def prepare():
    source = OfficialSource()
    code = ROOT / '.tools/source-packages/current' / source.version / 'official-code'
    code_receipt = code.parent / 'code-manifest.json'
    identity = json.loads(code_receipt.read_bytes())
    if identity['manifest'] != source.version:
        raise ValueError('Selected home binding code differs')
    inputs = {code_receipt.relative_to(ROOT).as_posix(): sha(code_receipt.read_bytes())}
    for name in ('Assembly-CSharp.dll', 'Framework.FirstPass.Runtime.dll'):
        assembly = code / 'managed/decoded' / name
        if sha(assembly.read_bytes()) != identity['assemblies'][name]['decoded_sha256']:
            raise ValueError('Original binding assembly differs: ' + name)
        inputs[assembly.relative_to(ROOT).as_posix()] = sha(assembly.read_bytes())
    for relative in ('framework-firstpass-source/MonoDictionary.cs', 'framework-firstpass-source/MonoKeyValuePair.cs'):
        path = code / relative
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    for relative in ('MonoDictionary.cs', 'MonoDictionary.cs.meta', 'MonoKeyValuePair.cs', 'MonoKeyValuePair.cs.meta'):
        path = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI' / relative
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    for relative in ('scripts/fixtures/home_entity_bindings_validation.cs', 'scripts/test_home_entity_bindings.ps1'):
        path = ROOT / relative
        inputs[relative] = sha(path.read_bytes())
    previous = json.loads(RECEIPT.read_bytes())['outputs'] if RECEIPT.exists() else {}
    outputs = {}

    def write(path, raw):
        relative = path.relative_to(ROOT).as_posix()
        if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get(relative):
            raise ValueError('Preserve another home binding change: ' + relative)
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists() or path.read_bytes() != raw:
            path.write_bytes(raw)
        outputs[relative] = sha(raw)

    def metadata(path, folder=False):
        token = hashlib.md5(('zjcs:home-entity-bindings:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        kind = 'DefaultImporter' if folder else 'AssemblyDefinitionImporter' if path.suffix == '.asmdef' else 'MonoImporter'
        raw = 'fileFormatVersion: 2\nguid: ' + token + '\n' + ('folderAsset: yes\n' if folder else '')
        write(Path(str(path) + '.meta'), (raw + kind + ':\n  externalObjects: {}\n').encode())

    def read(relative):
        path = code / (relative + '.cs')
        raw = path.read_bytes()
        inputs[path.relative_to(ROOT).as_posix()] = sha(raw)
        return raw.decode('utf-8-sig').replace('\r\n', '\n')

    def emit(name, text):
        text = re.sub(r'(?m)^using [^\n]+;\n|^namespace [^\n]+;\n|^\s*//IL_[^\n]*\n|^\s*\[SortingLayer\]\n', '', text)
        text = remove_logging(text, name)
        text = lower_primary_structs(text)
        # AssetGuidProxy is the original GUID-only serialized descriptor. The
        # binding cache never loads or executes its target. Retain the exact
        # descriptor while the action-graph runtime remains a separate owner.
        text = text.replace('AssetGuidProxy<HomeActionGraph>', 'AssetGuidProxy<ScriptableObject>')
        header = '// Selected original home binding implementation; see the source receipt.\n'
        header += 'using System;\nusing System.Collections.Generic;\nusing System.Linq;\nusing UnityEngine;\nusing UnityEngine.Serialization;\nusing Newtonsoft.Json;\nusing Zjcs.Official.ProfessionUI;\n'
        header += 'namespace ' + NAMESPACE + '\n{\nusing Object = UnityEngine.Object;\n'
        raw = '\n'.join(line.rstrip() for line in (header + text.strip() + '\n}\n').split('\n')).encode()
        path = DEST / (name + '.cs')
        write(path, raw)
        metadata(path)

    for name in ACTOR_TYPES:
        emit(name, read('client-source/Home.ActorBehaviour/' + name))
    for name in ('HomeEntityData', 'HomeEntityDataProvider', 'HomeCharacterType'):
        emit(name, read('client-source/' + name))
    emit('HomeEntityNodeRegister', read('client-source/Home.Furniture/HomeEntityNodeRegister'))
    emit('HomePlayableClipBind', read('client-source/HomePlayable/HomePlayableClipBind'))
    emit('HomePlayableClipData', read('client-source/HomePlayable/HomePlayableClipData'))
    emit('AssetGuidProxy', read('framework-firstpass-source/AssetGuidProxy'))
    text = read('client-source/Home.ActorBehaviour/HomeRuntimeBindingSnapshotUtils')
    for signature in ('public static HomeServiceResult<HomeTargetQueryResult> SelectUnitPoint(',
                      'public static HomeServiceResult<HomeTargetQueryResult> SelectReachableUnitPoint('):
        start = text.index(signature)
        opening = text.index('{', start)
        text = text[:start] + text[block_end(text, opening) + 1:]
    emit('HomeRuntimeBindingSnapshotUtils', text)
    ports = '''public interface IHomeEntityDataAgent
{
    void GetDatas<T>(List<T> list) where T : HomeEntityData;
}
public interface IHomeEntityManager
{
    IHomeEntityDataAgent EntityDataAgent { get; }
}'''
    emit('HomeBindingSources', ports)
    path = DEST / 'Zjcs.Official.HomeEntityBindings.asmdef'
    write(path, (json.dumps(dict(name='Zjcs.Official.HomeEntityBindings', references=[
        'Zjcs.Official.ProfessionUI', 'Unity.Newtonsoft.Json'], autoReferenced=True), indent=2) + '\n').encode())
    metadata(path)
    metadata(DEST, True)
    generator = Path(__file__)
    inputs[generator.relative_to(ROOT).as_posix()] = sha(generator.read_bytes())
    RECEIPT.write_text(json.dumps(dict(manifest=source.version, inputs=inputs, outputs=outputs,
        adaptations=['Original conditional diagnostics use Unity logging without changing surrounding branches.',
            'Primary struct constructors use equivalent explicit constructors for the installed Unity C# compiler.',
            'The original GUID-only action-graph descriptor keeps a ScriptableObject type boundary; binding caches do not load or execute graphs.',
            'The entity data source exposes only the original GetDatas call surface consumed by these caches.',
            'Snapshot selection methods requiring the still-separate actor/planner runtime are not migrated by this binding module.'],
        scope='Original home entity data, point/space/channel binding caches, profile overrides and automatic navigation edge generation. This does not complete actor action-graph execution or the storage building.'),
        ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Restored original home entity bindings:', len(outputs), 'outputs')


def validation_inputs(directory):
    from prepare_current_partner_native import PartnerNativeRecovery
    source = OfficialSource()
    directory = directory.resolve()
    directory.relative_to(ROOT / 'output/validation')
    bundle = 'defaultpackage_assets_prefabs_home_dynamic_furniture_storagebox.bundle'
    recovery = PartnerNativeRecovery(native_cache=ROOT / '.tools/source-packages/current' / source.version / 'home-storage-native',
        export_name='native-export', bundle=bundle)
    root, = [pointer.deref() for path, pointer in recovery.apk.container(bundle)
        if path.casefold() == 'assets/prefabs/home/dynamic/furniture/storagebox/storagebox.prefab'
        and pointer.deref().type.name == 'GameObject']
    authoring = register = transform = None
    for entry in root.read(check_read=False).m_Component:
        obj = entry.component.deref()
        if obj.type.name == 'Transform':
            transform = recovery.fields(obj)
        elif obj.type.name == 'MonoBehaviour':
            script = obj.read(check_read=False).m_Script.deref().read(check_read=False)
            if script.m_ClassName == 'HomeBindingAuthoring':
                authoring = obj
            elif script.m_ClassName == 'HomeEntityNodeRegister':
                register = recovery.fields(obj)
    if authoring is None or register is None or transform is None:
        raise ValueError('Resolve the actual storage authoring, transform and node registry')
    fields = recovery.fields(authoring)
    fields = {key: fields[key] for key in ('Profile', 'Units', 'Spaces', 'Channels')}
    data = dict(authoringJson=json.dumps(fields, ensure_ascii=False),
        position=transform['m_LocalPosition'], rotation=transform['m_LocalRotation'], scale=transform['m_LocalScale'],
        nodeKeys=[row['Key'] for row in register['GameObjectDict']['mSerializeBuffer']],
        source=recovery.apk.provenance(authoring), object_sha256=sha(authoring.get_raw_data()))
    path = directory / 'storage-binding-input.json'
    if path.exists():
        raise ValueError('A new owned validation input path is required')
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Prepared exact storage binding input:', path)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--validation-dir', type=Path)
    args = parser.parse_args()
    prepare()
    if args.validation_dir is not None:
        validation_inputs(args.validation_dir)
