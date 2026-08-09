class_name BackpackUI extends CanvasLayer
## UI de mochila (cofre). Dos paneles lado a lado:
## Izquierda: mochila (2 filas = 10 slots)
## Derecha: inventario del jugador (3 filas + hotbar)
## Cada backpack en el mapa tiene su propio ItemContainer con loot persistente.

const PLACEHOLDER: Texture2D = preload("res://src/inventory/icons/placeholder.png")

var backpack_container: ItemContainer = null
var inventory: Inventory = null
var is_open: bool = false
var _just_opened: bool = false
var _selected_slot: SlotUI = null

@onready var _panel: Control = %BackpackPanel
# Backpack slots (10 slots, indices 0-9 del backpack_container)
@onready var _bp_slots: Array[SlotUI] = [
	%B0, %B1, %B2, %B3, %B4,
	%B5, %B6, %B7, %B8, %B9,
]
# Inventory hotbar (indices 0-4 del inventory)
@onready var _hotbar_slots: Array[SlotUI] = [%IH0, %IH1, %IH2, %IH3, %IH4]
# Inventory body (indices 5-19 del inventory)
@onready var _inv_slots: Array[SlotUI] = [
	%IS5, %IS6, %IS7, %IS8, %IS9,
	%IS10, %IS11, %IS12, %IS13, %IS14,
	%IS15, %IS16, %IS17, %IS18, %IS19,
]


func _ready() -> void:
	add_to_group("container_ui")
	_panel.visible = false


func setup(p_inventory: Node) -> void:
	inventory = p_inventory
	# Inicializar inventory slots
	for i: int in _hotbar_slots.size():
		_hotbar_slots[i].init(i, inventory, PLACEHOLDER)
		_hotbar_slots[i].slot_clicked.connect(_on_slot_clicked)
		_hotbar_slots[i].slot_shift_clicked.connect(_on_shift_click_inv)
	for i: int in _inv_slots.size():
		_inv_slots[i].init(i + Inventory.COLUMNS, inventory, PLACEHOLDER)
		_inv_slots[i].slot_clicked.connect(_on_slot_clicked)
		_inv_slots[i].slot_shift_clicked.connect(_on_shift_click_inv)

	inventory.changed.connect(_refresh)


## Llamado por backpack.gd: ui.call("open", container, inv)
func open(container: ItemContainer, _inv: Node) -> void:
	if is_open:
		return
	backpack_container = container
	if inventory != _inv and _inv != null:
		setup(_inv)

	# Inicializar backpack slots
	for i: int in _bp_slots.size():
		_bp_slots[i].init(i, backpack_container, PLACEHOLDER)
		if not _bp_slots[i].slot_clicked.is_connected(_on_slot_clicked):
			_bp_slots[i].slot_clicked.connect(_on_slot_clicked)
		if not _bp_slots[i].slot_shift_clicked.is_connected(_on_shift_click_bp):
			_bp_slots[i].slot_shift_clicked.connect(_on_shift_click_bp)

	is_open = true
	_just_opened = true
	call_deferred("_reset_just_opened")
	backpack_container.changed.connect(_refresh)
	_refresh()
	_panel.visible = true
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	for hb: Node in get_tree().get_nodes_in_group("hotbar_ui"):
		hb.visible = false
	if inventory:
		inventory.inventory_opened.emit()


func _reset_just_opened() -> void:
	_just_opened = false


func close() -> void:
	if not is_open:
		return
	is_open = false
	_deselect()
	if backpack_container != null and backpack_container.changed.is_connected(_refresh):
		backpack_container.changed.disconnect(_refresh)
	backpack_container = null
	_panel.visible = false
	Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	for hb: Node in get_tree().get_nodes_in_group("hotbar_ui"):
		hb.visible = true
	if inventory:
		inventory.inventory_closed.emit()


func _unhandled_key_input(event: InputEvent) -> void:
	if not is_open or _just_opened:
		return
	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_E or event.keycode == KEY_TAB or event.keycode == KEY_ESCAPE:
			close()
			get_viewport().set_input_as_handled()


func _refresh() -> void:
	for s: SlotUI in _bp_slots:
		s.refresh()
	for s: SlotUI in _hotbar_slots:
		s.refresh()
	for s: SlotUI in _inv_slots:
		s.refresh()


# --- Click-select ---

func _on_slot_clicked(slot_ui: SlotUI) -> void:
	if _selected_slot == null:
		var slot: InventorySlot = slot_ui._slot()
		if slot != null and not slot.is_empty():
			_selected_slot = slot_ui
			slot_ui.set_selected(true)
	elif _selected_slot == slot_ui:
		_deselect()
	else:
		SlotUI.resolve_move(
			_selected_slot.container, _selected_slot.index,
			slot_ui.container, slot_ui.index)
		_deselect()


# --- Shift+click: transferir entre contenedores ---

func _on_shift_click_bp(index: int) -> void:
	if backpack_container == null or inventory == null:
		return
	var slot: InventorySlot = backpack_container.slots[index]
	if slot.is_empty():
		return
	backpack_container.transfer_to(inventory, index, slot.current_amount)


func _on_shift_click_inv(index: int) -> void:
	if backpack_container == null or inventory == null:
		return
	var slot: InventorySlot = inventory.slots[index]
	if slot.is_empty():
		return
	inventory.transfer_to(backpack_container, index, slot.current_amount)


func _deselect() -> void:
	if _selected_slot != null:
		_selected_slot.set_selected(false)
		_selected_slot = null
