"""Restore Map14 hand-in UI source fields and official button animation assets."""
from __future__ import annotations
import hashlib
import json
import re
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/".tools/assetripper-1.3.14/temp/zjcs-item-source-native/ExportedProject/Assets"
STAGING=ROOT/".tools/map14-handin-ui-verified"
DEST=ROOT/"unity/Assets/Official/World/Map/14/UI/HandIn"
RUNTIME=ROOT/"unity/Assets/Game/Resources/Map14/UI/HandIn"
RECEIPT=ROOT/"assets/reference/official-map/map14-handin-ui-source-manifest.json"
ASSETS=("Animation/UI/Common/Btn_Base_Click.controller",*["Animation/UI/Common/"+n+".anim" for n in ("Normal","Highlighted","Pressed","Selected","Disabled")])

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def guid(path):return re.search(r"^guid:\s*([a-f0-9]+)",path.read_text(encoding="utf-8"),re.M)[1]
def stable(path):return hashlib.md5(("zjcs-official:"+path.relative_to(ROOT/"unity").as_posix()).encode()).hexdigest()

def build():
    from map14_handin_ui_source import extract
    data=extract(STAGING)
    previous=json.loads(RECEIPT.read_text(encoding="utf-8")) if RECEIPT.exists() else {}
    owned={ROOT/r['path']:r['sha256'] for r in previous.get('outputs',[])}
    inputs=[];outputs=[]
    def write(path,raw):
        if path.exists() and path.read_bytes()!=raw and sha(path)!=owned.get(path):
            raise ValueError("Refusing to overwrite unrecognized file: "+str(path))
        path.parent.mkdir(parents=True,exist_ok=True)
        if not path.exists() or path.read_bytes()!=raw:path.write_bytes(raw)
        outputs.append(path)
    def meta(path,folder=False):
        m=path.with_name(path.name+'.meta')
        if not m.exists():
            kind="folderAsset: yes\nDefaultImporter" if folder else "DefaultImporter"
            write(m,(f"fileFormatVersion: 2\nguid: {stable(path)}\n{kind}:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode())
        elif m not in outputs and path not in (ROOT/'unity/Assets/Official', ROOT/'unity/Assets/Game', ROOT/'unity/Assets/Game/Resources'):outputs.append(m)
    replacements={}
    for record in data['sprites']:
        src=Path(record['unityAssetPath']); dst=DEST/'Sprites'/src.name
        replacements[record['uri']]='res://'+dst.relative_to(ROOT).as_posix()
        inputs.append({'path':src.relative_to(ROOT).as_posix(),'sha256':sha(src)})
        write(dst,src.read_bytes());meta(dst)
        record['unityAssetPath']=dst.relative_to(ROOT/'unity').as_posix()
    def resolve(v):
        if isinstance(v,str):return replacements.get(v,v)
        if isinstance(v,list):return [resolve(x) for x in v]
        if isinstance(v,dict):return {k:resolve(x) for k,x in v.items()}
        return v
    gray=data['grayMaterial'];src=Path(gray['shaderPath'])
    dst=ROOT/'unity/Assets/Game/Runtime/Official/UI/Map14HandInGray.shader'
    write(dst,src.read_bytes());meta(dst)
    gray['shaderPath']=dst.relative_to(ROOT/'unity').as_posix()
    data=resolve(data)
    raw=(json.dumps(data,ensure_ascii=False,indent=2)+'\n').encode()
    write(DEST/'handin-ui.json',raw);meta(DEST/'handin-ui.json')
    write(RUNTIME/'catalog.json',raw);meta(RUNTIME/'catalog.json')
    remap={guid((SOURCE/r).with_name(Path(r).name+'.meta')):stable(RUNTIME/Path(r).name) for r in ASSETS}
    for rel in ASSETS:
        src=SOURCE/rel;dst=RUNTIME/src.name
        raw=src.read_text(encoding='utf-8')
        for old,new in remap.items():raw=raw.replace('guid: '+old,'guid: '+new)
        write(dst,raw.encode('utf-8'))
        m=src.with_name(src.name+'.meta')
        text=m.read_text(encoding='utf-8')
        text=re.sub(r'^guid:\s*[a-f0-9]+', 'guid: '+stable(dst),text,count=1,flags=re.M)
        text=re.sub(r'^(\s*assetBundle(?:Name|Variant):).*$',r'\1',text,flags=re.M)
        write(dst.with_name(dst.name+'.meta'),text.encode())
        inputs.extend({'path':p.relative_to(ROOT).as_posix(),'sha256':sha(p)} for p in (src,m))
    for base in (DEST/'Sprites',RUNTIME):
        folders=[];p=base
        while p!=ROOT/'unity/Assets':folders.append(p);p=p.parent
        for p in reversed(folders):p.mkdir(parents=True,exist_ok=True);meta(p,True)
    code=ROOT/'.tools/source-packages/official-code'
    used={'client-source':['DialogHandInItemDialog','HandInItemNode','ItemIcon','ItemProgress','AwardPanelLayoutElement','MaintainGlobalScaleUI','MaintainGlobalScale','UIUtils','UIBloomGroup','BgDialogElement','CountRedDotImage','CountRedDotBase','UIBloomGroupBase'],
          'framework-source':['PreviewTool','StateSwitcher','StateGameObjectConfig'],
          'framework-firstpass-source':['PopupWindow','MirrorImage','NoDrawingRayCast','ButtonSound','PopupWindowSound','MultiLanguageText']}
    for folder,names in used.items():
        for name in names:
            p=code/folder/(name+'.cs')
            if not p.exists():raise FileNotFoundError(p)
            inputs.append({'path':p.relative_to(ROOT).as_posix(),'sha256':sha(p)})
    inputs.append(data['audioReceipt'])
    for record in data['audioBanks']:inputs.append({'path':record['path'],'sha256':record['sha256']})
    for record in data['audio'].values():
        inputs.append({'path':'unity/'+record['unityAssetPath'],'sha256':record['sha256']})
    for p in [Path(__file__),ROOT/'scripts/map14_handin_ui_source.py']:
        inputs.append({'path':p.relative_to(ROOT).as_posix(),'sha256':sha(p)})
    receipt={'format':2,'manifest':'871_156598','mapClassId':14,
             'operation':'Strictly decode original hand-in and item-icon hierarchies, UGUI/TMP/script fields, sprites, localization and popup animation curves. Restore native Unity prefabs through the existing official UI importer; retain source button controller and remap all five clips.',
             'source':data['source'],'nodeCounts':{p['key']:len(p['nodes']) for p in data['panels']},
             'componentCount':len(data['components']),'inputs':inputs,'guidRemappings':remap,
             'outputs':[{'path':p.relative_to(ROOT).as_posix(),'sha256':sha(p)} for p in dict.fromkeys(outputs)]}
    RECEIPT.parent.mkdir(parents=True,exist_ok=True)
    RECEIPT.write_bytes((json.dumps(receipt,ensure_ascii=False,indent=2)+'\n').encode('utf-8'))
    print(json.dumps({'sourceNodes':receipt['nodeCounts'],'sprites':len(data['sprites']),'outputs':len(receipt['outputs'])},ensure_ascii=False))

if __name__=='__main__':build()
