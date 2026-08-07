extends Node3D

@export var items: Array[PackedScene] = []
var player: CharacterBody3D
var current_item_index: int = 0

@onready var stats_label: Label = $UI/HBox/StatsPanel/VBox/StatsLabel
@onready var effects_label: Label = $UI/HBox/EffectsPanel/VBox/EffectsLabel
@onready var items_label: Label = $UI/HBox/ItemsPanel/VBox/ItemsLabel
@onready var log_label: Label = $UI/HBox/LogPanel/VBox/LogLabel

var log_lines: Array[String] = []
const MAX_LOG_LINES = 15

func _ready():
	player = $Player
	player.stats_changed.connect(_on_stats_changed)
	
	var palo = $Player/Head/Camera3D/Palo
	if palo and palo.has_node("anim"):
		palo.get_node("anim").play("equipar")
		
	_update_items_list()
	_update_stats_display()
	_add_log("=== TEST DE INTEGRACION ===")
	_add_log("WASD: Mover | Mouse: Mirar")
	_add_log("Click Izq: Atacar | E: Interactuar")
	_add_log("1-5: Seleccionar Item | C: Usar Item")
	_add_log("K: Dañar Jugador | L: Desbloquear Jugador")
	_add_log("U: Veneno | I: Hambre | O: Toggle Asfixia")


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
			KEY_C: _use_item()
			KEY_K: _damage_player()
			KEY_L: _unlock_player()
			KEY_U: _apply_test_poison()
			KEY_I: _apply_test_hunger()
			KEY_O: _toggle_test_asphyxia()


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
	player.hit(30)
	_add_log("[DAMAGE] Jugador recibe -30 HP")

func _unlock_player():
	player.SetInputLocked(false)
	_add_log("[RESET] Controles desbloqueados")

var _is_asphyxia_active: bool = false

func _apply_test_poison():
	var poison = PoisonStatus.new()
	poison.damage = 5.0
	poison.max_duration = 5.0
	poison.tick_interval = 1.0
	player.apply_status(poison)
	_add_log("[STATUS] Aplicado Veneno (5 HP/s por 5s)")

func _apply_test_hunger():
	var hunger = HungerStatus.new()
	hunger.hunger_drain = 3.0
	hunger.max_duration = 10.0
	hunger.tick_interval = 1.0
	player.apply_status(hunger)
	_add_log("[STATUS] Aplicado Hambre (3 Hambre/s por 10s)")

func _toggle_test_asphyxia():
	_is_asphyxia_active = !_is_asphyxia_active
	if _is_asphyxia_active:
		var asphyxia = AsphyxiaStatus.new()
		asphyxia.damage = 10.0
		asphyxia.tick_interval = 1.0
		player.apply_status(asphyxia)
		_add_log("[STATUS] Entrando a zona de Asfixia (10 HP/s)")
	else:
		player.remove_status("asfixia")
		_add_log("[STATUS] Saliendo de zona de Asfixia")


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
