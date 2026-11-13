using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Home;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeSceneUiControls;

public sealed partial class HomeHangUpHostRunner
{
    private IEnumerator CheckHangUp()
    {
        var owner=home.HangUpHud;
        Require(owner!=null && owner.Root.activeInHierarchy, "actual home owns native hangup guides");
        var guides=owner.Root.GetComponentsInChildren<HomeHangUpDirectionGuider>(true);
        var bed=guides.Single(value=>Read<EFurniture>(value,"mFurniture")==EFurniture.Bed);
        var cart=guides.Single(value=>value!=bed);
        var bedButton=Read<Button>(bed,"mGuiderBtn");
        var cartButton=Read<Button>(cart,"mGuiderBtn");
        Require(!bedButton.gameObject.activeInHierarchy && !cartButton.gameObject.activeInHierarchy,"no initial income creates no guide");
        Require(owner.Root.GetComponentsInChildren<ParticleSystem>(true).Length==6,"original six guide particle systems retained");
        Require(!owner.Root.GetComponentInChildren<AwardRetrieveNpcDirectionGuider>(true).gameObject.activeInHierarchy,"unported NPC owner is not simulated");
        long capacity=home.Session.Preview(1).Capacity;
        long stored=(long)((float)capacity*.05f);
        Require(stored>0 && home.Session.Mutate("fixture:stored-bed",save=>{
            var entry=save.Income.SingleOrDefault(value=>value.HangUpId==1);
            if(entry==null)save.Income.Add(entry=new OfflineHomeIncomeEntry{HangUpId=1});
            entry.StoredSeconds=0;entry.SettledCount=stored;
        }),"seed saved income using actual configured capacity");
        owner.Source.NotifyDataChanged(1);
        yield return null;
        Require(owner.TipLevel(EFurniture.Bed)==1 && owner.TipLevel(EFurniture.Cart)==-1,"real bed capacity drives original highest bed tier");
        Require(bedButton.gameObject.activeInHierarchy && !cartButton.gameObject.activeInHierarchy,"only original bed direction button appears");
        Capture("hangup-host-offscreen-bed.png");
        Click(bedButton);
        var mask=Read<GameObject>(bed,"mMask");
        Require(mask.activeSelf,"actual guide pointer enables original focus input mask");
        float deadline=Time.realtimeSinceStartup+5;
        while(mask.activeSelf && Time.realtimeSinceStartup<deadline)yield return null;
        Require(!mask.activeSelf && Math.Abs(home.Scene.Position.x-owner.GetFurniture(EFurniture.Bed).transform.position.x)<.001f,"original coroutine focuses actual bed anchor");
        yield return null;
        Require(!bedButton.gameObject.activeInHierarchy,"actual bed entering viewport hides direction button");
        Capture("hangup-host-focused-bed.png");
        // This checks the production transaction/event bridge. The historical
        // converted bed dialog has a raycast obstruction and is a separate
        // native-window migration; do not report this as a button interaction.
        Require(home.TryCollect(1)>0,"actual home transaction collects bed income");
        yield return null;
        Require(home.Session.Preview(1).Count==0 && owner.TipLevel(EFurniture.Bed)==-1,"real collect transaction clears stored bed income and tip");
        // Simulate a drag through the same scene input bridge used by the mouse.
        var input=typeof(Zjcs.Presentation.Home.HomeFeatureRuntime).GetField("input",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(home);
        var down=input.GetType().GetMethod("PointerDown",BindingFlags.Instance|BindingFlags.NonPublic);
        var move=input.GetType().GetMethod("PointerMove",BindingFlags.Instance|BindingFlags.NonPublic);
        var up=input.GetType().GetMethod("PointerUp",BindingFlags.Instance|BindingFlags.NonPublic);
        Vector2 start=new Vector2(300,500),end=new Vector2(1600,500);
        down.Invoke(input,new object[]{start,false});move.Invoke(input,new object[]{end});up.Invoke(input,new object[]{end,false});yield return null;
        Require(!bedButton.gameObject.activeInHierarchy,"camera drag cannot resurrect collected prompt");
        Require(home.SetHangUpQuickReceiveState(EFurniture.Bed,false),"persist original quick-receive preference");
        var old=owner;
        home.Close();CloseCapture();yield return null;
        Open();yield return new WaitForSeconds(1);
        Require(home.Session.Preview(1).Count==0 && home.HangUpHud.TipLevel(EFurniture.Bed)==-1,"disk reopen reconstructs collected tip state");
        Require(home.Session.Save.QuickReceiveStates.Single().Type==0 && !home.Session.Save.QuickReceiveStates.Single().Enabled,"disk reopen retains actual preference");
        Require(old.Root==null,"previous native guide host disposed");
        Capture("hangup-host-collected-reopened.png");
        Debug.Log("HOME_HANGUP_HOST_PASSED: actual profile, native repair pointer, configured income, original native guide pointer/mask/focus, direct production collection transaction, simulated drag, preference persistence and disk reopen. Bed collection button remains unverified.");
    }
    private static T Read<T>(object value,string name)
    {
        for(var type=value.GetType();type!=null;type=type.BaseType)
        {
            var field=type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly);
            if(field!=null)return (T)field.GetValue(value);
        }
        throw new MissingFieldException(name);
    }
}
