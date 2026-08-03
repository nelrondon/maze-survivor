extends Node3D

@export var items: Array[PackedScene] = []
var player: CharacterBody3D
var current_item_index: int = 0

@onready var minimap_camera: Camera3D = $MinimapViewport/SubViewport/MinimapCamera3D
@onready var stats_label: Label = $UI/TestLayout/HBox/StatsPanel/VBox/StatsLabel
@onready var effects_label: Label = $UI/TestLayout/HBox/EffectsPanel/VBox/EffectsLabel
@onready var items_label: Label = $UI/TestLayout/HBox/ItemsPanel/VBox/ItemsLabel
@onready var log_label: Label = $UI/TestLayout/HBox/LogPanel/VBox/LogLabel

var log_lines: Array[String] = []
const MAX_LOG_LINES = 10
var _is_asphyxia_active: bool = false

func _ready():
	player = $Player
	if player:
		if player.has_signal("stats_changed"):
			player.stats_changed.connect(_on_stats_changed)
			
		# Force character visual visible for 3rd-person minimap preview
		var visual = player.get_node_or_null("CharacterVisual")
		if visual:
			visual.visible = true
			
		# Equip test weapon animation if present
		var palo = player.get_node_or_null("Head/Camera3D/Palo")
		if palo and palo.has_node("anim"):
			palo.get_node("anim").play("equipar")
			
	# Connect HUD to Player
	var hud = $UI/PlayerHUD
	if hud and hud.has_method("setup_player"):
		hud.setup_player(player)

	_update_items_list()
	_update_stats_display()
	_add_log("=== TEST DE JUGADOR E INTEGRACION UNIFICADA ===")
	_add_log("WASD: Mover | Mouse: Mirar | Click Izq: Atacar | E: Interactuar")
	_add_log("1-5: Seleccionar Item | C: Usar Item")
	_add_log("K: Daño -30 HP | L: Curar / Desbloquear")
	_add_log("U: Veneno | I: Hambre | O: Toggle Asfixia")
	_add_log("Zonas de Entorno: Azul (Asfixia) | Verde (Veneno)")

func _process(_delta):
	_update_effects_display()
	_update_minimap_camera()

func _update_minimap_camera():
	if player and minimap_camera:
		# Position minimap camera behind and above the player looking down
		var target_pos = player.global_position + Vector3(0, 2.5, 3.5)
		minimap_camera.global_position = minimap_camera.global_position.lerp(target_pos, 0.15)
		minimap_camera.look_at(player.global_position + Vector3(0, 0.8, 0))

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
			KEY_L: _resurrect_player()
			KEY_U: _apply_test_poison()
			KEY_I: _apply_test_hunger()
			KEY_O: _toggle_test_asphyxia()

func _select_item(index: int):
	if index >= items.size(): return
	current_item_index = index
	var item = items[index].instantiate()
	_add_log("[SELECT] Item #%d: %s" % [index + 1, item.data.display_name if item.data else "Item"])
	item.queue_free()
	_update_items_list()

func _use_item():
	if current_item_index >= items.size():
		_add_log("[ERROR] No hay item seleccionado")
		return
	var item = items[current_item_index].instantiate()
	add_child(item)
	var comp = item.component
	if comp and comp.can_execute(player):
		var item_name = item.data.display_name if item.data else "Consumible"
		_add_log("[USE] Usado: %s" % item_name)
		comp.execute(player)
	else:
		_add_log("[ERROR] No se puede ejecutar el item")
	item.queue_free()

func _damage_player() -> void:
	if player.has_method("hit"):
		player.hit(30)
		_add_log("[DAMAGE] Daño infligido: -30 HP")
	elif player.has_method("modify_stat"):
		player.modify_stat(0, -30.0) # Stat 0 = HP
		_add_log("[DAMAGE] Daño infligido: -30 HP")

func _resurrect_player() -> void:
	if player.has_method("SetInputLocked"):
		player.SetInputLocked(false)
		
	if player.has_method("modify_stat"):
		player.modify_stat(0, 100.0) # HP
		player.modify_stat(1, 100.0) # Stamina
		player.modify_stat(2, 100.0) # Hunger
		
	if player.has_node("StatusManager"):
		player.get_node("StatusManager").clear_all()
		
	_add_log("[HEAL/RESET] Jugador restaurado al 100%")

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
	if stats_label and player.has_method("get_stats_text"):
		stats_label.text = player.get_stats_text()

func _update_effects_display():
	if effects_label and player.has_method("get_active_effects_text"):
		effects_label.text = player.get_active_effects_text()

func _update_items_list():
	if not items_label: return
	var text = ""
	for i in range(items.size()):
		var item = items[i].instantiate()
		var prefix = ">> " if i == current_item_index else "   "
		var item_name = item.data.display_name if item.data else "Item"
		text += "%s[%d] %s\n" % [prefix, i + 1, item_name]
		item.queue_free()
	items_label.text = text

func _add_log(message: String):
	log_lines.append(message)
	if log_lines.size() > MAX_LOG_LINES:
		log_lines.pop_front()
	if log_label:
		log_label.text = "\n".join(log_lines)
