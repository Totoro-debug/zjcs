"""Validate Unity project metadata and local references without importing assets.

This is a static check, not an Editor compilation, runtime or provenance check.
"""
from pathlib import Path
import json
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / 'unity'


def check():
    errors = []
    assets = PROJECT / 'Assets'
    required = [
        '.gdignore', 'Packages/manifest.json', 'ProjectSettings/ProjectVersion.txt',
        'ProjectSettings/ProjectSettings.asset', 'ProjectSettings/EditorBuildSettings.asset',
        'Assets/Game/Editor/ProjectTools.cs', 'Assets/Game/Editor/Zjcs.Editor.asmdef',
        'Assets/Game/Scenes/Bootstrap.unity', 'Assets/Game/Runtime/Official',
        'Assets/Game/Runtime/Offline', 'Assets/Official',
    ]
    for relative in required:
        if not (PROJECT / relative).exists():
            errors.append('Missing project input: ' + relative)
    guids = {}
    for path in assets.rglob('*'):
        if any(part.startswith('.') for part in path.relative_to(assets).parts):
            continue
        relative = path.relative_to(PROJECT).as_posix()
        if path.suffix == '.meta':
            if not path.with_suffix('').exists():
                errors.append('Orphan metadata: ' + relative)
            matches = re.findall(r'^guid: ([0-9a-f]{32})$', path.read_text('utf8'), re.M)
            if len(matches) != 1:
                errors.append('Invalid asset GUID: ' + relative)
                continue
            guid = matches[0]
            if guid in guids:
                errors.append('Duplicate GUID: ' + relative + ' and ' + guids[guid])
            guids[guid] = relative
        elif not path.with_name(path.name + '.meta').is_file():
            errors.append('Missing stable metadata: ' + relative)
    for path in PROJECT.glob('Packages/*.json'):
        try:
            manifest = json.loads(path.read_text('utf8'))
            if path.name == 'manifest.json' and not isinstance(manifest.get('dependencies'), dict):
                errors.append('Package manifest dependencies must be an object')
        except ValueError as error:
            errors.append(str(path) + ': ' + str(error))
    for path in assets.rglob('*.asmdef'):
        try:
            assembly = json.loads(path.read_text('utf8'))
            if path.parent.name == 'Editor' and assembly.get('includePlatforms') != ['Editor']:
                errors.append('Editor assembly must be excluded from players: ' + str(path))
        except ValueError as error:
            errors.append(str(path) + ': ' + str(error))
    build_settings = PROJECT / 'ProjectSettings/EditorBuildSettings.asset'
    if build_settings.is_file():
        text = build_settings.read_text('utf8')
        scenes = re.findall(r'^    path: (.+)\n    guid: ([0-9a-f]{32})$', text, re.M)
        if not scenes:
            errors.append('No scenes found in build settings')
        for scene, guid in scenes:
            if not (PROJECT / scene).is_file() or guids.get(guid) != scene + '.meta':
                errors.append('Scene path/GUID binding mismatch: ' + scene)
    for error in errors:
        print('ERROR: ' + error)
    if not errors:
        print(f'Unity static metadata check passed ({len(guids)} GUIDs). Editor import/compile/run not checked.')
    return bool(errors)


if __name__ == '__main__':
    sys.exit(check())
