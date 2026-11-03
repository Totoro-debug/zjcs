public struct GrapplingLineSetting
{
	public int LinkDensity { get; private set; }

	public float LinkLength { get; private set; }

	public int Stiffness { get; private set; }

	public float DiffusionAngle { get; private set; }

	public float DiffusionStrength { get; private set; }

	public GrapplingLineSetting(int linkDensity = 1, float linkLength = 0.1f, int stiffness = 10, float diffusionAngle = 90f, float diffusionStrength = 100f)
	{
		LinkDensity = linkDensity;
		LinkLength = linkLength;
		Stiffness = stiffness;
		DiffusionAngle = diffusionAngle;
		DiffusionStrength = diffusionStrength;
	}
}
