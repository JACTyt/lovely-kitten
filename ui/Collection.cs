using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>Main-menu star shop: unlock items for the run pool, buy and equip cosmetics.</summary>
public partial class Collection : PanelContainer
{
	private static readonly Texture2D CatSheet = GD.Load<Texture2D>("res://sprites/Cat Sprite Sheet.png");
	private static readonly Texture2D BirdSheet = GD.Load<Texture2D>("res://sprites/Bird Spritesheet.png");
	private static readonly Shader CatPalette = GD.Load<Shader>("res://shaders/cat_palette.gdshader");
	private static readonly Shader HsvShader = GD.Load<Shader>("res://shaders/hsv_tint.gdshader");

	public System.Action OnClose;

	private Label _stars;
	private TabContainer _tabs;

	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(960, 600);
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 12);
		AddChild(col);

		var header = new HBoxContainer();
		header.AddChild(UiKit.Label("Collection", 40));
		header.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
		header.AddChild(UiKit.Icon(UiKit.Tex("ui_star"), 32));
		_stars = UiKit.Label("0", 32);
		header.AddChild(_stars);
		header.AddChild(new Control { CustomMinimumSize = new Vector2(16, 0) });
		header.AddChild(UiKit.Button("Back", () => OnClose?.Invoke()));
		col.AddChild(header);

		_tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		_tabs.AddThemeFontSizeOverride("font_size", 24);
		col.AddChild(_tabs);

		GameState.I.Changed += Rebuild;
		Rebuild();
	}

	public override void _ExitTree()
	{
		GameState.I.Changed -= Rebuild;
	}

	private void Rebuild()
	{
		_stars.Text = GameState.I.Stars.ToString();
		int current = _tabs.CurrentTab;
		foreach (var c in _tabs.GetChildren())
		{
			_tabs.RemoveChild(c);
			c.QueueFree();
		}

		AddTab("Items", Catalog.Items.Select(ItemRow));
		AddTab("Cat", Catalog.Cosmetics
			.Where(c => c.Slot is Catalog.SlotCatSkin or Catalog.SlotHat or Catalog.SlotNeck or Catalog.SlotGlasses)
			.Select(CosmeticRow));
		AddTab("Birds", Catalog.Cosmetics.Where(c => c.Slot == Catalog.SlotBirdSkin).Select(CosmeticRow));
		AddTab("World", Catalog.Cosmetics.Where(c => c.Slot == Catalog.SlotWorld).Select(CosmeticRow));
		AddTab("Rebirth", RebirthRows());

		if (current >= 0 && current < _tabs.GetTabCount())
			_tabs.CurrentTab = current;
	}

	private void AddTab(string title, IEnumerable<Control> rows)
	{
		var scroll = new ScrollContainer { Name = title, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		list.AddThemeConstantOverride("separation", 8);
		foreach (var r in rows)
			list.AddChild(r);
		scroll.AddChild(list);
		_tabs.AddChild(scroll);
	}

	private IEnumerable<Control> RebirthRows()
	{
		var gs = GameState.I;
		var dim = new Color("5e5866");
		var head = new HBoxContainer();
		head.AddThemeConstantOverride("separation", 12);
		head.AddChild(UiKit.Icon(UiKit.Tex("ui_icon_rebirth"), 48));
		head.AddChild(UiKit.Label(gs.Rebirths > 0 ? $"Rebirth {GameState.Roman(gs.Rebirths)}" : "No rebirths yet", 30));
		yield return head;

		yield return UiKit.Label($"Now: coins ×{gs.CoinMul:0.00} · stars ×{gs.StarMul:0.00} · goals ×{gs.GoalMul:0.00}", 22);
		yield return UiKit.Label("Each rebirth: +25% coins per catch, +10% stars per run, daily goals +10%.", 18, dim);
		yield return UiKit.Label("Resets item unlocks and unspent stars. Cosmetics and records are kept.", 18, dim);

		int unlocked = Catalog.Items.Count(gs.IsUnlocked);
		yield return UiKit.Label($"{(gs.AllItemsUnlocked ? "[done]" : "[    ]")}  Unlock every item ({unlocked}/{Catalog.Items.Length})", 22);
		yield return UiKit.Label($"{(gs.BestDay >= 8 ? "[done]" : "[    ]")}  Survive {GameState.RebirthMinDayText} days in one run (best: {gs.BestDay})", 22);

		Button btn = null;
		bool armed = false;
		btn = UiKit.Button(gs.CanRebirth ? "Rebirth" : "Not ready yet", () =>
		{
			if (!armed)
			{
				armed = true;
				btn.Text = "Click again to confirm";
				return;
			}
			if (gs.TryRebirth())
				Audio.I.Play("event", 0f);
			else
				Deny(btn);
		}, 300);
		btn.Disabled = !gs.CanRebirth;
		yield return btn;
	}

	private Control ItemRow(ItemDef item)
	{
		bool unlocked = GameState.I.IsUnlocked(item);
		Button btn = null;
		btn = UiKit.Button(unlocked ? "In pool" : $"Unlock: {item.UnlockStars} stars", () =>
		{
			if (GameState.I.TryUnlockItem(item.Id))
				Audio.I.Play("buy", 0f);
			else
				Deny(btn);
		}, 190);
		btn.Disabled = unlocked;
		string stack = item.MaxStack > 1 ? $" · stacks to {item.MaxStack}" : "";
		return Row(UiKit.Icon(UiKit.Tex($"ui_icon_{item.Icon}"), 48), item.Name, item.Desc + stack, btn);
	}

	private Control CosmeticRow(CosmeticDef c)
	{
		var gs = GameState.I;
		bool owned = gs.Owns(c.Id);
		bool equipped = gs.Equipped(c.Slot) == c.Id;

		Button btn = null;
		btn = UiKit.Button(equipped ? "Equipped" : owned ? "Equip" : $"{c.Stars} stars", () =>
		{
			if (owned)
				gs.Equip(c.Id);
			else if (gs.TryBuyCosmetic(c.Id))
				Audio.I.Play("buy", 0f);
			else
				Deny(btn);
		}, 190);
		btn.Disabled = equipped;

		string slotName = c.Slot switch
		{
			Catalog.SlotCatSkin => "Fur",
			Catalog.SlotHat => "Hat",
			Catalog.SlotNeck => "Neck",
			Catalog.SlotGlasses => "Glasses",
			Catalog.SlotBirdSkin => "Bird feathers",
			_ => "Background · day & night",
		};
		return Row(Preview(c), c.Name, slotName, btn);
	}

	private static void Deny(Control c)
	{
		Audio.I.Play("deny", 0f);
		UiKit.Wiggle(c);
	}

	private static Control Row(Control icon, string name, string desc, Button action)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 16);
		var frame = new PanelContainer();
		frame.AddChild(icon);
		row.AddChild(frame);

		var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
		text.AddChild(UiKit.Label(name, 26));
		text.AddChild(UiKit.Label(desc, 18, new Color("5e5866")));
		row.AddChild(text);

		action.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		row.AddChild(action);
		return row;
	}

	private static Control Preview(CosmeticDef c)
	{
		switch (c.Slot)
		{
			case Catalog.SlotCatSkin:
			{
				var m = new ShaderMaterial { Shader = CatPalette };
				m.SetShaderParameter("light_col", c.Palette[0]);
				m.SetShaderParameter("mid_col", c.Palette[1]);
				m.SetShaderParameter("dark_col", c.Palette[2]);
				return UiKit.Icon(new AtlasTexture { Atlas = CatSheet, Region = new Rect2(8, 18, 16, 14) }, 48, m);
			}
			case Catalog.SlotHat:
			case Catalog.SlotNeck:
			case Catalog.SlotGlasses:
			{
				if (c.Cell < 0)
					return UiKit.Icon(UiKit.Tex("ui_icon_shirt"), 48);
				int row = c.Slot == Catalog.SlotHat ? 0 : c.Slot == Catalog.SlotNeck ? 1 : 2;
				return UiKit.Icon(new AtlasTexture
				{
					Atlas = UiKit.Tex("clothes"), Region = new Rect2(c.Cell * 16 + 3, row * 16 + 3, 10, 10),
				}, 48);
			}
			case Catalog.SlotBirdSkin:
			{
				var hsv = c.Hsv ?? new Vector3(0, 1, 1);
				var m = new ShaderMaterial { Shader = HsvShader };
				m.SetShaderParameter("hue_shift", hsv.X);
				m.SetShaderParameter("sat_mul", hsv.Y);
				m.SetShaderParameter("val_mul", hsv.Z);
				return UiKit.Icon(new AtlasTexture { Atlas = BirdSheet, Region = new Rect2(16, 16, 16, 16) }, 48, m);
			}
			default:
			{
				var icon = UiKit.Icon(BackgroundController.Texture(c.World, false), 48);
				icon.CustomMinimumSize = new Vector2(85, 48);
				return icon;
			}
		}
	}
}
