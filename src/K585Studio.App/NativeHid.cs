using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using K585Studio.Core;

namespace K585Studio.App;

internal sealed class NativeHid : ITransport
{
    private readonly FileStream stream;
    private bool failed;
    private NativeHid(SafeFileHandle handle) => stream = new FileStream(handle, FileAccess.ReadWrite, 1, true);

    public static void RequireOtherAppsClosed()
    {
        foreach (var process in Process.GetProcesses())
        using (process)
        {
            string name;
            try { name = process.ProcessName; } catch (InvalidOperationException) { continue; }
            if (name.StartsWith("Redragon K585", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("K585-", StringComparison.OrdinalIgnoreCase) || name.Equals("Update_Firmware", StringComparison.OrdinalIgnoreCase))
                throw new IOException($"Cierra por completo «{name}», incluido su icono junto al reloj, antes de conectar.");
        }
    }

    public static NativeHid Open()
    {
        RequireOtherAppsClosed();
        HidD_GetHidGuid(out var guid);
        var set = SetupDiGetClassDevs(ref guid, null, IntPtr.Zero, 0x12);
        if (set == new IntPtr(-1)) throw new Win32Exception();
        var candidates = new List<string>();
        try
        {
            for (uint index = 0; ; index++)
            {
                var info = new InterfaceData { Size = Marshal.SizeOf<InterfaceData>() };
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, index, ref info))
                {
                    if (Marshal.GetLastWin32Error() == 259) break;
                    throw new Win32Exception();
                }
                SetupDiGetDeviceInterfaceDetail(set, ref info, IntPtr.Zero, 0, out var length, IntPtr.Zero);
                var detail = Marshal.AllocHGlobal(checked((int)length));
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(set, ref info, detail, length, out _, IntPtr.Zero)) throw new Win32Exception();
                    var path = Marshal.PtrToStringUni(detail + 4)!;
                    if (!path.Contains("vid_0c45&pid_5004", StringComparison.OrdinalIgnoreCase)) continue;
                    using var handle = CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                    if (handle.IsInvalid) continue;
                    var attr = new Attributes { Size = Marshal.SizeOf<Attributes>() };
                    if (!HidD_GetAttributes(handle, ref attr) || attr.Vendor != 0x0c45 || attr.Product != 0x5004) continue;
                    if (!HidD_GetPreparsedData(handle, out var pre)) continue;
                    try
                    {
                        if (HidP_GetCaps(pre, out var caps) == 0x110000 && caps.UsagePage == 0xff1c && caps.Usage == 0x92 &&
                            caps.InputLength == 64 && caps.OutputLength == 64) candidates.Add(path);
                    }
                    finally { HidD_FreePreparsedData(pre); }
                }
                finally { Marshal.FreeHGlobal(detail); }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        if (candidates.Count != 1) throw new IOException($"Se esperaba un K585 compatible; se encontraron {candidates.Count}. Conecta solo uno.");
        var device = CreateFile(candidates[0], 0xc0000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero);
        if (device.IsInvalid) { device.Dispose(); throw new Win32Exception(); }
        try { return new NativeHid(device); } catch { device.Dispose(); throw; }
    }

    public async Task<byte[]> ExchangeAsync(byte[] request, CancellationToken cancellationToken)
    {
        if (failed) throw new IOException("La conexión USB necesita volver a abrirse.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        var response = new byte[64];
        // Post the read first. FileStream owns the pinned overlapped buffers until cancellation is drained.
        var read = stream.ReadAsync(response.AsMemory(), timeout.Token).AsTask();
        try
        {
            await stream.WriteAsync(request.AsMemory(), timeout.Token);
            if (await read != 64) throw new IOException("Respuesta USB incompleta.");
            return response;
        }
        catch
        {
            failed = true;
            await timeout.CancelAsync();
            try { await read; } catch (Exception) { /* Drain the outstanding I/O before disposing its handle. */ }
            stream.Dispose();
            throw;
        }
    }
    public void Dispose() => stream.Dispose();

    [StructLayout(LayoutKind.Sequential)] private struct InterfaceData { public int Size; public Guid Guid; public int Flags; public IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)] private struct Attributes { public int Size; public ushort Vendor, Product, Version; }
    [StructLayout(LayoutKind.Sequential)] private struct Caps
    {
        public ushort Usage, UsagePage, InputLength, OutputLength, FeatureLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 27)] public ushort[] ReservedAndCounts;
    }
    [DllImport("hid.dll")] private static extern void HidD_GetHidGuid(out Guid guid);
    [DllImport("hid.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool HidD_GetAttributes(SafeFileHandle device, ref Attributes attributes);
    [DllImport("hid.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool HidD_GetPreparsedData(SafeFileHandle device, out IntPtr data);
    [DllImport("hid.dll")] private static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")] private static extern int HidP_GetCaps(IntPtr data, out Caps caps);
    [DllImport("setupapi.dll", EntryPoint = "SetupDiGetClassDevsW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr SetupDiGetClassDevs(ref Guid guid, string? enumerator, IntPtr window, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr device, ref Guid guid, uint index, ref InterfaceData data);
    [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDeviceInterfaceDetailW", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref InterfaceData info, IntPtr detail, uint size, out uint needed, IntPtr device);
    [DllImport("setupapi.dll")] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
}
