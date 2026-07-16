class_name TrapData extends Resource

## Tipo de trampa (solo informativo / para lógica externa como UI o sonidos).
enum TrapType { SPIKES, ARROW, CAGE }

## Cómo se dispara la trampa.
## AREA_TRIGGER   -> se activa una vez al entrar en el área (ideal para flechas).
## PRESSURE_PLATE -> se activa al entrar y se "libera" al salir (anima una placa).
## TIMED_PATTERN  -> se activa/desactiva sola en ciclos, independiente del jugador
##                   (ej. pinchos que suben y bajan constantemente).
enum ActivationMode { AREA_TRIGGER, PRESSURE_PLATE, TIMED_PATTERN }

@export var id: String = ""
@export var display_name: String = ""
@export var description: String = ""
@export var trap_type: TrapType
@export var activation_mode: ActivationMode = ActivationMode.AREA_TRIGGER
## Mismo sistema que ConsumableComponent: se aplican todos estos Effect sobre
## el cuerpo que activa la trampa. Para daño, usá un StatEffect con value
## negativo sobre el stat de vida; para veneno/sangrado, un TickEffect; para
## ralentizar, un TempStatEffect sobre el stat de velocidad, etc.
@export var effects: Array[Effect] = []
## Tiempo mínimo entre activaciones consecutivas de la misma trampa.
@export var cooldown: float = 1.5

@export_group("Timed Pattern")
## Cuánto dura activa la trampa antes de desactivarse (solo TIMED_PATTERN).
@export var active_time: float = 1.0
## Cuánto dura inactiva antes de volver a activarse (solo TIMED_PATTERN).
@export var inactive_time: float = 2.0
## Retraso antes del primer ciclo, útil para desincronizar varias trampas.
@export var start_delay: float = 0.0
