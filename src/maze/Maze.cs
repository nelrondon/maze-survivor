using Godot;
using System;
using System.Collections.Generic;

public partial class Maze : Node3D
{
	[ExportGroup("Maze Settings")]
	[Export] public int Width = 51;
	[Export] public int Height = 51;
	[Export] public float GridScale = 6.0f;
	
	[Export] public float ComplexityFactor = 0.15f;

	[ExportGroup("Spawning & Entities")]
	[Export] public PackedScene PlayerScene;
	[Export] public PackedScene BossScene;
	[Export] public bool DebugSpawnPlayerNearBoss = true;
	[Export] public PackedScene KeyScene;
	[Export] public PackedScene DoorScene;
	[Export] public PackedScene BackpackScene;
	[Export] public int BackpackCount = 100;

	[ExportGroup("Trampas")]
	[Export] public PackedScene SpikeTrapScene;
	[Export] public PackedScene PoisonSpikeTrapScene;
	[Export] public PackedScene ArrowTrapScene;
	[Export] public PackedScene CageTrapScene;
	[Export] public int SpikeClusterCount = 100; // 100 clusters de 2 baldosas = 200 trampas de piso en total
	[Export(PropertyHint.Range, "0,1,0.05")] public float SpikeClusterChance = 0.95f;
	[Export] public int SpikeClusterSize = 2; // Máximo 2 trampas de piso juntas (a lo largo del pasillo)
	[Export] public int ArrowClusterCount = 60;
	[Export] public int ArrowClusterSize = 2;
	[Export] public int CageTrapCount = 40;

	[ExportGroup("Texture Options")]
	[Export] public Texture2D WallTexture;
	[Export] public Texture2D FloorTexture;

	public byte[,] Map;
	private Random _random = new Random();
	private NavigationRegion3D _navRegion;
	public Node3D SpawnedPlayer { get; private set; }
	private Map _mapUIInstance;

	public override void _Ready()
	{
		if (Width % 2 == 0) Width++;
		if (Height % 2 == 0) Height++;

		InitializeMap();
		
		// GENERACIÓN EXTREMA DE PASILLOS TORTUOSOS
		GenerateExtremeTortuousMaze(1, 1);
		
		// ELIMINAR CALLEJONES MANTENIENDO EL LABERINTO ENREDADO
		EliminateAllDeadEnds();

		CreateCentralRoom();
		
		// ASEGURAR CAMBIO DE RUTA VÁLIDO DESDE CUALQUIER ESQUINA AL CENTRO
		EnsureAllCornersCanReachCenter();

		_navRegion = new NavigationRegion3D();
		_navRegion.NavigationMesh = new NavigationMesh
		{
			AgentRadius = 0.6f,
			AgentHeight = 2.0f,
			AgentMaxClimb = 0.3f,
			AgentMaxSlope = 45.0f,
			CellSize = 0.25f,
			CellHeight = 0.25f
		};
		AddChild(_navRegion);

		CreateFloorWithCollision(); 
		DrawMapOptimized();
		
		_navRegion.BakeNavigationMesh(onThread: false);

		var spawner = new MazeSpawner();
		AddChild(spawner);
		spawner.SpawnEntities();

		SetupMapUI();
	}

	public override void _Process(double delta)
	{
		UpdateMapPlayersList();
	}

	private void SetupMapUI()
	{
		_mapUIInstance = GetNodeOrNull<Map>("Map") ?? FindChild("Map", true, false) as Map;

		if (_mapUIInstance == null)
		{
			_mapUIInstance = new Map();
			_mapUIInstance.Name = "Map";
			if (_mapUIInstance is Control controlMap)
			{
				controlMap.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			}
			AddChild(_mapUIInstance);
		}

		_mapUIInstance.SetProcessUnhandledInput(true);
		_mapUIInstance.InitializeMapData(Map, GridScale);

		UpdateMapPlayersList();
	}

	private void UpdateMapPlayersList()
	{
		if (_mapUIInstance == null) return;

		List<Node3D> allPlayersList = new List<Node3D>();

		foreach (Node node in GetTree().GetNodesInGroup("Players"))
		{
			if (node is Node3D playerNode)
			{
				allPlayersList.Add(playerNode);

				if (playerNode is Player pScript && pScript.IsMultiplayerAuthority())
				{
					_mapUIInstance.SetLocalPlayer(playerNode);
				}
			}
		}

		_mapUIInstance.UpdatePlayersList(allPlayersList);
	}

	public void SetSpawnedPlayer(Node3D player)
	{
		SpawnedPlayer = player;
	}

