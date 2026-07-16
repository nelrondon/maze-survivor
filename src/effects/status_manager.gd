class_name StatusManager extends Node

signal status_added(status_id: String, icon: Texture2D, max_duration: float)
signal status_removed(status_id: String)
signal status_updated(status_id: String, current_duration: float)

@export var target: Node

var active_statuses: Dictionary = {}

func _ready() -> void:
	if target == null:
		target = get_parent()

func _process(delta: float) -> void:
	# Iteramos sobre un duplicado de las claves para poder modificar el diccionario active_statuses
	# (remover estados que expiran) de forma segura durante el bucle.
	var keys = active_statuses.keys()
	for status_id in keys:
		if not active_statuses.has(status_id):
			continue
			
		var status: StatusEffect = active_statuses[status_id]
		
		# 1. Manejo del Tick (Efecto periódico)
		status.time_since_last_tick += delta
		if status.time_since_last_tick >= status.tick_interval:
			status.on_tick(target)
			status.time_since_last_tick -= status.tick_interval

		# 2. Manejo de la Duración (Si no es de entorno)
		if not status.is_environment_based:
			if not status.is_paused:
				status.current_duration -= delta
				status_updated.emit(status_id, status.current_duration)
				if status.current_duration <= 0.0:
					remove_status(status_id)
			else:
				status.current_duration = status.max_duration
				status_updated.emit(status_id, status.current_duration)


func apply_status(new_status: StatusEffect) -> void:
	if new_status == null:
		return
		
	var status_id = new_status.id
	if status_id.is_empty():
		push_error("StatusManager: Intentando aplicar un estado sin ID.")
		return
		
	if active_statuses.has(status_id):
		# Reiniciar duración al máximo (Stacking básico)
		var existing_status: StatusEffect = active_statuses[status_id]
		existing_status.current_duration = new_status.max_duration
		status_updated.emit(status_id, existing_status.current_duration)
	else:
		# Instanciar el recurso para evitar modificar el original compartido
		var status_instance: StatusEffect = new_status.duplicate()
		status_instance.current_duration = status_instance.max_duration
		status_instance.time_since_last_tick = 0.0
		active_statuses[status_id] = status_instance
		
		status_instance.on_start(target)
		status_added.emit(status_id, status_instance.icon, status_instance.max_duration)

func remove_status(status_id: String) -> void:
	if active_statuses.has(status_id):
		var status: StatusEffect = active_statuses[status_id]
		status.on_end(target)
		active_statuses.erase(status_id)
		status_removed.emit(status_id)

func clear_all() -> void:
	var keys = active_statuses.keys()
	for status_id in keys:
		remove_status(status_id)

func pause_status_duration(status_id: String, paused: bool) -> void:
	if active_statuses.has(status_id):
		var status: StatusEffect = active_statuses[status_id]
		status.is_paused = paused
		if not paused:
			status.current_duration = status.max_duration
			status_updated.emit(status_id, status.current_duration)

