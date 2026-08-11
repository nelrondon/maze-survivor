using Godot;
using System;

public partial class Player : CharacterBody3D {
	[Signal] public delegate void stats_changedEventHandler();

	[ExportGroup("Movimiento")]
	[Export] private float _speed = 9.0f;
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
	[Export] private Vector3 _rightHandGripRotation = Vector3.Zero;
	[Export] private Vector3 _rightHandGripPosition = Vector3.Zero;

	// Estado interno
	private float _pitch = 0.0f;
	private Vector3 _targetVelocity = Vector3.Zero;
	private Vector2 _newDir;
	private bool _isLocked = false;
	private float _airTime = 0.0f;
	private bool _isHoldingWeapon = false;

	// Sincronización multijugador remota
	private Vector3 _syncedPosition = Vector3.Zero;
	private float _syncedRotationY = 0.0f;
	private float _syncedPitch = 0.0f;
	private Vector2 _syncedDir = Vector2.Zero;
	private bool _syncedIsOnFloor = true;
	private bool _hasReceivedFirstTransformSync = false;

	// Nodos UI y Estado
	private Node _statusManager;
	private Map _mapUI;
	private CanvasLayer _hud;
	private AnimationTree _animTree;

	// Determina si esta instancia tiene el control local (soporta offline y multijugador)
	public bool IsLocallyControlled() {
		var mp = Multiplayer;
		if (mp == null || !mp.HasMultiplayerPeer() || mp.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected) 
			return true;
		return IsMultiplayerAuthority();
	}

	private bool _IsLocallyControlled() => IsLocallyControlled();

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
			if (_characterVisual != null) _characterVisual.Visible = true;
			if (_hud != null) _hud.Visible = true;
			Input.MouseMode = Input.MouseModeEnum.Captured;

