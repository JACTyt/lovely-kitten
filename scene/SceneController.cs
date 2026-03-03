using Godot;

public partial class SceneController : Node2D
{
	public static SceneController Singleton;
	[Export] private CameraController _cameraController;
	[Export] private Vector2 _playAxis;
	
	[Export] private int _birds;
	private bool _gameStarted = true;
	private static readonly PackedScene PlayerCat = (PackedScene)GD.Load("res://entity/cat/cat.tscn");
	private static readonly PackedScene BirdCharacter = (PackedScene)GD.Load("res://entity/bird/bird.tscn");
	private Node2D[] _birdPool = new Node2D[5];
	private double _birdSpawnTimer = 0;
	private double _birdSpawnRate = 2f;

	public override void _Ready()
	{
		Singleton = this;
		// Instance the scene
		Node2D playerInstance = (Node2D)PlayerCat.Instantiate();

		// Add it to the scene tree
		AddChild(playerInstance);

		// Set position or other properties
		playerInstance.Position = _cameraController.Position + _playAxis;
		_cameraController.Follow(playerInstance);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (!_gameStarted)
			return;
		
		_birdSpawnTimer += delta;
		if (_birdSpawnTimer >= _birdSpawnRate)
		{
			_birdSpawnTimer = 0;
			SpawnBirds();
		}
	}


	private void SpawnBirds()
	{
		Node2D birdInstance = (Node2D)BirdCharacter.Instantiate();
		AddChild(birdInstance);
		birdInstance.Position = _cameraController.Position + _playAxis + _playAxis;
	}

	public void AddBirdValue(int i)
	{
		_birds += i;
	}

	public int GetBirdValue()
	{
		return _birds;
	}
}
