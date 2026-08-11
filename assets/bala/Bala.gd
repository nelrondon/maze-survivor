extends Area3D

# -------------------------
# CONFIGURACIÓN DE LA BALA
# -------------------------
@export var velocidad: float = 20.0
@export var tiempo_vida: float = 1.5

# Variables que reciben la información desde el arma al disparar
var portador: Node3D = null
var damage: float = 1.0

# -------------------------
# READY
# -------------------------
func _ready() -> void:
	# Conecta el evento nativo de colisión
	body_entered.connect(_on_body_entered)

	# Temporizador directo para destruir la bala de forma segura
	get_tree().create_timer(tiempo_vida).timeout.connect(func():
		queue_free()
	)

# -------------------------
# PHYSICS PROCESS
# -------------------------
func _physics_process(delta: float) -> void:
	# Avanza hacia el frente local (Z negativo) transformado al espacio global
	position += -transform.basis.z * velocidad * delta

# -------------------------
# DETECCIÓN DE COLISIÓN
# -------------------------
func _on_body_entered(body: Node3D) -> void:
	# 🔥 AJUSTE DE SEGURIDAD ULTRA PRECISO: 
	# Evita que la bala choque contigo mismo (el portador) o con el arma que la disparó
	if body == portador or body.is_in_group("player") or body.name == "Player":
		return 

	# Si choca con un enemigo, busca si tiene métodos comunes para recibir daño
	if body.has_method("RecibirDanio"):
		body.call("RecibirDanio", damage)
	elif body.has_method("hit"):
		body.call("hit", damage)

	# La bala se destruye al impactar contra una pared u objeto válido
	queue_free()
