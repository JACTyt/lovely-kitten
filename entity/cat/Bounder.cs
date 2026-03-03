using Godot;
using System;

public class Bounder
{
	[Export] private Vector2 _maxBound;
	[Export] private Vector2 _minBound;

	public Bounder(Vector2 minBound, Vector2 maxBound)
	{
		_minBound = minBound;
		_maxBound = maxBound;
	}

	public Vector2 Bound(Vector2 position)
	{
		// Only X for now
		float posX = Mathf.Wrap(position.X, _minBound.X, _maxBound.X);
		position.X = posX;
		return position;
	}
}
