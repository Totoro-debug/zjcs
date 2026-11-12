using System.Collections.Generic;

namespace Zjcs.Offline.Progression
{
    // Original Processing/Completed/Received values. Unavailable is a local
    // absence of a task instance, before its acquisition condition is met.
    public enum OfflineMainlineTaskState { Unavailable = -1, Processing = 0, Completed = 1, Received = 2 }

    public sealed partial class OfflineMainlineProgressSession
    {
        // Confirmed offline state producer: acquisition + finish, independently
        // of the UI display condition. All selected goals are cumulative; their
        // saved source counters restore the same state without a second ledger.
        public ProgressionResult GetMainlineTaskState(int classId, out OfflineMainlineTaskState state)
        {
            var result = new TaskStateProjection(this, Save).Get(classId, out state);
            return Result(result.Status == ProgressionResultStatus.Unsupported
                ? ProgressionResultStatus.Unsupported : ProgressionResultStatus.Ready,
                classId, result.UnsupportedCondition);
        }

        private sealed class TaskStateProjection
        {
            readonly OfflineMainlineProgressSession owner;
            readonly OfflineMainlineSave save;
            readonly Dictionary<int, (OfflineMainlineTaskState state, ConditionEvaluation result)> cache =
                new Dictionary<int, (OfflineMainlineTaskState, ConditionEvaluation)>();
            readonly HashSet<int> visiting = new HashSet<int>();
            public TaskStateProjection(OfflineMainlineProgressSession owner, OfflineMainlineSave save)
            { this.owner = owner; this.save = save; }

            public ConditionEvaluation Get(int id, out OfflineMainlineTaskState state)
            {
                if (cache.TryGetValue(id, out var cached)) { state = cached.state; return cached.result; }
                state = OfflineMainlineTaskState.Unavailable;
                if (!visiting.Add(id)) return ConditionEvaluation.Unsupported("TaskCompleteCondition:Cycle:" + id);
                ConditionEvaluation result;
                try
                {
                    if (save.Tasks.ClaimedMainlineTaskIds.Contains(id))
                    { state = OfflineMainlineTaskState.Received; result = ConditionEvaluation.Met(true); }
                    else if (owner.catalog == null || !owner.catalog.TryGetTask(id, out var task))
                        result = ConditionEvaluation.Unsupported("TaskCompleteCondition:TaskUnavailable:" + id);
                    else
                    {
                        result = owner.Evaluate(task.acquestCondition, save, 0, this);
                        if (result.Status != ProgressionResultStatus.Unsupported && result.IsMet)
                        {
                            state = OfflineMainlineTaskState.Processing;
                            result = owner.Evaluate(task.finishCondition, save, 0, this);
                            if (result.Status != ProgressionResultStatus.Unsupported && result.IsMet)
                                state = OfflineMainlineTaskState.Completed;
                        }
                    }
                }
                finally { visiting.Remove(id); }
                cache[id] = (state, result);
                return result;
            }
        }

        private ConditionEvaluation CompletedTaskProgress(OfficialTaskCondition condition,
            OfflineMainlineSave save, TaskStateProjection projection, out long current)
        {
            current = 0;
            if (condition.taskClassIds == null)
                return ConditionEvaluation.Unsupported("TaskCompleteCondition:TaskIdsMissing");
            if (projection == null) projection = new TaskStateProjection(this, save);
            foreach (int id in condition.taskClassIds)
            {
                // Original TaskCompleteCondition.GetValue counts submitted IDs
                // and active Completed task instances; Received is not active.
                if (save.Tasks.ClaimedMainlineTaskIds.Contains(id)) current++;
                var result = projection.Get(id, out var state);
                if (result.Status == ProgressionResultStatus.Unsupported) return result;
                if (state == OfflineMainlineTaskState.Completed) current++;
            }
            return ConditionEvaluation.Met(current >= condition.taskClassIds.Length);
        }
    }
}
