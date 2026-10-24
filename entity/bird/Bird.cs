using Godot;

/// <summary>
/// A catchable creature. Crosses the screen once along an analytic path, then reports Gone.
/// Frames are stepped in code because owls/fireflies use different sheets than sparrows.
/// </summary>
public partial class Bird : Sprite2D
{
	private static readonly Texture2D BirdSheet = GD.Load<Texture2D>("res://sprites/Bird Spritesheet.png");
	private static readonly Texture2D OwlSheet = GD.Load<Texture2D>("res://sprites/generated/owl.png");
	private static readonly Texture2D FireflySheet = GD.Load<Texture2D>("res://sprites/generated/firefly.png");
	private static readonly Shader HsvShader = GD.Load<Shader>("res://shaders/hsv_tint.gdshader");

	private static readonly int[] FlyFrames = { 8, 9, 10, 12, 13, 14, 15 };
	private static readonly int[] OwlGlide = { 0, 1, 2, 3 };
	private static readonly int[] OwlDive = { 4, 5 };
	private static readonly int[] OwlRise = { 6, 7 };
	private static readonly int[] FireflyGlow = { 0, 1, 2, 3, 2, 1 };

	private const float DespawnX = 140f;
	private const float SleepDrop = 14f;
	private const float SleepSlow = 0.6f;
	private const float LureDrop = 22f;
	private const float LureSlow = 0.45f;
	private const float SwoopDepth = 26f;
	private const float SwoopTime = 1.3f;

	public BirdKind Kind { get; private set; }
	public BirdInfo Info { get; private set; }
	public bool Gone { get; private set; }
	/// <summary>Night fireflies are decoration only.</summary>
	public bool Catchable { get; private set; } = true;
	public Vector2 Center => Position + Info.CenterOffset;

	private int _dir;
	private float _speedMul = 1f;
	private float _lureT;
	private float _baseY;
	private float _t;
	private float _phase;
	private float _drop;
	private float _swoopIn;
	private float _swoop = -1f;
	private float _animT;
	private PointLight2D _glow;

	public void Setup(BirdKind kind, int dir, float y, float x, float speedMul = 1f, bool catchable = true)
	{
		Kind = kind;
		_speedMul = speedMul;
		Catchable = catchable;
		Info = BirdKinds.Info(kind);
		_dir = dir;
		_baseY = y;
		_phase = (float)GD.RandRange(0, Mathf.Tau);
		_swoopIn = (float)GD.RandRange(1.5, 3.5);
		Position = new Vector2(x, y);

		switch (kind)
		{
			case BirdKind.Owl:
				Texture = OwlSheet;
				Hframes = 4;
				Vframes = 2;
				break;
			case BirdKind.Firefly:
				Texture = FireflySheet;
				Hframes = 4;
				Vframes = 1;
				_glow = new PointLight2D
				{
					Texture = Lights.Radial, Color = new Color(0.8f, 1f, 0.45f), Energy = 0.8f, TextureScale = 0.6f,
				};
				AddChild(_glow);
				break;
			default:
				Texture = BirdSheet;
				Hframes = 8;
				Vframes = 4;
				break;
		}

		ApplyLook();
	}

	public override void _Process(double delta)
	{
		var sc = SceneController.Singleton;
		float d = (float)delta * (sc?.WorldSpeed ?? 1f);
		_lureT -= d;
		bool napLure = sc != null && sc.CatSleeping && Kind != BirdKind.Owl;
		bool skillLure = _lureT > 0f;
		float slow = skillLure ? LureSlow : napLure ? SleepSlow : 1f;
		float drop = skillLure ? LureDrop : napLure ? SleepDrop : 0f;
		_drop = Mathf.MoveToward(_drop, drop, (skillLure ? 45f : 20f) * d);
		_t += d * slow;

		float x = Position.X + (_dir * Info.Speed * _speedMul * slow + (sc?.Wind ?? 0f)) * d;
		float y = _baseY + _drop + Mathf.Sin(_t * Info.Frequency + _phase) * Info.Amplitude;

		int[] frames = FlyFrames;
		float fps = 10f;
		switch (Kind)
		{
			case BirdKind.Owl:
				y += Swoop(d, ref frames);
				fps = 8f;
				break;
			case BirdKind.Firefly:
				x += Mathf.Sin(_t * 0.9f + _phase) * 8f * d;
				y += Mathf.Sin(_t * 2.9f) * 3f;
				frames = FireflyGlow;
				fps = 8f;
				_glow.Energy = 0.55f + 0.35f * Mathf.Sin(_t * 4f + _phase);
				break;
		}

		Position = new Vector2(x, y);
		FlipH = _dir > 0; // sheets face left
		_animT += d * fps;
		Frame = frames[(int)_animT % frames.Length];

		// Second check guards against wind pushing a slow bird back off the screen it came from.
		if ((_dir > 0 && x > DespawnX) || (_dir < 0 && x < -DespawnX) || Mathf.Abs(x) > DespawnX + 20f)
			Gone = true;
	}

	/// <summary>Meow / dive shockwave: slow down and swoop low for a while.</summary>
	public void Lure(float time) => _lureT = Mathf.Max(_lureT, time);

	/// <summary>Magnet pull: shifts the path itself so the sine motion keeps working.</summary>
	public void Pull(Vector2 delta)
	{
		_baseY += delta.Y;
		Position += new Vector2(delta.X, delta.Y);
	}

	private float Swoop(float d, ref int[] frames)
	{
		if (_swoop < 0f)
		{
			_swoopIn -= d;
			frames = OwlGlide;
			if (_swoopIn > 0f)
				return 0f;
			_swoop = 0f;
			if (Mathf.Abs(Position.X) < 110f)
				Audio.I.Play("hoot", 0.08f);
		}

		_swoop += d;
		float k = _swoop / SwoopTime;
		if (k >= 1f)
		{
			_swoop = -1f;
			_swoopIn = (float)GD.RandRange(3.0, 5.0);
			frames = OwlGlide;
			return 0f;
		}
		frames = k < 0.45f ? OwlDive : OwlRise;
		return Mathf.Sin(k * Mathf.Pi) * SwoopDepth;
	}

	private void ApplyLook()
	{
		var info = Info;
		float hue = info.HueShift, sat = 1f, val = info.ValueMul;
		if (BirdKinds.Skinnable(Kind) && GameState.I.EquippedDef(Catalog.SlotBirdSkin)?.Hsv is Vector3 skin)
		{
			hue += skin.X;
			sat *= skin.Y;
			val *= skin.Z;
		}

		if (hue == 0f && sat == 1f && val == 1f && info.TintAmount == 0f)
		{
			Material = null;
			return;
		}

		var m = new ShaderMaterial { Shader = HsvShader };
		m.SetShaderParameter("hue_shift", hue);
		m.SetShaderParameter("sat_mul", sat);
		m.SetShaderParameter("val_mul", val);
		m.SetShaderParameter("tint", info.TintAmount > 0f ? info.Tint : Colors.White);
		m.SetShaderParameter("tint_amount", info.TintAmount);
		Material = m;
	}
}
