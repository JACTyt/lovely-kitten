using Godot;

/// <summary>
/// Player cat. Motion is analytic (no physics bodies): constant-gravity arc with separate rise/fall gravity,
/// variable height on early release, jump buffering, air control and optional double jump.
/// </summary>
public partial class Cat : Sprite2D
{
	[Signal] public delegate void JumpedEventHandler();
	[Signal] public delegate void LandedEventHandler(int catches);
	[Signal] public delegate void DiveLandedEventHandler();

	[Export] public float BaseJumpHeight = 40f;
	[Export] public float TimeToPeak = 0.32f;
	[Export] public float FallGravityMul = 1.8f;
	[Export] public float JumpCutMul = 0.45f;
	[Export] public float DoubleJumpMul = 0.8f;
	[Export] public float JumpBuffer = 0.1f;
	[Export] public float BaseMoveSpeed = 60f;
	[Export] public float GroundAccel = 600f;
	[Export] public float GroundDecel = 900f;
	[Export] public float AirControl = 0.6f;
	[Export] public float AirDrag = 120f;
	[Export] public float WrapHalfWidth = 120f;
	[Export] public float GroundY = 4f;

	private const float IdleWaitingDelay = 2f;
	private const float PounceTime = 0.3f;
	private const float GroundReach = 8f;
	private const float AirReach = 12f;
	private const float DashTime = 0.2f;
	private const float DashSpeed = 225f;
	private const float DiveSpeed = 420f;
	private const float GlideFallSpeed = 35f;
	private const float SwipeTime = 0.3f;

	private AnimationPlayer _anim;
	private ShaderMaterial _palette;
	private Vector2 _vel;
	private bool _inAir;
	private bool _cut;
	private bool _usedDouble;
	private float _buffer;
	private float _idleTimer;
	private bool _idleWaiting;
	private float _pounce;
	private int _airCatches;
	private float _dashT;
	private bool _airDashUsed;
	private bool _diving;
	private float _swipeT;
	private float _stunT;
	private float _invulnT;

	public bool InAir => _inAir;
	public bool IsSleeping { get; private set; }
	public Vector2 BodyCenter => Position + new Vector2(0, 10);
	public float CatchRadius =>
		((_inAir ? AirReach : GroundReach) + RunState.I.CatchReachBonus) * (_swipeT > 0f ? 2f : 1f);
	public bool IsDashing => _dashT > 0f;
	public bool IsDiving => _diving;
	/// <summary>Catches made mid-dash or mid-dive earn a bonus combo step.</summary>
	public bool IsStyling => IsDashing || IsDiving;
	public bool IsSwiping => _swipeT > 0f;
	public int Facing => FlipH ? -1 : 1;
	public bool IsStunned => _stunT > 0f;
	public bool Invulnerable => _invulnT > 0f;

	private float JumpHeight => BaseJumpHeight * RunState.I.JumpHeightMul;
	private float GravityUp => 2f * JumpHeight / (TimeToPeak * TimeToPeak);
	private float JumpVelocity => 2f * JumpHeight / TimeToPeak;

	public override void _Ready()
	{
		_anim = GetNode<AnimationPlayer>("CatAnimation");
		_anim.AnimationFinished += OnAnimationFinished;
		_palette = (ShaderMaterial)Material;
		GameState.I.Changed += ApplySkin;
		ApplySkin();
		_anim.Play("idle");
	}

	public override void _ExitTree()
	{
		if (GameState.I != null)
			GameState.I.Changed -= ApplySkin;
	}

	public override void _Process(double delta)
	{
		float d = (float)delta;
		// Outside daytime the cat only falls back down and sleeps; player input is ignored.
		_stunT -= d;
		_invulnT -= d;
		bool control = RunState.I.Playing && !IsStunned;
		float input = control ? Input.GetAxis("move_left", "move_right") : 0f;

		_buffer = control && Input.IsActionJustPressed("jump") ? JumpBuffer : _buffer - d;
		_pounce -= d;
		_swipeT -= d;

		IsSleeping = !_inAir && !IsStunned && (!RunState.I.Playing || (control && Input.IsActionPressed("sleep")));
		if (IsSleeping)
			input = 0f;

		UpdateHorizontal(input, d);

		if (!_inAir)
		{
			if (_buffer > 0f && !IsSleeping)
				StartJump(1f);
		}
		else if (control && Input.IsActionJustPressed("jump") && RunState.I.HasDoubleJump && !_usedDouble)
		{
			_usedDouble = true;
			StartJump(DoubleJumpMul);
		}

		if (_inAir)
			UpdateVertical(d);

		Position = new Vector2(Mathf.Wrap(Position.X, -WrapHalfWidth, WrapHalfWidth), Position.Y);

		if (input != 0f)
			FlipH = input < 0f;

		Modulate = new Color(1, 1, 1, Invulnerable && Mathf.Sin(_invulnT * 40f) > 0f ? 0.35f : 1f);
		Animate(d);
	}

	/// <summary>Called by SceneController when this cat catches a bird.</summary>
	public void OnCatch()
	{
		_airCatches++;
		if (_inAir)
		{
			_pounce = PounceTime;
			_usedDouble = false; // reward: catches refresh the double jump
		}
	}

