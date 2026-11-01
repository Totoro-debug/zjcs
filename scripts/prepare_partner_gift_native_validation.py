"""Import staged original friendship prefabs into their owned Unity project."""
import json
import os
from pathlib import Path
import re
import shutil

from prepare_partner_roster_ui_validation import prepare, ROOT
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    if Path(os.environ['ZJCS_REPOSITORY_ROOT']).resolve() != ROOT:
        raise ValueError('Repository validation context required')
    project = work / 'unity'
    if not project.is_dir():
        prepare(work)
        (project / 'Assets/Editor/PartnerRosterUiValidation.cs').unlink()
    for relative in ('Official/PartnersUI/Gifts', 'Official/CombatAppearance', 'Official/World/NativeLabelUI',
                     'Official/World/NativeText', 'Presentation/Opening/OfficialCoffeeUIParticle'):
        shutil.copytree(ROOT / 'unity/Assets/Game/Runtime' / relative,
                        project / 'Assets/Game/Runtime' / relative, dirs_exist_ok=True)
    shutil.copytree(work / 'gift-assets', project / 'Assets/PartnerGift', dirs_exist_ok=True)
    card = ROOT / 'unity/Assets/Official/Partners/UI/Card'
    font = json.loads((card / 'partner-card-images.json').read_bytes())['font']
    font_guid = font['source_font']['guid']
    source, = [path for path in card.rglob('*.meta') if re.search(r'(?m)^guid: ' + font_guid + '$', path.read_text('utf8'))]
    shutil.copy2(Path(str(source)[:-5]), project / 'Assets/Editor/FZY4K.ttf')
    shutil.copy2(source, project / 'Assets/Editor/FZY4K.ttf.meta')
    shutil.copy2(ROOT / 'scripts/fixtures/partner_gift_native_validation.cs', project / 'Assets/Editor/PartnerGiftNativeValidation.cs')
    assembly = project / 'Assets/Editor/Validation.asmdef'
    data = json.loads(assembly.read_bytes())
    data['references'] = sorted(set(data['references']) | {'Zjcs.Official.PartnerGiftUI', 'Zjcs.Official.World.NativeLabelUI',
                                                         'Zjcs.Official.World.NativeText', 'Coffee.UIParticle',
                                                         'Unity.RenderPipelines.Universal.Runtime', 'Unity.RenderPipelines.Core.Runtime'})
    assembly.write_text(json.dumps(data, indent=2), 'utf8')
    packages = project / 'Packages'
    urp = packages / 'com.unity.render-pipelines.universal'
    if not urp.is_dir():
        shutil.copytree(ROOT / 'unity/Packages/com.unity.render-pipelines.universal', urp)
    manifest = packages / 'manifest.json'
    data = json.loads(manifest.read_bytes())
    data['dependencies']['com.unity.render-pipelines.universal'] = 'file:com.unity.render-pipelines.universal'
    manifest.write_text(json.dumps(data, indent=2), 'utf8')
    print(project)


if __name__ == '__main__':
    main()
