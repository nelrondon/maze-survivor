using Godot;
using System;
using System.Linq;
using System.Net.Sockets;

public partial class LobbyHandler : Control
{
	// Constants
	[Export]
	private int PORT = 8910;

	[Export]
	private string ADDRESS = "127.0.0.1";

	[Export]
	private int MAX_PLAYERS = 4;

	[Export]
	private int MAX_SPECTATORS = 4;

	private int HOST_ID = 1;

	private bool _joiningAsSpectator = false;

	private ENetConnection.CompressionMode COMPRESSION_TYPE = ENetConnection.CompressionMode.RangeCoder;

	private ENetMultiplayerPeer peer;
	private ItemList playerList;
	private ItemList spectatorList;
	private Label playerListTitle;
	private Label spectatorListTitle;

	private T GetLobbyNode<T>(string nodeName) where T : Node
	{
		return GetNodeOrNull<T>("%" + nodeName) ?? (FindChild(nodeName) as T);
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		playerList = GetLobbyNode<ItemList>("PlayerList");
		spectatorList = GetLobbyNode<ItemList>("SpectatorList");
		playerListTitle = GetLobbyNode<Label>("PlayerListTitle");
		spectatorListTitle = GetLobbyNode<Label>("SpectatorListTitle");

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
		GD.Print($"Connected to server! Role: {(_joiningAsSpectator ? "Spectator" : "Player")}");
		var nameInput = GetLobbyNode<LineEdit>("LineEdit");
		string playerName = nameInput != null ? nameInput.Text : "";
		SetLobbyState(true, false, "Status: Connected to server!");
		RpcId(HOST_ID, "sendPlayerInformation", playerName, Multiplayer.GetUniqueId(), _joiningAsSpectator);
	}

	private void ConnectionFailed()
	{
		GD.Print("Connection failed!!");
		ReturnToLobby("Status: Connection failed!");
	}

	private void ServerDisconnected()
	{
		GD.Print("Server disconnected!!");
		ReturnToLobby("Status: Host disconnected. Returned to lobby.");
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
		_joiningAsSpectator = false;
		int port = GetTargetPort();
		// Create the server. Total capacity = MAX_PLAYERS + MAX_SPECTATORS - 1 client peers
		int maxClients = Math.Max(1, (this.MAX_PLAYERS + this.MAX_SPECTATORS) - 1);
		this.peer = new ENetMultiplayerPeer();
		var error = this.peer.CreateServer(port, maxClients);

		if (error != Error.Ok)
		{
			GD.Print("[ERROR]: cannot host!!\n" + error.ToString());
			SetLobbyState(false, false, $"[ERROR] Cannot host on port {port}");
			return;
		}
		this.peer.Host.Compress(this.COMPRESSION_TYPE);

		Multiplayer.MultiplayerPeer = this.peer;
		GD.Print($"Waiting for players & spectators on port {port}...!");

		SetLobbyState(true, true, $"Status: Hosting on port {port}...");
		GameManager.Players.Clear();
		var nameInput = GetLobbyNode<LineEdit>("LineEdit");
		string hostName = nameInput != null ? nameInput.Text : "";
		sendPlayerInformation(hostName, HOST_ID, false);
	}

	public void _on_join_button_down()
	{
		_joiningAsSpectator = false;
		JoinServer();
	}

	public void _on_join_spectator_button_down()
	{
		_joiningAsSpectator = true;
		JoinServer();
	}

	private void JoinServer()
	{
		string address = GetTargetAddress();
		int port = GetTargetPort();

		// Create a client session.
		this.peer = new ENetMultiplayerPeer();
		this.peer.CreateClient(address, port);
		this.peer.Host.Compress(this.COMPRESSION_TYPE);

		Multiplayer.MultiplayerPeer = this.peer;
		string mode = _joiningAsSpectator ? "spectator" : "player";
		GD.Print($"Joining game at {address}:{port} as {mode}!!");
		SetLobbyState(true, false, $"Status: Connecting to {address}:{port} ({mode})...");
	}

	public void _on_leave_button_down()
	{
		ReturnToLobby("Status: Disconnected");
	}

