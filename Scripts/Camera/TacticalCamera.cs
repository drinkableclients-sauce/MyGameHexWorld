using Godot;

public partial class TacticalCamera : Camera3D
{
	[Export] public float MoveSpeed = 35.0f;
	[Export] public float ZoomSpeed = 10.0f;
	[Export] public float RotationSpeed = 2.0f;
	[Export] public float MouseSensitivity = 0.2f;

	[Export] public float MinZoom = 10.0f;
	[Export] public float MaxZoom = 120.0f;

	private Vector2[] _mapPolygonVertices;
	private float _currentZoom = 45.0f;
	private bool _isRotatingWithMouse = false;

	public override void _Ready()
	{
		Position = new Vector3(0, _currentZoom, _currentZoom * 0.75f);
		LookAt(Vector3.Zero, Vector3.Up);
	}

	public void SetMapBoundaries(Vector2[] polygonVertices)
	{
		_mapPolygonVertices = polygonVertices;
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
				else if (mouseButton.ButtonIndex == MouseButton.Right)
				{
					_isRotatingWithMouse = true;
				}
			}
			else if (mouseButton.ButtonIndex == MouseButton.Right)
			{
				_isRotatingWithMouse = false;
			}
		}

		// Control con el mouse para inclinar (ver arriba/abajo) y rotar manteniendo el clic derecho
		if (_isRotatingWithMouse && @event is InputEventMouseMotion mouseMotion)
		{
			Vector3 rot = RotationDegrees;
			// Inclinar verticalmente (ver arriba y abajo) limitado entre -80° y -10°
			rot.X = Mathf.Clamp(rot.X - mouseMotion.Relative.Y * MouseSensitivity, -80.0f, -10.0f);
			// Rotar horizontalmente con el mouse si se desea
			rot.Y -= mouseMotion.Relative.X * MouseSensitivity;
			RotationDegrees = rot;
		}
	}

	public override void _Process(double delta)
	{
		// 1. ROTACIÓN SOBRE SU PROPIO EJE CON Q Y E
		float rotationInput = 0.0f;
		if (Input.IsKeyPressed(Key.Q)) rotationInput -= 1.0f;
		if (Input.IsKeyPressed(Key.E)) rotationInput += 1.0f;

		if (rotationInput != 0.0f)
		{
			RotateY(rotationInput * RotationSpeed * (float)delta);
		}

		// 2. MOVIMIENTO RELATIVO A LA VISTA DE LA CÁMARA
		Vector3 direction = Vector3.Zero;
		if (Input.IsKeyPressed(Key.W)) direction.Z -= 1; // Adelante según la cámara
		if (Input.IsKeyPressed(Key.S)) direction.Z += 1; // Atrás según la cámara
		if (Input.IsKeyPressed(Key.A)) direction.X -= 1; // Izquierda según la cámara
		if (Input.IsKeyPressed(Key.D)) direction.X += 1; // Derecha según la cámara

		if (direction != Vector3.Zero)
		{
			direction = direction.Normalized();
			
			// Obtenemos los vectores Forward y Right directamente de la base de la cámara en el plano XZ
			Vector3 forward = -GlobalTransform.Basis.Z;
			Vector3 right = GlobalTransform.Basis.X;
			forward.Y = 0; right.Y = 0;
			forward = forward.Normalized(); right = right.Normalized();

			Vector3 moveVector = (right * direction.X + forward * direction.Z) * MoveSpeed * (float)delta;
			Translate(moveVector);

			// Restricción estricta dentro del polígono real
			if (_mapPolygonVertices != null && _mapPolygonVertices.Length >= 3)
			{
				Vector3 currentPos = Position;
				Vector2 currentPos2D = new Vector2(currentPos.X, currentPos.Z);

				Vector2 constrained2D = PolygonBoundaryConstraint.ConstrainPosition(currentPos2D, _mapPolygonVertices);
				Position = new Vector3(constrained2D.X, currentPos.Y, constrained2D.Y);
			}
		}

		// Suavizado del Zoom en el eje Y
		Vector3 targetPos = Position;
		targetPos.Y = Mathf.Lerp(targetPos.Y, _currentZoom, 10.0f * (float)delta);
		Position = targetPos;
	}
}
