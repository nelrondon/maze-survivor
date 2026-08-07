# Walkthrough y Documentación Técnica: HUD de Estado del Jugador (UI y Mirilla)

Este documento detalla la arquitectura, el diseño visual y las instrucciones de uso/modificación de la nueva **Interfaz de Usuario (HUD)** y el sistema de **Mirilla de Apuntado (Crosshair)** implementados en la sesión de hoy.

El sistema se diseñó de forma modular en 2D sobre la pantalla (`CanvasLayer`), consumiendo eventos desacoplados del jugador y de su gestor de estados.

---

## 1. Resumen de la Implementación

1. **Marco Estático de Piedra**: Renderizado a partir de la textura de pizarra de piedra en `assets/ui/hud_stats_player.png`.
2. **Barras Animadas**: Tres barras con rectángulos de colores vivos (Rojo para Vida, Verde para Estamina, Naranja para Hambre) cuyo ancho se ajusta y anima suavemente mediante `Tween` según la proporción ($\text{actual} / \text{máximo}$).
3. **Llenado Exacto al 100%**: El ancho máximo se calcula de forma dinámica en píxeles según el contenedor padre de la barra (`parent_bg.size.x`), garantizando que al 100% no quede ningún espacio oscuro residual.
4. **Mirilla Centrada (Crosshair)**: Punto de mira discreto de $4 \times 4$ píxeles en el centro exacto de la pantalla para guiado en primera persona.
5. **Gasto de Estamina al Saltar**: Integrado en el controlador del jugador en C#, consumiendo 5 puntos de estamina por cada salto si se dispone del requerimiento mínimo.
6. **Controles de Depuración (Teclas 0 a 6)**: Menú de desarrollo integrado en `Maze.cs` para simular cambios de estadísticas y resurrección en tiempo real.

---

## 2. Estructura de Archivos

Los archivos que componen este desarrollo se organizan en las siguientes rutas relativas al proyecto:

### A. Assets y Recursos Gráficos
* **[assets/ui/hud_stats_player.png](../assets/ui/hud_stats_player.png)**: Textura oficial de la tabla de piedra/pizarra que actúa como marco de fondo del HUD.

### B. Interfaz de Usuario (GDScript)
* **[src/ui/hud.gd](../src/ui/hud.gd)**: Controlador script del HUD.
  * Conecta la señal `stats_changed` emitida por el jugador.
  * Lee estadísticas actuales y máximas vía getters de C#.
  * Anima en paralelo `size:x` y `custom_minimum_size:x` de los `ColorRect` de relleno.
  * Consulta al `StatusManager` para mostrar la lista de efectos y debuffs activos.
* **[src/ui/hud.tscn](../src/ui/hud.tscn)**: Escena principal de la interfaz (`CanvasLayer`):
  * **`Crosshair`**: Nodo `ColorRect` anclado al centro (`anchors_preset = 8`).
  * **`Frame`**: `TextureRect` que carga `hud_stats_player.png` ($200 \times 200\text{px}$).
  * **`Margin`**: `MarginContainer` con padding interno para evitar las correas de cuero superiores.
  * **`VBox`**: Contenedor vertical con las etiquetas numéricas, las barras de color y el panel de efectos.

### C. Lógica del Jugador (C#)
* **[Player/Player.Stats.cs](../Player/Player.Stats.cs)**: Exposición de los métodos accesorios de consulta para GDScript:
  * `get_stat(int stat)`: Devuelve el valor flotante actual del stat.
  * `get_max_stat(int stat)`: Devuelve el valor máximo configurado.
* **[Player/Player.cs](../Player/Player.cs)**: Integración del salto con consumo de estamina en `_PhysicsProcess`:
  ```csharp
  if (!IsOnFloor()) _targetVelocity.Y -= _gravity * (float)delta;
  else if (!_isLocked && Input.IsActionJustPressed("jump")) {
      if (GetStat(1) >= 5f) {
          _targetVelocity.Y = _jumpStrength;
          modify_stat(1, -5f); // Consume 5 de estamina al saltar
      }
  }
  ```

### D. Escena del Laberinto y Debug
* **[Maze/Maze.cs](../Maze/Maze.cs)** y **[maze.tscn](../maze.tscn)**: Instanciación automática del HUD al iniciar el laberinto e inclusión de controladores de prueba en el método `_Input`.

---

## 3. Guía de Atajos de Teclado (Modo Debug)

Al ejecutar la escena **[maze.tscn](../maze.tscn)**, se pueden utilizar los siguientes atajos numéricos para probar el comportamiento de la UI:

| Tecla | Acción | Efecto en el HUD |
| :---: | :--- | :--- |
| **`1`** | Daño a la Vida (-20 HP) | El rectángulo **Rojo** se encoge hacia la izquierda |
| **`2`** | Curar Vida (+20 HP) | El rectángulo **Rojo** se expande suavemente |
| **`3`** | Gastar Estamina (-25) | El rectángulo **Verde** se encoge |
| **`4`** | Recuperar Estamina (+25) | El rectángulo **Verde** se expande |
| **`5`** | Gastar Hambre (-30) | El rectángulo **Naranja** se encoge |
| **`6`** | Recuperar Hambre (+30) | El rectángulo **Naranja** se expande |
| **`0`** | **Resucitar y Desbloquear** | Restablece Vida/Estamina/Hambre al 100%, descongela los controles si el jugador murió y limpia todos los debuffs |

---

## 4. Cómo Modificar o Extender la UI

### A. Para cambiar el tamaño o posición del HUD:
En la escena **[src/ui/hud.tscn](../src/ui/hud.tscn)**:
1. Para mover el HUD de lugar: Modifica `offset_left` y `offset_top` del nodo **`Frame`**.
2. Para ajustar la escala de las barras: Modifica la variable `@export var max_bar_width` en el nodo raíz **`HUD`** o ajusta `custom_minimum_size` del contenedor de fondo.

### B. Para agregar una nueva Barra de Estadística (ej. Oxígeno / Maná):
1. En **[Player/Player.Stats.cs](../Player/Player.Stats.cs)**, define el nuevo índice en el diccionario `_stats` (ej. índice `7`).
2. En **[src/ui/hud.tscn](../src/ui/hud.tscn)**, duplica uno de los contenedores (ej. `HPContainer`), renómbralo (ej. `OxygenContainer`) y ajusta el color del `Fill`.
3. En **[src/ui/hud.gd](../src/ui/hud.gd)**:
   * Declara la variable `@onready var oxygen_fill: ColorRect = $Frame/Margin/VBox/OxygenContainer/Background/Fill`.
   * En `update_bars()`, lee el stat `var oxygen: float = _get_player_stat(7, 100.0)` e invoca `_update_bar(oxygen_fill, oxygen, max_oxygen, "oxygen_tween", animate)`.
