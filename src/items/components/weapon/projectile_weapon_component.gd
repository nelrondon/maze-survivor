class_name ProjectileWeaponComponent extends WeaponComponent
## Datos de un arma a distancia. La munición vive en slot.instance_data.
## La lógica de disparo (instanciar proyectil, trayectoria) la maneja la escena view_model.

@export var max_ammo: int = 10
@export var projectile_scene: PackedScene

func can_execute(user: Node) -> bool:
	if not super.can_execute(user):
		return false
	# Since can_execute doesn't know the slot, we will check the equipped slot in ItemUseHandler.
	# Actually, ItemUseHandler checks can_execute(player). It is better to check ammo in on_used, 
	# but we can't easily prevent the shot here without the slot reference.
	return true

func on_used(slot: InventorySlot) -> void:
	if slot == null:
		return
	if not slot.instance_data.has("ammo"):
		return
	slot.instance_data["ammo"] = maxi(slot.instance_data["ammo"] - 1, 0)

static func create_instance_data(ammo: int) -> Dictionary:
	return {"ammo": ammo}