			// Iniciar efecto de hambre natural gestionado por Stats


		}
		else {
			if (_gameCamera != null) _gameCamera.Current = false;
			if (_characterVisual != null) _characterVisual.Visible = true;
			if (_hud != null) _hud.Visible = false;
		}
	}

	private void DebugGiveWeapons() {
		// Los jugadores empiezan sin items (desde 0)
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
		if (Multiplayer != null && Multiplayer.HasMultiplayerPeer() && Multiplayer.MultiplayerPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected) {
			if (IsMultiplayerAuthority()) {
				Rpc(nameof(RpcDropKey), GlobalPosition);
			}
		} else {
			RpcDropKey(GlobalPosition);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcDropKey(Vector3 dropPos) {
		HasKey = false;
		if (KeyScene != null) {
			var keyInstance = KeyScene.Instantiate<Node3D>();
			keyInstance.Name = "SingleMazeKey";
			GetParent().AddChild(keyInstance);
			keyInstance.GlobalPosition = dropPos;
		}
	}

	private bool _isDead = false;
	public bool IsDead => _isDead;

	public void Die() {
		if (_isDead) return;

		if (Multiplayer != null && Multiplayer.HasMultiplayerPeer() && Multiplayer.MultiplayerPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected && IsMultiplayerAuthority()) {
			Rpc(nameof(RpcDie));
		} else {
			RpcDie();
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void RpcDie() {
		if (_isDead) return;
		_isDead = true;

		DropKey();
		SetInputLocked(true);

		if (_characterVisual != null) _characterVisual.Visible = false;
		if (_hud != null) _hud.Visible = false;

		if (_IsLocallyControlled()) {
			EndGameUI.ShowResult(this, false, "¡HAS MUERTO!", "Has sido eliminado en el laberinto. Puedes espectar a los sobrevivientes o volver al lobby.");
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

	private string _equippedItemId = "";

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void SyncEquippedWeapon(string itemId) {
		_equippedItemId = itemId ?? "";
		if (Multiplayer != null && Multiplayer.HasMultiplayerPeer() && Multiplayer.MultiplayerPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected) {
			Rpc(nameof(RpcSyncEquippedWeapon), _equippedItemId);
		} else {
			RpcSyncEquippedWeapon(_equippedItemId);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcSyncEquippedWeapon(string itemId) {
		_equippedItemId = itemId ?? "";
		if (_rightHand == null) _rightHand = GetNodeOrNull<BoneAttachment3D>("CharacterVisual/rig/Skeleton3D/RightHand");
		if (_rightHand == null) return;

		Node3D mountPoint = _rightHand.GetNodeOrNull<Node3D>("HandOffset") ?? _rightHand;

		foreach (Node child in mountPoint.GetChildren()) {
			child.QueueFree();
		}

		if (string.IsNullOrEmpty(itemId)) {
			_isHoldingWeapon = false;
			if (_animTree != null) {
				_animTree.Set("parameters/TransitionStrafeHolding/transition_request", "Unarmed");
			}
			return;
		}

		var registry = GetNodeOrNull("/root/ItemRegistry");
		if (registry != null) {
			var data = registry.Call("get_data", itemId);
			if (data.AsGodotObject() != null) {
				var vmObj = data.AsGodotObject().Get("view_model");
				if (vmObj.VariantType != Variant.Type.Nil && vmObj.AsGodotObject() is PackedScene scene) {
					var weapon3D = scene.Instantiate<Node3D>();
					mountPoint.AddChild(weapon3D);

					weapon3D.Position = Vector3.Zero;
					weapon3D.RotationDegrees = _rightHandGripRotation;
					float itemScale = (itemId == "sks_rifle" || itemId.Contains("rifle")) ? 0.02f : 0.005f;
					weapon3D.Scale = Vector3.One * itemScale;
					weapon3D.Visible = !_IsLocallyControlled();

					_isHoldingWeapon = true;
					if (_animTree != null) {
						_animTree.Set("parameters/TransitionStrafeHolding/transition_request", "Armed");
					}
					UpdateWeaponIK(weapon3D);
					GD.Print($"[Player {Name}] Arma 3D '{itemId}' montada en la mano del modelo en tercera persona.");
				}
			}
		}
	}

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
		float nodeScale = (weaponNode.Name.ToString().ToLower().Contains("rifle") || weaponNode.Name.ToString().ToLower().Contains("sks")) ? 0.02f : 0.005f;
		weaponNode.Scale = Vector3.One * nodeScale;
		weaponNode.Visible = !_IsLocallyControlled();

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

		// Debug: Otorgar set de armas y municiones al presionar la tecla 'º'
		if (@event is InputEventKey debugKey && debugKey.Pressed && !debugKey.Echo && 
			(debugKey.Keycode == Key.Section || debugKey.Keycode == Key.Quoteleft || debugKey.Keycode == Key.Asciitilde || debugKey.Unicode == 'º' || debugKey.Unicode == 'ª' || (int)debugKey.Keycode == 186 || (int)debugKey.Keycode == 167)) {
			DebugGiveWeaponsAndAmmo();
		}

		// Rotación de Cámara por Mouse
		if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured) {
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

		// Interacción (Tecla E / Acción "interact") - solo interactuar en el mundo si no hay menús abiertos (mouse capturado)
		bool isInteractPressed = Input.MouseMode == Input.MouseModeEnum.Captured && (
			(@event is InputEventKey interactKey && interactKey.Pressed && !interactKey.Echo && interactKey.Keycode == Key.E) || 
			(InputMap.HasAction("interact") && @event.IsActionPressed("interact"))
		);

		if (isInteractPressed) {
			if (_interactionRayCast != null && _interactionRayCast.IsColliding()) {
				GodotObject collider = _interactionRayCast.GetCollider();

				if (collider is Node node) {
					bool handled = false;
					if (node.HasMethod("interact")) {
						node.Call("interact", this);
						handled = true;
					} 
					else if (node.HasMethod("Interact")) {
						node.Call("Interact", this);
						handled = true;
					}
					else {
						Node parent = node.GetParent();
						if (parent != null) {
							if (parent.HasMethod("interact")) {
								parent.Call("interact", this);
								handled = true;
							} 
							else if (parent.HasMethod("Interact")) {
								parent.Call("Interact", this);
								handled = true;
							}
						}
					}

					if (handled) {
						GetViewport().SetInputAsHandled();
					}
				}
			}
		}
	}

	public override void _PhysicsProcess(double delta) {
		if (_IsLocallyControlled()) {
			ProcessStaminaRegen(delta);
			ProcessHunger(delta);
			ProcessStarvation(delta);

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
					modify_stat(1, -15.0f * (float)delta); 
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
					if (GetStat(1) >= 8f) {
						_targetVelocity.Y = _jumpStrength;
						modify_stat(1, -8f);
					}
				}
			} 

			Velocity = _targetVelocity;
			MoveAndSlide();

			UpdateAnimations(_newDir, IsOnFloor(), Velocity);
			UpdateWeaponAimPitch(delta, _pitch);

			if (Multiplayer != null && Multiplayer.HasMultiplayerPeer() && Multiplayer.MultiplayerPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected) {
				Rpc(nameof(RpcSyncTransform), GlobalPosition, Rotation.Y, _pitch, _newDir, IsOnFloor(), Velocity.Y);
			}
		}
		else {
			// Jugador remoto: interpolación suave de posición y rotación Y
			if (_hasReceivedFirstTransformSync) {
				GlobalPosition = GlobalPosition.Lerp(_syncedPosition, (float)delta * 18.0f);
				Vector3 currentRot = Rotation;
				currentRot.Y = Mathf.LerpAngle(currentRot.Y, _syncedRotationY, (float)delta * 18.0f);
				Rotation = currentRot;
			}
			UpdateAnimations(_syncedDir, _syncedIsOnFloor, Velocity);
			UpdateWeaponAimPitch(delta, _syncedPitch);
		}
	}

	private void UpdateWeaponAimPitch(double delta, float targetPitch) {
		if (_rightHand == null) _rightHand = GetNodeOrNull<BoneAttachment3D>("CharacterVisual/rig/Skeleton3D/RightHand");
		if (_rightHand == null) return;

		Node3D mountPoint = _rightHand.GetNodeOrNull<Node3D>("HandOffset") ?? _rightHand;
		if (mountPoint != null && _isHoldingWeapon) {
			Vector3 targetRot = _rightHandGripRotation + new Vector3(Mathf.RadToDeg(targetPitch), 0f, 0f);
			mountPoint.RotationDegrees = mountPoint.RotationDegrees.Lerp(targetRot, (float)delta * 15.0f);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
	private void RpcSyncTransform(Vector3 pos, float rotY, float pitch, Vector2 animDir, bool isOnFloor, float velY) {
		_syncedPosition = pos;
		_syncedRotationY = rotY;
		_syncedPitch = pitch;
		_syncedDir = animDir;
		_syncedIsOnFloor = isOnFloor;

		if (!_hasReceivedFirstTransformSync) {
			_hasReceivedFirstTransformSync = true;
			GlobalPosition = pos;
			Vector3 r = Rotation;
			r.Y = rotY;
			Rotation = r;
		}
		UpdateAnimations(animDir, isOnFloor, new Vector3(0, velY, 0));
	}

	private void UpdateAnimations(Vector2 dir, bool isOnFloor, Vector3 vel) {
		if (_animTree != null) {
			bool isJumping = !isOnFloor && vel.Y > 0.5f;
			bool isFalling = !isOnFloor && vel.Y < -0.5f;

			_animTree.Set("parameters/Strafe/blend_position", dir);
			_animTree.Set("parameters/StrafeHolding/blend_position", dir);
			_animTree.Set("parameters/TransitionStrafeHolding/transition_request", _isHoldingWeapon ? "Armed" : "Unarmed");
			_animTree.Set("parameters/TransitionStrafeJumping/transition_request", isOnFloor ? "Strafe" : "Jump");
			_animTree.Set("parameters/Jump/conditions/IsOnFloor", isOnFloor);
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

	public void set_movement_locked(bool locked) {
		SetInputLocked(locked);
	}

	public void SetMovementLocked(bool locked) {
		SetInputLocked(locked);
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

	private void DebugGiveWeaponsAndAmmo() {
		var inventory = GetNodeOrNull<Node>("Inventory");
		var itemRegistry = GetNodeOrNull<Node>("/root/ItemRegistry") ?? GetTree().Root.GetNodeOrNull<Node>("ItemRegistry");
		if (inventory == null || itemRegistry == null) {
			GD.PrintErr("[DEBUG º] Inventory o ItemRegistry no encontrados.");
			return;
		}

		string[] itemIds = new string[] { "tokarev_pistol", "sks_rifle", "cuchillo", "bala", "bala", "bala", "medkit_large" };
		foreach (string id in itemIds) {
			Variant itemData = itemRegistry.Call("get_data", id);
			if (itemData.VariantType != Variant.Type.Nil && itemData.AsGodotObject() != null) {
				int amount = id == "bala" ? 30 : 1;
				inventory.Call("add_item", itemData.AsGodotObject(), amount);
			}
		}
		GD.Print("[DEBUG] Tecla 'º': Otorgado set de armas (Pistola, Rifle, Cuchillo, Municiones y Botiquín) al inventario.");
	}

	#endregion
}
