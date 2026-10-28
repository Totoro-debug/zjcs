"""Restore the original two-frame offscreen UI update sequence and cancellation."""
import hashlib
import json
from pathlib import Path

from prepare_unity_profession_photographer import ROOT, CODE, RUNTIME, save, record, clean


def build():
    manifest=CODE.parent.parent/'code-manifest.json';metadata=json.loads(manifest.read_bytes());assembly=CODE.parent/'managed/decoded/Assembly-CSharp.dll'
    if metadata['manifest']!='871_156598' or hashlib.sha256(assembly.read_bytes()).hexdigest()!=metadata['assemblies'][assembly.name]['decoded_sha256']:raise ValueError('Original offscreen player assembly changed')
    source=CODE/'OffScreenUISpinePlayer.cs';original=source.read_text(encoding='utf-8');text=clean(original)
    a=text.index('\t[StructLayout(');b=text.index('\tprivate CancellationTokenSource',a);text=text[:a]+text[b:]
    for using in ('using System;\n','using System.Diagnostics;\n','using System.Runtime.CompilerServices;\n','using System.Runtime.InteropServices;\n','using Cysharp.Threading.Tasks.CompilerServices;\n','using Spine.Unity;\n'):
        text=text.replace(using,'')
    text=text.replace('private AppearanceUIPartHandler mPlayerSpinePartPartHandler;','private Component mPlayerSpinePartPartHandler;')
    text=text.replace('public AppearanceUIPartHandler PlayerSpinePartPartPartPartHandler','public Component PlayerSpinePartPartPartPartHandler')
    text=text.replace('GetComponent<AppearanceUIPartHandler>()','GetComponent<IProfessionOffScreenPartHandler>() as Component')
    text=text.replace('PlayerSpinePartPartPartPartHandler.OnSkinChanged','((IProfessionOffScreenPartHandler)PlayerSpinePartPartPartPartHandler).OnSkinChanged')
    text=text.replace('List<SkeletonGraphic>','List<IProfessionOffScreenGraphic>').replace('foreach (SkeletonGraphic listUISpine','foreach (IProfessionOffScreenGraphic listUISpine')
    a=text.index('\t[AsyncStateMachine(');b=text.index('\n\tprivate void OffScreenSet()',a)
    text=text[:a]+'''    private async UniTask DelayOffScreenSet(int waitFrameNum)
    {
        TryDisposeToken();
        mCancellationTokenSource = new CancellationTokenSource();
        await UniTask.DelayFrame(waitFrameNum, (PlayerLoopTiming)8, mCancellationTokenSource.Token, false);
        TryDisposeToken();
        OffScreenSet();
    }
'''+text[b:]
    text=text.replace('((Component)this).GetComponentsInChildren<SkeletonGraphic>(true, ListUISpines);','''foreach (MonoBehaviour component in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component is IProfessionOffScreenGraphic graphic) ListUISpines.Add(graphic);
            }''')
    text=text.replace('!((Object)(object)listUISpine == (Object)null)','listUISpine != null && listUISpine.Instance')
    text=text.replace('listUISpine.updateWhenInvisible = (UpdateMode)0;','listUISpine.updateWhenInvisible = 0;')
    text=text.replace('((Component)listUISpine).transform','listUISpine.Instance.transform').replace('((Component)listUISpine).gameObject','listUISpine.Instance.gameObject')
    for forbidden in ('Spine.Unity','SkeletonGraphic','AppearanceUIPartHandler','_003C','AsyncStateMachine','UpdateMode'):
        if forbidden in text:raise ValueError('Unresolved original offscreen dependency: '+forbidden)
    port=save('OffScreenUISpinePlayer',source,text)
    binding=RUNTIME/'ProfessionOffScreenGraphic.cs';validation=ROOT/'unity/Assets/Game/Editor/ProfessionUI/ProfessionOffScreenPlayerValidation.cs'
    proof=dict(format=1,manifest='871_156598',code_manifest=record(manifest),assembly=record(assembly),ports=[port],generator=record(Path(__file__)),
        bindings=[record(binding),record(binding.with_name(binding.name+'.meta'))],validation=[record(validation),record(validation.with_name(validation.name+'.meta'))],
        operation='Copy all original OffScreenUISpinePlayer application methods. Restore the decompiled UniTask state machine as async with its exact cancel/dispose/new-token/DelayFrame(2, timing 8)/dispose/update order. Preserve hierarchy traversal including inactive descendants, update mode zero for every live graphic, auto-size only active non-root graphics, activeInHierarchy-only event guard and part listener lifecycle. Adapt SDK component lookup to explicit native interfaces; no skeleton SDK implementation is used.',
        validated_scope='Isolated Unity real Play Mode frames passed: replacement cancellation, two-frame timing, ordered hierarchy collection including inactive children, mode zero on every graphic, bounds updates only for active non-root graphics, disable cancellation, inactive hierarchy guard, original disabled-component behavior, token disposal and destroy unsubscription. Native graphic/part endpoints are fixtures; source character rendering and full profession UI remain incomplete.',
        limitations=['Actual source native skeleton graphics/parts and complete profession-page rendering remain unbound.'])
    (ROOT/'assets/reference/profession-ui/offscreen-player-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Restored original offscreen UI player and two-frame cancellation flow.')


if __name__=='__main__':build()
