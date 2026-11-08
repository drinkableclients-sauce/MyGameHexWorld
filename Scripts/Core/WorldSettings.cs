using Godot;

public partial class WorldSettings : Node
{
	// Patrón Singleton accesible globalmente
	public static WorldSettings Instance { get; private set; }

	[Export] public long GlobalSeed = 1337;
	[Export] public float SeaLevelThreshold = 0.42f;
	[Export] public float RoughnessMultiplier = 1.0f;

	public override void _Ready()
	{
		if (Instance != null && Instance != this)
		{
			QueueFree();
			return;
		}
		Instance = this;
		
		// Opcional: Asegurarnos de que persista al cambiar de escena si se desea como Autoload,
		// o dejarlo como nodo hijo en la escena inicial.
		GD.Print($"[WorldSettings] Inicializado con Semilla: {GlobalSeed}, Nivel de Agua: {SeaLevelThreshold}, Rugosidad: {RoughnessMultiplier}");
	}

	/// <summary>
	/// Configura los parámetros globales elegidos por el usuario antes de generar el planeta.
	/// </summary>
	public void SetWorldParameters(long seed, float seaLevel, float roughness)
	{
		GlobalSeed = seed;
		SeaLevelThreshold = Mathf.Clamp(seaLevel, 0.1f, 0.9f);
		RoughnessMultiplier = Mathf.Clamp(roughness, 0.1f, 5.0f);
		
		GD.Print($"[WorldSettings] Parámetros actualizados -> Semilla: {GlobalSeed} | Nivel del Mar: {SeaLevelThreshold} | Rugosidad: {RoughnessMultiplier}");
	}
}
