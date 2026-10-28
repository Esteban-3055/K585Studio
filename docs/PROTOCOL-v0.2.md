# Protocolo habilitado en v0.2

> Registro de v0.2. Desde v0.3.0 también se permite editar el perfil 1 y activar perfiles desde Studio. Consulta [los cambios de protocolo y su validación](PROTOCOL-v0.3.md).

La descodificación parte del programa instalado y del firmware de este K585. El repositorio no redistribuye ninguno de esos binarios. Los comandos descritos modifican configuración; no son comandos de actualización de firmware.

## Correspondencia de teclas

El escáner del firmware calcula el índice físico `fila + columna * 8`. En `0x23A4–0x23C8` obtiene el registro de tres bytes en `0x5580 + perfil * 192 + índice * 3`. La tabla de fábrica de `0x4BA9` permitió comprobar las 42 áreas del editor contra los registros correspondientes. `BindingCatalog` conserva esa correspondencia; no permite editar Fn ni los botones M1–M4/REC.

| Registro | Significado |
|---|---|
| `02 02 usage` | Tecla HID normal |
| `02 01 mask` | Modificadores |
| `B0 mask usage` | Combinación de modificadores y una tecla |
| `01 01 mask` | Botón de ratón |
| `01 02 delta` | Rueda vertical |
| `01 05 delta` | Desplazamiento horizontal |
| `03 usageLo usageHi` | Uso Consumer de multimedia |
| `06 A2 n` | Función G original |
| `70 index 01` | Macro del dispositivo, ejecución por pulsación |
| `00 00 00` | Tecla desactivada |

La escritura de un registro usa comando 8, desplazamiento `(perfil - 1) * 192 + índice * 3`, longitud 3. Solo se permiten perfiles 2–4 y áreas físicas conocidas. La tabla Consumer del ejecutable original incluye exactamente los valores utilizados en el catálogo multimedia de Studio.

## Frecuencia USB

Los cuatro manejadores OEM en `0x41C200`, `0x41C280`, `0x41C300` y `0x41C380` escriben valores 0–3 con comando 6 en `(perfil - 1) * 64 + 15`. Los controles de la skin corresponden a 125, 250, 500 y 1000 Hz. Por tanto, la afirmación provisional de v0.1 de que faltaba ubicar un campo global se sustituye por este dato: **el ajuste se almacena por perfil**.

Studio verifica la persistencia del valor mediante lectura. La prueba no mide la frecuencia de llegada de eventos físicos por USB ni garantiza una mejora de latencia.

## Macros

La región de 6144 bytes empieza en `0x5B80`. Cabecera de 16 bytes: firma LE `55AA`, longitud usada en 2–3, número de macros en 4–5. A partir de byte 16 hay punteros LE de dos bytes relativos al inicio de la región. Cada registro contiene número de fotogramas (2 bytes), repeticiones (1), longitud del nombre UTF-16 (1), fotogramas de cuatro bytes y nombre UTF-16.

Un fotograma codifica pausa de 12 bits en unidades de 10 ms, tipo en bits 12–14, pulsación en bit 15 y dos bytes de acción. El motor `0x2634` admite los eventos de teclado/ratón usados por Studio. Se insertan fotogramas sin acción para conservar las pausas **antes** de los eventos del editor. Se comprueba que todas las pulsaciones tengan liberación. Las repeticiones admitidas por esta versión son 1–255.

Studio añade registros sin mover ni reescribir los existentes. Al crear una tabla vacía reserva espacio para 128 punteros. Se escribe primero el registro completo, luego su puntero, luego la longitud y cantidad de registros y, por último, el bindeo de la tecla. Una referencia sin definir en otro perfil impide reutilizar ese identificador. Si una tabla OEM está empaquetada sin espacio para ampliar su directorio, Studio rechaza la operación en vez de mover sus macros. La eliminación local de una macro no borra su registro del dispositivo; los registros antiguos no se compactan en v0.2.

Este orden conserva las macros previas y evita asignar una tecla a datos todavía incompletos. No proporciona una transacción resistente a cualquier corte de alimentación de la memoria del dispositivo.

## Protección y verificaciones

Dos lecturas completas deben coincidir antes de respaldar y escribir. Una configuración modificada externamente invalida el plan. El perfil seleccionado debe estar activo físicamente. Cada operación verifica el resultado mediante dos lecturas completas y compara todos los bloques con el resultado esperado. Un fallo después de iniciar una escritura bloquea la sesión y descarta el estado mostrado como actual; no se reintenta ni se restaura automáticamente un resultado incierto.

La prueba física de v0.2 cambió y restauró, uno a uno, brillo, G5 y frecuencia del perfil 2. Al final coincidieron todos los perfiles, bindeos, macros, RGB y configuración general con el respaldo inicial. La escritura y reproducción de macros se verificaron en emulación nativa: cuatro etapas USB, dos ciclos de W, pausas de 150 ms y liberaciones correctas. Todavía falta comprobar una macro físicamente pulsando la tecla asignada.
