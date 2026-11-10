"""Compile and execute original home condition checks without a desktop window."""
import argparse
import os
from pathlib import Path
import subprocess

from official_apk import ROOT
from validation_workspace import validation_directory


def main():
    parser=argparse.ArgumentParser(__doc__)
    parser.add_argument('--editor',type=Path,default=Path('D:/Tool/Unity/Editor/Unity.exe'))
    args=parser.parse_args()
    work=validation_directory()
    stage=work/'home-conditions'
    data=args.editor.parent/'Data'
    references=data/'UnityReferenceAssemblies/unity-4.8-api'
    program=stage/'HomeConditionValidation.exe'
    command=[str(ROOT/'.tools/dotnet/dotnet.exe'),str(data/'DotNetSdkRoslyn/csc.dll'),
        '-nologo','-target:exe','-out:'+str(program),'-nostdlib']
    command+=['-r:'+str(references/name) for name in
        ('mscorlib.dll','System.dll','System.Core.dll','Facades/netstandard.dll')]
    command+=['-r:'+str(data/'Managed/Newtonsoft.Json.dll')]
    command+=[str(path) for path in (stage/'HomeConditionRules.cs',
        ROOT/'unity/Assets/Game/Runtime/Official/Partners/PartnerConditionRules.cs',
        ROOT/'unity/Assets/Game/Runtime/Official/Partners/NpcRosterRules.cs',
        ROOT/'scripts/fixtures/home_condition_validation.cs')]
    options=dict(cwd=ROOT,creationflags=subprocess.CREATE_NO_WINDOW if os.name=='nt' else 0)
    with (work/'home-condition-compile.log').open('w',encoding='utf8') as log:
        subprocess.run(command,check=True,stdout=log,stderr=subprocess.STDOUT,**options)
    environment=dict(os.environ,MONO_PATH=str(data/'Managed'))
    with (work/'home-condition-validation.log').open('w',encoding='utf8') as log:
        subprocess.run([str(data/'MonoBleedingEdge/bin/mono.exe'),str(program),str(ROOT),str(work)],
            check=True,env=environment,stdout=log,stderr=subprocess.STDOUT,**options)
    print((work/'home-condition-validation.log').read_text('utf8'))


if __name__=='__main__':
    main()
