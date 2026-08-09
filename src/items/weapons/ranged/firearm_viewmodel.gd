extends ViewModelBase

@export var bala_scene: PackedScene
@export var damage: float = 15.0
@export var weapon_range: float = 60.0
@export var shoot_delay: float = 0.25
@export var reload_time: float = 1.0
@export var max_ammo: int = 10

@onready var animation_player = get_node_or_null("AnimationPlayer")
@onready var sound_shoot = get_node_or_null("SonidoDisparo") if get_node_or_null("SonidoDisparo") else get_node_or_null("AudioStreamPlayer3D")
@onready var sound_reload = get_node_or_null("SonidoRecarga") if get_node_or_null("SonidoRecarga") else get_node_or_null("AudioStreamPlayer3D2")
@onready var boca_canon = get_node_or_null("Boca_canon")

var can_shoot: bool = true
var is_reloading: bool = false
var current_slot: InventorySlot = null
var _portador: Node3D = null

func _ready() -> void:
	if damage <= 0.0:
		damage = 45.0
	var p = get_parent()
	while p != null:
		if p.is_in_group("player"):
			_portador = p
			break
		p = p.get_parent()

func use() -> void:
	if not can_shoot or is_reloading:
		return

	# Buscar y consumir 1 bala directamente del inventario en cada disparo
	var inv: Inventory = null
	if _portador:
		inv = _portador.get_node_or_null("Inventory") as Inventory

	var bullet_consumed: bool = false
	if inv != null:
		for slot in inv.slots:
			if not slot.is_empty() and slot.item_data.id == "bala" and slot.current_amount > 0:
				slot.current_amount -= 1
				if slot.current_amount <= 0:
					slot.clear()
				bullet_consumed = true
				break

	if not bullet_consumed:
		print("[FirearmViewModel] ¡Sin balas en el inventario para disparar!")
		return

	# Notificar al inventario para refrescar la UI (Hotbar e Inventario) en tiempo real
	_notify_inventory_changed()

	# Sincronizar cargador si aplica
	if current_slot != null:
		var current_ammo: int = current_slot.instance_data.get("ammo", max_ammo)
		current_slot.instance_data["ammo"] = maxi(0, current_ammo - 1)

	_fire()

func _fire() -> void:
	can_shoot = false
	get_tree().create_timer(shoot_delay).timeout.connect(func(): can_shoot = true)

	# Reproducir animación de disparo (recoil)
	if animation_player:
		if animation_player.has_animation("recoil"):
			animation_player.stop()
			animation_player.play("recoil")
		elif animation_player.has_animation("recoil2"):
			animation_player.stop()
			animation_player.play("recoil2")
	elif sound_shoot and not sound_shoot.playing:
		sound_shoot.play()

	var cam = get_viewport().get_camera_3d()
	if not cam:
		return

	var start_pos = cam.global_position
	var forward_dir = -cam.global_transform.basis.z.normalized()
	var end_pos = start_pos + forward_dir * weapon_range

	var space_state = cam.get_world_3d().direct_space_state
	var query = PhysicsRayQueryParameters3D.create(start_pos, end_pos)
	if _portador and _portador is CollisionObject3D:
		query.exclude = [_portador.get_rid()]

	var result = space_state.intersect_ray(query)
	var hit_point = end_pos

	if result:
		hit_point = result.position
		var col = result.collider
		var target = col
		if target != null and not target.has_method("hit") and target.get_parent() != null and target.get_parent().has_method("hit"):
			target = target.get_parent()

		if target != null and target.has_method("hit"):
			print("[FirearmViewModel] ¡Impacto directo en ", target.name, "! Daño infligido: ", damage)
			target.hit(damage, _portador)

	_create_tracer(boca_canon.global_position if boca_canon else global_position, hit_point)

func reload() -> void:
	if is_reloading or not visible:
		return

	if current_slot == null:
		return

	var current_ammo: int = current_slot.instance_data.get("ammo", 0)
	if current_ammo >= max_ammo:
		return

	var needed: int = max_ammo - current_ammo
	var inv: Inventory = null
	if _portador:
		inv = _portador.get_node_or_null("Inventory") as Inventory

	var bullets_taken: int = 0

	if inv != null:
		# Buscar stacks de balas en el inventario
		for slot in inv.slots:
			if not slot.is_empty() and slot.item_data.id == "bala":
				var available = slot.current_amount
				var take = mini(needed - bullets_taken, available)
				slot.current_amount -= take
				bullets_taken += take

				if slot.current_amount <= 0:
					slot.clear()

				if bullets_taken >= needed:
					break

	if bullets_taken <= 0 and current_ammo <= 0:
		print("[FirearmViewModel] ¡Sin munición en el inventario para recargar!")
		return

	is_reloading = true

	# Reproducir animación de recarga (reload)
	if animation_player:
		if animation_player.has_animation("reload"):
			animation_player.stop()
			animation_player.play("reload")
		elif animation_player.has_animation("reload2"):
			animation_player.stop()
			animation_player.play("reload2")
	elif sound_reload and not sound_reload.playing:
		sound_reload.play()

	var loaded_amount: int = bullets_taken
	get_tree().create_timer(reload_time).timeout.connect(func():
		if current_slot != null:
			current_slot.instance_data["ammo"] = current_ammo + loaded_amount
			_notify_inventory_changed()
		is_reloading = false
		can_shoot = true
	)

func _unhandled_input(event: InputEvent) -> void:
	if not visible:
		return
	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_R:
			reload()

func _notify_inventory_changed() -> void:
	if _portador:
		var inv = _portador.get_node_or_null("Inventory")
		if inv and inv.has_signal("changed"):
			inv.changed.emit()

func _create_tracer(start: Vector3, end: Vector3) -> void:
	var distance = start.distance_to(end)
	if distance < 0.1: return

	var mesh_inst = MeshInstance3D.new()
	var cyl = CylinderMesh.new()
	cyl.top_radius = 0.02
	cyl.bottom_radius = 0.02
	cyl.height = distance
	cyl.radial_segments = 4
	mesh_inst.mesh = cyl

	var mat = StandardMaterial3D.new()
	mat.albedo_color = Color(1.0, 1.0, 0.8, 0.7)
	mat.emission_enabled = true
	mat.emission = Color(1.0, 1.0, 0.8)
	mat.emission_energy_multiplier = 2.0
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mesh_inst.material_override = mat

	var scene_root = get_tree().current_scene
	if scene_root:
		scene_root.add_child(mesh_inst)
	else:
		get_tree().root.add_child(mesh_inst)

	mesh_inst.global_position = start.lerp(end, 0.5)

	var up = Vector3.UP
	if abs(start.direction_to(end).y) > 0.99:
		up = Vector3.RIGHT

	mesh_inst.look_at(end, up)
	mesh_inst.rotate_object_local(Vector3.RIGHT, PI/2.0)

	var tween = get_tree().create_tween()
	tween.tween_property(mat, "albedo_color:a", 0.0, 0.15)
	tween.tween_callback(mesh_inst.queue_free)

func equip() -> void:
	super.equip()
	is_reloading = false
	can_shoot = true
