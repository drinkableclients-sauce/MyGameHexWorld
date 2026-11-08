using Godot;
using System.Collections.Generic;

public class CellGeometryProvider
{
	private readonly HexCell _cell;
	private readonly float _planetRadius;
	private readonly float _expansionFactor;
	
	private Vector2[] _cachedLocalPolygon;

	public CellGeometryProvider(HexCell cell, float planetRadius, float expansionFactor = 120.0f)
	{
		_cell = cell;
		_planetRadius = planetRadius;
		_expansionFactor = expansionFactor;
	}

	/// <summary>
	/// Obtiene o genera el contorno poligonal 2D local de la celda de forma limpia y cacheada.
	/// </summary>
	public Vector2[] GetLocalPolygon()
	{
		if (_cachedLocalPolygon != null) return _cachedLocalPolygon;

		if (_cell == null || _cell.TileEdges == null || _cell.TileEdges.Count == 0)
		{
			return new Vector2[0];
		}

		List<Vector2> points = new List<Vector2>();

		foreach (var edge in _cell.TileEdges)
		{
			// Usamos nuestro WorldSpaceMapper centralizado para evitar dispersión matemática
			Vector2 p1 = WorldSpaceMapper.ProjectToLocal(edge[0], _cell.Normal, _planetRadius, _expansionFactor);
			Vector2 p2 = WorldSpaceMapper.ProjectToLocal(edge[1], _cell.Normal, _planetRadius, _expansionFactor);

			if (!points.Contains(p1)) points.Add(p1);
			if (!points.Contains(p2)) points.Add(p2);
		}

		// Opcional: ordenar los puntos en sentido horario/antihorario si es necesario, 
		// o dejar el orden natural de las aristas del generador geodésico.
		_cachedLocalPolygon = points.ToArray();
		return _cachedLocalPolygon;
	}

	/// <summary>
	/// Calcula el radio máximo (distancia desde el centro 0,0 hasta el vértice más lejano).
	/// </summary>
	public float GetMaxRadius()
	{
		var polygon = GetLocalPolygon();
		float maxDist = 0.0f;

		foreach (var pt in polygon)
		{
			float dist = pt.Length();
			if (dist > maxDist) maxDist = dist;
		}

		return maxDist > 0 ? maxDist : 60.0f;
	}
}
