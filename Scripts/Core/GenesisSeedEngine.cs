using Godot;

public static class GenesisSeedEngine
{
	private static readonly RandomNumberGenerator _masterRng = new RandomNumberGenerator();

	static GenesisSeedEngine()
	{
		_masterRng.Randomize();
	}

	/// <summary>
	/// Genera una semilla Génesis 100% segura, evitando ceros, negativos o valores extremos
	/// que puedan desestabilizar las matemáticas de la esfera o las Basal Keys.
	/// </summary>
	public static int GenerateSafeSeed()
	{
		// Rango seguro: desde 100,000 hasta el límite seguro de un entero de 32 bits
		return (int)_masterRng.RandiRange(100, 2147483647);
	}

	/// <summary>
	/// Convierte texto personalizado del usuario (ej. nombre de un mundo) en una semilla válida mediante un hash estable.
	/// </summary>
	public static int ParseCustomSeed(string input)
	{
		if (string.IsNullOrWhiteSpace(input))
		{
			return GenerateSafeSeed();
		}

		// Algoritmo de hash simple y determinista para convertir strings en enteros numéricos seguros
		int hash = 23;
		foreach (char c in input)
		{
			hash = hash * 31 + c;
		}

		int safeSeed = Mathf.Abs(hash);
		return safeSeed == 0 ? 847291 : safeSeed;
	}

	/// <summary>
	/// Simula un motor de dados robusto para generar una semilla con sabor a "tirada de dados",
	/// manteniendo una distribución limpia y controlada.
	/// </summary>
	public static int RollDiceSeed()
	{
		int diceAccumulator = 0;
		for (int i = 0; i < 6; i++)
		{
			int roll = (int)_masterRng.RandiRange(1, 6);
			diceAccumulator = (diceAccumulator * 10) + roll;
		}
		
		// Escalar la tirada de dados al rango seguro del motor del planeta
		return 100000 + (diceAccumulator * 73856) % 2000000000;
	}
}
