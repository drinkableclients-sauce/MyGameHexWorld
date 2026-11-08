using Godot;
using System.Collections.Generic;

public class ActiveTileKey
{
	public int TileId;
	public ulong LastSaveTimestamp;
	
	// Listas de estado vivo (ej. posiciones de árboles talados, enemigos vivos, estructuras)
	public List<Vector3> ActiveResources = new List<Vector3>();
	public List<Vector3> ActiveThreats = new List<Vector3>();
	public Dictionary<string, string> CustomMetaData = new Dictionary<string, string>();
}
