# Gestor de firmware

Studio utiliza el actualizador OEM ya auditado como motor de grabación. No implementa nuevos comandos de borrado o programación ni automatiza Start update. Desde v0.3.2 el EXE original viene incluido como recurso comprimido; no necesita un archivo externo, acceso a Internet ni una ruta del equipo del propietario. También se puede seleccionar otro EXE local reconocido.

Al abrir la pestaña de firmware se descomprime una copia acotada a 2.870.272 bytes en memoria y se comprueba el SHA-256 completo `5e454925dce22c25c86f3ea5d71be80aa9dbfabdcc215c7e05b5fa2e9e85d72f`, además de las huellas del código del actualizador y del firmware original. Cada lectura produce un arreglo nuevo; una modificación no altera la copia integrada.

No se extrae ni ejecuta el EXE al abrir el panel, iniciar o instalar Studio. El archivo ejecutable privado se genera al preparar, después de leer y respaldar el teclado, y con todos los ajustes actuales incorporados. La actualización física sigue siendo manual. La copia integrada es el original de fábrica, no uno de los paquetes históricos que contienen configuraciones del propietario. Véase [THIRD-PARTY.md](../THIRD-PARTY.md).

## Compatibilidad

Solo se estudió el Redragon K585, placa **8388-A-Rev:1(16A), VS11K16A**, USB `0C45:5004`, revisión `0x0108`, colección vendor `FF1C:0092` con paquetes de 64 bytes. La identificación USB no demuestra el modelo físico de la placa; el propietario debe confirmarlo antes de abrir el paquete preparado.

Se reconoce exclusivamente el ejecutable de 2.870.272 bytes cuya estructura coincide con el Update_Firmware.exe de 20200409 estudiado. El recurso de firmware tiene 65.536 bytes y comienza en el desplazamiento 2.686.512. Se comprueba SHA-256 del ejecutable con el recurso puesto a cero y del firmware con su región de configuración puesta a cero. Cambiar el código del actualizador, usar otro formato o un firmware no reconocido impide la preparación y ejecución.

Los códigos reconocidos desde v0.3.3 son original, Serpenteo previamente probado, el candidato anterior de arcoíris y la selección optimizada. Que una versión esté reconocida identifica sus bytes; no demuestra compatibilidad con todos los K585 ni garantiza recuperación.

## Flujo

1. Abrir la pestaña de firmware. Se carga y verifica el EXE integrado en memoria. **Usar integrado** restaura esa fuente; **Elegir otro EXE…** permite la importación opcional. No se ejecuta la herramienta.
2. Desde v0.3.3, activar opcionalmente **Preparar lluvia seleccionada · preset fijo (experimental)**. Se reconstruye el candidato optimizado exacto mediante diferencias de código incorporadas a Studio. Conserva la paleta nativa; no incluye configuraciones de otro teclado. Desactivado por defecto. La versión v0.3.2 conserva su opción anterior de arcoíris.
3. Preparar y respaldar. Studio vuelve a abrir la colección USB correcta, exige dos lecturas coincidentes, guarda el respaldo y copia todos los ajustes actuales al recurso del actualizador: general, los cuatro perfiles, bindeos, macros y RGB. Conserva los borradores locales sin enviarlos.
4. Abrir actualizador. Se muestran firmware y compatibilidad para confirmación. Se vuelve a leer y respaldar el teclado; si cambió desde la preparación, se exige preparar de nuevo. También se verifica el archivo y los bloques del respaldo. Se cierra el handle HID de Studio y se abre el EXE privado preparado.
5. La herramienta original controla la actualización. El usuario pulsa Start update y observa su resultado. Studio permanece ocupado y no permite operaciones USB ni termina forzosamente el proceso durante la espera.
6. Cerrar la herramienta y pulsar Verificar ajustes. Studio compara todos los datos con el respaldo, sin restaurar ni reintentar automáticamente. Esto no es una lectura del código ni confirma la animación física. Un cierre de ventana o exit code no se consideran prueba de instalación correcta.

El paquete y los cinco bloques privados se guardan en una carpeta nueva por preparación, bajo `%LOCALAPPDATA%\K585Studio\Firmware`, con manifiesto y hashes. `pending.json` permite continuar la verificación tras reiniciar Studio. No se escribe ni se comparte la configuración privada en GitHub.

Si el resultado no puede verificarse, los cambios de configuración y perfil permanecen deshabilitados. **Cerrar seguimiento** permite abandonar esa comparación explícitamente, conservando los archivos y exigiendo volver a leer antes de editar. No declara éxito ni cambia el teclado. El gestor no actualiza automáticamente la casilla «Firmware de lluvia instalado».

## Ajustes incompatibles

El firmware original no contiene modo 23. Serpenteo y el candidato arcoíris no contienen modo 20 y su modo 23 está restringido al perfil 2. Si los ajustes guardados contradicen esas capacidades, la preparación se detiene y pide cambiar primero el efecto correspondiente. No sustituye silenciosamente los efectos ni restablece perfiles.

