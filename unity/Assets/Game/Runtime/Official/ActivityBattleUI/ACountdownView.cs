// Migrated selected original countdown; see activity-countdown-runtime-source-manifest.json.
namespace Zjcs.Official.ActivityBattleUI
{
using System;
using UnityEngine;

[Serializable]
public abstract class ACountdownView : MonoBehaviour
{
	public abstract void UpdateCountdown(float progress, TimeSpan timeSpan);
}
}
