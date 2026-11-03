"""Add talent predicates to the existing owned full original partner window run."""
import json
from pathlib import Path
import shutil

from official_apk import ROOT
from prepare_partner_conversation_window_validation import replace_once
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    project = work / 'star-god-unity'
    if not (project / 'Assets/Editor/PartnerTipDotCatalogValidation.cs').is_file():
        from prepare_partner_tipdot_catalog_validation import main as prepare_catalog
        prepare_catalog()
    receipt = json.loads((ROOT / 'assets/reference/partner-talent-source-manifest.json').read_bytes())
    # This warmed window project previously parsed configuration directly. Add
    # the production Resources bindings used by this check, preserving GUIDs.
    bound = ['unity/Assets/Game/Resources/Partners/' + name + '.asset' for name in (
        'OfficialPartnerAssetCatalog', 'OfficialPartnerGiftAssetCatalog')]
    bound += ['unity/Assets/Official/Partners/' + name for name in (
        'partner-rules.json', 'partner-properties.json', 'partner-custom-characters.txt')]
    for relative in bound:
        for suffix in ('', '.meta'):
            path = ROOT / (relative + suffix)
            target = project / path.relative_to(ROOT / 'unity')
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)
    for relative, expected in receipt['outputs'].items():
        import hashlib
        path = ROOT / relative
        if hashlib.sha256(path.read_bytes()).hexdigest() != expected: raise ValueError('Talent source changed: ' + relative)
        target = project / path.relative_to(ROOT / 'unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, target)
    shutil.copy2(ROOT / 'scripts/fixtures/partner_talent_validation.cs', project / 'Assets/Editor/PartnerTalentValidation.cs')
    path = project / 'Assets/Editor/PartnerPanelHostValidation.cs'
    text = path.read_text('utf8')
    if '        PartnerTalentValidation.Check();' not in text:
        text = replace_once(text, '        PartnerTipDotCatalogValidation.Check();',
            '        PartnerTalentValidation.Check();\n        PartnerTipDotCatalogValidation.Check();')
        path.write_text(text, 'utf8', newline='\n')
    print('Prepared original partner talent predicates in the warmed full window project')


if __name__ == '__main__': main()
