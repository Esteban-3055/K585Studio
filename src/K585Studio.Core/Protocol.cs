using System.Buffers.Binary;
using System.Security.Cryptography;

namespace K585Studio.Core;

public static class Protocol
{
    public static readonly IReadOnlyDictionary<byte, int> Regions = new Dictionary<byte, int>
    { [3] = 128, [5] = 256, [7] = 768, [9] = 6144, [16] = 768 };

    public static byte[] ReadRequest(byte opcode, int offset, int length)
    {
        if (!Regions.TryGetValue(opcode, out var size) || length is < 1 or > 56 || offset < 0 || offset > size - length)
            throw new ArgumentOutOfRangeException(nameof(offset), "Lectura fuera de las regiones auditadas.");
        return Packet(opcode, offset, length, []);
    }

    public static byte[] LightingRequest(int profile, Lighting lighting)
    {
        if (profile != 2) throw new InvalidOperationException("La v0.1 solo escribe iluminación del perfil 2.");
        lighting.Validate();
        return Packet(6, 64, 8, lighting.Bytes());
    }

    private static byte[] Packet(byte opcode, int offset, int length, byte[] data)
    {
        var packet = new byte[64];
        packet[0] = 4; packet[3] = opcode; packet[4] = (byte)length;
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(5), (ushort)offset);
        data.CopyTo(packet, 8);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(1), (ushort)packet.Skip(3).Sum(b => b));
        return packet;
    }

    public static byte[] Response(byte[] request, byte[] response)
    {
        if (request.Length != 64 || response.Length != 64 || response[0] != 4 ||
            !request.AsSpan(1, 6).SequenceEqual(response.AsSpan(1, 6)))
            throw new IOException("La respuesta USB no corresponde a la petición.");
        // This firmware echoes the REQUEST checksum, not a checksum of the returned payload.
        if (response[7] is 0xfe or 0xff || (request[3] == 6 && response[7] != 0))
            throw new IOException($"El teclado rechazó la petición ({response[7]:X2}).");
        return response.AsSpan(8, request[4]).ToArray();
    }
}

public sealed record Lighting(byte Mode, byte Brightness, byte Speed, byte Direction, bool Colorful, byte R, byte G, byte B)
{
    public static Lighting Default => new(23, 4, 4, 255, false, 34, 157, 255);
    public static Lighting Parse(ReadOnlySpan<byte> bytes) => new(bytes[0], bytes[1], bytes[2], bytes[3], bytes[4] != 0, bytes[5], bytes[6], bytes[7]);
    public byte[] Bytes() => [Mode, Brightness, Speed, Direction, (byte)(Colorful ? 1 : 0), R, G, B];
    public void Validate()
    {
        if (!Effects.All.Any(e => e.Id == Mode) || Mode == 20 || Brightness > 4 || Speed > 4 || Direction is not (0 or 1 or 255))
            throw new InvalidOperationException("Ajuste fuera de los valores admitidos.");
    }
}

public sealed record Effect(byte Id, string Name, string Description)
{
    public override string ToString() => $"{Id:00} · {Name}";
}

public static class Effects
{
    public static readonly Effect[] All =
    [
        new(1, "Go with the stream", "Efecto original 1. Los ajustes no confirmados se conservan al aplicar."),
        new(4, "The trial of light", "Efecto original 4."),
        new(7, "Pass without trace", "Efecto original 7."),
        new(8, "Ripple graff", "Efecto original 8."),
        new(9, "Fast run without trace", "Efecto reactivo 9, comprobado físicamente."),
        new(18, "Fast and the Furious", "Efecto original 18."),
        new(23, "Lluvia · Serpenteo", "Recorridos variables, encendido suave y G1–G5. Colorful conserva el color de cada gota. Requiere nuestro firmware de lluvia.")
    ];
}

