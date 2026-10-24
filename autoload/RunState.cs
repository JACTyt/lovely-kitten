using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Autoload: one roguelike run. Day → Dusk → Sleeping → Night (shop, goal met) or Over (goal missed).
/// Holds run coins, item stacks and the stats derived from them. Pauses with the tree.
/// </summary>
public partial class RunState : Node
{
	public static RunState I { get; private set; }

	public enum Phase { Idle, Day, Dusk, Sleeping, Night, Over, Dawn }

	[Signal] public delegate void PhaseChangedEventHandler(int phase);
	[Signal] public delegate void ChangedEventHandler();
	[Signal] public delegate void BossKilledEventHandler(int reward);

	private float _baseDayLength = 120f;
	private const float SleepLength = 4f;
	private const float DawnLength = 3f;
	private const float DuskNight = 0.35f;
	private const int OfferCount = 3;
	private const int BaseRerollCost = 5;
	private const int RerollStep = 3;
	private const float SkillOfferWeight = 1f;
	private const int BossEvery = 5;
	private const int BossBaseHp = 12;
	private const int BossHpStep = 6;
	private const int BossStars = 3;
	private const int BaseTradeCost = 60;
	private const int TradeStep = 30;

	private readonly Dictionary<string, int> _stacks = new();
	private float _sleepT;
	private float _dawnT;
	private bool _livesUsed;

	public Phase Current { get; private set; } = Phase.Idle;
	public int Day { get; private set; }
	public int Coins { get; private set; }
	public int DayCoins { get; private set; }
	public int Caught { get; private set; }
	public int BestCombo { get; private set; }
	public int DaysCompleted { get; private set; }
	public int StarsEarned { get; private set; }
	public int CatnipLeft { get; set; }
	public int RerollCost { get; private set; }
	public float Elapsed { get; private set; }
	public List<string> Offers { get; } = new();
	/// <summary>True on the night after Nine Lives forgave a missed goal.</summary>
	public bool SavedByLives { get; private set; }
	public int TradeCost { get; private set; } = BaseTradeCost;
	public int BossHp { get; private set; }
	public int BossMaxHp { get; private set; }
	public bool BossDefeated { get; private set; }
	public int BossReward { get; private set; }
	public bool IsBossDay => IsBossDayNumber(Day);

	public bool Playing => Current is Phase.Day or Phase.Dusk;
	public float DayLength => _baseDayLength + 10f * Stack("feather_bed");
	private float DuskLength => Mathf.Min(30f, _baseDayLength * 0.25f);
	public float TimeLeft => Mathf.Max(0f, DayLength - Elapsed);
	public float DayProgress => Mathf.Clamp(Elapsed / DayLength, 0f, 1f);
	public int Goal => GoalFor(Day);
	public bool GoalMet => IsBossDay ? BossDefeated : DayCoins >= Goal;

	/// <summary>0 = bright day, ~0.35 at end of dusk, 1 = night.</summary>
	public float NightAmount => Current switch
	{
		Phase.Day => 0f,
		Phase.Dusk => DuskNight * Mathf.SmoothStep(0f, 1f, (Elapsed - (DayLength - DuskLength)) / DuskLength),
		Phase.Sleeping => Mathf.Lerp(DuskNight, 1f, Mathf.SmoothStep(0f, 1f, _sleepT / SleepLength)),
		Phase.Night or Phase.Over => 1f,
		Phase.Dawn => 1f - Mathf.SmoothStep(0f, 1f, _dawnT / DawnLength),
		_ => 0f,
	};

