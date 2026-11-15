"""Bind local world rank to the selected original home experience queries."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
EDITOR = ROOT / 'unity/Assets/Game/Editor/HomeExperienceEnvironment'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def meta(path, folder=False):
    target = Path(str(path) + '.meta')
    if not target.exists():
        guid = hashlib.md5(('zjcs:home-experience-environment:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        importer = 'DefaultImporter' if folder else 'MonoImporter' if path.suffix == '.cs' else 'AssemblyDefinitionImporter'
        target.write_text('fileFormatVersion: 2\nguid: ' + guid + '\n' + ('folderAsset: yes\n' if folder else '') + importer + ':\n  externalObjects: {}\n', 'utf8', newline='\n')
    return target


def prepare():
    EDITOR.mkdir(exist_ok=True)
    fixture = ROOT / 'scripts/fixtures/home_experience_environment_validation.cs'
    (EDITOR / 'HomeExperienceEnvironmentValidation.cs').write_text(fixture.read_text('utf8'), 'utf8', newline='\n')
    (EDITOR / 'Zjcs.Editor.HomeExperienceEnvironment.asmdef').write_text(json.dumps(dict(
        name='Zjcs.Editor.HomeExperienceEnvironment', references=['Zjcs.Offline.HomeExperienceUI', 'Zjcs.Official.HomeExperienceRules',
        'Zjcs.Offline.Progression', 'Zjcs.Offline.State', 'Zjcs.Offline.CharacterCreation', 'Zjcs.Editor',
        'Zjcs.Official.HomeExperienceRewards', 'Zjcs.Official.HomeHudUI', 'Zjcs.Official.ProfessionUI'], includePlatforms=['Editor']), indent=2)+'\n', 'utf8', newline='\n')
    outputs = {}
    for path in [ROOT/'unity/Assets/Game/Runtime/Offline/HomeExperienceUI/OfflineHomeExperienceEnvironmentSource.cs',
                 ROOT/'unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.Experience.cs', *EDITOR.glob('*.cs'), *EDITOR.glob('*.asmdef')]:
        for item in (path, meta(path)):
            outputs[item.relative_to(ROOT).as_posix()] = sha(item)
    item = meta(EDITOR, True); outputs[item.relative_to(ROOT).as_posix()] = sha(item)
    inputs = {}
    for path in [Path(__file__), fixture,
                 ROOT/'assets/reference/home-experience-rules-source-manifest.json',
                 ROOT/'unity/Assets/Game/Runtime/Official/HomeExperienceRules/PlayerBlessLevelUtils.cs',
                 ROOT/'unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterPropertyProgress.cs']:
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
    (ROOT/'assets/reference/home-experience-environment-source-manifest.json').write_text(json.dumps(dict(
        manifest='884_156692', inputs=inputs, outputs=outputs), ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
    print('Prepared local home experience environment bindings.', flush=True)


if __name__ == '__main__':
    prepare()
