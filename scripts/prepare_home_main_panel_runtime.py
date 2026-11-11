"""Restore the original main-panel animation lifecycle, including complete awaits."""
import json
from pathlib import Path
import re
import subprocess

from prepare_home_auto_increase_runtime import ROOT, CODE, sha, metadata

DEST=ROOT/'unity/Assets/Game/Runtime/Official/HomeMainPanelUI'
NAMES=('ESceneUIGraphicType','AnimatorUtils','MainUIPanelBase','MainUIPanelWithAnimator','HomeUI')


def build():
    inputs,outputs={},{}
    references=CODE.parent/'native-conversion/inputs/Managed'
    assembly=CODE/'managed/decoded/Assembly-CSharp.dll'
    ilspy=ROOT/'.tools/ilspycmd/tools/net10.0/any/ilspycmd.dll'
    dependencies={p.relative_to(ROOT).as_posix():sha(p) for p in [assembly,ilspy,*sorted(references.glob('*.dll'))]}
    inputs.update(dependencies)
    # Reuse the existing original base; do not create a second UI lifecycle.
    previous=ROOT/'.tools/source-packages/official-code/framework-firstpass-source/UIBase.cs'
    current=CODE/'framework-firstpass-source/UIBase.cs'
    assert previous.read_bytes()==current.read_bytes(),'Recheck the shared original UIBase version.'
    for file in (previous,current,ROOT/'unity/Assets/Game/Runtime/Official/ProfessionUI/UIBase.cs'):
        inputs[file.relative_to(ROOT).as_posix()]=sha(file)
    for name in NAMES:
        original=CODE/'client-source'/(name+'.cs')
        inputs[original.relative_to(ROOT).as_posix()]=sha(original)
        text=original.read_text('utf-8-sig')
        if name=='MainUIPanelWithAnimator':
            resolved=CODE/'client-source-resolved'/(name+'.cs')
            proof=resolved.with_suffix('.decompilation.json')
            if not resolved.exists():
                command=[str(ROOT/'.tools/dotnet/dotnet.exe'),str(ilspy),'-t',name,'-r',str(references),str(assembly)]
                result=subprocess.run(command,check=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
                raw=result.stdout.decode('utf-8-sig').replace('\r\n','\n')
                assert 'IAsyncStateMachine' not in raw and 'Unknown result type' not in raw
                resolved.write_text(raw,'utf8',newline='\n')
                proof.write_text(json.dumps(dict(inputs=dependencies,sha256=sha(resolved),command=command),indent=2)+'\n','utf8',newline='\n')
            evidence=json.loads(proof.read_bytes())
            assert evidence['inputs']==dependencies and evidence['sha256']==sha(resolved),name
            for file in (resolved,proof):inputs[file.relative_to(ROOT).as_posix()]=sha(file)
            text=resolved.read_text('utf8')
        assert 'Unknown result type' not in text,name
        using=list(re.finditer(r'^using [^\n]+;\n',text,re.M))
        index=using[-1].end() if using else 0
        text=text[:index]+'using UIBase = Zjcs.Official.ProfessionUI.UIBase;\nnamespace Zjcs.Official.HomeMainPanelUI\n{\n'+text[index:]+'\n}\n'
        text='\n'.join(line.rstrip() for line in text.splitlines()).rstrip()+'\n'
        path=DEST/(name+'.cs');path.parent.mkdir(parents=True,exist_ok=True)
        path.write_text('// Original main-panel implementation; namespace/base binding only.\n'+text,'utf8',newline='\n')
        for file in (path,metadata(path)):outputs[file.relative_to(ROOT).as_posix()]=sha(file)
    path=DEST/'Zjcs.Official.HomeMainPanelUI.asmdef'
    path.write_text(json.dumps(dict(name='Zjcs.Official.HomeMainPanelUI',references=['Zjcs.Official.ProfessionUI','Unity.ugui','UniTask']),indent=2)+'\n','utf8',newline='\n')
    for file in (path,metadata(path),metadata(DEST,True)):outputs[file.relative_to(ROOT).as_posix()]=sha(file)
    proof=dict(manifest='884_156692',generator_sha256=sha(Path(__file__)),inputs=inputs,outputs=outputs,
        scope='Complete original HomeUI/main-panel animation lifecycle and helper. MainUIRoot creation flow, tab/scene controller, native HomeUI bindings and production integration remain separate.')
    (ROOT/'assets/reference/home-main-panel-runtime-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Original home main-panel lifecycle:',len(outputs),'outputs',flush=True)


if __name__=='__main__':build()
