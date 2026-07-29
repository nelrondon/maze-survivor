# Documentación Técnica: Regeneración Pasiva de Estamina y Sistema de Carrera (Sprint)

Este documento detalla la arquitectura, parámetros de balance y guía de extensión para el nuevo sistema de **Regeneración Pasiva de Estamina** y la mecánica de **Carrera/Sprint con la tecla Shift**.

---

## 1. Resumen de la Funcionalidad

* **Carrera (Sprint)**: El jugador puede mantener presionada la tecla `Shift` para aumentar su velocidad de movimiento un 40% a cambio de un consumo continuo de estamina (12.0 pts/sec).
* **Delay de Recuperación (1.5s)**: Al saltar o correr, la regeneración se pausa. La barra de estamina no comienza a rellenarse hasta transcurrir 1.5 segundos consecutivos sin consumo.
* **Recuperación Progresiva (+20.0 pts/sec)**: Una vez transcurrido el delay, la estamina se regenera automáticamente hasta alcanzar el máximo (100.0).
* **Penalización por Inanición (Hambre < 20)**: Si el Hambre cae por debajo de 20 HP, la velocidad de regeneración de estamina se reduce automáticamente al 50% (+10.0 pts/sec).
* **Integración con Debuffs**: Los estados de entorno (como *Asfixia*) pueden bloquear temporalmente la regeneración mediante la bandera `CanRegenStamina`.

---

## 2. Archivos Modificados

### A. Lógica de Estadísticas y Regeneración
Ubicación: [Player/Player.Stats.cs](../Player/Player.Stats.cs)

* **Variables Miembro**:
  * `_timeSinceLastStaminaUse`: Contador de tiempo en segundos desde el último gasto de estamina.
  * `_staminaRegenDelay = 1.5f`: Tiempo de espera necesario en segundos.
  * `_staminaRegenRate = 20.0f`: Tasa base de regeneración por segundo.
  * `CanRegenStamina = true`: Bandera pública para bloquear la regeneración desde debuffs.
* **Reinicio de Cooldown en `modify_stat`**:
  Cada vez que la estamina (stat `1`) sufre una modificación negativa (`value < 0f`), `_timeSinceLastStaminaUse` se reinicia a `0.0f`.
* **Función `ProcessStaminaRegen(double delta)`**:
  Incrementa `_timeSinceLastStaminaUse += (float)delta`. Cuando se cumple el tiempo de delay y `CanRegenStamina` es verdadero, calcula la tasa efectiva (considerando Hambre < 20) y actualiza `_stats[1]` emitiendo `SignalName.stats_changed`.

### B. Controlador del Jugador y Movimiento
Ubicación: [Player/Player.cs](../Player/Player.cs)

* **Invocación en `_PhysicsProcess`**:
  Se invoca `ProcessStaminaRegen(delta)` en el ciclo de físicas de la autoridad multijugador local.
* **Mecánica de Carrera**:
  Detecta la presión de la tecla `Shift` o la acción `"sprint"`. Si el jugador se está desplazando y dispone de estamina (`GetStat(1) > 0f`), aplica el multiplicador `currentSpeed *= 1.4f` y descuenta `-12.0f * delta` de estamina.

---

## 3. Guía de Pruebas

Al ejecutar la escena principal **[maze.tscn](../maze.tscn)** o cualquier escena de prueba:

1. **Carrera**: Mantén la tecla **`Shift`** mientras te desplazas con **WASD**. El personaje se moverá más rápido y el **rectángulo verde del HUD** disminuirá su ancho.
2. **Regeneración con Delay**: Suelta `Shift` o salta con **Espacio**. Observa que la barra se mantiene estática durante 1.5s y luego se rellena de forma fluida.
3. **Agotamiento**: Si agotas la estamina al 0%, el personaje volverá a su velocidad normal de caminata automáticamente hasta recuperar estamina.
