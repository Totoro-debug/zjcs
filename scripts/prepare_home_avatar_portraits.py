"""Bind the home-owned original portrait/resource lifecycle and its Play Mode check."""
import hashlib
import json
import re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]

RUNTIME=ROOT/'unity/Assets/Game/Runtime/Offline/HomeAvatarUI'
EDITOR=ROOT/'unity/Assets/Game/Editor/HomeAvatarPortraits'
RECEIPT=ROOT/'assets/reference/home-avatar-portraits-source-manifest.json'


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def write(path,text): path.parent.mkdir(parents=True,exist_ok=True); path.write_text(text,'utf8',newline='\n')
def metadata(path,folder=False):
    file=Path(str(path)+'.meta')
    if not file.exists():
        identity=hashlib.md5(('zjcs:home-avatar-portraits:'+path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        importer='DefaultImporter' if folder else 'MonoImporter' if path.suffix=='.cs' else 'AssemblyDefinitionImporter'
        write(file,'fileFormatVersion: 2\nguid: '+identity+'\n'+('folderAsset: yes\n' if folder else '')+importer+':\n  externalObjects: {}\n')
    return file


def prepare():
    write(RUNTIME/'Zjcs.Offline.HomeAvatarUI.asmdef',json.dumps(dict(name='Zjcs.Offline.HomeAvatarUI',references=[
        'Zjcs.Offline.Progression','Zjcs.Offline.ProfessionUI','Zjcs.Official.ProfessionUI']),indent=2)+'\n')
    source=ROOT/'scripts/fixtures/home_avatar_native_validation.cs'
    text=source.read_text('utf8').replace('HomeAvatarNative','HomeAvatarPortraitOwner')
    text=text.replace('using Zjcs.Offline.HomeUI;', 'using Zjcs.Offline.HomeUI;\nusing Zjcs.Offline.HomeAvatarUI;')
    text=text.replace('OfflineProfessionPortraitSession portraitSession;', 'OfflineHomeAvatarPortraits portraitSession;')
    text=text.replace('            target=new RenderTexture(Screen.width,Screen.height,24);', '            target=new RenderTexture(608,1080,24);')
    start=text.index('        var resources=ProfessionNativePortraitStudioResources.LoadDefault();')
    end=text.index('        try\n',start)
    text=text[:start]+'''        var pipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");
        Require(pipeline,"packaged original rendering pipeline");
        GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        var previous=ProfessionAppearanceTextureBindings.Current;
        var images=ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();images.Entries=Array.Empty<OfflineProfessionImageCatalog.Entry>();
        using var actors=new OfflinePartnerActorUiRuntime(profile,growth,OfficialPartnerAssetCatalog.LoadDefault(),
            new OfflineProfessionImageResourceLoader(images),key=>key,_=>{throw new Exception("Home avatar requested NPC popup");},()=>opened++);
        portraitSession=new OfflineHomeAvatarPortraits(Path.Combine(work,"avatar-portrait-cache"),
            profile.Save.Player.CharacterProperties.LocalActorIdentities.PlayerId,()=>growth.Player,id=>growth.Catalog.ItemType(id));
        string initialPlayer=JsonUtility.ToJson(profile.Save.Player);
'''+text[end:]
    text=text.replace('            portraitSession.Dispose();portraitSession=null;', '''            profile.Reload();portraitSession.RefreshAppearance();
            Require(JsonUtility.ToJson(profile.Save.Player)==initialPlayer,"portrait lifetime never rewrites the saved player");
            portraitSession.Dispose();portraitSession=null;
            cameraUi.targetTexture=null;target.Release();Object.Destroy(target);
            Object.Destroy(root);Object.Destroy(cameraUi.gameObject);Object.Destroy(events.gameObject);
            yield return null;''')
    start=text.index('    sealed class Parts:IProfessionAppearancePartBindings')
    end=text.index('    sealed class Experience:',start)
    text=text[:start]+text[end:]
    text=text.replace('        if(!Path.GetFullPath(Application.dataPath).StartsWith(work+Path.DirectorySeparatorChar))\n            throw new InvalidOperationException("Owned validation project required.");', '''        string repository=Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT"));
        if(!work.StartsWith(Path.Combine(repository,"output","validation")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)
            || !File.Exists(Path.Combine(work,".zjcs-validation.json")))
            throw new InvalidOperationException("Owned validation workspace required.");''')
    text=text.replace('HOME_AVATAR_NATIVE_PASSED:', 'HOME_AVATAR_PORTRAIT_OWNER_PASSED:')
    text=text.replace('original prefab, actual selected-player portrait and internal pointer, experience branches/reward animation and subscription lifecycle.',
        'production home portrait owner, original studio/body/weapon/effect resource routes, actual profile, head geometry, internal pointer, controlled experience branches, reload and release. Formal HUD/reward queue is not exercised.')
    text=text.replace('        EditorApplication.Exit(error==null?0:1);', '''        SessionState.SetInt(Key+".Code",error==null?0:1);SessionState.SetBool(Key+".Exit",true);
        if(EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.ExitPlaymode();
        else EditorApplication.delayCall+=()=>EditorApplication.Exit(error==null?0:1);''')
    marker='                new GameObject("OriginalHomeAvatarFixture").AddComponent<HomeAvatarPortraitOwnerRunner>();'
    assert marker in text
    text=text.replace(marker,marker+'''
            if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key+".Exit",false))
            {
                SessionState.SetBool(Key+".Exit",false);
                double deadline=EditorApplication.timeSinceStartup+3;
                EditorApplication.CallbackFunction exit=null;
                exit=()=>{if(EditorApplication.timeSinceStartup<deadline || EditorApplication.isUpdating || EditorApplication.isCompiling)return;
                    EditorApplication.update-=exit;EditorApplication.Exit(SessionState.GetInt(Key+".Code",1));};
                EditorApplication.update+=exit;
            }''')
    write(EDITOR/'HomeAvatarPortraitOwnerValidation.cs',text)
    references=['Zjcs.Offline.HomeUI','Zjcs.Offline.HomeAvatarUI','Zjcs.Official.HomeHudUI','Zjcs.Offline.PartnersUI','Zjcs.Offline.ProfessionUI',
        'Zjcs.Offline.Progression','Zjcs.Offline.CharacterCreation','Zjcs.Offline.State','Zjcs.Official.Partners',
        'Zjcs.Official.ProfessionUI','Zjcs.Official.TeamUI','Zjcs.Official.WorldUI','UnityEngine.UI',
        'Unity.RenderPipelines.Universal.Runtime','Unity.RenderPipelines.Core.Runtime']
    write(EDITOR/'Zjcs.Editor.HomeAvatarPortraits.asmdef',json.dumps(dict(name='Zjcs.Editor.HomeAvatarPortraits',references=references,includePlatforms=['Editor']),indent=2)+'\n')
    outputs={}
    for folder in (RUNTIME,EDITOR):
        for path in sorted(folder.iterdir()):
            if path.suffix not in ('.cs','.asmdef'): continue
            for file in (path,metadata(path)): outputs[file.relative_to(ROOT).as_posix()]=sha(file)
        file=metadata(folder,True); outputs[file.relative_to(ROOT).as_posix()]=sha(file)
    inputs={source.relative_to(ROOT).as_posix():sha(source),Path(__file__).relative_to(ROOT).as_posix():sha(Path(__file__))}
    for name in ('partner-player-portraits-source-manifest.json','home-avatar-native-source-manifest.json',
        'battle-animation/player-effect-assets-source-manifest.json','battle-animation/scene-weapon-source-manifest.json'):
        path=ROOT/'assets/reference'/name; inputs[path.relative_to(ROOT).as_posix()]=sha(path)
    for name in ('OfflineProfessionPortraitSession','OfflineProfessionBattlePlayerResources','OfflineProfessionBattleSceneTextures',
        'OfflineProfessionAppearancePartBindings','OfflineProfessionScenePartPoolBindings'):
        path=ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI'/(name+'.cs'); inputs[path.relative_to(ROOT).as_posix()]=sha(path)
    write(RECEIPT,json.dumps(dict(manifest='884_156692',inputs=inputs,outputs=outputs),ensure_ascii=False,indent=2)+'\n')
    print('Prepared native home portrait resource owner and root-project Play Mode check.',flush=True)


if __name__=='__main__': prepare()
