using Godot;

public partial class Player : CharacterBody3D {
	[Signal] public delegate void stats_changedEventHandler();

	// Ajustes de movimiento y cámara
	[Export] private float _speed = 9.0f;
	[Export] private float _gravity = 9.8f;
	[Export] private float _jumpStrength = 4.0f;
	[Export] private float _mouseSensibility = 0.003f;
	
	// Nodos principales
	[Export] private Camera3D _gameCamera;
	[Export] private Node3D _characterVisual;
	[Export] private RayCast3D _interactionRayCast;
	[Export] private TextureRect _hudFace;
	[Export] private Texture2D _hudFaceDamageTexture;
	[Export] private float _fallPoseTime = 0.3f;
	[Export] private BoneAttachment3D _rightHand;
	[Export] private SkeletonIK3D _leftArmIK;
	
	// Agarre de armas en mano
	[Export] private Vector3 _rightHandGripRotation = new Vector3(-90f, 0f, 0f);
	[Export] private Vector3 _rightHandGripPosition = Vector3.Zero;
	
	// Estado interno
	private float _pitch = 0.0f;
	private Vector3 _targetVelocity = Vector3.Zero;
	private Vector2 _newDir;
	private bool _isLocked = false;
	private float _airTime = 0.0f;
	private bool _isHoldingWeapon = false;
	
	private Node _statusManager;
	private AnimationTree _animTree;

	// Revisa si esta instancia tiene la autoridad de red para controlarse localmente
	private bool _IsLocallyControlled() {
		var mp = Multiplayer;
		if (mp == null || !mp.HasMultiplayerPeer() || mp.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected) 
			return true;
		return IsMultiplayerAuthority();
	}
	
	// Inicializa referencias de nodos y configura visibilidad según autoridad de red
	public override void _Ready() {	
		_statusManager = GetNodeOrNull("StatusManager");
		if (_statusManager == null) {
			GD.Print("Player: Nodo StatusManager no encontrado. Se creará dinámicamente si es necesario.");
		}

		if (_gameCamera == null) _gameCamera = GetNodeOrNull<Camera3D>("Head/Camera3D");
		if (_characterVisual == null) _characterVisual = GetNodeOrNull<Node3D>("CharacterVisual");
		if (_interactionRayCast == null) _interactionRayCast = GetNodeOrNull<RayCast3D>("Head/Camera3D/RayCast3D");
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

		if (_IsLocallyControlled()) {
			if (_gameCamera != null) _gameCamera.Current = true;
			if (_characterVisual != null) _characterVisual.Visible = false;
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}
		else {
			if (_gameCamera != null) _gameCamera.Current = false;
			if (_characterVisual != null) _characterVisual.Visible = true;
		}
	}

	// Monta el objeto en la mano derecha y ajusta escalas e IK
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

