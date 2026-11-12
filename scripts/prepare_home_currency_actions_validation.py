"""Stage original selected currency actions and native pointer validation."""
import argparse
import json
from pathlib import Path
import shutil
from prepare_home_auto_increase_runtime import ROOT, sha
from prepare_home_currency_actions_runtime import build
from validation_workspace import validation_directory


def stage(project):
    assert project.resolve().is_relative_to(validation_directory().resolve())
    receipt=build()
    for relative,expected in receipt['outputs'].items():
        source=ROOT/relative;assert sha(source)==expected,relative
        target=project/source.relative_to(ROOT/'unity');target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
    fixture=ROOT/'scripts/fixtures/home_currency_actions_native_validation.cs'
    folder=project/'Assets/HomeCurrencyActionsNativeValidation';folder.mkdir(exist_ok=True)
    shutil.copy2(fixture,folder/'HomeCurrencyActionsNativeValidation.cs')
    references=['Unity.ugui','Unity.TextMeshPro','Zjcs.Official.HomeHudUI','Zjcs.Official.ProfessionUI',
        'Zjcs.Offline.HomeUI','Zjcs.Offline.HomeHudUI','Zjcs.Official.HomeCurrencyActionsUI']
    (folder/'HomeCurrencyActionsNativeValidation.asmdef').write_text(json.dumps(dict(name='Zjcs.HomeCurrencyActionsNativeValidation',includePlatforms=['Editor'],references=references),indent=2)+'\n','utf8',newline='\n')
    receipt['binding_generator_sha256']=sha(Path(__file__))
    receipt['binding_inputs']={fixture.relative_to(ROOT).as_posix():sha(fixture)}
    (ROOT/'assets/reference/home-currency-actions-runtime-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')

if __name__=='__main__':
    parser=argparse.ArgumentParser(__doc__);parser.add_argument('--project',type=Path,required=True);stage(parser.parse_args().project)
