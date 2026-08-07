class_name HotbarUI extends Control
## Hotbar siempre visible. Refleja slots 0-4 del Inventory.
## Input: 1-5 selección, scroll cambio, click izquierdo usa.

@export var name_fade_delay: float = 1.5
@export var progress_color: Color = Color(0.3, 0.9, 0.4, 0.8)

var inventory: Inventory = null
var use_handler: ItemUseHandler = null
var input_blocked: bool = false
var _name_tween: Tween
var _progress_bar: ProgressBar = null

@onready var _slots: Array[SlotUI] = [%Slot0, %Slot1, %Slot2, %Slot3, %Slot4]
@onready var _item_name: Label = %ItemName


func _ready() -> void:
	add_to_group("hotbar_ui")
	_item_name.modulate.a = 0.0


func setup(p_inventory: Node, p_handler: Node) -> void:
	if p_inventory == null:
		push_warning("HotbarUI.setup(): inventory is null")
		return

	inventory = p_inventory
	use_handler = p_handler

	var placeholder: Texture2D = preload("res://src/inventory/icons/placeholder.png")
	for i: int in _slots.size():
		_slots[i].init(i, inventory, placeholder)

	inventory.changed.connect(_refresh)
	inventory.hotbar_selection_changed.connect(_on_selection_changed)
	inventory.inventory_opened.connect(func() -> void: input_blocked = true)
	inventory.inventory_closed.connect(func() -> void: input_blocked = false)

	if use_handler:
		use_handler.use_started.connect(_on_use_started)
		use_handler.use_completed.connect(_on_use_finished)
		use_handler.use_cancelled.connect(_on_use_finished)

	_refresh()
	_on_selection_changed(inventory.selected_hotbar)


# ---------- Input ----------

func _input(event: InputEvent) -> void:
	if inventory == null or input_blocked:
		return

	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode >= KEY_1 and event.keycode <= KEY_5:
			inventory.select_hotbar(event.keycode - KEY_1)

	elif event is InputEventMouseButton and event.pressed:
		if event.button_index == MOUSE_BUTTON_WHEEL_UP:
			inventory.select_hotbar(
				(inventory.selected_hotbar - 1 + Inventory.COLUMNS) % Inventory.COLUMNS)
		elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			inventory.select_hotbar(
				(inventory.selected_hotbar + 1) % Inventory.COLUMNS)
		elif event.button_index == MOUSE_BUTTON_LEFT and not _is_ui_open():
			print("[DEBUG] HotbarUI: Left click detected, NOT UI open. Requesting use item.")
			inventory.request_use_item()
		elif event.button_index == MOUSE_BUTTON_LEFT and _is_ui_open():
			print("[DEBUG] HotbarUI: Left click detected but UI is open. IGNORING.")


func _is_ui_open() -> bool:
	return Input.mouse_mode == Input.MOUSE_MODE_VISIBLE


# ---------- Visual ----------

func _refresh() -> void:
	for s: SlotUI in _slots:
		s.refresh()


func _on_selection_changed(index: int) -> void:
	for i: int in _slots.size():
		_slots[i].set_selected(i == index)
	_show_item_name(index)


func _show_item_name(index: int) -> void:
	var slot: InventorySlot = inventory.get_item(index)
	if slot.is_empty():
		_item_name.modulate.a = 0.0
		return
	_item_name.text = slot.item_data.display_name
	if _name_tween and _name_tween.is_running():
		_name_tween.kill()
	_item_name.modulate.a = 1.0
	_name_tween = create_tween()
	_name_tween.tween_interval(name_fade_delay)
	_name_tween.tween_property(_item_name, "modulate:a", 0.0, 0.5)


# ---------- Barra de progreso ----------

func _on_use_started(slot_index: int, use_time: float) -> void:
	if use_time <= 0.0:
		return
	if slot_index < 0 or slot_index >= _slots.size():
		return

	_remove_progress_bar()
	_progress_bar = ProgressBar.new()
	_progress_bar.min_value = 0.0
	_progress_bar.max_value = 1.0
	_progress_bar.value = 0.0
	_progress_bar.show_percentage = false
	_progress_bar.mouse_filter = Control.MOUSE_FILTER_IGNORE

	var fill := StyleBoxFlat.new()
	fill.bg_color = progress_color
	fill.set_corner_radius_all(2)
	_progress_bar.add_theme_stylebox_override("fill", fill)
	var bg := StyleBoxFlat.new()
	bg.bg_color = Color(0, 0, 0, 0.5)
	bg.set_corner_radius_all(2)
	_progress_bar.add_theme_stylebox_override("background", bg)

	_slots[slot_index].get_overlay().add_child(_progress_bar)
	_progress_bar.set_anchors_preset(Control.PRESET_TOP_WIDE)
	_progress_bar.offset_left = 3.0
	_progress_bar.offset_right = -3.0
	_progress_bar.offset_top = 3.0
	_progress_bar.offset_bottom = 8.0

	var tween: Tween = create_tween()
	tween.tween_property(_progress_bar, "value", 1.0, use_time)


func _on_use_finished(_slot_index: int) -> void:
	_remove_progress_bar()


func _remove_progress_bar() -> void:
	if _progress_bar != null and is_instance_valid(_progress_bar):
		_progress_bar.queue_free()
	_progress_bar = null
