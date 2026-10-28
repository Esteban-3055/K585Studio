K585 Studio v0.2.0 para Windows x64.

## Cambios

- Interfaz reconstruida con el estilo de VictusFanControl: superficie gráfica, barra superior, iconos, tarjetas, navegación, botones y pie de estado. Los controles se adaptan juntos al tamaño de la ventana; desaparecen los combos blancos de la pantalla principal.
- Reasignación operativa en perfiles 2–4: teclas, modificadores, combinaciones, ratón, multimedia, funciones habituales y macros. El borde naranja refleja asignaciones diferentes de fábrica y cambios pendientes.
- Frecuencia USB de 125/250/500/1000 Hz guardada por perfil.
- Iluminación, color fijo, Colorful, brillo y velocidad. La lluvia conserva sus controles compatibles con el firmware instalado. Los sliders preparan cambios y no escriben mientras se arrastran.
- Editor y grabador de macros con captura limitada a su zona enfocada, liberaciones, pausas, orden de pasos y repeticiones. Las macros nuevas se añaden sin sobrescribir las existentes.
- Proyectos persistentes con asignaciones, macros y ajustes de iluminación/frecuencia pendientes. Lectura y respaldos completos antes de escribir.
- Instalador EXE por usuario, acceso desde Inicio y desinstalador. Conserva proyectos y respaldos.
- Actualización desde la aplicación: buscar, descargar, comprobar tamaño/SHA-256, reconfirmar la publicación, instalar y reiniciar. Reutiliza descargas válidas al volver a abrir.

## Protección y límites

El perfil 1 está bloqueado. Fn, M1–M4 y REC conservan su función reservada. No hay reset general ni actualización de firmware en esta aplicación. El modo 23 requiere el firmware de lluvia personalizado ya instalado; el modo 20 no existe en ese firmware.

Las macros existentes no se desplazan ni compactan. Una tabla OEM sin espacio para ampliar su directorio se conserva y se rechaza la ampliación. La eliminación de la biblioteca no borra registros del dispositivo. Las pausas de macros se redondean a 10 ms y la velocidad/brillo de iluminación conservan los cinco niveles del hardware.

La prueba USB real escribió, leyó y restauró brillo, G5 y frecuencia del perfil 2; la configuración completa volvió al respaldo. La reproducción de macros se validó con el motor nativo emulado, pendiente de comprobación física pulsando una tecla.

Descarga **K585Studio-0.2.0-Setup-win-x64.exe** para instalar. No requiere instalar .NET ni actualizar el teclado. Cierra las copias anteriores y el programa original antes de usar Studio. También se ofrece el ZIP portable y sumas SHA-256.
