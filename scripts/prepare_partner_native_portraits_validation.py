"""Stage current portrait geometry validation in an owned imported Unity cache."""
import argparse
import hashlib
import json
import shutil
from pathlib import Path

from validation_workspace import require_temporary_unity_project, validation_directory

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--project', required=True, type=Path)
    project = require_temporary_unity_project(parser.parse_args().project)
    validation_directory()
    for relative in (
        'Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSkeletonData.PartnerPortrait.cs',
        'Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSkeletonData.PartnerPortrait.cs.meta',
        'Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeIdleTransform.cs',
        'Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeIdleIk.cs',
        'Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeBonePose.cs',
        'Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSkeletonData.cs',
        'Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeAnimationChannels.cs',
        'Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeDeformAnimation.cs',
        'Assets/Game/Runtime/Offline/PartnersUI/PartnerNativePortraitPose.cs',
        'Assets/Game/Runtime/Offline/PartnersUI/PartnerNativePortraitPose.cs.meta',
        'Assets/Game/Runtime/Offline/PartnersUI/PartnerNativePortraitIk.cs',
        'Assets/Game/Runtime/Offline/PartnersUI/PartnerNativePortraitIk.cs.meta',
        'Assets/Game/Runtime/Offline/PartnersUI/OfficialPartnerPortraitAsset.cs',
        'Assets/Game/Runtime/Offline/PartnersUI/OfficialPartnerPortraitAsset.cs.meta',
    ):
        target = project/relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(ROOT/'unity'/relative, target)
    editor = project/'Assets/PartnerCurrentValidation/Editor'
    if not (editor/'PartnerCurrentValidation.asmdef').is_file():
        raise ValueError('Reuse the owned current partner validation cache')
    shutil.copy2(ROOT/'scripts/fixtures/partner_native_portraits_validation.cs',editor/'PartnerNativePortraitsValidation.cs')
    if (project/'Assets/PartnerCurrentValidation/Portrait/SkeletonGraphicDefault.mat').is_file():
        shutil.copy2(ROOT/'scripts/fixtures/partner_portrait_graphics_validation.cs',editor/'PartnerPortraitGraphicsValidation.cs')
    receipt=ROOT/'assets/reference/partner-portrait-assets-source-manifest.json'
    if receipt.is_file():
        for relative,expected in json.loads(receipt.read_bytes())['installed_output_sha256'].items():
            raw=(ROOT/relative).read_bytes()
            if hashlib.sha256(raw).hexdigest()!=expected:raise ValueError('Installed portrait changed: '+relative)
            target=project/Path(relative).relative_to('unity')
            target.parent.mkdir(parents=True,exist_ok=True)
            if not target.is_file() or target.read_bytes()!=raw:target.write_bytes(raw)
    print('Prepared original portrait packages/native geometry validation in',project)


if __name__ == '__main__':
    main()
