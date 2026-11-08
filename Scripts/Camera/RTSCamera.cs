using Godot;

public partial class RTSCamera : Node3D
{
	[Export] private float mouseSensitivity = 0.003f;
	[Export] private float zoomSpeed = 2.0f;
	[Export] private float minZoom = 2.0f;
	[Export] private float maxZoom = 40.0f;

	private Camera3D _camera;

	public override void _Ready()
	{
		_camera = GetNode<Camera3D>("Camera3D");
	}

	// Usamos _Input en lugar de _UnhandledInput para capturar el ratón directamente
	public override void _Input(InputEvent @event)
	{
		// Rotar la cámara al arrastrar el mouse con el botón derecho presionado
		if (@event is InputEventMouseMotion mouseMotion && Input.IsMouseButtonPressed(MouseButton.Right))
		{
			Rotation = new Vector3(
				Rotation.X,
				Rotation.Y - mouseMotion.Relative.X * mouseSensitivity,
				Rotation.Z
			);

			float newX = Rotation.X - mouseMotion.Relative.Y * mouseSensitivity;
Rotation = new Vector3(
	Mathf.Clamp(newX, Mathf.DegToRad(-89), Mathf.DegToRad(89)),
	Rotation.Y,
	Rotation.Z
);
		}

		// Zoom con la rueda del ratón
		if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
		{
			if (_camera == null) return;

			Vector3 currentPos = _camera.Position;

			if (mouseButton.ButtonIndex == MouseButton.WheelUp)
			{
				float newZ = Mathf.Max(currentPos.Z - zoomSpeed, minZoom);
				_camera.Position = new Vector3(currentPos.X, currentPos.Y, newZ);
				GD.Print("Zoom In detectado: " + newZ);
			}

			if (mouseButton.ButtonIndex == MouseButton.WheelDown)
			{
				float newZ = Mathf.Min(currentPos.Z + zoomSpeed, maxZoom);
				_camera.Position = new Vector3(currentPos.X, currentPos.Y, newZ);
				GD.Print("Zoom Out detectado: " + newZ);
			}
		}
	}
}
