using Godot;

public partial class KeyItem : Area3D
{
	[ExportGroup("Interacción")]
	[Export] public string PromptText = "Presiona E para recoger la llave";
	[Export] public string InteractAction = "interact"; // Nombre de la acción en Input Map

	private bool _isPlayerInside = false;
	private Node3D _playerNode = null;

	public override void _Ready()
	{
		// Conectar señales de detección
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	private void OnBodyEntered(Node3D body)
	{
		if (body.HasMethod("PickUpKey"))
		{
			if (Multiplayer != null && Multiplayer.HasMultiplayerPeer() && Multiplayer.MultiplayerPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected)
			{
				if (!body.IsMultiplayerAuthority()) return;
			}

			_isPlayerInside = true;
			_playerNode = body;
			GD.Print($"{PromptText}");
		}
	}

	private void OnBodyExited(Node3D body)
	{
		if (body == _playerNode)
		{
			_isPlayerInside = false;
			_playerNode = null;
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// Detecta la tecla 'E' solo si el jugador está dentro del rango
		if (_isPlayerInside && @event.IsActionPressed(InteractAction))
		{
			CollectKey();
		}
	}

	private void CollectKey()
	{
		if (_playerNode != null && _playerNode.HasMethod("PickUpKey"))
		{
			_playerNode.Call("PickUpKey");
			GD.Print("¡Llave recogida con éxito!");
			if (Multiplayer != null && Multiplayer.HasMultiplayerPeer() && Multiplayer.MultiplayerPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected)
			{
				Rpc(nameof(RpcRemoveKeyFromWorld));
			}
			else
			{
				QueueFree();
			}
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRemoveKeyFromWorld()
	{
		QueueFree();
	}
}