	// Derived stats
	public float JumpHeightMul => 1f + 0.10f * Stack("spring_paws");
	public float MoveSpeedMul => 1f + 0.10f * Stack("swift_paws");
	public float CatchReachBonus => 2f * Stack("whiskers");
	public float SpawnInterval => Mathf.Max(0.7f, 1.6f * Mathf.Pow(0.96f, Day - 1)) * Mathf.Pow(0.9f, Stack("whistle"));
	public float GoldenChance => 0.03f + 0.03f * Stack("golden_bell");
	public float SwiftChance => Mathf.Min(0.5f, 0.25f + 0.03f * (Day - 1));
	public float BirdSpeedMul => 1f + 0.05f * (Day - 1);
	public int LuckyBonus => Stack("lucky_coin");
	public bool HasDoubleJump => Stack("double_jump") > 0;
	public bool HasMagnet => Stack("magnet") > 0;
	public bool HasGlide => Stack("glide") > 0;
	public bool Has(string id) => Stack(id) > 0;
	/// <summary>Each extra stack of a skill cuts its cooldown by 25%.</summary>
	public float CooldownMul(string id) => Mathf.Pow(0.75f, Mathf.Max(0, Stack(id) - 1));

	public static int GoalFor(int day) => Mathf.RoundToInt(24f * Mathf.Pow(1.35f, day - 1) * GameState.I.GoalMul);
	public static bool IsBossDayNumber(int day) => day % BossEvery == 0;

	public override void _Ready()
	{
		I = this;
	}

	public int Stack(string id) => _stacks.TryGetValue(id, out int n) ? n : 0;
	public IEnumerable<KeyValuePair<string, int>> Stacks => _stacks.Where(kv => kv.Value > 0);

	public override void _Process(double delta)
	{
		float d = (float)delta;
		switch (Current)
		{
			case Phase.Day:
			case Phase.Dusk:
				Elapsed += d;
				if (Current == Phase.Day && Elapsed >= DayLength - DuskLength)
					SetPhase(Phase.Dusk);
				if (Elapsed >= DayLength)
				{
					_sleepT = 0f;
					SetPhase(Phase.Sleeping);
				}
				break;
			case Phase.Sleeping:
				_sleepT += d;
				if (_sleepT >= SleepLength)
					EndDay();
				break;
			case Phase.Dawn:
				_dawnT += d;
				if (_dawnT >= DawnLength)
					SetPhase(Phase.Day);
				break;
		}
	}

	public void StartRun()
	{
		Day = 1;
		Coins = 0;
		Caught = 0;
		BestCombo = 0;
		DaysCompleted = 0;
		StarsEarned = 0;
		_livesUsed = false;
		_stacks.Clear();
		Offers.Clear();
		BeginDay();
		ApplyDevArgs();
	}

	public void NextDay()
	{
		if (Current != Phase.Night)
			return;
		Day++;
		ResetDay();
		_dawnT = 0f;
		SetPhase(Phase.Dawn); // sky brightens, cat keeps sleeping; Day starts when it's light
		EmitSignal(SignalName.Changed);
	}

	/// <summary>Ends the run early (pause → main menu). Completed days still earn stars.</summary>
	public void Abandon()
	{
		if (Current is Phase.Idle or Phase.Over)
		{
			Current = Phase.Idle;
			return;
		}
		StarsEarned = GameState.I.RecordRun(DaysCompleted, BestCombo, Caught);
		Current = Phase.Idle;
	}

	public void AddCatch(int coins, int combo)
	{
		Coins += coins;
		DayCoins += coins;
		Caught++;
		BestCombo = Mathf.Max(BestCombo, combo);
		EmitSignal(SignalName.Changed);
	}

	public bool TryBuy(string id)
	{
		var def = Catalog.Item(id);
		if (Current != Phase.Night || def == null || Stack(id) >= def.MaxStack)
			return false;
		int cost = Catalog.ItemCost(def, Day);
		if (Coins < cost)
			return false;
		Coins -= cost;
		_stacks[id] = Stack(id) + 1;
		Offers.Remove(id);
		EmitSignal(SignalName.Changed);
		return true;
	}

	/// <summary>Night shop: coins → 1 star, banked immediately. Price rises with each trade that night.</summary>
	public bool TryTrade()
	{
		if (Current != Phase.Night || Coins < TradeCost)
			return false;
		Coins -= TradeCost;
		TradeCost += TradeStep;
		GameState.I.AddStars(1);
		EmitSignal(SignalName.Changed);
		return true;
	}

