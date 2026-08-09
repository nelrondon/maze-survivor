class_name LootTable extends Resource
## Tabla de loot. Genera ítems aleatorios sin repetir ids.

@export var entries: Array[LootEntry] = []
@export var min_items: int = 2
@export var max_items: int = 5

func generate(container: ItemContainer) -> void:
	var available: Array[LootEntry] = entries.duplicate()
	available.shuffle()
	var items_to_add: int = randi_range(min_items, max_items)
	var added: int = 0

	for entry: LootEntry in available:
		if added >= items_to_add:
			break
		if randf() > entry.probability:
			continue
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
