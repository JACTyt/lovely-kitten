using Godot;

public partial class Cat : Sprite2D
{
	private AnimationPlayer _anim;
	private Bounder _bounder;

	private const float IdleWaitingDelay = 2.0f;
	private float _idleTimer = 0f;

	private int _moveDir = 0;
	[Export] private float _moveSpeed = 50f;

	private bool _isJumping = false;
	private bool _isIdle = false;
	private bool _playingIdleWaiting = false;

	[Export] private Vector2 _minBound;
	[Export] private Vector2 _maxBound;

	[Export] private float JumpHeight = 100f; // 1 meter
	[Export] private float JumpDistance = 250f; // 2.5 meters
	[Export] private float Gravity = 900f;

	private Vector2 _velocity = Vector2.Zero;
	private float _jumpTime = 0f;
	private float _jumpDuration;

	[Export] private float GroundY = 1f;

	public override void _Ready()
	{
		_anim = GetNode<AnimationPlayer>("CatAnimation");
		_anim.AnimationFinished += OnAnimationFinished;
		_bounder = new Bounder(_minBound, _maxBound);
		EnterIdle();
	}

	public override void _Process(double delta)
	{
		float d = (float)delta;
		Position = _bounder.Bound(Position);

		// --- Jumping
		if (_isJumping)
		{
			_velocity.Y += Gravity * d;
			Position += _velocity * d;
			
			// 🔑 LANDING CHECK
			if (Position.Y >= GroundY)
			{
				Position = new Vector2(Position.X, GroundY); // вирівнюємо
				_velocity = Vector2.Zero;
				_isJumping = false;

				// 🔑 STOP / SWITCH ANIMATION
				_anim.Stop();
				EnterIdle(); // або PlayIfNot("idle")
			}
			
			return;
		}


		// --- Jump
		if (Input.IsActionJustPressed("jump") && !_isJumping)
		{
			_isJumping = true;
			ExitIdle();
			Jump(d);
			PlayIfNot("jump");
			return;
		}

		// --- Sleep
		if (Input.IsActionPressed("sleep") && !_isJumping)
		{
			ExitIdle();
			PlayIfNot("sleep");
			return;
		}

		// --- Movement
		if (!_isJumping)
		{
			if (Input.IsActionPressed("move_left"))
			{
				Move(-1, d);
				return;
			}
			else if (Input.IsActionPressed("move_right"))
			{
				Move(1, d);
				return;
			}
		}

		// --- Idle logic (FIXED)
		if (!_isJumping)
		{
			HandleIdle(d);
		}
	}

	private void Jump(float delta)
	{
		_isJumping = true;
		ExitIdle();

		// Time needed to reach peak
		_jumpDuration = Mathf.Sqrt((2f * JumpHeight) / Gravity);

		// Initial vertical velocity
		_velocity.Y = -Gravity * _jumpDuration;

		// Horizontal velocity (based on direction)
		float dir = _moveDir != 0 ? _moveDir : (FlipH ? -1 : 1);
		_velocity.X = JumpDistance / (2f * _jumpDuration) * dir;
	}


	private void Move(int dir, float delta)
	{
		_moveDir = dir;
		ExitIdle();

		Position += new Vector2(dir * _moveSpeed * delta, 0);

		Flip(dir > 0);
		PlayIfNot("move");
	}

	private void HandleIdle(float delta)
	{
		if (!_isIdle)
		{
			EnterIdle();
			return;
		}

		if (_playingIdleWaiting)
			return;

		_idleTimer += delta;

		if (_idleTimer >= IdleWaitingDelay)
		{
			_idleTimer = 0f;
			_playingIdleWaiting = true;
			PlayIfNot("idle_waiting");
		}
	}

	private void EnterIdle()
	{
		_isIdle = true;
		_playingIdleWaiting = false;
		_idleTimer = 0f;
		PlayIfNot("idle");
	}

	private void ExitIdle()
	{
		_isIdle = false;
		_playingIdleWaiting = false;
		_idleTimer = 0f;
	}

	private void OnAnimationFinished(StringName animName)
	{
		if (animName == "jump")
		{
			_isJumping = false;

			if (_moveDir != 0)
				PlayIfNot("move");
			else
				EnterIdle();
		}
		else if (animName == "idle_waiting")
		{
			// 🔑 REQUIRED BEHAVIOR
			EnterIdle();
		}
	}

	private void PlayIfNot(string name)
	{
		if (_anim.CurrentAnimation != name)
			_anim.Play(name);
	}

	private void Flip(bool faceRight)
	{
		FlipH = !faceRight;
	}
}
