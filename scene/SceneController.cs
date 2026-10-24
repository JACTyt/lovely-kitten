using Godot;
using System.Collections.Generic;

/// <summary>
/// Game scene root: starts the run, spawns creatures per phase (day birds, dusk owls, night fireflies),
/// runs mid-day bird events, detects catches by distance (cat and yarn balls) and tracks combos.
/// </summary>
public partial class SceneController : Node2D
{
	public static SceneController Singleton { get; private set; }

	[Signal] public delegate void CaughtEventHandler(int coins, int combo, Vector2 globalAt, bool style);
	[Signal] public delegate void ComboChangedEventHandler(int combo);
	[Signal] public delegate void CatnipUsedEventHandler(Vector2 globalAt);
	[Signal] public delegate void BirdEventEventHandler(string title, string subtitle);
	[Signal] public delegate void PopupEventHandler(string text, Vector2 globalAt, Color color);

	[Export] private CameraController _cameraController;
	[Export] private Vector2 _playAxis;
	[Export] private Node2D _birdLayer;

	private static readonly PackedScene PlayerCat = GD.Load<PackedScene>("res://entity/cat/cat.tscn");
	private static readonly PackedScene BirdScene = GD.Load<PackedScene>("res://entity/bird/bird.tscn");
	private static readonly Texture2D YarnTexture = GD.Load<Texture2D>("res://sprites/generated/yarn.png");
	private static readonly Texture2D DarkFeather = GD.Load<Texture2D>("res://sprites/generated/dark_feather.png");

	private const int MaxBirds = 8;
	private const int MaxBirdsDuringFlock = 14;
	private const int MaxFireflies = 6;
	private const float FireflyInterval = 1.2f;
	private const float SpawnX = 130f;
	private const float DuskOwlChance = 0.4f;
	private const float MagnetRange = 40f;
	private const float MagnetSpeed = 30f;
	private const float ComboStep = 0.5f;
	private const float ComboCap = 4f;
	private const float ShockwaveRange = 50f;
	private const float ShockwaveLure = 2f;
	private const float YarnSpeedX = 120f;
	private const float YarnSpeedY = -150f;
	private const float YarnGravity = 380f;
	private const float YarnReach = 7f;
	private const int FlockSize = 6;
	private const float FlockGap = 0.25f;
	private const float EventLength = 10f;
	private const float WindStrength = 18f;
	private const float GoldenHourMul = 3f;
	private const float BossArrivalDelay = 2f;
	private const float StunTime = 1f;
	private const int StunCoinLoss = 5;
	private const float FeatherHitRadius = 6f;

	private readonly List<Bird> _birds = new();
	private readonly List<(Sprite2D sprite, Vector2 vel)> _yarns = new();
	private readonly List<(Sprite2D sprite, Vector2 vel)> _bossFeathers = new();
	private CrowKing _boss;
	private float _bossSpawnT = -1f;
	private Cat _cat;
	private float _spawnTimer;
	private int _combo;

	private float _eventTimer;
	private int _flockLeft;
	private float _flockTimer;
	private int _flockDir;
	private float _flockY;
	private float _goldenHour;
	private float _windT;
	private int _windDir;

	public Cat Cat => _cat;
	public IReadOnlyList<Bird> Birds => _birds;
	public bool CatSleeping => _cat?.IsSleeping ?? false;
	public int Combo => _combo;
	/// <summary>Time scale for everything except the cat (Cat Time skill).</summary>
	public float WorldSpeed { get; set; } = 1f;
	/// <summary>Horizontal push on birds during the Windy event, px/s.</summary>
	public float Wind { get; private set; }

	public static float ComboMultiplier(int combo) => Mathf.Min(ComboCap, 1f + ComboStep * Mathf.Max(0, combo - 1));

	public override void _Ready()
	{
		Singleton = this;

		_cat = PlayerCat.Instantiate<Cat>();
		AddChild(_cat);
		_cat.Position = _cameraController.Position + _playAxis;
		_cat.GroundY = _cat.Position.Y;
		_cat.Landed += OnCatLanded;
		_cat.DiveLanded += OnDiveLanded;
		_cat.Jumped += () => _cameraController.Shake(0.6f, 0.05f);
		_cameraController.Follow(_cat);

		RunState.I.PhaseChanged += OnPhaseChanged;
		RunState.I.BossKilled += OnBossDefeated;
		RunState.I.StartRun();
	}

	public override void _ExitTree()
	{
		if (Singleton == this)
			Singleton = null;
		RunState.I.PhaseChanged -= OnPhaseChanged;
		RunState.I.BossKilled -= OnBossDefeated;
	}

