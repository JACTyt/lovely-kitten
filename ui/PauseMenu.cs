using Godot;

/// <summary>Esc pause overlay: Resume / Settings / Main Menu.</summary>
public partial class PauseMenu : CanvasLayer
{
	public bool IsOpen => _root.Visible;

	private Control _root;
	private CenterContainer _center;
	private Control _main;

	public override void _Ready()
	{
		Layer = 30;
		ProcessMode = ProcessModeEnum.Always;

		_root = new Control { Theme = UiKit.Theme, Visible = false };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		var dim = new ColorRect { Color = new Color(0.07f, 0.06f, 0.12f, 0.6f) };
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(dim);

		_center = new CenterContainer();
		_center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(_center);

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(420, 0) };
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 14);
		var title = UiKit.Label("Paused", 40);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		col.AddChild(title);
		col.AddChild(UiKit.Button("Resume", Close));
		col.AddChild(UiKit.Button("Controls", ShowControls));
		col.AddChild(UiKit.Button("Settings", ShowSettings));
		col.AddChild(UiKit.Button("Abandon run", () =>
		{
			GetTree().Paused = false;
			RunState.I.Abandon();
			GetTree().ChangeSceneToFile("res://ui/main_menu.tscn");
		}));
		panel.AddChild(col);
		_main = panel;
		_center.AddChild(_main);
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (!e.IsActionPressed("pause"))
			return;
		if (IsOpen)
			Close();
		else
			Open();
		GetViewport().SetInputAsHandled();
	}

	private void Open()
	{
		_root.Visible = true;
		GetTree().Paused = true;
	}

	private void Close()
	{
		ShowMain();
		_root.Visible = false;
		GetTree().Paused = false;
	}

	private void ShowSettings()
	{
		_main.Visible = false;
		_center.AddChild(Settings.Build(ShowMain));
	}

	private void ShowControls()
	{
		_main.Visible = false;
		var panel = new PanelContainer { CustomMinimumSize = new Vector2(560, 0) };
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 8);
		col.AddChild(UiKit.Label("Controls", 36));
		foreach (var (keys, what) in new[]
		         {
			         ("A / D  or  arrows", "Move"),
			         ("Space / W / Up", "Jump (hold for higher)"),
			         ("C", "Nap: birds fly low and slow"),
			         ("Esc", "Pause"),
		         })
			col.AddChild(ControlRow(keys, what));
		col.AddChild(UiKit.Label("Skills (buy them at night)", 26));
		foreach (var id in Skills.Order)
		{
			var item = Catalog.Item(id);
			col.AddChild(ControlRow(item.Key + (id == "dive" ? " / Down (in air)" : ""), item.Name));
		}
		col.AddChild(UiKit.Button("Back", ShowMain));
		panel.AddChild(col);
		_center.AddChild(panel);
	}

	private static Control ControlRow(string keys, string what)
	{
		var row = new HBoxContainer();
		var k = UiKit.Label(keys, 22);
		k.CustomMinimumSize = new Vector2(240, 0);
		row.AddChild(k);
		row.AddChild(UiKit.Label(what, 22, new Color("5e5866")));
		return row;
	}

	private void ShowMain()
	{
		foreach (var c in _center.GetChildren())
			if (c != _main)
				c.QueueFree();
		_main.Visible = true;
	}
}
