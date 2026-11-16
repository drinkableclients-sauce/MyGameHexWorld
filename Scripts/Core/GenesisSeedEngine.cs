using Godot;

public static class GenesisSeedEngine
{
	private static readonly RandomNumberGenerator _masterRng = new RandomNumberGenerator();

	static GenesisSeedEngine()
	{
		_masterRng.Randomize();
	}

	public static int GenerateSafeSeed()
	{
		return (int)_masterRng.RandiRange(100, 2147483647);
	}

	/// <summary>
	/// Parsea texto o números introducidos por el usuario de forma segura.
	/// </summary>
	public static int ParseCustomSeed(string input)
	{
		if (string.IsNullOrWhiteSpace(input))
		{
			return GenerateSafeSeed();
		}

		// Si el usuario introduce un número directamente por teclado, lo usamos de forma limpia
		if (int.TryParse(input, out int directNumber))
		{
			int absVal = Mathf.Abs(directNumber);
			return absVal == 0 ? 847291 : absVal;
		}

		// Si introduce texto (ej. nombres), calculamos un hash determinista
		int hash = 23;
		foreach (char c in input)
		{
			hash = hash * 31 + c;
		}

		int safeSeed = Mathf.Abs(hash);
		return safeSeed == 0 ? 847291 : safeSeed;
	}

	public static int RollDiceSeed()
	{
		int diceAccumulator = 0;
		for (int i = 0; i < 6; i++)
		{
			int roll = (int)_masterRng.RandiRange(1, 6);
			diceAccumulator = (diceAccumulator * 10) + roll;
		}
		
		return 100000 + (diceAccumulator * 73856) % 2000000000;
	}
}
