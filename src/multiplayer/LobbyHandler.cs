using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

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

	// Betting System UI Elements inside Lobby
	private Control bettingVBox;
	private Label labelSaldoBet;
	private OptionButton optionJugadorBet;
	private OptionButton optionMercadoBet;
	private LineEdit inputMontoBet;
	private Label labelGananciaBet;
	private Label labelMensajeBet;
	private Button buttonApostarBet;

	private decimal cuotaActualBet = 2.50m;

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

		// Referencias del sistema de apuestas integrado
		bettingVBox        = GetLobbyNode<Control>("BettingVBox");
		labelSaldoBet      = GetLobbyNode<Label>("LabelSaldoBet");
		optionJugadorBet  = GetLobbyNode<OptionButton>("OptionButtonJugadorBet");
		optionMercadoBet  = GetLobbyNode<OptionButton>("OptionButtonMercadoBet");
		inputMontoBet     = GetLobbyNode<LineEdit>("LineEditMontoBet");
		labelGananciaBet  = GetLobbyNode<Label>("LabelGananciaBet");
		labelMensajeBet   = GetLobbyNode<Label>("LabelMensajeBet");
		buttonApostarBet  = GetLobbyNode<Button>("ButtonApostarBet");

		if (bettingVBox != null)
		{
			bettingVBox.Visible = false; // Oculto por defecto hasta unirse como espectador
		}

		if (buttonApostarBet != null)
		{
			buttonApostarBet.Pressed += OnButtonApostarBetPressed;
		}

		if (inputMontoBet != null)
		{
			inputMontoBet.TextChanged += OnMontoBetChanged;
		}

		if (optionMercadoBet != null)
		{
			optionMercadoBet.ItemSelected += OnMercadoBetSelected;
			ConfigurarMercadosBetting();
		}

		Multiplayer.PeerConnected += PeerConnected;
		Multiplayer.PeerDisconnected += PeerDisconnected;
		Multiplayer.ConnectedToServer += ConnectedToServer;
		Multiplayer.ConnectionFailed += ConnectionFailed;
		Multiplayer.ServerDisconnected += ServerDisconnected;

		UpdatePlayerListUI();
		ActualizarSaldoBettingUI();
		string localIp = GetLocalIPv4Address();
		SetLobbyState(false, false, $"Status: Disconnected (Tu IP Local: {localIp})");

		_ = FetchPublicIpForLobbyInitAsync();
	}

	private void ConfigurarMercadosBetting()
	{
		if (optionMercadoBet == null) return;

		optionMercadoBet.Clear();
		optionMercadoBet.AddItem("Ganador de la Partida (x2.50)", 0);
		optionMercadoBet.AddItem("Primera Kill (x3.00)", 1);
		optionMercadoBet.AddItem("Primera Llave (x2.10)", 2);
	}

	private void OnMercadoBetSelected(long index)
	{
		switch (index)
		{
			case 0: cuotaActualBet = 2.50m; break;
			case 1: cuotaActualBet = 3.00m; break;
			case 2: cuotaActualBet = 2.10m; break;
		}
		CalcularGananciaBet();
	}

	private void OnMontoBetChanged(string text)
	{
		CalcularGananciaBet();
	}

	private void CalcularGananciaBet()
	{
		if (labelGananciaBet == null) return;

		if (decimal.TryParse(inputMontoBet?.Text, out decimal monto) && monto > 0)
		{
			decimal ganancia = monto * cuotaActualBet;
			labelGananciaBet.Text = $"Ganancia Potencial: ${ganancia:F2} (Cuota: {cuotaActualBet:F2})";
		}
		else
		{
			labelGananciaBet.Text = $"Ganancia Potencial: $0.00 (Cuota: {cuotaActualBet:F2})";
		}
	}

	private void ActualizarSaldoBettingUI()
	{
		var mgr = SupabaseManager.Instance;
		decimal saldo = mgr != null ? mgr.GetSaldo() : 100.00m;
		if (labelSaldoBet != null)
		{
			labelSaldoBet.Text = $"Saldo Disponible: ${saldo:F2}";
		}
	}


	private async void OnButtonApostarBetPressed()
	{
		if (optionJugadorBet == null || optionJugadorBet.ItemCount == 0 || optionJugadorBet.Selected < 0)
		{
			MostrarMensajeBetting("No hay jugadores válidos en la sala para apostar.", true);
			return;
		}

		if (!decimal.TryParse(inputMontoBet?.Text, out decimal monto) || monto <= 0)
		{
			MostrarMensajeBetting("Ingresa un monto válido a apostar.", true);
			return;
		}

		var activePlayers = GameManager.Players.Where(p => !p.IsSpectator).ToList();
		string jugadorPronosticadoId = activePlayers.Count > 0 && optionJugadorBet.Selected < activePlayers.Count
			? activePlayers[optionJugadorBet.Selected].Id.ToString()
			: Guid.NewGuid().ToString();

		string tipoMercado = optionMercadoBet != null ? optionMercadoBet.GetItemText(optionMercadoBet.Selected) : "Ganador";

		// ID de la partida activa actual en la sala
		string partidaId = !string.IsNullOrEmpty(_cachedHostIp) ? _cachedHostIp : "PARTIDA_LOBBY_ACTIVA";

		MostrarMensajeBetting("Procesando apuesta...", false);
		if (buttonApostarBet != null) buttonApostarBet.Disabled = true;

		var (success, error) = await SupabaseManager.Instance.RealizarApuestaAsync(
			partidaId,
			jugadorPronosticadoId,
			tipoMercado,
			monto,
			cuotaActualBet
		);

		if (buttonApostarBet != null) buttonApostarBet.Disabled = false;

		if (success)
		{
			ActualizarSaldoBettingUI();
			MostrarMensajeBetting($"¡Apuesta realizada! Ganancia potencial: ${monto * cuotaActualBet:F2}", false);
			if (inputMontoBet != null) inputMontoBet.Text = "";
			CalcularGananciaBet();
		}
		else
		{
			MostrarMensajeBetting($"Error al apostar: {error}", true);
		}
	}

	private void MostrarMensajeBetting(string text, bool esError)
	{
		if (labelMensajeBet != null)
		{
			labelMensajeBet.Text = text;
			labelMensajeBet.Modulate = esError ? new Color(1, 0.4f, 0.4f) : new Color(0.4f, 1, 0.4f);
		}
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
						return upnp.QueryExternalAddress();
					}
				}
			}
		}
		catch { }
		return null;
	}

	private void SetLobbyState(bool isConnected, bool isHost, string statusText = "")
	{
		var connectionContainer = GetLobbyNode<Control>("ConnectionContainer");
		var lobbyActionContainer = GetLobbyNode<Control>("LobbyActionContainer");
		var startGameButton = GetLobbyNode<Button>("StartGame");
		var statusLabel = GetLobbyNode<Label>("StatusLabel");

		if (connectionContainer != null) connectionContainer.Visible = !isConnected;
		if (lobbyActionContainer != null) lobbyActionContainer.Visible = isConnected;
		if (startGameButton != null) startGameButton.Visible = isConnected && isHost;

		if (statusLabel != null)
		{
			if (!string.IsNullOrEmpty(statusText)) statusLabel.Text = statusText;
			else statusLabel.Text = isConnected ? (isHost ? "Status: Hosting server..." : "Status: Connected to lobby") : "Status: Disconnected";
		}
	}

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
		ReturnToLobby("Status: Connection failed!");
	}

	private void ServerDisconnected()
	{
		ReturnToLobby("Status: Host disconnected. Returned to lobby.");
	}

	private void PeerConnected(long id) { }

	private void PeerDisconnected(long id)
	{
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
		if (bettingVBox != null) bettingVBox.Visible = false;
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
		if (bettingVBox != null) bettingVBox.Visible = false;
		int port = GetTargetPort();
		int maxClients = Math.Max(1, (this.MAX_PLAYERS + this.MAX_SPECTATORS) - 1);
		this.peer = new ENetMultiplayerPeer();
		var error = this.peer.CreateServer(port, maxClients);

		if (error != Error.Ok)
		{
			SetLobbyState(false, false, $"[ERROR] Cannot host on port {port}");
			return;
		}
		this.peer.Host.Compress(this.COMPRESSION_TYPE);
		Multiplayer.MultiplayerPeer = this.peer;

		string upnpIp = SetupUPnP(port);
		SetLobbyState(true, true, $"Status: Obteniendo IP pública...");
		string publicIp = await GetPublicIPv4AddressAsync();
		if ((string.IsNullOrWhiteSpace(publicIp) || publicIp == "127.0.0.1") && !string.IsNullOrEmpty(upnpIp))
		{
			publicIp = upnpIp;
		}
		_cachedHostIp = publicIp;

		string roomCode = RoomCodeManager.IpToRoomCode(publicIp);
		SetLobbyState(true, true, $"Status: Servidor Activo ({publicIp}:{port}) | CÓDIGO DE SALA: {roomCode}");
		GameManager.Players.Clear();
		var nameInput = GetLobbyNode<LineEdit>("LineEdit");
		string hostName = nameInput != null ? nameInput.Text : "";
		sendPlayerInformation(hostName, HOST_ID, false);
	}

	public void _on_join_button_down()
	{
		_joiningAsSpectator = false;
		if (bettingVBox != null) bettingVBox.Visible = false;
		JoinServer();
	}

	public void _on_join_spectator_button_down()
	{
		_joiningAsSpectator = true;
		if (bettingVBox != null)
		{
			bettingVBox.Visible = true; // Mostrar panel de apuestas integrado en el Lobby
		}
		ActualizarSaldoBettingUI();
		JoinServer();
	}

	private void JoinServer()
	{
		string address = GetTargetAddress();
		int port = GetTargetPort();

		this.peer = new ENetMultiplayerPeer();
		this.peer.CreateClient(address, port);
		this.peer.Host.Compress(this.COMPRESSION_TYPE);

		Multiplayer.MultiplayerPeer = this.peer;
		string mode = _joiningAsSpectator ? "spectator" : "player";
		SetLobbyState(true, false, $"Status: Connecting to {address}:{port} ({mode})...");
	}

	public void _on_leave_button_down()
	{
		if (bettingVBox != null) bettingVBox.Visible = false;
		ReturnToLobby("Status: Disconnected");
	}

	public void _on_back_to_menu_button_down()
	{
		if (bettingVBox != null) bettingVBox.Visible = false;
		ReturnToLobby("Status: Disconnected");
		GetTree().ChangeSceneToFile("res://src/ui/login/menu.tscn");
	}

	public void ReturnToLobby(string statusMessage = "Status: Disconnected")
	{
		var activeGame = GetTree().Root.GetNodeOrNull("ActiveGameScene");
		if (activeGame != null) activeGame.QueueFree();

		foreach (Node child in GetTree().Root.GetChildren())
		{
			if (child is SceneManager) child.QueueFree();
		}

		if (Multiplayer.MultiplayerPeer != null)
		{
			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null;
		}

		GameManager.Players.Clear();
		UpdatePlayerListUI();

		Input.MouseMode = Input.MouseModeEnum.Visible;
		this.Show();
		SetLobbyState(false, false, statusMessage);
	}

	public void _on_start_game_button_down()
	{
		int mazeSeed = (int)GD.Randi();
		Rpc(nameof(startGame), mazeSeed);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public async void startGame(int mazeSeed = 0)
	{	
		if (mazeSeed == 0) mazeSeed = (int)GD.Randi();

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
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		if (loadingScreen != null)
		{
			loadingScreen.SetStatus("Construyendo muros, salas y navegación 3D...");
			loadingScreen.SetProgress(55f);
		}
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

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

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
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
			if (Multiplayer.IsServer())
			{
				int activeCount = GameManager.Players.Count(p => !p.IsSpectator);
				int specCount = GameManager.Players.Count(p => p.IsSpectator);

				if (!isSpectator && activeCount >= MAX_PLAYERS)
				{
					if (peer != null && id != HOST_ID) peer.DisconnectPeer(id);
					return;
				}
				else if (isSpectator && specCount >= MAX_SPECTATORS)
				{
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
				if (player.Id == HOST_ID) text += " [Host]";
				playerList.AddItem(text);
			}
		}

		if (spectatorList != null)
		{
			spectatorList.Clear();
			foreach (var spectator in spectators)
			{
				string text = $"{spectator.Name} (ID: {spectator.Id})";
				if (spectator.Id == HOST_ID) text += " [Host]";
				spectatorList.AddItem(text);
			}
		}

		// Actualizar dinámicamente el selector "Jugador Pronosticado" en el panel de apuestas
		if (optionJugadorBet != null)
		{
			optionJugadorBet.Clear();
			if (activePlayers.Count > 0)
			{
				foreach (var player in activePlayers)
				{
					optionJugadorBet.AddItem($"{player.Name} (ID: {player.Id})");
				}
			}
			else
			{
				optionJugadorBet.AddItem("Esperando jugadores en la sala...");
			}
		}
	}
}
