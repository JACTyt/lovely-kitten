using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>Autoload: persistent meta progression (stars, records, cosmetics, item unlocks, settings).
/// Per-run state lives in RunState. Emits Changed on any mutation.</summary>
public partial class GameState : Node
{
	public static GameState I { get; private set; }

	[Signal] public delegate void ChangedEventHandler();

	public int Stars;
	public int BestDay;
	public int BestCombo;
	public int TotalCaught;
	public int Rebirths;
	public float MasterVolume = 0.8f;
	public float MusicVolume = 0.6f;
	public float SfxVolume = 0.8f;

	private readonly HashSet<string> _owned = new();
	private readonly HashSet<string> _unlocked = new();
	private readonly Dictionary<string, string> _equipped = new();

	private const int RebirthMinDay = 8;

	/// <summary>Permanent bonuses from rebirths: +25% coins, +10% stars, +10% daily goal each.</summary>
	public float CoinMul => 1f + 0.25f * Rebirths;
	public float StarMul => 1f + 0.10f * Rebirths;
	public float GoalMul => 1f + 0.10f * Rebirths;
	public bool AllItemsUnlocked => Catalog.Items.All(IsUnlocked);
	public bool CanRebirth => AllItemsUnlocked && BestDay >= RebirthMinDay;
	public static string RebirthMinDayText => RebirthMinDay.ToString();

	public override void _Ready()
	{
		I = this;
		ResetToDefaults();
		SaveGame.Load(this);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationWMCloseRequest)
			SaveGame.Save(this);
	}

	public void ResetToDefaults()
	{
		Stars = 0;
		BestDay = 0;
		BestCombo = 0;
		TotalCaught = 0;
		Rebirths = 0;
		_owned.Clear();
		_unlocked.Clear();
		_equipped.Clear();
		foreach (var slot in Catalog.Slots)
		{
			string id = Catalog.DefaultFor(slot);
			_owned.Add(id);
			_equipped[slot] = id;
		}
	}

	public bool Owns(string id) => _owned.Contains(id);
	public bool IsUnlocked(ItemDef item) => item.UnlockStars == 0 || _unlocked.Contains(item.Id);
	public string Equipped(string slot) => _equipped.TryGetValue(slot, out var id) ? id : Catalog.DefaultFor(slot);
	public CosmeticDef EquippedDef(string slot) => Catalog.Cosmetic(Equipped(slot));

	// Raw setters for SaveGame (no signal, no save).
	public void MarkOwned(string id) => _owned.Add(id);
	public void MarkUnlocked(string id) => _unlocked.Add(id);
	public void SetEquipped(string slot, string id) => _equipped[slot] = id;

	public bool TryUnlockItem(string id)
	{
		var item = Catalog.Item(id);
		if (item == null || IsUnlocked(item) || Stars < item.UnlockStars)
			return false;
		Stars -= item.UnlockStars;
		_unlocked.Add(id);
		Commit();
		return true;
	}

	public bool TryBuyCosmetic(string id)
	{
		var def = Catalog.Cosmetic(id);
		if (def == null || Owns(id) || Stars < def.Stars)
			return false;
		Stars -= def.Stars;
		_owned.Add(id);
		_equipped[def.Slot] = id;
		Commit();
		return true;
	}

	public void Equip(string id)
	{
		var def = Catalog.Cosmetic(id);
		if (def == null || !Owns(id))
			return;
		_equipped[def.Slot] = id;
		Commit();
	}

	/// <summary>Resets item unlocks and unspent stars for a permanent multiplier. Cosmetics and records stay.</summary>
	public bool TryRebirth()
	{
		if (!CanRebirth)
			return false;
		Rebirths++;
		Stars = 0;
		_unlocked.Clear();
		Commit();
		return true;
	}

	/// <summary>Stars banked immediately (coin trades, boss rewards).</summary>
	public void AddStars(int n)
	{
		Stars += n;
		Commit();
	}

	public static string Roman(int n)
	{
		string[] r = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
		return n < r.Length ? r[n] : n.ToString();
	}

	/// <summary>Banks a finished run's results. Returns stars earned.</summary>
	public int RecordRun(int daysCompleted, int bestCombo, int caught)
	{
		int stars = Mathf.RoundToInt((3 * daysCompleted + bestCombo / 2) * StarMul);
		Stars += stars;
		BestDay = Mathf.Max(BestDay, daysCompleted);
		BestCombo = Mathf.Max(BestCombo, bestCombo);
		TotalCaught += caught;
		Commit();
		return stars;
	}

	public void SetVolumes(float master, float music, float sfx)
	{
		MasterVolume = master;
		MusicVolume = music;
		SfxVolume = sfx;
		EmitSignal(SignalName.Changed);
	}

	public void Commit()
	{
		SaveGame.Save(this);
		EmitSignal(SignalName.Changed);
	}
}
