"""Run the real-profile currency fixture inside the original portrait screen frame."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import shutil

from official_apk import ROOT
from validation_workspace import validation_directory
from prepare_home_currency_profile_validation import stage as stage_profile
from prepare_home_top_bar_validation import transfer
from prepare_current_partner_native import documents
from prepare_partner_card_native import guid


def restore_validation_font_cache(work,project):
    """Discard only the isolated project's generated dynamic TMP glyph cache."""
    receipt=json.loads((work/'home-top-bar-native-source-manifest.json').read_bytes())
    font_script=receipt['script_bindings']['TMPro.TMP_FontAsset']['pointer']
    staged={guid(work/'gift-assets'/name):work/'gift-assets'/name[:-5]
        for name in receipt['outputs'] if name.endswith('.meta')}
    selected=set()
    for key,path in staged.items():
        if path.suffix!='.asset':continue
        raw=path.read_bytes()
        if b'MonoBehaviour:' not in raw[:100]:continue
        for doc in documents(raw.decode('utf8')).values():
            fields=doc.get('MonoBehaviour',{})
            if fields.get('m_Script')==font_script and fields.get('m_AtlasPopulationMode')==1:
                selected.add(key)
                for texture in fields['m_AtlasTextures']:selected.add(texture['guid'])
    assert selected,'Selected original dynamic font closure required.'
    pattern='^guid: ('+'|'.join(sorted(selected))+')\r?$'
    result=subprocess.run(['rg','--files-with-matches','--null','--no-ignore','--glob','*.meta','-e',pattern,str(project/'Assets')],stdout=subprocess.PIPE,stderr=subprocess.PIPE)
    if result.returncode not in (0,1):raise RuntimeError(result.stderr.decode('utf8','replace'))
    seen=set()
    for value in result.stdout.decode('utf8').split('\0'):
        if not value:continue
        meta=Path(value);key=guid(meta);assert key not in seen;seen.add(key)
        target=Path(str(meta)[:-5]);assert target.resolve().is_relative_to(project.resolve())
        source=staged[key]
        assert hashlib.sha256(source.read_bytes()).hexdigest()==receipt['outputs'][source.relative_to(work/'gift-assets').as_posix()]
        if target.read_bytes()!=source.read_bytes():shutil.copy2(source,target)
    print('Restored isolated original font/cache closure:',len(seen),flush=True)


def stage(work,project):
    assert project.resolve().is_relative_to(validation_directory().resolve())
    restore_validation_font_cache(work,project)
    transfer(work,project)
    stage_profile(project)
    source=(ROOT/'scripts/fixtures/home_currency_profile_validation.cs').read_text('utf8')
    for before,after in [('HomeCurrencyProfileValidation','HomeCurrencyPortraitValidation'),
        ('HomeCurrencyProfileRunner','HomeCurrencyPortraitRunner'),('Zjcs.HomeCurrencyProfile','Zjcs.HomeCurrencyPortrait'),
        ('HOME_CURRENCY_PROFILE_PASSED','HOME_CURRENCY_PORTRAIT_PASSED'),
        ('currency-profile','currency-portrait-profile'),('home-currency-overflow.png','home-currency-portrait-overflow.png')]:
        source=source.replace(before,after)
    start=source.index('        // WorldSafeArea reads native Screen')
    end=source.index('        events=new GameObject',start)
    source=source[:start]+'''        target=new RenderTexture(608,1080,24);target.Create();uiCamera.targetTexture=target;
        screenBinding=Zjcs.Official.HomeScreenUI.ScreenMetrics.Bind(new TargetMetrics());
        screenService=new Zjcs.Official.HomeScreenUI.ScreenAdaptService();screenService.Initialize();
        frameHolder=new GameObject("InactivePortraitFrame");frameHolder.SetActive(false);
        var root=Object.Instantiate(Zjcs.Official.HomeScreenUI.OfficialHomeScreenAssetCatalog.LoadDefault().framePrefab,frameHolder.transform,false);
        root.transform.localPosition=Vector3.zero;root.transform.localRotation=Quaternion.identity;root.transform.localScale=Vector3.one;root.SetActive(true);
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=uiCamera;canvas.planeDistance=1;
        root.GetComponent<Zjcs.Official.HomeScreenUI.CanvasWidthAdjuster>().ScreenAdaptService=screenService;
        foreach(var area in root.GetComponentsInChildren<Zjcs.Official.HomeScreenUI.SafeArea>(true))area.ScreenAdaptService=screenService;
        frameHolder.SetActive(true);
'''+source[end:]
    source=source.replace('new OfflineHomeTopBar(canvas.transform,','new OfflineHomeTopBar(canvas.transform.Find("Content"),')
    source=source.replace('        foreach(var item in Items())\n        {','''        Require(view.Root.GetComponent<Zjcs.Official.HomeScreenUI.SafeArea>()!=null,"original full SafeArea script is bound");
        Require(Math.Abs(canvas.scaleFactor-Math.Min(608f/1080,1080f/1920))<.001f,"original Expand uses render-target display size");
        foreach(var item in Items())
        {''',1)
    source=source.replace('    OfflineMainlineProgressSession profile;','''    sealed class TargetMetrics:Zjcs.Official.HomeScreenUI.IScreenMetrics
    {
        public int Width=>608;
        public int Height=>1080;
        public ScreenOrientation Orientation=>ScreenOrientation.Portrait;
        public Rect SafeArea=>new Rect(0,0,608,1080);
    }
    IDisposable screenBinding;
    Zjcs.Official.HomeScreenUI.ScreenAdaptService screenService;
    GameObject frameHolder;
    OfflineMainlineProgressSession profile;''')
    source=source.replace('        Require(errors==0,"no Unity runtime errors");','''        Object.Destroy(frameHolder);yield return null;
        Require(!screenService.CanvasWidthAdjusters.Any(),"frame unregisters before screen service disposal");
        screenService.Dispose();screenService=null;screenBinding.Dispose();screenBinding=null;
        Require(errors==0,"no Unity runtime errors");''')
    folder=project/'Assets/HomeCurrencyPortraitValidation';folder.mkdir(parents=True,exist_ok=True)
    (folder/'HomeCurrencyPortraitValidation.cs').write_text(source,'utf8',newline='\n')
    original=json.loads((project/'Assets/HomeCurrencyProfileValidation/HomeCurrencyProfileValidation.asmdef').read_bytes())
    original['name']='Zjcs.HomeCurrency.PortraitValidation'
    original['references'].append('Zjcs.Official.HomeScreenUI')
    (folder/'HomeCurrencyPortraitValidation.asmdef').write_text(json.dumps(original,indent=2)+'\n','utf8',newline='\n')
    print('Original portrait frame + real-profile fixture:',hashlib.sha256(source.encode()).hexdigest(),flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(__doc__)
    parser.add_argument('--source-work',type=Path,required=True)
    parser.add_argument('--project',type=Path,required=True)
    args=parser.parse_args();stage(args.source_work,args.project)
