using Godot;

public partial class LocalTacticalCamera : Node3D
{
	[Export] public float MaxBoundaryRadius = 620.0f; // Límite estricto dentro del diorama de 680m
	[Export] public float MoveSpeed = 450.0f;
	[Export] public float RotationSensitivity = 0.005f;
	[Export] public float ZoomSpeed = 65.0f;

	// Límites de seguridad para que la cámara NUNCA llegue hasta el suelo
	[Export] public float MinZoomDistance = 180.0f;
	[Export] public float MaxZoomDistance = 1250.0f;
	[Export] public float MinPitchDegrees = 25.0f; // Ángulo mínimo sobre el horizonte (no baja al ras)
	[Export] public float MaxPitchDegrees = 80.0f;

	private Camera3D _camera;
	private float _currentYaw = 0.0f;
	private float _currentPitch = 45.0f;
	private float _currentDistance = 850.0f;
	private bool _isRightDragging = false;

	public override void _Ready()
	{
		_camera = GetNodeOrNull<Camera3D>("Camera3D");
		if (_camera == null)
		{
			_camera = GetViewport().GetCamera3D();
		}

		Position = new Vector3(0, 20.0f, 0); // Pivote ligeramente elevado sobre el nivel base
		UpdateCameraTransform();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		HandleKeyboardMovement(dt);
	}

	private void HandleKeyboardMovement(float dt)
	{
		Vector2 inputDir = Vector2.Zero;
		if (Input.IsKeyPressed(Key.W)) inputDir.Y -= 1;
		if (Input.IsKeyPressed(Key.S)) inputDir.Y += 1;
		if (Input.IsKeyPressed(Key.A)) inputDir.X -= 1;
		if (Input.IsKeyPressed(Key.D)) inputDir.X += 1;

		if (inputDir.LengthSquared() > 0.001f)
		{
			inputDir = inputDir.Normalized();

			// Calcular vectores adelante y derecha en el plano horizontal según el giro actual (Yaw)
			Vector3 forward = new Vector3(Mathf.Sin(_currentYaw), 0, Mathf.Cos(_currentYaw));
			Vector3 right = new Vector3(Mathf.Cos(_currentYaw), 0, -Mathf.Sin(_currentYaw));

			Vector3 moveDelta = (forward * inputDir.Y + right * inputDir.X) * MoveSpeed * dt;
			Vector3 nextPos = Position + moveDelta;

			// Restringir estrictamente dentro del radio circular/poligonal del diorama
			Vector2 horizontalPos = new Vector2(nextPos.X, nextPos.Z);
			if (horizontalPos.Length() > MaxBoundaryRadius)
			{
				horizontalPos = horizontalPos.Normalized() * MaxBoundaryRadius;
				nextPos.X = horizontalPos.X;
				nextPos.Z = horizontalPos.Y;
			}

			Position = nextPos;
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb)
		{
			if (mb.ButtonIndex == MouseButton.Right)
			{
				_isRightDragging = mb.Pressed;
			}
			else if (mb.Pressed && mb.ButtonIndex == MouseButton.WheelUp)
			{
				_currentDistance = Mathf.Clamp(_currentDistance - ZoomSpeed, MinZoomDistance, MaxZoomDistance);
				UpdateCameraTransform();
			}
			else if (mb.Pressed && mb.ButtonIndex == MouseButton.WheelDown)
			{
				_currentDistance = Mathf.Clamp(_currentDistance + ZoomSpeed, MinZoomDistance, MaxZoomDistance);
				UpdateCameraTransform();
			}
		}
		else if (@event is InputEventMouseMotion motion && _isRightDragging)
		{
			_currentYaw -= motion.Relative.X * RotationSensitivity;
			_currentPitch = Mathf.Clamp(
				_currentPitch + motion.Relative.Y * (RotationSensitivity * 50.0f),
				MinPitchDegrees,
				MaxPitchDegrees
			);
			UpdateCameraTransform();
		}
	}

	private void UpdateCameraTransform()
	{
		if (_camera == null) return;

		float pitchRad = Mathf.DegToRad(_currentPitch);
		float horizontalDist = _currentDistance * Mathf.Cos(pitchRad);
		float verticalHeight = _currentDistance * Mathf.Sin(pitchRad);

		// Calcular posición orbital relativa al pivote, garantizando altura positiva segura
		Vector3 offset = new Vector3(
			Mathf.Sin(_currentYaw) * horizontalDist,
			Mathf.Max(120.0f, verticalHeight), // Nunca baja de 120m sobre el diorama
			Mathf.Cos(_currentYaw) * horizontalDist
		);

		_camera.Position = offset;
		_camera.LookAt(GlobalPosition, Vector3.Up);
	}
}
