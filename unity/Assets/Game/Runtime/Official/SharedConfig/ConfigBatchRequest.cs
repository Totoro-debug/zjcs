using System;
using UnityEngine;

namespace GConfig
{
    public class ConfigBatchRequest : CustomYieldInstruction
    {
        public int finishedCount;
        public int totalCount;
        public bool Completed => finishedCount >= totalCount;
        public override bool keepWaiting => finishedCount < totalCount;
        public float Progress => totalCount == 0 ? 1f : Math.Min(1f, (float)finishedCount / totalCount);
    }
}
