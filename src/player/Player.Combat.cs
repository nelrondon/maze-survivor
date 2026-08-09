using Godot;

public partial class Player {
	public void hit(float damage, Node3D attacker = null) {
		if (Multiplayer != null && Multiplayer.HasMultiplayerPeer() && Multiplayer.MultiplayerPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected) {
			Rpc(nameof(RpcTakeDamage), damage);
		} else {
			RpcTakeDamage(damage);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void RpcTakeDamage(float damage) {
		modify_stat(0, -damage);
		TakeDamage();
	}

	public void TakeDamage() {
		if (!_IsLocallyControlled()) return;
		
		SetInputLocked(true);
		
		if (_hudFace != null && _hudFaceDamageTexture != null) _hudFace.Texture = _hudFaceDamageTexture;
		
		GD.Print("Player took damage. Controls locked.");
	}
}
