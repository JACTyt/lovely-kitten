using Godot;

/// <summary>Shown when a day's goal is missed: run summary, stars earned, Try again / Main menu.</summary>
public partial class RunOver : CanvasLayer
{
	private Control _root;
	private CenterContainer _center;

	public override void _Ready()
	{
		Layer = 25;
		_root = new Control { Theme = UiKit.Theme, Visible = false };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		var dim = new ColorRect { Color = new Color(0.07f, 0.06f, 0.12f, 0.55f) };
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(dim);

		_center = new CenterContainer();
		_center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(_center);

		RunState.I.PhaseChanged += OnPhaseChanged;
	}

	public override void _ExitTree()
	{
		RunState.I.PhaseChanged -= OnPhaseChanged;
	}

	private void OnPhaseChanged(int phase)
	{
		if ((RunState.Phase)phase != RunState.Phase.Over)
			return;
		Build();
		_root.Visible = true;
		Audio.I.Play("deny", 0f);
	}

	private void Build()
	{
		foreach (var c in _center.GetChildren())
			c.QueueFree();

		var run = RunState.I;
		var gs = GameState.I;
		var panel = new PanelContainer { CustomMinimumSize = new Vector2(520, 0) };
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 10);
		panel.AddChild(col);
		_center.AddChild(panel);

		Line(col, "The kitten is too tired…", 36);
		Line(col, run.IsBossDay
			? $"The Crow King got away ({run.BossHp}/{run.BossMaxHp} HP left)"
			: $"Day {run.Day} goal missed: {run.DayCoins}/{run.Goal}", 22, new Color("d9475b"));
		col.AddChild(new HSeparator());
		Line(col, $"Days survived: {run.DaysCompleted}" + (run.DaysCompleted >= gs.BestDay && run.DaysCompleted > 0 ? "  (best!)" : ""), 24);
		Line(col, $"Birds caught: {run.Caught}", 24);
		Line(col, $"Best combo: x{run.BestCombo}", 24);

		var stars = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		stars.AddChild(UiKit.Icon(UiKit.Tex("ui_star"), 36));
		stars.AddChild(UiKit.Label($"+{run.StarsEarned} stars   (total {gs.Stars})", 28));
		col.AddChild(stars);

		col.AddChild(UiKit.Button("Try again", () => GetTree().ReloadCurrentScene()));
		col.AddChild(UiKit.Button("Main menu", () => GetTree().ChangeSceneToFile("res://ui/main_menu.tscn")));
	}

	private static void Line(VBoxContainer col, string text, int size, Color? color = null)
	{
		var l = UiKit.Label(text, size, color);
		l.HorizontalAlignment = HorizontalAlignment.Center;
		col.AddChild(l);
	}
}
