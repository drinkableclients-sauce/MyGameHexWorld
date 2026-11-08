using Godot;
using System.Collections.Generic;

public class BasalKey
{
	public int Id;
	public Vector3 Position;
	public Vector3 Normal;
	public int TileSeed;
	public string Biome;
	public float Elevation;
	public bool HasSeaConnection;
	public float ArableArea;
	public int ThreatLevel;
	
	// Coordenadas exactas de los vértices de las caras (para construir el polígono real)
	public List<Vector3[]> TileEdges = new List<Vector3[]>();
}
