class_name SpikeTrap extends TrapBase

## Estructura de nodos esperada, además de los de TrapBase:
##   - Spikes (Node3D, ej. MeshInstance3D + CollisionShape3D en un StaticBody3D)
##     Es el mesh que sube/baja del suelo.

@export var spikes_path: NodePath = ^"Spikes"
@export var rise_height: float = 0.5
@export var rise_time: float = 0.15
@export var retract_time: float = 0.3
## Cuánto tiempo se quedan arriba antes de bajar solos (solo en AREA_TRIGGER /
## PRESSURE_PLATE; en TIMED_PATTERN el tiempo lo controla TrapData.active_time).
@export var stay_up_time: float = 0.6

@onready var _spikes: Node3D = get_node_or_null(spikes_path)

var _base_y: float = 0.0
var _tween: Tween


func _ready() -> void:
	super._ready()
	if _spikes:
		_base_y = _spikes.position.y


func _on_trigger(body: Node3D) -> void:
	if data.activation_mode != TrapData.ActivationMode.TIMED_PATTERN:
		_rise_and_retract()
	apply_effects(body)


# En modo TIMED_PATTERN los pinchos suben/bajan solos siguiendo el ciclo,
# el daño se aplica en _on_trigger cuando el jugador está encima mientras
# están arriba.
func _on_activate() -> void:
	if data.activation_mode == TrapData.ActivationMode.TIMED_PATTERN:
		_animate_to(_base_y + rise_height, rise_time)


func _on_deactivate() -> void:
	if data.activation_mode == TrapData.ActivationMode.TIMED_PATTERN:
		_animate_to(_base_y, retract_time)


func _rise_and_retract() -> void:
	if _spikes == null:
		return
	if _tween:
		_tween.kill()
	_tween = create_tween()
	_tween.tween_property(_spikes, "position:y", _base_y + rise_height, rise_time)
	_tween.tween_interval(stay_up_time)
	_tween.tween_property(_spikes, "position:y", _base_y, retract_time)


func _animate_to(target_y: float, time: float) -> void:
	if _spikes == null:
		return
	if _tween:
		_tween.kill()
	_tween = create_tween()
	_tween.tween_property(_spikes, "position:y", target_y, time)
