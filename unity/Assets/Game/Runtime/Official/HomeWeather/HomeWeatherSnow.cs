// Selected original shared snow controller; see its source receipt.
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using MainModule = UnityEngine.ParticleSystem.MainModule;
using Zjcs.Official.HomeEnvironmentTime;
using Zjcs.Official.HomeEntityCreation;
using Zjcs.Official.HomeFurniture;
namespace Zjcs.Official.HomeWeather
{
public class HomeWeatherSnow : MonoBehaviour, IHomeEntityChangeCreateNotify
{
    [SerializeField] private float mAppearEffectDuration = 10f;
    [SerializeField] private ParticleSystem[] mSnowParticleSystems;
    [SerializeField] private AnimationCurve mParticleAppearCurve;
    private static int sSnowTimeId = Shader.PropertyToID("_SnowTime");
    private CancellationTokenSource mCancellationDisposable;
    private int[] mSnowParticleSystemInitialMaxParticles;
    public IHomeEnvironmentManager HomeEnvironmentManager { get; set; }
    public IHomeFurnitureViewModes HomeData { get; set; }
private void Awake()
	{
		mSnowParticleSystemInitialMaxParticles = new int[mSnowParticleSystems.Length];
		for (int i = 0; i < mSnowParticleSystems.Length; i++)
		{
			MainModule main = mSnowParticleSystems[i].main;
			mSnowParticleSystemInitialMaxParticles[i] = main.maxParticles;
		}
	}

private void OnEnable()
	{
		Shader.SetGlobalFloat(sSnowTimeId, 1f);
		SetSnowParticleSystemProgress(1f);
	}

private void OnDisable()
	{
		CancelAppearEffect();
	}

public void OnChangeCreate()
	{
		if (HomeData.HomeViewMode != HomeViewMode.TakePhoto)
		{
			CancelAppearEffect();
			mCancellationDisposable = new CancellationTokenSource();
			UniTaskVoid val = PlayAppearEffectAsync(mCancellationDisposable.Token);
			val.Forget();
		}
	}
    private async UniTaskVoid PlayAppearEffectAsync(CancellationToken cancellationToken)
    {
        float elapsedTime = 0f;
        float duration = HomeEnvironmentManager.Config.Value.HomeTimeScaleRuntime * mAppearEffectDuration;
        while (elapsedTime < duration && !cancellationToken.IsCancellationRequested)
        {
            elapsedTime += HomeEnvironmentManager.DeltaTime;
            float progress = Mathf.Clamp01(elapsedTime / duration);
            SetSnowParticleSystemProgress(progress);
            Shader.SetGlobalFloat(sSnowTimeId, progress);
            await UniTask.Yield(cancellationToken, false);
        }
    }
private void CancelAppearEffect()
	{
		if (mCancellationDisposable != null)
		{
			mCancellationDisposable.Cancel();
            mCancellationDisposable.Dispose();
			mCancellationDisposable = null;
		}
	}

private void SetSnowParticleSystemProgress(float progress)
	{
		progress = mParticleAppearCurve.Evaluate(progress);
		for (int i = 0; i < mSnowParticleSystems.Length; i++)
		{
			MainModule main = mSnowParticleSystems[i].main;
			main.maxParticles = (int)Mathf.Lerp(0f, (float)mSnowParticleSystemInitialMaxParticles[i], progress);
		}
	}
}
}