	public override void _Process(double delta)
	{
		float d = (float)delta;
		float wd = d * WorldSpeed;
		var run = RunState.I;

		UpdateEvents(wd);

		_spawnTimer += wd;
		float interval = run.SpawnInterval * (run.IsBossDay && !run.BossDefeated ? 2f : 1f);
		if (run.Playing && _spawnTimer >= interval)
		{
			_spawnTimer = 0f;
			if (_birds.Count < MaxBirds)
				SpawnBird(run.Current == RunState.Phase.Dusk);
		}
		else if (run.Current == RunState.Phase.Night && _spawnTimer >= FireflyInterval)
		{
			_spawnTimer = 0f;
			if (_birds.Count < MaxFireflies)
				Spawn(BirdKind.Firefly, -30f, 6f, 1f, catchable: false);
		}

		UpdateYarns(wd);
		UpdateBoss(d, wd);

		var body = _cat.BodyCenter;
		for (int i = _birds.Count - 1; i >= 0; i--)
		{
			var bird = _birds[i];
			if (bird.Gone)
			{
				RemoveBird(i);
				continue;
			}
			if (!run.Playing || !bird.Catchable)
				continue;

			var toCat = body - bird.Center;
			float dist = toCat.Length();
			if (run.HasMagnet && dist < MagnetRange && dist > 0.01f)
				bird.Pull(toCat / dist * MagnetSpeed * wd);

			if (dist <= _cat.CatchRadius + bird.Info.Reach)
				CatchBird(i, _cat.IsStyling);
		}
	}

	public void ThrowYarn()
	{
		int dir = _cat.Facing;
		var yarn = new Sprite2D { Texture = YarnTexture, Position = _cat.BodyCenter + new Vector2(dir * 4, -4) };
		_birdLayer.AddChild(yarn);
		_yarns.Add((yarn, new Vector2(dir * YarnSpeedX, YarnSpeedY)));
	}

	private void UpdateYarns(float d)
	{
		for (int y = _yarns.Count - 1; y >= 0; y--)
		{
			var (sprite, vel) = _yarns[y];
			vel.Y += YarnGravity * d;
			sprite.Position += vel * d;
			sprite.Rotation += Mathf.Sign(vel.X) * 12f * d;
			_yarns[y] = (sprite, vel);

			bool hit = false;
			if (_boss != null && _boss.Vulnerable && sprite.Position.DistanceTo(_boss.Center) <= CrowKing.Radius + 4f)
			{
				HitBoss(1, sprite.Position);
				hit = true;
			}
			if (!hit && RunState.I.Playing)
			{
				for (int i = _birds.Count - 1; i >= 0; i--)
				{
					var b = _birds[i];
					if (b.Catchable && !b.Gone && b.Center.DistanceTo(sprite.Position) <= YarnReach + b.Info.Reach)
					{
						CatchBird(i, style: true);
						hit = true;
						break;
					}
				}
			}

			if (hit || sprite.Position.Y > _cat.GroundY + 16f || Mathf.Abs(sprite.Position.X) > 140f)
			{
				sprite.QueueFree();
				_yarns.RemoveAt(y);
			}
		}
	}

	private void SpawnBird(bool dusk)
	{
		var run = RunState.I;
		BirdKind kind;
		float golden = run.GoldenChance * (_goldenHour > 0f ? GoldenHourMul : 1f);
		if (GD.Randf() < golden)
			kind = BirdKind.Golden;
		else if (dusk && GD.Randf() < DuskOwlChance)
			kind = BirdKind.Owl;
		else
			kind = GD.Randf() < run.SwiftChance ? BirdKind.Swift : BirdKind.Sparrow;

		// Day birds stay out of ground reach (body centre y≈14, reach 8) unless lured.
		if (kind == BirdKind.Owl)
			Spawn(kind, -50f, -30f, run.BirdSpeedMul);
		else
			Spawn(kind, -48f, -14f, run.BirdSpeedMul);
	}

	private Bird Spawn(BirdKind kind, float minY, float maxY, float speedMul, bool catchable = true, int dir = 0)
	{
		if (dir == 0)
			dir = GD.Randf() < 0.5f ? 1 : -1;
		var bird = BirdScene.Instantiate<Bird>();
		_birdLayer.AddChild(bird);
		bird.Setup(kind, dir, (float)GD.RandRange(minY, maxY), -dir * SpawnX, speedMul, catchable);
		_birds.Add(bird);
		return bird;
	}

