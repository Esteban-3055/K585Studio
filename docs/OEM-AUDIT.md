# Inventario del programa original y alcance de v0.1

Revisión: 2026-10-27. Este inventario distingue recursos de interfaz, protocolo descodificado y prueba física. No implica haber ejecutado todos los comandos del original sobre el teclado. No se usó «Restore Default» para probarlo.

## Identificación de la fuente instalada

| Archivo | SHA-256 |
|---|---|
| Redragon K585RGB Keyboard.exe | FE580905B4705B335C12E020AF311FC4257CDD5486799DFECEA80CF036957AEC |
| HidServ.dll | EFBAFE30A73B55AC3E3D9C1AD06BF92368760194F34E744B3F54698E39B48038 |
| Skins/Main.ini | 63395F58619A08CB4E5BE90245E68D0B0DC5F81511BC65AA941E847F675FF0E3 |
| Skins/Composite.ini | A98AFE74B3D0EBD6104E72E2AC3F4C4127A796A73F9CD4D32F2C807C0CFA243A |

El ejecutable declara 1.0.0.0; el texto About del idioma inglés dice Driver v1.0.6. Son identificadores distintos. Se revisaron Main.ini, Composite.ini, idiomas, llamadas de iluminación del ejecutable desensamblado y el transporte ya identificado. `peizhi.ini` contiene datos binarios; no se interpreta como texto ni se importa a Studio.

## Matriz funcional

| Función original | Evidencia / particularidad | K585 Studio v0.1 |
|---|---|---|
| Cuatro perfiles | FUNC_PROFILE1–4; bloques por perfil | Navegación y lectura; perfil 1 bloqueado |
| Selección de perfil activo | General[10], índice desde cero | Se muestra; selección física M1–M4, sin escritura global |
| Teclas individuales y combinaciones | Composite: entrada de teclado y selector de ratón | Editor local, sin escritura de bindeos |
| Ratón | Izquierdo, derecho, central, doble clic, rueda ±, desplazamiento lateral | Catálogo de asignación local |
| Multimedia | Pistas, pausa, detener, volumen, silenciar, calculadora, navegador, correo, equipo, reproductor, alimentación | Catálogo de asignación local |
| Funciones de oficina | Cadenas Open/New/Undo/Save/Copy/Cut/Paste/Find/SelectAll/Zoom/ventanas/Office | Catálogo local; CONTROL_FUNCTION está comentado en la skin, no se afirma que todas estén accesibles en el original |
| G1–G5 | FUNC_G1–5 en Main.ini | Selección visual y borradores |
| Tecla T y tecla ~ | FUNC_G6 y FUNC_G7 en la skin original | Alias visibles para evitar confundir imagen y nombres OEM |
| Tecla con dragón | KEY_032 corresponde a M en la skin | Alias «M · dragón» |
| Fn | KEY_FN | Selección local; comportamiento físico reservado no reprogramado |
| M1–M4 y REC de la carcasa | Sin áreas de reasignación en Main.ini | No se ofrecen como teclas reprogramables |
| Macros nuevas / nombres | Nuevo, eliminar, validación de nombres duplicados | Biblioteca local con nombres únicos, renombrado y eliminación |
| Grabación de macro | Inicio/parada, teclado/ratón, record delay | Grabación solo en zona enfocada; sin captura global |
| Edición de pasos | Acción, tecla, pausa; borrar/vaciar; subir/bajar/inicio/final | Editor local equivalente de pasos |
| Ciclos de macro | Campo Cycles | Repeticiones locales 1–999; no se atribuye este límite al hardware |
| Capacidad de macros | El original advierte espacio agotado / nombres omitidos | No se convierte ni descarga al hardware; límites locales explícitos |
| Efectos originales seleccionables | Lista comprobada del ejecutable: 1,4,7,8,9,18,20 | Lista 1,4,7,8,9,18 y nuevo 23; 20 retirado del firmware actual |
| Otros nombres de efectos en idiomas | Nombres 2–23 no prueban implementación | No se habilitan solo por aparecer en el idioma |
| Brillo y velocidad | Campos 0–4; velocidad 4 = más lenta | Cinco posiciones reales, edición separada de Aplicar |
| Color fijo / Colorful | RGB y bandera en cabecera de iluminación | Habilitados para lluvia; otros efectos conservan los parámetros no verificados |
| Dirección | Controles BLFX en skin y byte 3 de iluminación | Se conserva el byte; edición pendiente de validar por efecto |
| Pintura por tecla / Coastal 20 | Herramientas y paleta de color en Main.ini; tabla RGB persistente | Deshabilitado: modo 20 fue sustituido para alojar el efecto personalizado |
| Lluvia 23 / Serpenteo | Firmware personalizado, comprobado físicamente por el propietario antes de Studio | Color, Colorful, cinco niveles de brillo/velocidad; rutas/colas/fundido no ajustables con este firmware |
| USB 125/250/500/1000 Hz | FUNC_REPORT1–4; opción del dispositivo, no cuatro tasas independientes demostradas | Visible en cada perfil, deshabilitado hasta validar campo y escritura global |
| Importar / exportar configuración | Menús Restore / Backups del original | Formato JSON propio de proyectos locales; no compatible con binarios OEM ni restauración del dispositivo |
| Restaurar valores predeterminados | Botón Default; fallo de bindeos observado anteriormente por el usuario | Sin comando ni botón de restauración física |
| OK, Cancel, Apply / bandeja | Controles y cadenas de menú | Aplicar iluminación explícito; cerrar termina la aplicación y libera HID, sin bandeja oculta |
| About / minimizar / salir | Recursos del original | Versión en ventana y página Actualizaciones |
| Actualización desde GitHub | No es función original | Consulta Releases estables; apertura de versión publicada e instalación manual en v0.1 |

