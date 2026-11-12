"""Migrate original quantity controls used by home currency windows."""
import json,re
from pathlib import Path
from prepare_home_auto_increase_runtime import ROOT,CODE,metadata,sha,method
DEST=ROOT/'unity/Assets/Game/Runtime/Official/HomeCountControlsUI'


def build():
    inputs,outputs={},{}
    def read(name,common=False):
        path=CODE/('common-source/Common' if common else 'client-source')/(name+'.cs')
        inputs[path.relative_to(ROOT).as_posix()]=sha(path)
        return path.read_text('utf-8-sig').replace('\r\n','\n')
    def emit(name,body):
        path=DEST/(name+'.cs');path.parent.mkdir(parents=True,exist_ok=True)
        header='// Original quantity control; language/color/input services explicitly bound.\nnamespace Zjcs.Official.HomeCountControlsUI\n{\nusing Object=UnityEngine.Object;\nusing Zjcs.Official.WorldUI;\nusing Zjcs.Official.World.NativeLabelUI.Framework;\n'
        path.write_text(header+body+'\n}\n','utf8',newline='\n')
        for item in (path,metadata(path)):outputs[item.relative_to(ROOT).as_posix()]=sha(item)
    for name in ('SetCountSlider','SetCountButtonGroup','LongTouch'):
        text=read(name)
        text=re.sub(r'^using (?:Common|Framework(?:\.[\w.]+)?);\n','',text,flags=re.M)
        text=re.sub(r'^\s*//IL_.*\n','\n',text,flags=re.M)
        text=text.replace('MultiLanguageText','WorldMultiLanguageText')
        if name=='SetCountSlider':
            text=text.replace('\t[Inject]\n\tprivate IScriptableObjectConfig<GlobalColors> GlobalColors { get; set; }','\tpublic IHomeCountColors GlobalColors { get; set; }')
            text=text.replace('GlobalColors.Value.WrapColor("0", Color32.op_Implicit(GlobalColors.Value.LightRed))','GlobalColors.WrapColor("0", GlobalColors.LightRed)')
        elif name=='LongTouch':
            # Original diagnostic interpolators only; state and timing untouched.
            text=re.sub(r'\t\tGLoggerInfoInterpolatedStringHandler message =.*?\t\tLog.Info\(ref message,.*?;\n','',text,flags=re.S)
            text=text.replace('Vector2.op_Implicit(Input.mousePosition)','(Vector2)Input.mousePosition')
            pos=text.index('{')+1
            text=text[:pos]+'\n    public Func<Vector2?> PointerPositionOverride { get; set; }\n'+text[pos:]
            anchor='private Vector2 GetCurrentPointerPosition()\n\t{';assert anchor in text
            text=text.replace(anchor,anchor+'\n        var injected = PointerPositionOverride?.Invoke();\n        if (injected.HasValue) return injected.Value;',1)
        assert all(value not in text for value in ('[Inject]','GLogger','op_Implicit','IScriptableObjectConfig'))
        emit(name,text)
    emit('MathUtils','using System;\ninternal static class MathUtils\n{\n'+method(read('MathUtils',True),'public static long Clamp(long val, long min, long max)')+'}\n')
    emit('IHomeCountColors','''using UnityEngine;
public interface IHomeCountColors
{
    Color LightRed { get; }
    string WrapColor(string text, Color color);
}''')
    path=DEST/'Zjcs.Official.HomeCountControlsUI.asmdef'
    path.write_text(json.dumps(dict(name='Zjcs.Official.HomeCountControlsUI',references=['Unity.ugui','Zjcs.Official.WorldUI','Zjcs.Official.World.NativeLabelUI']),indent=2)+'\n','utf8',newline='\n')
    for item in (path,metadata(path),metadata(DEST,True)):outputs[item.relative_to(ROOT).as_posix()]=sha(item)
    receipt=dict(manifest='884_156692',generator_sha256=sha(Path(__file__)),inputs=inputs,outputs=outputs,
        scope='Original SetCountSlider, SetCountButtonGroup and LongTouch. Diagnostics omitted; optional internal-test pointer position injected before original touch/mouse fallback. Native prefabs and production currency windows remain separate.')
    (ROOT/'assets/reference/home-count-controls-runtime-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Original quantity controls:',len(outputs),'outputs',flush=True)
    return receipt

if __name__=='__main__':build()
