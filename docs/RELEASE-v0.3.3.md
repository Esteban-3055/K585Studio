# K585 Studio v0.3.3

Incluye la actualización opcional de firmware con el preset fijo de lluvia seleccionado: entrada suave de 1,998 s, caída media de 1,845 s con ritmos variables, ciclo arcoíris de 4,5 s y hasta cinco estelas con nacimientos repartidos. G1–G5 participan y las gotas desde G2 alternan finales Ctrl/Fn. El color fijo sigue disponible.

En **Actualizaciones → Firmware del teclado**, activa **Preparar lluvia seleccionada · preset fijo (experimental)** y pulsa **Preparar y respaldar**. La aplicación utiliza el actualizador original integrado, conserva la configuración recién leída y vuelve a verificarla antes de abrir la herramienta. No necesitas descargar un firmware separado.

**Instalar o actualizar Studio no instala firmware en el teclado.** La grabación se inicia manualmente con **Start update** en la herramienta original. Al terminar, utiliza **Verificar ajustes**. Para reproducir el preset, selecciona después **perfil 2 → efecto 23 → Colorful → velocidad mínima**. La preparación conserva los ajustes existentes; no activa esos valores automáticamente.

El firmware sigue siendo **experimental y pendiente de prueba física**: se comprobaron 124.000 cuadros en emulación, incluida una secuencia de 30 minutos nominales. Eso no garantiza recuperación si falla una actualización. Solo se estudió la placa 8388-A-Rev:1(16A), chip VS11K16A, USB 0C45:5004 y revisión 0x0108. El efecto 1 se conserva; el modo 20 sigue retirado y el modo 23 funciona únicamente en el perfil 2.

Los tiempos del preset son fijos; el motor con todos los parámetros configurables queda pendiente. Se mantienen los cinco niveles nativos de brillo y velocidad: mover la velocidad también escala entrada y pausas. El ciclo de color conserva sus 4,5 s nominales.

Se incluyen las fuentes y la distribución comprobada del candidato. Las diferencias incorporadas a la aplicación contienen solo código, sin perfiles ni macros del propietario. Se conservan el respaldo, el seguimiento pendiente entre sesiones y las comprobaciones anteriores del actualizador.
