using Godot;

public partial class CameraController : Camera2D
{
	private Node2D _followNode;

	[Export] private Vector2 _maxCameraPosition;
	[Export] private Vector2 _minCameraPosition;

	public override void _Ready()
	{
	}

	public override void _Process(double delta)
	{
		if (_followNode == null) return;

		// Follow only on X-axis, keep current Y position
		float posX = Mathf.Clamp(_followNode.Position.X, _minCameraPosition.X, _maxCameraPosition.X);
		float posY = Mathf.Clamp(_followNode.Position.Y, _minCameraPosition.Y, _maxCameraPosition.Y);
		Position = new Vector2(posX, posY);
	}

	public void Follow(Node2D target)
	{
		_followNode = target;
	}
}
