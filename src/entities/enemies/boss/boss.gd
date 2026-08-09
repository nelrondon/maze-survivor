extends CharacterBody3D

enum BossState { PATROLLING, CHASING, ATTACKING, DEAD }
var current_boss_state = BossState.PATROLLING

@export var max_health: float = 600.0
var current_health: float = max_health

@export var attack_range: float = 4.0
@export var attack_damage: float = 25.0
@export var attack_cooldown: float = 1.5
@export var attack_lunge_speed: float = 2.5

var player_target: Node3D = null
var can_attack: bool = true

@onready var senses: BossSenses = $Senses
@onready var movement: BossMovement = $Movement
@onready var attack_cooldown_timer = $AttackCooldownTimer
@onready var health_bar = $Sprite3D/SubViewport/ProgressBar

@onready var animation_player: AnimationPlayer = $BossModel/AnimationPlayer

func _ready():
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

	# Si está atacando, se frena en seco
	# Si está atacando, avanza un poco hacia el jugador (lunge) en vez de congelarse
	if current_boss_state == BossState.ATTACKING:
		var target_vel = Vector3.ZERO

		if player_target != null and is_instance_valid(player_target):
			var flat_pos = Vector3(global_position.x, 0, global_position.z)
			var flat_target = Vector3(player_target.global_position.x, 0, player_target.global_position.z)
			var dir = flat_pos.direction_to(flat_target)
			if dir.length() > 0.01:
				var look_transform = transform.looking_at(global_position + dir, Vector3.UP)
				transform = transform.interpolate_with(look_transform, movement.rotation_speed * delta)
				# Solo avanza si no está ya pegado al jugador, para no seguir empujando
				if flat_pos.distance_to(flat_target) > movement.arrival_distance:
					target_vel = dir * attack_lunge_speed

		velocity.x = lerp(velocity.x, target_vel.x, 10.0 * delta)
		velocity.z = lerp(velocity.z, target_vel.z, 10.0 * delta)

		move_and_slide()
	else:
		var state_int = 0 if current_boss_state == BossState.PATROLLING else 1
		movement.move(delta, state_int, player_target)

# Manejo de animación según si se está moviendo o no
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

func _on_player_detected(player: Node3D, reason: String):
	if current_boss_state == BossState.DEAD:
		return
	if player.has_method("get_stat") and player.get_stat(0) <= 0.0:
		return
	if current_boss_state != BossState.CHASING:
		_start_chase(player, reason)

func _on_player_lost(player: Node3D):
	if player == player_target:
		_lose_the_trail()

func _start_chase(target: Node3D, msg: String):
	movement.cancel_wait()
	player_target = target
	current_boss_state = BossState.CHASING
	print(msg)

func _lose_the_trail():
	print("El Oyente no encontró nada. Volviendo a patrullar...")
	current_boss_state = BossState.PATROLLING
	player_target = null
	movement.reset_patrol_origin()

	attack_cooldown_timer.stop()
	can_attack = true
	
	if animation_player.has_animation("Idle"):
		animation_player.play("Idle")

func _try_attack():
	if not can_attack or player_target == null or not is_instance_valid(player_target):
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

	if not is_instance_valid(target) or not target.has_method("hit") or current_boss_state != BossState.ATTACKING:
		return

	# Revalidamos la distancia al momento del golpe: si el jugador ya se alejó, el ataque falla
	var flat_boss_pos = Vector3(global_position.x, 0, global_position.z)
	var flat_target_pos = Vector3(target.global_position.x, 0, target.global_position.z)
	if flat_boss_pos.distance_to(flat_target_pos) <= attack_range:
		target.hit(attack_damage)

func _on_attack_cooldown_timeout():
	can_attack = true
	# Si el jugador sigue vivo y seguíamos atacando, vuelve a la persecución
	if current_boss_state == BossState.ATTACKING and player_target != null and is_instance_valid(player_target):
		current_boss_state = BossState.CHASING

# Combate (recibido, no infligido)
func hit(damage: float, attacker: Node3D = null):
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
