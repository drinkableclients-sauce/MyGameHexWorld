using Godot;

public partial class PlanetCamera : Camera3D
{
	[Export] public Node3D Target; // Arrastra aquí tu nodo PlanetGenerator o déjalo en el centro (Vector3.Zero)
	[Export] public float MouseSensitivity = 0.005f;
	[Export] public float ZoomSpeed = 2.0f;
	[Export] public float MinZoom = 30.0f;
	[Export] public float MaxZoom = 100.0f;

	private float _distance = 25.0f;
	private float _angleX = 0.0f;
	private float _angleY = 0.0f;
	private bool _isRotating = false;

	public override void _Ready()
	{
		if (Target == null)
		{
			// Si no hay target asignado, asume que el centro del mundo es Vector3.Zero
			GetNodeOrNull<Node3D>("../PlanetGenerator");
		}
		UpdateCameraTransform();
	}

	public override void _Input(InputEvent @event)
	{
		// Detectar clic derecho o izquierdo para arrastrar y rotar la vista
		if (@event is InputEventMouseButton mouseButton)
		{
			if (mouseButton.ButtonIndex == MouseButton.Left || mouseButton.ButtonIndex == MouseButton.Right)
			{
				_isRotating = mouseButton.Pressed;
			}
			else if (mouseButton.ButtonIndex == MouseButton.WheelUp && mouseButton.Pressed)
			{
				_distance = Mathf.Clamp(_distance - ZoomSpeed, MinZoom, MaxZoom);
				UpdateCameraTransform();
			}
			else if (mouseButton.ButtonIndex == MouseButton.WheelDown && mouseButton.Pressed)
			{
				_distance = Mathf.Clamp(_distance + ZoomSpeed, MinZoom, MaxZoom);
				UpdateCameraTransform();
			}
		}

		if (@event is InputEventMouseMotion mouseMotion && _isRotating)
		{
			_angleX -= mouseMotion.Relative.X * MouseSensitivity;
			_angleY = Mathf.Clamp(_angleY - mouseMotion.Relative.Y * MouseSensitivity, -Mathf.Pi / 2.2f, Mathf.Pi / 2.2f);
			UpdateCameraTransform();
		}
	}

	private void UpdateCameraTransform()
	{
		Vector3 targetPos = Target != null ? Target.GlobalPosition : Vector3.Zero;

		// Calcular nueva posición orbital usando trigonometría esférica
		float x = targetPos.X + _distance * Mathf.Cos(_angleY) * Mathf.Sin(_angleX);
		float y = targetPos.Y + _distance * Mathf.Sin(_angleY);
		float z = targetPos.Z + _distance * Mathf.Cos(_angleY) * Mathf.Cos(_angleX);

		GlobalPosition = new Vector3(x, y, z);
		LookAt(targetPos, Vector3.Up);
	}
}
