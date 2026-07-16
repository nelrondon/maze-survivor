using Godot;

public partial class Player : CharacterBody3D {
	[Signal] public delegate void stats_changedEventHandler();

	[Export] private float _speed = 9.0f;
	[Export] private float _gravity = 9.8f;
	[Export] private float _jumpStrength = 4.0f;
	[Export] private float _mouseSensibility = 0.0005f;
	
	[Export] private Camera3D _gameCamera;
	[Export] private Node3D _characterVisual;
	[Export] private RayCast3D _interactionRayCast;
	[Export] private TextureRect _hudFace;
	[Export] private Texture2D _hudFaceDamageTexture;
	
	private float _pitch = 0.0f;
	private Vector3 _targetVelocity = Vector3.Zero;
	private bool _isLocked = false;
	
	private Node _statusManager;
	
	public override void _Ready() {
		_statusManager = GetNodeOrNull("StatusManager");
		if (_statusManager == null) {
			GD.Print("Player: Nodo StatusManager no encontrado. Se creará dinámicamente si es necesario.");
		}

		if (_gameCamera == null) _gameCamera = GetNodeOrNull<Camera3D>("Head/Camera3D");
		if (_characterVisual == null) _characterVisual = GetNodeOrNull<Node3D>("MeshInstance3D");
		if (_interactionRayCast == null) _interactionRayCast = GetNodeOrNull<RayCast3D>("Head/Camera3D/RayCast3D");

		if (IsMultiplayerAuthority()) {
			if (_gameCamera != null) _gameCamera.Current = true;
			if (_characterVisual != null) _characterVisual.Visible = false;
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}
		else {
			if (_gameCamera != null) _gameCamera.Current = false;
			if (_characterVisual != null) _characterVisual.Visible = true;
		}
	}
	
	public override void _Input(InputEvent @event) {
		if (!IsMultiplayerAuthority() || _isLocked) return;
		
		if (@event is InputEventMouseMotion mouseMotion) {
			RotateY(-mouseMotion.Relative.X * _mouseSensibility);
			
			_pitch = Mathf.Clamp(
				_pitch - mouseMotion.Relative.Y * _mouseSensibility, 
				Mathf.DegToRad(-89), 
				Mathf.DegToRad(89)
			);

			if (_gameCamera != null) {
				Vector3 cameraRotation = _gameCamera.Rotation;
				cameraRotation.X = _pitch;
				_gameCamera.Rotation = cameraRotation;
			}
		}

		if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape) Input.MouseMode = Input.MouseModeEnum.Visible;

		bool isInteractPressed = (@event is InputEventKey interactKey && interactKey.Pressed && interactKey.Keycode == Key.E) || 
			(InputMap.HasAction("interact") && @event.IsActionPressed("interact"));

		if (isInteractPressed) {
			if (_interactionRayCast != null && _interactionRayCast.IsColliding()) {
				GodotObject collider = _interactionRayCast.GetCollider();
				if (collider != null) collider.Call("interact", this);
			}
		}
	}

	public override void _PhysicsProcess(double delta) {
		if (!IsMultiplayerAuthority()) return;
		
		Vector3 direction = Vector3.Zero;

		if (!_isLocked) {
			if (Input.IsActionPressed("up")) direction -= Transform.Basis.Z;
			if (Input.IsActionPressed("down")) direction += Transform.Basis.Z;
			if (Input.IsActionPressed("left")) direction -= Transform.Basis.X;
			if (Input.IsActionPressed("right")) direction += Transform.Basis.X;
		}

		if (direction != Vector3.Zero) {
			direction = direction.Normalized();
			_targetVelocity.X = direction.X * _speed;
			_targetVelocity.Z = direction.Z * _speed;
		} 
		else {
			_targetVelocity.X = 0f;
			_targetVelocity.Z = 0f;
		}

		if (!IsOnFloor()) _targetVelocity.Y -= _gravity * (float)delta;
		else if (!_isLocked && Input.IsActionJustPressed("jump")) _targetVelocity.Y = _jumpStrength;

		Velocity = _targetVelocity;
		MoveAndSlide();
	}

	public void SetInputLocked(bool locked) {
		_isLocked = locked;
		if (_isLocked) {
			_targetVelocity.X = 0f;
			_targetVelocity.Z = 0f;
		}
	}

	public void apply_status(Resource statusEffect) {
		if (_statusManager != null) {
			_statusManager.Call("apply_status", statusEffect);
		}
	}

	public void ApplyStatus(Resource statusEffect) {
		apply_status(statusEffect);
	}

	public void remove_status(string statusId) {
		if (_statusManager != null) {
			_statusManager.Call("remove_status", statusId);
		}
	}

	public void RemoveStatus(string statusId) {
		remove_status(statusId);
	}
}
