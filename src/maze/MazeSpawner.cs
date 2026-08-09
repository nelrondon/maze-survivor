using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MazeSpawner : Node
{
	private Maze _maze;
	private Random _random = new Random();
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

		_random = new Random(_maze.MazeSeed + 100);
		_occupiedPositions.Clear();

		Vector2I bossSpawnPos = SpawnBoss();
		SpawnPlayer();
		SpawnInventoryUI();
		SpawnKey(bossSpawnPos);   
		SpawnDoorOnWall();
		SpawnBackpacks();
		SpawnTraps();
	}

	private Vector2I SpawnBoss()
	{
		Vector2I spawnPos = new Vector2I(_maze.Width / 2, _maze.Height / 2);

		if (_maze.BossScene != null && _maze.GetNodeOrNull("SingleMazeBoss") == null)
		{
			var boss = _maze.BossScene.Instantiate<Node3D>();
			boss.Name = "SingleMazeBoss";
			boss.Position = new Vector3(spawnPos.X * _maze.GridScale, 1.50f, spawnPos.Y * _maze.GridScale);
			_maze.AddChild(boss);
			_occupiedPositions.Add(spawnPos);
			GD.Print($"[MazeSpawner] Boss generado exitosamente en el centro del laberinto: {boss.Position}");
		}

		return spawnPos;
	}
	
	public void SpawnInventoryUI()
	{
		var player = _maze.SpawnedPlayer;
		if (player == null) return;

		var inv = player.GetNodeOrNull("Inventory");
		var handler = player.GetNodeOrNull("ItemUseHandler");

		// Hotbar
		var hotbar = GD.Load<PackedScene>(
			"res://src/inventory/Hotbar/HotbarUI.tscn").Instantiate();
		player.AddChild(hotbar);
		hotbar.Call("setup", inv, handler);

		// Inventario (Tab)
		var invUI = GD.Load<PackedScene>(
			"res://src/inventory/PlayerInventory/PlayerInventoryUI.tscn").Instantiate();
		player.AddChild(invUI);
		invUI.Call("setup", inv);

		// BackpackUI (se abre al interactuar con mochilas)
		var bpUI = GD.Load<PackedScene>(
			"res://src/inventory/Backpack/BackpackUI.tscn").Instantiate();
		player.AddChild(bpUI);
		bpUI.Call("setup", inv);
	}
	
	private void SpawnBackpacks()
	{
		if (_maze.BackpackScene == null) return;

		int cantidad = Math.Max(100, _maze.BackpackCount);
		int spawned = 0;

		for (int i = 0; i < cantidad; i++)
		{
			Vector2I pos = ObtenerEspacioVacioAleatorio();
			var backpack = _maze.BackpackScene.Instantiate<Node3D>();
			backpack.Position = new Vector3(pos.X * _maze.GridScale, 0.2f, pos.Y * _maze.GridScale);
			backpack.RotationDegrees = new Vector3(0, _random.Next(0, 360), 0);
			_maze.AddChild(backpack);
			_occupiedPositions.Add(pos);
			spawned++;
		}
		GD.Print($"[MazeSpawner] {spawned} mochilas generadas individualmente de forma separada por todo el laberinto.");
	}

	private Vector2I GetEmptySpaceInQuadrant(int startX, int endX, int startZ, int endZ)
	{
		int intentos = 0;
		while (intentos < 500)
		{
			int x = _random.Next(startX, endX + 1);
			int z = _random.Next(startZ, endZ + 1);
			Vector2I pos = new Vector2I(x, z);
			if (_maze.Map[x, z] == 0 && !_occupiedPositions.Contains(pos))
				return pos;
			intentos++;
		}
		return ObtenerEspacioVacioAleatorio();
	}

	private void SpawnKey(Vector2I bossPosition)
	{
		if (_maze.KeyScene == null) return;
		if (_maze.GetNodeOrNull("SingleMazeKey") != null) return;

		var key = _maze.KeyScene.Instantiate<Node3D>();
		key.Name = "SingleMazeKey";
		key.Position = new Vector3(bossPosition.X * _maze.GridScale, 0.5f, bossPosition.Y * _maze.GridScale);
		_maze.AddChild(key);
		GD.Print($"[MazeSpawner] Llave única generada en posición: {key.Position}");
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
		Vector2I centerSpawnPos = new Vector2I(_maze.Width / 2 + 2, _maze.Height / 2);
		_maze.Map[centerSpawnPos.X, centerSpawnPos.Y] = 0; // Asegurar pasillo libre en el centro

		if (activePlayers.Count > 0)
		{
			for (int i = 0; i < activePlayers.Count; i++)
			{
				var playerInfo = activePlayers[i];

				Vector2I spawnPos = _maze.DebugSpawnPlayerNearBoss ? centerSpawnPos : FindCornerSpace(i);

				var player = _maze.PlayerScene.Instantiate<Node3D>();
				player.Name = playerInfo.Id.ToString();
				player.SetMultiplayerAuthority(playerInfo.Id);
				player.Position = new Vector3(spawnPos.X * _maze.GridScale, 1.5f, spawnPos.Y * _maze.GridScale);
				_maze.AddChild(player);
				_occupiedPositions.Add(spawnPos);

				if (playerInfo.Id == Multiplayer.GetUniqueId())
				{
					_maze.SetSpawnedPlayer(player);
				}
				GD.Print($"[MazeSpawner] Spawning player '{playerInfo.Name}' (ID: {playerInfo.Id}) en esquina {i} pos {spawnPos}");
			}
		}
		else
		{
			Vector2I spawnPos = _maze.DebugSpawnPlayerNearBoss ? centerSpawnPos : FindCornerSpace(0);

			var player = _maze.PlayerScene.Instantiate<Node3D>();
			player.Position = new Vector3(spawnPos.X * _maze.GridScale, 1.5f, spawnPos.Y * _maze.GridScale); 
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

	public void SetupSpectatorModeForCurrentClient()
	{
		if (_spectatorUI == null || !IsInstanceValid(_spectatorUI))
		{
			var specUiScene = ResourceLoader.Load<PackedScene>("res://src/multiplayer/SpectatorUI.tscn");
			if (specUiScene != null)
			{
				_spectatorUI = specUiScene.Instantiate<SpectatorUI>();
				_maze.AddChild(_spectatorUI);
				_spectatorUI.Connect(SpectatorUI.SignalName.CycleTarget, Callable.From<int>(OnCycleSpectateTarget));
			}
		}

		Input.MouseMode = Input.MouseModeEnum.Visible;
		SetSpectatedTargetIndex(0);
	}

	private void SetupSpectatorMode()
	{
		SetupSpectatorModeForCurrentClient();
	}

	private void OnCycleSpectateTarget(int direction)
	{
		SetSpectatedTargetIndex(_spectateIndex + direction);
	}

	public void SetSpectatedTargetIndex(int newIndex)
	{
		var alivePlayers = GameManager.Players
			.Where(p => !p.IsSpectator)
			.Where(p => {
				var pNode = _maze.GetNodeOrNull<Player>(p.Id.ToString());
				return pNode != null && IsInstanceValid(pNode) && !pNode.IsDead;
			})
			.ToList();

		if (alivePlayers.Count == 0)
		{
			if (_currentlySpectatedPlayer != null && IsInstanceValid(_currentlySpectatedPlayer))
			{
				_currentlySpectatedPlayer.SetMeshVisible(true);
				_currentlySpectatedPlayer = null;
			}
			if (_spectatorUI != null)
			{
				_spectatorUI.UpdateSpectateText("No hay jugadores vivos disponibles", 0);
			}
			return;
		}

		_spectateIndex = (newIndex % alivePlayers.Count + alivePlayers.Count) % alivePlayers.Count;
		var targetInfo = alivePlayers[_spectateIndex];
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

	private void SpawnTraps()
	{
		SpawnSpikeClusters();
		SpawnArrowClusters();
		SpawnCageTraps();
	}

	private void SpawnSpikeClusters()
	{
		var spikeScenes = new List<PackedScene>();
		if (_maze.SpikeTrapScene != null) spikeScenes.Add(_maze.SpikeTrapScene);
		if (_maze.PoisonSpikeTrapScene != null) spikeScenes.Add(_maze.PoisonSpikeTrapScene);
		if (spikeScenes.Count == 0) return;

		int largo = Math.Clamp(_maze.SpikeClusterSize, 1, 2);

		for (int c = 0; c < _maze.SpikeClusterCount; c++)
		{
			if (_random.NextDouble() > _maze.SpikeClusterChance) continue;

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

	private void SpawnArrowClusters()
	{
		if (_maze.ArrowTrapScene == null) return;

		const float espaciado = 1.6f;

		for (int c = 0; c < _maze.ArrowClusterCount; c++)
		{
			var (pos, rotationY, wallOffset) = ObtenerEspacioConParedYRotacion();
			if (_occupiedPositions.Contains(pos)) continue;

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

				if (_maze.Map[x - 1, z] == 1) candidatos.Add((pos, -90f, new Vector2(-offset, 0)));
				if (_maze.Map[x + 1, z] == 1) candidatos.Add((pos, 90f, new Vector2(offset, 0)));
				if (_maze.Map[x, z - 1] == 1) candidatos.Add((pos, 180f, new Vector2(0, -offset)));
				if (_maze.Map[x, z + 1] == 1) candidatos.Add((pos, 0f, new Vector2(0, offset)));
			}
		}

		if (candidatos.Count == 0) return (ObtenerEspacioVacioAleatorio(), 0f, Vector2.Zero);

		return candidatos[_random.Next(candidatos.Count)];
	}
}
