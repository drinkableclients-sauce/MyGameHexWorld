using Godot;

public partial class MainMenu : Control
{
	private Button _newWorldButton;

	public override void _Ready()
	{
		// Buscar el botón automáticamente en los hijos
		_newWorldButton = FindChild("NewWorld", true, false) as Button;
		
		if (_newWorldButton != null)
		{
			_newWorldButton.Pressed += OnNewWorldBtnPressed;
			GD.Print("[MainMenu] Botón 'Nuevo Mundo' localizado y enlazado con éxito.");
		}
		else
		{
			GD.PrintErr("[MainMenu] ADVERTENCIA: No se encontró ningún nodo llamado 'NewWorld' en la escena.");
		}
	}

	private void OnNewWorldBtnPressed()
	{
		GD.Print("[MainMenu] ¡Clic en Nuevo Mundo! Abriendo menú de configuración de partida...");

		// CORRECCIÓN DEL FLUJO: Abrir el menú de opciones/seed generator primero
		// (Asegúrate de que la ruta coincida con la ubicación real de tu escena SeedGenerator.tscn)
		Error err = GetTree().ChangeSceneToFile("res://Scenes/Global/SeedGeneratorMenu.tscn");
		
		if (err != Error.Ok)
		{
			GD.PrintErr($"[MainMenu] Error al intentar abrir SeedGenerator.tscn: {err}");
		}
	}
}
