using Godot;

public partial class CameraController : Camera2D
{
	private Node2D _followNode;

	[Export] private Vector2 _maxCameraPosition;
	[Export] private Vector2 _minCameraPosition;

	private float _shakeStrength;
	private float _shakeTime;
	private float _shakeLeft;

	public override void _Process(double delta)
	{
		if (_followNode != null)
		{
			// Follow only on X-axis, keep current Y position
			float posX = Mathf.Clamp(_followNode.Position.X, _minCameraPosition.X, _maxCameraPosition.X);
			float posY = Mathf.Clamp(_followNode.Position.Y, _minCameraPosition.Y, _maxCameraPosition.Y);
			Position = new Vector2(posX, posY);
		}

		if (_shakeLeft > 0f)
		{
			_shakeLeft -= (float)delta;
			float k = Mathf.Max(0f, _shakeLeft / _shakeTime) * _shakeStrength;
			Offset = new Vector2((float)GD.RandRange(-k, k), (float)GD.RandRange(-k, k));
		}
		else
		{
			Offset = Vector2.Zero;
		}
	}

	public void Follow(Node2D target)
	{
		_followNode = target;
	}

	/// <summary>Decaying random offset; a stronger shake overrides a weaker one in progress.</summary>
	public void Shake(float strength, float time)
	{
		if (_shakeLeft > 0f && strength < _shakeStrength * (_shakeLeft / _shakeTime))
			return;
		_shakeStrength = strength;
		_shakeTime = time;
		_shakeLeft = time;
	}
}
