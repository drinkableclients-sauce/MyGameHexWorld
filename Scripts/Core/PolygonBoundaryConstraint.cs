using Godot;

public static class PolygonBoundaryConstraint
{
	/// <summary>
	/// Restringe una posición 2D (X, Z) para que permanezca estrictamente dentro del polígono convexo.
	/// Si está fuera, la proyecta de vuelta sobre el perímetro del polígono.
	/// </summary>
	public static Vector2 ConstrainPosition(Vector2 currentPos, Vector2[] polygonVertices)
	{
		if (polygonVertices == null || polygonVertices.Length < 3)
			return currentPos;

		// 1. Verificar si el punto ya está dentro del polígono (Ray Casting / Winding Number o Semiplanos)
		if (IsPointInsidePolygon(currentPos, polygonVertices))
		{
			return currentPos;
		}

		// 2. Si está fuera, encontrar el punto más cercano sobre el perímetro de las aristas del polígono
		Vector2 closestPoint = currentPos;
		float minDstSq = float.MaxValue;

		for (int i = 0; i < polygonVertices.Length; i++)
		{
			Vector2 a = polygonVertices[i];
			Vector2 b = polygonVertices[(i + 1) % polygonVertices.Length]; // Siguiente vértice

			Vector2 projected = GetClosestPointOnSegment(currentPos, a, b);
			float dstSq = currentPos.DistanceSquaredTo(projected);

			if (dstSq < minDstSq)
			{
				minDstSq = dstSq;
				closestPoint = projected;
			}
		}

		return closestPoint;
	}

	private static bool IsPointInsidePolygon(Vector2 p, Vector2[] polygon)
	{
		bool inside = false;
		int j = polygon.Length - 1;

		for (int i = 0; i < polygon.Length; i++)
		{
			if ((polygon[i].Y > p.Y) != (polygon[j].Y > p.Y) &&
				(p.X < (polygon[j].X - polygon[i].X) * (p.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X))
			{
				inside = !inside;
			}
			j = i;
		}

		return inside;
	}

	private static Vector2 GetClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
	{
		Vector2 ab = b - a;
		float t = (p - a).Dot(ab) / ab.Dot(ab);
		t = Mathf.Clamp(t, 0.0f, 1.0f);
		return a + ab * t;
	}
}
