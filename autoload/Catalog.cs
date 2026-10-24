using Godot;
using System.Linq;

/// <summary>Run-scoped shop item. Effects are read through RunState's derived stats. Skills are active abilities
/// (see Skills.cs): the first stack grants the skill, further stacks cut its cooldown by 25%.</summary>
public record ItemDef(string Id, string Name, string Icon, string Desc, int BaseCost, int MaxStack, int UnlockStars,
	string Key = null)
{
	public bool IsSkill => Key != null;
}

/// <summary>Persistent cosmetic bought with stars. Slot is one of Catalog.Slots. Palette = cat skin colours
/// (light, mid, outline), Hsv = bird skin (hue shift, saturation mul, value mul), Cell = clothes sheet column.</summary>
public record CosmeticDef(string Id, string Name, string Slot, int Stars,
	Color[] Palette = null, Vector3? Hsv = null, int Cell = -1, string World = null);

public static class Catalog
{
	public const string SlotCatSkin = "cat_skin";
	public const string SlotHat = "hat";
	public const string SlotNeck = "neck";
	public const string SlotGlasses = "glasses";
	public const string SlotBirdSkin = "bird_skin";
	public const string SlotWorld = "world";
	public static readonly string[] Slots = { SlotCatSkin, SlotHat, SlotNeck, SlotGlasses, SlotBirdSkin, SlotWorld };

	public static readonly ItemDef[] Items =
	{
		new("spring_paws", "Spring Paws", "jump", "+10% jump height", 12, 5, 0),
		new("swift_paws", "Swift Paws", "speed", "+10% move speed", 12, 5, 0),
		new("whiskers", "Long Whiskers", "radius", "+2 px catch reach", 15, 5, 0),
		new("whistle", "Bird Whistle", "spawn", "Birds appear 10% more often", 15, 5, 0),
		new("feather_bed", "Feather Bed", "bed", "Days last 10 s longer", 18, 3, 0),
		new("double_jump", "Double Jump", "double", "Jump again in mid-air. Catches refresh it!", 40, 1, 0),
		new("golden_bell", "Golden Bell", "golden", "+3% golden bird chance", 20, 3, 6),
		new("catnip", "Catnip", "catnip", "Combo survives one empty landing per day", 20, 3, 5),
		new("lucky_coin", "Lucky Coin", "lucky", "+1 coin per catch", 25, 3, 10),
		new("magnet", "Bird Magnet", "magnet", "Nearby birds drift toward you", 45, 1, 8),
		new("glide", "Glide", "glide", "Hold jump while falling to drift down", 20, 1, 4),
		new("nine_lives", "Nine Lives", "lives", "Once per run, a missed goal is forgiven", 60, 1, 10),

		new("dash", "Pounce Dash", "dash", "Leap sideways, even in mid-air", 30, 3, 0, "Shift"),
		new("swipe", "Tail Swipe", "swipe", "Double catch reach for a moment", 25, 3, 0, "E"),
		new("dive", "Dive Bomb", "dive", "Slam down; the shockwave pulls birds low", 30, 3, 0, "S"),
		new("meow", "Meow", "meow", "Nearby birds slow down and swoop low", 35, 3, 0, "Q"),
		new("yarn", "Yarn Ball", "yarn", "Throw yarn that snags the first bird it hits", 40, 3, 0, "F"),
		new("cattime", "Cat Time", "cattime", "Slow the world to 40% for 3 s", 50, 3, 0, "R"),
	};

	public static readonly CosmeticDef[] Cosmetics =
	{
		new("cat_classic", "Classic", SlotCatSkin, 0, Palette: new[] { C("e0e0e0"), C("b5b5b5"), C("2f2f2e") }),
		new("cat_ginger", "Ginger", SlotCatSkin, 3, Palette: new[] { C("f2a65a"), C("d9773a"), C("4a2a1a") }),
		new("cat_midnight", "Midnight", SlotCatSkin, 5, Palette: new[] { C("4a4a5e"), C("35354a"), C("101018") }),
		new("cat_siamese", "Siamese", SlotCatSkin, 5, Palette: new[] { C("f4ead8"), C("7a5a48"), C("2f2420") }),
		new("cat_ghost", "Ghost", SlotCatSkin, 8, Palette: new[] { C("e6f8ff"), C("a8d8f0"), C("5a8fb0") }),

		new("hat_none", "No Hat", SlotHat, 0),
		new("hat_beanie", "Beanie", SlotHat, 2, Cell: 0),
		new("hat_tophat", "Top Hat", SlotHat, 4, Cell: 1),
		new("hat_crown", "Crown", SlotHat, 6, Cell: 2),

		new("neck_none", "Bare Neck", SlotNeck, 0),
		new("neck_bow", "Bow Tie", SlotNeck, 2, Cell: 0),
		new("neck_scarf", "Scarf", SlotNeck, 3, Cell: 1),
		new("neck_lantern", "Lantern Collar", SlotNeck, 5, Cell: 2),

		new("glasses_none", "No Glasses", SlotGlasses, 0),
		new("glasses_round", "Round Glasses", SlotGlasses, 2, Cell: 0),
		new("glasses_sun", "Sunglasses", SlotGlasses, 4, Cell: 1),

		new("bird_classic", "Classic", SlotBirdSkin, 0, Hsv: new Vector3(0f, 1f, 1f)),
		new("bird_tropical", "Tropical", SlotBirdSkin, 3, Hsv: new Vector3(0.35f, 1.4f, 1.05f)),
		new("bird_candy", "Candy", SlotBirdSkin, 4, Hsv: new Vector3(0.82f, 1.2f, 1.15f)),
		new("bird_shadow", "Shadow", SlotBirdSkin, 5, Hsv: new Vector3(0f, 0.15f, 0.55f)),

		new("world_street", "Street", SlotWorld, 0, World: "street"),
		new("world_rooftops", "Rooftops", SlotWorld, 6, World: "rooftops"),
		new("world_park", "Park", SlotWorld, 6, World: "park"),
	};

	public static ItemDef Item(string id) => Items.FirstOrDefault(i => i.Id == id);
	public static CosmeticDef Cosmetic(string id) => Cosmetics.FirstOrDefault(c => c.Id == id);
	public static string DefaultFor(string slot) => Cosmetics.First(c => c.Slot == slot && c.Stars == 0).Id;

	public static int ItemCost(ItemDef d, int day) => Mathf.RoundToInt(d.BaseCost * (1f + 0.15f * (day - 1)));

	private static Color C(string hex) => Color.FromHtml(hex);
}
