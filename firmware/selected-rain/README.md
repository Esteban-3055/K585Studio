# Lluvia seleccionada: candidato optimizado

Este directorio contiene fuentes para un candidato de análisis, todavía sin prueba física. No es una imagen completa de firmware ni contiene el actualizador. La preparación del preset está integrada en Studio v0.3.3 mediante `WithSelectedRain`; incluirlo en la aplicación no instala el firmware automáticamente.

## Resultado

La animación y sus adaptadores ocupan **930 bytes**, más los diez bytes del despacho nativo. El primer prototipo equivalente necesitaba 1044 bytes con adaptadores: se ahorraron 114 bytes. La imagen enlazada ocupa 940 de los 971 bytes de los rangos ya estudiados y deja **31 bytes separados** entre varios huecos. No se utilizó memoria nueva sin auditar ni se quitó otro efecto.

La comparación incluye la inicialización: se combinó la inicialización del estado con el adaptador, por lo que comparar solamente 952 frente a 822 bytes de núcleo exageraría el ahorro. El efecto 1 y su paleta permanecen intactos; el modo 20 ya estaba retirado en Serpenteo, la base de este estudio.

| Rango | Utilizado | Capacidad |
|---|---:|---:|
| Cola del renderizado, `0x394C` | 132 | 140 |
| Adaptador de temporizador, `0x39D8` | 32 | 32 |
| Recorridos, máscaras y generador aleatorio, `0x3B12` | 94 | 98 |
| Inicialización, `0x3BF8` | 48 | 50 |
| Despacho, `0x3F50` | 10 | 10 |
| Inicio del renderizado, `0x4C6C` | 188 | 189 |
| Motor y curva, `0x523C` | 408 | 424 |
| Adaptador de llamada, `0x53E4` | 28 | 28 |

## Comportamiento

Con la velocidad nativa mínima, valor guardado 4, el paso nominal es 18 ms:

- Entrada suave de 111 pasos, **1998 ms**, independiente del ritmo individual de la gota.
- Seis ritmos de recorrido: 1620, 1710, 1800, 1890, 1980 y 2070 ms. La media de sus valores es **1845 ms**, cercana a los 1850 ms elegidos, con aproximadamente ±12% de variación. No son tiempos medidos en el teclado.
- Colorful usa la paleta original de 192 colores, ciclo nominal **4500 ms** y desfase de siete posiciones entre teclas. RGB fijo sigue disponible y toma el color actual.
- Cinco gotas máximas, nacimientos escalonados con variación cercana a ±20% y reintentos espaciados si las rutas están ocupadas.
- Catorce rutas compactas cubren 42 teclas, incluidas G1–G5. Desde G2 alternan finales Ctrl y Fn; se mantienen los finales variables de los demás inicios. No es el generador geométrico completo del navegador.
- Estado de 56 bytes, sin aumentarlo respecto del candidato arcoíris anterior. El ritmo ocupa el byte de relleno existente en cada gota.

Las rutas se guardan con cuatro bits por fila. El redondeo RGB de la implementación nueva puede diferir en una unidad del cálculo anterior. La asignación de desfases de color recorre doce valores sin repetir entre las gotas activas de las secuencias comprobadas. Los destinos y los ritmos siguen usando el generador seudoaleatorio.

La entrada es independiente de los ritmos de las gotas, pero todavía cambia si se modifica el slider nativo de cinco velocidades. Este candidato contiene un preset fijo; no implementa todos los sliders del simulador.

El modo 23 sigue restringido al perfil 2. Este trabajo no amplía su compatibilidad a los demás perfiles.

## Fuentes y compilación

`selected.c` expresa el algoritmo y permite estudiar cambios. `selected.s` contiene la versión medida del compilador, con el renderizado dividido en dos secciones para encajarlo. Los saltos entre secciones usan BL; la función guarda LR en su entrada y no lo utiliza como variable. `adapters.s` y `selected.ld` completan el enlace en los rangos existentes.

El ensamblador es la fuente del candidato exacto. Recompilar solamente el C no reproduce su distribución. Si cambia el C, hay que regenerar y revisar el ensamblador, recolocar los bloques y repetir la emulación. No basta con aceptar una nueva huella.

Para construir únicamente un ELF de análisis con Zig 0.13.0:

```powershell
python firmware/selected-rain/build.py --zig C:/ruta/zig.exe --output artifacts/selected-rain-analysis
```

El script verifica direcciones, tamaños y SHA-256 de cada sección. No lee el teclado, no genera ROM ni EXE de actualización y no programa memoria. El ELF depende de funciones y datos de la ROM original en sus direcciones estudiadas; no se puede instalar como firmware independiente.

## Configuración desde Studio

Todavía no está implementada. El primer motor con parámetros leídos de RAM mide 970 bytes aislados, antes de validar el bloque, identificar capacidades, almacenarlo y añadir el transporte USB. Necesita otra optimización y una colocación verificada. Es una medición exploratoria, no una demostración de que sea imposible ni una versión configurable ya operativa.

El modelo actual de iluminación de Studio transmite ocho bytes: modo, brillo, velocidad, dirección, Colorful y RGB. No se usaron bytes desconocidos del perfil ni se añadieron sliders que aparenten controlar parámetros que el firmware instalado no entiende.

## Verificación y límites

La comprobación del candidato ejecutó 24.000 cuadros RGB nativos en diez combinaciones de brillo y color, los 1023 casos restantes de perfil/modo, los 65535 estados no nulos del generador aleatorio, eventos de pulsación y el temporizador original en sus cinco velocidades. Se comprobó la entrada de 111 pasos en cada fila y ritmo. Las escrituras quedaron acotadas, la ROM fue de solo lectura y se conservaron R4–R11 y la pila.

La compilación de este directorio coincide byte a byte con los ocho bloques del candidato emulado. Los resultados resumidos se guardan en `validation.json`. La comparación con la base conserva todos los bytes desde `0x5400`, los vectores y las instrucciones y tablas del efecto 1; no es una lectura actual del dispositivo.

Una secuencia adicional de 100.000 cuadros, equivalente a 30 minutos nominales con una segunda semilla, también coincidió con la referencia RGB. Mantuvo cuatro o cinco gotas activas en 88.824 cuadros de esa secuencia. Tras el arranque, no hubo cuadros con todos los canales por debajo de 26/255, el umbral usado para detectar oscuridad visible. Esto describe esa secuencia emulada, no todos los ajustes ni el brillo físico del teclado.

No hay prueba física de este candidato, medición del reloj del teclado ni garantía de recuperación. No se ha preparado ni lanzado un actualizador para el teclado conectado durante su desarrollo. La integración en el gestor usa diferencias de código verificadas y está desactivada por defecto; una instalación física es un paso separado.
