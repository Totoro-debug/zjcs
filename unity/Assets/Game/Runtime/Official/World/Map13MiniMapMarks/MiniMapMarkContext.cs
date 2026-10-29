using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
namespace Zjcs.Official.World.Map13MiniMap
{
    public sealed class MiniMapMarkDisplayInfo { public string MapBgPath; }
    public interface IMiniMapCharacterActionGroupHandle { UniTask ConfirmInterruptAndContinueAsync(Action callback); }
    [AttributeUsage(AttributeTargets.Class,Inherited=true)]
    public sealed class MiniMapWindowAttribute:Attribute { public string Path { get; } public MiniMapWindowAttribute(string path){Path=path;} }
    public abstract class MiniMapMarkContext:MiniMapCharacterContext
    {
        public abstract IMiniMapCharacterActionGroupHandle CharacterActionGroup { get; }
        public abstract bool HasMapGroundRoot { get; }
        public abstract bool HasPartitionController { get; }
        public abstract void ReservePartition(Vector3 position);
        public abstract string GetAreaTag(int map,Zjcs.Official.World.NativeTerrain.Common.Pos2d position);
        public abstract MiniMapMarkDisplayInfo GetMapUIDisplay(int map,string areaTag);
        public abstract IMiniMapDescriptionAssetLease<Sprite> LoadSprite(string path);
        public abstract void Popup(Type dialogType,Action<MiniMapMarkDialogBase> callback);
        public void Popup<T>(Action<MiniMapMarkDialogBase> callback) where T:MiniMapMarkDialogBase=>Popup(typeof(T),callback);
        public abstract void CloseWindow(GameObject window);
        public abstract void ShowError(int code);
        public abstract void FireToCloseFullScreen();
    }
    public static class MiniMapMarkLanguage
    {
        public static Func<string,object[],string> Formatter { get; set; }
        public static string Format(string key,object[] args)=>(Formatter??throw new InvalidOperationException("Original marker language service is unbound."))(key,args);
    }
    internal struct MiniMapMarkDisposableBag:IDisposable
    {
        private List<IDisposable> values;
        public void Add(IDisposable value){if(values==null)values=new List<IDisposable>();values.Add(value);}
        public void Dispose(){if(values==null)return;foreach(var value in values)value.Dispose();values.Clear();}
    }
    internal static class MiniMapMarkDisposable
    {
        public static T AddTo<T>(T value,ref MiniMapMarkDisposableBag bag) where T:IDisposable { bag.Add(value);return value; }
    }
}
