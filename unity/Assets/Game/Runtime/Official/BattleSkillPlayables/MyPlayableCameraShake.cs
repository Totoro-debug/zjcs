using Cinemachine;
using UnityEngine;
using Zjcs.Official.StoryPlayables;

public class MyPlayableCameraShake : MyPlayable
{
    public CinemachineImpulseSource myImpulseSource;
    public Vector3 Offset = new Vector3(0f, 0f, -250f);
    public float Duration;

    protected override void PlayImpl()
    {
        if (myImpulseSource == null)
            myImpulseSource = GetComponent<CinemachineImpulseSource>();
        if (myImpulseSource != null)
        {
            myImpulseSource.transform.position = mySource.transform.position + Offset;
            myImpulseSource.GenerateImpulse();
        }
    }

    public override float GetDuration() => Duration;
}
