class_name CageTrap extends TrapBase

@export var cage_path: NodePath = ^"Cage"
@export var close_time: float = 0.4
@export var open_time: float = 0.4
@export var trap_duration: float = 3.0
@export var open_offset: Vector3 = Vector3(0, 3.0, 0)

@onready var _cage: Node3D = get_node_or_null(cage_path)
@onready var _cage_collision: CollisionShape3D = _cage.get_node_or_null("CollisionShape3D") if _cage else null

var _closed_position: Vector3
var _open_position: Vector3
var _trapped_body: Node3D = null

func _ready() -> void:
	super._ready()
	if _cage:
		_closed_position = _cage.position
		_open_position = _closed_position + open_offset
		_cage.position = _open_position
	if _cage_collision:
		# La jaula es solo decorativa: quien atrapa al jugador de verdad es
		# set_movement_locked(). Si dejamos esta colisión activa, la caja
		# (1.3x2x1.3) se cierra/abre encima de un CharacterBody3D ya
		# centrado ahí adentro y move_and_slide() lo empuja/dispara al
		# resolver el solape — pasa apenas hay contacto y también al abrir.
		_cage_collision.disabled = true

func _on_trigger(body: Node3D) -> void:
	if _trapped_body != null:
		return  # Ya hay alguien atrapado, no vuelve a dispararse hasta liberarlo.

	_trapped_body = body
	_close_cage()

	# Centrar al jugador en la trampa
	body.global_position = Vector3(global_position.x, body.global_position.y, global_position.z)

	if body.has_method("set_movement_locked"):
		body.set_movement_locked(true)
	elif body.has_method("SetMovementLocked"):
		body.SetMovementLocked(true)

	get_tree().create_timer(trap_duration).timeout.connect(_release_trap)

func _release_trap() -> void:
	if _trapped_body:
		if _trapped_body.has_method("set_movement_locked"):
			_trapped_body.set_movement_locked(false)
		elif _trapped_body.has_method("SetMovementLocked"):
			_trapped_body.SetMovementLocked(false)
		apply_effects(_trapped_body)  # el debuff de lentitud empieza AHORA, cuando ya puede caminar
	_open_cage()
	_trapped_body = null

func _close_cage() -> void:
	if _cage == null:
		return
	var tween := create_tween()
	tween.tween_property(_cage, "position", _closed_position, close_time)

func _open_cage() -> void:
	if _cage == null:
		return
	var tween := create_tween()
	tween.tween_property(_cage, "position", _open_position, open_time)
