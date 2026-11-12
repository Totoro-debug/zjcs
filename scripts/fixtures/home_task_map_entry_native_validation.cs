using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.HomeTaskUI;
using TaskUI=Zjcs.Official.HomeTaskUI;

public sealed partial class HomeCurrencyWindowsNativeRunner
{
    readonly List<(int,int)> taskEntryRequests=new List<(int,int)>();
    void ConfigureTaskMapEntryNative()
    {
        var data=OfflineHomeTaskMapEntryData.LoadDefault();var route=new TaskUI.HomeTaskMapEntryNavigation(data);
        var previousFactory=taskRowNavigationFactory;
        taskRowNavigationFactory=tasks=>{
            var navigation=previousFactory(tasks);var target=new NativeTaskEntryPort(this,rowNavigationHost);
            rowNavigationHost.EntryRoute=(map,region)=>Require(route.GotoMapRegion(target,target,map,region),"original native confirmation selects an official map entry");
            return navigation;
        };
        var previousVerifier=taskRowNavigationVerifier;
        taskRowNavigationVerifier=row=>VerifyTaskMapEntryNative(row,previousVerifier,data);
    }
    IEnumerator VerifyTaskMapEntryNative(TaskUI.TaskListItem row,Func<TaskUI.TaskListItem,IEnumerator> previous,OfflineHomeTaskMapEntryData data)
    {
        Require(taskEntryRequests.Count==0,"no entry request before native pointer sequence");
        yield return previous(row);
        Require(taskEntryRequests.SequenceEqual(new[]{(10,data.GetMapPartInfo(10).DefEntryId)}),"cancel issues no entry and confirm selects the actual default entry once");
        Debug.Log("HOME_TASK_MAP_ENTRY_NATIVE_PASSED: actual character original task row pointers, original native cancel/confirm, selected official entry search and exact map/default entry. Actual scene entry remains an explicit host boundary.");
    }
    sealed class NativeTaskEntryPort:TaskUI.IHomeTaskMapEntryState,TaskUI.IHomeTaskMapEntryRoute
    {
        readonly HomeCurrencyWindowsNativeRunner owner;readonly TaskJumpHost host;
        public NativeTaskEntryPort(HomeCurrencyWindowsNativeRunner owner,TaskJumpHost host){this.owner=owner;this.host=host;}
        // This validation only supplies a private region with no activated entry.
        public List<int> GetActivedMapEntries(int map)=>null;
        public TaskUI.HomeTaskMapRegionStatus GetMapRegionStatus(int map,int region)=>TaskUI.HomeTaskMapRegionStatus.Private;
        public int GetWorldPeriodicityInteractionCount(int group)=>0;
        public bool TryGetLastMapEntry(int map,out int region)=>host.TryGetLastMapEntry(map,out region);
        public int CurrentMapClassId=>host.CurrentMapClassId;
        public int GetGroupCurrentMap(int map)=>host.GetGroupCurrentMap(map);
        public void Broadcast(string key)=>host.Broadcast(key);
        public void DoEnterMap(int map,int entry)=>owner.taskEntryRequests.Add((map,entry));
    }
}
