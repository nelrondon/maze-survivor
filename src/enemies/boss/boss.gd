extends CharacterBody3D

enum BossState { PATROLLING, CHASING, DEAD }
var current_boss_state = BossState.PATROLLING

@export var max_health: float = 600.0
var current_health: float = max_health

@export var attack_range: float = 2.0
@export var attack_damage: float = 25.0
@export var attack_cooldown: float = 1.5

var player_target: Node3D = null
var can_attack: bool = true

@onready var senses: BossSenses = $Senses
@onready var movement: BossMovement = $Movement
@onready var attack_cooldown_timer = $AttackCooldownTimer

func _ready():
	senses.player_detected.connect(_on_player_detected)
	senses.player_lost.connect(_on_player_lost)
	attack_cooldown_timer.timeout.connect(_on_attack_cooldown_timeout)

func _physics_process(delta):
	if current_boss_state == BossState.DEAD:
		return

	senses.is_active = (current_boss_state == BossState.PATROLLING)

	if current_boss_state == BossState.CHASING:
		_try_attack()

	var state_int = 0 if current_boss_state == BossState.PATROLLING else 1
	movement.move(delta, state_int, player_target)

func _on_player_detected(player: Node3D, reason: String):
	if current_boss_state == BossState.DEAD:
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

func _try_attack():
	if not can_attack or player_target == null:
		return
	if global_position.distance_to(player_target.global_position) <= attack_range:
		if player_target.has_method("take_damage"):
			player_target.take_damage(attack_damage)
		can_attack = false
		attack_cooldown_timer.wait_time = attack_cooldown
		attack_cooldown_timer.start()

func _on_attack_cooldown_timeout():
	can_attack = true

# Combate (recibido, no infligido)
func take_damage(damage: float, attacker: Node3D = null):
	if current_boss_state == BossState.DEAD:
		return
	current_health -= damage
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
	# await $AnimationPlayer.animation_finished (para cuando consiga la anim)
	queue_free()
