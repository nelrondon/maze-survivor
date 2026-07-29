using Godot;
using System;
using System.Net.Sockets;

public partial class LobbyHandler : Control
{
	// Constants
	[Export]
	private int PORT = 8910;

	[Export]
	private string ADDRESS = "127.0.0.1";

	[Export]
	private int MAX_CLIENTS = 4;

	private int HOST_ID = 1;

	private ENetConnection.CompressionMode COMPRESSION_TYPE = ENetConnection.CompressionMode.RangeCoder;

	private ENetMultiplayerPeer peer;
	private ItemList playerList;

	private T GetLobbyNode<T>(string nodeName) where T : Node
	{
		return GetNodeOrNull<T>("%" + nodeName) ?? (FindChild(nodeName) as T);
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		playerList = GetLobbyNode<ItemList>("PlayerList");
		Multiplayer.PeerConnected += PeerConnected;
		Multiplayer.PeerDisconnected += PeerDisconnected;
		Multiplayer.ConnectedToServer += ConnectedToServer;
		Multiplayer.ConnectionFailed += ConnectionFailed;
		Multiplayer.ServerDisconnected += ServerDisconnected;

		UpdatePlayerListUI();
		SetLobbyState(false, false);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void SetLobbyState(bool isConnected, bool isHost, string statusText = "")
	{
		var connectionContainer = GetLobbyNode<Control>("ConnectionContainer");
		var lobbyActionContainer = GetLobbyNode<Control>("LobbyActionContainer");
		var startGameButton = GetLobbyNode<Button>("StartGame");
		var statusLabel = GetLobbyNode<Label>("StatusLabel");

		if (connectionContainer != null)
		{
			connectionContainer.Visible = !isConnected;
		}

		if (lobbyActionContainer != null)
		{
			lobbyActionContainer.Visible = isConnected;
		}

		if (startGameButton != null)
		{
			startGameButton.Visible = isConnected && isHost;
		}

		if (statusLabel != null)
		{
			if (!string.IsNullOrEmpty(statusText))
			{
				statusLabel.Text = statusText;
			}
			else
			{
				statusLabel.Text = isConnected ? (isHost ? "Status: Hosting server..." : "Status: Connected to lobby") : "Status: Disconnected";
			}
		}
	}

	// Signals handling
	private void ConnectedToServer()
	{
		GD.Print("Connected to server!!");
		var nameInput = GetLobbyNode<LineEdit>("LineEdit");
		string playerName = nameInput != null ? nameInput.Text : "";
		SetLobbyState(true, false, "Status: Connected to server!");
		RpcId(HOST_ID, "sendPlayerInformation", playerName, Multiplayer.GetUniqueId());
	}

	private void ConnectionFailed()
	{
		GD.Print("Connection failed!!");
		SetLobbyState(false, false, "Status: Connection failed!");
	}

	private void ServerDisconnected()
	{
		GD.Print("Server disconnected!!");
		GameManager.Players.Clear();
		UpdatePlayerListUI();
		SetLobbyState(false, false, "Status: Server disconnected!");
	}

	private void PeerConnected(long id)
	{
		GD.Print("Peer connected: " + id.ToString());
	}

	private void PeerDisconnected(long id)
	{
		GD.Print("Peer disconnected: " + id.ToString());
		removePlayerFromList((int)id);

		if (Multiplayer.IsServer())
		{
			Rpc("removePlayerInformation", (int)id);
		}
	}

	private string GetTargetAddress()
	{
		var ipInput = GetLobbyNode<LineEdit>("IpLineEdit");
		if (ipInput != null && !string.IsNullOrWhiteSpace(ipInput.Text))
		{
			return ipInput.Text.Trim();
		}
		return this.ADDRESS;
	}

	private int GetTargetPort()
	{
		var portInput = GetLobbyNode<LineEdit>("PortLineEdit");
		if (portInput != null && int.TryParse(portInput.Text.Trim(), out int customPort) && customPort > 0)
		{
			return customPort;
		}
		return this.PORT;
	}

	public void _on_host_button_down()
	{
		int port = GetTargetPort();
		// Create the server.
		this.peer = new ENetMultiplayerPeer();
		var error = this.peer.CreateServer(port, this.MAX_CLIENTS);

		if (error != Error.Ok)
		{
			GD.Print("[ERROR]: cannot host!!\n" + error.ToString());
			SetLobbyState(false, false, $"[ERROR] Cannot host on port {port}");
			return;
		}
		this.peer.Host.Compress(this.COMPRESSION_TYPE);

		Multiplayer.MultiplayerPeer = this.peer;
		GD.Print($"Waiting for players on port {port}...!");

		SetLobbyState(true, true, $"Status: Hosting on port {port}...");
		GameManager.Players.Clear();
		var nameInput = GetLobbyNode<LineEdit>("LineEdit");
		string hostName = nameInput != null ? nameInput.Text : "";
		sendPlayerInformation(hostName, HOST_ID);
	}

	public void _on_join_button_down()
	{
		string address = GetTargetAddress();
		int port = GetTargetPort();

		// Create a client session.
		this.peer = new ENetMultiplayerPeer();
		this.peer.CreateClient(address, port);
		this.peer.Host.Compress(this.COMPRESSION_TYPE);

		Multiplayer.MultiplayerPeer = this.peer;
		GD.Print($"Joining game at {address}:{port}!!");
		SetLobbyState(true, false, $"Status: Connecting to {address}:{port}...");
	}

	public void _on_leave_button_down()
	{
		if (Multiplayer.MultiplayerPeer != null)
		{
			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null;
		}
		GameManager.Players.Clear();
		UpdatePlayerListUI();
		SetLobbyState(false, false, "Status: Disconnected");
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
	[Rpc(MultiplayerApi.RpcMode.AnyPeer /*, CallLocal = true*/ )]
	private void sendPlayerInformation(string name, int id)
	{
		PlayerInfo playerInfo = new PlayerInfo()
		{
			Name = name,
			Id = id
		};

		int existingIndex = GameManager.Players.FindIndex(p => p.Id == id);
		if (existingIndex >= 0)
		{
			GameManager.Players[existingIndex] = playerInfo;
		}
		else
		{
			GameManager.Players.Add(playerInfo);
		}

		UpdatePlayerListUI();

		if (Multiplayer.IsServer())
		{
			foreach (var item in GameManager.Players)
			{
				Rpc("sendPlayerInformation", item.Name, item.Id);
			}
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void removePlayerInformation(int id)
	{
		removePlayerFromList(id);
	}

	private void removePlayerFromList(int id)
	{
		GameManager.Players.RemoveAll(p => p.Id == id);
		UpdatePlayerListUI();
	}

	private void UpdatePlayerListUI()
	{
		if (playerList == null)
			return;

		playerList.Clear();
		foreach (var player in GameManager.Players)
		{
			string text = $"{player.Name} (ID: {player.Id})";
			if (player.Id == HOST_ID)
			{
				text += " [Host]";
			}
			playerList.AddItem(text);
		}
	}
}

