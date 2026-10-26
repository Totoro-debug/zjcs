using UnityEngine;

namespace Zjcs.Official.Startup
{
    public sealed class LaunchUIRoot : MonoBehaviour
    {
        [SerializeField] private LaunchPanel mLaunchPanel;
        public LaunchPanel LaunchPanel => mLaunchPanel;
    }
}
