class_name ComponentBase extends Node

signal use_completed

@export var consumable: bool = false
@export var use_time: float = 0.0    ## Tiempo de uso/cooldown en segundos

func can_execute(_user: Node) -> bool:
	return true

func execute(_user: Node) -> void:
	pass

func on_used(slot: InventorySlot) -> void:
	if consumable and slot != null:
		slot.remove(1)
