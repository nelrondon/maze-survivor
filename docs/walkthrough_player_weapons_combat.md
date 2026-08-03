# Documentacion Tecnica: Controlador del Jugador, Sistema de Armas y Combate Cuerpo a Cuerpo

Este documento describe la arquitectura completa del controlador del jugador (`Player.cs`), el sistema de equipamiento y combate cuerpo a cuerpo con el palo de madera, la estructura del AnimationTree, y todas las deudas tecnicas pendientes de resolver antes de integraciones futuras como el inventario, armas de fuego y animaciones de apuntado.

---

## 1. Arquitectura del Controlador del Jugador

El jugador se implementa como un `CharacterBody3D` en C# con la clase `Player`, ubicada en [Player/Player.cs](../Player/Player.cs). La decision de usar C# para el controlador fue deliberada: todo lo que toca directamente al jugador (movimiento, camara, armas, combate) se mantiene en C#, mientras que los sistemas de items, efectos y entorno permanecen en GDScript. Ambos lenguajes se comunican exclusivamente via `Call()`, senales y grupos de Godot.

### Estructura de nodos de la escena (player.tscn)

```
Player (CharacterBody3D, C#)
  |-- CharacterVisual (instancia de Idle.fbx, modelo 3D)
  |     |-- rig (Node3D, escala interna ~100)
  |     |     |-- Skeleton3D
  |     |           |-- Head (BoneAttachment3D, hueso "head")
  |     |           |     |-- Camera3D (camara en primera persona)
  |     |           |           |-- RayCast3D (rayo de interaccion, 3m)
  |     |           |-- male_player (MeshInstance3D, malla del personaje)
  |     |           |-- RightHand (BoneAttachment3D, hueso "hand_right")
  |     |           |     |-- HandOffset (Marker3D, punto de montaje para armas)
  |     |           |-- LeftArmIK (SkeletonIK3D, brazo izquierdo -> mano izquierda)
  |     |-- AnimationPlayer (CharLib: walk, run, jump, fall, holdidle, holdwalk, meleeattack)
  |-- CollisionShape3D (CapsuleShape3D)
  |-- StatusManager (GDScript, sistema de efectos de estado)
  |-- HUD (instancia de src/ui/hud.tscn)
  |-- MultiplayerSynchronizer (replica posicion y rotacion)
  |-- AnimationTree (BlendTree principal)
```

### La escala del rig y por que importa

El nodo `rig` dentro de `CharacterVisual` tiene una escala interna de aproximadamente 100 unidades. Esto es producto de la importacion del modelo FBX desde Mixamo, donde la convencion de unidades es centimetros frente a los metros que usa Godot. Esta escala se propaga a todos los nodos hijos del esqueleto, incluyendo los `BoneAttachment3D` como `RightHand`.

Consecuencia directa: cualquier objeto que se monte como hijo de `HandOffset` (o `RightHand`) heredara esa escala de 100x. Para que un arma aparezca a tamano normal en la mano, hay que asignarle `Scale = Vector3.One * 0.01f`, que es exactamente 1/100. Sin esta compensacion, el objeto se vera 100 veces mas grande de lo esperado.

### Autoridad de red y visibilidad

El metodo `_IsLocallyControlled()` determina si la instancia actual es la autoridad local. Si lo es, la camara se activa, el modelo visual se oculta (primera persona) y el mouse se captura. Si no lo es, la camara se desactiva y el modelo se muestra (tercera persona para los demas jugadores). El jugador pertenece a los grupos `"Players"` y `"player"` para deteccion desde GDScript.

---

## 2. Sistema de Equipamiento de Armas

### Flujo de pickup y montaje

El flujo completo de recoger un arma del suelo y montarla en la mano es el siguiente:

