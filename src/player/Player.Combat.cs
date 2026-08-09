using Godot;

public partial class Player {
	public void hit(float damage) {
		modify_stat(0, -damage);
	}

	public void TakeDamage() {
		if (!_IsLocallyControlled()) return;
		
		SetInputLocked(true);
		
		if (_hudFace != null && _hudFaceDamageTexture != null) _hudFace.Texture = _hudFaceDamageTexture;
		
		GD.Print("Player took damage. Controls locked.");
	}
}
