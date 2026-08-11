extends CharacterBody3D

enum BossState { PATROLLING, CHASING, ATTACKING, DEAD }
var current_boss_state = BossState.PATROLLING

@export var max_health: float = 600.0
var current_health: float = max_health

@export var attack_range: float = 4.0
@export var attack_damage: float = 25.0
@export var attack_cooldown: float = 1.5
@export var attack_lunge_speed: float = 2.5

@export var guards_exit_on_key: bool = true
@export var follow_target: Node3D = null

var player_target: Node3D = null
var can_attack: bool = true

@onready var senses: BossSenses = $Senses
@onready var movement: BossMovement = $Movement
@onready var attack_cooldown_timer = $AttackCooldownTimer
@onready var health_bar = $Sprite3D/SubViewport/ProgressBar

@onready var animation_player: AnimationPlayer = $BossModel/AnimationPlayer

var _synced_pos: Vector3 = Vector3.ZERO
var _synced_rot_y: float = 0.0
var _has_synced_transform: bool = false


func _ready():
	current_health = max_health
	add_to_group("enemies")
	add_to_group("boss")

	if has_node("Sprite3D") and has_node("Sprite3D/SubViewport"):
		$Sprite3D.texture = $Sprite3D/SubViewport.get_texture()

	senses.player_detected.connect(_on_player_detected)
	senses.player_lost.connect(_on_player_lost)
	attack_cooldown_timer.timeout.connect(_on_attack_cooldown_timeout)
	
	health_bar.max_value = max_health
	health_bar.value = current_health
	
	# Animación por defecto al iniciar
	if animation_player.has_animation("Idle"):
		animation_player.play("Idle")


func _physics_process(delta):
	if current_boss_state == BossState.DEAD:
		return

	# Si es un cliente remoto en multijugador, interpolar posición del servidor
	if multiplayer.has_multiplayer_peer() and multiplayer.multiplayer_peer.get_connection_status() != MultiplayerPeer.CONNECTION_DISCONNECTED and not multiplayer.is_server():
		if _has_synced_transform:
			global_position = global_position.lerp(_synced_pos, delta * 15.0)
			var cur_rot = rotation
			cur_rot.y = lerp_angle(cur_rot.y, _synced_rot_y, delta * 15.0)
			rotation = cur_rot
		return

	# Comprobar si el jugador objetivo murió o desapareció
	if player_target != null:
		if not is_instance_valid(player_target):
			_lose_the_trail()
			return
		if player_target.has_method("get_stat") and player_target.get_stat(0) <= 0.0:
			_lose_the_trail()
			return

	senses.is_active = (current_boss_state == BossState.PATROLLING)

	if current_boss_state == BossState.CHASING:
		_try_attack()

	# Si está atacando, avanza un poco hacia el jugador (lunge)
	if current_boss_state == BossState.ATTACKING:
		var target_vel = Vector3.ZERO

		if player_target != null and is_instance_valid(player_target):
			var flat_pos = Vector3(global_position.x, 0, global_position.z)
			var flat_target = Vector3(player_target.global_position.x, 0, player_target.global_position.z)
			var dir = flat_pos.direction_to(flat_target)
			if dir.length() > 0.01:
				var look_transform = transform.looking_at(global_position + dir, Vector3.UP)
				transform = transform.interpolate_with(look_transform, movement.rotation_speed * delta)
				if flat_pos.distance_to(flat_target) > movement.arrival_distance:
					target_vel = dir * attack_lunge_speed

		velocity.x = lerp(velocity.x, target_vel.x, 10.0 * delta)
		velocity.z = lerp(velocity.z, target_vel.z, 10.0 * delta)

		move_and_slide()
	else:
		var state_int = 0 if current_boss_state == BossState.PATROLLING else 1
		movement.move(delta, state_int, player_target)

		var horizontal_speed = Vector2(velocity.x, velocity.z).length()
		if horizontal_speed > 0.3:
			if animation_player.has_animation("Walk"):
				animation_player.speed_scale = clamp(movement.current_speed / movement.patrol_speed, 1.0, 3.0)
				if animation_player.current_animation != "Walk":
					animation_player.play("Walk")
		else:
			animation_player.speed_scale = 1.0
			if animation_player.current_animation != "Idle" and animation_player.has_animation("Idle"):
				animation_player.play("Idle")

	# Servidor transmite la transformación del jefe a todos los clientes
	if multiplayer.has_multiplayer_peer() and multiplayer.multiplayer_peer.get_connection_status() != MultiplayerPeer.CONNECTION_DISCONNECTED and multiplayer.is_server():
		rpc("_sync_boss_transform", global_position, rotation.y, current_boss_state, current_health)


@rpc("any_peer", "unreliable_ordered")
func _sync_boss_transform(pos: Vector3, rot_y: float, state_val: int, hp: float) -> void:
	if multiplayer.is_server():
		return
	_synced_pos = pos
	_synced_rot_y = rot_y
	current_boss_state = state_val
	current_health = hp
	health_bar.value = hp
	if not _has_synced_transform:
		_has_synced_transform = true
		global_position = pos
		var r = rotation
		r.y = rot_y
		rotation = r

func _on_player_detected(player: Node3D, reason: String):
	if current_boss_state == BossState.DEAD or player == self or player.is_in_group("boss") or player.is_in_group("enemies"):
		return
	if player.has_method("get_stat") and player.get_stat(0) <= 0.0:
		return
	if current_boss_state != BossState.CHASING:
		_start_chase(player, reason)

func _on_player_lost(player: Node3D):
	if player == player_target:
		_lose_the_trail()

