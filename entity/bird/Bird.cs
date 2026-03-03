using Godot;
using System;
public partial class Bird : Sprite2D
{
	private Area2D _area; 
	private AnimationPlayer _anim;

	private double _speed = 200f;
	private double _amplitude = 40f;
	private double _frequency = 3f;

	private double _time;
	private double _phase;
	private Vector2 _startPosition;
	
	private float _flyDirection = -1f;
	private Vector2 _flyDirectionChangeRate = new Vector2(1,3);
	private double _currentFlyDirectionRate;
	private double _flyDirectionChangeTimer = 0;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_area = GetNode<Area2D>("Area2D");
		_area.BodyEntered += OnBodyEntered;
		
		_anim = GetNode<AnimationPlayer>("AnimationPlayer");
		_anim.Play("fly");
		_flyDirection = GD.Randf() < 0.5f ? -1f : 1f;
		_currentFlyDirectionRate = GD.RandRange(_flyDirectionChangeRate.X, _flyDirectionChangeRate.Y);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		ChangeDirectionTimer(delta);
		Fly(delta);
	}

	private void ChangeDirectionTimer(double delta)
	{
		_flyDirectionChangeTimer += delta;

		if (_flyDirectionChangeTimer >= _currentFlyDirectionRate)
		{
			_flyDirectionChangeTimer = 0;

			// Flip direction
			_flyDirection *= -1f;

			// Optional: flip sprite visually
			FlipH = _flyDirection > 0;
			
			_currentFlyDirectionRate = GD.RandRange(_flyDirectionChangeRate.X, _flyDirectionChangeRate.Y);
		}
	}

	private void Fly(double delta)
	{
		_time += delta;

		double newX = Position.X + _flyDirection * _speed * delta;

		double newY = _startPosition.Y +
					 Mathf.Sin(_time * _frequency + _phase) * _amplitude;

		Position = new Vector2((float)newX, (float)newY);
	}

	private void OnBodyEntered(Node body)
	{
		if (body is CharacterBody2D)
		{
			SceneController.Singleton.AddBirdValue(1);
			GD.Print("Bird collided with player: ", SceneController.Singleton.GetBirdValue());
			QueueFree(); // remove bird
		}
	}
}
