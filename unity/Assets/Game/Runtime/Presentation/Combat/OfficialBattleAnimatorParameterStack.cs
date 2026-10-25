using System.Collections.Generic;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattleAnimatorParameterStack<T>
    {
        private struct Entry
        {
            public long Owner;
            public T Value;
        }

        private readonly T initialValue;
        private readonly List<Entry> entries = new List<Entry>();

        public OfficialBattleAnimatorParameterStack(T initialValue)
        {
            this.initialValue = initialValue;
        }

        public int Count => entries.Count;

        public T Current => entries.Count == 0 ? initialValue : entries[entries.Count - 1].Value;

        public void Push(long owner, T value)
        {
            Remove(owner);
            entries.Add(new Entry { Owner = owner, Value = value });
        }

        public bool Remove(long owner)
        {
            for (int index = entries.Count - 1; index >= 0; index--)
            {
                if (entries[index].Owner != owner) continue;
                entries.RemoveAt(index);
                return true;
            }
            return false;
        }
    }
}
