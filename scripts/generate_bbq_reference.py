"""Execute pinned BBQ C# and verbatim skewer/visitor methods with engine adapters."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import tempfile
from official_apk import ROOT
from official_tables import Constructors
from prepare_map_regions import Regions
from prepare_partner_detail import DetailExporter


def method(text,signature):
    start=text.index(signature);brace=text.index('{',start);depth=1;at=brace+1
    while depth:
        if text[at]=='{':depth+=1
        if text[at]=='}':depth-=1
        at+=1
    return text[start:at]


def main():
    source=ROOT/'.tools/source-packages/official-code'
    client=source/'client-source';common=source/'common-source/Common';framework=source/'framework-firstpass-source/Framework'
    files=[client/'BBQGameSession.cs',client/'BBQCookState.cs',client/'BBQSkewerFlowState.cs',common/'WeightRandomSelector.cs',framework/'SystemRandom.cs',framework/'IRandom.cs']
    groups={
        'SKEWER':('BBQSkewerView.cs',['public void SetFlowState(','public void SetCookStatus(','private void PropagateCookStatusToIngredients(',
            'public void BeginGrillingOnGrill(','private void Update()', 'public bool TryAddFood(int foodId, Sprite iconSprite, out string failReason)',
            'private void PlayFoodMountAnimationClipAtIndex(', 'private bool IsGameplayPaused()']),
        'VISITOR':('BBQNpcSlot.cs',['private void Update()']),
        'NORMAL':('BBQNormalGameModeController.cs',['public bool CanSpawnMoreVisitors(','public int GetInitialVisitorSlotCount(', 'private static bool AreNormalLevelVisitorsExhausted(']),
        'ENDLESS':('BBQEndlessGameModeController.cs',['public bool CanSpawnMoreVisitors(','public int GetInitialVisitorSlotCount('])}
    harness=(ROOT/'scripts/fixtures/bbq_reference_harness.cs').read_text('utf8')
    hashes={p.relative_to(source).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in files};methods={}
    for key,(name,signatures) in groups.items():
        p=client/name;text=p.read_text('utf8');original='\n'.join(method(text,s) for s in signatures)
        harness=harness.replace('/*PINNED_'+key+'_METHODS*/',original)
        hashes[p.relative_to(source).as_posix()]=hashlib.sha256(p.read_bytes()).hexdigest()
        methods[name]=dict(signatures=signatures,verbatim_sha256=hashlib.sha256(original.encode()).hexdigest())
    e=Regions();c=Constructors();tables={};inputs=[]
    mapping={m[2]:m[1] for m in re.finditer(r'new (Tb\w+)\(loader\("([^"]+)"\)\)',c.source('Tables'))}
    for key,name in [('stages','ActivityMiniGame/activity_minigame_bbq_level'),('formulas','ActivityMiniGame/activity_minigame_bbq_formula'),('foods','ActivityMiniGame/activity_minigame_bbq_food'),('settings','game_settings'),('common','ActivityMiniGame/activity_minigame_level')]:
        _,obj=e.locate('Assets/Config/Luban/'+name+'.g.bytes');raw=obj.read().m_Script.encode('utf8','surrogateescape')
        data=c.table(raw,mapping[name]);tables[key]=data['BBQSetting'] if key=='settings' else data
        inputs.append(dict(e.apk.provenance(obj),sha256=hashlib.sha256(raw).hexdigest()))
    tables['common']=[r for r in tables['common'] if r['ActivityId'] in {s['ActivityId'] for s in tables['stages']}]
    # Read the actual serialized values, which override the C# initializers.
    detail=DetailExporter();scene={};scene_inputs=[]
    def visit(obj):
        for part in obj.read().m_Component:
            component=part.component.deref()
            if component.type.name=='MonoBehaviour':
                name=detail.mono(component)[0]
                if name in ['BBQNpcSlotsView','BBQGrillAreaView','BBQSkewerView']:
                    fields=detail.schemas.read(component)
                    if name=='BBQNpcSlotsView':
                        scene.update(slots=len(fields['mNpcSlots']),first_delay=fields['mVisitorStaggerFirstDelaySeconds'],interval=fields['mVisitorStaggerIntervalSeconds'],intro_count=fields['mVisitorStaggerIntroCount'])
                    elif name=='BBQGrillAreaView':scene['grill_points']=len(fields['mGrillSnapPoints'])
                    elif fields['mFoodMountPoints']:scene['food_mounts']=len(fields['mFoodMountPoints'])
                    else:continue
                    scene_inputs.append(dict(detail.apk.provenance(component),sha256=hashlib.sha256(component.get_raw_data()).hexdigest()))
            elif component.type.name in ['Transform','RectTransform']:
                for child in component.read().m_Children:visit(child.read().m_GameObject.deref())
    for name in ['BBQGameContent','BBQSkewerPrefab']:
        _,obj=detail.locate('Assets/Prefabs/UI/ActivityMayDay/ActivityBBQ/'+name+'.prefab');visit(obj)
    assert set(scene)=={'slots','first_delay','interval','intro_count','grill_points','food_mounts'},scene
    for name in ['BBQMainGameContent.cs','BBQNpcSlotsView.cs','BBQGrillAreaView.cs','BBQSkewerPrepareAreaView.cs','BBQGameModeControllerFactory.cs','ActivityMiniGameLevelUtils.cs']:
        p=client/name;hashes[p.relative_to(source).as_posix()]=hashlib.sha256(p.read_bytes()).hexdigest()
    fixture=dict(manifest='854_156383',sources=hashes,extracted_methods=methods,table_sources=inputs,tables=tables,scene=scene,scene_sources=scene_inputs)
    with tempfile.TemporaryDirectory(prefix='bbq-native-',dir=ROOT/'.tools') as folder:
        folder=Path(folder);assert folder.resolve().is_relative_to((ROOT/'.tools').resolve())
        native=folder/'NativeBBQ.cs';native.write_text(harness,'utf8')
        input_path=folder/'input.json';input_path.write_text(json.dumps(fixture),'utf8')
        subprocess.run(['pwsh','-NoProfile','-File',str(ROOT/'scripts/test_bbq_reference.ps1'),'-InputPath',str(input_path),'-NativeSource',str(native),'-OriginalFilesJson',json.dumps([str(p) for p in files])],check=True)


if __name__=='__main__':main()
