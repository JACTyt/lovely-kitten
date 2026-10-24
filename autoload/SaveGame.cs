using Godot;
using System;

/// <summary>Reads/writes meta progression to user://save.cfg. A missing or corrupt file leaves defaults in place.</summary>
public static class SaveGame
{
	private const string Path = "user://save.cfg";

	public static void Load(GameState s)
	{
		var cfg = new ConfigFile();
		if (cfg.Load(Path) != Error.Ok)
			return;

		try
		{
			s.Stars = Math.Max(0, cfg.GetValue("meta", "stars", 0).AsInt32());
			s.BestDay = Math.Max(0, cfg.GetValue("meta", "best_day", 0).AsInt32());
			s.BestCombo = Math.Max(0, cfg.GetValue("meta", "best_combo", 0).AsInt32());
			s.TotalCaught = Math.Max(0, cfg.GetValue("meta", "total_caught", 0).AsInt32());
			s.Rebirths = Math.Max(0, cfg.GetValue("meta", "rebirths", 0).AsInt32());

			foreach (var c in Catalog.Cosmetics)
				if (cfg.GetValue("owned", c.Id, false).AsBool())
					s.MarkOwned(c.Id);
			foreach (var i in Catalog.Items)
				if (cfg.GetValue("unlocked", i.Id, false).AsBool())
					s.MarkUnlocked(i.Id);

			foreach (var slot in Catalog.Slots)
			{
				string id = cfg.GetValue("equipped", slot, "").AsString();
				var def = Catalog.Cosmetic(id);
				if (def != null && def.Slot == slot && s.Owns(id))
					s.SetEquipped(slot, id);
			}

			s.MasterVolume = Mathf.Clamp((float)cfg.GetValue("audio", "master", 0.8f).AsDouble(), 0f, 1f);
			s.MusicVolume = Mathf.Clamp((float)cfg.GetValue("audio", "music", 0.6f).AsDouble(), 0f, 1f);
			s.SfxVolume = Mathf.Clamp((float)cfg.GetValue("audio", "sfx", 0.8f).AsDouble(), 0f, 1f);
		}
		catch (Exception e)
		{
			GD.PushWarning($"Save file unreadable, using defaults: {e.Message}");
			s.ResetToDefaults();
		}
	}

	public static void Save(GameState s)
	{
		var cfg = new ConfigFile();
		cfg.SetValue("meta", "stars", s.Stars);
		cfg.SetValue("meta", "best_day", s.BestDay);
		cfg.SetValue("meta", "best_combo", s.BestCombo);
		cfg.SetValue("meta", "total_caught", s.TotalCaught);
		cfg.SetValue("meta", "rebirths", s.Rebirths);

		foreach (var c in Catalog.Cosmetics)
			if (s.Owns(c.Id))
				cfg.SetValue("owned", c.Id, true);
		foreach (var i in Catalog.Items)
			if (i.UnlockStars > 0 && s.IsUnlocked(i))
				cfg.SetValue("unlocked", i.Id, true);
		foreach (var slot in Catalog.Slots)
			cfg.SetValue("equipped", slot, s.Equipped(slot));

		cfg.SetValue("audio", "master", s.MasterVolume);
		cfg.SetValue("audio", "music", s.MusicVolume);
		cfg.SetValue("audio", "sfx", s.SfxVolume);

		var err = cfg.Save(Path);
		if (err != Error.Ok)
			GD.PushWarning($"Could not save game: {err}");
	}
}
