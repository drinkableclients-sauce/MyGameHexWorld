using Godot;

public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; }

	[Export] public int GlobalSeed = 1337;
	
	// Celda activa en el planeta global
	public HexCell ActiveCell { get; set; }
	
	// ATK (Active Tile Key) actual cargada en la RAM
	public ActiveTileKey CurrentActiveTile { get; private set; }

	public override void _Ready()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		else
		{
			QueueFree();
		}
	}

	/// <summary>
	/// Orquesta el viaje desde el planeta global hacia un sector táctico local.
	/// Guarda el tile anterior, carga/crea la ATK del nuevo tile y cambia de escena.
	/// </summary>
	public void EnterTile(HexCell targetCell)
	{
		// 1. Guardar el estado del tile actual antes de abandonarlo (Persistencia segura)
		SaveCurrentActiveTile();

		// 2. Asignar la nueva celda activa
		ActiveCell = targetCell;

		// 3. Intentar cargar la ATK desde su archivo SVG individual en Resources/WorldData/
		CurrentActiveTile = SVGManager.LoadActiveTile(targetCell.Id);

		// 4. Si el archivo no existe (primera visita) o falló, creamos una ATK limpia
		if (CurrentActiveTile == null)
		{
			GD.Print($"[GameManager] Generando nueva ATK para el Tile #{targetCell.Id} desde su BK.");
			CurrentActiveTile = new ActiveTileKey
			{
				TileId = targetCell.Id,
				LastSaveTimestamp = (ulong)Time.GetUnixTimeFromSystem()
			};
			
			// Guardamos el SVG inicial de este tile
			SVGManager.SaveActiveTile(CurrentActiveTile);
		}

		// 5. Viajar a la escena del diorama táctico local
		GetTree().ChangeSceneToFile("res://Scenes/World/LocalSelector.tscn");
	}

	/// <summary>
	/// Serializa y guarda la ATK actual en su archivo SVG independiente.
	/// </summary>
	public void SaveCurrentActiveTile()
	{
		if (CurrentActiveTile != null)
		{
			CurrentActiveTile.LastSaveTimestamp = (ulong)Time.GetUnixTimeFromSystem();
			SVGManager.SaveActiveTile(CurrentActiveTile);
		}
	}

	// Opcional: Autoguardado al cerrar la aplicación
	public override void _Notification(int what)
	{
		if (what == NotificationWMCloseRequest)
		{
			SaveCurrentActiveTile();
		}
	}
}
