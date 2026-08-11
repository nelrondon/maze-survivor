extends Node
## Test Fase 1: ItemRegistry + componentes + InventorySlot.
## Escena: test/test_items.tscn (Node3D con este script + test_player.gd como hijo).
## Ejecutar como escena principal para ver resultados en consola.

var _passed: int = 0
var _failed: int = 0

func _ready() -> void:
	print("\n========================================")
	print("       TEST FASE 1: ITEMS")
	print("========================================\n")

	_test_registry_consumable()
	_test_registry_weapon()
	_test_data_fields_consumable()
	_test_data_fields_weapon()
	_test_consumable_flag()
	_test_weapon_flag()
	_test_execute_consumable()
	_test_execute_weapon()
	_test_on_used_consumable_decrement()
	_test_on_used_consumable_removes_at_zero()
	_test_on_used_melee_durability()
	_test_on_used_melee_breaks()
	_test_on_used_projectile_ammo()
	_test_on_used_projectile_floor_zero()
	_test_slot_can_stack_consumable()
	_test_slot_cannot_stack_weapon()
	_test_slot_add_respects_max()
	_test_slot_clear()

	print("\n========================================")
	if _failed == 0:
		print("  RESULTADO: %d tests PASSED" % _passed)
	else:
		print("  RESULTADO: %d passed, %d FAILED" % [_passed, _failed])
	print("========================================\n")


func _assert(condition: bool, msg: String) -> void:
	if condition:
		_passed += 1
		print("  [OK]   %s" % msg)
	else:
		_failed += 1
		print("  [FAIL] %s" % msg)


# --- ItemRegistry: carga lazy ---

func _test_registry_consumable() -> void:
	var data: ItemData = ItemRegistry.get_data("bandages")
	_assert(data != null, "Registry carga 'bandages' por lazy loading")

func _test_registry_weapon() -> void:
	var data: ItemData = ItemRegistry.get_data("palo_de_madera")
	_assert(data != null, "Registry carga 'palo_de_madera' por lazy loading")


# --- ItemData: campos correctos ---

func _test_data_fields_consumable() -> void:
	var data: ItemData = ItemRegistry.get_data("bandages")
	if data == null:
		_assert(false, "bandages no encontrado")
		return
	_assert(data.id == "bandages", "bandages.id == 'bandages'")
	_assert(data.display_name != "", "bandages tiene display_name")
	_assert(data.item_type == ItemData.ItemType.CONSUMABLE, "bandages es CONSUMABLE")
	_assert(data.stackable == true, "bandages es stackable")

func _test_data_fields_weapon() -> void:
	var data: ItemData = ItemRegistry.get_data("palo_de_madera")
	if data == null:
		_assert(false, "palo_de_madera no encontrado")
		return
	_assert(data.id == "palo_de_madera", "palo.id == 'palo_de_madera'")
	_assert(data.display_name != "", "palo tiene display_name")
	_assert(data.item_type == ItemData.ItemType.WEAPON, "palo es WEAPON")
	_assert(data.stackable == false, "palo NO es stackable")
	_assert(data.view_model != null, "palo tiene view_model")
	_assert(data.use_sound != null, "palo tiene use_sound")


# --- Flags consumable ---

func _test_consumable_flag() -> void:
	var comp: ComponentBase = ItemRegistry.get_component("bandages")
	if comp == null:
		_assert(false, "bandages component no encontrado")
		return
	_assert(comp.consumable == true, "ConsumableComponent.consumable == true")
	_assert(comp is ConsumableComponent, "bandages es ConsumableComponent")

func _test_weapon_flag() -> void:
	var comp: ComponentBase = ItemRegistry.get_component("palo_de_madera")
	if comp == null:
		_assert(false, "palo component no encontrado")
		return
	_assert(comp.consumable == false, "WeaponComponent.consumable == false")
	_assert(comp is MeleeWeaponComponent, "palo es MeleeWeaponComponent")


# --- execute() no crashea ---

func _test_execute_consumable() -> void:
	var mock: Node = _create_mock_target()
	var comp: ComponentBase = ItemRegistry.get_component("bandages")
	if comp == null:
		_assert(false, "bandages no encontrado")
		mock.queue_free()
		return
	comp.execute(mock)
	_assert(true, "execute() consumible no crasheó")
	mock.queue_free()

func _test_execute_weapon() -> void:
	var mock: Node = _create_mock_target()
	var comp: ComponentBase = ItemRegistry.get_component("palo_de_madera")
	if comp == null:
		_assert(false, "palo no encontrado")
		mock.queue_free()
		return
	comp.execute(mock)
	_assert(true, "execute() arma no crasheó")
	mock.queue_free()


