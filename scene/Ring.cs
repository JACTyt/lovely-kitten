using Godot;

/// <summary>Expanding, fading ring — visual for meow, swipe and dive shockwave. Frees itself.</summary>
public partial class Ring : Node2D
{
	private float _radius;
	private float _time;
	private float _age;
	private Color _color;

	public static void Spawn(Node2D parent, Vector2 at, float radius, Color color, float time = 0.35f)
	{
		parent.AddChild(new Ring { Position = at, _radius = radius, _color = color, _time = time });
	}

	public override void _Process(double delta)
	{
		_age += (float)delta;
		if (_age >= _time)
			QueueFree();
		QueueRedraw();
	}

	public override void _Draw()
	{
		float k = Mathf.Clamp(_age / _time, 0f, 1f);
		float r = _radius * (0.35f + 0.65f * Mathf.Sqrt(k));
		var c = new Color(_color, 1f - k);
		DrawArc(Vector2.Zero, r, 0f, Mathf.Tau, 32, c, 1.5f);
		DrawArc(Vector2.Zero, r * 0.8f, 0f, Mathf.Tau, 24, new Color(c, c.A * 0.4f), 1f);
	}
}
