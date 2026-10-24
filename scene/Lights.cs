using Godot;

/// <summary>Shared soft radial light texture for PointLight2D (lantern, fireflies).</summary>
public static class Lights
{
	private static GradientTexture2D _radial;

	public static Texture2D Radial => _radial ??= new GradientTexture2D
	{
		Width = 64,
		Height = 64,
		Fill = GradientTexture2D.FillEnum.Radial,
		FillFrom = new Vector2(0.5f, 0.5f),
		FillTo = new Vector2(1f, 0.5f),
		Gradient = new Gradient
		{
			Offsets = new[] { 0f, 1f },
			Colors = new[] { new Color(1, 1, 1, 1), new Color(1, 1, 1, 0) },
		},
	};
}
