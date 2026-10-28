using System.Buffers.Binary;
using K585Studio.Core;

namespace K585Studio.App;

// Only constructed by --ui-self-test. It has no native handles or device access.
internal sealed class UiCheckTransport(Snapshot snapshot) : ITransport
{
    private readonly Dictionary<byte,byte[]> blocks=new()
    {
        [3]=snapshot.General.ToArray(),[5]=snapshot.Illumination.ToArray(),[7]=snapshot.Bindings.ToArray(),
        [9]=snapshot.Macros.ToArray(),[16]=snapshot.Rgb.ToArray()
    };
    internal int Writes { get; private set; }
    internal bool TimeoutOnSelection { get; set; }
    public Task<byte[]> ExchangeAsync(byte[] request,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();var response=request.ToArray();int offset=BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(5)),count=request[4];
        if(request[3]==4)
        {
            if(offset!=10||count!=1||request[8]>3)throw new InvalidOperationException("UI check attempted a non-profile write.");
            Writes++;blocks[3][10]=request[8];
            if(TimeoutOnSelection)throw new TimeoutException("Simulated profile write timeout.");
        }
        else if(Protocol.Regions.ContainsKey(request[3]))blocks[request[3]].AsSpan(offset,count).CopyTo(response.AsSpan(8));
        else throw new InvalidOperationException("Selecting a profile must not apply pending settings.");
        return Task.FromResult(response);
    }
    public void Dispose() { }
}