public sealed record Snapshot(DateTimeOffset ReadAt, byte[] General, byte[] Illumination, byte[] Bindings, byte[] Macros, byte[] Rgb)
{
    public int ActiveProfile => General[10] < 4 ? General[10] + 1 : 0;
    public Lighting Profile(int profile) => Lighting.Parse(Illumination.AsSpan((profile - 1) * 64, 8));
    public string Digest => Convert.ToHexString(SHA256.HashData(General.Concat(Illumination).Concat(Bindings).Concat(Macros).Concat(Rgb).ToArray()));
    public bool SameData(Snapshot other) => General.AsSpan().SequenceEqual(other.General) &&
        Illumination.AsSpan().SequenceEqual(other.Illumination) && Bindings.AsSpan().SequenceEqual(other.Bindings) &&
        Macros.AsSpan().SequenceEqual(other.Macros) && Rgb.AsSpan().SequenceEqual(other.Rgb);
    public bool OnlyProfile2LightingChanged(Snapshot after, Lighting expected)
    {
        var lights = Illumination.ToArray(); expected.Bytes().CopyTo(lights, 64);
        return this with { Illumination = lights } is var wanted && wanted.SameData(after);
    }
}

public interface ITransport : IDisposable
{
    Task<byte[]> ExchangeAsync(byte[] request, CancellationToken cancellationToken);
}

public sealed class KeyboardSession(ITransport transport, Func<Snapshot, Task> backup) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public Snapshot? Current { get; private set; }
    public bool WriteFaulted { get; private set; }

    private async Task<Snapshot> ReadOnce(CancellationToken ct)
    {
        var data = new Dictionary<byte, byte[]>();
        foreach (var (op, size) in Protocol.Regions)
        {
            var bytes = new byte[size];
            for (int offset = 0; offset < size; offset += 56)
            {
                var request = Protocol.ReadRequest(op, offset, Math.Min(56, size - offset));
                var result = Protocol.Response(request, await transport.ExchangeAsync(request, ct));
                result.CopyTo(bytes, offset);
            }
            data.Add(op, bytes);
        }
        return new(DateTimeOffset.UtcNow, data[3], data[5], data[7], data[9], data[16]);
    }

    private async Task<Snapshot> StableRead(CancellationToken ct)
    {
        var first = await ReadOnce(ct); var second = await ReadOnce(ct);
        if (!first.SameData(second)) throw new IOException("El teclado cambió durante la lectura. Cierra el programa original y vuelve a leer.");
        return second;
    }

    public async Task<Snapshot> RefreshAsync(CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try { var snapshot = await StableRead(ct); await backup(snapshot); return Current = snapshot; }
        finally { gate.Release(); }
    }

    public async Task<Snapshot> ApplyLightingAsync(int profile, Lighting lighting, bool rainFirmwareConfirmed, CancellationToken ct = default)
    {
        var request = Protocol.LightingRequest(profile, lighting);
        if (lighting.Mode == 23 && !rainFirmwareConfirmed) throw new InvalidOperationException("Confirma que este teclado tiene instalado el firmware de lluvia.");
        await gate.WaitAsync(ct);
        try
        {
            if (WriteFaulted) throw new InvalidOperationException("Las escrituras están bloqueadas tras un error. Reconecta y revisa el respaldo.");
            var previous = Current ?? throw new InvalidOperationException("Primero lee el teclado.");
            var fresh = await StableRead(ct);
            if (!previous.SameData(fresh)) throw new IOException("La configuración cambió fuera de Studio. Vuelve a leerla antes de aplicar.");
            if (fresh.ActiveProfile != 2) throw new InvalidOperationException("Activa el perfil 2 con el botón físico M2 y vuelve a leer. Studio no cambia el perfil activo.");
            await backup(fresh);
            if (fresh.Profile(2) == lighting) return Current = fresh;
            // From this point, never retry automatically: even a timeout can follow a successful persistent write.
            WriteFaulted = true;
            Protocol.Response(request, await transport.ExchangeAsync(request, ct));
            var after = await StableRead(ct);
            await backup(after);
            if (!fresh.OnlyProfile2LightingChanged(after, lighting))
                throw new IOException("La verificación detectó cambios inesperados. No se harán más escrituras ni restauraciones automáticas.");
            WriteFaulted = false;
            return Current = after;
        }
        finally { gate.Release(); }
    }

    public void Dispose() { transport.Dispose(); gate.Dispose(); }
}
