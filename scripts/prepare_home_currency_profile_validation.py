"""Stage real-profile currency recovery and original native top-bar validation."""
import argparse
import json
from pathlib import Path
import shutil

from official_apk import ROOT
from validation_workspace import validation_directory
from validation_assemblies import stage_runtime_modules


def stage(project):
    work = validation_directory()
    if not project.resolve().is_relative_to(work.resolve()):
        raise ValueError('Owned isolated validation project required.')
    stage_runtime_modules(project,ROOT/'unity/Assets/Game/Runtime',
        ['Zjcs.Offline.HomeUI','Zjcs.Offline.CharacterCreation','Zjcs.Official.HomePlayerInfoUI'])
    folder = project/'Assets/HomeCurrencyProfileValidation'
    folder.mkdir(parents=True,exist_ok=True)
    shutil.copyfile(ROOT/'scripts/fixtures/home_currency_profile_validation.cs',folder/'HomeCurrencyProfileValidation.cs')
    refs = ['Zjcs.Offline.HomeUI','Zjcs.Offline.CharacterCreation','Zjcs.Offline.Progression',
        'Zjcs.Offline.State','Zjcs.Official.HomeHudUI','Zjcs.Official.HomeCurrencyUI',
        'Zjcs.Official.HomePlayerInfoUI','Zjcs.Official.HomeAutoIncrease','Zjcs.Official.ProfessionUI',
        'Unity.TextMeshPro','Unity.ugui']
    (folder/'HomeCurrencyProfileValidation.asmdef').write_text(json.dumps(dict(
        name='Zjcs.HomeCurrency.ProfileValidation',references=refs,includePlatforms=['Editor']),indent=2)+'\n','utf8',newline='\n')
    for relative in ['Assets/Game/Resources/Home/item-auto-increase.json']:
        for suffix in ('','.meta'):
            source = ROOT/'unity'/(relative+suffix)
            target = project/(relative+suffix)
            target.parent.mkdir(parents=True,exist_ok=True)
            shutil.copy2(source,target)
    print('Real-profile currency fixture staged:',project,flush=True)


if __name__ == '__main__':
    parser=argparse.ArgumentParser(__doc__)
    parser.add_argument('--project',type=Path,required=True)
    stage(parser.parse_args().project)
