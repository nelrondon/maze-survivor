using Godot;
using System;

public partial class Door : Node3D
{
	private bool _isTimerStarted = false;
	private bool _timeIsReady = false;
	private float _timer = 0.0f;
	private const float UnlockTime = 120.0f; // 2 minutos (120 segundos)
	
	private Label _timerLabel;

	public override void _Ready()
	{
		AddToGroup("Door");

		// Creamos un Label de UI en tiempo de ejecución para mostrar el temporizador en una esquina (ej. superior derecha)
		var canvasLayer = new CanvasLayer();
		_timerLabel = new Label();
		_timerLabel.Text = "";
		
		// Posicionar en la esquina superior derecha con un pequeño margen
		_timerLabel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		_timerLabel.OffsetLeft = -250;
		_timerLabel.OffsetTop = 20;
		_timerLabel.OffsetRight = -20;
		_timerLabel.OffsetBottom = 60;
		
		// Estilo opcional para que se vea bien en pantalla
		_timerLabel.AddThemeFontSizeOverride("font_size", 24);
		
		canvasLayer.AddChild(_timerLabel);
		AddChild(canvasLayer);
	}

	public override void _Process(double delta)
	{
		if (_isTimerStarted && !_timeIsReady)
		{
			_timer += (float)delta;
			float remainingTime = UnlockTime - _timer;

			int minutes = Mathf.FloorToInt(remainingTime / 60);
			int seconds = Mathf.FloorToInt(remainingTime % 60);

			// Actualizar el texto en la esquina en formato MM:SS
			if (_timerLabel != null)
			{
				_timerLabel.Text = $"Escape: {minutes:D2}:{seconds:D2}";
			}

			if (_timer >= UnlockTime)
			{
				_timeIsReady = true;
				_isTimerStarted = false;
				
				if (_timerLabel != null)
				{
					_timerLabel.Text = "¡Puerta Lista! Interactúa de nuevo";
				}
				
				GD.Print("🔔 ¡Los 2 minutos han transcurrido! Regresa a la puerta e interactúa de nuevo para salir.");
			}
		}
	}

	public void interact(Node3D interactor)
	{
		GD.Print("🚪 ¡Puerta detectó la interacción del jugador!");

		if (interactor == null) return;

		bool hasKey = false;
		var keyProp = interactor.Get("HasKey");
		if (keyProp.VariantType != Variant.Type.Nil)
		{
			hasKey = (bool)keyProp;
		}

		GD.Print($"🚪 Estado de HasKey: {hasKey}");

		if (hasKey)
		{
			// Si el tiempo ya pasó, esta es la SEGUNDA interacción para salir definitivamente
			if (_timeIsReady)
			{
				GD.Print("🎉 ¡VICTORIA! Abriendo puerta definitivamente...");
				long winnerPeerId = Multiplayer.HasMultiplayerPeer() ? interactor.GetMultiplayerAuthority() : 1;

				if (Multiplayer.HasMultiplayerPeer())
				{
					Rpc(nameof(RpcOnDoorOpened), winnerPeerId);
				}
				else
				{
					RpcOnDoorOpened(winnerPeerId);
				}
			}
			// Si el temporizador ya empezó pero sigue corriendo
			else if (_isTimerStarted)
			{
				float remainingTime = UnlockTime - _timer;
				int mins = Mathf.FloorToInt(remainingTime / 60);
				int secs = Mathf.FloorToInt(remainingTime % 60);
				GD.Print($"⏱️ El temporizador está activo... Faltan {mins} minutos y {secs} segundos para poder abrir.");
			}
			// Si es la PRIMERA vez que interactúa
			else
			{
				if (Multiplayer != null && Multiplayer.HasMultiplayerPeer() && Multiplayer.MultiplayerPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected)
				{
					Rpc(nameof(RpcStartTimer));
				}
				else
				{
					RpcStartTimer();
				}
			}
		}
		else
		{
			GD.Print("🔒 Se necesita la llave.");
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void RpcStartTimer()
	{
		_isTimerStarted = true;
		_timer = 0.0f;
		GD.Print("🔑 Llave aceptada. Cuenta atrás de 2 minutos iniciada. ¡Ya puedes esconderte!");
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void RpcOnDoorOpened(long winnerId)
	{
		long localPeerId = Multiplayer.HasMultiplayerPeer() ? Multiplayer.GetUniqueId() : 1;
		bool isWinner = (localPeerId == winnerId);

		string winnerIdStr = winnerId.ToString();
		string partidaId = "PARTIDA_LOBBY_ACTIVA";

		// Disparar liquidación de pozo del ganador y apuestas de espectadores en Supabase
		_ = SupabaseManager.Instance.LiquidarPartidaAsync(partidaId, winnerIdStr);

		if (isWinner)
		{
			EndGameUI.ShowResult(this, true, "¡VICTORIA!", "¡Felicidades! Has logrado escapar del laberinto. Se ha transferido el pozo acumulado a tu billetera.");
		}
		else
		{
			EndGameUI.ShowResult(this, false, "¡HAS PERDIDO!", "Otro jugador ha logrado escapar por la puerta antes que tú. Las apuestas han sido procesadas.");
		}
	}

}
