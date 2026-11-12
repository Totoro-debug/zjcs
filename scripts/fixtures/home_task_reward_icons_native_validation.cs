// Compiled alongside the existing currency fixture to reuse its real native
// pointer, font, mask and ItemIcon context setup. The grid is a test host only.
public sealed partial class HomeCurrencyWindowsNativeRunner
{
    Zjcs.Offline.HomeHudUI.OfflineHomeMainlineTaskData taskIconData;
    System.Collections.IEnumerator ExerciseTaskRewardIcons()
    {
        taskIconData=Zjcs.Offline.HomeHudUI.OfflineHomeMainlineTaskData.LoadDefault();
        var catalog=UnityEngine.Resources.Load<Zjcs.Offline.ProfessionUI.OfflineProfessionImageCatalog>("Home/OfflineHomeTaskRewardImages");
        Require(catalog&&catalog.Entries.Length==7,"seven explicit selected native reward sprites");
        int beforeSubscriptions=subscribers;
        var pool=new Zjcs.Offline.HomeTaskUI.OfflineHomeTaskRewardIcons(canvas.transform,this,fonts,Post,taskIconData,assets);
        var ids=new[]{1,2,50,110,120,41300,42200};
        var icons=new System.Collections.Generic.List<Zjcs.Offline.HomeTaskUI.OfflineHomeTaskRewardIcon>();
        for(int index=0;index<ids.Length;index++) {
            int id=ids[index];
            var icon=(Zjcs.Offline.HomeTaskUI.OfflineHomeTaskRewardIcon)pool.GetIcon(id,canvas.transform);
            var rect=icon.GetComponent<UnityEngine.RectTransform>();
            rect.anchoredPosition=new UnityEngine.Vector2(-300+(index%4)*200,index<4?140:-140);
            icon.Native.SetCount(30+index).SetCountTextScale(Zjcs.Official.ProfessionUI.ItemIcon.sTextScale);
            icon.SetClickPopupInfoDialog();
            Require(icon.myInfo.ClassId==id&&icon.Native.IconSprite==catalog.Entries[index].Asset,"actual native item info and original sprite: "+id);
            Require(icon.Native.IconSprite.vertices.Length>0&&icon.Native.IconSprite.triangles.Length>0,"native sprite mesh survives import: "+id);
            icons.Add(icon);
        }
        yield return null;
        Capture("home-task-reward-icons-native.png");
        int beforeDetails=details;
        foreach(var icon in icons) Click(icon.Native.Button);
        Require(details==beforeDetails+ids.Length,"all unobscured native reward pointers reach the supplied detail host");
        foreach(var icon in icons) {icon.ResetCountTextScale();pool.RecycleIcon(icon);}
        Require(subscribers==beforeSubscriptions,"returning native task icons releases actual player subscriptions");
        var reused=(Zjcs.Offline.HomeTaskUI.OfflineHomeTaskRewardIcon)pool.GetIcon(42200,canvas.transform);
        Require(icons.Exists(icon=>icon.GetInstanceID()==reused.GetInstanceID()),"original task pool reuses returned prefab");
        Require(reused.Native.IconSprite==catalog.Entries[6].Asset&&reused.myInfo.ClassId==42200,"reused icon initializes actual new item");
        pool.RecycleIcon(reused);pool.Dispose();pool.Dispose();taskIconData=null;
        yield return null;
        Require(subscribers==beforeSubscriptions,"task icon scope disposal releases subscriptions");
        UnityEngine.Debug.Log("HOME_TASK_REWARD_ICONS_PASSED: seven selected native sprite meshes, original ItemIcon prefab/info/count/quality bindings, original pool reuse and subscription disposal. Task transactions, row integration and real detail-window host remain separate.");
    }
}
