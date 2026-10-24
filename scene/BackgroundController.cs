using Godot;

/// <summary>Tints the world warm at dusk and blue at night, cross-fading to the equipped world's night background.</summary>
public partial class BackgroundController : Node2D
{
	private const float DuskEnd = 0.35f;
	private static readonly Color DuskTint = new("ffc89a");
	// Night backgrounds are pre-darkened, so the canvas tint only needs to dim sprites, not the scene.
	private static readonly Color NightTint = new("7880b0");

	[Export] private Sprite2D _day;
	[Export] private Sprite2D _night;
	[Export] private CanvasModulate _tint;

	private string _world;

	public override void _Ready()
	{
		GameState.I.Changed += Refresh;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (GameState.I != null)
			GameState.I.Changed -= Refresh;
	}

	public override void _Process(double delta)
	{
		float n = RunState.I.NightAmount;
		float night = Mathf.Clamp((n - DuskEnd) / (1f - DuskEnd), 0f, 1f);
		_night.Modulate = new Color(1, 1, 1, night);
		_tint.Color = n <= DuskEnd
			? Colors.White.Lerp(DuskTint, n / DuskEnd)
			: DuskTint.Lerp(NightTint, night);
	}

	public static Texture2D Texture(string world, bool night) =>
		GD.Load<Texture2D>($"res://sprites/generated/bg_{world}_{(night ? "night" : "day")}.png");

	private void Refresh()
	{
		string world = GameState.I.EquippedDef(Catalog.SlotWorld)?.World ?? "street";
		if (world == _world)
			return;
		_world = world;
		_day.Texture = Texture(world, false);
		_night.Texture = Texture(world, true);
	}
}
