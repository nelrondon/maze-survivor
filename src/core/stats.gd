class_name Stats

enum Type {
	HP,
	STAMINA,
	HUNGER,
	SPEED,
	SPRINT_SPEED,
	DEFENSE,
	BASE_DAMAGE,
	ATTACK_SPEED
}

const DISPLAY_NAMES = {
	Type.HP: "Vida",
	Type.STAMINA: "Stamina",
	Type.HUNGER: "Hambre",
	Type.SPEED: "Velocidad",
	Type.SPRINT_SPEED: "Velocidad de sprint",
	Type.DEFENSE: "Defensa",
	Type.BASE_DAMAGE: "Daño base",
	Type.ATTACK_SPEED: "Velocidad de ataque"
}

static func get_display_name(type: Type) -> String:
	return DISPLAY_NAMES.get(type, "Desconocido")