		Node3D leftTarget = weaponNode.GetNodeOrNull<Node3D>("LeftHandTarget");
		if (_leftArmIK != null) {
			if (leftTarget != null) {
				_leftArmIK.TargetNode = leftTarget.GetPath();
				_leftArmIK.Start();
			}
			else {
				_leftArmIK.Stop();
			}
		}
	}

	// Busca marcadores de agarre dentro del objeto si existen
	private Node3D _FindGripMarker(Node weaponNode) {
		string[] candidateNames = { "HandTarget", "RightHandTarget", "Grip", "HandMarker" };
		foreach (var name in candidateNames) {
			var node = weaponNode.FindChild(name, true, false) as Node3D;
			if (node != null) return node;
		}
		return null;
	}
	
	// Procesa eventos de entrada: movimiento de cámara, interacción, ataques y soltar objetos
	public override void _Input(InputEvent @event) {
		if (!_IsLocallyControlled() || _isLocked) return;

		if (@event is InputEventMouseMotion mouseMotion) {
			RotateY(-mouseMotion.Relative.X * _mouseSensibility);
			_pitch -= mouseMotion.Relative.Y * _mouseSensibility;
			_pitch = Mathf.Clamp(_pitch, Mathf.DegToRad(-89f), Mathf.DegToRad(89f));

			if (_gameCamera != null) {
				_gameCamera.RotationDegrees = new Vector3(Mathf.RadToDeg(_pitch), 0f, 0f);
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

		bool isAttackPressed = (@event is InputEventMouseButton mouseBtn && mouseBtn.Pressed && mouseBtn.ButtonIndex == MouseButton.Left) ||
			(InputMap.HasAction("shoot") && @event.IsActionPressed("shoot"));

		if (isAttackPressed && _isHoldingWeapon && _animTree != null) {
			_animTree.Set("parameters/MeleeAttack/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
		}

		bool isDropPressed = (@event is InputEventKey dropKey && dropKey.Pressed && dropKey.Keycode == Key.G) ||
			(InputMap.HasAction("drop") && @event.IsActionPressed("drop"));

		if (isDropPressed) {
			DropWeapon();
		}
	}

	// DEUDA TÉCNICA: Sistema temporal de ítem único en mano.
	// PENDIENTE: Sistema de inventario con 3 slots intercambiables por teclado (1, 2, 3).
	// Soltar objeto equipado al suelo y restaurar estados
	public void DropWeapon() {
		if (!_isHoldingWeapon || _rightHand == null) return;

		Node3D mountPoint = _rightHand.GetNodeOrNull<Node3D>("HandOffset") ?? _rightHand;
		Node3D itemToDrop = null;

		foreach (Node child in mountPoint.GetChildren()) {
			if (child is Node3D node3D) {
				itemToDrop = node3D;
				break;
			}
		}

		if (itemToDrop != null) {
			Node sceneRoot = GetTree().CurrentScene ?? GetParent();
			Vector3 dropPos = GlobalPosition + (-Transform.Basis.Z * 1.5f);
			dropPos.Y = 0.2f;

			itemToDrop.Reparent(sceneRoot, true);
			itemToDrop.GlobalPosition = dropPos;
			itemToDrop.Rotation = Vector3.Zero;
			itemToDrop.Scale = Vector3.One;

			if (itemToDrop.HasMethod("on_drop")) {
				itemToDrop.Call("on_drop");
			}
			else if (itemToDrop.HasMethod("OnDrop")) {
				itemToDrop.Call("OnDrop");
			}
		}

		if (_leftArmIK != null) _leftArmIK.Stop();
		_isHoldingWeapon = false;
	}

	// Calcula físicas de movimiento, gravedad, salto y actualiza el árbol de animación
	public override void _PhysicsProcess(double delta) {

		Vector3 direction = Vector3.Zero;
		Vector2 localInput = Vector2.Zero;

		if (_IsLocallyControlled()) {
			if (!_isLocked) {
				if (Input.IsActionPressed("up")) { direction -= Transform.Basis.Z; localInput.Y += 1f; }
				if (Input.IsActionPressed("down")) { direction += Transform.Basis.Z; localInput.Y -= 1f; }
				if (Input.IsActionPressed("left")) { direction -= Transform.Basis.X; localInput.X -= 1f; }
				if (Input.IsActionPressed("right")) { direction += Transform.Basis.X; localInput.X += 1f; }
			}

			if (direction != Vector3.Zero) {
				direction = direction.Normalized();
				_targetVelocity.X = direction.X * _speed;
				_targetVelocity.Z = direction.Z * _speed;
			}
			else {
				_targetVelocity.X = Mathf.MoveToward(Velocity.X, 0, _speed);
				_targetVelocity.Z = Mathf.MoveToward(Velocity.Z, 0, _speed);
			}

			_newDir = localInput;

			if (!IsOnFloor()) {
				_airTime += (float)delta;
				_targetVelocity.Y -= _gravity * (float)delta;
			}
			else {
				_airTime = 0.0f;
				if (Input.IsActionJustPressed("jump") && !_isLocked) {
					_targetVelocity.Y = _jumpStrength;
				}
			}

			Velocity = _targetVelocity;
			MoveAndSlide();
		}

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

	// Bloquea o desbloquea las entradas del jugador
	public void SetInputLocked(bool locked) {
		_isLocked = locked;
		if (_isLocked) {
			_targetVelocity.X = 0f;
			_targetVelocity.Z = 0f;
		}
	}

	// Aplica un efecto de estado enviándolo al StatusManager
	public void apply_status(Resource statusEffect) {
		if (_statusManager != null) {
			_statusManager.Call("apply_status", statusEffect);
		}
	}

	// Wrapper en PascalCase para llamadas C#
	public void ApplyStatus(Resource statusEffect) {
		apply_status(statusEffect);
	}

	// Remueve un efecto de estado enviándolo al StatusManager
	public void remove_status(string statusId) {
		if (_statusManager != null) {
			_statusManager.Call("remove_status", statusId);
		}
	}

	// Wrapper en PascalCase para llamadas C#
	public void RemoveStatus(string statusId) {
		remove_status(statusId);
	}

	// Obtiene la referencia a la cámara del jugador
	public Camera3D GetCamera() {
		if (_gameCamera == null) _gameCamera = GetNodeOrNull<Camera3D>("Head/Camera3D");
		return _gameCamera;
	}

	// Alterna la visibilidad del modelo 3D del personaje
	public void SetMeshVisible(bool visible) {
		if (_characterVisual == null) _characterVisual = GetNodeOrNull<Node3D>("CharacterVisual");
		if (_characterVisual != null) _characterVisual.Visible = visible;
	}
}
