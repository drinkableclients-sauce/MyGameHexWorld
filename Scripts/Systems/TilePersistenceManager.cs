using Godot;
using Godot.Collections;
using System.IO;

public partial class TilePersistenceManager : Node
{
	public static TilePersistenceManager Instance { get; private set; }

	private string SaveDirectory => "user://WorldSaves/Tiles/";

	public override void _Ready()
	{
		if (Instance != null && Instance != this)
		{
			QueueFree();
			return;
		}
		Instance = this;

		// Asegurar que el directorio de guardado de los tiles exista
		var dir = DirAccess.Open("user://");
		if (!dir.DirExists("WorldSaves/Tiles"))
		{
			dir.MakeDirRecursive("WorldSaves/Tiles");
		}
	}

	/// <summary>
	/// Comprueba si ya existe un Acta de Tile (ATK) guardada para una celda específica.
	/// </summary>
	public bool TileDataExists(int tileId)
	{
		string filePath = ProjectSettings.GlobalizePath($"{SaveDirectory}tile_{tileId}.json");
		return File.Exists(filePath);
	}

	/// <summary>
	/// Guarda el estado actual del tile de forma permanente (Write-Once / Snapshot).
	/// </summary>
	public void SaveTileATK(int tileId, Dictionary tileData)
	{
		string filePath = ProjectSettings.GlobalizePath($"{SaveDirectory}tile_{tileId}.json");
		string jsonString = Json.Stringify(tileData, "\t");
		
		File.WriteAllText(filePath, jsonString);
		GD.Print($"[TilePersistenceManager] ATK del Tile #{tileId} guardada exitosamente en disco.");
	}

	/// <summary>
	/// Carga el estado persistente del tile si ya fue visitado previamente.
	/// </summary>
	public Dictionary LoadTileATK(int tileId)
	{
		string filePath = ProjectSettings.GlobalizePath($"{SaveDirectory}tile_{tileId}.json");
		
		if (File.Exists(filePath))
		{
			string jsonString = File.ReadAllText(filePath);
			var json = new Json();
			var parseResult = json.Parse(jsonString);

			if (parseResult == Error.Ok)
			{
				GD.Print($"[TilePersistenceManager] ATK del Tile #{tileId} cargada desde disco (Estado persistente recuperado).");
				return (Dictionary)json.Data;
			}
			else
			{
				GD.PrintErr($"[TilePersistenceManager] Error al parsear el ATK del Tile #{tileId}: {json.GetErrorMessage()}");
			}
		}

		return null;
	}
}
