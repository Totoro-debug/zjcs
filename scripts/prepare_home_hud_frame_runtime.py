"""Port the official player-panel stencil used by the shared HomeUI frame."""
import hashlib
import json
from pathlib import Path
import re
from official_apk import ROOT
from prepare_partner_card_native import sha

def build():
    source=ROOT/'.tools/source-packages/official-code/client-source/StencilMask.cs'
    text=source.read_text('utf-8-sig').replace('\r\n','\n').replace('using Framework.DependencyInjection;\n','')
    text=re.sub(r'^\s*//IL_[^\n]*\n','\n',text,flags=re.M)
    before='IServiceProvider sceneContainer = ((Component)(object)((Component)this).transform.parent).GetSceneContainer();\n\t\t\tmCopy = ServiceProviderExtensions.Instantiate(sceneContainer, ((Component)this).gameObject, ((Component)this).transform.parent, false);'
    assert text.count(before)==1
    text=text.replace(before,'mCopy = Object.Instantiate(((Component)this).gameObject, ((Component)this).transform.parent, false);')
    text=text.replace('using UnityEngine.UI;\n','using UnityEngine.UI;\nusing Object = UnityEngine.Object;\n')
    text='// Original StencilMask; scene-container instantiation maps to local Unity objects.\nnamespace Zjcs.Official.HomeScreenUI\n{\n'+text+'\n}\n'
    dest=ROOT/'unity/Assets/Game/Runtime/Official/HomeScreenUI/StencilMask.cs'
    if dest.exists():assert dest.read_text('utf8').startswith('// Original StencilMask;')
    dest.write_text(text,'utf8',newline='\n')
    meta=Path(str(dest)+'.meta')
    if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:home-hud-stencil-mask').hexdigest()+'\nMonoImporter:\n  serializedVersion: 2\n  externalObjects: {}\n','utf8',newline='\n')
    receipt=dict(generator_sha256=sha(Path(__file__).read_bytes()),inputs={source.relative_to(ROOT).as_posix():sha(source.read_bytes())},outputs={item.relative_to(ROOT).as_posix():sha(item.read_bytes()) for item in (dest,meta)},adaptation='Original fields, order and material values retained. Scene-container instantiation maps to Unity local instantiation; this original object contains only Image and StencilMask.')
    (ROOT/'assets/reference/home-hud-frame-runtime-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')

if __name__=='__main__':build()
