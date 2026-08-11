extends Node
class_name BossMovement

# --- Movimiento (patrulla / persecución) ---
@export var patrol_speed: float = 3.0
@export var chase_speed: float = 8.0
@export var patrol_radio: float = 10.0
@export var gravity_scale: float = 1.0
@export var rotation_speed: float = 10.0
@export var arrival_distance: float = 3.5
@export var direct_chase_range: float = 10.0

# --- Pathfinding (AStarGrid2D vía Maze.cs) ---
@export var chase_repath_interval: float = 0.4  # Segundos entre recálculos de ruta al perseguir
@export var chase_repath_min_delta: float = 1.0 # Unidades que el objetivo debe moverse para forzar recálculo

var current_speed: float = 1.2
var initial_pos: Vector3
var current_path: PackedVector3Array = []
var path_index: int = 0
var waiting_on_point: bool = false
var gravity: float = ProjectSettings.get_setting("physics/3d/default_gravity")

var _chase_repath_timer: float = 0.0
var _last_chase_target_pos: Vector3 = Vector3(INF, INF, INF)
var _patrol_repath_timer: float = 0.0

@onready var boss: CharacterBody3D = get_parent()
@onready var patrol_wait_timer = $"../PatrolWaitTimer"
var maze: Node = null

signal reached_target

func _get_maze() -> Node:
	if maze == null or not is_instance_valid(maze):
		maze = get_tree().get_first_node_in_group("Maze")
		if maze == null and get_tree().root != null:
			maze = get_tree().root.find_child("ActiveGameScene", true, false)
			if maze == null:
				maze = get_tree().root.find_child("Maze", true, false)
	return maze

func _ready():
	initial_pos = boss.global_position
	current_speed = patrol_speed
	patrol_wait_timer.timeout.connect(_on_patrol_wait_timeout)

	if _get_maze() != null:
		choose_new_destination()
	else:
		call_deferred("_check_maze_deferred")

func _check_maze_deferred():
	if _get_maze() != null:
		choose_new_destination()

func move(delta: float, state: int, target: Node3D) -> void:
	if not boss.is_on_floor():
		boss.velocity.y -= gravity * gravity_scale * delta
	else:
		boss.velocity.y = 0.0

	if state == 1 and target != null:
		current_speed = chase_speed
		_update_chase_path(delta, target)
	else:
		current_speed = patrol_speed
		_chase_repath_timer = 0.0
		_last_chase_target_pos = Vector3(INF, INF, INF)
		_update_patrol_or_special_path(delta)

	_follow_current_path(delta, state, target)

func _update_patrol_or_special_path(delta: float) -> void:
	# 1. Modo Escolta (sigue al Boss principal)
	if boss.get("follow_target") != null and is_instance_valid(boss.follow_target):
		_patrol_repath_timer -= delta
		var flat_boss_pos = Vector3(boss.global_position.x, 0, boss.global_position.z)
		var flat_target_pos = Vector3(boss.follow_target.global_position.x, 0, boss.follow_target.global_position.z)

		if flat_boss_pos.distance_to(flat_target_pos) <= arrival_distance:
			current_path = []
			path_index = 0
			return

		if current_path.is_empty() or _patrol_repath_timer <= 0.0:
			_request_path_to(boss.follow_target.global_position)
			_patrol_repath_timer = 1.5
		return

	# 2. Modo Custodia de Puerta (si un jugador tiene la llave)
	if boss.get("guards_exit_on_key") and boss.has_method("get_key_holder"):
		var key_holder = boss.get_key_holder()
		var door = boss.get_exit_door() if boss.has_method("get_exit_door") else null
		if key_holder != null and door != null:
			_patrol_repath_timer -= delta
			var flat_boss_pos = Vector3(boss.global_position.x, 0, boss.global_position.z)
			var flat_door_pos = Vector3(door.global_position.x, 0, door.global_position.z)

			if flat_boss_pos.distance_to(flat_door_pos) <= arrival_distance:
				current_path = []
				path_index = 0
				return

			if current_path.is_empty() or _patrol_repath_timer <= 0.0:
				_request_path_to(door.global_position)
				_patrol_repath_timer = 1.5
			return

func _update_chase_path(delta: float, target: Node3D) -> void:
	var flat_boss_pos = Vector3(boss.global_position.x, 0, boss.global_position.z)
	var flat_target_pos = Vector3(target.global_position.x, 0, target.global_position.z)

	# A corta distancia, ignoramos el grid y vamos directo
	if flat_boss_pos.distance_to(flat_target_pos) <= direct_chase_range:
		current_path = []
		path_index = 0
		return

	_chase_repath_timer -= delta
	var target_pos = target.global_position
	var target_moved = _last_chase_target_pos.distance_to(target_pos) > chase_repath_min_delta

	if current_path.is_empty() or _chase_repath_timer <= 0.0 or target_moved:
		_request_path_to(target_pos)
		_chase_repath_timer = chase_repath_interval
		_last_chase_target_pos = target_pos

