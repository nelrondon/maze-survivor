using Godot;
using System;
using System.Net.Sockets;

public partial class LobbyHandler : Control
{
	// Constants
	[Export]
	private int PORT = 8910;

	// TODO: add the ability to write a custom IP
	[Export]
	private string ADDRESS = "127.0.0.1";

	[Export]
	private int MAX_CLIENTS = 4;

	private int HOST_ID = 1;

	private ENetConnection.CompressionMode COMPRESSION_TYPE = ENetConnection.CompressionMode.RangeCoder;

	private ENetMultiplayerPeer peer;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Multiplayer.PeerConnected += PeerConnected;
		Multiplayer.PeerDisconnected += PeerDisconnected;
		Multiplayer.ConnectedToServer += ConnectedToServer;
		Multiplayer.ConnectionFailed += ConnectionFailed;
	}

  // Called every frame. 'delta' is the elapsed time since the previous frame.
  public override void _Process(double delta)
	{
	}

	// Signals handling
	private void ConnectedToServer()
  {
    GD.Print("Connected to server!!");
		RpcId(HOST_ID, "sendPlayerInformation", GetNode<LineEdit>("LineEdit").Text, Multiplayer.GetUniqueId());
  }

	 private void ConnectionFailed()
  {
    GD.Print("Connection failed!!");
  }

	 private void PeerConnected(long id)
  {
    GD.Print("Peer connected: " + id.ToString());
  }

	private void PeerDisconnected(long id)
  {
    GD.Print("Peer disconnected: " + id.ToString());
  }

	public void _on_host_button_down()
	{
		// Create the server.
		this.peer = new ENetMultiplayerPeer();
		var error = this.peer.CreateServer(this.PORT, this.MAX_CLIENTS);

		if (error != Error.Ok)
		{
			GD.Print("[ERROR]: cannot host!!\n" + error.ToString());
			return;
		}
		this.peer.Host.Compress(this.COMPRESSION_TYPE);

		Multiplayer.MultiplayerPeer = this.peer;
		GD.Print("Waiting for players...!");

		sendPlayerInformation(GetNode<LineEdit>("LineEdit").Text, HOST_ID);
	}

	public void _on_join_button_down()
	{
		// Create a client session.
		this.peer = new ENetMultiplayerPeer();
		this.peer.CreateClient(this.ADDRESS, this.PORT);
		this.peer.Host.Compress(this.COMPRESSION_TYPE);

		Multiplayer.MultiplayerPeer = this.peer;
		GD.Print("Joining game!!");
	}

	public void _on_start_game_button_down()
	{
		// Launch the game in all clients involved.
		Rpc("startGame");
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void startGame()
	{	
		// NOTE (DiGiorgio-L): Modify this to load a different scene. Right now it is set up to work with the test_scene.
		var scene = ResourceLoader.Load<PackedScene>("res://test/test_multiplayer_scene.tscn").Instantiate<SceneManager>();
		GetTree().Root.AddChild(scene);
		this.Hide();
	}

	// Send player information across multiple locations/scenes, etc.
	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void sendPlayerInformation(string name, int id)
	{
		PlayerInfo playerInfo = new PlayerInfo()
		{
			Name = name,
			Id = id
		};
		if (!GameManager.Players.Contains(playerInfo))
		{
			GameManager.Players.Add(playerInfo);
		}

		if (Multiplayer.IsServer())
		{
			foreach (var item in GameManager.Players)
			{
				Rpc("sendPlayerInformation", item.Name, item.Id);
			}
		}
	}

	
}