1. El jugador camina sobre el arma (o presiona E mirando hacia ella).
2. El script GDScript del arma (`palo_test.gd`) detecta la colision en `_on_pickup_area_body_entered` o la interaccion en `interact(user)`.
3. El script verifica si el jugador tiene el metodo `EquipWeapon`. Si lo tiene, llama `body.EquipWeapon(self)`.
4. En C#, `EquipWeapon` ejecuta la siguiente secuencia:
   - Localiza el punto de montaje: `HandOffset` (hijo de `RightHand`) o `RightHand` como fallback.
   - Libera cualquier arma previamente montada con `QueueFree()`.
   - Reparenta el nodo del arma al punto de montaje con `Reparent(mountPoint, false)`. El `false` indica que no se conserva la transformacion global.
   - Aplica la transformacion de agarre:
     ```csharp
     weaponNode.Position = Vector3.Zero;
     weaponNode.RotationDegrees = _rightHandGripRotation; // (-90, 0, 0)
     weaponNode.Scale = Vector3.One * 0.01f;              // compensa escala 100x del rig
     ```
   - Activa la bandera `_isHoldingWeapon = true`.
   - Solicita la transicion "Armed" en el AnimationTree.
   - Si el arma tiene un nodo `LeftHandTarget`, activa el IK del brazo izquierdo.

5. De vuelta en GDScript, `_pickup()` deshabilita el area de recoleccion del suelo y reproduce la animacion "equipar" si existe.

### Parametros exportados para ajuste fino

Los siguientes parametros se exponen en el Inspector de Godot para ajustar el agarre sin modificar codigo:

| Parametro | Tipo | Default | Descripcion |
|:---|:---|:---|:---|
| `_rightHandGripRotation` | Vector3 | (-90, 0, 0) | Rotacion en grados del arma al montarla. -90 en X gira el eje vertical del modelo para apuntar hacia adelante. |
| `_rightHandGripPosition` | Vector3 | (0, 0, 0) | Desplazamiento local del arma respecto al punto de montaje. Actualmente no se usa en EquipWeapon, pero esta disponible para futuras armas que lo necesiten. |

### Soltar arma (DropWeapon)

Al presionar G, `DropWeapon()` ejecuta la operacion inversa:

- Busca el primer hijo `Node3D` en el punto de montaje.
- Calcula la posicion de soltar: 1.5 metros delante del jugador, a altura Y = 0.2.
- Reparenta el arma a la raiz de la escena con `Reparent(sceneRoot, true)` (conservando transformacion global).
- Resetea la escala a `Vector3.One` (deshace el 0.01f).
- Llama `on_drop()` u `OnDrop()` en el arma si existe el metodo.
- Detiene el IK del brazo izquierdo y desactiva `_isHoldingWeapon`.

---

## 3. El Palo de Madera (Arma de Referencia)

El palo de madera sirve como implementacion de referencia para todas las armas cuerpo a cuerpo del juego. Cualquier arma nueva deberia seguir la misma estructura.

### Estructura de archivos

* **[test/Combate/Golpe/palo_test.gd](../test/Combate/Golpe/palo_test.gd)**: Script del arma en GDScript. Controla pickup, ataque local, hitbox y animaciones propias del arma.
* **[test/Combate/Golpe/palo_madera_test.tscn](../test/Combate/Golpe/palo_madera_test.tscn)**: Escena del arma. Contiene el modelo 3D, las areas de colision, el AnimationPlayer con las animaciones del arma, y el marcador de agarre.

### Estructura de nodos del palo

```
Palo (Node3D, palo_test.gd)
  |-- Madera (instancia de wood_stick_1_lowpoly.glb)
  |     |-- Sketchfab_model (escala interna 0.1)
  |     |-- HandTarget (Marker3D, posicion 0, -0.25, 0)
  |-- Hitbox (Area3D, zona de dano)
  |     |-- CollisionShape3D (BoxShape3D)
  |-- PickupArea (Area3D, zona de recoleccion en el suelo)
  |     |-- CollisionShape3D (BoxShape3D)
  |-- Madera_golpe_sonido (AudioStreamPlayer, sonido de golpe)
  |-- anim (AnimationPlayer, animaciones del arma)
```

