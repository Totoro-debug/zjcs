"""Stage current home rules into an owned Unity validation project."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

from official_apk import ROOT
from validation_workspace import validation_directory
from validation_assemblies import stage_runtime_modules


def main():
    parser=argparse.ArgumentParser(__doc__)
    parser.add_argument('--isolated',action='store_true')
    args=parser.parse_args()
    work=validation_directory()
    project=work/('home-conditions-unity' if args.isolated else 'unity')
    if not (work/'.zjcs-validation.json').is_file():
        raise ValueError('An owned Unity validation workspace is required')
    if args.isolated:
        if project.exists():
            raise FileExistsError('Keep the existing isolated home validation project')
        project.mkdir()
        for folder in ('Packages','ProjectSettings'):
            shutil.copytree(ROOT/'unity'/folder,project/folder)
        stage_runtime_modules(project,ROOT/'unity/Assets/Game/Runtime',['Zjcs.Offline.HomeUI'])
        shutil.copytree(ROOT/'unity/Assets/Spine/Runtime',project/'Assets/Spine/Runtime')
        for relative in ('Assets/Spine.meta','Assets/Spine/Runtime.meta'):
            shutil.copy2(ROOT/'unity'/relative,project/relative)
    elif not (project/'Assets/Game/Editor/ProjectTools.cs').is_file():
        raise ValueError('An existing owned full Unity validation project is required')
    for name in ('home-rules-current-source-manifest.json','home-condition-source-manifest.json'):
        receipt=json.loads((ROOT/'assets/reference'/name).read_bytes())
        for relative,expected in receipt['installed_outputs'].items():
            source=ROOT/relative
            if hashlib.sha256(source.read_bytes()).hexdigest()!=expected:
                raise ValueError('Edited home validation input: '+relative)
            target=project/Path(relative).relative_to('unity');target.parent.mkdir(parents=True,exist_ok=True)
            shutil.copy2(source,target)
    for relative in ('Assets/Game/Runtime/Offline/HomeUI/Zjcs.Offline.HomeUI.asmdef',):
        shutil.copy2(ROOT/'unity'/relative,project/relative)
    editor=project/'Assets/Editor/HomeConditions';editor.mkdir(parents=True,exist_ok=True)
    shutil.copy2(ROOT/'scripts/fixtures/home_condition_validation.cs',editor/'HomeConditionValidation.cs')
    (editor/'HomeConditionsValidation.asmdef').write_text(json.dumps(dict(name='HomeConditionsValidation',
        includePlatforms=['Editor'],references=['Zjcs.Official.HomeConditions','Zjcs.Official.Partners',
            'Zjcs.Offline.HomeUI','Unity.Newtonsoft.Json']),indent=2)+'\n','utf8',newline='\n')


if __name__=='__main__':
    main()
