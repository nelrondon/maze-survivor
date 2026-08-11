class_name HandHandler extends Node
## Maneja el montaje/desmontaje de viewmodels en la mano del jugador.

var inventory: Inventory = null
var _current_viewmodel: ViewModelBase = null
var _hand_mount: Node3D = null
var _current_slot_index: int = -1
var _is_switching: bool = false


func _ready() -> void:
	var player: Node = get_parent()
	inventory = player.get_node_or_null("Inventory") as Inventory
	_hand_mount = player.get_node_or_null("Head/Camera3D/HandMount") as Node3D

	if inventory == null:
		push_warning("HandHandler: Inventory not found")
		return
	if _hand_mount == null:
		push_warning("HandHandler: HandMount not found in Head/Camera3D/")
		return

	inventory.hotbar_selection_changed.connect(_on_hotbar_changed)
	inventory.changed.connect(_on_inventory_changed)
	inventory.inventory_opened.connect(_on_inventory_opened)
	inventory.inventory_closed.connect(_on_inventory_closed)

	call_deferred("_on_hotbar_changed", inventory.selected_hotbar)


func get_current_viewmodel() -> ViewModelBase:
	return _current_viewmodel


func _on_hotbar_changed(index: int) -> void:
	_current_slot_index = index
	_update_viewmodel()


func _on_inventory_changed() -> void:
	_update_viewmodel()


func _on_inventory_opened() -> void:
	if _current_viewmodel != null:
		_current_viewmodel.visible = false


func _on_inventory_closed() -> void:
	if _current_viewmodel != null:
		_current_viewmodel.visible = true


func _update_viewmodel() -> void:
	if inventory == null or _hand_mount == null:
		return
	if _current_slot_index < 0 or _current_slot_index >= inventory.slots.size():
		return
	if _is_switching:
		return

	var slot: InventorySlot = inventory.slots[_current_slot_index]
	var needed_scene: PackedScene = null

	if not slot.is_empty() and slot.item_data.view_model != null:
		needed_scene = slot.item_data.view_model

	# Si ya tenemos el viewmodel correcto montado, solo sincronizar
	if _current_viewmodel != null and is_instance_valid(_current_viewmodel) and needed_scene != null:
		if _current_viewmodel.scene_file_path == needed_scene.resource_path:
			_sync_viewmodel_data(slot)
			return

	# Si no necesitamos viewmodel y no hay ninguno, salir
	if _current_viewmodel == null and needed_scene == null:
		return

	# Cambiar viewmodel
	_is_switching = true
	_unmount()
	if needed_scene != null:
		_mount(needed_scene, slot)
	_is_switching = false


func _mount(scene: PackedScene, slot: InventorySlot) -> void:
	var instance: Node3D = scene.instantiate() as Node3D
	if not (instance is ViewModelBase):
		push_warning("HandHandler: view_model no extiende ViewModelBase")
		instance.queue_free()
		return
	_current_viewmodel = instance as ViewModelBase
	_hand_mount.add_child(_current_viewmodel)

	var player: Node = get_parent()
	var is_local: bool = true
	if player != null:
		if player.has_method("IsLocallyControlled"):
			is_local = player.call("IsLocallyControlled")
		elif player.has_method("_IsLocallyControlled"):
			is_local = player.call("_IsLocallyControlled")

	_current_viewmodel.visible = is_local

	_sync_viewmodel_data(slot)
	_current_viewmodel.equip()
	if player != null and player.has_method("SetIsHoldingWeapon"):
		player.call("SetIsHoldingWeapon", true)
	if slot != null and not slot.is_empty() and slot.item_data != null:
		if player != null and player.has_method("SyncEquippedWeapon"):
			player.call("SyncEquippedWeapon", slot.item_data.id)


func _unmount() -> void:
	if _current_viewmodel == null:
		return
	var vm: ViewModelBase = _current_viewmodel
	_current_viewmodel = null
	if is_instance_valid(vm):
		vm.queue_free()
	# Limpiar cualquier hijo residual del mount
	for child: Node in _hand_mount.get_children():
		child.queue_free()
	var player: Node = get_parent()
	if player.has_method("SetIsHoldingWeapon"):
		player.call("SetIsHoldingWeapon", false)
	if player.has_method("SyncEquippedWeapon"):
		player.call("SyncEquippedWeapon", "")


func _sync_viewmodel_data(slot: InventorySlot) -> void:
	if _current_viewmodel == null:
		return
	if "current_slot" in _current_viewmodel:
		_current_viewmodel.current_slot = slot
	var comp: ComponentBase = ItemRegistry.get_component(slot.item_data.id)
	if comp is WeaponComponent:
		var weapon: WeaponComponent = comp as WeaponComponent
		if "damage" in _current_viewmodel:
			_current_viewmodel.damage = weapon.damage
	if comp is ProjectileWeaponComponent:
		var proj: ProjectileWeaponComponent = comp as ProjectileWeaponComponent
		if "max_ammo" in _current_viewmodel:
			_current_viewmodel.max_ammo = proj.max_ammo