	private void CreateFloorWithCollision()
	{
		var staticBody = new StaticBody3D();
		staticBody.Position = new Vector3(((Width * GridScale) / 2) - (GridScale/2), 0, ((Height * GridScale) / 2) - (GridScale/2));
		
		var meshInstance = new MeshInstance3D();
		meshInstance.Mesh = new PlaneMesh() { Size = new Vector2(Width * GridScale, Height * GridScale) };
		
		var collisionShape = new CollisionShape3D();
		collisionShape.Shape = new BoxShape3D { Size = new Vector3(Width * GridScale, 0.2f, Height * GridScale) };
		
		staticBody.AddChild(meshInstance);
		staticBody.AddChild(collisionShape);
		
		var mat = new StandardMaterial3D();
		if (FloorTexture != null)
		{
			mat.AlbedoTexture = FloorTexture;
			mat.Uv1Scale = new Vector3(Width / 2.0f, Height / 2.0f, 1.0f);
		}
		else
		{
			mat.AlbedoColor = new Color(0.2f, 0.2f, 0.2f);
		}

		meshInstance.SetSurfaceOverrideMaterial(0, mat);
		_navRegion.AddChild(staticBody);
	}

	private void DrawMapOptimized()
	{
		var wallMaterial = new StandardMaterial3D();
		if (WallTexture != null)
		{
			wallMaterial.AlbedoTexture = WallTexture;
			wallMaterial.Uv1Scale = new Vector3(1.0f, 1.0f, 1.0f);
		}
		else
		{
			wallMaterial.AlbedoColor = new Color(0.2f, 0.6f, 0.8f);
		}

		var boxMesh = new BoxMesh() { Size = new Vector3(GridScale, GridScale, GridScale) };
		boxMesh.Material = wallMaterial;

		int wallCount = 0;
		for (int z = 0; z < Height; z++)
			for (int x = 0; x < Width; x++)
				if (Map[x, z] == 1) wallCount++;

		var multiMesh = new MultiMesh();
		multiMesh.Mesh = boxMesh;
		multiMesh.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
		multiMesh.InstanceCount = wallCount;

		int index = 0;
		var staticBody = new StaticBody3D();

		for (int z = 0; z < Height; z++)
		{
			for (int x = 0; x < Width; x++)
			{
				if (Map[x, z] == 1)
				{
					Vector3 pos = new Vector3(x * GridScale, GridScale / 2, z * GridScale);
					Transform3D transform = new Transform3D(Basis.Identity, pos);
					multiMesh.SetInstanceTransform(index, transform);

					var colShape = new CollisionShape3D();
					colShape.Shape = new BoxShape3D { Size = new Vector3(GridScale, GridScale, GridScale) };
					colShape.Position = pos;
					staticBody.AddChild(colShape);

					index++;
				}
			}
		}

		var multiMeshInstance = new MultiMeshInstance3D();
		multiMeshInstance.Multimesh = multiMesh;
		
		_navRegion.AddChild(multiMeshInstance);
		_navRegion.AddChild(staticBody);
	}

	public Vector2I FindEmptySpace() { 
		for (int x = 0; x < Width; x++) 
			for (int z = 0; z < Height; z++) 
				if (Map[x, z] == 0) return new Vector2I(x, z); 
		return new Vector2I(1, 1); 
	}
	
	private void InitializeMap() { 
		Map = new byte[Width, Height]; 
		for (int z = 0; z < Height; z++) 
			for (int x = 0; x < Width; x++) 
				Map[x, z] = 1; 
	}

	private void GenerateExtremeTortuousMaze(int startX, int startZ)
	{
		var stack = new Stack<Vector2I>();
		Map[startX, startZ] = 0;
		stack.Push(new Vector2I(startX, startZ));

		while (stack.Count > 0)
		{
			var current = stack.Peek();
			var neighbors = GetTortuousNeighbors(current.X, current.Y);

			if (neighbors.Count > 0)
			{
				var next = neighbors[_random.Next(neighbors.Count)];
				Map[current.X + (next.X - current.X) / 2, current.Y + (next.Y - current.Y) / 2] = 0;
				Map[next.X, next.Y] = 0;
				stack.Push(next);
			}
			else
			{
				stack.Pop();
			}
		}
	}