	private void UpdateEvents(float d)
	{
		var run = RunState.I;
		_goldenHour -= d;
		_windT -= d;
		float windTarget = _windT > 0f ? _windDir * WindStrength * Mathf.Min(1f, _windT) : 0f;
		Wind = Mathf.MoveToward(Wind, windTarget, 20f * d);

		if (_flockLeft > 0 && run.Playing)
		{
			_flockTimer -= d;
			if (_flockTimer <= 0f && _birds.Count < MaxBirdsDuringFlock)
			{
				_flockTimer = FlockGap;
				_flockLeft--;
				Spawn(BirdKind.Sparrow, _flockY - 6f, _flockY + 6f, run.BirdSpeedMul, dir: _flockDir);
			}
		}

		if (run.Current != RunState.Phase.Day || run.IsBossDay)
			return;
		_eventTimer -= d;
		if (_eventTimer > 0f)
			return;
		_eventTimer = (float)GD.RandRange(25.0, 40.0);

		switch (GD.Randi() % 3)
		{
			case 0:
				_flockLeft = FlockSize;
				_flockTimer = 0f;
				_flockDir = GD.Randf() < 0.5f ? 1 : -1;
				_flockY = (float)GD.RandRange(-40.0, -20.0);
				Announce("Flock!", "A whole flock is passing through");
				break;
			case 1:
				_goldenHour = EventLength;
				Announce("Golden hour", "Golden birds ×3 for 10 s");
				break;
			default:
				_windT = EventLength;
				_windDir = GD.Randf() < 0.5f ? 1 : -1;
				Announce("Windy!", _windDir > 0 ? "Birds drift right »" : "« Birds drift left");
				break;
		}
	}

	/// <summary>Crow King attack: dark feathers fall around the cat.</summary>
	public void SpawnFeatherRain(int count)
	{
		for (int i = 0; i < count; i++)
		{
			float x = i % 2 == 0
				? _cat.BodyCenter.X + (float)GD.RandRange(-30.0, 30.0)
				: (float)GD.RandRange(-105.0, 105.0);
			var f = new Sprite2D { Texture = DarkFeather, Scale = new Vector2(1.5f, 1.5f), Position = new Vector2(x, -70f - i * 6f) };
			_birdLayer.AddChild(f);
			_bossFeathers.Add((f, new Vector2((float)GD.RandRange(-8.0, 8.0), (float)GD.RandRange(55.0, 75.0))));
		}
	}

	/// <summary>Crow King attack: small crows (worth coins) fly in from the far side.</summary>
	public void SpawnMinions(int count)
	{
		int dir = _boss != null && _boss.Position.X > 0 ? -1 : 1;
		for (int i = 0; i < count; i++)
			Spawn(BirdKind.Crow, -40f, -16f, RunState.I.BirdSpeedMul, dir: dir);
	}

	private void UpdateBoss(float d, float wd)
	{
		var run = RunState.I;
		if (_bossSpawnT >= 0f)
		{
			_bossSpawnT -= d;
			if (_bossSpawnT < 0f && run.Playing && !run.BossDefeated)
			{
				_boss = new CrowKing();
				_birdLayer.AddChild(_boss);
				Audio.I.Play("hoot", 0f);
			}
		}

		for (int i = _bossFeathers.Count - 1; i >= 0; i--)
		{
			var (sprite, vel) = _bossFeathers[i];
			sprite.Position += new Vector2(vel.X + Mathf.Sin(sprite.Position.Y * 0.15f) * 12f, vel.Y) * wd;
			sprite.Rotation = Mathf.Sin(sprite.Position.Y * 0.1f) * 0.5f;
			bool hitCat = run.Playing && !_cat.Invulnerable && sprite.Position.DistanceTo(_cat.BodyCenter) < FeatherHitRadius;
			if (hitCat)
				StunCat(sprite.Position);
			if (hitCat || sprite.Position.Y > _cat.GroundY + 16f)
			{
				sprite.QueueFree();
				_bossFeathers.RemoveAt(i);
			}
		}

		if (_boss == null)
			return;
		if (_boss.Done)
		{
			_boss.QueueFree();
			_boss = null;
			return;
		}
		if (!run.Playing)
			return;

		var body = _cat.BodyCenter;
		float dist = body.DistanceTo(_boss.Center);
		bool touching = dist < CrowKing.Radius + 4f;
		bool swipeReach = _cat.IsSwiping && dist < CrowKing.Radius + _cat.CatchRadius;

		if (touching && _boss.Dangerous && !_cat.IsStyling)
		{
			if (!_cat.Invulnerable)
				StunCat(body);
		}
		else if ((touching && (_cat.InAir || _cat.IsDashing)) || swipeReach)
		{
			if (HitBoss(_cat.IsDiving ? 2 : 1, body) && _cat.InAir)
				_cat.Bounce();
		}
	}

	private bool HitBoss(int damage, Vector2 from)
	{
		if (_boss == null || !_boss.Vulnerable || !RunState.I.DamageBoss(damage))
			return false;
		_boss.Hit(from);
		Feathers.Burst(_birdLayer, _boss.Center, new Color("4a4466"));
		_cameraController.Shake(3f, 0.15f);
		Audio.I.Play("catch");
		Audio.I.Play("boom", 0.1f);
		EmitSignal(SignalName.Popup, $"-{damage}", _birdLayer.ToGlobal(_boss.Center), new Color("ff8a8a"));
		return true;
	}

