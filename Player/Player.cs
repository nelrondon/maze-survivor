using Godot;
using System;

public partial class Player : CharacterBody3D {
	[Signal] public delegate void stats_changedEventHandler();

	[ExportGroup("Movimiento")]
	[Export] private float _speed = 100.0f;
	[Export] private float _gravity = 9.8f;
	[Export] private float _jumpStrength = 4.0f;
	[Export] private float _mouseSensibility = 0.003f;

	[ExportGroup("Llave e Interacción")]
	[Export] public PackedScene KeyScene;
	[Export] public bool HasKey { get; set; } = false;

	[ExportGroup("Referencias Nodos")]
	[Export] private Camera3D _gameCamera;
	[Export] private Node3D _characterVisual;
	[Export] private RayCast3D _interactionRayCast;
	[Export] private TextureRect _hudFace;
	[Export] private Texture2D _hudFaceDamageTexture;

	[ExportGroup("Armas y Animación")]
	[Export] private float _fallPoseTime = 0.3f;
	[Export] private BoneAttachment3D _rightHand;
	[Export] private SkeletonIK3D _leftArmIK;
	[Export] private Vector3 _rightHandGripRotation = new Vector3(-90f, 0f, 0f);
	[Export] private Vector3 _rightHandGripPosition = Vector3.Zero;

	// Estado interno
	private float _pitch = 0.0f;
	private Vector3 _targetVelocity = Vector3.Zero;
	private Vector2 _newDir;
	private bool _isLocked = false;
	private float _airTime = 0.0f;
	private bool _isHoldingWeapon = false;

	// Nodos UI y Estado
	private Node _statusManager;
	private Map _mapUI;
	private CanvasLayer _hud;
	private AnimationTree _animTree;

	// Determina si esta instancia tiene el control local (soporta offline y multijugador)
	private bool _IsLocallyControlled() {
		var mp = Multiplayer;
		if (mp == null || !mp.HasMultiplayerPeer() || mp.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected) 
			return true;
		return IsMultiplayerAuthority();
	}

	public override void _Ready() {        
		// Registrar en grupo global para ser detectado por el mapa u otros sistemas
		AddToGroup("Players");

		_statusManager = GetNodeOrNull("StatusManager");
		_hud = GetNodeOrNull<CanvasLayer>("HUD");

		if (_gameCamera == null) _gameCamera = GetNodeOrNull<Camera3D>("Head/Camera3D");
		if (_characterVisual == null) _characterVisual = GetNodeOrNull<Node3D>("CharacterVisual") ?? GetNodeOrNull<Node3D>("MeshInstance3D");
		if (_interactionRayCast == null) _interactionRayCast = GetNodeOrNull<RayCast3D>("Head/Camera3D/RayCast3D");

		if (_interactionRayCast != null) {
			_interactionRayCast.CollideWithAreas = true;
			_interactionRayCast.CollideWithBodies = true;
		}

		if (_animTree == null) _animTree = GetNodeOrNull<AnimationTree>("AnimationTree");
		if (_animTree != null) _animTree.Active = true;

		if (_rightHand == null) _rightHand = GetNodeOrNull<BoneAttachment3D>("CharacterVisual/rig/Skeleton3D/RightHand");
		if (_leftArmIK == null) _leftArmIK = GetNodeOrNull<SkeletonIK3D>("CharacterVisual/rig/Skeleton3D/LeftArmIK");

		var animPlayer = GetNodeOrNull<AnimationPlayer>("CharacterVisual/AnimationPlayer");
		if (animPlayer != null && animPlayer.HasAnimation("CharLib/fall")) {
			var fallAnim = animPlayer.GetAnimation("CharLib/fall");
			fallAnim.LoopMode = Animation.LoopModeEnum.None;
			fallAnim.Length = _fallPoseTime;
		}

		// Configuración según la autoridad de red/control local
		if (_IsLocallyControlled()) {
			if (_gameCamera != null) _gameCamera.Current = true;
			if (_characterVisual != null) _characterVisual.Visible = false;
			if (_hud != null) _hud.Visible = true;
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}
		else {
			if (_gameCamera != null) _gameCamera.Current = false;
			if (_characterVisual != null) _characterVisual.Visible = true;
			if (_hud != null) _hud.Visible = false;
		}
	}