	private List<Vector2I> GetTortuousNeighbors(int x, int z)
	{
		var valid = new List<Vector2I>();
		var dirs = new Vector2I[] { new(2, 0), new(0, 2), new(-2, 0), new(0, -2) };

		for (int i = 0; i < dirs.Length; i++)
		{
			int randIndex = _random.Next(i, dirs.Length);
			var temp = dirs[randIndex];
			dirs[randIndex] = dirs[i];
			dirs[i] = temp;
		}

		foreach (var dir in dirs)
		{
			int nx = x + dir.X;
			int nz = z + dir.Y;

			if (nx > 0 && nx < Width - 1 && nz > 0 && nz < Height - 1)
			{
				if (Map[nx, nz] == 1)
				{
					valid.Add(new Vector2I(nx, nz));
				}
			}
		}
		return valid;
	}

	private void EliminateAllDeadEnds()
	{
		bool changesMade = true;
		while (changesMade)
		{
			changesMade = false;
			for (int z = 1; z < Height - 1; z++)
			{
				for (int x = 1; x < Width - 1; x++)
				{
					if (Map[x, z] == 0)
					{
						int openNeighbors = 0;
						if (Map[x + 1, z] == 0) openNeighbors++;
						if (Map[x - 1, z] == 0) openNeighbors++;
						if (Map[x, z + 1] == 0) openNeighbors++;
						if (Map[x, z - 1] == 0) openNeighbors++;

						if (openNeighbors == 1)
						{
							var closedDirs = new List<Vector2I>();
							if (Map[x + 1, z] == 1 && x + 1 < Width - 1) closedDirs.Add(new Vector2I(1, 0));
							if (Map[x - 1, z] == 1 && x - 1 > 0) closedDirs.Add(new Vector2I(-1, 0));
							if (Map[x, z + 1] == 1 && z + 1 < Height - 1) closedDirs.Add(new Vector2I(0, 1));
							if (Map[x, z - 1] == 1 && z - 1 > 0) closedDirs.Add(new Vector2I(0, -1));

							if (closedDirs.Count > 0)
							{
								var openDir = closedDirs[_random.Next(closedDirs.Count)];
								Map[x + openDir.X, z + openDir.Y] = 0;
								changesMade = true;
							}
						}
					}
				}
			}
		}
	}

	// --- VERIFICACIÓN Y GARANTÍA DE RUTA DESDE CADA ESQUINA HASTA EL CENTRO ---
	private void EnsureAllCornersCanReachCenter()
	{
		Vector2I[] corners = new Vector2I[]
		{
			new Vector2I(1, 1),
			new Vector2I(Width - 2, 1),
			new Vector2I(1, Height - 2),
			new Vector2I(Width - 2, Height - 2)
		};

		Vector2I center = new Vector2I(Width / 2, Height / 2);

		foreach (var corner in corners)
		{
			if (!HasPathToCenter(corner, center))
			{
				// Si por alguna razón la esquina quedó totalmente aislada, se fuerza un pasillo directo al centro respetando el laberinto
				CarveDirectPath(corner, center);
			}
		}
	}

	private bool HasPathToCenter(Vector2I start, Vector2I target)
	{
		var visited = new bool[Width, Height];
		var queue = new Queue<Vector2I>();

		queue.Enqueue(start);
		visited[start.X, start.Y] = true;

		var dirs = new Vector2I[] { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

		while (queue.Count > 0)
		{
			var current = queue.Dequeue();

			if (Math.Abs(current.X - target.X) <= 3 && Math.Abs(current.Y - target.Y) <= 3)
			{
				return true; // Ya conecta con la sala central o sus adyacencias
			}

			foreach (var dir in dirs)
			{
				int nx = current.X + dir.X;
				int nz = current.Y + dir.Y;

				if (nx >= 0 && nx < Width && nz >= 0 && nz < Height)
				{
					if (Map[nx, nz] == 0 && !visited[nx, nz])
					{
						visited[nx, nz] = true;
						queue.Enqueue(new Vector2I(nx, nz));
					}
				}
			}
		}
		return false;
	}

	private void CarveDirectPath(Vector2I from, Vector2I to)
	{
		int currX = from.X;
		int currZ = from.Y;

		while (currX != to.X)
		{
			Map[currX, currZ] = 0;
			currX += (to.X > currX) ? 1 : -1;
		}
		while (currZ != to.Y)
		{
			Map[currX, currZ] = 0;
			currZ += (to.Y > currZ) ? 1 : -1;
		}
	}
	
	private void CreateCentralRoom() 
	{
		int centerX = Width / 2;
		int centerZ = Height / 2;
		int radius = 3;
		for (int x = centerX - radius; x <= centerX + radius; x++)
			for (int z = centerZ - radius; z <= centerZ + radius; z++)
				Map[x, z] = 0;
	}
}
