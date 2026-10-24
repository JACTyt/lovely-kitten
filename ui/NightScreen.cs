using Godot;

/// <summary>Shown while the cat sleeps after a successful day: results, 3 item offers, re-roll, inventory, wake up.</summary>
public partial class NightScreen : CanvasLayer
{
	private Control _root;
	private VBoxContainer _body;

	public override void _Ready()
	{
		Layer = 20;
		_root = new Control { Theme = UiKit.Theme, Visible = false };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(600, 0) };
		_root.AddChild(panel);
		panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.RightWide, Control.LayoutPresetMode.Minsize, 12);
		panel.GrowHorizontal = Control.GrowDirection.Begin;

		_body = new VBoxContainer();
		_body.AddThemeConstantOverride("separation", 6);
		panel.AddChild(_body);

		RunState.I.PhaseChanged += OnPhaseChanged;
		RunState.I.Changed += Rebuild;
	}

	public override void _ExitTree()
	{
		RunState.I.PhaseChanged -= OnPhaseChanged;
		RunState.I.Changed -= Rebuild;
	}

	private void OnPhaseChanged(int phase)
	{
		_root.Visible = (RunState.Phase)phase == RunState.Phase.Night;
		Rebuild();
	}

	private void Rebuild()
	{
		if (!_root.Visible)
			return;
		var run = RunState.I;
		foreach (var c in _body.GetChildren())
		{
			_body.RemoveChild(c);
			c.QueueFree();
		}

		_body.AddChild(UiKit.Label($"Night {run.Day}   Zzz…", 32));
		if (run.SavedByLives)
			_body.AddChild(UiKit.Label("Nine Lives saved you!", 22, new Color("d9475b")));
		else if (run.IsBossDay)
			_body.AddChild(UiKit.Label($"Crow King defeated! +{run.BossReward} coins, +3 stars", 22, new Color("3f7a3c")));
		else
			_body.AddChild(UiKit.Label($"Day {run.Day} done: {run.DayCoins}/{run.Goal} coins", 22, new Color("3f7a3c")));
		_body.AddChild(RunState.IsBossDayNumber(run.Day + 1)
			? UiKit.Label("Tomorrow: BOSS DAY — the Crow King!", 22, new Color("d9475b"))
			: UiKit.Label($"Tomorrow's goal: {RunState.GoalFor(run.Day + 1)} coins", 22, new Color("5e5866")));

		var wallet = new HBoxContainer();
		wallet.AddChild(UiKit.Label("Dream Market", 26));
		wallet.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
		wallet.AddChild(UiKit.Icon(UiKit.Tex("ui_coin"), 30));
		wallet.AddChild(UiKit.Label(run.Coins.ToString(), 30));
		_body.AddChild(wallet);

		if (run.Offers.Count == 0)
			_body.AddChild(UiKit.Label("Sold out! Re-roll for more.", 20, new Color("5e5866")));
		foreach (var id in run.Offers)
			_body.AddChild(OfferRow(Catalog.Item(id)));

		var actions = new HBoxContainer();
		actions.AddThemeConstantOverride("separation", 8);
		Button reroll = null;
		reroll = UiKit.Button($"Re-roll  {run.RerollCost} ¢", () =>
		{
			if (!RunState.I.TryReroll())
				Deny(reroll);
		});
		reroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		actions.AddChild(reroll);
		Button trade = null;
		trade = UiKit.Button($"Trade {run.TradeCost} ¢ » 1 star", () =>
		{
			if (RunState.I.TryTrade())
				Audio.I.Play("buy", 0f);
			else
				Deny(trade);
		});
		trade.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		trade.TooltipText = $"Stars are banked right away. You have {GameState.I.Stars}.";
		actions.AddChild(trade);
		_body.AddChild(actions);

		_body.AddChild(UiKit.Label("Your items", 24));
		var inv = new HFlowContainer();
		inv.AddThemeConstantOverride("h_separation", 12);
		bool any = false;
		foreach (var (id, n) in run.Stacks)
		{
			var item = Catalog.Item(id);
			var cell = new HBoxContainer { TooltipText = $"{item.Name}: {item.Desc}" };
			cell.AddChild(UiKit.Icon(UiKit.Tex($"ui_icon_{item.Icon}"), 32));
			cell.AddChild(UiKit.Label($"x{n}", 20));
			inv.AddChild(cell);
			any = true;
		}
		if (!any)
			inv.AddChild(UiKit.Label("none yet", 18, new Color("5e5866")));
		_body.AddChild(inv);

		_body.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
		_body.AddChild(UiKit.Button($"Wake up  »  Day {run.Day + 1}", () => RunState.I.NextDay()));
	}

	private Control OfferRow(ItemDef item)
	{
		var run = RunState.I;
		int cost = Catalog.ItemCost(item, run.Day);
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);
		var frame = new PanelContainer();
		frame.AddChild(UiKit.Icon(UiKit.Tex($"ui_icon_{item.Icon}"), 40));
		row.AddChild(frame);

		var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
		string stack = item.MaxStack > 1 ? $"  ({run.Stack(item.Id)}/{item.MaxStack})" : "";
		string desc = item.IsSkill
			? (run.Has(item.Id) ? "Upgrade: cooldown −25%" : $"SKILL [{item.Key}] · {item.Desc}")
			: item.Desc;
		text.AddChild(UiKit.Label(item.Name + stack, 22, item.IsSkill ? new Color("356da3") : null));
		text.AddChild(UiKit.Label(desc, 16, new Color("5e5866")));
		row.AddChild(text);

		Button buy = null;
		buy = UiKit.Button($"{cost} ¢", () =>
		{
			if (RunState.I.TryBuy(item.Id))
				Audio.I.Play("buy", 0f);
			else
				Deny(buy);
		}, 110);
		buy.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		row.AddChild(buy);
		return row;
	}

	private static void Deny(Control c)
	{
		Audio.I.Play("deny", 0f);
		UiKit.Wiggle(c);
	}
}
