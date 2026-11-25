"""Create local runtime data from the current official airship tables."""
import argparse
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from prepare_home_airship_activity_config import extract
from validation_workspace import require_temporary_unity_project


def sha(data):
    return hashlib.sha256(data).hexdigest()


def guid(path):
    return hashlib.md5(('zjcs:home-airship-config:' + path).encode()).hexdigest()


def prepare(project):
    extracted = extract()
    template = 'scripts/templates/home_airship/OfflineHomeAirshipConfiguration.cs'
    code_path = 'Assets/Game/Runtime/Offline/HomeAirship/OfflineHomeAirshipConfiguration.cs'
    data_path = 'Assets/Game/Resources/Home/Airship/airship-config.json'
    code = (ROOT / template).read_bytes()
    data = (json.dumps({'tables': extracted['tables']}, ensure_ascii=False, indent=2) + '\n').encode()
    def meta(path, importer):
        return (f'fileFormatVersion: 2\nguid: {guid(path)}\n' + importer + ':\n  externalObjects: {}\n').encode()
    outputs = {code_path: code, code_path + '.meta': meta(code_path, 'MonoImporter'),
               data_path: data, data_path + '.meta': meta(data_path, 'TextScriptImporter')}
    for name, raw in outputs.items():
        path = project / name
        if path.exists() and path.read_bytes() != raw:
            raise ValueError('Preserve existing airship configuration file: ' + str(path))
    for name, raw in outputs.items():
        path = project / name
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists():
            path.write_bytes(raw)
    inputs = {}
    for name in (template, 'scripts/prepare_home_airship_activity_config.py',
                 'scripts/prepare_home_airship_configuration.py',
                 'unity/Assets/Game/Runtime/Offline/HomeAirship/Zjcs.Offline.HomeAirship.asmdef'):
        inputs[name] = sha((ROOT / name).read_bytes())
    decoder = 'scripts/official_tables.py'
    return {'official_manifest': extracted['official_manifest'], 'sources': extracted['sources'],
            'source_code_sha256': extracted['source_code_sha256'], 'inputs': inputs,
            'normalized_tool_sha256': {decoder: sha((ROOT / decoder).read_bytes().replace(b'\r\n', b'\n'))},
            'outputs': {name: sha(raw) for name, raw in outputs.items()}}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    destination = parser.add_mutually_exclusive_group(required=True)
    destination.add_argument('--project', help='Owned independent Unity validation project')
    destination.add_argument('--install', action='store_true', help='Install the validated source assets')
    args = parser.parse_args()
    project = ROOT / 'unity' if args.install else require_temporary_unity_project(Path(args.project))
    if args.install:
        output = ROOT / 'assets/reference/home-airship-config-source-manifest.json'
        if output.exists():
            old = json.loads(output.read_text(encoding='utf8'))
            current = extract()
            if (old['official_manifest'] != current['official_manifest'] or
                    old['sources'] != current['sources'] or
                    old['source_code_sha256'] != current['source_code_sha256']):
                raise ValueError('Airship configuration does not match the current official input')
            for name, expected in {**old['inputs'], **old['outputs']}.items():
                path = ROOT / name
                if not path.is_file() or sha(path.read_bytes()) != expected:
                    raise ValueError('Preserve changed configuration input or output: ' + name)
            for name, expected in old['normalized_tool_sha256'].items():
                if sha((ROOT / name).read_bytes().replace(b'\r\n', b'\n')) != expected:
                    raise ValueError('Airship configuration decoder has changed: ' + name)
            print('Verified existing airship configuration')
            return
    result = prepare(project)
    if args.install:
        result['outputs'] = {'unity/' + key: value for key, value in result['outputs'].items()}
    else:
        output = project.parent / 'airship-config-validation-source.json'
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print('Prepared airship configuration in', project)


if __name__ == '__main__':
    main()