	public void ReturnToLobby(string statusMessage = "Status: Disconnected")
	{
		// 1. Clean up active game scenes
		var activeGame = GetTree().Root.GetNodeOrNull("ActiveGameScene");
		if (activeGame != null)
		{
			activeGame.QueueFree();
		}

		foreach (Node child in GetTree().Root.GetChildren())
		{
			if (child is SceneManager)
			{
				child.QueueFree();
			}
		}

		// 2. Safely close multiplayer peer
		if (Multiplayer.MultiplayerPeer != null)
		{
			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null;
		}

		// 3. Clear player list state
		GameManager.Players.Clear();
		UpdatePlayerListUI();

		// 4. Restore input mouse mode and reveal Lobby UI
		Input.MouseMode = Input.MouseModeEnum.Visible;
		this.Show();
		SetLobbyState(false, false, statusMessage);
	}

	public void _on_start_game_button_down()
	{
		// Launch the game in all clients involved.
		Rpc("startGame");
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void startGame()
	{	
		var scene = ResourceLoader.Load<PackedScene>("res://maze.tscn").Instantiate();
		scene.Name = "ActiveGameScene";
		GetTree().Root.AddChild(scene);
		this.Hide();
	}

	// Send player information across multiple locations/scenes, etc.
	[Rpc(MultiplayerApi.RpcMode.AnyPeer /*, CallLocal = true*/ )]
	private void sendPlayerInformation(string name, int id, bool isSpectator)
	{
		PlayerInfo playerInfo = new PlayerInfo()
		{
			Name = name,
			Id = id,
			IsSpectator = isSpectator
		};

		int existingIndex = GameManager.Players.FindIndex(p => p.Id == id);
		if (existingIndex >= 0)
		{
			GameManager.Players[existingIndex] = playerInfo;
		}
		else
		{
			// Server-side validation: disconnect if player or spectator capacity is reached
			if (Multiplayer.IsServer())
			{
				int activeCount = GameManager.Players.Count(p => !p.IsSpectator);
				int specCount = GameManager.Players.Count(p => p.IsSpectator);

				if (!isSpectator && activeCount >= MAX_PLAYERS)
				{
					GD.PrintErr($"[Server] Connection rejected: Active player limit reached ({activeCount}/{MAX_PLAYERS}). Peer ID: {id}");
					if (peer != null && id != HOST_ID) peer.DisconnectPeer(id);
					return;
				}
				else if (isSpectator && specCount >= MAX_SPECTATORS)
				{
					GD.PrintErr($"[Server] Connection rejected: Spectator limit reached ({specCount}/{MAX_SPECTATORS}). Peer ID: {id}");
					if (peer != null && id != HOST_ID) peer.DisconnectPeer(id);
					return;
				}
			}
			GameManager.Players.Add(playerInfo);
		}

		UpdatePlayerListUI();

		if (Multiplayer.IsServer())
		{
			foreach (var item in GameManager.Players)
			{
				Rpc("sendPlayerInformation", item.Name, item.Id, item.IsSpectator);
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
		var activePlayers = GameManager.Players.Where(p => !p.IsSpectator).ToList();
		var spectators = GameManager.Players.Where(p => p.IsSpectator).ToList();

		if (playerListTitle != null)
		{
			playerListTitle.Text = $"Connected Players ({activePlayers.Count}/{MAX_PLAYERS}):";
		}

		if (spectatorListTitle != null)
		{
			spectatorListTitle.Text = $"Spectators ({spectators.Count}/{MAX_SPECTATORS}):";
		}

		if (playerList != null)
		{
			playerList.Clear();
			foreach (var player in activePlayers)
			{
				string text = $"{player.Name} (ID: {player.Id})";
				if (player.Id == HOST_ID)
				{
					text += " [Host]";
				}
				playerList.AddItem(text);
			}
		}

		if (spectatorList != null)
		{
			spectatorList.Clear();
			foreach (var spectator in spectators)
			{
				string text = $"{spectator.Name} (ID: {spectator.Id})";
				if (spectator.Id == HOST_ID)
				{
					text += " [Host]";
				}
				spectatorList.AddItem(text);
			}
		}
	}
}
