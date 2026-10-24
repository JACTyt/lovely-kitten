using Godot;

/// <summary>One-shot feather burst that frees itself when done.</summary>
public static class Feathers
{
	private static readonly Texture2D Feather = GD.Load<Texture2D>("res://sprites/generated/feather.png");

	public static void Burst(Node2D parent, Vector2 at, Color color)
	{
		var p = new CpuParticles2D
		{
			Position = at,
			Texture = Feather,
			Amount = 10,
			Lifetime = 0.7f,
			OneShot = true,
			Explosiveness = 1f,
			Direction = Vector2.Up,
			Spread = 180f,
			InitialVelocityMin = 20f,
			InitialVelocityMax = 45f,
			Gravity = new Vector2(0, 60f),
			DampingMin = 20f,
			DampingMax = 30f,
			AngularVelocityMin = -360f,
			AngularVelocityMax = 360f,
			ScaleAmountMin = 0.6f,
			ScaleAmountMax = 1.1f,
			Color = color.Lightened(0.3f),
		};
		p.Finished += p.QueueFree;
		parent.AddChild(p);
		p.Emitting = true;
	}
}
