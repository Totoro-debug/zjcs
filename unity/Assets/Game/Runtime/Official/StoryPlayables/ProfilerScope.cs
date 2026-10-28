// Migrated selected official source; see map14-story-playable-runtime source receipt.
using Object = UnityEngine.Object;
namespace Zjcs.Official.StoryPlayables
{
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Size = 1)]
public readonly struct ProfilerScope : IDisposable
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ProfilerScope Sample(string name)
	{
		return default;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Dispose()
	{
	}
}

}
