using Godot;

public static class WorldSpaceMapper
{
	/// <summary>
	/// Convierte un punto esférico global en coordenadas locales 2D (X, Z) del diorama,
	/// utilizando un plano tangente ortonormal estricto basado en la normal de la celda.
	/// </summary>
	public static Vector2 ProjectToLocal(Vector3 sphericalPoint, Vector3 cellNormal, float planetRadius, float expansionFactor)
	{
		// Aseguramos la escala real del planeta
		Vector3 scaledPoint = sphericalPoint * planetRadius;
		Vector3 scaledCenter = cellNormal * planetRadius;

		Vector3 v = scaledPoint - scaledCenter;
		Vector3 tangentV = v - cellNormal * v.Dot(cellNormal);

		// Generamos la base ortonormal local (Tangente y Bitangente)
		Vector3 T1 = cellNormal.Cross(Vector3.Up);
		if (T1.LengthSquared() < 0.0001f) 
			T1 = cellNormal.Cross(Vector3.Forward);
		T1 = T1.Normalized();
		
		Vector3 T2 = cellNormal.Cross(T1).Normalized();

		// Proyección escalar limpia multiplicada por el factor de expansión deseado
		float x = tangentV.Dot(T1) * expansionFactor;
		float z = tangentV.Dot(T2) * expansionFactor;

		return new Vector2(x, z);
	}
}
