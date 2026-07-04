extends Node3D

@export var items: Array[PackedScene] = []
var player: Node3D
var current_item_index: int = 0

@onready var stats_label: Label = $UI/HBox/StatsPanel/VBox/StatsLabel
@onready var effects_label: Label = $UI/HBox/EffectsPanel/VBox/EffectsLabel
@onready var items_label: Label = $UI/HBox/ItemsPanel/VBox/ItemsLabel
@onready var log_label: Label = $UI/HBox/LogPanel/VBox/LogLabel

var log_lines: Array[String] = []
const MAX_LOG_LINES = 15

func _ready():
	player = $TestPlayer
	player.stats_changed.connect(_on_stats_changed)
	_update_items_list()
	_update_stats_display()
	_add_log("=== TEST DE CONSUMIBLES ===")
	_add_log("1-9: Seleccionar | E: Usar | D: Daño")

func _process(_delta):
	_update_effects_display()

func _input(event):
	if event is InputEventKey and event.pressed:
		match event.keycode:
			KEY_1: _select_item(0)
			KEY_2: _select_item(1)
			KEY_3: _select_item(2)
			KEY_4: _select_item(3)
			KEY_5: _select_item(4)
			KEY_6: _select_item(5)
			KEY_7: _select_item(6)
			KEY_8: _select_item(7)
			KEY_9: _select_item(8)
			KEY_E: _use_item()
			KEY_D: _damage_player()

func _select_item(index: int):
	if index >= items.size(): return
	current_item_index = index
	var item = items[index].instantiate()
	_add_log("[SELECT] %s" % item.data.display_name)
	item.queue_free()
	_update_items_list()

func _use_item():
	if current_item_index >= items.size():
		_add_log("[ERROR] No hay item")
		return
	var item = items[current_item_index].instantiate()
	add_child(item)
	var comp = item.component
	if comp and comp.can_execute(player):
		_add_log("[USE] %s" % item.data.display_name)
		comp.execute(player)
	else:
		_add_log("[ERROR] No se puede usar %s" % item.data.display_name)
	item.queue_free()

func _damage_player():
	player.modify_stat(Stats.Type.HP, -30)
	player.modify_stat(Stats.Type.STAMINA, -20)
	player.modify_stat(Stats.Type.HUNGER, -25)
	_add_log("[DAMAGE] -30 HP, -20 STA, -25 HUN")

func _on_stats_changed():
	_update_stats_display()

func _update_stats_display():
	if stats_label:
		stats_label.text = player.get_stats_text()

func _update_effects_display():
	if effects_label:
		effects_label.text = player.get_active_effects_text()

func _update_items_list():
	if not items_label: return
	var text = ""
	for i in range(items.size()):
		var item = items[i].instantiate()
		var prefix = ">> " if i == current_item_index else "   "
		text += "%s[%s] %s\n" % [prefix, i + 1, item.data.display_name]
		if item.component is ConsumableComponent:
			for effect in item.component.effects:
				text += "       %s\n" % effect.get_description()
		item.queue_free()
	items_label.text = text

func _add_log(message: String):
	log_lines.append(message)
	if log_lines.size() > MAX_LOG_LINES:
		log_lines.pop_front()
	if log_label:
		log_label.text = "\n".join(log_lines)
