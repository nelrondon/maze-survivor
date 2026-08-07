extends ViewModelBase
@export var bala_scene: PackedScene
@export var damage: float = 25.0
@export var weapon_range: float = 50.0
@export var shoot_delay: float = 0.5
@export var reload_time: float = 1.0

@onready var animation_player = get_node_or_null("AnimationPlayer")
@onready var sound_shoot = get_node_or_null("SonidoDisparo") if get_node_or_null("SonidoDisparo") else get_node_or_null("AudioStreamPlayer3D")
@onready var sound_reload = get_node_or_null("SonidoRecarga") if get_node_or_null("SonidoRecarga") else get_node_or_null("AudioStreamPlayer3D2")
@onready var boca_canon = get_node_or_null("Boca_canon")

var can_shoot: bool = true
var is_reloading: bool = false
var _portador: Node3D = null

func _ready() -> void:
	if damage <= 0.0:
		damage = 25.0
	# Encontrar el portador (Player)
	var p = get_parent()
	while p != null:
		if p.is_in_group("player"):
			_portador = p
			break
		p = p.get_parent()

func use() -> void:
	print("[DEBUG] FirearmViewModel: use() called. can_shoot=", can_shoot, " is_reloading=", is_reloading)
	if not can_shoot or is_reloading:
		print("[DEBUG] FirearmViewModel: cannot shoot, returning.")
		return
	print("[DEBUG] FirearmViewModel: firing!")
	_fire()

func _fire() -> void:
	print("[DEBUG] FirearmViewModel: _fire() executing.")
	can_shoot = false
	get_tree().create_timer(shoot_delay).timeout.connect(func(): can_shoot = true)
	
	if animation_player:
		if animation_player.has_animation("reload"):
			animation_player.stop()
			animation_player.play("reload")
		elif animation_player.has_animation("reload2"):
			animation_player.stop()
			animation_player.play("reload2")
		
	if sound_shoot:
		print("[DEBUG] FirearmViewModel: sound_shoot present (will be played by animation)")
	else:
		print("[DEBUG] FirearmViewModel: sound_shoot is null!")
	var cam = get_viewport().get_camera_3d()
	if not cam:
		print("[DEBUG] FirearmViewModel: No camera found!")
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
		print("[DEBUG] FirearmViewModel: Raycast hit ", col.name)
		if col.has_method("hit"):
			col.hit(damage)
			
	_create_tracer(boca_canon.global_position if boca_canon else global_position, hit_point)

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
	
	# Usar una dirección "up" segura para look_at
	var up = Vector3.UP
	if abs(start.direction_to(end).y) > 0.99:
		up = Vector3.RIGHT
		
	mesh_inst.look_at(end, up)
	mesh_inst.rotate_object_local(Vector3.RIGHT, PI/2.0)
	
	var tween = get_tree().create_tween()
	tween.tween_property(mat, "albedo_color:a", 0.0, 0.15)
	tween.tween_callback(mesh_inst.queue_free)

func _process(_delta: float) -> void:
	# Fallback a la tecla R si no existe la acción "recargar"
	var wants_reload = false
	if InputMap.has_action("recargar"):
		wants_reload = Input.is_action_just_pressed("recargar")
	else:
		wants_reload = Input.is_physical_key_pressed(KEY_R)

	if wants_reload and not is_reloading and visible:
		# TODO: Implementar lógica de recarga con inventario si es necesario.
		# Por ahora solo reproducimos la animación de recarga.
		_play_reload_anim()

func _play_reload_anim() -> void:
	is_reloading = true
	if sound_reload:
		print("[DEBUG] FirearmViewModel: sound_reload present (will be played by animation)")
		
	if animation_player:
		if animation_player.has_animation("recoil"):
			animation_player.stop()
			animation_player.play("recoil")
		elif animation_player.has_animation("recoil2"):
			animation_player.stop()
			animation_player.play("recoil2")
		
	get_tree().create_timer(reload_time).timeout.connect(func(): is_reloading = false)

func equip() -> void:
	super.equip()
	is_reloading = false
	can_shoot = true