## Contrato USB y protecciones

Interfaz: VID 0C45, PID 5004, usage page FF1C, usage 0092, reportes de entrada/salida de 64 bytes. Se enumeran las colecciones al conectar; no se fija la ruta de una máquina. No se abre la colección de escritura de teclas estándar.

Reporte: ID 04; suma de bytes 3–63 en 1–2 little endian; comando en 3, longitud en 4, desplazamiento little endian en 5–6, estado en 7, carga en 8–63. La respuesta conserva la suma de la **petición**. Por eso se compara la cabecera y se hacen dos lecturas independientes; no se inventa una suma de la respuesta.

| Región | Lectura | Longitud | Escritura OEM conocida | Uso en v0.1 |
|---|---:|---:|---:|---|
| General | 3 | 128 | 4 | Solo leer |
| Iluminación | 5 | 256 | 6 | Solo desplazamiento 64, longitud 8: perfil 2 |
| Bindeos | 7 | 768 | 8 | Solo leer |
| Macros | 9 | 6144 | 10 | Solo leer |
| RGB por tecla | 16 | 768 | 17 | Solo leer |

El lector no usa comandos de cargador, actualización, borrado ni reset. La sesión exige dos lecturas iguales y un respaldo guardado. Antes de escribir vuelve a leer y compara con su estado anterior; si otro programa cambió algo, rechaza la escritura. Después vuelve a leer dos veces y exige que la única diferencia sean los ocho bytes previstos del perfil 2. Ante timeout o discrepancia bloquea más escrituras en esa sesión, sin reintentos ni restauraciones automáticas. Esta verificación detecta cambios; no convierte una interfaz USB compartida en exclusión absoluta frente al programa OEM.

## Qué queda pendiente de verificación

- Correspondencia entre cada tecla física y cada registro de tres bytes de bindeos; codificación completa de combinaciones y macros, índices y límites de hardware.
- Campo exacto de Hz, persistencia, efecto sobre enumeración y si intervienen más bytes globales.
- Matriz de controles y semántica de dirección para cada efecto original.
- Formatos OEM de importación/exportación. No reutilizar el reseteo para inferirlos.
- Identificación automática fiable del firmware modificado: VID/PID/REV no distinguen el contenido de la ROM. El usuario confirma el firmware de lluvia antes de escribir modo 23.
- Instalación automática de futuras versiones. El comprobador v0.1 abre únicamente páginas HTTPS de Releases del repositorio configurado; no descarga/ejecuta binarios.

Los binarios del fabricante, firmware, bindeos y macros personales no se incluyen en el repositorio público. La imagen se incorpora tal como fue proporcionada por el propietario; no se afirma licencia de marca o autoría propia sobre ella.
