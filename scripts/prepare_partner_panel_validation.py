"""Prepare real panel assets and the independent native Canvas backend."""
import hashlib
import json
from pathlib import Path
import re
import shutil
import sys

from official_apk import ROOT
from prepare_partner_star_god_local_validation import main as prepare_local
from prepare_partner_star_god_validation import copy_modules
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    project = work / 'star-god-unity'
    if not project.exists(): prepare_local()
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    modules = {json.loads(path.read_bytes())['name']: path for path in runtime.rglob('*.asmdef')}
    pending, selected = ['Zjcs.Offline.PartnersUI', 'Zjcs.Official.TeamUI'], set()
    while pending:
        name = pending.pop()
        if name in selected or name not in modules: continue
        selected.add(name); pending.extend(json.loads(modules[name].read_bytes()).get('references', []))
    copy_modules(project, runtime, modules, selected)
    editor = project / 'Assets/Editor'
    (editor / 'PartnerStarGodLocalValidation.cs').unlink(missing_ok=True)
    shutil.copy2(ROOT / 'scripts/fixtures/partner_panel_native_graphic_validation.cs', editor / 'PartnerPanelNativeGraphicValidation.cs')
    definition = editor / 'Validation.asmdef'
    value = json.loads(definition.read_bytes())
    value['references'] = list(dict.fromkeys(value['references'] + ['Zjcs.Official.TeamUI']))
    definition.write_text(json.dumps(value), 'utf8', newline='\n')
    for meta in (project / 'Assets/Game/Runtime').rglob('*.meta'):
        if not Path(str(meta)[:-5]).exists(): meta.unlink()
    if '--sync-runtime' in sys.argv:
        print('Refreshed original panel runtime assemblies and native graphic fixture', flush=True); return
    index = {}
    for meta in (ROOT / 'unity/Assets').rglob('*.meta'):
        source = Path(str(meta)[:-5])
        if not source.is_file(): continue
        match = re.search(rb'(?m)^guid: ([0-9a-f]{32})', meta.read_bytes())
        if match: index[match[1]] = source
    package_guids = set()
    for folder in (ROOT / 'unity/Packages', ROOT / 'unity/Library/PackageCache'):
        for package in folder.iterdir():
            if package.is_dir() and package.name.startswith(('com.unity.textmeshpro', 'com.unity.ugui', 'com.unity.render-pipelines.', 'com.unity.shadergraph')):
                for meta in package.rglob('*.meta'):
                    match = re.search(rb'(?m)^guid: ([0-9a-f]{32})', meta.read_bytes())
                    if match: package_guids.add(match[1])
    pending = [ROOT / ('unity/Assets/Game/Resources/Partners/' + name + '.asset') for name in
        ('OfficialPartnerPanelAssetCatalog', 'OfficialPartnerRosterAssetCatalog')]
    pending += [ROOT / ('unity/Assets/Game/Resources/Partners/Portraits/Npc' + str(id) + '.asset') for id in (8401, 10412)]
    visited, copied = set(), {}
    while pending:
        source = pending.pop()
        if source in visited: continue
        visited.add(source)
        if source.suffix in ('.cs', '.asmdef'): continue
        for path in (source, Path(str(source) + '.meta')):
            target = project / path.relative_to(ROOT / 'unity'); target.parent.mkdir(parents=True, exist_ok=True)
            if target.exists():
                current = target.read_bytes()
                source_bytes = path.read_bytes()
                if current != source_bytes and current.splitlines() != source_bytes.splitlines():
                    raise ValueError('Preserve differing fixture input: ' + str(target))
            else:
                shutil.copy2(path, target)
            copied[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
        raw = source.read_bytes()
        if raw.startswith(b'%YAML'):
            for key in set(re.findall(rb'guid: ([0-9a-f]{32})', raw)):
                if key.startswith(b'000000') or key in package_guids: continue
                if key not in index: raise ValueError('Unbound original panel asset: ' + key.decode() + ' in ' + str(source))
                pending.append(index[key])
    (work / 'panel-validation-inputs.json').write_text(json.dumps(copied, indent=2), 'utf8', newline='\n')
    print('Prepared complete original panel dependency closure:', len(copied), 'files;', len(selected), 'runtime assemblies', flush=True)


if __name__ == '__main__': main()
