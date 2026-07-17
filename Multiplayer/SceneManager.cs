using Godot;
using System;

// Currently only used to handle the process of spawning players and asigning them their authority ID.
public partial class SceneManager : Node
{
	[Export]
	private PackedScene playerScene;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		int index = 0;
		foreach (var item in GameManager.Players)
		{
			// The spawnpoints is scalable due to the fact that it uses a group of 3D nodes.
			// NOTE (DiGiorgio-L): it currently is only set up to be used with 2 players; that means only two spawnPoints. 
			Player currentPlayer = playerScene.Instantiate<Player>();
			currentPlayer.Name = item.Id.ToString();
			currentPlayer.SetMultiplayerAuthority(item.Id); // TEST
			AddChild(currentPlayer);
			foreach (Node3D spawnPoint in GetTree().GetNodesInGroup("PlayerSpawnPoints"))
			{
				if (int.Parse(spawnPoint.Name) == index)
				{
					currentPlayer.GlobalPosition = spawnPoint.GlobalPosition;
				}
			}
			index ++;
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