	#region Sistema de Llave (Sincronizado)

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

	private bool _isDead = false;

	public void Die() {
		if (_isDead) return;
		_isDead = true;

		DropKey();
		SetInputLocked(true);

		if (_IsLocallyControlled()) {
			EndGameUI.ShowResult(this, false, "¡HAS MUERTO!", "Has sido eliminado en el laberinto.");
		}
	}

	#endregion

	#region Sistema de Mapa

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

	#endregion

	#region Sistema de Armas y Combate (Sincronizado)

	public void RequestEquipWeapon(Node3D weaponNode) {
		if (weaponNode == null) return;

		if (Multiplayer.HasMultiplayerPeer() && IsMultiplayerAuthority()) {
			Rpc(nameof(RpcEquipWeapon), weaponNode.GetPath());
		} else {
			EquipWeapon(weaponNode);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void RpcEquipWeapon(NodePath weaponPath) {
		var weaponNode = GetNodeOrNull<Node3D>(weaponPath);
		if (weaponNode != null) {
			EquipWeapon(weaponNode);
		}
	}

	public void EquipWeapon(Node3D weaponNode) {
		if (weaponNode == null || _rightHand == null) return;

		Node3D mountPoint = _rightHand.GetNodeOrNull<Node3D>("HandOffset") ?? _rightHand;

		foreach (Node child in mountPoint.GetChildren()) {
			child.QueueFree();
		}

		if (weaponNode.GetParent() != null) {
			weaponNode.Reparent(mountPoint, false);
		}
		else {
			mountPoint.AddChild(weaponNode);
		}

		weaponNode.Position = Vector3.Zero;
		weaponNode.RotationDegrees = _rightHandGripRotation;
		weaponNode.Scale = Vector3.One * 0.01f;

		_isHoldingWeapon = true;
		if (_animTree != null) {
			_animTree.Set("parameters/TransitionStrafeHolding/transition_request", "Armed");
		}

		UpdateWeaponIK(weaponNode);
	}

	public void UpdateWeaponIK(Node3D weaponNode) {
		if (_leftArmIK == null) return;
		Node3D leftTarget = weaponNode?.GetNodeOrNull<Node3D>("LeftHandTarget");
		if (leftTarget != null) {
			_leftArmIK.TargetNode = leftTarget.GetPath();
			_leftArmIK.Start();
		}
		else {
			_leftArmIK.Stop();
		}
	}

	public void SetIsHoldingWeapon(bool holding) {
		_isHoldingWeapon = holding;
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void RpcTriggerAttack() {
		if (_animTree != null) {
			_animTree.Set("parameters/MeleeAttack/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
		}
	}

	#endregion

	public override void _Input(InputEvent @event) {
		if (!_IsLocallyControlled() || _isLocked) return;

		// Alternar Mapa (Tecla M)
		if (@event is InputEventKey mapKey && mapKey.Pressed && !mapKey.Echo && mapKey.Keycode == Key.M) {
			ToggleMap();
		}

		// Rotación de Cámara por Mouse
		if (@event is InputEventMouseMotion mouseMotion) {
			RotateY(-mouseMotion.Relative.X * _mouseSensibility);

			_pitch -= mouseMotion.Relative.Y * _mouseSensibility;
			_pitch = Mathf.Clamp(_pitch, Mathf.DegToRad(-89f), Mathf.DegToRad(89f));

			if (_gameCamera != null) {
				_gameCamera.RotationDegrees = new Vector3(Mathf.RadToDeg(_pitch), 0f, 0f);
			}
		}

		// Mostrar Cursor (Tecla Escape)
		if (@event is InputEventKey escapeKey && escapeKey.Pressed && escapeKey.Keycode == Key.Escape) {
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}

		// Interacción (Tecla E / Acción "interact")
		bool isInteractPressed = (@event is InputEventKey interactKey && interactKey.Pressed && interactKey.Keycode == Key.E) || 
			(InputMap.HasAction("interact") && @event.IsActionPressed("interact"));

		if (isInteractPressed) {
			if (_interactionRayCast != null && _interactionRayCast.IsColliding()) {
				GodotObject collider = _interactionRayCast.GetCollider();

				if (collider is Node node) {
					if (node.HasMethod("interact")) {
						node.Call("interact", this);
					} 
					else if (node.HasMethod("Interact")) {
						node.Call("Interact", this);
					}
				}
			}
		}
	}

	public override void _PhysicsProcess(double delta) {
		if (!_IsLocallyControlled()) return;

		ProcessStaminaRegen(delta);

		Vector3 direction = Vector3.Zero;
		Vector2 localInput = Vector2.Zero;

		if (!_isLocked) {
			if (Input.IsActionPressed("up")) { direction -= Transform.Basis.Z; localInput.Y += 1f; }
			if (Input.IsActionPressed("down")) { direction += Transform.Basis.Z; localInput.Y -= 1f; }
			if (Input.IsActionPressed("left")) { direction -= Transform.Basis.X; localInput.X -= 1f; }
			if (Input.IsActionPressed("right")) { direction += Transform.Basis.X; localInput.X += 1f; }
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
			_targetVelocity.X = Mathf.MoveToward(Velocity.X, 0, currentSpeed);
			_targetVelocity.Z = Mathf.MoveToward(Velocity.Z, 0, currentSpeed);
		}

		_newDir = localInput;

		if (!IsOnFloor()) {
			_airTime += (float)delta;
			_targetVelocity.Y -= _gravity * (float)delta;
		}
		else {
			_airTime = 0.0f;
			if (Input.IsActionJustPressed("jump") && !_isLocked) {
				if (GetStat(1) >= 5f) {
					_targetVelocity.Y = _jumpStrength;
					modify_stat(1, -5f);
				}
			}
		} 

		Velocity = _targetVelocity;
		MoveAndSlide();

		if (_animTree != null) {
			bool isJumping = !IsOnFloor() && Velocity.Y > 0 && _airTime < 0.45f;
			bool isFalling = !IsOnFloor() && (Velocity.Y <= 0 || _airTime >= 0.45f);

			_animTree.Set("parameters/Strafe/blend_position", _newDir);
			_animTree.Set("parameters/StrafeHolding/blend_position", _newDir);
			_animTree.Set("parameters/TransitionStrafeHolding/transition_request", _isHoldingWeapon ? "Armed" : "Unarmed");
			_animTree.Set("parameters/TransitionStrafeJumping/transition_request", IsOnFloor() ? "Strafe" : "Jump");
			_animTree.Set("parameters/Jump/conditions/IsOnFloor", IsOnFloor());
			_animTree.Set("parameters/Jump/conditions/IsJumping", isJumping);
			_animTree.Set("parameters/Jump/conditions/IsFalling", isFalling);
		}
	}

	public void SetInputLocked(bool locked) {
		_isLocked = locked;
		if (_isLocked) {
			_targetVelocity.X = 0f;
			_targetVelocity.Z = 0f;
		}
	}

	#region Status Manager Wrappers

	public void apply_status(Resource statusEffect) {
		if (_statusManager != null) _statusManager.Call("apply_status", statusEffect);
	}
	public void ApplyStatus(Resource statusEffect) => apply_status(statusEffect);

	public void remove_status(string statusId) {
		if (_statusManager != null) _statusManager.Call("remove_status", statusId);
	}
	public void RemoveStatus(string statusId) => remove_status(statusId);

	#endregion

	#region Helpers y Getters

	public Camera3D GetCamera() {
		if (_gameCamera == null) _gameCamera = GetNodeOrNull<Camera3D>("Head/Camera3D");
		return _gameCamera;
	}

	public void SetMeshVisible(bool visible) {
		if (_characterVisual == null) _characterVisual = GetNodeOrNull<Node3D>("CharacterVisual") ?? GetNodeOrNull<Node3D>("MeshInstance3D");
		if (_characterVisual != null) _characterVisual.Visible = visible;
	}

	#endregion
}
