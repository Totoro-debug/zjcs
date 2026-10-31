"""Build an isolated real progression runtime and an unchanged official binary reader reference."""
from pathlib import Path
import shutil

from official_apk import ROOT
from official_source import OfficialSource
from prepare_partner_gift_validation import prepare
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    project = prepare(work)
    assets = project / 'Assets'
    source = ROOT / 'unity/Assets/Game/Resources/Partners'
    shutil.copytree(source, assets / 'Resources/Partners')
    for name in ('partner-rules.json', 'partner-rules.json.meta', 'partner-properties.json', 'partner-properties.json.meta'):
        destination = assets / 'Official/Partners' / name
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(ROOT / 'unity/Assets/Official/Partners' / name, destination)
    code = ROOT / '.tools/source-packages/current' / OfficialSource().version / 'official-code/common-source/Common'
    for name in ('LevelPropBinaryReader', 'PropType'):
        original = (code / (name + '.cs')).read_text('utf-8-sig')
        if original.count('namespace Common;') != 1:
            raise ValueError('Review changed reference namespace: ' + name)
        text = original.replace('namespace Common;', 'namespace PartnerPropertyReference.Common\n{') + '\n}\n'
        text = text.replace('using Framework.Logging', 'using PartnerPropertyReference.Framework.Logging')
        (assets / 'Editor' / ('Reference' + name + '.cs')).write_text(text, 'utf8', newline='\n')
    shutil.copy2(ROOT / 'scripts/fixtures/partner_property_catalog_validation.cs', assets / 'Editor/PartnerPropertyCatalogValidation.cs')
    print(project)


if __name__ == '__main__':
    main()
