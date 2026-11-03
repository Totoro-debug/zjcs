"""Validate the real three-page window; dialogue scope is a new empty profile."""
import hashlib
import json
from pathlib import Path
import shutil
import sys

from official_apk import ROOT
from partner_panel_language import panel_language
from prepare_partner_panel_validation import main as prepare_panel
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    project = work / 'star-god-unity'
    if not project.exists() or '--sync-runtime' in sys.argv: prepare_panel()
    # This owned fixture can advance the one runtime catalog after its original
    # dependency-copy receipt proves the previous bytes were not edited locally.
    copied_path = work / 'panel-validation-inputs.json'
    copied = json.loads(copied_path.read_bytes())
    relative = 'unity/Assets/Game/Resources/Partners/OfficialPartnerPanelAssetCatalog.asset'
    source = ROOT / relative
    target = project / source.relative_to(ROOT / 'unity')
    if target.read_bytes() != source.read_bytes():
        if hashlib.sha256(target.read_bytes()).hexdigest() != copied[relative]:
            raise ValueError('Preserve edited fixture panel catalog: ' + str(target))
        shutil.copy2(source, target)
        copied[relative] = hashlib.sha256(source.read_bytes()).hexdigest()
        copied_path.write_text(json.dumps(copied, indent=2), 'utf8', newline='\n')
    native = json.loads((ROOT / 'assets/reference/partner-panel-native-source-manifest.json').read_bytes())
    for relative, expected in native['installed_output_sha256'].items():
        if not relative.endswith(('.wav', '.wav.meta')): continue
        source = ROOT / relative
        target = project / source.relative_to(ROOT / 'unity')
        source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
        if source_hash != expected:
            if (relative.endswith('.meta') and target.exists()
                    and hashlib.sha256(target.read_bytes()).hexdigest() == expected
                    and target.read_bytes().splitlines() == source.read_bytes().splitlines()):
                continue
            raise ValueError('Panel PCM changed: ' + relative)
        if target.exists():
            current = target.read_bytes()
            source_bytes = source.read_bytes()
            if current != source_bytes and current.splitlines() != source_bytes.splitlines():
                raise ValueError('Preserve changed fixture PCM: ' + str(target))
        else:
            target.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(source, target)
    editor = project / 'Assets/Editor'
    for name in ('PartnerPanelNativeGraphicValidation.cs', 'PartnerStarGodLocalValidation.cs'):
        (editor / name).unlink(missing_ok=True)
    fixture = ROOT / 'scripts/fixtures/partner_panel_host_validation.cs'
    shutil.copy2(fixture, editor / 'PartnerPanelHostValidation.cs')
    texts, inputs = panel_language(ROOT / 'unity/Assets/Official/Partners/UI/Panel/NpcPanel.prefab')
    (editor / 'panel-words.json').write_text(json.dumps(dict(texts=texts),
        ensure_ascii=False), 'utf8', newline='\n')
    (work / 'panel-host-inputs.json').write_text(json.dumps(dict(inputs=inputs,
        fixture_sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()), indent=2), 'utf8', newline='\n')
    print('Prepared complete panel host fixture with', len(texts), 'original language entries', flush=True)


if __name__ == '__main__': main()
