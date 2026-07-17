using Godot;
using System;
using System.Collections.Generic;

public partial class Maze : Node3D
{
	[Export] public int Width = 51;
	[Export] public int Height = 51;
	[Export] public float GridScale = 6.0f;
	[Export] public PackedScene PlayerScene;
	[Export] public PackedScene BossScene;
	[Export] public bool DebugSpawnPlayerNearBoss = false;

	public byte[,] Map;
	private Random _random = new Random();
	private NavigationRegion3D _navRegion;

	public override void _Ready()
	{
		if (Width % 2 == 0) Width++;
		if (Height % 2 == 0) Height++;

		// 1. Iluminación
		var light = new DirectionalLight3D();
		light.RotationDegrees = new Vector3(-60, 45, 0);
		AddChild(light);

		// 2. Generar datos
		InitializeMap();
		GenerateIterative(1, 1);
		CreateCentralRoom();

		// 3. Preparar región de navegación (el suelo y las paredes se agregan dentro de ella)
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

		// 4. Generar Suelo y Paredes
		CreateFloorWithCollision(); 
		DrawMapOptimized();
		
		// 5. Bakear el navmesh en base a la geometría recién creada (síncrono, bloquea hasta terminar)
		_navRegion.BakeNavigationMesh(onThread: false);
 
		// 6. Instanciar Jugador y Boss
		SpawnPlayer();
		SpawnBoss();
	}

	private void SpawnPlayer()
	{
		if (PlayerScene == null) return;
		// Vector2I spawnPos = FindEmptySpace();
		
		Vector2I center = new Vector2I(Width / 2, Height / 2);
		Vector2I spawnPos = DebugSpawnPlayerNearBoss
			? center + new Vector2I(2, 0) : FindEmptySpace();

		var player = PlayerScene.Instantiate<Node3D>();
		player.Position = new Vector3(spawnPos.X * GridScale, 3.0f, spawnPos.Y * GridScale); 
		AddChild(player);

		var cam = player.GetNodeOrNull<Camera3D>("Head/Camera3D");
		if (cam != null) cam.Current = true;
	}

	private void SpawnBoss()
	{
		if (BossScene == null) return;
 
		// El boss vive en el cuarto central que ya generamos en CreateCentralRoom()
		Vector2I spawnPos = new Vector2I(Width / 2, Height / 2);
 
		var boss = BossScene.Instantiate<Node3D>();
		boss.Position = new Vector3(spawnPos.X * GridScale, 1.20f, spawnPos.Y * GridScale);
		AddChild(boss);
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
		
		var mat = new StandardMaterial3D() { AlbedoColor = new Color(0.2f, 0.2f, 0.2f) };
		meshInstance.SetSurfaceOverrideMaterial(0, mat);
		_navRegion.AddChild(staticBody);
	}

	private void DrawMapOptimized()
	{
		var wallMaterial = new StandardMaterial3D() { AlbedoColor = new Color(0.2f, 0.6f, 0.8f) };
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		var boxMesh = new BoxMesh() { Size = new Vector3(GridScale, GridScale, GridScale) };

		for (int z = 0; z < Height; z++)
		{
			for (int x = 0; x < Width; x++)
			{
				if (Map[x, z] == 1)
				{
					Transform3D transform = new Transform3D(Basis.Identity, new Vector3(x * GridScale, GridScale / 2, z * GridScale));
					st.AppendFrom(boxMesh, 0, transform);
				}
			}
		}
		st.GenerateNormals();
		st.SetMaterial(wallMaterial);
		
		var meshInstance = new MeshInstance3D { Mesh = st.Commit() };
		
		// CORRECCIÓN: Se usa CreateTrimeshCollision para formas complejas no convexas
		meshInstance.CreateTrimeshCollision(); 
		_navRegion.AddChild(meshInstance);
	}

	private Vector2I FindEmptySpace() { for (int x = 0; x < Width; x++) for (int z = 0; z < Height; z++) if (Map[x, z] == 0) return new Vector2I(x, z); return new Vector2I(1, 1); }
	private void InitializeMap() { Map = new byte[Width, Height]; for (int z = 0; z < Height; z++) for (int x = 0; x < Width; x++) Map[x, z] = 1; }
	private void GenerateIterative(int startX, int startZ) { 
		var stack = new Stack<Vector2I>();
		Map[startX, startZ] = 0;
		stack.Push(new Vector2I(startX, startZ));
		while (stack.Count > 0) {
			var current = stack.Peek();
			var neighbors = GetValidNeighbors(current.X, current.Y);
			if (neighbors.Count > 0) {
				var next = neighbors[_random.Next(neighbors.Count)];
				Map[current.X + (next.X - current.X) / 2, current.Y + (next.Y - current.Y) / 2] = 0;
				Map[next.X, next.Y] = 0;
				stack.Push(next);
			} else stack.Pop();
		}
	}
	private List<Vector2I> GetValidNeighbors(int x, int z) {
		var valid = new List<Vector2I>();
		var dirs = new Vector2I[] { new(2, 0), new(0, 2), new(-2, 0), new(0, -2) };
		foreach (var dir in dirs) {
			int nx = x + dir.X, nz = z + dir.Y;
			if (nx > 0 && nx < Width - 1 && nz > 0 && nz < Height - 1 && Map[nx, nz] == 1)
				valid.Add(new Vector2I(nx, nz));
		}
		return valid;
	}
	private void CreateCentralRoom() {
		int centerX = Width / 2;
		int centerZ = Height / 2;
		int radius = 3;
		for (int x = centerX - radius; x <= centerX + radius; x++)
			for (int z = centerZ - radius; z <= centerZ + radius; z++)
				Map[x, z] = 0;
	}
}