### Animaciones del arma (AnimationPlayer "anim")

El palo tiene cuatro animaciones propias que animan la posicion y rotacion del nodo `Madera` dentro del arma:

| Animacion | Duracion | Descripcion |
|:---|:---|:---|
| `Golpear` | 0.5s | Movimiento de golpe: mueve Madera en Z, rota en X/Y simulando un swing. |
| `equipar` | 0.5s | Transicion de equipar: el arma entra desde la izquierda con rotacion -90 en Z. |
| `desequipar` | 0.5s | Transicion de desequipar: el arma sale hacia la izquierda con rotacion. |
| `RESET` | 0.001s | Pose de reposo: posicion y rotacion en Vector3.ZERO. |

La senal `animation_finished` del AnimationPlayer esta conectada a `_on_anim_animation_finished` en el script, que maneja la recarga del ataque (`can_attack = true`) y la ocultacion al desequipar.

### Prevencion de auto-impacto

El hitbox del arma detecta cuerpos que entran en su zona. Para evitar que el jugador se golpee a si mismo, `_on_hitbox_body_entered` recorre la jerarquia de padres del arma buscando un nodo que pertenezca al grupo `"player"`. Si el cuerpo detectado es ese mismo jugador, se ignora.

### Colocar el arma en una escena de prueba

En la escena de integracion ([test/test_player_integration.tscn](../test/test_player_integration.tscn)), el arma se instancia como nodo independiente en el suelo:

```
[node name="PaloSuelo" parent="." instance=ExtResource("3_weapon")]
transform = Transform3D(0.75, 0, 0, 0, 0.75, 0, 0, 0, 0.75, 1.5, 0.2, -1.5)
```

La escala de 0.75 reduce ligeramente el tamano del palo en el suelo. La posicion (1.5, 0.2, -1.5) lo coloca a una altura visible del suelo y cerca del punto de spawn del jugador.

---

## 4. AnimationTree del Jugador

El AnimationTree usa un `AnimationNodeBlendTree` como raiz. El flujo de nodos es el siguiente:

```
Strafe (BlendSpace2D) ---\
                          |--> TransitionStrafeHolding --> TransitionStrafeJumping --> MeleeAttack --> output
StrafeHolding (BlendSpace2D) --/                              |
                                                         Jump (StateMachine) --/
                                                              |
AnimationAttack (meleeattack) --------------------------> MeleeAttack (OneShot, slot 1)
```

### Nodos y sus funciones

| Nodo | Tipo | Funcion |
|:---|:---|:---|
| `Strafe` | BlendSpace2D | Mezcla walk/idle segun direccion (Vector2). Posicion central = idle de Mixamo. |
| `StrafeHolding` | BlendSpace2D | Mezcla holdidle/holdwalk segun direccion. Se usa cuando el jugador sostiene un arma. |
| `TransitionStrafeHolding` | Transition | Alterna entre "Unarmed" (Strafe) y "Armed" (StrafeHolding). Controlado por `_isHoldingWeapon`. |
| `TransitionStrafeJumping` | Transition | Alterna entre "Strafe" (en suelo) y "Jump" (en aire). |
| `Jump` | StateMachine | Maquina de estados: Start -> jump -> fall -> End. Condiciones: IsJumping, IsFalling, IsOnFloor. |
| `MeleeAttack` | OneShot | Dispara la animacion de ataque sobre el flujo principal. `break_loop_at_end = true`. |
| `AnimationAttack` | AnimationNode | Reproduce `CharLib/meleeattack`. |

### Animaciones registradas en CharLib

Todas las animaciones estan en la libreria `CharLib` del `AnimationPlayer`, cargadas desde archivos `.anim` en `assets/player_models/character_model_1/animations/`:

