extends Node
class_name BossMovement

@export var patrol_speed: float = 1.2
@export var chase_speed: float = 4.8
@export var patrol_radio: float = 8.0
@export var gravity_scale: float = 1.0
@export var rotation_speed: float = 10.0

var current_speed: float = 1.2
var initial_pos: Vector3
var patrol_destination: Vector3
var waiting_on_point: bool = false
var gravity: float = ProjectSettings.get_setting("physics/3d/default_gravity")

@onready var boss: CharacterBody3D = get_parent()
@onready var nav_agent = $"../NavigationAgent3D"
@onready var patrol_wait_timer = $"../PatrolWaitTimer"

signal reached_target
signal patrol_point_reached

func _ready():
	initial_pos = boss.global_position
	current_speed = patrol_speed
	choose_new_destination()
	patrol_wait_timer.timeout.connect(_on_patrol_wait_timeout)

func move(delta: float, state: int, target: Node3D) -> void:
	# state: 0 = PATROLLING, 1 = CHASING (le pasamos el enum desde Boss.gd)
	if not boss.is_on_floor():
		boss.velocity.y -= gravity * gravity_scale * delta
	else:
		boss.velocity.y = 0.0

	var destination = Vector3.ZERO
	var has_destination = true

	if state == 1 and target != null:
		destination = target.global_position
		current_speed = chase_speed
	elif state == 0:
		destination = patrol_destination
		current_speed = patrol_speed
	else:
		has_destination = false

	if not has_destination:
		boss.velocity.x = 0
		boss.velocity.z = 0
		boss.move_and_slide()
		return

	nav_agent.target_position = destination

	if nav_agent.is_navigation_finished():
		if state == 0 and not waiting_on_point:
			waiting_on_point = true
			boss.velocity.x = 0
			boss.velocity.z = 0
			patrol_wait_timer.wait_time = randf_range(2.0, 5.0)
			patrol_wait_timer.start()
		elif state == 1:
			boss.velocity.x = 0
			boss.velocity.z = 0
		boss.move_and_slide()
		return

	var next_pos = nav_agent.get_next_path_position()
	var direction = boss.global_position.direction_to(next_pos)
	direction.y = 0

	if direction.length() > 0.01:
		var target_look = boss.global_position + direction
		var target_transform = boss.transform.looking_at(target_look, Vector3.UP)
		boss.transform = boss.transform.interpolate_with(target_transform, rotation_speed * delta)

	boss.velocity.x = direction.x * current_speed
	boss.velocity.z = direction.z * current_speed
	boss.move_and_slide()

func choose_new_destination():
	var angle = randf() * TAU
	var distance = randf() * patrol_radio
	var offset = Vector3(cos(angle) * distance, 0, sin(angle) * distance)
	patrol_destination = initial_pos + offset

func reset_patrol_origin():
	initial_pos = boss.global_position
	choose_new_destination()

func cancel_wait():
	if waiting_on_point:
		waiting_on_point = false
		patrol_wait_timer.stop()

func _on_patrol_wait_timeout():
	waiting_on_point = false
	choose_new_destination()
