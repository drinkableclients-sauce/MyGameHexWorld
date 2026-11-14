using Godot;

public partial class MainMenu : Control
{
	private Button _newWorldButton;

	public override void _Ready()
	{
		// Buscar el botón automáticamente en cualquier nivel de los hijos (evita errores de rutas exactas)
		_newWorldButton = FindChild("NewWorld", true, false) as Button;
		
		if (_newWorldButton != null)
		{
			_newWorldButton.Pressed += OnNewWorldBtnPressed;
			GD.Print("[MainMenu] Botón 'Nuevo Mundo' localizado y enlazado con éxito.");
		}
		else
		{
			GD.PrintErr("[MainMenu] ADVERTENCIA: No se encontró ningún nodo llamado 'NewWorld' en la escena. Revisa el nombre del botón en el árbol de nodos.");
		}
	}

	private void OnNewWorldBtnPressed()
	{
		// 1. Generar semilla segura con el motor de dados
		int finalSeed = GenesisSeedEngine.RollDiceSeed();

		// 2. Inyectar en el GameManager global
		if (GameManager.Instance != null)
		{
			GameManager.Instance.GenesisSeed = finalSeed;
			GameManager.Instance.CurrentWorldConfig.ApplyDifficultyModifiers();
			GD.Print($"[MainMenu] ¡Clic en Nuevo Mundo! Semilla asignada: {finalSeed}");
		}
		else
		{
			GD.PrintErr("[MainMenu] ERROR CRÍTICO: GameManager.Instance es null.");
			return;
		}

		// 3. Saltar a la escena del Planeta Global
		Error err = GetTree().ChangeSceneToFile("res://Scenes/Global/Planet.tscn");
		if (err != Error.Ok)
		{
			GD.PrintErr($"[MainMenu] Error al intentar cambiar a la escena Planet.tscn: {err}");
		}
	}
}
