"""Prepare current UI appearance with actual original avatar and character assets."""
from concurrent.futures import ThreadPoolExecutor
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess

from official_apk import ROOT
from prepare_partner_player_portrait_validation import main as prepare_portraits
from prepare_partner_main_player_validation import main as prepare_profile
from prepare_partner_star_god_validation import copy_modules
from validation_workspace import validation_directory


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--reuse-inputs', action='store_true', help='Reuse this owned workspace\'s prepared portrait/profile inputs')
    args = parser.parse_args()
    work = validation_directory()
    if not (work / '.zjcs-validation.json').is_file():
        raise ValueError('Owned validation workspace required')
    if args.reuse_inputs:
        if not (work / 'portrait-validation-inputs.json').is_file():
            raise ValueError('No prepared owned inputs to reuse')
    else:
        prepare_portraits()
        prepare_profile()
    project = work / 'unity'
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    modules = {json.loads(path.read_bytes())['name']: path for path in runtime.rglob('*.asmdef')}
    selected, pending = set(), ['Zjcs.Offline.PartnersUI', 'Coffee.UIParticle']
    while pending:
        name = pending.pop()
        if name not in modules or name in selected:
            continue
        selected.add(name)
        pending.extend(json.loads(modules[name].read_bytes()).get('references', []))
    copy_modules(project, runtime, modules, selected)
    print('Copied current UI application/native ports', flush=True)
    high = json.loads((ROOT / 'assets/reference/partner-ui-high-textures-source-manifest.json').read_bytes())
    if high['manifest'] != '884_156692':
        raise ValueError('Use the selected original High loader')
    for row in high['outputs']:
        source = ROOT / row['path']
        if hashlib.sha256(source.read_bytes()).hexdigest() != row['sha256']:
            raise ValueError('Prepared High texture differs: ' + str(source))
        target = project / source.relative_to(ROOT / 'unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    print('Copied verified complete original High loader and native texture outputs', flush=True)
    paths = subprocess.run(['git', 'ls-files', '-z', 'unity/Assets'], cwd=ROOT,
                           check=True, capture_output=True).stdout
    def entry(relative):
        if not relative.endswith(b'.meta'):
            return None
        meta = ROOT / relative.decode()
        source = Path(str(meta)[:-5])
        if not source.is_file():
            return None
        match = re.search(rb'(?m)^guid: ([0-9a-f]{32})', meta.read_bytes())
        return (match[1], source) if match else None
    with ThreadPoolExecutor(max_workers=16) as pool:
        index = dict(value for value in pool.map(entry, filter(None, paths.split(b'\0'))) if value)
    print('Indexed original avatar dependency GUIDs', flush=True)
    pending = [ROOT / 'unity/Assets/Official/Partners/UI/Panel/NpcPanel.prefab',
        ROOT / 'unity/Assets/Official/Partners/Appearance/Resources/Partners/appearance-asset-manager-config.asset']
    visited = set()
    while pending:
        source = pending.pop()
        if source in visited:
            continue
        visited.add(source)
        if source.suffix in ('.cs', '.asmdef'):
            continue
        for path in (source, Path(str(source) + '.meta')):
            target = project / path.relative_to(ROOT / 'unity')
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)
        raw = source.read_bytes()
        if raw.startswith(b'%YAML'):
            pending.extend(index[key] for key in set(re.findall(rb'guid: ([0-9a-f]{32})', raw)) if key in index)
    # The installed prefab now uses the selected current application bindings.
    # Copy that exact production prefab; do not repair it only in the fixture.
    prefab = project / 'Assets/Official/Partners/UI/Panel/NpcPanel.prefab'
    text = prefab.read_text('utf8')
    count = {}
    for name in ('AppearanceCharacter', 'AppearanceUI', 'PlayerAnimationAppearance'):
        def guid(folder):
            source = runtime / 'Official' / folder / (name + '.cs.meta')
            return re.search(r'(?m)^guid: ([0-9a-f]{32})', source.read_text('utf8'))[1]
        old, new = guid('ProfessionUI'), guid('CombatAppearance')
        count[name] = text.count('guid: ' + new)
        if count[name] != 6 or ('guid: ' + old) in text:
            raise ValueError('Bind the installed original avatars to the selected current application: ' + name)
    editor = project / 'Assets/Editor'
    references = selected | {'Unity.ugui', 'Unity.TextMeshPro',
        'Unity.RenderPipelines.Universal.Runtime', 'Unity.RenderPipelines.Core.Runtime'}
    (editor / 'Validation.asmdef').write_text(json.dumps(dict(name='PartnerMainAvatarValidation',
        includePlatforms=['Editor'], references=sorted(references))), 'utf8', newline='\n')
    fixture = ROOT / 'scripts/fixtures/partner_main_avatar_validation.cs'
    if fixture.exists():
        shutil.copy2(fixture, editor / 'PartnerMainAvatarValidation.cs')
    print('Prepared actual avatar dependency closure and selected current script bindings:', count, flush=True)


if __name__ == '__main__':
    main()