| Animacion | Archivo | Uso |
|:---|:---|:---|
| `walk` | walk.anim | Movimiento frontal sin arma |
| `run` | run.anim | No integrado aun en el BlendSpace (ver deuda tecnica) |
| `jump` | jump.anim | Salto en StateMachine Jump |
| `fall` | fall.anim | Caida en StateMachine Jump. Loop desactivado, duracion ajustada a `_fallPoseTime` (0.3s). |
| `holdidle` | holdidle.anim | Idle sosteniendo arma |
| `holdwalk` | holdwalk.anim | Caminar sosteniendo arma |
| `meleeattack` | meleeattack.anim | Ataque cuerpo a cuerpo (swing) |

### Parametros controlados desde codigo

Todos estos parametros se actualizan cada frame en `_PhysicsProcess`:

```csharp
_animTree.Set("parameters/Strafe/blend_position", _newDir);
_animTree.Set("parameters/StrafeHolding/blend_position", _newDir);
_animTree.Set("parameters/TransitionStrafeHolding/transition_request", _isHoldingWeapon ? "Armed" : "Unarmed");
_animTree.Set("parameters/TransitionStrafeJumping/transition_request", IsOnFloor() ? "Strafe" : "Jump");
_animTree.Set("parameters/Jump/conditions/IsOnFloor", IsOnFloor());
_animTree.Set("parameters/Jump/conditions/IsJumping", isJumping);
_animTree.Set("parameters/Jump/conditions/IsFalling", isFalling);
```

El MeleeAttack se dispara desde `_Input` al presionar click izquierdo, solo si `_isHoldingWeapon` es verdadero:

```csharp
if (isAttackPressed && _isHoldingWeapon && _animTree != null) {
    _animTree.Set("parameters/MeleeAttack/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
}
```

---

## 5. Guia para Integrar Nuevas Armas

### Requisitos minimos de una escena de arma

Para que un arma sea compatible con el sistema actual de `EquipWeapon`, su escena debe cumplir lo siguiente:

1. **Nodo raiz**: `Node3D` con un script que implemente `interact(user: Node3D)`.
2. **Modelo 3D**: Cualquier malla como hijo. El tamano visual final depende de la escala interna del modelo y del factor 0.01 que aplica `EquipWeapon`.
3. **PickupArea** (Area3D): Zona de deteccion para recoger el arma del suelo. Debe emitir `body_entered` y verificar que el cuerpo pertenezca al grupo `"player"`.
4. **Metodo `on_drop()`**: Se invoca automaticamente cuando el jugador suelta el arma con G. Debe reactivar el PickupArea.
5. **AnimationPlayer `anim`** (opcional): Si el arma tiene animaciones propias (golpe, equipar, etc.), deben animar nodos internos del arma, no del jugador.
6. **LeftHandTarget** (Marker3D, opcional): Si se coloca un nodo con este nombre como hijo del arma, el IK del brazo izquierdo se activara automaticamente para posicionar la mano izquierda sobre ese punto.
7. **HandTarget** (Marker3D, opcional): Marcador de agarre. Actualmente no se utiliza en la logica de montaje (la funcion `_FindGripMarker` existe pero no se invoca en `EquipWeapon`), pero puede activarse en el futuro para alineacion automatica del grip.

### Calcular la escala visual del arma

La escala efectiva del modelo visual del arma en la mano del jugador sigue esta formula:

```
escala_global_visual = escala_rig (100) * escala_arma (0.01) * escala_interna_modelo
```

Para el palo de madera: `100 * 0.01 * 0.1 = 0.1` (10% del tamano original del modelo GLB).

Si un arma nueva tiene un modelo a escala 1.0 (sin escala interna), su tamano visible en la mano sera `100 * 0.01 * 1.0 = 1.0`, es decir, el tamano original del modelo. Ajustar la escala interna del modelo dentro de la escena del arma es la forma recomendada de controlar el tamano sin tocar `Player.cs`.

---

## 6. Deudas Tecnicas y Trabajo Pendiente

### 6.1 Sistema de inventario (Prioridad alta)

