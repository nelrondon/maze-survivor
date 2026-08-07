using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MazeSpawner : Node
{
	private Maze _maze;
	private readonly Random _random = new Random();
	private readonly HashSet<Vector2I> _occupiedPositions = new HashSet<Vector2I>();

	private SpectatorUI _spectatorUI;
	private int _spectateIndex = 0;
	private Player _currentlySpectatedPlayer = null;

	public override void _Ready()
	{
		_maze = GetParent<Maze>();
		if (_maze == null) GD.PrintErr("MazeSpawner: ¡No se encontró el nodo principal 'Maze'!");

		Multiplayer.PeerDisconnected += OnPeerDisconnected;
	}

	public override void _ExitTree()
	{
		Multiplayer.PeerDisconnected -= OnPeerDisconnected;
	}

	private void OnPeerDisconnected(long id)
	{
		var playerNode = _maze?.GetNodeOrNull(id.ToString());
		if (playerNode != null)
		{
			playerNode.QueueFree();
			GD.Print($"[MazeSpawner] Safely removed player node for disconnected peer {id}");
		}

		var localPlayerInfo = GameManager.Players.FirstOrDefault(p => p.Id == Multiplayer.GetUniqueId());
		if (localPlayerInfo != null && localPlayerInfo.IsSpectator)
		{
			CallDeferred(nameof(RefreshSpectatorTarget));
		}
	}

	private void RefreshSpectatorTarget()
	{
		SetSpectatedTargetIndex(_spectateIndex);
	}

	public void SpawnEntities()
	{
		if (_maze == null) return;

		_occupiedPositions.Clear();

		Vector2I bossSpawnPos = SpawnBoss();
		SpawnPlayer();
		SpawnPalo();
		SpawnKey(bossSpawnPos);   
		SpawnDoorOnWall();
		SpawnTraps();
	}

	private void SpawnTraps()
	{
		SpawnSpikeClusters();
		SpawnArrowClusters();
		SpawnCageTraps();
	}

	/// Trampas de pinchos (normales/venenosas) en fila, siguiendo el pasillo real
	/// (los corredores de este laberinto tienen 1 celda de ancho: nunca hay un
	/// área libre 3x3 salvo cerca de la sala central, así que exigir un bloque
	/// cuadrado dejaba los clusters casi siempre en zonas abiertas fáciles de
	/// rodear). Cada celda de la fila ya cubre casi todo el ancho del pasillo
	/// (ver spike_trap_example.tscn, hazard 5.6 de 6), así que una fila de N
	/// celdas consecutivas en un corredor de 1 de ancho no deja por dónde pasar.
	private void SpawnSpikeClusters()
	{
		var spikeScenes = new List<PackedScene>();
		if (_maze.SpikeTrapScene != null) spikeScenes.Add(_maze.SpikeTrapScene);
		if (_maze.PoisonSpikeTrapScene != null) spikeScenes.Add(_maze.PoisonSpikeTrapScene);
		if (spikeScenes.Count == 0) return;

		int largo = Math.Max(1, _maze.SpikeClusterSize);

		for (int c = 0; c < _maze.SpikeClusterCount; c++)
		{
			if (_random.NextDouble() > _maze.SpikeClusterChance) continue; // este intento no tuvo suerte

			var fila = ObtenerLineaLibre(largo);
			if (fila == null)
			{
				GD.Print($"No hay pasillo libre de {largo} celdas seguidas para otro cluster de pinchos, se omite.");
				continue;
			}

			foreach (var pos in fila)
			{
				var scene = spikeScenes[_random.Next(spikeScenes.Count)];
				var trap = scene.Instantiate<Node3D>();
				trap.Position = new Vector3(pos.X * _maze.GridScale, 0.0f, pos.Y * _maze.GridScale);
				_maze.AddChild(trap);
				_occupiedPositions.Add(pos);
			}
			GD.Print($"Fila de {fila.Count} pinchos colocada en pasillo, inicio {fila[0]}");
		}
	}

	/// Busca una fila de "largo" celdas libres y contiguas dentro de un mismo
	/// pasillo (horizontal o vertical, ya que los corredores de este laberinto
	/// son de 1 celda de ancho). No exige nada sobre las celdas perpendiculares:
	/// que haya pared a los costados es justamente lo normal en un pasillo y es
	/// lo que hace que la fila de pinchos sea infranqueable. Devuelve null si no
	/// encuentra ninguna fila que entre completa.
	private List<Vector2I> ObtenerLineaLibre(int largo)
	{
		var inicios = new List<(Vector2I pos, bool horizontal)>();

		bool FilaLibre(int x, int z, bool horizontal)
		{
			for (int i = 0; i < largo; i++)
			{
				var pos = horizontal ? new Vector2I(x + i, z) : new Vector2I(x, z + i);
				if (_maze.Map[pos.X, pos.Y] != 0 || _occupiedPositions.Contains(pos)) return false;
			}
			return true;
		}

		for (int x = 1; x < _maze.Width - 1; x++)
		{
			for (int z = 1; z < _maze.Height - 1; z++)
			{
				if (x + largo - 1 < _maze.Width - 1 && FilaLibre(x, z, true))
					inicios.Add((new Vector2I(x, z), true));

				if (z + largo - 1 < _maze.Height - 1 && FilaLibre(x, z, false))
					inicios.Add((new Vector2I(x, z), false));
			}
		}

		if (inicios.Count == 0) return null;

		var (inicio, horiz) = inicios[_random.Next(inicios.Count)];
		var fila = new List<Vector2I>();
		for (int i = 0; i < largo; i++)
			fila.Add(horiz ? new Vector2I(inicio.X + i, inicio.Y) : new Vector2I(inicio.X, inicio.Y + i));

		return fila;
	}

	/// Trampas de flechas: varias montadas una al lado de la otra en el MISMO tramo
	/// de pared (no una por celda de laberinto), formando una fila apretada de
	/// disparadores contiguos que entre todos cubren el ancho del pasillo.
	private void SpawnArrowClusters()
	{
		if (_maze.ArrowTrapScene == null) return;

		const float espaciado = 1.6f; // distancia entre disparadores del mismo grupo

		for (int c = 0; c < _maze.ArrowClusterCount; c++)
		{
			var (pos, rotationY, wallOffset) = ObtenerEspacioConParedYRotacion();
			if (_occupiedPositions.Contains(pos)) continue;

			// Eje a lo largo de la pared (perpendicular a la dirección de disparo).
			Vector2 ejeLateral = (Mathf.Abs(rotationY) == 90f) ? new Vector2(0, 1) : new Vector2(1, 0);

			int n = Math.Max(1, _maze.ArrowClusterSize);
			for (int i = 0; i < n; i++)
			{
				float lateral = (i - (n - 1) / 2.0f) * espaciado;
				Vector2 offsetFinal = wallOffset + ejeLateral * lateral;

				var trap = _maze.ArrowTrapScene.Instantiate<Node3D>();
				trap.Position = new Vector3(
					pos.X * _maze.GridScale + offsetFinal.X,
					0.0f,
					pos.Y * _maze.GridScale + offsetFinal.Y
				);
				trap.RotationDegrees = new Vector3(0, rotationY, 0);
				_maze.AddChild(trap);
			}

			_occupiedPositions.Add(pos);
			GD.Print($"Grupo de {n} disparadores de flecha en {pos}, rot Y={rotationY}");
		}
	}

	private void SpawnCageTraps()
	{
		if (_maze.CageTrapScene == null) return;

		for (int i = 0; i < _maze.CageTrapCount; i++)
		{
			Vector2I spawnPos = ObtenerEspacioVacioAleatorio();
			var trap = _maze.CageTrapScene.Instantiate<Node3D>();
			trap.Position = new Vector3(spawnPos.X * _maze.GridScale, 0.0f, spawnPos.Y * _maze.GridScale);
			_maze.AddChild(trap);
			_occupiedPositions.Add(spawnPos);
		}
	}

	private bool HayParedAdyacente(Vector2I pos, float rotationY)
	{
		if (rotationY == -90f) return _maze.Map[pos.X - 1, pos.Y] == 1; // pared al oeste
		if (rotationY == 90f) return _maze.Map[pos.X + 1, pos.Y] == 1;  // pared al este
		if (rotationY == 180f) return _maze.Map[pos.X, pos.Y - 1] == 1; // pared al norte
		return _maze.Map[pos.X, pos.Y + 1] == 1;                        // pared al sur (rotationY == 0)
	}

	/// Busca una celda libre con al menos una pared adyacente y devuelve, junto a
	/// la posición, la rotación en Y (grados) para que la trampa dispare hacia el
	/// lado abierto, y un pequeño offset para pegarla visualmente contra la pared.
	private (Vector2I pos, float rotationY, Vector2 wallOffset) ObtenerEspacioConParedYRotacion()
	{
		float offset = _maze.GridScale * 0.47f;
		var candidatos = new List<(Vector2I pos, float rot, Vector2 off)>();

		for (int x = 1; x < _maze.Width - 1; x++)
		{
			for (int z = 1; z < _maze.Height - 1; z++)
			{
				var pos = new Vector2I(x, z);
				if (_maze.Map[x, z] != 0 || _occupiedPositions.Contains(pos)) continue;

				// shoot_direction local por defecto de ArrowTrap es Vector3.FORWARD (-Z),
				// así que la rotación en Y determina hacia dónde vuela la flecha.
				if (_maze.Map[x - 1, z] == 1) candidatos.Add((pos, -90f, new Vector2(-offset, 0)));   // pared al oeste -> dispara al este
				if (_maze.Map[x + 1, z] == 1) candidatos.Add((pos, 90f, new Vector2(offset, 0)));     // pared al este -> dispara al oeste
				if (_maze.Map[x, z - 1] == 1) candidatos.Add((pos, 180f, new Vector2(0, -offset)));   // pared al norte -> dispara al sur
				if (_maze.Map[x, z + 1] == 1) candidatos.Add((pos, 0f, new Vector2(0, offset)));      // pared al sur -> dispara al norte
			}
		}

		if (candidatos.Count > 0)
		{
			return candidatos[_random.Next(candidatos.Count)];
		}

		// Fallback: no hay celdas junto a una pared, usamos una libre cualquiera sin offset.
		return (ObtenerEspacioVacioAleatorio(), _random.Next(0, 4) * 90f, Vector2.Zero);
	}

	private Vector2I SpawnBoss()
	{
		Vector2I spawnPos = new Vector2I(_maze.Width / 2, _maze.Height / 2);

		if (_maze.BossScene != null)
		{
			var boss = _maze.BossScene.Instantiate<Node3D>();
			boss.Position = new Vector3(spawnPos.X * _maze.GridScale, 1.20f, spawnPos.Y * _maze.GridScale);
			_maze.AddChild(boss);
			_occupiedPositions.Add(spawnPos);
		}

		return spawnPos;
	}

	private void SpawnKey(Vector2I bossPosition)
	{
		if (_maze.KeyScene == null) return;

		var key = _maze.KeyScene.Instantiate<Node3D>();
		key.Position = new Vector3(bossPosition.X * _maze.GridScale, 0.5f, bossPosition.Y * _maze.GridScale);
		_maze.AddChild(key);
	}

	private void SpawnDoorOnWall()
	{
		if (_maze.DoorScene == null) return;

		Vector2I freeSpace = ObtenerEspacioConParedAdyacente();
		var door = _maze.DoorScene.Instantiate<Node3D>();
		Vector3 basePos = new Vector3(freeSpace.X * _maze.GridScale, 0.0f, freeSpace.Y * _maze.GridScale);

		float offset = _maze.GridScale * 0.45f;

		if (freeSpace.X == 1)
		{
			basePos.X -= offset;
			door.RotationDegrees = new Vector3(0, 90, 0);
		}
		else if (freeSpace.X == _maze.Width - 2)
		{
			basePos.X += offset;
			door.RotationDegrees = new Vector3(0, -90, 0);
		}
		else if (freeSpace.Y == 1)
		{
			basePos.Z -= offset;
			door.RotationDegrees = new Vector3(0, 0, 0);
		}
		else if (freeSpace.Y == _maze.Height - 2)
		{
			basePos.Z += offset;
			door.RotationDegrees = new Vector3(0, 180, 0);
		}

		door.Position = basePos;
		_maze.AddChild(door);

		_occupiedPositions.Add(freeSpace);
		GD.Print($"Puerta colocada en el borde interior accesible en: {freeSpace}");
	}

	private Vector2I ObtenerEspacioConParedAdyacente()
	{
		List<Vector2I> candidatos = new List<Vector2I>();

		int maxX = _maze.Width - 1;
		int maxZ = _maze.Height - 1;

		for (int x = 1; x < maxX; x++)
		{
			for (int z = 1; z < maxZ; z++)
			{
				if (_maze.Map[x, z] == 0 && !_occupiedPositions.Contains(new Vector2I(x, z)))
				{
					bool tocaBordeIzquierdo = (x == 1 && _maze.Map[x - 1, z] == 1);
					bool tocaBordeDerecho = (x == maxX - 1 && _maze.Map[x + 1, z] == 1);
					bool tocaBordeSuperior = (z == 1 && _maze.Map[x, z - 1] == 1);
					bool tocaBordeInferior = (z == maxZ - 1 && _maze.Map[x, z + 1] == 1);

					if (tocaBordeIzquierdo || tocaBordeDerecho || tocaBordeSuperior || tocaBordeInferior)
					{
						candidatos.Add(new Vector2I(x, z));
					}
				}
			}
		}

		if (candidatos.Count > 0)
		{
			return candidatos[_random.Next(candidatos.Count)];
		}

		return ObtenerEspacioVacioAleatorio();
	}

	private void SpawnPlayer()
	{
		if (_maze.PlayerScene == null) return;

		var activePlayers = GameManager.Players.Where(p => !p.IsSpectator).ToList();

		if (activePlayers.Count > 0)
		{
			for (int i = 0; i < activePlayers.Count; i++)
			{
				var playerInfo = activePlayers[i];
				
				// Todos los jugadores (incluyendo el ID 1) aparecen en las esquinas por igual
				Vector2I spawnPos = FindCornerSpace(i);

				var player = _maze.PlayerScene.Instantiate<Node3D>();
				player.Name = playerInfo.Id.ToString();
				player.SetMultiplayerAuthority(playerInfo.Id);
				player.Position = new Vector3(spawnPos.X * _maze.GridScale, 3.0f, spawnPos.Y * _maze.GridScale);
				_maze.AddChild(player);
				_occupiedPositions.Add(spawnPos);

				if (playerInfo.Id == Multiplayer.GetUniqueId())
				{
					_maze.SetSpawnedPlayer(player);
				}
				GD.Print($"[MazeSpawner] Spawning player '{playerInfo.Name}' (ID: {playerInfo.Id}) at corner pos {spawnPos}");
			}
		}
		else
		{
			// Offline / single-player fallback también usa esquina
			Vector2I spawnPos = FindCornerSpace(0);

			var player = _maze.PlayerScene.Instantiate<Node3D>();
			player.Position = new Vector3(spawnPos.X * _maze.GridScale, 3.0f, spawnPos.Y * _maze.GridScale); 
			_maze.AddChild(player);
			_occupiedPositions.Add(spawnPos);

			_maze.SetSpawnedPlayer(player);
		}

		var localPlayerInfo = GameManager.Players.FirstOrDefault(p => p.Id == Multiplayer.GetUniqueId());
		if (localPlayerInfo != null && localPlayerInfo.IsSpectator)
		{
			SetupSpectatorMode();
		}
	}

	private void SetupSpectatorMode()
	{
		var specUiScene = ResourceLoader.Load<PackedScene>("res://Multiplayer/SpectatorUI.tscn");
		if (specUiScene != null)
		{
			_spectatorUI = specUiScene.Instantiate<SpectatorUI>();
			_maze.AddChild(_spectatorUI);
			_spectatorUI.Connect(SpectatorUI.SignalName.CycleTarget, Callable.From<int>(OnCycleSpectateTarget));
		}

		Input.MouseMode = Input.MouseModeEnum.Visible;
		SetSpectatedTargetIndex(0);
	}

	private void OnCycleSpectateTarget(int direction)
	{
		SetSpectatedTargetIndex(_spectateIndex + direction);
	}

	public void SetSpectatedTargetIndex(int newIndex)
	{
		var activePlayers = GameManager.Players.Where(p => !p.IsSpectator).ToList();

		if (activePlayers.Count == 0)
		{
			if (_currentlySpectatedPlayer != null && IsInstanceValid(_currentlySpectatedPlayer))
			{
				_currentlySpectatedPlayer.SetMeshVisible(true);
				_currentlySpectatedPlayer = null;
			}
			if (_spectatorUI != null)
			{
				_spectatorUI.UpdateSpectateText("", 0);
			}
			return;
		}

		_spectateIndex = (newIndex % activePlayers.Count + activePlayers.Count) % activePlayers.Count;
		var targetInfo = activePlayers[_spectateIndex];
		var targetPlayerNode = _maze.GetNodeOrNull<Player>(targetInfo.Id.ToString());

		if (_currentlySpectatedPlayer != null && IsInstanceValid(_currentlySpectatedPlayer) && _currentlySpectatedPlayer != targetPlayerNode)
		{
			_currentlySpectatedPlayer.SetMeshVisible(true);
		}

		if (targetPlayerNode != null && IsInstanceValid(targetPlayerNode))
		{
			var cam = targetPlayerNode.GetCamera();
			if (cam != null)
			{
				cam.Current = true;
			}
			targetPlayerNode.SetMeshVisible(false);
			_currentlySpectatedPlayer = targetPlayerNode;

			if (_spectatorUI != null)
			{
				_spectatorUI.UpdateSpectateText(targetInfo.Name, targetInfo.Id);
			}
		}
	}

	private void SpawnPalo()
	{
		if (_maze.palo_de_madera == null) return;
		int cantidadPalos = _random.Next(5, 16);
		float alturaPalo = 1.0f; 
		for (int i = 0; i < cantidadPalos; i++)
		{
			Vector2I spawnPos = ObtenerEspacioVacioAleatorio();
			var palo = _maze.palo_de_madera.Instantiate<Node3D>();
			palo.Position = new Vector3(spawnPos.X * _maze.GridScale, alturaPalo, spawnPos.Y * _maze.GridScale); 
			_maze.AddChild(palo);
			_occupiedPositions.Add(spawnPos);
		}
	}

	private Vector2I ObtenerEspacioVacioAleatorio()
	{
		int intentos = 0;
		while (intentos < 1000)
		{
			int x = _random.Next(1, _maze.Width - 1);
			int z = _random.Next(1, _maze.Height - 1);
			Vector2I pos = new Vector2I(x, z);
			if (_maze.Map[x, z] == 0 && !_occupiedPositions.Contains(pos)) return pos;
			intentos++;
		}
		return _maze.FindEmptySpace();
	}

	private Vector2I FindCornerSpace(int cornerIndex = -1)
	{
		int esquinaElegida = cornerIndex >= 0 ? (cornerIndex % 4) : _random.Next(0, 4);
		int targetX = 1;
		int targetZ = 1;

		if (esquinaElegida == 1) { targetX = _maze.Width - 2; targetZ = 1; }
		if (esquinaElegida == 2) { targetX = 1; targetZ = _maze.Height - 2; }
		if (esquinaElegida == 3) { targetX = _maze.Width - 2; targetZ = _maze.Height - 2; }

		int startX = Math.Max(1, targetX - 3);
		int endX = Math.Min(_maze.Width - 2, targetX + 3);
		int startZ = Math.Max(1, targetZ - 3);
		int endZ = Math.Min(_maze.Height - 2, targetZ + 3);

		for (int x = startX; x <= endX; x++)
		{
			for (int z = startZ; z <= endZ; z++)
			{
				Vector2I pos = new Vector2I(x, z);
				if (_maze.Map[x, z] == 0 && !_occupiedPositions.Contains(pos)) return pos;
			}
		}
		return _maze.FindEmptySpace();
	}
}
