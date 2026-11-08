using Godot;
using System.IO;

public static class SVGManager
{
	private static string StoragePath = "res://Resources/WorldData/";

	public static void SaveActiveTile(ActiveTileKey atk)
	{
		if (!DirAccess.DirExistsAbsolute(StoragePath))
		{
			DirAccess.MakeDirAbsolute(StoragePath);
		}

		string filePath = $"{StoragePath}tile_{atk.TileId}.svg";
		
		string svgContent = $@"<svg xmlns=""http://www.w3.org/2000/svg"" width=""512"" height=""512"" viewBox=""0 0 512 512"">
  <metadata>
    <tile_id>{atk.TileId}</tile_id>
    <timestamp>{atk.LastSaveTimestamp}</timestamp>
  </metadata>
  <rect width=""100%"" height=""100%"" fill=""#123456"" />
  <text x=""20"" y=""50"" fill=""#ffffff"" font-size=""24"">ATK Tile #{atk.TileId}</text>
</svg>";

		File.WriteAllText(ProjectSettings.GlobalizePath(filePath), svgContent);
		GD.Print($"[SVGManager] Tile {atk.TileId} guardado exitosamente en SVG.");
	}

	public static ActiveTileKey LoadActiveTile(int tileId)
	{
		string filePath = $"{StoragePath}tile_{tileId}.svg";
		string globalPath = ProjectSettings.GlobalizePath(filePath);

		if (!File.Exists(globalPath))
		{
			GD.Print($"[SVGManager] No se encontró ATK previa para el tile {tileId}. Se generará una nueva.");
			return null; 
		}

		try
		{
			GD.Print($"[SVGManager] Cargando datos del Tile {tileId} desde su SVG.");
			ActiveTileKey atk = new ActiveTileKey { TileId = tileId };
			return atk;
		}
		catch (System.Exception e) // <-- CORREGIDO: Añadido catch y paréntesis
		{
			GD.PrintErr($"[SVGManager] Error al leer el archivo SVG del tile {tileId}: {e.Message}");
			return null; 
		}
	}
}
