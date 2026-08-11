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

	private string _cachedHostIp = "";

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
		string localIp = GetLocalIPv4Address();
		SetLobbyState(false, false, $"Status: Disconnected (Tu IP Local: {localIp})");

		// Obtener IP pública en segundo plano para la UI
		_ = FetchPublicIpForLobbyInitAsync();
	}

	private async System.Threading.Tasks.Task FetchPublicIpForLobbyInitAsync()
	{
		string pubIp = await GetPublicIPv4AddressAsync();
		if (!string.IsNullOrEmpty(pubIp) && pubIp != "127.0.0.1")
		{
			_cachedHostIp = pubIp;
			string localIp = GetLocalIPv4Address();
			SetLobbyState(false, false, $"Status: Disconnected (IP Pública: {pubIp} | IP Local: {localIp})");
		}
	}

	public string GetLocalIPv4Address()
	{
		foreach (string ip in IP.GetLocalAddresses())
		{
			if (ip.Contains(":") || ip.StartsWith("127.")) continue;
			if (ip.StartsWith("192.168.") || ip.StartsWith("10.") || ip.StartsWith("172."))
			{
				return ip;
			}
		}
		return "127.0.0.1";
	}

	public async System.Threading.Tasks.Task<string> GetPublicIPv4AddressAsync()
	{
		try
		{
			using (var httpClient = new System.Net.Http.HttpClient())
			{
				httpClient.Timeout = TimeSpan.FromSeconds(3);
				string response = await httpClient.GetStringAsync("https://api.ipify.org");
				string ip = response.Trim();
				if (!string.IsNullOrWhiteSpace(ip) && !ip.Contains(":"))
				{
					GD.Print($"[Network] IP Pública obtenida vía ipify: {ip}");
					return ip;
				}
			}
		}
		catch (Exception ex)
		{
			GD.Print($"[Network] Falló obtención de IP pública vía ipify: {ex.Message}");
		}

		try
		{
			using (var httpClient = new System.Net.Http.HttpClient())
			{
				httpClient.Timeout = TimeSpan.FromSeconds(3);
				string response = await httpClient.GetStringAsync("https://icanhazip.com");
				string ip = response.Trim();
				if (!string.IsNullOrWhiteSpace(ip) && !ip.Contains(":"))
				{
					GD.Print($"[Network] IP Pública obtenida vía icanhazip: {ip}");
					return ip;
				}
			}
		}
		catch { }

		return GetLocalIPv4Address();
	}

	private string SetupUPnP(int port)
	{
		try
		{
			var upnp = new Upnp();
			int discoverResult = upnp.Discover();
			if (discoverResult == (int)Upnp.UpnpResult.Success)
			{
				if (upnp.GetGateway() != null && upnp.GetGateway().IsValidGateway())
				{
					int mapResult = upnp.AddPortMapping(port, port, "MazeSurvivor", "UDP");
					if (mapResult == (int)Upnp.UpnpResult.Success)
					{
						string extIp = upnp.QueryExternalAddress();
						GD.Print($"[UPnP] Puerto {port} mapeado exitosamente. IP Externa: {extIp}");
						return extIp;
					}
				}
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr("[UPnP] Discovery no soportado o falló: " + ex.Message);
		}
		return null;
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
			return RoomCodeManager.RoomCodeToIp(ipInput.Text);
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

	public void _on_solo_button_down()
	{
		_joiningAsSpectator = false;
		int port = GetTargetPort();
		this.peer = new ENetMultiplayerPeer();
		var error = this.peer.CreateServer(port, 1);

		if (error != Error.Ok)
		{
			var offlinePeer = new OfflineMultiplayerPeer();
			Multiplayer.MultiplayerPeer = offlinePeer;
		}
		else
		{
			this.peer.Host.Compress(this.COMPRESSION_TYPE);
			Multiplayer.MultiplayerPeer = this.peer;
		}

		GameManager.Players.Clear();
		var nameInput = GetLobbyNode<LineEdit>("LineEdit");
		string hostName = (nameInput != null && !string.IsNullOrWhiteSpace(nameInput.Text)) ? nameInput.Text : "Jugador Local";
		sendPlayerInformation(hostName, HOST_ID, false);
		startGame();
	}

	public async void _on_host_button_down()
	{
		_joiningAsSpectator = false;
		int port = GetTargetPort();
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

		// Mapeo UPnP primero
		string upnpIp = SetupUPnP(port);

		SetLobbyState(true, true, $"Status: Obteniendo IP pública...");
		string publicIp = await GetPublicIPv4AddressAsync();
		if ((string.IsNullOrWhiteSpace(publicIp) || publicIp == "127.0.0.1") && !string.IsNullOrEmpty(upnpIp))
		{
			publicIp = upnpIp;
		}
		_cachedHostIp = publicIp;

		string roomCode = RoomCodeManager.IpToRoomCode(publicIp);
		GD.Print($"Hosting server on {publicIp}:{port} (Room Code: {roomCode})...");

		SetLobbyState(true, true, $"Status: Servidor Activo ({publicIp}:{port}) | CÓDIGO DE SALA: {roomCode}");
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

	public void _on_back_to_menu_button_down()
	{
		ReturnToLobby("Status: Disconnected");
		GetTree().ChangeSceneToFile("res://src/ui/login/menu.tscn");
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
		// Launch the game in all clients involved with a synchronized maze seed.
		int mazeSeed = (int)GD.Randi();
		Rpc(nameof(startGame), mazeSeed);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public async void startGame(int mazeSeed = 0)
	{	
		if (mazeSeed == 0) mazeSeed = (int)GD.Randi();

		// 1. Mostrar pantalla de carga inmediatamente
		var loadingScene = GD.Load<PackedScene>("res://src/ui/loading/LoadingScreen.tscn");
		LoadingScreen loadingScreen = null;
		if (loadingScene != null)
		{
			loadingScreen = loadingScene.Instantiate<LoadingScreen>();
			GetTree().Root.AddChild(loadingScreen);
			loadingScreen.SetStatus("Iniciando generación del laberinto...");
			loadingScreen.SetProgress(20f);
		}

		this.Hide();

		// Renderizar primer frame de la pantalla de carga antes de la generación intensiva
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		if (loadingScreen != null)
		{
			loadingScreen.SetStatus("Construyendo muros, salas y navegación 3D...");
			loadingScreen.SetProgress(55f);
		}
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		// 2. Instanciar y construir el laberinto
		var mazeNode = ResourceLoader.Load<PackedScene>("res://src/maze/maze.tscn").Instantiate<Maze>();
		mazeNode.MazeSeed = mazeSeed;
		mazeNode.Name = "ActiveGameScene";

		if (loadingScreen != null)
		{
			loadingScreen.SetStatus("Ubicando mochilas, botiquines y enemigos...");
			loadingScreen.SetProgress(85f);
		}
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		GetTree().Root.AddChild(mazeNode);

		if (loadingScreen != null)
		{
			loadingScreen.SetStatus("¡Laberinto generado! Entrando al juego...");
			loadingScreen.SetProgress(100f);
			await ToSignal(GetTree().CreateTimer(0.3f), SceneTreeTimer.SignalName.Timeout);
			await loadingScreen.FadeOutAndFreeAsync();
		}
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
