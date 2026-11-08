using Godot;
using System.Collections.Generic;

public static class PolygonalMeshBuilder
{
	private static void GetLocalCoords(Vector3 sphericalPoint, HexCell cell, float planetRadius, float scale, out float x, out float z)
	{
		Vector3 scaledPoint = sphericalPoint * planetRadius;
		Vector3 scaledCenter = cell.Normal * planetRadius;

		Vector3 C = cell.Normal;
		Vector3 v = scaledPoint - scaledCenter;
		Vector3 tangentV = v - C * v.Dot(C);

		Vector3 T1 = C.Cross(Vector3.Up);
		if (T1.LengthSquared() < 0.0001f) T1 = C.Cross(Vector3.Forward);
		T1 = T1.Normalized();
		Vector3 T2 = C.Cross(T1).Normalized();

		float expansionFactor = 120.0f * scale;
		x = tangentV.Dot(T1) * expansionFactor;
		z = tangentV.Dot(T2) * expansionFactor;
	}

	public static Mesh BuildTerrainMesh(HexCell cell, float planetRadius, float scale, FastNoiseLite noise, out Vector3 centerPosition, out float outMaxRadius)
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		centerPosition = Vector3.Zero;
		outMaxRadius = 1.0f;
		if (cell.TileEdges == null || cell.TileEdges.Count == 0) return st.Commit();

		Vector3 localCenter = Vector3.Zero;
		float centerHeight = GetHeight(localCenter, cell, noise);
		localCenter.Y = centerHeight;
		centerPosition = localCenter;

		float maxDist = 0.0f;
		st.SetColor(new Color(0.85f, 0.80f, 0.68f));

		foreach (var edge in cell.TileEdges)
		{
			GetLocalCoords(edge[0], cell, planetRadius, scale, out float x1, out float z1);
			GetLocalCoords(edge[1], cell, planetRadius, scale, out float x2, out float z2);

			float d1 = new Vector2(x1, z1).Length();
			float d2 = new Vector2(x2, z2).Length();
			if (d1 > maxDist) maxDist = d1;
			if (d2 > maxDist) maxDist = d2;

			Vector3 p1 = new Vector3(x1, 0, z1);
			Vector3 p2 = new Vector3(x2, 0, z2);

			p1.Y = GetHeight(p1, cell, noise);
			p2.Y = GetHeight(p2, cell, noise);

			// Tapa Superior (Terreno con relieve marcado)
			st.AddVertex(localCenter);
			st.AddVertex(p1);
			st.AddVertex(p2);

			// Paredes Laterales del Pilar
			float wallBottom = -40.0f;
			Vector3 p1_down = new Vector3(p1.X, wallBottom, p1.Z);
			Vector3 p2_down = new Vector3(p2.X, wallBottom, p2.Z);

			st.AddVertex(p1);
			st.AddVertex(p1_down);
			st.AddVertex(p2_down);

			st.AddVertex(p1);
			st.AddVertex(p2_down);
			st.AddVertex(p2);
		}

		outMaxRadius = maxDist > 0 ? maxDist : 80.0f;
		return st.Commit();
	}

	public static Mesh BuildWaterMesh(HexCell cell, float planetRadius, float scale, float waterLevel)
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		// Generamos agua si el bioma o las conexiones indican presencia de agua
		bool hasWater = cell.Elevation <= 0.42f || cell.HasSeaConnection || cell.Biome.Contains("Océano") || cell.Biome.Contains("Costa") || cell.Biome.Contains("Lago");
		if (!hasWater) return st.Commit();

		if (cell.TileEdges == null || cell.TileEdges.Count == 0) return st.Commit();

		Vector3 localCenter = new Vector3(0, waterLevel, 0);

		foreach (var edge in cell.TileEdges)
		{
			GetLocalCoords(edge[0], cell, planetRadius, scale, out float x1, out float z1);
			GetLocalCoords(edge[1], cell, planetRadius, scale, out float x2, out float z2);

			Vector3 p1 = new Vector3(x1, waterLevel, z1);
			Vector3 p2 = new Vector3(x2, waterLevel, z2);

			st.AddVertex(localCenter);
			st.AddVertex(p1);
			st.AddVertex(p2);
		}

		return st.Commit();
	}

	public static float GetHeight(Vector3 localPos, HexCell cell, FastNoiseLite noise)
	{
		if (noise == null) return 0.0f;
		// Incrementamos la amplitud y frecuencia para generar relieve montañoso/accidentado real
		float noiseVal = (noise.GetNoise2D(localPos.X * 0.03f, localPos.Z * 0.03f) + 1.0f) * 0.5f;
		
		// Base de elevación heredada directamente de la celda de la esfera
		float baseElevation = (cell.Elevation - 0.5f) * 45.0f; 
		
		// Relieve orgánico pronunciado
		return baseElevation + (Mathf.Pow(noiseVal, 1.4f) * 25.0f);
	}
}
