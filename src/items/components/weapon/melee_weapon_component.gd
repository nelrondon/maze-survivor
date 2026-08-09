class_name MeleeWeaponComponent extends WeaponComponent

@export var max_durability: float = 100.0
@export var attack_range: float = 2.0
@export var durability_cost: float = 1.0

func on_used(slot: InventorySlot) -> void:
	if slot == null:
		return
	if not slot.instance_data.has("durability"):
		return
	slot.instance_data["durability"] -= durability_cost
	if slot.instance_data["durability"] <= 0.0:
		slot.remove(1)

static func create_instance_data(max_dur: float) -> Dictionary:
	return {"durability": max_dur}
