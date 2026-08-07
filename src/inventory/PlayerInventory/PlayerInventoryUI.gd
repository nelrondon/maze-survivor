class_name PlayerInventoryUI extends CanvasLayer
## UI del inventario del jugador. Escena fija con 20 slots.
## Tab abre/cierra. Hotbar (slots 0-4) abajo, separada.
## Soporta drag-and-drop y click-select para mover/swap/stack.

const PLACEHOLDER: Texture2D = preload("res://src/inventory/icons/placeholder.png")

var inventory: Inventory = null
var is_open: bool = false
var _selected_slot: SlotUI = null

@onready var _panel: Control = %InventoryPanel
# Hotbar slots (fila 0: indices 0-4)
@onready var _hotbar_slots: Array[SlotUI] = [%H0, %H1, %H2, %H3, %H4]
# Inventory slots (filas 1-3: indices 5-19)
@onready var _inv_slots: Array[SlotUI] = [
	%S5, %S6, %S7, %S8, %S9,
	%S10, %S11, %S12, %S13, %S14,
	%S15, %S16, %S17, %S18, %S19,
]


func setup(p_inventory: Node) -> void:
	inventory = p_inventory
	# Inicializar hotbar slots (indices 0-4)
	for i: int in _hotbar_slots.size():
		_hotbar_slots[i].init(i, inventory, PLACEHOLDER)
		_hotbar_slots[i].slot_clicked.connect(_on_slot_clicked)
		_hotbar_slots[i].slot_shift_clicked.connect(_on_shift_click)
	# Inicializar inventory slots (indices 5-19)
	for i: int in _inv_slots.size():
		_inv_slots[i].init(i + Inventory.COLUMNS, inventory, PLACEHOLDER)
		_inv_slots[i].slot_clicked.connect(_on_slot_clicked)
		_inv_slots[i].slot_shift_clicked.connect(_on_shift_click)

	inventory.changed.connect(_refresh)
	_panel.visible = false


func _unhandled_key_input(event: InputEvent) -> void:
	if not (event is InputEventKey and event.pressed and not event.echo):
		return
	if event.keycode == KEY_TAB:
		if is_open:
			close()
		else:
			open()
		get_viewport().set_input_as_handled()
	elif event.keycode == KEY_ESCAPE and is_open:
		close()
		get_viewport().set_input_as_handled()


func open() -> void:
	if is_open or inventory == null:
		return
	is_open = true
	_refresh()
	_panel.visible = true
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	for hb: Node in get_tree().get_nodes_in_group("hotbar_ui"):
		hb.visible = false
	inventory.inventory_opened.emit()


func close() -> void:
	if not is_open:
		return
	is_open = false
	_deselect()
	_panel.visible = false
	Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	for hb: Node in get_tree().get_nodes_in_group("hotbar_ui"):
		hb.visible = true
	inventory.inventory_closed.emit()


func _refresh() -> void:
	for s: SlotUI in _hotbar_slots:
		s.refresh()
	for s: SlotUI in _inv_slots:
		s.refresh()


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


func _on_shift_click(index: int) -> void:
	if inventory == null:
		return
	var slot: InventorySlot = inventory.slots[index]
	if slot.is_empty():
		return
	# Si está en la hotbar (0-4), mover al primer slot libre del inventario (5-19)
	# Si está en el inventario (5-19), mover al primer slot libre de la hotbar (0-4)
	var is_hotbar: bool = index < Inventory.COLUMNS
	var target_start: int = Inventory.COLUMNS if is_hotbar else 0
	var target_end: int = inventory.slots.size() if is_hotbar else Inventory.COLUMNS
	# Buscar slot donde stackear o primer vacío
	var dst: int = -1
	for i: int in range(target_start, target_end):
		if inventory.slots[i].can_stack(slot.item_data, inventory.max_stack):
			dst = i
			break
	if dst == -1:
		for i: int in range(target_start, target_end):
			if inventory.slots[i].is_empty():
				dst = i
				break
	if dst != -1:
		SlotUI.resolve_move(inventory, index, inventory, dst)


func _deselect() -> void:
	if _selected_slot != null:
		_selected_slot.set_selected(false)
		_selected_slot = null
