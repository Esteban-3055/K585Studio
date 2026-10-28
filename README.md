# K585 Studio · v0.1

Aplicación de Windows para Redragon K585 RGB (0C45:5004). Interfaz oscura inspirada en VictusFanControl, cuatro perfiles, imagen interactiva del teclado y editor lateral.

## Incluido

- Lectura doble del dispositivo y respaldos privados completos de su configuración.
- **Perfil 1 protegido**: no se modifica ni desde proyectos importados.
- Iluminación del **perfil 2**, incluidos brillo, velocidad, color fijo y Colorful de nuestra lluvia Serpenteo (modo 23).
- Asignaciones locales de teclado, ratón, multimedia, funciones y macros. Los bordes naranjas indican borradores pendientes, todavía **no enviados al teclado**.
- Editor de macros local, pausas, repeticiones, orden de pasos y captura solo dentro de la zona de grabación.
- Importación/exportación de proyectos JSON propios.
- Consulta de versiones estables de GitHub Releases; instalación manual en esta primera versión.

Hz, bindeos y macros del dispositivo permanecen en solo lectura hasta completar su validación. La frecuencia USB se muestra como opción global pendiente de habilitar. Dirección y pintura por tecla no están habilitadas. El modo 20 no está disponible en el firmware personalizado actual. Esta versión no actualiza firmware.

## Compilar y ejecutar

Windows 10/11, SDK .NET 8:

```powershell
dotnet build K585Studio.sln -c Release
dotnet run --project tests/K585Studio.Checks -c Release --no-build
dotnet run --project src/K585Studio.App -c Release --no-build
```

Portable sin instalar .NET:

```powershell
dotnet publish src/K585Studio.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/K585Studio-win-x64
```

Cierra por completo el programa original antes de conectar. La aplicación no cambia el perfil activo: para aplicar iluminación activa M2 físicamente y pulsa **Conectar / leer**. Mover controles solo prepara el ajuste; **Aplicar iluminación** realiza una escritura respaldada y verificada. La velocidad dispone de cinco niveles reales; el extremo izquierdo es el más lento.

Proyectos, respaldos e informes se guardan en `%LOCALAPPDATA%\K585Studio`, fuera del repositorio. Los respaldos pueden contener macros personales; no los publiques.

Consulta el [inventario de funciones original](docs/OEM-AUDIT.md) y el [procedimiento de validación](docs/VALIDATION.md) para distinguir lo comprobado físicamente, lo descodificado y lo pendiente. Studio es un proyecto independiente, sin afiliación con Redragon; el repositorio no redistribuye el ejecutable ni firmware del fabricante.
Aplicación para controlar el Redragon K585 RGB con perfiles protegidos.
