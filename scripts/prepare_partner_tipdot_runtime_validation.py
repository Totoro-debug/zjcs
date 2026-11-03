"""Run graph scheduling/lifecycle checks alongside the real full-window fixture."""
import hashlib
import json
from pathlib import Path
import shutil

from official_apk import ROOT
from prepare_partner_conversation_window_validation import main as prepare_window, replace_once
from validation_workspace import validation_directory
from prepare_partner_star_god_validation import copy_modules


def main():
    prepare_window()
    work = validation_directory()
    project = work / 'star-god-unity'
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


if __name__ == '__main__': main()