func _follow_current_path(delta: float, state: int, target: Node3D = null) -> void:
	if current_path.is_empty() or path_index >= current_path.size():
		if state == 1 and target != null:
			_move_directly_to(delta, target.global_position)
			return

		if state == 0:
			if boss.get("follow_target") != null and is_instance_valid(boss.follow_target):
				_move_directly_to(delta, boss.follow_target.global_position)
				return

			if boss.get("guards_exit_on_key") and boss.has_method("get_key_holder") and boss.get_key_holder() != null:
				var door = boss.get_exit_door() if boss.has_method("get_exit_door") else null
				if door != null:
					_move_directly_to(delta, door.global_position)
					return

			if not waiting_on_point:
				waiting_on_point = true
				patrol_wait_timer.wait_time = randf_range(1.0, 2.0)
				patrol_wait_timer.start()
		return

	var next_point = current_path[path_index]
	var flat_boss_pos = Vector3(boss.global_position.x, 0, boss.global_position.z)
	var flat_next = Vector3(next_point.x, 0, next_point.z)

	# Pasar al siguiente punto cuando entramos en el radio deseado
	if flat_boss_pos.distance_to(flat_next) <= arrival_distance:
		path_index += 1
		if path_index >= current_path.size():
			reached_target.emit()

	var direction = flat_boss_pos.direction_to(flat_next)

	# Rotación Suave
	if direction.length() > 0.01:
		var target_look = boss.global_position + direction
		var target_transform = boss.transform.looking_at(target_look, Vector3.UP)
		boss.transform = boss.transform.interpolate_with(target_transform, rotation_speed * delta)

	# Movimiento Fluido
	var target_velocity = direction * current_speed
	boss.velocity.x = lerp(boss.velocity.x, target_velocity.x, 8.0 * delta)
	boss.velocity.z = lerp(boss.velocity.z, target_velocity.z, 8.0 * delta)

	boss.move_and_slide()

func _move_directly_to(delta: float, world_pos: Vector3) -> void:
	var flat_boss_pos = Vector3(boss.global_position.x, 0, boss.global_position.z)
	var flat_target = Vector3(world_pos.x, 0, world_pos.z)

	if flat_boss_pos.distance_to(flat_target) <= arrival_distance:
		boss.velocity.x = lerp(boss.velocity.x, 0.0, 8.0 * delta)
		boss.velocity.z = lerp(boss.velocity.z, 0.0, 8.0 * delta)
		boss.move_and_slide()
		return

	var direction = flat_boss_pos.direction_to(flat_target)

	if direction.length() > 0.01:
		var target_look = boss.global_position + direction
		var target_transform = boss.transform.looking_at(target_look, Vector3.UP)
		boss.transform = boss.transform.interpolate_with(target_transform, rotation_speed * delta)

	var target_velocity = direction * current_speed
	boss.velocity.x = lerp(boss.velocity.x, target_velocity.x, 8.0 * delta)
	boss.velocity.z = lerp(boss.velocity.z, target_velocity.z, 8.0 * delta)

	boss.move_and_slide()

func choose_new_destination():
	var angle = randf() * TAU
	var distance = randf_range(4.0, patrol_radio)
	var offset = Vector3(cos(angle) * distance, 0, sin(angle) * distance)

	var raw_point = initial_pos + offset
	_request_path_to(raw_point)

func _request_path_to(world_pos: Vector3) -> void:
	var m = _get_maze()
	if m == null or not m.has_method("FindPath"):
		return
	current_path = m.FindPath(boss.global_position, world_pos)
	path_index = 0

func reset_patrol_origin():
	initial_pos = boss.global_position
	choose_new_destination()

func cancel_wait():
	if waiting_on_point:
		waiting_on_point = false
		patrol_wait_timer.stop()
	current_path = []
	path_index = 0

func _on_patrol_wait_timeout():
	waiting_on_point = false
	if boss.get("follow_target") != null and is_instance_valid(boss.follow_target):
		_request_path_to(boss.follow_target.global_position)
		return
	if boss.get("guards_exit_on_key") and boss.has_method("get_key_holder") and boss.get_key_holder() != null:
		var door = boss.get_exit_door() if boss.has_method("get_exit_door") else null
		if door != null:
			_request_path_to(door.global_position)
			return
	choose_new_destination()