	/// <summary>Horizontal burst; once per air time. Holds height while dashing.</summary>
	public bool TryDash()
	{
		if (IsSleeping || (_inAir && _airDashUsed))
			return false;
		float input = Input.GetAxis("move_left", "move_right");
		int dir = input != 0f ? Mathf.Sign(input) : Facing;
		FlipH = dir < 0;
		_dashT = DashTime;
		_vel.X = dir * DashSpeed;
		_diving = false;
		if (_inAir)
		{
			_airDashUsed = true;
			_vel.Y = 0f;
		}
		return true;
	}

	/// <summary>Straight-down slam; DiveLanded fires on touchdown.</summary>
	public bool TryDive()
	{
		if (!_inAir || _diving)
			return false;
		_diving = true;
		_dashT = 0f;
		_cut = true;
		_vel = new Vector2(0f, DiveSpeed);
		return true;
	}

	public void Swipe() => _swipeT = SwipeTime;

	/// <summary>Boss hit: lose control briefly, then blink invulnerable.</summary>
	public void Stun(float time)
	{
		_stunT = time;
		_invulnT = time + 0.8f;
		_dashT = 0f;
		_diving = false;
		_vel.X = 0f;
	}

	/// <summary>Pop upward after landing a hit on the boss.</summary>
	public void Bounce()
	{
		_dashT = 0f;
		_diving = false;
		_cut = true;
		_inAir = true;
		_vel.Y = -JumpVelocity * 0.6f;
	}

	private void UpdateHorizontal(float input, float d)
	{
		if (_dashT > 0f)
		{
			_dashT -= d;
			Position += new Vector2(_vel.X * d, 0f);
			return;
		}
		float speed = BaseMoveSpeed * RunState.I.MoveSpeedMul;
		float target = input * speed;
		float rate;
		if (_inAir)
			rate = input != 0f ? GroundAccel * AirControl : AirDrag;
		else
			rate = input != 0f ? GroundAccel : GroundDecel;
		_vel.X = Mathf.MoveToward(_vel.X, target, rate * d);
		Position += new Vector2(_vel.X * d, 0f);
	}

	private void UpdateVertical(float d)
	{
		// Variable height: releasing jump while rising cuts upward speed once.
		if (!_cut && _vel.Y < 0f && !Input.IsActionPressed("jump"))
		{
			_vel.Y *= JumpCutMul;
			_cut = true;
		}

		if (_dashT > 0f)
			return; // hover while dashing

		float g = _vel.Y < 0f ? GravityUp : GravityUp * FallGravityMul;
		if (!_diving)
			_vel.Y += g * d;
		if (!_diving && _vel.Y > GlideFallSpeed && RunState.I.HasGlide && Input.IsActionPressed("jump"))
			_vel.Y = GlideFallSpeed;
		Position += new Vector2(0f, _vel.Y * d);

		if (Position.Y >= GroundY)
			Land();
	}

	private void StartJump(float mul)
	{
		_vel.Y = -JumpVelocity * mul;
		_inAir = true;
		_cut = false;
		_buffer = 0f;
		if (mul >= 1f)
			_usedDouble = false;
		Audio.I.Play("jump", mul < 1f ? 0.02f : 0.05f);
		EmitSignal(SignalName.Jumped);
	}

	private void Land()
	{
		Position = new Vector2(Position.X, GroundY);
		_vel.Y = 0f;
		_inAir = false;
		_pounce = 0f;
		_airDashUsed = false;
		if (_diving)
		{
			_diving = false;
			EmitSignal(SignalName.DiveLanded);
		}
		Audio.I.Play("land");
		int catches = _airCatches;
		_airCatches = 0;
		EmitSignal(SignalName.Landed, catches);
	}

	private void Animate(float d)
	{
		string next;
		if (IsStunned)
			next = "idle_waiting";
		else if (_pounce > 0f || _dashT > 0f)
			next = "pounce";
		else if (_diving)
			next = "fall";
		else if (_inAir)
			next = _vel.Y < 0f ? "rise" : "fall";
		else if (IsSleeping)
			next = "sleep";
		else if (Mathf.Abs(_vel.X) > 5f)
			next = "move";
		else
		{
			if (_anim.CurrentAnimation != "idle" && _anim.CurrentAnimation != "idle_waiting")
			{
				_idleTimer = 0f;
				_idleWaiting = false;
			}
			if (!_idleWaiting)
			{
				_idleTimer += d;
				if (_idleTimer >= IdleWaitingDelay)
				{
					_idleTimer = 0f;
					_idleWaiting = true;
				}
			}
			next = _idleWaiting ? "idle_waiting" : "idle";
		}

		if (_anim.CurrentAnimation != next)
			_anim.Play(next);
	}

	private void OnAnimationFinished(StringName animName)
	{
		if (animName == "idle_waiting")
			_idleWaiting = false;
	}

	private void ApplySkin()
	{
		var pal = GameState.I.EquippedDef(Catalog.SlotCatSkin)?.Palette;
		if (pal == null)
			return;
		_palette.SetShaderParameter("light_col", pal[0]);
		_palette.SetShaderParameter("mid_col", pal[1]);
		_palette.SetShaderParameter("dark_col", pal[2]);
	}
}
