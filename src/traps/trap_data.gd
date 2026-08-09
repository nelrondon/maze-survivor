class_name TrapData extends Resource

enum TrapType { SPIKES, ARROW, CAGE }
enum ActivationMode { AREA_TRIGGER, PRESSURE_PLATE, TIMED_PATTERN }

@export var id: String = ""
@export var display_name: String = ""
@export var description: String = ""
@export var trap_type: TrapType
@export var activation_mode: ActivationMode = ActivationMode.AREA_TRIGGER
@export var effects: Array[Effect] = []
@export var cooldown: float = 1.5

@export_group("Timed Pattern")
@export var active_time: float = 1.0
@export var inactive_time: float = 2.0
@export var start_delay: float = 0.0
