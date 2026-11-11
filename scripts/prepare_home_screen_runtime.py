"""Port the selected original screen-width, safe-area and border lifecycle."""
import hashlib
import json
from pathlib import Path
import re
import subprocess

from prepare_home_auto_increase_runtime import CODE, ROOT, metadata, sha

DEST=ROOT/'unity/Assets/Game/Runtime/Official/HomeScreenUI'
NAMES=('CanvasWidthAdjuster','SafeArea','ScreenWidthAdapterUtils','IScreenAdaptService','ScreenAdaptService')


def build():
    inputs,outputs={},{}
    references=CODE.parent/'native-conversion/inputs/Managed'
    assembly=references/'Framework.FirstPass.Runtime.dll'
    ilspy=ROOT/'.tools/ilspycmd/tools/net10.0/any/ilspycmd.dll'
    dependencies={p.relative_to(ROOT).as_posix():sha(p) for p in [ilspy,*sorted(references.glob('*.dll'))]}
    inputs.update(dependencies)
    def emit(name,text):
        path=DEST/(name+'.cs');path.parent.mkdir(parents=True,exist_ok=True)
        text='\n'.join(line.rstrip() for line in text.splitlines()).rstrip()+'\n'
        path.write_text('// Selected original screen adaptation; local platform/service boundaries only.\n'
            +text,'utf8',newline='\n')
        for file in (path,metadata(path)):outputs[file.relative_to(ROOT).as_posix()]=sha(file)
    for name in NAMES:
        original=CODE/'framework-firstpass-source'/(name+'.cs')
        inputs[original.relative_to(ROOT).as_posix()]=sha(original)
        resolved=CODE/'framework-firstpass-source-resolved'/(name+'.cs')
        proof=resolved.with_suffix('.decompilation.json')
        if not resolved.exists() or not proof.exists():
            command=[str(ROOT/'.tools/dotnet/dotnet.exe'),str(ilspy),'-t',name,'-r',str(references),str(assembly)]
            result=subprocess.run(command,check=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
            text=result.stdout.decode('utf-8-sig').replace('\r\n','\n')
            assert 'Unknown result type' not in text and 'Invalid comparison' not in text,name
            resolved.parent.mkdir(parents=True,exist_ok=True)
            resolved.write_text(text,'utf8',newline='\n')
            proof.write_text(json.dumps(dict(inputs=dependencies,sha256=sha(resolved),command=command),indent=2)+'\n','utf8',newline='\n')
        evidence=json.loads(proof.read_bytes())
        assert evidence['inputs']==dependencies and evidence['sha256']==sha(resolved),name
        for file in (resolved,proof):inputs[file.relative_to(ROOT).as_posix()]=sha(file)
        text=resolved.read_text('utf8')
        text=re.sub(r'^using Framework[^;]*;\n','',text,flags=re.M)
        text=text.replace('using UnityEngine.Device;\n','').replace('[Inject]\n','')
        # Keep logging at the original branch and replace only its backend.
        pattern=r'(?:GLogger\w+InterpolatedStringHandler\s+)?message\d* = new GLogger\w+InterpolatedStringHandler\(.*?Log\.(Error|Info|Debug)\([^;]+;'
        def logging(match):
            literals=re.findall(r'\.AppendLiteral\(("(?:[^"\\]|\\.)*")\)',match.group())
            message=' + '.join(literals) or '"Original screen adaptation diagnostic"'
            method='LogError' if match[1]=='Error' else 'Log'
            return 'UnityEngine.Debug.'+method+'('+message+');'
        text=re.sub(pattern,logging,text,flags=re.S)
        text=re.sub(r'^\s*GLogger\w+InterpolatedStringHandler message\d*;\n','',text,flags=re.M)
        text=text.replace('AndroidSafeArea.SafeArea','ScreenMetrics.SafeArea')
        text=text.replace('UnityEngine.Device.Screen.','Screen.')
        text=re.sub(r'\bScreen\.', 'ScreenMetrics.',text)
        if name=='ScreenAdaptService':
            text=text.replace(': BackgroundServiceBase, IScreenAdaptService',': IScreenAdaptService, System.IDisposable')
            text=text.replace('protected override void Initialize()','public void Initialize()')
            text=text.replace('protected override void Dispose()','public void Dispose()')
            text=text.replace('base.Initialize();','').replace('base.Dispose();','')
        # Explicit pre-activation binding replaces framework property injection.
        text=text.replace('private IScreenAdaptService ScreenAdaptService { get; set; }',
                          'public IScreenAdaptService ScreenAdaptService { private get; set; }')
        assert 'GLogger' not in text and '[Inject]' not in text and 'Framework.' not in text,name
        using=list(re.finditer(r'^using [^\n]+;\n',text,re.M))
        first=using[-1].end() if using else 0
        text=text[:first]+'namespace Zjcs.Official.HomeScreenUI\n{\n'+text[first:]+'\n}\n'
        emit(name,text)
    android=CODE/'framework-firstpass-source/AndroidSafeArea.cs'
    inputs[android.relative_to(ROOT).as_posix()]=sha(android)
    emit('ScreenMetrics','''using System;
using UnityEngine;
namespace Zjcs.Official.HomeScreenUI
{
    // Windows follows AndroidSafeArea's uninitialized/non-Android Screen branch.
    // A scoped metrics port also permits rendering checks without changing the OS display.
    public interface IScreenMetrics
    {
        int Width { get; }
        int Height { get; }
        ScreenOrientation Orientation { get; }
        Rect SafeArea { get; }
    }
    public static class ScreenMetrics
    {
        private sealed class Native : IScreenMetrics
        {
            public int Width => Screen.width;
            public int Height => Screen.height;
            public ScreenOrientation Orientation => Screen.orientation;
            public Rect SafeArea => Screen.safeArea;
        }
        private static IScreenMetrics current = new Native();
        public static int width => current.Width;
        public static int height => current.Height;
        public static ScreenOrientation orientation => current.Orientation;
        public static Rect SafeArea => current.SafeArea;
        public static IDisposable Bind(IScreenMetrics metrics)
        {
            var previous=current;
            current=metrics ?? throw new ArgumentNullException(nameof(metrics));
            return new Binding(previous,current);
        }
        private sealed class Binding : IDisposable
        {
            private IScreenMetrics previous, bound;
            public Binding(IScreenMetrics previous,IScreenMetrics bound) { this.previous=previous;this.bound=bound; }
            public void Dispose()
            {
                if(bound==null)return;
                if(!ReferenceEquals(current,bound))throw new InvalidOperationException("Release screen bindings in reverse order.");
                current=previous;previous=null;bound=null;
            }
        }
    }
}
''')
    emit('OfficialHomeScreenAssetCatalog','''using System;
using UnityEngine;
namespace Zjcs.Official.HomeScreenUI
{
    // Explicit local resource address; all frame geometry remains in the original prefab.
    public sealed class OfficialHomeScreenAssetCatalog : ScriptableObject
    {
        public GameObject framePrefab;
        public static OfficialHomeScreenAssetCatalog LoadDefault()
        {
            var result=Resources.Load<OfficialHomeScreenAssetCatalog>("Home/OfficialHomeScreenAssetCatalog");
            if(result==null || result.framePrefab==null)
                throw new InvalidOperationException("Original home screen frame catalog is unavailable.");
            return result;
        }
    }
}
''')
    path=DEST/'Zjcs.Official.HomeScreenUI.asmdef'
    path.write_text(json.dumps(dict(name='Zjcs.Official.HomeScreenUI',rootNamespace='Zjcs.Official.HomeScreenUI',references=['Unity.ugui']),indent=2)+'\n','utf8',newline='\n')
    for file in (path,metadata(path),metadata(DEST,True)):outputs[file.relative_to(ROOT).as_posix()]=sha(file)
    proof=dict(manifest='884_156692',generator_sha256=sha(Path(__file__)),inputs=inputs,outputs=outputs,
        scope='Complete original safe-area/width calculation and border lifecycle; Windows Screen metrics and explicit local service ownership. MainCanvas and production HUD binding are separate.')
    (ROOT/'assets/reference/home-screen-runtime-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Original home screen adaptation:',len(outputs),'outputs',flush=True)


if __name__=='__main__':build()
