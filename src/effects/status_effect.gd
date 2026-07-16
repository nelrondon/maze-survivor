class_name StatusEffect extends Resource

@export var id: String = ""
@export var is_environment_based: bool = false
@export var max_duration: float = 5.0
@export var tick_interval: float = 1.0
@export var icon: Texture2D

var current_duration: float = 0.0
var time_since_last_tick: float = 0.0
var is_paused: bool = false

## Se ejecuta una sola vez al aplicar el estado. Útil para aplicar modificadores (ej. reducir velocidad).
func on_start(_target: Node) -> void:
	pass

## Se ejecuta cada vez que el temporizador de tick se cumple. Útil para daño recurrente (ej. restar HP).
func on_tick(_target: Node) -> void:
	pass

## Se ejecuta una sola vez al terminar la duración o curarse. Revierte los cambios de on_start.
func on_end(_target: Node) -> void:
	pass
