"""Migrate original home prefab creators, addresses and selected local tables."""
import hashlib
import inspect
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from official_source import OfficialSource
from official_tables import Constructors
from prepare_home_entity_bindings import remove_logging
from prepare_unity_profession_appearance_resolver import method


DEST = ROOT / 'unity/Assets/Game/Runtime/Official/HomeEntityCreation'
DATA = ROOT / 'unity/Assets/Game/Resources/Home/EntityCreation'
RECEIPT = ROOT / 'assets/reference/home-entity-creation-source-manifest.json'
TEMPLATES = ROOT / 'scripts/templates/home_entity_creation'
TABLES = {'furniture_skin': ('TbFurnitureSkinInfo', 'FurnitureSkins'),
    'home_theme_skin': ('TbHomeThemeSkinInfo', 'ThemeSkins'),
    'home_skin_weather_override_prefab': ('TbHomeSkinWeatherOverridePrefabInfo', 'WeatherOverrides')}


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def prepare():
    source = OfficialSource()
    code = ROOT / '.tools/source-packages/current' / source.version / 'official-code'
    code_receipt = code.parent / 'code-manifest.json'
    identity = json.loads(code_receipt.read_bytes())
    if identity['manifest'] != source.version:
        raise ValueError('Selected home creator source differs')
    inputs = {code_receipt.relative_to(ROOT).as_posix(): sha(code_receipt.read_bytes())}
    for name in ('Assembly-CSharp.dll', 'Console.dll'):
        path = code / 'managed/decoded' / name
        if sha(path.read_bytes()) != identity['assemblies'][name]['decoded_sha256']:
            raise ValueError('Selected home creator assembly differs: ' + name)
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    previous = json.loads(RECEIPT.read_bytes())['outputs'] if RECEIPT.exists() else {}
    outputs = {}

    def write(path, raw):
        relative = path.relative_to(ROOT).as_posix()
        if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get(relative):
            raise ValueError('Preserve another home creator edit: ' + relative)
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists() or path.read_bytes() != raw:
            path.write_bytes(raw)
        outputs[relative] = sha(raw)

    def metadata(path, folder=False):
        token = hashlib.md5(('zjcs:home-entity-creation:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        kind = 'DefaultImporter' if folder else 'AssemblyDefinitionImporter' if path.suffix == '.asmdef' else 'TextScriptImporter' if path.suffix == '.json' else 'MonoImporter'
        raw = 'fileFormatVersion: 2\nguid: ' + token + '\n' + ('folderAsset: yes\n' if folder else '')
        write(Path(str(path) + '.meta'), (raw + kind + ':\n  externalObjects: {}\n').encode())

    def read(relative):
        path = code / (relative + '.cs')
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        text = path.read_text('utf-8-sig').replace('\r\n', '\n')
        return re.sub(r'(?m)^using [^\n]+;\n|^namespace [^\n]+;\n|^\s*//IL_[^\n]*\n', '', text)

    def emit(name, body):
        body = remove_logging(body, name)
        body = re.sub(r'\bAssetOperationHandle<GameObject>\b', 'IHomeEntityPrefabHandle', body)
        # A generic closing bracket is not a word character, so handle its
        # normal declaration spelling as well as the selected explicit types.
        body = body.replace('AssetOperationHandle<GameObject>', 'IHomeEntityPrefabHandle')
        body = body.replace('.Deconstruct(ref ', '.Deconstruct(out ').replace(', ref ', ', out ')
        body = body.replace('TryGetComponent<HomeEntityCreateData>(ref ', 'TryGetComponent<HomeEntityCreateData>(out ')
        body = body.replace('TryGetComponent<IHomeEntityChangeCreateNotify>(ref ', 'TryGetComponent<IHomeEntityChangeCreateNotify>(out ')
        body = body.replace('ServiceProviderExtensions.Instantiate(content.ServiceProvider, result, value, false)',
            'content.Instantiate(result, value, false)')
        body = body.replace('Parsers.Instance.HomeSkinWeatherOverridePrefabInfoParser.Get(', 'context.Tables.GetWeatherOverride(')
        body = body.replace('LanguageUtils.Get', 'context.PrefabPaths.Get')
        if 'CollectionExtensions.GetValueOrDefault<EFurniture, EFurniture>' in body:
            body = body.replace('CollectionExtensions.GetValueOrDefault<EFurniture, EFurniture>((IReadOnlyDictionary<EFurniture, EFurniture>)mCreateMapping, key, key)',
                '(mCreateMapping.TryGetValue(key, out var value) ? value : key)')
        if any(token in body for token in ('Parsers.', 'MeManager', 'ServiceProviderExtensions', 'AssetOperationHandle', 'Framework.', 'GLogger', 'Log.')):
            raise ValueError('Unbound original home creator dependency: ' + name)
        header = '// Selected original home entity creation implementation; see its source receipt.\nusing System;\nusing System.Collections.Generic;\nusing UnityEngine;\nusing Zjcs.Official.HomeUI;\nusing Zjcs.Official.ProfessionUI;\nnamespace Zjcs.Official.HomeEntityCreation\n{\nusing Object = UnityEngine.Object;\n'
        text = '\n'.join(line.rstrip() for line in (header + body.strip() + '\n}\n').split('\n'))
        path = DEST / (name + '.cs'); write(path, text.encode()); metadata(path)

    enum_source = read('common-source/Common/EFurniture')
    enum_path = ROOT / 'unity/Assets/Game/Runtime/Official/HomeUI/EFurniture.cs'
    installed = enum_path.read_text('utf-8-sig')
    def enums(text):
        return [(key, int(value)) for key, value in re.findall(r'\b(\w+)\s*=\s*(\d+)', text)]
    if enums(enum_source) != enums(installed):
        raise ValueError('Reuse the selected current furniture enum only after identity verification')
    inputs[enum_path.relative_to(ROOT).as_posix()] = sha(enum_path.read_bytes())
    for name in ('HomeThemeType', 'HomeEnvironmentType'):
        emit(name, read('common-source/Common/' + name))
    for name in ('HomeEntity', 'IHomeEntityCreator', 'BaseHomeEntityCreator', 'FurnitureEntityCreator',
                 'ThemeEntityCreator', 'IHomeEntityChangeCreateNotify', 'HomeFurnitureChangeData'):
        emit(name, read('client-source/' + name))
    emit('HomeEntityCreateData', read('client-source/Home.ActorBehaviour.Entity.Creator/HomeEntityCreateData'))
    language = read('common-source/Common/LanguageUtils')
    bodies = []
    for name in ('GetFurniturePrefabPath', 'GetThemePrefabPath'):
        for match in re.finditer(r'public static string ' + name + r'\([^\n]*', language):
            bodies.append(method(language, match[0]).replace('public static string ', 'public string ')
                .replace('Parsers.Instance.FurnitureSkinInfoParser.Get(', 'Tables.GetFurnitureSkin(')
                .replace('Parsers.Instance.HomeThemeSkinInfoParser.Get(', 'Tables.GetThemeSkin('))
    if len(bodies) != 4:
        raise ValueError('Preserve the complete original furniture/theme address overloads')
    emit('HomeEntityPrefabPaths', 'public sealed class HomeEntityPrefabPaths\n{\npublic IHomeEntityPrefabTables Tables { get; }\npublic HomeEntityPrefabPaths(IHomeEntityPrefabTables tables) { Tables = tables ?? throw new ArgumentNullException(nameof(tables)); }\n' + '\n'.join(bodies) + '\n}')
    for path in sorted(TEMPLATES.glob('*.cs')):
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        write(DEST / path.name, path.read_bytes()); metadata(DEST / path.name)
    definition = DEST / 'Zjcs.Official.HomeEntityCreation.asmdef'
    write(definition, (json.dumps(dict(name='Zjcs.Official.HomeEntityCreation', references=[
        'Zjcs.Official.HomeUI', 'Zjcs.Official.ProfessionUI']), indent=2) + '\n').encode())
    metadata(definition); metadata(DEST, True)

    apk = Apk.current()
    table_sources, payload = [], {}
    try:
        if apk.version != source.version:
            raise ValueError('Creator configuration/code sources differ')
        constructors = Constructors(code)
        for name, (constructor, key) in TABLES.items():
            obj = apk.asset('config_luban', '/' + name + '.g.bytes')
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            payload[key] = constructors.table(raw, constructor)
            table_sources.append(dict(table=name, constructor=constructor, source=apk.provenance(obj),
                original_sha256=sha(obj.get_raw_data()), payload_sha256=sha(raw)))
        for name, value in constructors.sources.items():
            if value:
                path = code / 'common-source/Common' / (name + '.cs')
                if not path.is_file(): path = code / 'common-source/Common.LubanGen' / (name + '.cs')
                inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    finally:
        apk.reader.close()
    raw = (json.dumps(payload, ensure_ascii=False, indent=2, allow_nan=False) + '\n').encode()
    path = DATA / 'catalog.json'; write(path, raw); metadata(path); metadata(DATA, True)
    for helper in (remove_logging, method, Constructors):
        path = Path(inspect.getsourcefile(helper)); inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    for name in ('official_apk.py', 'official_source.py', 'official_tables.py', 'prepare_home_entity_creation.py'):
        path = ROOT / 'scripts' / name; inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    validations = [ROOT / name for name in ('scripts/fixtures/home_entity_creation_validation.cs', 'scripts/test_home_entity_creation.ps1')]
    proof = dict(manifest=source.version, inputs=inputs, outputs=outputs, tables=table_sources,
        validation_inputs={p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in validations if p.is_file()},
        adaptations=[
            'Original creator caching, slot lookup, prefab offsets, replacement notification/release/create order and failure branches are retained.',
            'Original furniture mapping, preview-weather precedence and four address overloads are retained.',
            'Framework prefab handles and service instantiation are explicit local owner ports; runtime loading uses no source-version gates.',
            'Selected scalar configuration fields have local DTOs; complete decoded source rows are retained in the resource payload.',
            'Decompiler ref/out syntax is lowered without changing assignments; diagnostic I/O uses Unity logging.'
        ], scope='Shared original prefab creation and selected local configuration. Full entity registration, home-host integration, playable graphs and building verification remain separate.')
    RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Restored original home entity creation:', len(outputs), 'outputs;', {k: len(v) for k,v in payload.items()})


if __name__ == '__main__':
    prepare()
