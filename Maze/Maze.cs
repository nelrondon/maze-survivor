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
	[Export] public bool DebugSpawnPlayerNearBoss = true;
	[Export] public PackedScene palo_de_madera;

	public byte[,] Map;
	private Random _random = new Random();
	private NavigationRegion3D _navRegion;
	private MazeSpawner _spawner;

	public override void _Ready()
	{
		if (Width % 2 == 0) Width++;
		if (Height % 2 == 0) Height++;

		var light = new DirectionalLight3D();
		light.RotationDegrees = new Vector3(-60, 45, 0);
		AddChild(light);

		InitializeMap();
		GenerateIterative(1, 1);
		CreateCentralRoom();

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
 
		_spawner = new MazeSpawner();
		AddChild(_spawner); 
		_spawner.SpawnEntities();
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
		meshInstance.CreateTrimeshCollision(); 
		_navRegion.AddChild(meshInstance);
	}

	public Vector2I FindEmptySpace() { for (int x = 0; x < Width; x++) for (int z = 0; z < Height; z++) if (Map[x, z] == 0) return new Vector2I(x, z); return new Vector2I(1, 1); }
	
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
