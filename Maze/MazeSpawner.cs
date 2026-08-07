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
		SpawnInventoryUI();
		SpawnKey(bossSpawnPos);   
		SpawnDoorOnWall();
		SpawnBackpacks();
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

		int cantidad = _random.Next(5, 11) * 3; // 15-30 mochilas

		// Dividir el mapa en 4 cuadrantes (uno por esquina donde spawnean jugadores)
		// y distribuir equitativamente
		int porCuadrante = cantidad / 4;
		int extras = cantidad % 4;

		int midX = _maze.Width / 2;
		int midZ = _maze.Height / 2;

		// Cuadrantes: (startX, endX, startZ, endZ)
		var cuadrantes = new (int, int, int, int)[]
		{
			(1, midX - 4, 1, midZ - 4),            // Esquina sup-izq
			(midX + 4, _maze.Width - 2, 1, midZ - 4),   // Esquina sup-der
			(1, midX - 4, midZ + 4, _maze.Height - 2),  // Esquina inf-izq
			(midX + 4, _maze.Width - 2, midZ + 4, _maze.Height - 2), // Esquina inf-der
		};

		for (int q = 0; q < 4; q++)
		{
			int cantidadEnCuadrante = porCuadrante + (q < extras ? 1 : 0);
			var (sx, ex, sz, ez) = cuadrantes[q];

			for (int i = 0; i < cantidadEnCuadrante; i++)
			{
				Vector2I pos = GetEmptySpaceInQuadrant(sx, ex, sz, ez);
				var backpack = _maze.BackpackScene.Instantiate<Node3D>();
				backpack.Position = new Vector3(
					pos.X * _maze.GridScale,
					0.5f,
					pos.Y * _maze.GridScale);
				_maze.AddChild(backpack);
				_occupiedPositions.Add(pos);
			}
		}
		GD.Print($"[MazeSpawner] Spawned {cantidad} backpacks distributed across 4 quadrants");
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
				GD.Print($"[MazeSpawner] Spawning player '{playerInfo.Name}' (ID: {playerInfo.Id}) at center pos {spawnPos}");
			}
		}
		else
		{
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
