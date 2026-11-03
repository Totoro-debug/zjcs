"""Run graph scheduling/lifecycle checks alongside the real full-window fixture."""
import hashlib
import json
from pathlib import Path
import re
import shutil

from official_apk import ROOT
from prepare_partner_conversation_window_validation import main as prepare_window, replace_once
from validation_workspace import validation_directory
from prepare_partner_star_god_validation import copy_modules


def main():
    prepare_window()
    work = validation_directory()
    project = work / 'star-god-unity'
    copy_partner_gift_configuration(project)
    # The gift catalog holds a native prefab whose CountRedDotImage/PreviewTool
    # bindings live in this shared original window assembly. Include its code
    # closure even though no C# reference from the panel reaches those scripts.
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    modules = {json.loads(path.read_bytes())['name']: path for path in runtime.rglob('*.asmdef')}
    pending, selected = ['Zjcs.Official.World.Map13MiniMapWindows'], set()
    while pending:
        name = pending.pop()
        if name in selected or name not in modules: continue
        selected.add(name)
        pending.extend(json.loads(modules[name].read_bytes()).get('references', []))
    copy_modules(project, runtime, modules, selected)
    packages = project / 'Packages/manifest.json'
    package_data = json.loads(packages.read_bytes())
    root_packages = json.loads((ROOT / 'unity/Packages/manifest.json').read_bytes())
    package_data['dependencies']['com.unity.modules.tilemap'] = root_packages['dependencies']['com.unity.modules.tilemap']
    packages.write_text(json.dumps(package_data, indent=2) + '\n', 'utf8', newline='\n')
    receipt = json.loads((ROOT / 'assets/reference/partner-tipdot-runtime-source-manifest.json').read_bytes())
    for relative, expected in receipt['outputs'].items():
        source = ROOT / relative
        if hashlib.sha256(source.read_bytes()).hexdigest() != expected: raise ValueError('Runtime source changed: ' + relative)
        target = project / source.relative_to(ROOT / 'unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    source = ROOT / 'scripts/fixtures/partner_tipdot_runtime_validation.cs'
    shutil.copy2(source, project / 'Assets/Editor/PartnerTipDotRuntimeValidation.cs')
    fixture = project / 'Assets/Editor/PartnerPanelHostValidation.cs'
    text = fixture.read_text('utf8')
    text = replace_once(text, '    IEnumerator Check()\n    {',
        '    IEnumerator Check()\n    {\n        yield return PartnerTipDotRuntimeValidation.Check();\n'
        '        Require(PartnerTipDotRuntimeValidation.Passed, "Original graph runtime checks completed");')
    fixture.write_text(text, 'utf8', newline='\n')
    print('Prepared original graph lifecycle/scheduling with actual full-window regression')


def copy_partner_gift_configuration(project):
    receipts = [json.loads((ROOT / relative).read_bytes()) for relative in (
        'assets/reference/partner-gift-configuration-source-manifest.json',
        'assets/reference/partner-properties-source-manifest.json',
        'assets/reference/partner-tipdot-catalog-source-manifest.json')]
    index = {}
    for meta in (ROOT / 'unity/Assets').rglob('*.meta'):
        source = Path(str(meta)[:-5])
        if not source.is_file():
            continue
        match = re.search(rb'(?m)^guid: ([0-9a-f]{32})', meta.read_bytes())
        if match:
            index[match[1]] = source
    package_guids = set()
    for root in (ROOT / 'unity/Packages', ROOT / 'unity/Library/PackageCache'):
        if not root.exists():
            continue
        for meta in root.rglob('*.meta'):
            match = re.search(rb'(?m)^guid: ([0-9a-f]{32})', meta.read_bytes())
            if match:
                package_guids.add(match[1])
    pending = [ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerGiftAssetCatalog.asset',
        ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerTipDotAssetCatalog.asset']
    copied = set()
    while pending:
        source = pending.pop()
        if source in copied:
            continue
        copied.add(source)
        relative = source.relative_to(ROOT).as_posix()
        raw = source.read_bytes()
        known = [receipt.get('outputs', {}).get(relative) or receipt.get('inputs', {}).get(relative)
            for receipt in receipts]
        known = [value for value in known if value]
        if known and hashlib.sha256(raw).hexdigest() not in known:
            raise ValueError('Partner gift configuration source differs: ' + relative)
        for path in (source, Path(str(source) + '.meta')):
            target = project / path.relative_to(ROOT / 'unity')
            target.parent.mkdir(parents=True, exist_ok=True)
            data = path.read_bytes()
            if target.exists():
                current = target.read_bytes()
                if current != data and current.splitlines() != data.splitlines():
                    raise ValueError('Preserve differing partner gift fixture asset: ' + str(target))
            else:
                shutil.copy2(path, target)
        if raw.startswith(b'%YAML'):
            for guid in set(re.findall(rb'guid: ([0-9a-f]{32})', raw)):
                if guid.startswith(b'000000') or guid in package_guids:
                    continue
                dependency = index.get(guid)
                if dependency is None:
                    raise ValueError('Unbound partner gift fixture asset GUID: ' + guid.decode())
                pending.append(dependency)
    print('Copied original partner gift configuration closure:', len(copied), 'assets', flush=True)


if __name__ == '__main__': main()
