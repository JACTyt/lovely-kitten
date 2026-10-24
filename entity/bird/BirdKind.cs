using Godot;

public enum BirdKind { Sparrow, Swift, Golden, Owl, Firefly, Crow }

/// <summary>Per-kind tuning. Reach is added to the cat's catch radius (bigger target = easier).</summary>
public readonly record struct BirdInfo(
	int Coins, float Speed, float Amplitude, float Frequency, float Reach, Vector2 CenterOffset,
	Color Particles, float HueShift = 0f, float TintAmount = 0f, Color Tint = default, float ValueMul = 1f);

public static class BirdKinds
{
	public static BirdInfo Info(BirdKind kind) => kind switch
	{
		BirdKind.Sparrow => new(1, 35f, 10f, 3f, 0f, new Vector2(0, 2), new Color("c9a27a")),
		BirdKind.Swift => new(3, 70f, 5f, 5f, -1f, new Vector2(0, 2), new Color("8fd3ff"), HueShift: 0.55f),
		BirdKind.Golden => new(10, 50f, 8f, 3.5f, 1f, new Vector2(0, 2), new Color("f2c84b"),
			TintAmount: 0.65f, Tint: new Color("ffd84a"), ValueMul: 1.25f),
		BirdKind.Owl => new(5, 25f, 4f, 1.5f, 2f, new Vector2(0, 1), new Color("8a6a4f")),
		BirdKind.Firefly => new(2, 15f, 6f, 1.3f, -2f, Vector2.Zero, new Color("e6ff8c")),
		// Crow King's minions
		BirdKind.Crow => new(2, 55f, 6f, 4f, 0f, new Vector2(0, 2), new Color("4a4466"),
			TintAmount: 0.8f, Tint: new Color("3a3450"), ValueMul: 0.6f),
		_ => default,
	};

	/// <summary>Bird skins only recolour ordinary day birds; special kinds keep their identity.</summary>
	public static bool Skinnable(BirdKind kind) => kind is BirdKind.Sparrow or BirdKind.Swift;
}
