using System;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // Cache metadata is separate from the player's gameplay save. The original
    // persister owns version comparison, image names and expiry decisions.
    public sealed class OfflineProfessionPortraitState : IPersistentState<CharacterPortraitPersisterState>
    {
        private readonly string path;
        public CharacterPortraitPersisterState State { get; private set; }
        [Serializable] private sealed class Payload { public int Id; public int CodeVersion; }

        public OfflineProfessionPortraitState(string cacheRoot)
        {
            if (string.IsNullOrWhiteSpace(cacheRoot)) throw new ArgumentException(nameof(cacheRoot));
            path = Path.Combine(Path.GetFullPath(cacheRoot), "CharacterPortraitState.json");
            ReadStateAsync().GetAwaiter().GetResult();
        }

        public UniTask ReadStateAsync()
        {
            var data = File.Exists(path) ? JsonUtility.FromJson<Payload>(File.ReadAllText(path, Encoding.UTF8)) : null;
            State = data == null ? new CharacterPortraitPersisterState()
                : new CharacterPortraitPersisterState { Id = data.Id, CodeVersion = data.CodeVersion };
            return UniTask.CompletedTask;
        }

        public UniTask WriteStateAsync()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var data = new Payload { Id = State.Id, CodeVersion = State.CodeVersion };
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(data), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
            return UniTask.CompletedTask;
        }

        public UniTask ClearStateAsync()
        {
            if (File.Exists(path)) File.Delete(path);
            State = new CharacterPortraitPersisterState();
            return UniTask.CompletedTask;
        }
    }
}
