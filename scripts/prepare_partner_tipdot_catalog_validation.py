"""Verify actual original graph/configuration identities in the full native window."""
import hashlib
import json
from pathlib import Path
import shutil

from official_apk import ROOT
from prepare_partner_tipdot_runtime_validation import main as prepare_runtime
from prepare_partner_conversation_window_validation import replace_once
from validation_workspace import validation_directory


def main():
    prepare_runtime()
    work = validation_directory()
    project = work / 'star-god-unity'
    receipt = json.loads((ROOT / 'assets/reference/partner-tipdot-catalog-source-manifest.json').read_bytes())
    for relative, expected in receipt['outputs'].items():
        source = ROOT / relative
        if hashlib.sha256(source.read_bytes()).hexdigest() != expected: raise ValueError('Selected catalog output changed: ' + relative)
        target = project / source.relative_to(ROOT / 'unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    helper = ROOT / 'scripts/fixtures/partner_tipdot_catalog_validation.cs'
    shutil.copy2(helper, project / 'Assets/Editor/PartnerTipDotCatalogValidation.cs')
    fixture = project / 'Assets/Editor/PartnerPanelHostValidation.cs'
    text = fixture.read_text('utf8')
    text = replace_once(text, '        yield return PartnerTipDotRuntimeValidation.Check();',
        '        PartnerTipDotCatalogValidation.Check();\n        yield return PartnerTipDotRuntimeValidation.Check();')
    fixture.write_text(text, 'utf8', newline='\n')
    print('Prepared actual selected configuration and native graph import checks')


if __name__ == '__main__': main()
