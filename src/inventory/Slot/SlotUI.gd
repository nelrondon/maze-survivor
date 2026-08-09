class_name SlotUI extends PanelContainer
## Slot visual. Drag-and-drop nativo + click-select + hover + tooltip BBCode.

signal slot_shift_clicked(index: int)
signal slot_clicked(slot_ui: SlotUI)

static var style_normal: StyleBoxFlat
static var style_selected: StyleBoxFlat
static var style_hovered: StyleBoxFlat

var index: int = -1
var container: ItemContainer = null
var placeholder: Texture2D
var _is_hovered: bool = false
var _is_selected: bool = false

@onready var icon: TextureRect = %Icon
@onready var amount_label: Label = %Amount


static func _build_styles() -> void:
	if style_normal:
		return
	style_normal = StyleBoxFlat.new()
	style_normal.bg_color = Color(0.18, 0.18, 0.22)
	style_normal.set_corner_radius_all(6)
	style_normal.set_border_width_all(2)
	style_normal.border_color = Color(0.35, 0.35, 0.42, 0.6)
	style_normal.set_content_margin_all(5)

	style_selected = style_normal.duplicate()
	style_selected.bg_color = Color(0.35, 0.35, 0.42)
	style_selected.border_color = Color(0.95, 0.85, 0.3, 0.95)

	style_hovered = style_normal.duplicate()
	style_hovered.bg_color = Color(0.25, 0.25, 0.30)
	style_hovered.border_color = Color(0.6, 0.6, 0.7, 0.8)


func _ready() -> void:
	_build_styles()
	focus_mode = Control.FOCUS_NONE
	mouse_filter = Control.MOUSE_FILTER_STOP
	mouse_entered.connect(func() -> void: _is_hovered = true; _update_style())
	mouse_exited.connect(func() -> void: _is_hovered = false; _update_style())
	_update_style()


func init(p_index: int, p_container: ItemContainer, p_placeholder: Texture2D = null) -> void:
	index = p_index
	container = p_container
	placeholder = p_placeholder
	if is_node_ready():
		refresh()


func set_selected(on: bool) -> void:
	_is_selected = on
	_update_style()


func get_overlay() -> Control:
	if amount_label == null:
		return null
	return amount_label.get_parent()


func _slot() -> InventorySlot:
	if container == null or index < 0 or index >= container.slots.size():
		return null
	return container.slots[index]


func refresh() -> void:
	if icon == null or amount_label == null:
		return
	var slot: InventorySlot = _slot()
	if slot == null or slot.is_empty():
		icon.texture = null
		amount_label.text = ""
		tooltip_text = ""
		return
	icon.texture = slot.item_data.icon if slot.item_data.icon else placeholder
	amount_label.text = str(slot.current_amount) if slot.current_amount > 1 else ""
	tooltip_text = _build_tooltip(slot)


func _update_style() -> void:
	if _is_selected:
		add_theme_stylebox_override("panel", style_selected)
	elif _is_hovered:
		add_theme_stylebox_override("panel", style_hovered)
	else:
		add_theme_stylebox_override("panel", style_normal)


# ---------- Drag-and-drop nativo ----------

func _get_drag_data(_pos: Vector2):
	var slot: InventorySlot = _slot()
	if slot == null or slot.is_empty():
		return null
	var preview := TextureRect.new()
	preview.texture = icon.texture
	preview.custom_minimum_size = Vector2(48, 48)
	preview.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	preview.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	preview.modulate.a = 0.85
	set_drag_preview(preview)
	return {"index": index, "container": container}


func _can_drop_data(_pos: Vector2, data) -> bool:
	return data is Dictionary and data.has("container") and data.has("index") \
			and container != null


func _drop_data(_pos: Vector2, data) -> void:
	resolve_move(data["container"], data["index"], container, index)


static func resolve_move(src_c: ItemContainer, src_i: int, dst_c: ItemContainer, dst_i: int) -> void:
	if src_c == dst_c and src_i == dst_i:
		return
	var src: InventorySlot = src_c.slots[src_i]
	var dst: InventorySlot = dst_c.slots[dst_i]
	if src.is_empty():
		return
	# Stack
	if not dst.is_empty() and dst.can_stack(src.item_data, dst_c.max_stack):
		var leftover: int = dst.add(src.current_amount, dst_c.max_stack)
		var moved: int = src.current_amount - leftover
		if moved > 0:
			src.remove(moved)
		src_c.changed.emit()
		if src_c != dst_c:
			dst_c.changed.emit()
		return
	# Swap mismo contenedor
	if src_c == dst_c:
		src_c.swap_slots(src_i, dst_i)
		return
	# Swap/mover entre contenedores
	if not dst_c._can_place(dst_i, src.item_data):
		return
	if not dst.is_empty() and not src_c._can_place(src_i, dst.item_data):
		return
	src_c.slots[src_i] = dst
	dst_c.slots[dst_i] = src
	src_c.changed.emit()
	dst_c.changed.emit()


# ---------- Click ----------

func _gui_input(event: InputEvent) -> void:
	if not (event is InputEventMouseButton and event.pressed):
		return
	if event.button_index != MOUSE_BUTTON_LEFT:
		return
	if event.shift_pressed or event.ctrl_pressed:
		slot_shift_clicked.emit(index)
	else:
		slot_clicked.emit(self)
	accept_event()


# ---------- Tooltip ----------

func _make_custom_tooltip(for_text: String) -> Object:
	var rt := RichTextLabel.new()
	rt.bbcode_enabled = true
	rt.fit_content = true
	rt.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	rt.custom_minimum_size = Vector2(230, 0)
	rt.text = for_text
	return rt


func _build_tooltip(slot: InventorySlot) -> String:
	var d: ItemData = slot.item_data
	var t: String = "[b]%s[/b]\n" % d.display_name
	t += "[color=#aaaacc]%s[/color]" % _type_name(d.item_type)
	if d.description != "":
		t += "\n%s" % d.description
	var fx: String = _effects_text(slot)
	if fx != "":
		t += "\n[color=#8fd694]%s[/color]" % fx
	return t


func _type_name(item_type: ItemData.ItemType) -> String:
	match item_type:
		ItemData.ItemType.CONSUMABLE:
			return "Consumible"
		ItemData.ItemType.INTERACTABLE:
			return "Interactuable"
		ItemData.ItemType.WEAPON:
			return "Arma"
		_:
			return "Objeto"


func _effects_text(slot: InventorySlot) -> String:
	var comp: ComponentBase = ItemRegistry.get_component(slot.item_data.id)
	if comp == null:
		return ""
	var effects = comp.get("effects")
	if effects == null or effects.is_empty():
		return ""
	var text: String = ""
	for effect in effects:
		if effect.has_method("get_description"):
			var desc: String = effect.get_description()
			if desc != "":
				text += "• " + desc + "\n"
	return text.strip_edges()