	private void StunCat(Vector2 at)
	{
		_cat.Stun(StunTime);
		int lost = RunState.I.LoseCoins(StunCoinLoss);
		SetCombo(0);
		_cameraController.Shake(4f, 0.25f);
		Audio.I.Play("deny", 0f);
		EmitSignal(SignalName.Popup, lost > 0 ? $"Ouch! -{lost}" : "Ouch!", _birdLayer.ToGlobal(at), new Color("d9475b"));
	}

	private void OnBossDefeated(int reward)
	{
		_boss?.Defeat();
		foreach (var (sprite, _) in _bossFeathers)
			sprite.QueueFree();
		_bossFeathers.Clear();
		_cameraController.Shake(6f, 0.5f);
		Announce("Crow King defeated!", $"+{reward} coins  ·  +3 stars");
	}

	private void ClearBoss()
	{
		_bossSpawnT = -1f;
		_boss?.QueueFree();
		_boss = null;
		foreach (var (sprite, _) in _bossFeathers)
			sprite.QueueFree();
		_bossFeathers.Clear();
	}

	private void Announce(string title, string subtitle)
	{
		Audio.I.Play("event", 0f);
		EmitSignal(SignalName.BirdEvent, title, subtitle);
	}

	private void CatchBird(int index, bool style)
	{
		var bird = _birds[index];
		_combo += style ? 2 : 1;
		int coins = Mathf.RoundToInt((bird.Info.Coins + RunState.I.LuckyBonus) * ComboMultiplier(_combo) * GameState.I.CoinMul);

		RunState.I.AddCatch(coins, _combo);
		_cat.OnCatch();

		Feathers.Burst(_birdLayer, bird.Center, bird.Info.Particles);
		_cameraController.Shake(style ? 3f : 2f, 0.12f);
		Audio.I.Play("catch");
		Audio.I.Play("coin");
		if (_combo >= 2)
			Audio.I.Play("combo", 0f);

		EmitSignal(SignalName.Caught, coins, _combo, _birdLayer.ToGlobal(bird.Center), style);
		EmitSignal(SignalName.ComboChanged, _combo);
		RemoveBird(index);
	}

	private void RemoveBird(int index)
	{
		_birds[index].QueueFree();
		_birds.RemoveAt(index);
	}

	private void ClearBirds()
	{
		for (int i = _birds.Count - 1; i >= 0; i--)
			RemoveBird(i);
		foreach (var (sprite, _) in _yarns)
			sprite.QueueFree();
		_yarns.Clear();
	}

	private void OnDiveLanded()
	{
		var at = _cat.BodyCenter;
		foreach (var b in _birds)
			if (b.Catchable && Mathf.Abs(b.Center.X - at.X) < ShockwaveRange)
				b.Lure(ShockwaveLure);
		Ring.Spawn(this, at + new Vector2(0, 6), ShockwaveRange, new Color("fff3c4"), 0.4f);
		Feathers.Burst(this, at + new Vector2(0, 6), new Color("bdac9b"));
		_cameraController.Shake(3.5f, 0.2f);
		Audio.I.Play("boom");
	}

	private void OnCatLanded(int catches)
	{
		if (catches > 0 || _combo == 0)
			return;
		if (RunState.I.CatnipLeft > 0)
		{
			RunState.I.CatnipLeft--;
			EmitSignal(SignalName.CatnipUsed, ToGlobal(_cat.BodyCenter));
			return;
		}
		SetCombo(0);
	}

	private void SetCombo(int combo)
	{
		_combo = combo;
		EmitSignal(SignalName.ComboChanged, combo);
	}

	private void OnPhaseChanged(int phase)
	{
		var p = (RunState.Phase)phase;
		Audio.I.SetMusic(p is not (RunState.Phase.Day or RunState.Phase.Dusk or RunState.Phase.Dawn));
		if (p == RunState.Phase.Day)
		{
			ClearBirds();
			SetCombo(0);
			_spawnTimer = 0f;
			_eventTimer = (float)GD.RandRange(20.0, 30.0);
			_flockLeft = 0;
			_goldenHour = 0f;
			_windT = 0f;
			Wind = 0f;
			ClearBoss();
			if (RunState.I.IsBossDay)
			{
				_bossSpawnT = BossArrivalDelay;
				Announce("Boss Day!", "The Crow King approaches — hit it while airborne");
			}
		}
		else if (p is RunState.Phase.Night or RunState.Phase.Dawn)
		{
			ClearBirds();
			ClearBoss();
		}
	}
}
