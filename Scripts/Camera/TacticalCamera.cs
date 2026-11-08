using Godot;

public partial class TacticalCamera : Camera3D
{
	[Export] public float MoveSpeed = 35.0f;
	[Export] public float ZoomSpeed = 10.0f;
	[Export] public float RotationSpeed = 2.0f;

	[Export] public float MinZoom = 10.0f;
	[Export] public float MaxZoom = 120.0f;

	public float MapLimit { get; set; } = 50.0f;

	private float _currentZoom = 45.0f;
	private bool _isRotating = false;

	public override void _Ready()
	{
		Position = new Vector3(0, _currentZoom, _currentZoom * 0.75f);
		LookAt(Vector3.Zero, Vector3.Up);
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton)
		{
			if (mouseButton.Pressed)
			{
				if (mouseButton.ButtonIndex == MouseButton.WheelUp)
				{
					_currentZoom = Mathf.Clamp(_currentZoom - ZoomSpeed, MinZoom, MaxZoom);
				}
				else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
				{
					_currentZoom = Mathf.Clamp(_currentZoom + ZoomSpeed, MinZoom, MaxZoom);
				}

				if (mouseButton.ButtonIndex == MouseButton.Right)
				{
					_isRotating = true;
				}
			}
			else if (mouseButton.ButtonIndex == MouseButton.Right)
			{
				_isRotating = false;
			}
		}

		if (_isRotating && @event is InputEventMouseMotion mouseMotion)
		{
			Vector3 rot = RotationDegrees;
			rot.Y -= mouseMotion.Relative.X * RotationSpeed * 0.1f;
			rot.X = Mathf.Clamp(rot.X - mouseMotion.Relative.Y * RotationSpeed * 0.1f, -80.0f, -10.0f);
			RotationDegrees = rot;
		}
	}

	public override void _Process(double delta)
	{
		Vector3 direction = Vector3.Zero;
		if (Input.IsActionPressed("ui_right") || Input.IsKeyPressed(Key.D)) direction.X += 1;
		if (Input.IsActionPressed("ui_left") || Input.IsKeyPressed(Key.A)) direction.X -= 1;
		if (Input.IsActionPressed("ui_down") || Input.IsKeyPressed(Key.S)) direction.Z += 1;
		if (Input.IsActionPressed("ui_up") || Input.IsKeyPressed(Key.W)) direction.Z -= 1;

		if (direction != Vector3.Zero)
		{
			direction = direction.Normalized();
			Vector3 forward = GlobalTransform.Basis.Z;
			Vector3 right = GlobalTransform.Basis.X;
			forward.Y = 0; right.Y = 0;
			forward = forward.Normalized(); right = right.Normalized();

			Vector3 moveVector = (right * direction.X + forward * direction.Z) * MoveSpeed * (float)delta;
			Translate(moveVector); // Movimiento local puro

			// Restricción basada en la posición local XZ respecto al centro (0,0)
			Vector3 currentPos = Position;
			float distanceFromCenter = new Vector2(currentPos.X, currentPos.Z).Length();

			if (distanceFromCenter > MapLimit)
			{
				Vector2 clamped = new Vector2(currentPos.X, currentPos.Z).Normalized() * MapLimit;
				Position = new Vector3(clamped.X, currentPos.Y, clamped.Y);
			}
		}

		Vector3 targetPos = Position;
		targetPos.Y = Mathf.Lerp(targetPos.Y, _currentZoom, 10.0f * (float)delta);
		Position = targetPos;
	}
}
