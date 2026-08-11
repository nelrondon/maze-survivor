class_name LootTable extends Resource
## Tabla de loot. Genera ítems aleatorios sin repetir ids.

@export var entries: Array[LootEntry] = []
@export var min_items: int = 1
@export var max_items: int = 4

func generate(container: ItemContainer) -> void:
	if entries.is_empty():
		return

	# 1. Evaluar probabilidad individual para cada entrada disponible
	var passed_entries: Array[LootEntry] = []
	for entry: LootEntry in entries:
		if randf() <= entry.probability:
			passed_entries.append(entry)

	# Si nada pasó por azar, tomar al menos 1 entrada al azar para que el bolso no esté vacío
	if passed_entries.is_empty():
		var copy = entries.duplicate()
		copy.shuffle()
		passed_entries.append(copy[0])

	# 2. Separar curativos (botiquines / vendas) de otros ítems y mezclar dentro de su categoría
	var heal_entries: Array[LootEntry] = []
	var other_entries: Array[LootEntry] = []

	for entry: LootEntry in passed_entries:
		if entry.item_id.begins_with("medkit") or entry.item_id == "bandages":
			heal_entries.append(entry)
		else:
			other_entries.append(entry)

	heal_entries.shuffle()
	other_entries.shuffle()

	# Unir manteniendo los ítems de curación al inicio para garantizar su lugar en la mochila
	var final_pool: Array[LootEntry] = []
	final_pool.append_array(heal_entries)
	final_pool.append_array(other_entries)

	var items_to_add: int = randi_range(min_items, max_items)
	var added: int = 0

	for entry: LootEntry in final_pool:
		if added >= items_to_add:
			break
		var data: ItemData = ItemRegistry.get_data(entry.item_id)
		if data == null:
			continue
		var amount: int = randi_range(entry.min_amount, entry.max_amount)
		var inst: Dictionary = {}
		var comp: ComponentBase = ItemRegistry.get_component(entry.item_id)
		if comp is MeleeWeaponComponent:
			var melee: MeleeWeaponComponent = comp as MeleeWeaponComponent
			inst = MeleeWeaponComponent.create_instance_data(melee.max_durability)
		elif comp is ProjectileWeaponComponent:
			var proj: ProjectileWeaponComponent = comp as ProjectileWeaponComponent
			inst = ProjectileWeaponComponent.create_instance_data(proj.max_ammo)
		container.add_item(data, amount, inst)
		added += 1
