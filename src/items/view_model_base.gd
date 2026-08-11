class_name ViewModelBase extends Node3D
## Base para todos los viewmodels de ítems.
## Cada ítem extiende esta clase e implementa use().

func use() -> void:
	pass

func equip() -> void:
	visible = true

func unequip() -> void:
	visible = false