Actualmente el jugador solo puede sostener un unico objeto en la mano. El sistema de `EquipWeapon` reemplaza (destruye con `QueueFree`) cualquier arma anterior al recoger una nueva.

**Pendiente**: Implementar un sistema de inventario con 3 slots intercambiables por teclado (teclas 1, 2, 3). Cada slot almacenaria una referencia al arma, y el jugador podria alternar entre ellas sin destruir las demas. El arma no activa deberia ocultarse o almacenarse fuera del arbol de nodos.

### 6.2 Animacion de run no integrada en el BlendSpace

La animacion `run.anim` esta registrada en la libreria `CharLib` pero no se utiliza en ninguno de los dos BlendSpace2D (Strafe y StrafeHolding). Los cinco puntos del BlendSpace usan solo `walk` e `idle`. Para integrar `run`, se necesitaria un sistema de sprint que altere el BlendSpace o anada un tercer anillo al BlendSpace que interpole entre walk y run segun la velocidad actual. La animacion de sprint con consumo de estamina ya existe en `Player.Stats.cs` pero no esta reflejada visualmente.

### 6.3 Animaciones de apuntado con armas de fuego (Prioridad media)

El AnimationTree actual no tiene soporte para las siguientes animaciones que se necesitaran cuando se integren armas de fuego:

| Animacion necesaria | Descripcion | Tipo de arma |
|:---|:---|:---|
| `aim_pistol_idle` | Idle apuntando con pistola (una mano) | Pistola |
| `aim_pistol_walk` | Caminar apuntando con pistola | Pistola |
| `aim_rifle_idle` | Idle apuntando con rifle (dos manos) | Rifle |
| `aim_rifle_walk` | Caminar apuntando con rifle | Rifle |
| `shoot_pistol` | Disparo con pistola (OneShot) | Pistola |
| `shoot_rifle` | Disparo con rifle (OneShot) | Rifle |

Para integrar estas animaciones, se necesitaria:

1. Exportar los FBX de Mixamo con las animaciones correspondientes, utilizando el mismo esqueleto base del modelo (`Idle.fbx`).
2. Configurar cada FBX con `save_to_file` en su `.import` apuntando a la carpeta `animations/`, tal como se hizo con las animaciones existentes.
3. Agregar un nuevo `TransitionStrafeAiming` al AnimationTree con estados como "Unarmed", "MeleeArmed", "PistolAim", "RifleAim".
4. Agregar nodos OneShot separados para el disparo de pistola y rifle, similares al `MeleeAttack` existente.
5. En `Player.cs`, extender la logica para distinguir entre tipo de arma equipada y solicitar la transicion correspondiente.

### 6.4 Camara del jugador montada en el hueso de la cabeza

La camara esta montada como hija del `BoneAttachment3D` "Head", que sigue el hueso "head" del esqueleto. Esto significa que la camara se mueve con las animaciones de la cabeza del personaje (cabeceo al caminar, inclinacion al saltar, etc.). Dependiendo del efecto deseado, esto puede ser un feature o un problema:

- Si se quiere una camara estable en primera persona, deberia desmontarse de Head y colocarse como hija directa del nodo Player, a una altura fija.
- Si se quiere que la camara siga el movimiento organico de la cabeza (estilo retro inmersivo), la configuracion actual es correcta.

La transformada actual de la Camera3D (`Transform3D(-0.0097, ...)`) es una escala muy reducida porque compensa la escala 100x del rig. Esto puede causar imprecisiones de punto flotante. Una alternativa mas robusta seria mover la camara fuera de la jerarquia del rig.

### 6.5 SkeletonIK3D deprecado

El nodo `LeftArmIK` usa `SkeletonIK3D`, que esta marcado como obsoleto en Godot 4 (genera warnings CS0618 al compilar). Funciona por ahora, pero deberia migrarse a `SkeletonModifier3D` o al nuevo sistema de IK cuando se estabilice en versiones futuras de Godot.

