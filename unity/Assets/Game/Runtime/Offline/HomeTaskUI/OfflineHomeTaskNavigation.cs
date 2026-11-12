using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Official.HomeTaskUI;
namespace Zjcs.Offline.HomeTaskUI
{
    public sealed class OfflineHomeTaskNavigation:IOfflineHomeTaskNavigation
    {
        readonly Dictionary<int,HomeTaskJumpCondition> conditions=new Dictionary<int,HomeTaskJumpCondition>();
        readonly Dictionary<int,ITaskTraceTarget> traces=new Dictionary<int,ITaskTraceTarget>();
        readonly Action<object> completed;
        public OfflineHomeTaskNavigation(OfflineHomeMainlineTaskData data,IHomeTaskJumpHost host,Action<object> completed)
        {
            if(data==null)throw new ArgumentNullException(nameof(data));
            if(host==null)throw new ArgumentNullException(nameof(host));
            this.completed=completed??throw new ArgumentNullException(nameof(completed));
            foreach(var task in data.OrderedTasks){
                var row=task.CopyOriginalRow();
                // All selected mainline rows take the original condition branch
                // in NavigateUtils; never silently discard a future NavigateKey.
                if(row["NavigateKey"].Type!=JTokenType.Null)throw new NotSupportedException("Review original task NavigateKey: "+task.ClassId);
                var finish=(JObject)row["FinishCondition"];HomeTaskJumpCondition condition;
                switch((string)finish["_type"]){
                    case "MapGameTotalCountCondition":condition=new ClientMapGameTotalCountCondition();break;
                    case "WorldMapProgressCondition":condition=new ClientWorldMapProgressCondition();break;
                    case "MapLogicEntityEndByTagCondition":condition=new ClientMapLogicEntityEndByTagCondition();break;
                    case "MapRegionGoddessCondition":condition=new ClientMapRegionGoddessCondition();break;
                    case "TotalOpenChestCondition":condition=new ClientTotalOpenChestCondition();break;
                    case "DemonBattleLayerPassCondition":condition=new ClientDemonBattleLayerPassCondition();break;
                    // Selected redirector has no client jumpable implementation.
                    case "MaxProfessionRankCondition":conditions.Add(task.ClassId,null);continue;
                    default:throw new NotSupportedException("Review original task navigation condition: "+finish["_type"]);
                }
                condition.Host=host;condition.IsJumpable=(bool?)finish["Jumpable"]==true;
                condition.MapClassId=(int?)finish["MapClassId"]??0;condition.Region=(int?)finish["Region"]??0;
                condition.Tag=(string)finish["Tag"];conditions.Add(task.ClassId,condition);
                if(condition is ClientWorldMapProgressCondition||condition is ClientMapLogicEntityEndByTagCondition)
                    traces.Add(task.ClassId,new Trace(condition));
            }
        }
        public bool CanNavigate(int classId)=>conditions[classId]?.CanJump()??false;
        public void Navigate(int classId,IMapInputSupportHandlerProxy input,INavigateManager navigation,object context)
        {
            if(context!=null&&!(context is Action<object>))throw new ArgumentException("A navigation completion callback is required.",nameof(context));
            var condition=conditions[classId];
            if(condition!=null&&condition.CanJump())condition.Jump(classId,result=>{
                (context as Action<object>)?.Invoke(result);completed(result);
            });
        }
        public ITaskTraceTarget TraceTarget(int classId)=>traces.TryGetValue(classId,out var trace)?trace:null;
        sealed class Trace:ITaskTraceTarget
        {
            readonly HomeTaskJumpCondition condition;
            public Trace(HomeTaskJumpCondition condition){this.condition=condition;}
            public bool CanTrace()=>condition.CanTrace();
        }
    }
}
