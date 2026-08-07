using Godot;
using System;

public partial class Player : CharacterBody3D {
	[Signal] public delegate void stats_changedEventHandler();

	[ExportGroup("Movimiento")]
	[Export] private float _speed = 7.0f;
	[Export] private float _gravity = 9.8f;
	[Export] private float _jumpStrength = 4.0f;
	[Export] private float _mouseSensibility = 0.0005f;

	[ExportGroup("Llave e Interacción")]
	[Export] public PackedScene KeyScene;

	[ExportGroup("Referencias")]
	[Export] private Camera3D _gameCamera;
	[Export] private Node3D _characterVisual;
	[Export] private RayCast3D _interactionRayCast;
	[Export] private TextureRect _hudFace;
	[Export] private Texture2D _hudFaceDamageTexture;

	[Export] public bool HasKey { get; set; } = false;

	private float _pitch = 0.0f;
	private Vector3 _targetVelocity = Vector3.Zero;
	private bool _isLocked = false;

	private Node _statusManager;
	private Map _mapUI;
	private CanvasLayer _hud;

	public override void _Ready() {	
		// Añadir automáticamente al grupo global "Players" para que el mapa lo detecte
		AddToGroup("Players");
		// Grupo "player" (minúscula) usado por el sistema de trampas y efectos de
		// entorno (TrapBase, EnvironmentZone, armas). No renombrar "Players": lo usan
		// MazeSpawner/GameManager para el spectator y demás lógica multiplayer.
		AddToGroup("player");

		_statusManager = GetNodeOrNull("StatusManager");
		_hud = GetNodeOrNull<CanvasLayer>("HUD");

		if (_gameCamera == null) _gameCamera = GetNodeOrNull<Camera3D>("Head/Camera3D");
		if (_characterVisual == null) _characterVisual = GetNodeOrNull<Node3D>("MeshInstance3D");
		if (_interactionRayCast == null) _interactionRayCast = GetNodeOrNull<RayCast3D>("Head/Camera3D/RayCast3D");

		if (_interactionRayCast != null) {
			_interactionRayCast.CollideWithAreas = true;
			_interactionRayCast.CollideWithBodies = true;
		}

		if (IsMultiplayerAuthority()) {
			if (_gameCamera != null) {
				_gameCamera.Current = true;
			}
			if (_characterVisual != null) _characterVisual.Visible = false;
			if (_hud != null) _hud.Visible = true;
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}
		else {
			if (_gameCamera != null) {
				_gameCamera.Current = false;
			}
			if (_characterVisual != null) _characterVisual.Visible = true;
			if (_hud != null) _hud.Visible = false;
		}
	}

	#region Sistema de Llave

	public void PickUpKey() {
		HasKey = true;
		GD.Print("Jugador: ¡Has recogido la llave!");
		
		if (Multiplayer.HasMultiplayerPeer() && IsMultiplayerAuthority()) {
			Rpc(nameof(SyncKeyStatus), true);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void SyncKeyStatus(bool status) {
		HasKey = status;
	}

	public void DropKey() {
		if (!HasKey) return;
		HasKey = false;

		if (KeyScene != null) {
			var keyInstance = KeyScene.Instantiate<Node3D>();
			GetParent().AddChild(keyInstance);
			keyInstance.GlobalPosition = GlobalPosition;
		}
		
		if (Multiplayer.HasMultiplayerPeer() && IsMultiplayerAuthority()) {
			Rpc(nameof(SyncKeyStatus), false);
		}
	}

	public void Die() {
		DropKey();
		SetInputLocked(true);
	}

	#endregion

	public override void _Input(InputEvent @event) {
		if (!IsMultiplayerAuthority() || _isLocked) return;

		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.M) {
			ToggleMap();
		}

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

		if (@event is InputEventKey escapeKey && escapeKey.Pressed && escapeKey.Keycode == Key.Escape) {
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}

		bool isInteractPressed = (@event is InputEventKey interactKey && interactKey.Pressed && interactKey.Keycode == Key.E) || 
			(InputMap.HasAction("interact") && @event.IsActionPressed("interact"));

		if (isInteractPressed) {
			if (_interactionRayCast != null && _interactionRayCast.IsColliding()) {
				GodotObject collider = _interactionRayCast.GetCollider();
				
				if (collider is Node node) {
					if (node.HasMethod("interact")) {
						node.Call("interact", this);
					}
					else if (node.GetParent() != null && node.GetParent().HasMethod("interact")) {
						node.GetParent().Call("interact", this);
					}
				}
			}
		}
	}

	private void ToggleMap() {
		if (_mapUI == null || !IsInstanceValid(_mapUI)) {
			var mapNode = GetTree().Root.FindChild("Map", recursive: true, owned: false);
			if (mapNode is Map map) {
				_mapUI = map;
			}
		}

		if (_mapUI != null) {
			_mapUI.Visible = !_mapUI.Visible;
			if (_mapUI.Visible) {
				_mapUI.MoveToFront();
				_mapUI.QueueRedraw();
			}
		}
	}

	public override void _PhysicsProcess(double delta) {
		if (!IsMultiplayerAuthority()) return;

		ProcessStaminaRegen(delta);

		Vector3 direction = Vector3.Zero;

		if (!_isLocked) {
			if (Input.IsActionPressed("up")) direction -= Transform.Basis.Z;
			if (Input.IsActionPressed("down")) direction += Transform.Basis.Z;
			if (Input.IsActionPressed("left")) direction -= Transform.Basis.X;
			if (Input.IsActionPressed("right")) direction += Transform.Basis.X;
		}

		bool isSprintingRequested = !_isLocked && (Input.IsKeyPressed(Key.Shift) || (InputMap.HasAction("sprint") && Input.IsActionPressed("sprint")));
		float currentSpeed = _speed;

		if (direction != Vector3.Zero) {
			direction = direction.Normalized();

			if (isSprintingRequested && GetStat(1) > 0f) {
				currentSpeed *= 1.3f; 
				modify_stat(1, -12.0f * (float)delta); 
			}

			_targetVelocity.X = direction.X * currentSpeed;
			_targetVelocity.Z = direction.Z * currentSpeed;
		} 
		else {
			_targetVelocity.X = 0f;
			_targetVelocity.Z = 0f;
		}

		if (!IsOnFloor()) {
			_targetVelocity.Y -= _gravity * (float)delta;
		}
		else if (!_isLocked && Input.IsActionJustPressed("jump")) {
			if (GetStat(1) >= 5f) {
				_targetVelocity.Y = _jumpStrength;
				modify_stat(1, -5f);
			}
		} 

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

	// Alias en snake_case para que scripts de GDScript (como CageTrap) puedan
	// detectarlo con has_method("set_movement_locked") e inmovilizar al jugador.
	public void set_movement_locked(bool locked) => SetInputLocked(locked);

	public void apply_status(Resource statusEffect) {
		if (_statusManager != null) _statusManager.Call("apply_status", statusEffect);
	}
	public void ApplyStatus(Resource statusEffect) => apply_status(statusEffect);

	public void remove_status(string statusId) {
		if (_statusManager != null) _statusManager.Call("remove_status", statusId);
	}
	public void RemoveStatus(string statusId) => remove_status(statusId);

	public Camera3D GetCamera() {
		if (_gameCamera == null) _gameCamera = GetNodeOrNull<Camera3D>("Head/Camera3D");
		return _gameCamera;
	}

	public void SetMeshVisible(bool visible) {
		if (_characterVisual == null) _characterVisual = GetNodeOrNull<Node3D>("MeshInstance3D");
		if (_characterVisual != null) _characterVisual.Visible = visible;
	}
}
