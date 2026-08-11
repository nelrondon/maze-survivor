using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MazeSpawner : Node
{
	private struct WallCell
	{
		public Vector2I Pos;
		public float RotationY;
		public Vector2 WallOffset;

		public WallCell(Vector2I pos, float rotY, Vector2 offset)
		{
			Pos = pos;
			RotationY = rotY;
			WallOffset = offset;
		}
	}

	private Maze _maze;
	private Random _random = new Random();
	private readonly HashSet<Vector2I> _occupiedPositions = new HashSet<Vector2I>();

	// Pools indexados para selección en tiempo constante O(1)
	private readonly List<Vector2I> _freeFloorPool = new List<Vector2I>();
	private readonly List<WallCell> _wallAdjacentPool = new List<WallCell>();
	private readonly Dictionary<int, List<Vector2I>> _sectorFloorPools = new Dictionary<int, List<Vector2I>>();

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

		// Indexación espacial O(N) en un solo paso inicial
		IndexMazeCells();

		Vector2I bossSpawnPos = SpawnBoss();
		SpawnMiniBosses(bossSpawnPos);
		SpawnPlayer();
		SpawnInventoryUI();
		SpawnKey(bossSpawnPos);   
		SpawnDoorOnWall();
		SpawnBackpacks();
		SpawnTraps();
		SpawnDecoration();
	}

	private void IndexMazeCells()
	{
		_freeFloorPool.Clear();
		_wallAdjacentPool.Clear();
		_sectorFloorPools.Clear();
		for (int i = 0; i < 16; i++)
		{
			_sectorFloorPools[i] = new List<Vector2I>();
		}

		int width = _maze.Width;
		int height = _maze.Height;
		float offset = _maze.GridScale * 0.47f;

		Vector2I centerPos = new Vector2I(width / 2, height / 2);
		int centerRadius = 3;

		for (int x = 1; x < width - 1; x++)
		{
			for (int z = 1; z < height - 1; z++)
			{
				if (_maze.Map[x, z] != 0) continue;

				// Excluir la sala central del boss para evitar que mochilas/trampas aparezcan pegadas al spawn inicial
				if (Math.Abs(x - centerPos.X) <= centerRadius && Math.Abs(z - centerPos.Y) <= centerRadius)
					continue;

				Vector2I pos = new Vector2I(x, z);

				_freeFloorPool.Add(pos);

				// Clasificación estratificada por sub-regiones (4x4 = 16 sectores espaciales)
				int sectorX = Math.Clamp((x * 4) / width, 0, 3);
				int sectorZ = Math.Clamp((z * 4) / height, 0, 3);
				int sector = sectorZ * 4 + sectorX;
				_sectorFloorPools[sector].Add(pos);

				// Detectar paredes contiguas para colocación de puertas y trampas de pared
				if (_maze.Map[x - 1, z] == 1) _wallAdjacentPool.Add(new WallCell(pos, -90f, new Vector2(-offset, 0)));
				if (_maze.Map[x + 1, z] == 1) _wallAdjacentPool.Add(new WallCell(pos, 90f, new Vector2(offset, 0)));
				if (_maze.Map[x, z - 1] == 1) _wallAdjacentPool.Add(new WallCell(pos, 180f, new Vector2(0, -offset)));
				if (_maze.Map[x, z + 1] == 1) _wallAdjacentPool.Add(new WallCell(pos, 0f, new Vector2(0, offset)));
			}
		}

		// Barajado determinista Fisher-Yates sobre cada piscina
		ShuffleList(_freeFloorPool);
		ShuffleList(_wallAdjacentPool);
		for (int i = 0; i < 16; i++)
		{
			ShuffleList(_sectorFloorPools[i]);
		}
	}

	private void ShuffleList<T>(IList<T> list)
	{
		int n = list.Count;
		while (n > 1)
		{
			n--;
			int k = _random.Next(n + 1);
			T value = list[k];
			list[k] = list[n];
			list[n] = value;
		}
	}

	private Vector2I PopFreeCell(int preferredSector = -1)
	{
		if (preferredSector >= 0 && preferredSector < 16 && _sectorFloorPools.ContainsKey(preferredSector))
		{
			var sectorList = _sectorFloorPools[preferredSector];
			for (int i = sectorList.Count - 1; i >= 0; i--)
			{
				Vector2I p = sectorList[i];
				sectorList.RemoveAt(i);
				if (!_occupiedPositions.Contains(p))
				{
					_occupiedPositions.Add(p);
					_freeFloorPool.Remove(p);
					return p;
				}
			}
		}

		for (int i = _freeFloorPool.Count - 1; i >= 0; i--)
		{
			Vector2I p = _freeFloorPool[i];
			_freeFloorPool.RemoveAt(i);
			if (!_occupiedPositions.Contains(p))
			{
				_occupiedPositions.Add(p);
				return p;
			}
		}

		return _maze.FindEmptySpace();
	}

	private WallCell PopWallCell()
	{
		for (int i = _wallAdjacentPool.Count - 1; i >= 0; i--)
		{
			WallCell cell = _wallAdjacentPool[i];
			_wallAdjacentPool.RemoveAt(i);
			if (!_occupiedPositions.Contains(cell.Pos))
			{
				return cell;
			}
		}

		var fallbackPos = PopFreeCell();
		return new WallCell(fallbackPos, 0f, Vector2.Zero);
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

	private void SpawnMiniBosses(Vector2I bossSpawnPos)
	{
		if (_maze.MiniBossScene == null)
		{
			var loadedScene = GD.Load<PackedScene>("res://src/entities/enemies/mini_boss/mini_boss.tscn");
			if (loadedScene != null)
			{
				_maze.MiniBossScene = loadedScene;
			}
		}

		if (_maze.MiniBossScene == null) return;

		var bossNode = _maze.GetNodeOrNull<Node3D>("SingleMazeBoss");

		// 1. Generar MiniBosses repartidos equitativamente, alejados del Boss principal y entre sí
		int count = Math.Max(1, _maze.MiniBossCount);
		int spawned = 0;
		const int minDistanceCells = 5; // Distancia mínima entre MiniBosses
		const int minBossDistanceCells = 10; // Distancia mínima al Boss principal
		var miniBossPositions = new List<Vector2I>();

		for (int i = 0; i < count; i++)
		{
			int targetSector = i % 16;
			Vector2I pos = FindMiniBossPosition(bossSpawnPos, targetSector, miniBossPositions, minDistanceCells, minBossDistanceCells);
			if (pos.X < 0) continue; // No se encontró posición válida fuera de la zona del boss

			var miniBoss = _maze.MiniBossScene.Instantiate<Node3D>();
			miniBoss.Name = $"MiniBoss_{i + 1}";
			miniBoss.Position = new Vector3(pos.X * _maze.GridScale, 1.0f, pos.Y * _maze.GridScale);
			miniBoss.Set("guards_exit_on_key", false);
			_maze.AddChild(miniBoss);
			miniBossPositions.Add(pos);
			_occupiedPositions.Add(pos);
			spawned++;
		}
		GD.Print($"[MazeSpawner] {spawned} MiniBosses generados fuera de la zona del Boss (distancia mín al boss: {minBossDistanceCells} celdas).");

		// 2. Generar únicamente los MiniBosses escolta que siguen al Boss principal
		int escortCount = Math.Max(0, _maze.MiniBossEscortCount);
		int escortsSpawned = 0;
		Vector3 bossWorldPos = new Vector3(bossSpawnPos.X * _maze.GridScale, 1.0f, bossSpawnPos.Y * _maze.GridScale);

		for (int i = 0; i < escortCount; i++)
		{
			var escort = _maze.MiniBossScene.Instantiate<Node3D>();
			escort.Name = $"MiniBossEscort_{i + 1}";
			
			float offsetX = (i == 0) ? 2.5f : -2.5f;
			float offsetZ = (i == 0) ? 2.5f : -2.5f;
			escort.Position = bossWorldPos + new Vector3(offsetX, 0, offsetZ);

			escort.Set("guards_exit_on_key", false);
			if (bossNode != null)
			{
				escort.Set("follow_target", bossNode);
			}

			_maze.AddChild(escort);
			escortsSpawned++;
		}
		GD.Print($"[MazeSpawner] {escortsSpawned} MiniBosses escoltas generados junto al Boss.");
	}
	
	public void SpawnInventoryUI()
	{
		var player = _maze.SpawnedPlayer;
		if (player == null) return;

		var inv = player.GetNodeOrNull("Inventory");
		var handler = player.GetNodeOrNull("ItemUseHandler");

		var hotbar = GD.Load<PackedScene>("res://src/inventory/Hotbar/HotbarUI.tscn").Instantiate();
		player.AddChild(hotbar);
		hotbar.Call("setup", inv, handler);

		var invUI = GD.Load<PackedScene>("res://src/inventory/PlayerInventory/PlayerInventoryUI.tscn").Instantiate();
		player.AddChild(invUI);
		invUI.Call("setup", inv);

		var bpUI = GD.Load<PackedScene>("res://src/inventory/Backpack/BackpackUI.tscn").Instantiate();
		player.AddChild(bpUI);
		bpUI.Call("setup", inv);
	}
	
	private void SpawnBackpacks()
	{
		if (_maze.BackpackScene == null) return;

		int cantidad = Math.Max(1, _maze.BackpackCount);
		int spawned = 0;

		for (int i = 0; i < cantidad; i++)
		{
			int targetSector = i % 16; // Distribución equitativa entre los 16 sectores espaciales
			Vector2I pos = PopFreeCell(targetSector);
			var backpack = _maze.BackpackScene.Instantiate<Node3D>();
			backpack.Position = new Vector3(pos.X * _maze.GridScale, 0.2f, pos.Y * _maze.GridScale);
			backpack.RotationDegrees = new Vector3(0, _random.Next(0, 360), 0);
			_maze.AddChild(backpack);
			spawned++;
		}
		GD.Print($"[MazeSpawner] {spawned} mochilas generadas en O(1) con distribución estratificada en 16 sectores.");
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

		WallCell wallCell = PopWallCell();
		var door = _maze.DoorScene.Instantiate<Node3D>();
		Vector3 basePos = new Vector3(wallCell.Pos.X * _maze.GridScale, 0.0f, wallCell.Pos.Y * _maze.GridScale);
		basePos.X += wallCell.WallOffset.X;
		basePos.Z += wallCell.WallOffset.Y;

		door.Position = basePos;
		door.RotationDegrees = new Vector3(0, wallCell.RotationY, 0);
		_maze.AddChild(door);

		_occupiedPositions.Add(wallCell.Pos);
		GD.Print($"[MazeSpawner] Puerta colocada en O(1) en la posición: {wallCell.Pos}");
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
				if (_maze.Map[x, z] == 0 && !_occupiedPositions.Contains(pos))
				{
					_occupiedPositions.Add(pos);
					return pos;
				}
			}
		}
		return PopFreeCell();
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
			if (fila == null) continue;

			foreach (var pos in fila)
			{
				var scene = spikeScenes[_random.Next(spikeScenes.Count)];
				var trap = scene.Instantiate<Node3D>();
				trap.Position = new Vector3(pos.X * _maze.GridScale, 0.0f, pos.Y * _maze.GridScale);
				_maze.AddChild(trap);
				_occupiedPositions.Add(pos);
			}
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
			if (_wallAdjacentPool.Count == 0) break;
			WallCell cell = PopWallCell();

			Vector2 ejeLateral = (Mathf.Abs(cell.RotationY) == 90f) ? new Vector2(0, 1) : new Vector2(1, 0);

			int n = Math.Max(1, _maze.ArrowClusterSize);
			for (int i = 0; i < n; i++)
			{
				float lateral = (i - (n - 1) / 2.0f) * espaciado;
				Vector2 offsetFinal = cell.WallOffset + ejeLateral * lateral;

				var trap = _maze.ArrowTrapScene.Instantiate<Node3D>();
				trap.Position = new Vector3(
					cell.Pos.X * _maze.GridScale + offsetFinal.X,
					0.0f,
					cell.Pos.Y * _maze.GridScale + offsetFinal.Y
				);
				trap.RotationDegrees = new Vector3(0, cell.RotationY, 0);
				_maze.AddChild(trap);
			}

			_occupiedPositions.Add(cell.Pos);
		}
	}

	private void SpawnCageTraps()
	{
		if (_maze.CageTrapScene == null) return;

		for (int i = 0; i < _maze.CageTrapCount; i++)
		{
			Vector2I spawnPos = PopFreeCell();
			var trap = _maze.CageTrapScene.Instantiate<Node3D>();
			trap.Position = new Vector3(spawnPos.X * _maze.GridScale, 0.0f, spawnPos.Y * _maze.GridScale);
			_maze.AddChild(trap);
		}
	}

	// ===== DECORACIÓN AMBIENTAL =====

	private void SpawnDecoration()
	{
		_maze.TorchScene ??= GD.Load<PackedScene>("res://src/entities/world/torch.tscn");
		SpawnTorches();
	}

	private void SpawnTorches()
	{
		if (_maze.TorchScene == null) return;

		int count = Math.Max(0, _maze.TorchCount);
		int spawned = 0;

		for (int i = 0; i < count; i++)
		{
			if (_wallAdjacentPool.Count == 0) break;

			WallCell cell = PopWallCell();
			var torch = _maze.TorchScene.Instantiate<Node3D>();

			Vector3 pos = new Vector3(
				cell.Pos.X * _maze.GridScale + cell.WallOffset.X,
				0.0f,
				cell.Pos.Y * _maze.GridScale + cell.WallOffset.Y
			);
			torch.Position = pos;
			torch.RotationDegrees = new Vector3(0, cell.RotationY, 0);

			var light = torch.GetNodeOrNull<OmniLight3D>("OmniLight3D");
			if (light != null)
			{
				light.ShadowEnabled = false;
				light.OmniRange = 5.0f;
				light.LightEnergy = 1.2f;
			}

			_maze.AddChild(torch);
			spawned++;
		}
		GD.Print($"[MazeSpawner] {spawned} antorchas colocadas en paredes.");
	}

	// ===== UTILIDAD: DISTANCIA MÍNIMA ENTRE MINIBOSSES =====

	private Vector2I FindMiniBossPosition(Vector2I bossSpawnPos, int preferredSector, List<Vector2I> existingPositions, int minDistance, int minBossDistance)
	{
		// Intentar primero en el sector preferido
		if (preferredSector >= 0 && preferredSector < 16 && _sectorFloorPools.ContainsKey(preferredSector))
		{
			var sectorList = _sectorFloorPools[preferredSector];
			for (int i = sectorList.Count - 1; i >= 0; i--)
			{
				Vector2I p = sectorList[i];
				if (!_occupiedPositions.Contains(p) && IsFarEnough(p, bossSpawnPos, minBossDistance, existingPositions, minDistance))
				{
					sectorList.RemoveAt(i);
					_freeFloorPool.Remove(p);
					return p;
				}
			}
		}

		// Fallback: buscar en el pool general
		for (int i = _freeFloorPool.Count - 1; i >= 0; i--)
		{
			Vector2I p = _freeFloorPool[i];
			if (!_occupiedPositions.Contains(p) && IsFarEnough(p, bossSpawnPos, minBossDistance, existingPositions, minDistance))
			{
				_freeFloorPool.RemoveAt(i);
				return p;
			}
		}

		// Relajar la distancia entre minibosses si es necesario, manteniendo siempre la distancia con el Boss
		for (int i = _freeFloorPool.Count - 1; i >= 0; i--)
		{
			Vector2I p = _freeFloorPool[i];
			if (!_occupiedPositions.Contains(p) && IsFarEnough(p, bossSpawnPos, minBossDistance, existingPositions, 2))
			{
				_freeFloorPool.RemoveAt(i);
				return p;
			}
		}

		return new Vector2I(-1, -1);
	}

	private bool IsFarEnough(Vector2I candidate, Vector2I bossSpawnPos, int minBossDistance, List<Vector2I> existingPositions, int minDistance)
	{
		// Distancia mínima al Boss principal
		int dxBoss = Math.Abs(candidate.X - bossSpawnPos.X);
		int dzBoss = Math.Abs(candidate.Y - bossSpawnPos.Y);
		if (dxBoss + dzBoss < minBossDistance)
		{
			return false;
		}

		// Distancia mínima a otros MiniBosses
		foreach (var existing in existingPositions)
		{
			int dx = Math.Abs(candidate.X - existing.X);
			int dz = Math.Abs(candidate.Y - existing.Y);
			if (dx + dz < minDistance) // Distancia Manhattan
			{
				return false;
			}
		}
		return true;
	}
}
