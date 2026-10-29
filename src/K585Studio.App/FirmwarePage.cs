using System.Diagnostics;
using K585Studio.Core;

namespace K585Studio.App;

internal sealed partial class MainForm
{
    internal bool FirmwareTab { get; private set; }
    internal string FirmwareStatus { get; private set; }="El actualizador viene integrado. Se verifica en memoria al abrir esta pestaña; no se instala firmware automáticamente.";
    internal FirmwareInspection? LoadedFirmware { get; private set; }
    internal FirmwarePrepared? PreparedFirmware { get; private set; }
    internal bool FirmwareNeedsVerification { get; private set; }
    internal bool HasFirmwareSource => firmwareSource!=null;
    internal bool UseRainbowFirmware { get; private set; }
    private static string FirmwareRoot=>Path.Combine(Storage.Root,"Firmware");
    private byte[]? firmwareSource;
    private ushort deviceRevision;

    private async Task LoadBundledFirmware(bool replace=false)
    {
        if(FirmwareNeedsVerification||!replace&&firmwareSource!=null)return;
        await Run(async()=>
        {
            PreparedFirmware=null;LoadedFirmware=null;firmwareSource=null;UseRainbowFirmware=false;
            FirmwareStatus="Verificando el actualizador integrado…";
            var bytes=await Task.Run(Firmware.ReadBundledUpdater);
            firmwareSource=bytes;LoadedFirmware=Firmware.Inspect(bytes);
            FirmwareStatus="Actualizador integrado verificado. Preparar y respaldar incorpora los ajustes actuales del teclado antes de generar el EXE de actualización.";
        });
    }

    private async Task LoadFirmware()
    {
        if(FirmwareNeedsVerification)return;
        using var dialog=new OpenFileDialog{Filter="Actualizador K585|*.exe",Title="Selecciona el actualizador de firmware del K585"};
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        // Reading and hashing is bounded to the audited 2.9 MB executable.
        await Run(async()=>
        {
            PreparedFirmware=null;LoadedFirmware=null;firmwareSource=null;UseRainbowFirmware=false;
            var bytes=await Firmware.ReadUpdaterAsync(dialog.FileName,lifetime.Token);
            var inspection=await Task.Run(()=>Firmware.Inspect(bytes));
            firmwareSource=bytes;LoadedFirmware=inspection;
            FirmwareStatus=inspection.Identity.Experimental
                ? "Candidato experimental: comprobado en emulación, pendiente de prueba física. Prepáralo para revisar el respaldo."
                : "Actualizador reconocido. Prepara un paquete con la configuración actual del teclado.";
        });
    }
    private async Task PrepareFirmware()
    {
        if(firmwareSource==null||FirmwareNeedsVerification)return;
        await Run(async()=>
        {
            PreparedFirmware=null;FirmwareStatus="Leyendo y respaldando los cuatro perfiles, bindeos y macros…";
            await RefreshKeyboard();
            Firmware.ValidateTarget(deviceRevision);
            byte[] source=UseRainbowFirmware?await Task.Run(()=>Firmware.WithRainbow(firmwareSource)):firmwareSource;
            PreparedFirmware=await Firmware.PrepareAsync(source,Snapshot!,deviceRevision,FirmwareRoot,lifetime.Token);
            FirmwareStatus="Paquete verificado con los ajustes leídos del teclado. Los borradores no se aplican. Tú inicias la grabación con Start update en la herramienta.";
            SetStatus("Respaldo y paquete preparados. No se ha escrito firmware.");
        });
    }
    private async Task OpenFirmwareUpdater()
    {
        if(PreparedFirmware is not {} prepared||FirmwareNeedsVerification)return;
        // This dialog is deliberately AFTER a concrete, inspected package and backup exist.
        string experimental=prepared.Inspection.Identity.Experimental?"\n\nEsta variante solo está probada en emulación.":"";
        if(MessageBox.Show(this,$"Firmware: {prepared.Inspection.Identity.Name}\n\nSolo para placa 8388-A-Rev:1(16A), chip VS11K16A y revisión USB 0x0108. Confirma que tu teclado corresponde a esa placa.\n\nSe liberará USB y se abrirá el actualizador. La grabación comienza cuando pulses Start update en su ventana. No desconectes el teclado durante la actualización. No hay una recuperación garantizada si falla.{experimental}\n\n¿Abrir el actualizador preparado?","Actualizar firmware",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
        await Run(async()=>
        {
            FirmwareStatus="Releyendo la configuración y verificando el paquete antes de abrir…";
            NativeHid.RequireOtherAppsClosed();await RefreshKeyboard();Firmware.ValidateTarget(deviceRevision);
            await Firmware.VerifyPreparedAsync(prepared,Snapshot!,lifetime.Token);
            session!.Dispose();session=null;Snapshot=null;DeviceMacros=[];
            Firmware.SavePending(prepared,FirmwareRoot);FirmwareNeedsVerification=true;
            var start=new ProcessStartInfo(prepared.Path){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(prepared.Path)!,WindowStyle=ProcessWindowStyle.Normal};
            using var process=Process.Start(start)??throw new IOException("No se pudo abrir el actualizador.");
            FirmwareStatus="Actualizador abierto. No cierres ni desconectes durante la grabación. Studio mantiene USB libre.";canvas.Invalidate();
            // Do not time out or kill a process that may be programming the keyboard.
            await process.WaitForExitAsync();
            FirmwareStatus="El actualizador se cerró. Su cierre no confirma el resultado: revisa que indicó update successful al 100 % y verifica la configuración.";
            SetStatus("Pendiente de verificar la configuración después del actualizador.");
        });
    }
    private async Task VerifyFirmwareConfiguration()
    {
        if(!FirmwareNeedsVerification||PreparedFirmware is not {} prepared)return;
        await Run(async()=>
        {
            FirmwareStatus="Leyendo la configuración después de cerrar el actualizador…";await RefreshKeyboard();
            if(!prepared.Before.SameData(Snapshot!))throw new InvalidDataException("La configuración leída difiere del respaldo. No se restaurará ni se reintentará automáticamente. Revisa el respaldo antes de seguir.");
            Firmware.ClearPending(FirmwareRoot);
            FirmwareNeedsVerification=false;
            FirmwareStatus="Los cuatro perfiles, bindeos, macros y RGB coinciden con el respaldo. Esto no verifica el código instalado; comprueba el resultado y la iluminación físicamente.";
            SetStatus("Configuración comparada y conservada. Comprueba las teclas y la iluminación.");
        });
    }
    private void StopFirmwareTracking()
    {
        if(!FirmwareNeedsVerification)return;
        if(MessageBox.Show(this,"Esto cierra el seguimiento sin confirmar la instalación ni comparar la configuración. Los respaldos se conservan. No se ejecutará ninguna restauración.\n\nAntes de editar tendrás que volver a leer el teclado. Cierra primero el actualizador si sigue abierto.\n\n¿Cerrar el seguimiento pendiente?","Seguimiento de firmware",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
        NativeHid.RequireOtherAppsClosed();Firmware.ClearPending(FirmwareRoot);
        FirmwareNeedsVerification=false;PreparedFirmware=null;LoadedFirmware=null;firmwareSource=null;UseRainbowFirmware=false;
        session?.Dispose();session=null;Snapshot=null;DeviceMacros=[];
        FirmwareStatus="Seguimiento cerrado sin verificar la actualización. Respaldos conservados. Vuelve a leer el teclado antes de editar.";SetStatus(FirmwareStatus);
    }
}