	/// <summary>Returns true if the hit landed; the killing blow pays coins + stars.</summary>
	public bool DamageBoss(int amount)
	{
		if (!IsBossDay || BossDefeated || !Playing)
			return false;
		BossHp = Mathf.Max(0, BossHp - amount);
		if (BossHp == 0)
		{
			BossDefeated = true;
			BossReward = Mathf.RoundToInt(15 * Day * GameState.I.CoinMul);
			Coins += BossReward;
			DayCoins += BossReward;
			GameState.I.AddStars(BossStars);
			EmitSignal(SignalName.BossKilled, BossReward);
		}
		EmitSignal(SignalName.Changed);
		return true;
	}

	public int LoseCoins(int amount)
	{
		int lost = Mathf.Min(Coins, amount);
		Coins -= lost;
		EmitSignal(SignalName.Changed);
		return lost;
	}

	public bool TryReroll()
	{
		if (Current != Phase.Night || Coins < RerollCost)
			return false;
		Coins -= RerollCost;
		RerollCost += RerollStep;
		RollOffers();
		EmitSignal(SignalName.Changed);
		return true;
	}

	private void BeginDay()
	{
		ResetDay();
		SetPhase(Phase.Day);
		EmitSignal(SignalName.Changed);
	}

	private void ResetDay()
	{
		DayCoins = 0;
		Elapsed = 0f;
		CatnipLeft = Stack("catnip");
		BossDefeated = false;
		BossReward = 0;
		BossMaxHp = IsBossDay ? BossBaseHp + BossHpStep * (Day / BossEvery - 1) : 0;
		BossHp = BossMaxHp;
	}

	private void EndDay()
	{
		SavedByLives = !GoalMet && Has("nine_lives") && !_livesUsed;
		if (SavedByLives)
			_livesUsed = true;
		if (GoalMet || SavedByLives)
		{
			DaysCompleted++;
			RerollCost = BaseRerollCost;
			TradeCost = BaseTradeCost;
			RollOffers();
			SetPhase(Phase.Night);
		}
		else
		{
			StarsEarned = GameState.I.RecordRun(DaysCompleted, BestCombo, Caught);
			SetPhase(Phase.Over);
		}
		EmitSignal(SignalName.Changed);
	}

	/// <summary>Dev aid: `godot -- --dev-day=8 --dev-coins=100 --dev-items=dash,meow --dev-start=5` shortens days,
	/// grants starting coins / items, starts on a later day.</summary>
	private void ApplyDevArgs()
	{
		foreach (var arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--dev-day=") && float.TryParse(arg[10..], out float len))
				_baseDayLength = Mathf.Max(3f, len);
			else if (arg.StartsWith("--dev-start=") && int.TryParse(arg[12..], out int startDay))
			{
				Day = Mathf.Max(1, startDay);
				ResetDay();
				EmitSignal(SignalName.PhaseChanged, (int)Current); // re-run day setup (boss arrival etc.)
			}
			else if (arg.StartsWith("--dev-items="))
			{
				foreach (var id in arg[12..].Split(','))
					if (Catalog.Item(id) is ItemDef item)
						_stacks[id] = Mathf.Min(item.MaxStack, Stack(id) + 1);
			}
			else if (arg.StartsWith("--dev-coins=") && int.TryParse(arg[12..], out int coins))
			{
				Coins += coins;
				DayCoins += coins;
			}
		}
	}

	private void RollOffers()
	{
		Offers.Clear();
		// Weighted sampling without replacement (Efraimidis–Spirakis): skills are rarer than passive items.
		var pool = Catalog.Items.Where(i => GameState.I.IsUnlocked(i) && Stack(i.Id) < i.MaxStack)
			.Where(i => i.Id != "nine_lives" || !_livesUsed)
			.OrderByDescending(i => Mathf.Pow(GD.Randf(), 1f / (i.IsSkill ? SkillOfferWeight : 1f)))
			.Take(OfferCount);
		Offers.AddRange(pool.Select(i => i.Id));
	}

	private void SetPhase(Phase p)
	{
		if (p == Current)
			return;
		Current = p;
		EmitSignal(SignalName.PhaseChanged, (int)p);
	}
}
