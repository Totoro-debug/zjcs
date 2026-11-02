"""Prepare the original star-god page against real local saves and services."""
import hashlib
import json
from pathlib import Path
import re
import shutil
import sys

from official_apk import ROOT
from prepare_partner_star_god_validation import main as prepare_page, copy_modules
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    if not (work / 'star-god-unity').exists(): prepare_page()
    project = work / 'star-god-unity'
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    modules = {json.loads(path.read_bytes())['name']: path for path in runtime.rglob('*.asmdef')}
    pending, selected = ['Zjcs.Offline.PartnersUI'], set()
    while pending:
        name = pending.pop()
        if name in selected or name not in modules: continue
        selected.add(name)
        pending.extend(json.loads(modules[name].read_bytes()).get('references', []))
    copy_modules(project, runtime, modules, selected)
    editor = project / 'Assets/Editor'
    for name in ('OfficialPartnerStarGodAssetCatalog.cs', 'OfficialPartnerStarGodAssetCatalog.cs.meta', 'PartnerStarGodValidation.cs'):
        (editor / name).unlink(missing_ok=True)
    shutil.copy2(ROOT / 'scripts/fixtures/partner_star_god_local_validation.cs', editor / 'PartnerStarGodLocalValidation.cs')
    assembly = editor / 'Validation.asmdef'
    value = json.loads(assembly.read_bytes())
    value['references'] = list(dict.fromkeys(value['references'] + ['Zjcs.Offline.PartnersUI', 'Zjcs.Offline.Progression', 'Zjcs.Offline.State']))
    assembly.write_text(json.dumps(value), 'utf8', newline='\n')
    if '--sync-runtime' in sys.argv:
        for meta in (project / 'Assets/Game/Runtime').rglob('*.meta'):
            if not Path(str(meta)[:-5]).exists(): meta.unlink()
        print('Refreshed real runtime sources, assembly references and fixture', flush=True)
        return
    # Copy the actual gift ItemIcon context configuration and its serialized
    # dependency closure. Keep original installed paths/GUIDs and source bytes.
    index = {}
    for meta in (ROOT / 'unity/Assets').rglob('*.meta'):
        source = Path(str(meta)[:-5])
        if not source.is_file(): continue
        match = re.search(rb'(?m)^guid: ([0-9a-f]{32})', meta.read_bytes())
        if match: index[match[1].decode()] = source
    package_guids = set()
    for root in (ROOT / 'unity/Packages', ROOT / 'unity/Library/PackageCache'):
        for package in root.iterdir():
            if not package.is_dir() or not package.name.startswith(('com.unity.textmeshpro', 'com.unity.ugui',
                'com.unity.render-pipelines.', 'com.unity.shadergraph')): continue
            for meta in package.rglob('*.meta'):
                match = re.search(rb'(?m)^guid: ([0-9a-f]{32})', meta.read_bytes())
                if match: package_guids.add(match[1])
    pending = [ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerGiftPageAssetCatalog.asset']
    visited, copied = set(), {}
    while pending:
        source = pending.pop()
        if source in visited: continue
        visited.add(source)
        if source.suffix in ('.cs', '.asmdef'): continue  # Runtime assemblies already copied.
        for left in (source, Path(str(source) + '.meta')):
            target = project / left.relative_to(ROOT / 'unity')
            target.parent.mkdir(parents=True, exist_ok=True)
            if target.exists() and target.read_bytes() != left.read_bytes(): raise ValueError('Preserve differing fixture asset: ' + str(target))
            shutil.copy2(left, target)
            copied[left.relative_to(ROOT).as_posix()] = hashlib.sha256(left.read_bytes()).hexdigest()
        raw = source.read_bytes()
        if raw.startswith(b'%YAML'):
            for key in set(re.findall(rb'guid: ([0-9a-f]{32})', raw)):
                if key.startswith(b'000000'): continue
                if key in package_guids: continue  # Resolved by the fixture's actual Unity packages.
                if key.decode() not in index: raise ValueError('Unbound fixture asset GUID: ' + key.decode() + ' in ' + str(source))
                pending.append(index[key.decode()])
    resource = project / 'Assets/Resources/Map14'
    resource.mkdir(parents=True, exist_ok=True)
    shutil.copy2(ROOT / 'unity/Assets/Game/Resources/Map14/world-alone-preparation.json', resource)
    for meta in (project / 'Assets/Game/Runtime').rglob('*.meta'):
        if not Path(str(meta)[:-5]).exists(): meta.unlink()
    (work / 'star-god-local-inputs.json').write_text(json.dumps(copied, indent=2), 'utf8', newline='\n')
    print('Prepared real star-god services,', len(selected), 'runtime assemblies and', len(copied), 'item asset files', flush=True)


if __name__ == '__main__': main()
