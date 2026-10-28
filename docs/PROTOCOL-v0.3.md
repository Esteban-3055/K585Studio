# Perfiles y verificación en v0.3

La codificación de iluminación, bindeos, frecuencia y macros conserva el [protocolo de v0.2](PROTOCOL-v0.2.md). Esta versión habilita también el perfil 1 y añade selección de perfil por USB. No modifica firmware.

## Selección

El comando **4**, desplazamiento **10**, longitud **1**, escribe el índice cero del perfil (0–3) en la región general de configuración. La dirección persistente comprobada en el firmware es `0x540A`. No se envía un bloque general completo, no se utiliza reset y no se envían borradores al seleccionar.

`KeyboardSession.SelectProfileAsync` valida el perfil, serializa la operación, compara dos lecturas completas con la instantánea anterior y guarda un respaldo antes del comando. Después, otras dos lecturas deben coincidir exactamente con la instantánea anterior salvo `general[10]`. Si el perfil ya está activo, no escribe. Un rechazo, timeout o cambio inesperado tras el envío deja la sesión bloqueada, sin reintentos ni restauraciones automáticas.

La interfaz muestra el nuevo perfil activo tras la verificación. Si falla, conserva el editor anterior y descarta una lectura incierta. Con el teclado desconectado puede abrirse un editor local; no se habilita Aplicar sin una lectura válida y el perfil activo correspondiente. Al arrancar y al pulsar Conectar / leer, la vista sigue el perfil que informa el dispositivo.

## Edición de los cuatro perfiles

Los perfiles 1–4 usan las mismas fórmulas de desplazamiento:

- Iluminación: comando 6, `(perfil - 1) * 64`, ocho bytes.
- Frecuencia: comando 6, `(perfil - 1) * 64 + 15`, un byte.
- Bindeo: comando 8, `(perfil - 1) * 192 + índice físico * 3`, tres bytes.

Cada plan conserva la región general, RGB y las regiones de iluminación y bindeos de los otros tres perfiles. El resultado se compara completo con el plan, incluido el contenido de los macros globales. Las macros nuevas siguen añadiéndose sin mover ni reescribir las existentes y sin activar referencias sin definir de otros perfiles. Fn y los botones M1–M4/REC siguen reservados.

## Evidencia

Se ejecutaron 188 paquetes producidos por el código C# en el emulador del firmware: selección, iluminación, 41 teclas editables y cuatro frecuencias para cada perfil. Se comprobaron las direcciones, longitudes, datos y conservación de todas las demás regiones persistentes.

La prueba USB de v0.3.0 partió del perfil 1, activó 2, 3 y 4 y restauró 1. Las lecturas de cada paso conservaron todos los ajustes y la lectura final completa coincidió con el respaldo inicial. No se escribieron ajustes de iluminación, bindeos, frecuencia ni macros en esta prueba física. La escritura de esos ajustes en perfil 1 se comprobó en modelo y emulación nativa, sin modificar su configuración real.