# --- on_used: consumibles ---

func _test_on_used_consumable_decrement() -> void:
	var slot := InventorySlot.new()
	slot.item_data = ItemRegistry.get_data("bandages")
	slot.current_amount = 3
	var comp: ComponentBase = ItemRegistry.get_component("bandages")
	comp.on_used(slot)
	_assert(slot.current_amount == 2,
		"on_used consumible: 3 → 2 (actual: %d)" % slot.current_amount)

func _test_on_used_consumable_removes_at_zero() -> void:
	var slot := InventorySlot.new()
	slot.item_data = ItemRegistry.get_data("bandages")
	slot.current_amount = 1
	var comp: ComponentBase = ItemRegistry.get_component("bandages")
	comp.on_used(slot)
	_assert(slot.is_empty(),
		"on_used consumible con 1 → slot vacío")


# --- on_used: melee durabilidad ---

func _test_on_used_melee_durability() -> void:
	var slot := InventorySlot.new()
	slot.item_data = ItemRegistry.get_data("palo_de_madera")
	slot.current_amount = 1
	slot.instance_data = MeleeWeaponComponent.create_instance_data(50.0)
	var comp: ComponentBase = ItemRegistry.get_component("palo_de_madera")
	comp.on_used(slot)
	_assert(slot.instance_data["durability"] == 49.0,
		"on_used melee: durabilidad 50 → 49 (actual: %s)" % slot.instance_data["durability"])

func _test_on_used_melee_breaks() -> void:
	var slot := InventorySlot.new()
	slot.item_data = ItemRegistry.get_data("palo_de_madera")
	slot.current_amount = 1
	slot.instance_data = MeleeWeaponComponent.create_instance_data(0.5)
	var comp: ComponentBase = ItemRegistry.get_component("palo_de_madera")
	comp.on_used(slot)
	_assert(slot.is_empty(),
		"on_used melee con durabilidad 0.5 → se rompe → slot vacío")


# --- on_used: projectile munición ---

func _test_on_used_projectile_ammo() -> void:
	var slot := InventorySlot.new()
	slot.item_data = ItemData.new()
	slot.current_amount = 1
	slot.instance_data = ProjectileWeaponComponent.create_instance_data(10)
	var comp := ProjectileWeaponComponent.new()
	comp.on_used(slot)
	_assert(slot.instance_data["ammo"] == 9,
		"on_used projectile: ammo 10 → 9 (actual: %s)" % slot.instance_data["ammo"])

func _test_on_used_projectile_floor_zero() -> void:
	var slot := InventorySlot.new()
	slot.item_data = ItemData.new()
	slot.current_amount = 1
	slot.instance_data = ProjectileWeaponComponent.create_instance_data(0)
	var comp := ProjectileWeaponComponent.new()
	comp.on_used(slot)
	_assert(slot.instance_data["ammo"] == 0,
		"on_used projectile: ammo 0 no baja de 0")


# --- InventorySlot ---

func _test_slot_can_stack_consumable() -> void:
	var slot := InventorySlot.new()
	var data: ItemData = ItemRegistry.get_data("bandages")
	slot.item_data = data
	slot.current_amount = 2
	_assert(slot.can_stack(data, 5) == true,
		"Slot con 2 bandages puede stackear (max 5)")

func _test_slot_cannot_stack_weapon() -> void:
	var slot := InventorySlot.new()
	var data: ItemData = ItemRegistry.get_data("palo_de_madera")
	slot.item_data = data
	slot.current_amount = 1
	_assert(slot.can_stack(data, 5) == false,
		"Slot con palo NO puede stackear (stackable=false)")

func _test_slot_add_respects_max() -> void:
	var slot := InventorySlot.new()
	slot.item_data = ItemRegistry.get_data("bandages")
	slot.current_amount = 3
	var leftover: int = slot.add(4, 5)
	_assert(slot.current_amount == 5,
		"add(4, max=5) con 3 → 5 (actual: %d)" % slot.current_amount)
	_assert(leftover == 2,
		"add retorna sobrante 2 (actual: %d)" % leftover)

func _test_slot_clear() -> void:
	var slot := InventorySlot.new()
	slot.item_data = ItemData.new()
	slot.current_amount = 3
	slot.instance_data = {"durability": 50.0}
	slot.clear()
	_assert(slot.is_empty(), "clear() → is_empty()")
	_assert(slot.current_amount == 0, "clear() → amount = 0")
	_assert(slot.instance_data.is_empty(), "clear() → instance_data vacío")


# --- Helpers ---

func _create_mock_target() -> Node:
	var mock := Node.new()
	mock.set_script(load("res://test/test_player.gd"))
	add_child(mock)
	return mock
