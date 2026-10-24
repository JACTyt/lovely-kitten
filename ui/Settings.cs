using Godot;

/// <summary>Volume settings panel shared by the main menu and the pause menu.</summary>
public static class Settings
{
	public static Control Build(System.Action onBack)
	{
		var gs = GameState.I;
		var panel = new PanelContainer { CustomMinimumSize = new Vector2(560, 0) };
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 14);
		panel.AddChild(col);
		col.AddChild(UiKit.Label("Settings", 36));

		HSlider master = null, music = null, sfx = null;
		void Apply() => gs.SetVolumes((float)master.Value, (float)music.Value, (float)sfx.Value);
		master = Slider(col, "Master", gs.MasterVolume, Apply);
		music = Slider(col, "Music", gs.MusicVolume, Apply);
		sfx = Slider(col, "Sound FX", gs.SfxVolume, () =>
		{
			Apply();
			Audio.I.Play("coin", 0f);
		});

		col.AddChild(UiKit.Button("Back", () =>
		{
			gs.Commit();
			onBack();
		}));
		return panel;
	}

	private static HSlider Slider(VBoxContainer col, string name, float value, System.Action changed)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 16);
		var label = UiKit.Label(name, 24);
		label.CustomMinimumSize = new Vector2(150, 0);
		row.AddChild(label);
		var s = new HSlider
		{
			MinValue = 0, MaxValue = 1, Step = 0.05, Value = value,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(0, 24), FocusMode = Control.FocusModeEnum.None,
		};
		s.ValueChanged += _ => changed();
		row.AddChild(s);
		col.AddChild(row);
		return s;
	}
}
