using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

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
	[Export] public PackedScene palo_de_madera;
	[Export] public PackedScene KeyScene;
	[Export] public PackedScene DoorScene;

	[ExportGroup("Texture Options")]
	[Export] public Texture2D WallTexture;
	[Export] public Texture2D FloorTexture;

	public byte[,] Map;
	private Random _random = new Random();
	private Node3D _geometryRoot;
	private AStarGrid2D _pathGrid;
	private Node3D _spawnedPlayer;
	private Map _mapUIInstance;

	public override void _Ready()
	{
		AddToGroup("Maze");

		if (Width % 2 == 0) Width++;
		if (Height % 2 == 0) Height++;

		InitializeMap();
		GenerateExtremeTortuousMaze(1, 1);
		EliminateAllDeadEnds();
		CreateCentralRoom();
		EnsureAllCornersCanReachCenter();

		BuildPathGrid();

		_geometryRoot = new Node3D { Name = "Geometry" };
		AddChild(_geometryRoot);

		CreateFloorWithCollision(); 
		DrawMapOptimized();

		var spawner = new MazeSpawner();
		AddChild(spawner);
		spawner.SpawnEntities();

		SetupMapUI();
	}

	private void BuildPathGrid()
	{
		_pathGrid = new AStarGrid2D();
		_pathGrid.Region = new Rect2I(0, 0, Width, Height);
		_pathGrid.CellSize = Vector2.One;
		_pathGrid.DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles;
		_pathGrid.Update();

		for (int z = 0; z < Height; z++)
			for (int x = 0; x < Width; x++)
				_pathGrid.SetPointSolid(new Vector2I(x, z), Map[x, z] == 1);

		GD.Print("[DEBUG] AStarGrid2D listo: ", Width, "x", Height, " celdas");
	}

	public Vector3[] FindPath(Vector3 fromWorld, Vector3 toWorld)
	{
		if (_pathGrid == null) return Array.Empty<Vector3>();

		Vector2I from = WorldToCell(fromWorld);
		Vector2I to = WorldToCell(toWorld);

		from.X = Mathf.Clamp(from.X, 0, Width - 1);
		from.Y = Mathf.Clamp(from.Y, 0, Height - 1);
		to.X = Mathf.Clamp(to.X, 0, Width - 1);
		to.Y = Mathf.Clamp(to.Y, 0, Height - 1);

		if (Map[from.X, from.Y] == 1) from = FindNearestOpenCell(from);
		if (Map[to.X, to.Y] == 1) to = FindNearestOpenCell(to);

		Vector2I[] cellPath = _pathGrid.GetIdPath(from, to).ToArray();
		var worldPath = new Vector3[cellPath.Length];
		for (int i = 0; i < cellPath.Length; i++)
			worldPath[i] = CellToWorld(cellPath[i], fromWorld.Y);

		return worldPath;
	}

	private Vector2I WorldToCell(Vector3 worldPos)
	{
		return new Vector2I(
			Mathf.RoundToInt(worldPos.X / GridScale),
			Mathf.RoundToInt(worldPos.Z / GridScale)
		);
	}

	private Vector3 CellToWorld(Vector2I cell, float y)
	{
		return new Vector3(cell.X * GridScale, y, cell.Y * GridScale);
	}

	private Vector2I FindNearestOpenCell(Vector2I from)
	{
		int maxRadius = Math.Max(Width, Height);
		for (int r = 1; r < maxRadius; r++)
		{
			for (int dx = -r; dx <= r; dx++)
			{
				for (int dz = -r; dz <= r; dz++)
				{
					int x = from.X + dx;
					int z = from.Y + dz;
					if (x < 0 || x >= Width || z < 0 || z >= Height) continue;
					if (Map[x, z] == 0) return new Vector2I(x, z);
				}
			}
		}
		return from;
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
		_spawnedPlayer = player;
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
		_geometryRoot.AddChild(staticBody);
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
		
		_geometryRoot.AddChild(multiMeshInstance);
		_geometryRoot.AddChild(staticBody);
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
				return true; 
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
