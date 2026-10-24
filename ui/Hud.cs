using Godot;
using System.Collections.Generic;

/// <summary>Run coins, day + goal bar, day clock, combo, controls hint and floating labels that track world positions.</summary>
public partial class Hud : CanvasLayer
{
	private const float FloatTime = 0.9f;
	private const float FloatRise = 70f;

	private Control _root;
	private Label _coins;
	private Label _day;
	private Label _goal;
	private ProgressBar _goalBar;
	private Label _combo;
	private TextureRect _clockIcon;
	private ProgressBar _clock;
	private Label _clockText;
	private Control _floatLayer;
	private HBoxContainer _skillBar;
	private readonly Dictionary<string, (Control slot, ColorRect shade)> _skillSlots = new();
	private Skills _skills;
	private Label _banner;
	private Label _bannerSub;
	private Control _bannerBox;
	private ColorRect _slowTint;
	private Texture2D _sun;
	private Texture2D _moon;
	private int _shownCoins = -1;
	private bool _goalWasMet;
	private readonly List<(Label label, Vector2 world, float age)> _floats = new();

	public override void _Ready()
	{
		Layer = 10;
		_sun = UiKit.Tex("ui_sun");
		_moon = UiKit.Tex("ui_moon");

		_root = new Control { Theme = UiKit.Theme, MouseFilter = Control.MouseFilterEnum.Ignore };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		_slowTint = new ColorRect { Color = new Color(0.35f, 0.55f, 1f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		_slowTint.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(_slowTint);

		_floatLayer = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		_floatLayer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(_floatLayer);

		// Top-left: day, goal progress, coins
		var left = new PanelContainer { Position = new Vector2(16, 16), CustomMinimumSize = new Vector2(280, 0) };
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 4);
		var dayRow = new HBoxContainer();
		_day = UiKit.Label("Day 1", 28);
		_day.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		dayRow.AddChild(_day);
		dayRow.AddChild(UiKit.Icon(UiKit.Tex("ui_coin"), 28));
		_coins = UiKit.Label("0", 28);
		dayRow.AddChild(_coins);
		col.AddChild(dayRow);
		_goalBar = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, 20) };
		col.AddChild(_goalBar);
		_goal = UiKit.Label("", 18, new Color("5e5866"));
		col.AddChild(_goal);
		left.AddChild(col);
		_root.AddChild(left);