La variante arcoíris mantiene la configuración leída: instalarla no activa automáticamente modo 23 ni Colorful. Tras comprobar la actualización y los ajustes, el propietario puede seleccionar perfil 2, efecto 23 y Colorful. En esa variante, Colorful sustituye el color constante por gota por un arcoíris en la estela; el RGB fijo sigue disponible.

## Selección optimizada de v0.3.3

La elección fija del simulador está integrada en la preparación mediante `Firmware.WithSelectedRain`. Reconstruye el código normalizado `fcf256c124f1125a7e3a8838c46a5aa96e58650252e9ece7ed7547f7e55f470b` desde original, Serpenteo o arcoíris anterior. Los planes de `SelectedRainPatch.json` solo contienen diferencias inferiores a `0x5400`; la configuración leída se incorpora después. Las tres conversiones se verificaron, junto con la preservación de perfiles y las restricciones de efectos.

La selección ocupa 930 bytes con sus adaptadores, más diez del despacho. Su entrada es de 1998 ms nominales en velocidad mínima y no cambia entre los seis ritmos individuales de caída. La caída media es de 1845 ms, con valores de 1620 a 2070 ms; el ciclo de color es de 4500 ms, con desfase siete. Hay cinco gotas máximas, nacimientos repartidos, catorce rutas compactas y finales alternos Ctrl/Fn desde G2. Utiliza 56 bytes de estado y conserva el efecto 1.

Para reproducir ese preset después de una instalación autorizada, seleccionar perfil 2, modo 23, Colorful y velocidad mínima, valor guardado 4. Preparar el paquete no impone esos ajustes: conserva exactamente los leídos y no activa efectos automáticamente. El slider nativo todavía escala caída, entrada y pausas conjuntamente. El motor con todos los parámetros ajustables queda pendiente.

La vista previa se comparó con 24.000 cuadros del candidato ARM en las diez combinaciones de color y brillo. La comprobación nativa adicional incluye 1023 casos restantes de perfil/modo, pruebas de entrada por fila y ritmo, eventos de teclado, temporizador y una secuencia de 30 minutos nominales. No es una prueba física. Fuentes, límites y resumen de evidencia en [selected-rain](../firmware/selected-rain/README.md).

Instalar o abrir Studio no instala el firmware por sí solo. La prueba física sigue pendiente y utiliza el mismo flujo explícito de respaldo, revisión, apertura manual y comparación posterior.

## Candidato anterior de arcoíris

El núcleo y adaptadores ocupan 910 bytes; el área principal de 452 bytes está llena. El estado temporal usa 56 bytes. Reutiliza la paleta original de 192 colores y conserva el efecto 1. Utiliza trece recorridos compactos, hasta cinco gotas y un ciclo de color fijo de diez segundos nominales, independiente de los cinco niveles de velocidad. A velocidad mínima, el recorrido nominal es de 990 ms y el encendido suave de 1206 ms. No implementa todos los controles del simulador.

La investigación fuera de este repositorio comprobó 20.000 cuadros RGB y 1138 condiciones en emulación nativa. El candidato conserva la configuración, los vectores y los datos de arranque de su base en memoria. La modificación incorporada reconstruye el mismo código normalizado (`42c23b381019a6d0ce5c182e502a6625391c283d280fdbc5bb40b0df9c9938b0`) tanto desde el EXE original como desde Serpenteo. Las diferencias tienen únicamente desplazamientos inferiores a `0x5400`; nunca llevan perfiles, macros ni RGB personales.

Esta variante **todavía no está probada físicamente**. El contador de color pierde el tiempo transcurrido si no se muestrea durante más de 2,56 segundos nominales. No se midieron la cadencia real, la suavidad bajo carga USB ni todos los posibles anidamientos de interrupciones.

## Límites de la actualización

La herramienta OEM realiza una actualización completa de la aplicación, 30.720 bytes; no escribe únicamente el hueco del efecto. Puede borrar más memoria de la que cambia el código. El respaldo de ajustes no equivale a un respaldo del cargador, y el archivo de firmware anterior no garantiza recuperación por USB si el cargador deja de responder. No hay una vía de recuperación demostrada para un fallo de programación de esta placa.

Las pruebas de Studio no ejecutan el actualizador ni escriben firmware. Desde v0.3.2 las pruebas normales también comprueban la identidad exacta del recurso, su independencia entre lecturas, la reconstrucción de arcoíris, la preparación con ajustes ficticios, el respaldo y la verificación antes de ejecutar. Se ejecutan en CI sin archivos del escritorio ni dispositivo conectado. Probar otro archivo del propietario sigue siendo opcional:

```powershell
dotnet run --project tests/K585Studio.Checks -c Release -- --firmware-updater "C:\ruta\Update_Firmware.exe"
```

Comprueban huellas, reconstrucción del candidato, límites de escritura, incorporación exacta de ajustes, respaldo, reanudación del seguimiento, cambios externos, archivos alterados, revisión incorrecta y cancelación. El recurso original forma parte del producto; los paquetes del propietario, datos privados y lecturas reales del teclado no son fixtures ni se descargan en CI.
