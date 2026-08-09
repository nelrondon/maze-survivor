class_name EnvironmentZone extends Area3D

@export var status_effect: StatusEffect
@export var remove_on_exit: bool = true

func _ready() -> void:
	body_entered.connect(_on_body_entered)
	body_exited.connect(_on_body_exited)

func _on_body_entered(body: Node3D) -> void:
	if body.is_in_group("player") and status_effect != null:
		if body.has_method("apply_status"):
			body.apply_status(status_effect)
		if not remove_on_exit and body.has_node("StatusManager"):
			body.get_node("StatusManager").pause_status_duration(status_effect.id, true)

func _on_body_exited(body: Node3D) -> void:
	if body.is_in_group("player") and status_effect != null:
		if remove_on_exit:
			if body.has_method("remove_status"):
				body.remove_status(status_effect.id)
		else:
			if body.has_node("StatusManager"):
				body.get_node("StatusManager").pause_status_duration(status_effect.id, false)