		// Top-right: day clock
		var clockPanel = new PanelContainer();
		_root.AddChild(clockPanel);
		var clockRow = new HBoxContainer();
		clockRow.AddThemeConstantOverride("separation", 10);
		_clockIcon = UiKit.Icon(_sun, 36);
		clockRow.AddChild(_clockIcon);
		var clockCol = new VBoxContainer();
		_clockText = UiKit.Label("75s", 20);
		clockCol.AddChild(_clockText);
		_clock = new ProgressBar
		{
			MinValue = 0, MaxValue = 1, Step = 0.001, ShowPercentage = false, CustomMinimumSize = new Vector2(160, 16),
		};
		clockCol.AddChild(_clock);
		clockRow.AddChild(clockCol);
		clockPanel.AddChild(clockRow);
		clockPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight, Control.LayoutPresetMode.Minsize, 16);
		clockPanel.GrowHorizontal = Control.GrowDirection.Begin;

		// Top-centre: combo
		_combo = UiKit.Label("COMBO", 40, UiKit.Gold, outline: true);
		_combo.HorizontalAlignment = HorizontalAlignment.Center;
		_root.AddChild(_combo);
		_combo.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop, Control.LayoutPresetMode.Minsize, 24);
		_combo.GrowHorizontal = Control.GrowDirection.Both;

		// Centre: bird event banner
		_bannerBox = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
		_banner = UiKit.Label("", 48, UiKit.Cream, outline: true);
		_banner.HorizontalAlignment = HorizontalAlignment.Center;
		_bannerSub = UiKit.Label("", 22, Colors.White, outline: true);
		_bannerSub.HorizontalAlignment = HorizontalAlignment.Center;
		_bannerBox.AddChild(_banner);
		_bannerBox.AddChild(_bannerSub);
		_root.AddChild(_bannerBox);
		_bannerBox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop, Control.LayoutPresetMode.Minsize, 110);
		_bannerBox.GrowHorizontal = Control.GrowDirection.Both;

		// Bottom: skill bar (only skills owned this run)
		_skillBar = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		_skillBar.AddThemeConstantOverride("separation", 10);
		_root.AddChild(_skillBar);
		_skillBar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom, Control.LayoutPresetMode.Minsize, 14);
		_skillBar.GrowHorizontal = Control.GrowDirection.Both;
		_skillBar.GrowVertical = Control.GrowDirection.Begin;

		var sc = GetParent<SceneController>();
		_skills = sc.GetNode<Skills>("Skills");
		_skills.SkillReady += id =>
		{
			if (_skillSlots.TryGetValue(id, out var s))
				Pop(s.slot, 1.25f);
		};
		sc.BirdEvent += ShowBanner;
		sc.Popup += (text, at, color) => Float(text, at, color, 28);
		RunState.I.Changed += RebuildSkills;
		sc.Caught += OnCaught;
		sc.ComboChanged += OnComboChanged;
		sc.CatnipUsed += at => Float("Catnip!", at, new Color("7fc46a"), 26);
		RunState.I.PhaseChanged += OnPhaseChanged;
		OnComboChanged(0);
	}

	public override void _ExitTree()
	{
		RunState.I.PhaseChanged -= OnPhaseChanged;
		RunState.I.Changed -= RebuildSkills;
	}

	public override void _Process(double delta)
	{
		var run = RunState.I;
		_root.Visible = run.Current is not (RunState.Phase.Night or RunState.Phase.Over);

		if (run.Coins != _shownCoins)
		{
			if (_shownCoins >= 0 && run.Coins > _shownCoins)
				Pop(_coins, 1.25f);
			_shownCoins = run.Coins;
			_coins.Text = run.Coins.ToString();
		}

		_day.Text = $"Day {run.Day}";
		if (run.IsBossDay)
		{
			_goalBar.MaxValue = Mathf.Max(1, run.BossMaxHp);
			_goalBar.Value = run.BossHp;
			_goal.Text = run.BossDefeated ? "Crow King defeated!" : $"Crow King  {run.BossHp}/{run.BossMaxHp} HP";
			_goal.AddThemeColorOverride("font_color", run.BossDefeated ? new Color("3f7a3c") : new Color("d9475b"));
		}
		else
		{
			_goalBar.MaxValue = run.Goal;
			_goalBar.Value = Mathf.Min(run.DayCoins, run.Goal);
			_goal.Text = run.GoalMet ? $"Goal reached! {run.DayCoins}/{run.Goal}" : $"Goal {run.DayCoins}/{run.Goal}";
			_goal.AddThemeColorOverride("font_color", new Color("5e5866"));
		}
		if (run.GoalMet && !_goalWasMet)
		{
			Pop(_goal, 1.4f);
			Audio.I.Play("buy", 0f);
		}
		_goalWasMet = run.GoalMet;

		_clock.Value = run.DayProgress;
		_clockIcon.Texture = run.Current == RunState.Phase.Dusk || run.Current == RunState.Phase.Sleeping ? _moon : _sun;
		_clockText.Text = run.Current == RunState.Phase.Dusk ? $"Dusk {run.TimeLeft:0}s" : $"{run.TimeLeft:0}s";
		_clockText.AddThemeColorOverride("font_color", run.TimeLeft < 10f ? new Color("d9475b") : UiKit.Ink);

		foreach (var (id, (_, shade)) in _skillSlots)
		{
			float f = _skills.Fraction(id);
			shade.AnchorTop = 1f - f;
			shade.Visible = f > 0f;
		}
		var tint = _slowTint.Color;
		tint.A = Mathf.MoveToward(tint.A, _skills.CatTimeActive ? 0.09f : 0f, (float)delta * 0.6f);
		_slowTint.Color = tint;

		var xf = GetViewport().GetCanvasTransform();
		float d = (float)delta;
		for (int i = _floats.Count - 1; i >= 0; i--)
		{
			var (label, world, age) = _floats[i];
			age += d;
			if (age >= FloatTime)
			{
				label.QueueFree();
				_floats.RemoveAt(i);
				continue;
			}
			float k = age / FloatTime;
			label.Position = xf * world - label.Size / 2 - new Vector2(0, FloatRise * Mathf.Sqrt(k));
			label.Modulate = new Color(1, 1, 1, 1f - k * k);
			_floats[i] = (label, world, age);
		}
	}

	private void OnPhaseChanged(int phase)
	{
		if ((RunState.Phase)phase == RunState.Phase.Dusk)
			Float("Dusk — owls are out!", ToWorldCenter(), new Color("ffc89a"), 30);
		else if ((RunState.Phase)phase == RunState.Phase.Day)
		{
			_goalWasMet = false;
			RebuildSkills();
		}
	}

	private Vector2 ToWorldCenter() => GetViewport().GetCanvasTransform().AffineInverse() * (GetViewport().GetVisibleRect().Size / 2);

	private void OnCaught(int coins, int combo, Vector2 globalAt, bool style) =>
		Float(style ? $"+{coins} Style!" : $"+{coins}", globalAt, combo >= 2 || style ? UiKit.Gold : UiKit.Cream,
			combo >= 2 || style ? 34 : 28);

	private void ShowBanner(string title, string subtitle)
	{
		_banner.Text = title;
		_bannerSub.Text = subtitle;
		var t = _bannerBox.CreateTween();
		t.TweenProperty(_bannerBox, "modulate:a", 1f, 0.2);
		t.TweenInterval(2.2);
		t.TweenProperty(_bannerBox, "modulate:a", 0f, 0.5);
	}

	private void RebuildSkills()
	{
		var owned = new List<string>();
		foreach (var id in Skills.Order)
			if (RunState.I.Has(id))
				owned.Add(id);
		if (owned.Count == _skillSlots.Count && owned.TrueForAll(_skillSlots.ContainsKey))
			return;

		foreach (var c in _skillBar.GetChildren())
			c.QueueFree();
		_skillSlots.Clear();
		foreach (var id in owned)
		{
			var item = Catalog.Item(id);
			var slot = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			var iconBox = new Control { CustomMinimumSize = new Vector2(44, 44), MouseFilter = Control.MouseFilterEnum.Ignore };
			var icon = UiKit.Icon(UiKit.Tex($"ui_icon_{item.Icon}"), 44);
			icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			iconBox.AddChild(icon);
			var shade = new ColorRect { Color = new Color(0.17f, 0.14f, 0.2f, 0.6f), MouseFilter = Control.MouseFilterEnum.Ignore };
			shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			iconBox.AddChild(shade);
			col.AddChild(iconBox);
			var key = UiKit.Label(item.Key, 16);
			key.HorizontalAlignment = HorizontalAlignment.Center;
			col.AddChild(key);
			slot.AddChild(col);
			_skillBar.AddChild(slot);
			_skillSlots[id] = (slot, shade);
		}
	}

	private void Float(string text, Vector2 globalAt, Color color, int size)
	{
		var l = UiKit.Label(text, size, color, outline: true);
		_floatLayer.AddChild(l);
		_floats.Add((l, globalAt, 0f));
	}

	private void OnComboChanged(int combo)
	{
		_combo.Visible = combo >= 2;
		if (!_combo.Visible)
			return;
		_combo.Text = $"COMBO x{combo}   ×{SceneController.ComboMultiplier(combo):0.0}";
		Pop(_combo, 1.3f);
	}

	private static void Pop(Control c, float scale)
	{
		c.PivotOffset = c.Size / 2;
		c.Scale = new Vector2(scale, scale);
		c.CreateTween().TweenProperty(c, "scale", Vector2.One, 0.18).SetTrans(Tween.TransitionType.Back)
			.SetEase(Tween.EaseType.Out);
	}
}
