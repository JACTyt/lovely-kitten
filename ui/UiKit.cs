using Godot;
using System.Collections.Generic;

/// <summary>Shared pixel-art theme and small widget factories. Art is upscaled with nearest filtering.</summary>
public static class UiKit
{
	public const int Scale = 4;
	public static readonly Color Ink = new("2b2433");
	public static readonly Color Gold = new("f2c84b");
	public static readonly Color Cream = new("fff3c4");

	private static readonly Dictionary<string, Texture2D> Cache = new();
	private static Theme _theme;

	public static Texture2D Tex(string name) => GD.Load<Texture2D>($"res://sprites/generated/{name}.png");

	/// <summary>Nearest-neighbour upscaled copy, for nine-patch styleboxes whose margins must scale too.</summary>
	public static Texture2D Scaled(string name, int scale = Scale)
	{
		string key = $"{name}@{scale}";
		if (Cache.TryGetValue(key, out var t))
			return t;
		var img = Tex(name).GetImage();
		if (img.IsCompressed())
			img.Decompress();
		img.Resize(img.GetWidth() * scale, img.GetHeight() * scale, Image.Interpolation.Nearest);
		return Cache[key] = ImageTexture.CreateFromImage(img);
	}

	public static Theme Theme => _theme ??= BuildTheme();

	private static StyleBoxTexture Box(string name, int margin, int content)
	{
		var sb = new StyleBoxTexture { Texture = Scaled(name) };
		sb.SetTextureMarginAll(margin * Scale);
		sb.SetContentMarginAll(content);
		return sb;
	}

	private static Theme BuildTheme()
	{
		var t = new Theme();
		t.DefaultFontSize = 26;

		var panel = Box("ui_panel", 6, 20);
		t.SetStylebox("panel", "PanelContainer", panel);
		t.SetStylebox("panel", "Panel", panel);

		foreach (var (state, tex) in new[]
		         {
			         ("normal", "ui_button"), ("hover", "ui_button_hover"), ("pressed", "ui_button_pressed"),
			         ("disabled", "ui_button_disabled"), ("focus", "ui_button_hover"),
		         })
		{
			var sb = Box(tex, 4, 0);
			sb.ContentMarginLeft = sb.ContentMarginRight = 18;
			sb.ContentMarginTop = 6;
			sb.ContentMarginBottom = 14;
			t.SetStylebox(state, "Button", sb);
		}
		foreach (var c in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
			t.SetColor(c, "Button", Ink);
		t.SetColor("font_disabled_color", "Button", new Color("6e6862"));

		t.SetColor("font_color", "Label", Ink);

		var tabOn = Box("ui_button_hover", 4, 0);
		var tabOff = Box("ui_button_disabled", 4, 0);
		foreach (var sb in new[] { tabOn, tabOff })
		{
			sb.ContentMarginLeft = sb.ContentMarginRight = 16;
			sb.ContentMarginTop = 6;
			sb.ContentMarginBottom = 12;
		}
		t.SetStylebox("tab_selected", "TabContainer", tabOn);
		t.SetStylebox("tab_unselected", "TabContainer", tabOff);
		t.SetStylebox("tab_hovered", "TabContainer", tabOn);
		t.SetStylebox("panel", "TabContainer", new StyleBoxEmpty());
		t.SetColor("font_selected_color", "TabContainer", Ink);
		t.SetColor("font_unselected_color", "TabContainer", new Color("4a4250"));
		t.SetColor("font_hovered_color", "TabContainer", Ink);

		var track = new StyleBoxFlat { BgColor = new Color("d9c7a8"), BorderColor = Ink };
		track.SetBorderWidthAll(3);
		track.SetContentMarginAll(4);
		var fill = new StyleBoxFlat { BgColor = new Color("4d9be6"), BorderColor = Ink };
		fill.SetBorderWidthAll(3);
		t.SetStylebox("slider", "HSlider", track);
		t.SetStylebox("grabber_area", "HSlider", fill);
		t.SetStylebox("grabber_area_highlight", "HSlider", fill);
		t.SetStylebox("background", "ProgressBar", track);
		t.SetStylebox("fill", "ProgressBar", fill);
		return t;
	}

	public static Label Label(string text, int size = 26, Color? color = null, bool outline = false)
	{
		var l = new Label { Text = text };
		l.AddThemeFontSizeOverride("font_size", size);
		if (color is Color c)
			l.AddThemeColorOverride("font_color", c);
		if (outline)
		{
			l.AddThemeColorOverride("font_outline_color", Ink);
			l.AddThemeConstantOverride("outline_size", Mathf.Max(6, size / 4));
		}
		return l;
	}

	public static Button Button(string text, System.Action onPressed, int minWidth = 0)
	{
		var b = new Button { Text = text, CustomMinimumSize = new Vector2(minWidth, 0), FocusMode = Control.FocusModeEnum.None };
		b.Pressed += () =>
		{
			Audio.I.Play("click", 0.02f);
			onPressed();
		};
		return b;
	}

	public static TextureRect Icon(Texture2D tex, float size, Material material = null) => new()
	{
		Texture = tex,
		Material = material,
		ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
		StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
		CustomMinimumSize = new Vector2(size, size),
		TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
	};

	/// <summary>Short horizontal wiggle — used when a purchase is refused.</summary>
	public static void Wiggle(Control c)
	{
		var t = c.CreateTween();
		float x = c.Position.X;
		for (int i = 0; i < 3; i++)
		{
			t.TweenProperty(c, "position:x", x + 6, 0.03);
			t.TweenProperty(c, "position:x", x - 6, 0.03);
		}
		t.TweenProperty(c, "position:x", x, 0.03);
	}
}
