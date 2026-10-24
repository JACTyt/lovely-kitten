using Godot;

/// <summary>Title screen: animated cat over the equipped world, Play / Settings / Quit.</summary>
public partial class MainMenu : Control
{
	private static readonly Texture2D CatSheet = GD.Load<Texture2D>("res://sprites/Cat Sprite Sheet.png");
	private static readonly Shader CatPalette = GD.Load<Shader>("res://shaders/cat_palette.gdshader");
	private static readonly int[] IdleFrames = { 0, 1, 2, 3, 0, 1, 2, 3, 24, 25, 26, 27 };

	private CenterContainer _center;
	private Control _main;
	private AtlasTexture _catFrame;
	private float _animT;

	public override void _Ready()
	{
		Theme = UiKit.Theme;
		SetAnchorsPreset(LayoutPreset.FullRect);
		RunState.I.Abandon();
		Audio.I.SetMusic(false);

		string world = GameState.I.EquippedDef(Catalog.SlotWorld)?.World ?? "street";
		var bg = new TextureRect
		{
			Texture = BackgroundController.Texture(world, false),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = TextureFilterEnum.Nearest,
		};
		bg.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(bg);

		// Cat sits on the sidewalk, in the equipped fur colour.
		var pal = GameState.I.EquippedDef(Catalog.SlotCatSkin)?.Palette;
		var mat = new ShaderMaterial { Shader = CatPalette };
		if (pal != null)
		{
			mat.SetShaderParameter("light_col", pal[0]);
			mat.SetShaderParameter("mid_col", pal[1]);
			mat.SetShaderParameter("dark_col", pal[2]);
		}
		_catFrame = new AtlasTexture { Atlas = CatSheet, Region = new Rect2(0, 0, 32, 32) };
		var cat = UiKit.Icon(_catFrame, 320, mat);
		cat.SetAnchorsPreset(LayoutPreset.BottomLeft);
		cat.Position = new Vector2(120, -470);
		AddChild(cat);

		_center = new CenterContainer();
		_center.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(_center);

		var col = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
		col.AddThemeConstantOverride("separation", 16);
		var title = UiKit.Label("Lovely Kitten", 72, UiKit.Cream, outline: true);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		col.AddChild(title);
		var gs = GameState.I;
		string rebirth = gs.Rebirths > 0 ? $"Rebirth {GameState.Roman(gs.Rebirths)} · " : "";
		var sub = UiKit.Label($"{rebirth}{gs.Stars} stars · best: {gs.BestDay} days, combo x{gs.BestCombo}", 24, Colors.White,
			outline: true);
		sub.HorizontalAlignment = HorizontalAlignment.Center;
		col.AddChild(sub);
		col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });
		col.AddChild(UiKit.Button("Play", () => GetTree().ChangeSceneToFile("res://scene/main_scene.tscn")));
		col.AddChild(UiKit.Button("Collection", ShowCollection));
		col.AddChild(UiKit.Button("Settings", ShowSettings));
		col.AddChild(UiKit.Button("Quit", () =>
		{
			GameState.I.Commit();
			GetTree().Quit();
		}));
		_main = col;
		_center.AddChild(_main);
	}

	public override void _Process(double delta)
	{
		_animT += (float)delta * 8f;
		int f = IdleFrames[(int)_animT % IdleFrames.Length];
		_catFrame.Region = new Rect2(f % 8 * 32, f / 8 * 32, 32, 32);
	}

	private void ShowSettings()
	{
		_main.Visible = false;
		_center.AddChild(Settings.Build(ShowMain));
	}

	private void ShowCollection()
	{
		_main.Visible = false;
		_center.AddChild(new Collection { OnClose = ShowMain });
	}

	private void ShowMain()
	{
		foreach (var c in _center.GetChildren())
			if (c != _main)
				c.QueueFree();
		_main.Visible = true;
		GetTree().ReloadCurrentScene(); // refresh star count, fur colour and world
	}
}
