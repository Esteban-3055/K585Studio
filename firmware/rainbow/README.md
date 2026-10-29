# Núcleo de lluvia arcoíris experimental

`rainbow.c` y `rainbow.ld` son el núcleo y la distribución usados en la investigación nativa. No son un firmware completo y el ELF que producen no se puede instalar en el teclado. Requieren los adaptadores de llamada, puntos de enganche y datos nativos de la imagen estudiada.

`src/K585Studio.Core/RainbowPatch.json` contiene dos planes de diferencias: 898 bytes distintos desde el original y 776 desde Serpenteo, en 50 y 30 tramos respectivamente. Su representación JSON ocupa 5736 bytes. Ambos reconstruyen el mismo código normalizado; la preparación posterior copia la configuración nueva del propietario.

El asset se generó después de enlazar y verificar el candidato fuera de Studio. `Firmware.WithRainbow` comprueba la base, aplica únicamente cambios anteriores a `0x5400`, conserva la configuración y exige la huella final conocida. No utiliza una búsqueda heurística de huecos en el equipo del usuario.

La compilación del núcleo usó Zig/Clang para `thumb-freestanding-eabi`, Cortex-M0, `-Oz`, sin biblioteca estándar ni rutinas de división externas. Cambiar el código, el compilador o la distribución exige repetir el enlace, revisar instrucciones y límites, regenerar los planes y sus huellas y repetir la emulación. No basta con editar la huella para aceptar otro resultado.

La paleta de 192 colores y las tablas nativas no se incluyen como copia en estas fuentes. Se leen en las direcciones del firmware ya estudiado. El núcleo ocupa 818 bytes; con adaptadores, 910. La zona principal queda llena. La descripción de controles y límites está en [FIRMWARE.md](../../docs/FIRMWARE.md).

Esta variante continúa pendiente de prueba física. Estos archivos no proporcionan recuperación del cargador ni una garantía de actualización sin riesgo.