### 6.6 Metodo `_FindGripMarker` sin uso activo

La funcion `_FindGripMarker` esta implementada en `Player.cs` pero no se invoca actualmente en `EquipWeapon`. Existe como infraestructura para un sistema futuro donde distintas armas puedan definir puntos de agarre personalizados via Marker3D con nombres como "HandTarget", "RightHandTarget", "Grip" o "HandMarker". Si se activa, la logica deberia usar la transformada inversa del marcador para alinear el agarre automaticamente, pero esto require pruebas exhaustivas con la escala del rig.

### 6.7 Limpieza de assets en source/hero

La carpeta `assets/player_models/character_model_1/source/hero/` contiene 7 archivos FBX de animacion que ya no se referencian desde ninguna escena (las animaciones se exportaron a archivos `.anim` independientes). Estos FBX deberian eliminarse para reducir el peso del repositorio:

* `Falling.fbx`, `HoldIdle.fbx`, `HoldWalk.fbx`, `Jump.fbx`, `MeleeAttack.fbx`, `Running.fbx`, `Walking.fbx` (y sus `.import`).
* `hero.fbx` y su `.import` (modelo alternativo no referenciado).
* `man_t256.png` y `man_tex256.png` (duplicados de la textura en `textures/`).

El unico archivo que debe conservarse es `Idle.fbx` (y su `.import`), referenciado directamente por `player.tscn` como modelo base del personaje.

### 6.8 Controles hardcodeados vs InputMap

Varios controles en `_Input` verifican tanto la tecla fisica como la accion del InputMap con un patron dual:

```csharp
bool isInteractPressed = (@event is InputEventKey interactKey && interactKey.Pressed && interactKey.Keycode == Key.E) ||
    (InputMap.HasAction("interact") && @event.IsActionPressed("interact"));
```

Esto es funcional pero redundante. Una vez que todas las acciones esten definidas en el InputMap del proyecto, las verificaciones de tecla fisica deberian eliminarse para mantener la configuracion centralizada.

---

## 7. Controles del Jugador (Estado Actual)

| Tecla / Input | Accion | Condicion |
|:---|:---|:---|
| WASD | Movimiento | Siempre (si no esta bloqueado) |
| Mouse | Rotar camara | Siempre |
| Espacio / "jump" | Saltar | En el suelo |
| Shift / "sprint" | Correr (sprint) | Con estamina disponible |
| E / "interact" | Interactuar / Recoger | Mirando objeto interactable (RayCast3D) |
| Click Izq / "shoot" | Ataque cuerpo a cuerpo | Con arma equipada |
| G / "drop" | Soltar arma | Con arma equipada |
| Escape | Liberar cursor | Siempre |

---

## 8. Guia de Pruebas

### Escena de prueba

La escena principal de pruebas es [test/test_player_integration.tscn](../test/test_player_integration.tscn). Incluye:

- Un jugador con spawn en posicion (0, 1, 0).
- Un palo de madera en el suelo en posicion (1.5, 0.2, -1.5) a escala 0.75.
- Una caja interactable en (0, 1, -4).
- Zonas de entorno: Asfixia (azul, -8, 2, -8) y Veneno (verde, 8, 2, -8).
- HUD con barras de vida, estamina y hambre.
- Minimapa con vista de tercera persona.
- Panel inferior con stats, efectos, items y logs.

### Verificacion del arma

1. Caminar hacia el palo en el suelo. El jugador deberia recogerlo automaticamente al entrar en la PickupArea.
2. En el minimapa, verificar que el palo se ve en la mano derecha del personaje a un tamano proporcional.
3. Presionar click izquierdo: deberia reproducirse la animacion `meleeattack` del personaje y la animacion `Golpear` del arma.
4. Presionar G: el arma deberia soltarse 1.5 metros delante del jugador a altura 0.2.
5. Click izquierdo sin arma: no deberia ocurrir nada.
