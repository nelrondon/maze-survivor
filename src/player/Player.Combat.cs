using Godot;

public partial class Player {
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void hit(float damage) {
		hit(damage, null);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void hit(float damage, Node3D attacker) {
		GD.Print($"[Player {Name}] Procesando impacto de arma (Daño: {damage}, Atacante: {attacker?.Name ?? "Desconocido"})");
		int targetPeer = GetMultiplayerAuthority();
		if (Multiplayer != null && Multiplayer.HasMultiplayerPeer() && Multiplayer.MultiplayerPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected) {
			RpcId(targetPeer, nameof(RpcReceiveDamage), damage);
		} else {
			RpcReceiveDamage(damage);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void RpcReceiveDamage(float damage) {
		if (Multiplayer != null && Multiplayer.HasMultiplayerPeer() && Multiplayer.MultiplayerPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected && !IsMultiplayerAuthority()) {
			return;
		}

		modify_stat(0, -damage);
		TakeDamage();

		if (Multiplayer != null && Multiplayer.HasMultiplayerPeer() && IsMultiplayerAuthority()) {
			float currentHp = get_stat(0);
			Rpc(nameof(RpcSyncHealth), currentHp);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	private void RpcSyncHealth(float newHp) {
		_stats[0] = newHp;
		EmitSignal(SignalName.stats_changed);
	}

	public void TakeDamage() {
		if (!_IsLocallyControlled()) return;
		
		if (_hudFace != null && _hudFaceDamageTexture != null) _hudFace.Texture = _hudFaceDamageTexture;
		
		GD.Print($"[Player {Name}] ¡Recibió daño! Nueva salud: {get_stat(0)}");
	}
}
