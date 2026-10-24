using Godot;

/// <summary>
/// Boss Day enemy. Flies in, hovers, then cycles Feather Rain → Swoop → Summon, attacking faster as its health
/// drops. Health lives in RunState; SceneController resolves contact, hits and projectiles.
/// </summary>
public partial class CrowKing : Sprite2D
{
	private enum State { Enter, Hover, Rain, Windup, Swoop, Summon, Defeated }

	private static readonly Texture2D Sheet = GD.Load<Texture2D>("res://sprites/generated/crow_king.png");
	private static readonly int[] Flap = { 0, 1, 2, 3 };
	private static readonly int[] Dive = { 4, 5 };
	private static readonly State[] Cycle = { State.Rain, State.Swoop, State.Summon };

	public const float Radius = 12f;
	// Low enough that a plain jump (body reaches y≈-26) or a yarn arc (peak y≈-20) can hit it.
	private const float HoverY = -28f;
	private const float SwoopTime = 1.3f;
	private const float WindupTime = 0.7f;
	private const float HurtTime = 0.6f;
	private const float Knockback = 10f;

	private State _state = State.Enter;
	private float _t;
	private float _stateT;
	private float _hurtT;
	private float _animT;
	private int _attack;
	private bool _fired;
	private Vector2 _p0, _p1, _p2;

	public Vector2 Center => Position + new Vector2(0, 2);
	/// <summary>Contact hurts the cat (unless it is dashing/diving into it).</summary>
	public bool Dangerous => _state == State.Swoop;
	public bool Vulnerable => _state is not (State.Enter or State.Defeated) && _hurtT <= 0f;
	public bool Done { get; private set; }

	public override void _Ready()
	{
		Texture = Sheet;
		Hframes = 4;
		Vframes = 2;
		Position = new Vector2(150f, -55f);
	}

	public override void _Process(double delta)
	{
		var sc = SceneController.Singleton;
		float d = (float)delta * (sc?.WorldSpeed ?? 1f);
		_t += d;
		_stateT += d;
		_hurtT -= d;
		Modulate = _hurtT > 0f && Mathf.Sin(_hurtT * 50f) > 0f ? new Color(1f, 0.45f, 0.45f) : Colors.White;

		var run = RunState.I;
		float rage = run.BossMaxHp > 0 ? 1f - (float)run.BossHp / run.BossMaxHp : 0f;
		int[] frames = Flap;
		float fps = 10f;
		float prevX = Position.X;

		switch (_state)
		{
			case State.Enter:
				Position = Position.MoveToward(new Vector2(60f, HoverY), 60f * d);
				if (Position.DistanceTo(new Vector2(60f, HoverY)) < 1f)
					Next(State.Hover);
				break;

			case State.Hover:
				Position = new Vector2(Mathf.MoveToward(Position.X, Mathf.Sin(_t * 0.6f) * 70f, 50f * d),
					HoverY + Mathf.Sin(_t * 2f) * 4f);
				if (_stateT >= 3.5f - 1.6f * rage && run.Playing)
				{
					Next(Cycle[_attack % Cycle.Length]);
					_attack++;
				}
				break;

			case State.Rain:
				fps = 18f;
				if (!_fired && _stateT >= 0.3f)
				{
					_fired = true;
					sc?.SpawnFeatherRain(6 + Mathf.RoundToInt(4 * rage));
				}
				if (_stateT >= 1.2f)
					Next(State.Hover);
				break;

			case State.Windup:
				// Telegraph: rise and shiver, then commit to a curve through the cat's current position.
				fps = 16f;
				Position = Position.MoveToward(new Vector2(Position.X, -52f), 40f * d)
				           + new Vector2((float)GD.RandRange(-0.6, 0.6), 0f);
				if (_stateT >= WindupTime)
				{
					var cat = sc?.Cat?.BodyCenter ?? Vector2.Zero;
					_p0 = Position;
					_p2 = new Vector2(Position.X < cat.X ? 120f : -120f, -45f);
					// Choose the control point so the curve's midpoint (k = 0.5) passes through the cat.
					_p1 = 2f * cat - 0.5f * (_p0 + _p2);
					Next(State.Swoop);
				}
				break;

			case State.Swoop:
			{
				float k = _stateT / SwoopTime;
				float u = 1f - k;
				Position = u * u * _p0 + 2f * u * k * _p1 + k * k * _p2;
				frames = Dive;
				if (k >= 1f)
					Next(State.Hover);
				break;
			}

			case State.Summon:
				fps = 14f;
				if (!_fired && _stateT >= 0.4f)
				{
					_fired = true;
					sc?.SpawnMinions(3);
				}
				if (_stateT >= 1f)
					Next(State.Hover);
				break;

			case State.Defeated:
				frames = Dive;
				Rotation += 6f * d;
				Position += new Vector2(Mathf.Sign(Position.X) * 20f * d, 70f * d);
				if (Position.Y > 90f)
					Done = true;
				break;
		}

		if (Mathf.Abs(Position.X - prevX) > 0.05f)
			FlipH = Position.X > prevX; // sheet faces left
		_animT += d * fps;
		Frame = frames[(int)_animT % frames.Length];
	}

	/// <summary>Knockback + brief invulnerability. A hit interrupts a swoop.</summary>
	public bool Hit(Vector2 from)
	{
		if (!Vulnerable)
			return false;
		_hurtT = HurtTime;
		var away = (Center - from).Normalized();
		Position += (away == Vector2.Zero ? Vector2.Up : away) * Knockback;
		if (_state is State.Swoop or State.Windup)
			Next(State.Hover);
		return true;
	}

	public void Defeat() => Next(State.Defeated);

	private void Next(State s)
	{
		// Swoop is entered through its telegraph.
		_state = s == State.Swoop && _state != State.Windup ? State.Windup : s;
		_stateT = 0f;
		_fired = false;
	}
}
