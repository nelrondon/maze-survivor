class_name CageTrap extends TrapBase

## Estructura de nodos esperada, además de los de TrapBase:
##   - Cage (Node3D, ej. MeshInstance3D + CollisionShape3D en un StaticBody3D)
##     La jaula/red empieza levantada (posición "abierta") y baja para atrapar.
##
## Bloqueo de movimiento: si tu PlayerController tiene un método
## set_movement_locked(bool), esta trampa lo llama automáticamente. Si no
## existe, usa la señal trap_triggered para que tus compañeros conecten su
## propia lógica de inmovilización.

@export var cage_path: NodePath = ^"Cage"
@export var close_time: float = 0.4
@export var open_time: float = 0.4
@export var trap_duration: float = 3.0
## Desplazamiento (relativo a la posición cerrada) al que sube la jaula cuando
## está abierta, esperando a que alguien pise el área.
@export var open_offset: Vector3 = Vector3(0, 2.0, 0)

@onready var _cage: Node3D = get_node_or_null(cage_path)

var _closed_position: Vector3
var _open_position: Vector3
var _trapped_body: Node3D = null


func _ready() -> void:
	super._ready()
	if _cage:
		_closed_position = _cage.position
		_open_position = _closed_position + open_offset
		_cage.position = _open_position


func _on_trigger(body: Node3D) -> void:
	if _trapped_body != null:
		return  # Ya hay alguien atrapado, no vuelve a dispararse hasta liberarlo.

	_trapped_body = body
	_close_cage()
	apply_effects(body)

	if body.has_method("set_movement_locked"):
		body.set_movement_locked(true)

	get_tree().create_timer(trap_duration).timeout.connect(_release_trap)


func _release_trap() -> void:
	if _trapped_body and _trapped_body.has_method("set_movement_locked"):
		_trapped_body.set_movement_locked(false)
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
