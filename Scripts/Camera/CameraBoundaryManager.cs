using Godot;

public static class CameraBoundaryManager
{
	public static float CalculateBoundaryLimit(HexCell cell, float scale)
	{
		if (cell == null || cell.TileEdges == null || cell.TileEdges.Count == 0)
			return 40.0f;

		float maxDistance = 0.0f;
		Vector3 C = cell.Normal;
		Vector3 T1 = C.Cross(Vector3.Up);
		if (T1.LengthSquared() < 0.0001f) T1 = C.Cross(Vector3.Forward);
		T1 = T1.Normalized();
		Vector3 T2 = C.Cross(T1).Normalized();

		foreach (var edge in cell.TileEdges)
		{
			Vector3 v1 = edge[0] - C;
			Vector3 tangentV1 = v1 - C * v1.Dot(C);
			float d1 = new Vector2(tangentV1.Dot(T1), tangentV1.Dot(T2)).Length() * scale;

			Vector3 v2 = edge[1] - C;
			Vector3 tangentV2 = v2 - C * v2.Dot(C);
			float d2 = new Vector2(tangentV2.Dot(T1), tangentV2.Dot(T2)).Length() * scale;

			if (d1 > maxDistance) maxDistance = d1;
			if (d2 > maxDistance) maxDistance = d2;
		}

		// Radio límite ajustado para contener la cámara dentro del borde del polígono
		return maxDistance * 0.8f;
	}
}