func _start_chase(target: Node3D, msg: String):
	if target == self or target.is_in_group("boss") or target.is_in_group("enemies"):
		return
	movement.cancel_wait()
	player_target = target
	current_boss_state = BossState.CHASING
	print(msg)

func get_key_holder() -> Node3D:
	var players = get_tree().get_nodes_in_group("Players")
	for p in players:
		if is_instance_valid(p) and not p.is_queued_for_deletion():
			if "HasKey" in p and p.HasKey:
				return p
			elif "has_key" in p and p.has_key:
				return p
	return null

func get_exit_door() -> Node3D:
	return get_tree().get_first_node_in_group("Door")

func _lose_the_trail():
	current_boss_state = BossState.PATROLLING
	player_target = null

	attack_cooldown_timer.stop()
	can_attack = true

	if follow_target != null and is_instance_valid(follow_target):
		print("El MiniBoss perdió al jugador. Retomando la escolta del Boss...")
		movement.cancel_wait()
		movement._request_path_to(follow_target.global_position)
	else:
		var key_holder = get_key_holder()
		var door = get_exit_door()
		if guards_exit_on_key and key_holder != null and door != null:
			print("¡Un jugador tiene la llave y el Boss lo perdió de vista! El Boss se devuelve a la puerta de salida...")
			movement.cancel_wait()
			movement._request_path_to(door.global_position)
		else:
			print("El Oyente no encontró nada. Volviendo a patrullar...")
			movement.reset_patrol_origin()

	if animation_player.has_animation("Idle"):
		animation_player.play("Idle")

func _try_attack():
	if not can_attack or player_target == null or not is_instance_valid(player_target) or player_target == self:
		return
		
	# Aplanamos la posición (Y = 0) para ignorar la diferencia de alturas
	var flat_boss_pos = Vector3(global_position.x, 0, global_position.z)
	var flat_player_pos = Vector3(player_target.global_position.x, 0, player_target.global_position.z)

	# Si la distancia plana es menor al rango, ataca
	if flat_boss_pos.distance_to(flat_player_pos) <= attack_range:
		current_boss_state = BossState.ATTACKING
		
		# Selecciona aleatoriamente una de tus 3 animaciones de ataque
		var attacks = ["Attack1", "Attack2", "Attack3"]
		var chosen_attack = attacks[randi() % attacks.size()]
		var hit_delay = 0.0
		
		if animation_player.has_animation(chosen_attack):
			animation_player.speed_scale = 1.0
			animation_player.play(chosen_attack)
			# Aproximamos el momento del golpe al 40% del clip (ajusta este valor si se ve desincronizado)
			hit_delay = animation_player.get_animation(chosen_attack).length * 0.5

		can_attack = false
		attack_cooldown_timer.wait_time = attack_cooldown
		attack_cooldown_timer.start()

		_deal_delayed_damage(player_target, hit_delay)

func _deal_delayed_damage(target: Node3D, delay: float) -> void:
	if delay > 0.0:
		await get_tree().create_timer(delay).timeout

	if not is_instance_valid(target) or target == self or not target.has_method("hit") or current_boss_state != BossState.ATTACKING:
		return

	# Revalidamos la distancia al momento del golpe: si el jugador ya se alejó, el ataque falla
	var flat_boss_pos = Vector3(global_position.x, 0, global_position.z)
	var flat_target_pos = Vector3(target.global_position.x, 0, target.global_position.z)
	if flat_boss_pos.distance_to(flat_target_pos) <= attack_range:
		if target.has_method("hit"):
			target.call("hit", attack_damage, self)

func _on_attack_cooldown_timeout():
	can_attack = true
	# Si el jugador sigue vivo y seguíamos atacando, vuelve a la persecución
	if current_boss_state == BossState.ATTACKING and player_target != null and is_instance_valid(player_target):
		current_boss_state = BossState.CHASING

# Combate (recibido, no infligido)
func hit(damage: float, attacker: Node3D = null):
	if current_boss_state == BossState.DEAD:
		return
	var attacker_path = attacker.get_path() if is_instance_valid(attacker) else NodePath("")
	if multiplayer.has_multiplayer_peer() and multiplayer.multiplayer_peer.get_connection_status() != MultiplayerPeer.CONNECTION_DISCONNECTED:
		rpc("_sync_hit", damage, attacker_path)
	else:
		_apply_damage(damage, attacker)

@rpc("any_peer", "call_local")
func _sync_hit(damage: float, attacker_path: NodePath = NodePath("")) -> void:
	var attacker_node = get_node_or_null(attacker_path) if not attacker_path.is_empty() else null
	_apply_damage(damage, attacker_node)

func _apply_damage(damage: float, attacker: Node3D = null) -> void:
	if current_boss_state == BossState.DEAD:
		return
	current_health -= damage
	health_bar.value = current_health
	print("Vida del jefe: ", current_health, "/", max_health)

	if attacker != null:
		_start_chase(attacker, "¡EL OYENTE RECIBIÓ DAÑO Y CORRE HACIA EL ATACANTE!")

	if current_health <= 0:
		_die()

func _die():
	current_boss_state = BossState.DEAD
	print("¡EL OYENTE HA SIDO ELIMINADO!")
	set_physics_process(false)
	
	if has_node("CollisionShape3D"):
		$CollisionShape3D.set_deferred("disabled", true)
		
	# Reproduce la animación de muerte y espera a que termine para borrarlo
	if animation_player.has_animation("Defence3"):
		animation_player.play("Defence3")
		await animation_player.animation_finished
		
	queue_free()
