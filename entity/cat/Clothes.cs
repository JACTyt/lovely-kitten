using Godot;

/// <summary>Places hat/neck/glasses sprites on the cat's head every frame using the generated anchor table.</summary>
public partial class Clothes : Node2D
{
	// Anchor pixel inside each 16x16 clothes cell, and offset from the cat's head-top pixel.
	private static readonly Vector2I HatAnchor = new(8, 12), HatOffset = new(0, 1);
	private static readonly Vector2I NeckAnchor = new(8, 8), NeckOffset = new(-1, 6);
	private static readonly Vector2I GlassesAnchor = new(10, 8), GlassesOffset = new(2, 3);

	private Cat _cat;
	private Sprite2D _hat;
	private Sprite2D _neck;
	private Sprite2D _glasses;

	public override void _Ready()
	{
		_cat = GetParent<Cat>();
		_hat = GetNode<Sprite2D>("Hat");
		_neck = GetNode<Sprite2D>("Neck");
		_glasses = GetNode<Sprite2D>("Glasses");
	}

	public override void _Process(double delta)
	{
		var head = CatAnchors.Head[_cat.Frame];
		bool flip = _cat.FlipH;

		Place(_hat, Catalog.SlotHat, 0, HatAnchor, HatOffset, head, flip);
		Place(_neck, Catalog.SlotNeck, 1, NeckAnchor, NeckOffset, head, flip);
		Place(_glasses, Catalog.SlotGlasses, 2, GlassesAnchor, GlassesOffset, head, flip);
	}

	private static void Place(Sprite2D s, string slot, int row, Vector2I anchor, Vector2I offset, Vector2I head, bool flip)
	{
		int cell = GameState.I.EquippedDef(slot)?.Cell ?? -1;
		s.Visible = cell >= 0 && head.X >= 0;
		if (!s.Visible)
			return;
		s.Frame = row * 3 + cell;
		// Cell pixel `anchor` lands on frame pixel `head + offset` (both sprites centered: 32px cat, 16px cell).
		var pos = head + offset - anchor - new Vector2I(8, 8);
		s.Position = new Vector2(flip ? -pos.X : pos.X, pos.Y);
		s.FlipH = flip;
	}
}
