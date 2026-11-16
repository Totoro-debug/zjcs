namespace Zjcs.Official.HomeCamera
{
public class PerformanceSettingState
{
	public int Id { get; set; }
	public bool? BloomEnabled { get; set; }
	public EFrameRateMode? FrameRateMode { get; set; }
	public ESettingsLevel? MapCharacterGridOverlayLevel { get; set; }
	public ERenderQualityLevel? RenderQualityLevel { get; set; }
	public bool? ReflectionEnabled { get; set; }
}
}
