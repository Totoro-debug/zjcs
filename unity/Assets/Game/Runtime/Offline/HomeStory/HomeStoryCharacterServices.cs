using System;
using Common;
using UnityEngine;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomeStoryCharacter;
using Zjcs.Offline.HomeUI;

namespace Zjcs.Offline.HomeStory
{
    // The dialogue owner supplies the selected regional and voice-over settings.
    public sealed class HomeStoryCharacterServices : IStoryPortraitServices
    {
        private readonly HomeStoryPortraitAssets portraits;
        private readonly Func<RegionCodeEnum> region;
        private readonly Func<int, bool> voiceOver;

        public HomeStoryCharacterServices(HomeStoryPortraitAssets portraits, Func<RegionCodeEnum> region,
            Func<int, bool> voiceOver)
        {
            this.portraits = portraits ? portraits : throw new ArgumentNullException(nameof(portraits));
            this.region = region ?? throw new ArgumentNullException(nameof(region));
            this.voiceOver = voiceOver ?? throw new ArgumentNullException(nameof(voiceOver));
        }

        public RegionCodeEnum Region() => region();
        public bool IsVoiceOver(int roleId) => voiceOver(roleId);

        public IHomeHudAssetHandle<GameObject> LoadPortrait(string path)
        {
            var prefab = path == "UI/Portrait/Npc_Portrait_8402" ? portraits.Portrait8402
                : path == "UI/Portrait/Npc_Portrait_8411" ? portraits.Portrait8411 : null;
            if (!prefab) throw new InvalidOperationException("Unbound original story portrait: " + path);
            return new Handle(prefab);
        }

        private sealed class Handle : IHomeHudAssetHandle<GameObject>
        {
            private bool valid = true;
            public GameObject Result { get; }
            public bool IsValid => valid && Result;
            public int Status => 2;
            public event Action<IHomeHudAssetHandle<GameObject>> Completed { add { value?.Invoke(this); } remove { } }
            public Handle(GameObject result) { Result = result; }
            public GameObject WaitForAsyncComplete() => Result;
            public void Dispose() => valid = false;
        }
    }
}
