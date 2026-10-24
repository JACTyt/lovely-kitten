using Godot;
using System.Collections.Generic;

/// <summary>
/// Active skills bought in the night shop. Each has its own key (input action "skill_&lt;id&gt;") and cooldown;
/// extra stacks shorten the cooldown (RunState.CooldownMul). Cooldowns reset at the start of each day.
/// </summary>
public partial class Skills : Node
{
	[Signal] public delegate void SkillReadyEventHandler(string id);

	public static readonly string[] Order = { "dash", "swipe", "dive", "meow", "yarn", "cattime" };

	private static readonly Dictionary<string, float> BaseCooldown = new()
	{
		["dash"] = 1.5f, ["swipe"] = 3f, ["dive"] = 4f, ["meow"] = 10f, ["yarn"] = 6f, ["cattime"] = 25f,
	};

	private const float MeowRadius = 60f;
	private const float MeowLure = 2.5f;
	private const float CatTimeLength = 3f;
	private const float CatTimeSpeed = 0.4f;

	private readonly Dictionary<string, float> _cooldown = new();
	private SceneController _sc;
	private float _catTime;

	public float Cooldown(string id) => BaseCooldown[id] * RunState.I.CooldownMul(id);
	public float Remaining(string id) => _cooldown.TryGetValue(id, out float r) ? r : 0f;
	/// <summary>1 = just used, 0 = ready.</summary>
	public float Fraction(string id) => Mathf.Clamp(Remaining(id) / Cooldown(id), 0f, 1f);
	public bool CatTimeActive => _catTime > 0f;

	public override void _Ready()
	{
		_sc = GetParent<SceneController>();
		RunState.I.PhaseChanged += OnPhaseChanged;
	}

	public override void _ExitTree()
	{
		RunState.I.PhaseChanged -= OnPhaseChanged;
	}

	public override void _Process(double delta)
	{
		float d = (float)delta;
		foreach (var id in Order)
		{
			float r = Remaining(id);
			if (r <= 0f)
				continue;
			r -= d;
			_cooldown[id] = Mathf.Max(0f, r);
			if (r <= 0f && RunState.I.Playing)
			{
				Audio.I.Play("ready", 0f);
				EmitSignal(SignalName.SkillReady, id);
			}
		}

		_catTime -= d;
		_sc.WorldSpeed = Mathf.MoveToward(_sc.WorldSpeed, _catTime > 0f ? CatTimeSpeed : 1f, 4f * d);

		if (!RunState.I.Playing)
			return;
		foreach (var id in Order)
			if (RunState.I.Has(id) && Remaining(id) <= 0f && Input.IsActionJustPressed("skill_" + id) && Activate(id))
				_cooldown[id] = Cooldown(id);
	}

	private bool Activate(string id)
	{
		var cat = _sc.Cat;
		switch (id)
		{
			case "dash":
				if (!cat.TryDash())
					return false;
				Audio.I.Play("dash");
				return true;
			case "swipe":
				cat.Swipe();
				Ring.Spawn(_sc, cat.BodyCenter, cat.CatchRadius, new Color("fff3c4"), 0.25f);
				Audio.I.Play("swipe");
				return true;
			case "dive":
				if (!cat.TryDive())
					return false;
				Audio.I.Play("dive");
				return true;
			case "meow":
				foreach (var b in _sc.Birds)
					if (b.Catchable && b.Center.DistanceTo(cat.BodyCenter) < MeowRadius)
						b.Lure(MeowLure);
				Ring.Spawn(_sc, cat.BodyCenter, MeowRadius, new Color("e46fa0"), 0.45f);
				Audio.I.Play("meow", 0.08f);
				return true;
			case "yarn":
				_sc.ThrowYarn();
				Audio.I.Play("throw");
				return true;
			case "cattime":
				_catTime = CatTimeLength;
				Audio.I.Play("timeslow", 0f);
				return true;
		}
		return false;
	}

	private void OnPhaseChanged(int phase)
	{
		if ((RunState.Phase)phase != RunState.Phase.Day)
			return;
		_cooldown.Clear();
		_catTime = 0f;
	}
}
