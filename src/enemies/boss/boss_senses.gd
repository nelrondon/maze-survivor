extends Node
class_name BossSenses

signal player_detected(player: Node3D, reason: String)
signal player_lost(player: Node3D)

var players_in_hearing: Array[Node3D] = []

@onready var hearing_zone = $HearingZone
@onready var smell_zone = $SmellZone

var is_active: bool = true

func _ready():
	hearing_zone.body_entered.connect(_on_hearing_zone_entered)
	hearing_zone.body_exited.connect(_on_hearing_zone_exited)
	smell_zone.body_entered.connect(_on_smell_zone_entered)

func _physics_process(_delta):
	if not is_active:
		return
	_check_for_noise()

func _check_for_noise():
	for player in players_in_hearing:
		var speed = player.velocity.length()
		var is_crouching = false

		if "is_crouched" in player:
			is_crouching = player.is_crouched
		else:
			is_crouching = speed < 2.0

		if speed > 0.5 and not is_crouching:
			player_detected.emit(player, "¡EL OYENTE ESCUCHÓ TUS PASOS Y SE LANZA AL ATAQUE!")
			break

func _on_smell_zone_entered(body):
	if body.is_in_group("Player"):
		player_detected.emit(body, "¡EL OYENTE SINTIÓ TU OLOR A MUY CORTA DISTANCIA!")

func _on_hearing_zone_entered(body):
	if body.is_in_group("Player"):
		if not players_in_hearing.has(body):
			players_in_hearing.append(body)

func _on_hearing_zone_exited(body):
	if body.is_in_group("Player"):
		players_in_hearing.erase(body)
		player_lost.emit(body)
