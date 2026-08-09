class_name StatusEffect extends Effect

@export var id: String = ""
@export var is_environment_based: bool = false
@export var max_duration: float = 5.0
@export var tick_interval: float = 1.0
@export var icon: Texture2D

var current_duration: float = 0.0
var time_since_last_tick: float = 0.0
var is_paused: bool = false

func apply(target) -> void:
	if target == null:
		return
	var sm = target.get_node_or_null("StatusManager") if target.has_node("StatusManager") else null
	if sm != null and sm.has_method("apply_status"):
		sm.apply_status(self)

## Se ejecuta una sola vez al aplicar el estado. Útil para aplicar modificadores (ej. reducir velocidad).
func on_start(_target: Node) -> void:
	pass

## Se ejecuta cada vez que el temporizador de tick se cumple. Útil para daño recurrente (ej. restar HP).
func on_tick(_target: Node) -> void:
	pass

## Se ejecuta una sola vez al terminar la duración o curarse. Revierte los cambios de on_start.
func on_end(_target: Node) -> void:
	pass
