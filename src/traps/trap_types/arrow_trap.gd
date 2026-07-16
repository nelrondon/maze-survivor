class_name ArrowTrap extends TrapBase

## Estructura de nodos esperada, además de los de TrapBase:
##   - SpawnPoint (Node3D) -> opcional, punto y rotación desde donde sale la flecha.
##     Si no existe, se usa la posición/rotación de la propia trampa.
##
## Requiere asignar arrow_scene con una escena raíz de tipo ArrowProjectile
## (ver arrow_projectile.gd).

@export var arrow_scene: PackedScene
@export var spawn_point_path: NodePath = ^"SpawnPoint"
## Dirección de disparo en espacio LOCAL de la trampa (se rota con ella).
@export var shoot_direction: Vector3 = Vector3.FORWARD
@export var arrow_speed: float = 15.0

@onready var _spawn_point: Node3D = get_node_or_null(spawn_point_path)


func _on_trigger(_body: Node3D) -> void:
	_shoot_arrow()


func _shoot_arrow() -> void:
	if arrow_scene == null:
		push_warning("ArrowTrap '%s': no hay arrow_scene asignada en el inspector." % data.id)
		return

	var arrow: Node3D = arrow_scene.instantiate()
	get_tree().current_scene.add_child(arrow)

	var origin: Vector3 = _spawn_point.global_position if _spawn_point else global_position
	arrow.global_position = origin

	var world_direction: Vector3 = (global_transform.basis * shoot_direction).normalized()

	if arrow.has_method("launch"):
		arrow.launch(world_direction, arrow_speed, data.effects)
	else:
		push_warning("ArrowTrap '%s': la escena de flecha no tiene launch(direction, speed, effects)." % data.id)
