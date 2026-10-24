using Godot;
using System.Collections.Generic;

/// <summary>Autoload: pooled SFX players and cross-faded day/night music. Runs while the tree is paused.</summary>
public partial class Audio : Node
{
	public static Audio I { get; private set; }

	private const int PoolSize = 10;
	private const float FadeTime = 2.5f;
	private const float SilentDb = -40f;
	private static readonly string[] SfxNames =
	{
		"jump", "land", "catch", "coin", "combo", "buy", "deny", "click", "hoot",
		"meow", "swipe", "dash", "dive", "boom", "throw", "timeslow", "ready", "event",
	};

	private readonly Dictionary<string, AudioStream> _sfx = new();
	private readonly List<AudioStreamPlayer> _pool = new();
	private int _next;
	private AudioStreamPlayer _dayMusic;
	private AudioStreamPlayer _nightMusic;
	private bool? _night;
	private Tween _fade;

	public override void _Ready()
	{
		I = this;
		ProcessMode = ProcessModeEnum.Always;

		EnsureBus("Music");
		EnsureBus("SFX");

		foreach (var n in SfxNames)
			_sfx[n] = GD.Load<AudioStream>($"res://audio/generated/{n}.wav");

		for (int i = 0; i < PoolSize; i++)
		{
			var p = new AudioStreamPlayer { Bus = "SFX" };
			AddChild(p);
			_pool.Add(p);
		}

		_dayMusic = MakeMusic("res://audio/generated/music_day.wav");
		_nightMusic = MakeMusic("res://audio/generated/music_night.wav");

		ApplyVolumes();
		GameState.I.Changed += ApplyVolumes;
	}

	public override void _ExitTree()
	{
		if (GameState.I != null)
			GameState.I.Changed -= ApplyVolumes;
	}

	public void Play(string name, float pitchJitter = 0.05f)
	{
		if (!_sfx.TryGetValue(name, out var stream) || stream == null)
			return;
		var p = _pool[_next];
		_next = (_next + 1) % _pool.Count;
		p.Stream = stream;
		p.PitchScale = 1f + (float)GD.RandRange(-pitchJitter, pitchJitter);
		p.Play();
	}

	public void SetMusic(bool night)
	{
		if (_night == night)
			return;
		_night = night;

		var fadeIn = night ? _nightMusic : _dayMusic;
		var fadeOut = night ? _dayMusic : _nightMusic;
		if (!fadeIn.Playing)
		{
			fadeIn.VolumeDb = SilentDb;
			fadeIn.Play();
		}

		_fade?.Kill();
		_fade = CreateTween().SetParallel();
		_fade.TweenProperty(fadeIn, "volume_db", 0f, FadeTime);
		_fade.TweenProperty(fadeOut, "volume_db", SilentDb, FadeTime);
		_fade.Chain().TweenCallback(Callable.From(fadeOut.Stop));
	}

	private void ApplyVolumes()
	{
		var s = GameState.I;
		SetBus("Master", s.MasterVolume);
		SetBus("Music", s.MusicVolume);
		SetBus("SFX", s.SfxVolume);
	}

	private static void SetBus(string name, float linear)
	{
		int idx = AudioServer.GetBusIndex(name);
		if (idx < 0)
			return;
		AudioServer.SetBusMute(idx, linear <= 0.001f);
		AudioServer.SetBusVolumeDb(idx, Mathf.LinearToDb(Mathf.Max(linear, 0.001f)));
	}

	private static void EnsureBus(string name)
	{
		if (AudioServer.GetBusIndex(name) >= 0)
			return;
		AudioServer.AddBus();
		int idx = AudioServer.BusCount - 1;
		AudioServer.SetBusName(idx, name);
		AudioServer.SetBusSend(idx, "Master");
	}

	private AudioStreamPlayer MakeMusic(string path)
	{
		var stream = GD.Load<AudioStream>(path);
		if (stream is AudioStreamWav wav)
		{
			wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
			wav.LoopBegin = 0;
			wav.LoopEnd = (int)(wav.GetLength() * wav.MixRate);
		}
		var p = new AudioStreamPlayer { Stream = stream, Bus = "Music", VolumeDb = SilentDb };
		// Fallback for import modes that ignore loop points.
		p.Finished += () => p.Play();
		AddChild(p);
		return p;
	}
}
