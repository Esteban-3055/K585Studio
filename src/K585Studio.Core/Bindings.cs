using System.Buffers.Binary;
using System.Text;

namespace K585Studio.Core;

public sealed record HardwareKey(string Name, int Slot, byte[] Factory, bool Reserved = false);
public sealed record KeyBinding(byte Kind, byte A, byte B)
{
    public byte[] Bytes() => [Kind, A, B];
    public static KeyBinding Parse(ReadOnlySpan<byte> data) => new(data[0], data[1], data[2]);
    public static KeyBinding Keyboard(byte usage, byte modifiers = 0) => modifiers == 0 ? new(2, 2, usage) : new(0xb0, modifiers, usage);
    public static KeyBinding Modifier(byte mask) => new(2, 1, mask);
    public static KeyBinding Macro(byte id) => new(0x70, id, 1);
    public void Validate()
    {
        bool valid = Kind switch
        {
            0 => A == 0 && B == 0,
            2 => (A == 2 && B is >= 4 and <= 0x73) || (A == 1 && B != 0),
            0xb0 => A != 0 && B is >= 4 and <= 0x73,
            1 => (A == 1 && B is 1 or 2 or 4) || (A is 2 or 5 && B is 1 or 255),
            3 => BindingCatalog.Media.Values.Any(v => v.A == A && v.B == B),
            6 => A == 0xa2 && B is >= 1 and <= 8,
            0x70 => B == 1 && A < 128,
            _ => false
        };
        if (!valid) throw new InvalidDataException("Esta asignación todavía no tiene una codificación admitida.");
    }
}

public static class BindingCatalog
{
    public static readonly HardwareKey[] Keys = BuildKeys();
    public static readonly IReadOnlyDictionary<string, byte> Keyboard = BuildKeyboard();
    public static readonly IReadOnlyDictionary<string, KeyBinding> Media = new Dictionary<string, KeyBinding>
    {
        ["Pista siguiente"] = Consumer(0xb5), ["Pista anterior"] = Consumer(0xb6), ["Detener"] = Consumer(0xb7),
        ["Reproducir / pausa"] = Consumer(0xcd), ["Silenciar"] = Consumer(0xe2), ["Volumen +"] = Consumer(0xe9), ["Volumen −"] = Consumer(0xea),
        ["Calculadora"] = Consumer(0x192), ["Correo"] = Consumer(0x18a), ["Reproductor multimedia"] = Consumer(0x183),
        ["Mi PC"] = Consumer(0x194), ["Web: inicio"] = Consumer(0x223), ["Web: buscar"] = Consumer(0x221),
        ["Web: favoritos"] = Consumer(0x22a), ["Web: actualizar"] = Consumer(0x227), ["Web: detener"] = Consumer(0x226),
        ["Web: adelante"] = Consumer(0x225), ["Web: atrás"] = Consumer(0x224)
    };
    public static readonly IReadOnlyDictionary<string, KeyBinding> Mouse = new Dictionary<string, KeyBinding>
    {
        ["Botón izquierdo"] = new(1,1,1), ["Botón derecho"] = new(1,1,2), ["Botón central"] = new(1,1,4),
        ["Rueda arriba"] = new(1,2,1), ["Rueda abajo"] = new(1,2,255),
        ["Desplazar derecha"] = new(1,5,1), ["Desplazar izquierda"] = new(1,5,255)
    };
    public static readonly IReadOnlyDictionary<string, KeyBinding> Functions = new Dictionary<string, KeyBinding>
    {
        ["Copiar"] = KeyBinding.Keyboard(6,1), ["Cortar"] = KeyBinding.Keyboard(27,1), ["Pegar"] = KeyBinding.Keyboard(25,1),
        ["Deshacer"] = KeyBinding.Keyboard(29,1), ["Guardar"] = KeyBinding.Keyboard(22,1), ["Seleccionar todo"] = KeyBinding.Keyboard(4,1),
        ["Buscar"] = KeyBinding.Keyboard(9,1), ["Nuevo"] = KeyBinding.Keyboard(17,1), ["Abrir"] = KeyBinding.Keyboard(18,1),
        ["Cerrar ventana"] = KeyBinding.Keyboard(61,4)
    };
    private static KeyBinding Consumer(int usage) => new(3, (byte)usage, (byte)(usage >> 8));
    public static HardwareKey Key(string name) => Keys.SingleOrDefault(k => k.Name == name) ?? throw new InvalidDataException("Tecla desconocida.");
    public static KeyBinding Read(Snapshot snapshot, int profile, string key) => KeyBinding.Parse(snapshot.Bindings.AsSpan((profile - 1) * 192 + Key(key).Slot * 3, 3));
    public static string Describe(KeyBinding value, byte[]? macros = null)
    {
        if (value.Kind == 0) return "Sin función";
        if (value.Kind == 2 && value.A == 2) return Keyboard.FirstOrDefault(k => k.Value == value.B).Key ?? $"Tecla HID {value.B:X2}";
        if (value.Kind == 2 && value.A == 1) return Modifiers(value.B);
        if (value.Kind == 0xb0) return Modifiers(value.A) + " + " + Describe(new(2,2,value.B));
        if (value.Kind == 1) return Mouse.FirstOrDefault(k => k.Value == value).Key ?? "Ratón";
        if (value.Kind == 3) return Media.FirstOrDefault(k => k.Value == value).Key ?? $"Multimedia {value.A | value.B << 8:X4}";
        if (value.Kind == 6 && value.A == 0xa2) return $"Función G{value.B} original";
        if (value.Kind is 0x70 or 0x71)
        {
            try { return "Macro: " + (macros == null ? $"{value.A + 1}" : HardwareMacros.Read(macros).First(m => m.Index == value.A).Draft.Name); }
            catch (Exception) { return $"Macro del dispositivo {value.A + 1}"; }
        }
        if (value.Kind == 5) return "Fn · función reservada";
        return $"Asignación OEM {value.Kind:X2} {value.A:X2} {value.B:X2}";
    }
    public static string Modifiers(byte mask)
    {
        string[] names = ["Ctrl", "Shift", "Alt", "Win", "Ctrl derecho", "Shift derecho", "Alt derecho", "Win derecho"];
        return string.Join(" + ", names.Where((_, i) => (mask & (1 << i)) != 0));
    }
    public static string ModifierInput(byte mask) => string.Join(" + ",new[]{"Ctrl","Shift","Alt","Win","Rctrl","Rshift","Ralt","Rwin"}.Where((_,i)=>(mask&(1<<i))!=0));
    public static Assignment? AsAssignment(KeyBinding binding)
    {
        if(binding.Kind==2&&binding.A==1)return new("Teclado",ModifierInput(binding.B));
        if(binding.Kind==0xb0)return new("Teclado",ModifierInput(binding.A)+" + "+Describe(new(2,2,binding.B)));
        if(binding.Kind==2&&binding.A==2)return new("Teclado",Describe(binding));
        if(binding.Kind==1&&Mouse.FirstOrDefault(p=>p.Value==binding).Key is {} mouse)return new("Ratón",mouse);
        if(binding.Kind==3&&Media.FirstOrDefault(p=>p.Value==binding).Key is {} media)return new("Multimedia",media);
        if(binding.Kind==0)return new("Desactivar","Sin función");return null;
    }
    public static KeyBinding Encode(Assignment assignment)
    {
        return assignment.Category switch
        {
            "Teclado" => ParseKeyboard(assignment.Value),
            "Ratón" => Mouse.GetValueOrDefault(assignment.Value) ?? throw new InvalidDataException("Acción de ratón no admitida."),
            "Multimedia" => Media.GetValueOrDefault(assignment.Value) ?? throw new InvalidDataException("Multimedia no admitida."),
            "Función" => Functions.GetValueOrDefault(assignment.Value) ?? throw new InvalidDataException("Función no admitida."),
            "Desactivar" => new(0,0,0),
            _ => throw new InvalidDataException("Selecciona una macro válida en la biblioteca.")
        };
    }
    public static KeyBinding ParseKeyboard(string text)
    {
        text=text.Trim();
        var keyName=Keyboard.Keys.OrderByDescending(k=>k.Length).FirstOrDefault(k=>text.Equals(k,StringComparison.OrdinalIgnoreCase)||text.EndsWith("+"+k,StringComparison.OrdinalIgnoreCase)||text.EndsWith("+ "+k,StringComparison.OrdinalIgnoreCase));
        byte? usage=keyName==null?null:Keyboard[keyName];
        string prefix=keyName==null?text:text[..^keyName.Length].Trim().TrimEnd('+').Trim();
        var parts = prefix.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        byte modifiers = 0;
        foreach (var part in parts)
        {
            byte modifier = part.ToLowerInvariant() switch { "ctrl" or "control" => 1, "shift" => 2, "alt" => 4, "win" => 8, "rctrl" => 16, "rshift" => 32, "ralt" => 64, "rwin" => 128, _ => 0 };
            if (modifier != 0) modifiers |= modifier;
            else throw new InvalidDataException("Usa una tecla del selector, opcionalmente con Ctrl, Shift, Alt o Win.");
        }
        if (usage != null) return KeyBinding.Keyboard(usage.Value, modifiers);
        if (modifiers != 0) return KeyBinding.Modifier(modifiers);
        throw new InvalidDataException("Asigna una tecla antes de aplicar.");
    }
    private static IReadOnlyDictionary<string, byte> BuildKeyboard()
    {
        var keys = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < 26; i++) keys[((char)('A' + i)).ToString()] = (byte)(4 + i);
        for (int i = 1; i <= 9; i++) keys[i.ToString()] = (byte)(29 + i);
        keys["0"] = 39;
        string[] special = ["Enter", "Esc", "Backspace", "Tab", "Espacio", "-", "=", "[", "]", "\\", "#", ";", "'", "~", ",", ".", "/", "Caps Lock"];
        for (int i = 0; i < special.Length; i++) keys[special[i]] = (byte)(40 + i);
        for (int i = 1; i <= 12; i++) keys["F" + i] = (byte)(57 + i);
        string[] nav = ["Print Screen", "Scroll Lock", "Pause", "Insert", "Home", "Page Up", "Delete", "End", "Page Down", "Derecha", "Izquierda", "Abajo", "Arriba", "Num Lock", "Num /", "Num *", "Num -", "Num +", "Num Enter", "Num 1", "Num 2", "Num 3", "Num 4", "Num 5", "Num 6", "Num 7", "Num 8", "Num 9", "Num 0", "Num ."];
        for (int i = 0; i < nav.Length; i++) keys[nav[i]] = (byte)(70 + i);
        keys["Menú"] = 0x65;
        for (int i = 13; i <= 24; i++) keys["F" + i] = (byte)(0x68 + i - 13);
        return keys;
    }
    private static HardwareKey[] BuildKeys()
    {
        var list = new List<HardwareKey>();
        void Key(string name, int slot, byte usage) => list.Add(new(name, slot, [2,2,usage]));
        Key("Esc",1,41); for(int i=1;i<=4;i++) Key("F"+i,2+i,(byte)(57+i)); Key("M · dragón",7,16);
        foreach (var (name,slot,id) in new[] { ("G1",39,1),("G2",8,2),("G3",16,3),("G4",24,4),("G5",32,5),("T · G6",22,6),("~ · G7",9,7) }) list.Add(new(name,slot,[6,0xa2,(byte)id]));
        for(int i=1;i<=6;i++)Key(i.ToString(),9+i,(byte)(29+i));
        Key("Tab",17,43);Key("Q",18,20);Key("W",19,26);Key("E",20,8);Key("R",21,21);
        Key("Caps Lock",25,57);Key("A",26,4);Key("S",27,22);Key("D",28,7);Key("F",29,9);Key("G",30,10);
        list.Add(new("Shift",33,[2,1,2]));Key("Z",34,29);Key("X",35,27);Key("C",36,6);Key("V",37,25);Key("B",38,5);
        list.Add(new("Ctrl",41,[2,1,1]));list.Add(new("Fn",42,[5,2,2],true));list.Add(new("Alt",43,[2,1,4]));Key("N",44,17);Key("P",45,19);Key("Espacio",46,44);
        return list.ToArray();
    }
}

public sealed record DeviceMacro(int Index, int Offset, int Length, MacroDraft Draft);
public sealed record MacroAllocation(int Index, int DataOffset, byte[] Data, int DirectoryOffset, byte[] Header, byte[] Expected);

public static class HardwareMacros
{
    public const int Capacity = 6144, MaximumCount = 128;
    public static IReadOnlyList<DeviceMacro> Read(byte[] block)
    {
        if (block.Length != Capacity || BinaryPrimitives.ReadUInt16LittleEndian(block) != 0x55aa) throw new InvalidDataException("Cabecera de macros no reconocida.");
        int used = BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(2)), count = BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(4));
        if (used < 16 + count * 2 || used > Capacity || count > MaximumCount) throw new InvalidDataException("Tabla de macros no válida.");
        var macros = new List<DeviceMacro>();
        for (int i = 0; i < count; i++)
        {
            int offset = BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(16 + 2 * i));
            if (offset < 16 + count * 2 || offset > used - 4) throw new InvalidDataException("Puntero de macro fuera del bloque.");
            int steps = BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(offset));
            int length = checked(4 + steps * 4 + block[offset + 3] * 2);
            if (offset > used - length || macros.Any(m => offset < m.Offset + m.Length && offset + length > m.Offset)) throw new InvalidDataException("Las macros se superponen o están incompletas.");
            string name = block[offset + 3] > 0 ? Encoding.Unicode.GetString(block, offset + 4 + steps * 4, block[offset + 3] * 2).TrimEnd('\0') : $"Macro {i + 1}";
            var draft = new MacroDraft { Name = name, Cycles = Math.Max(1, (int)block[offset + 2]) };
            int before = 0;
            for (int s = 0; s < steps; s++)
            {
                int p = offset + 4 + s * 4; int flags = BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(p));
                int kind = (flags >> 12) & 7; bool down = (flags & 0x8000) != 0;
                if (kind == 0) before += (flags & 0xfff) * 10;
                else
                {
                    string action, value;
                    if (kind == 2) { action = down ? "Tecla ↓" : "Tecla ↑"; value = block[p + 2] == 1 ? BindingCatalog.ModifierInput(block[p + 3]) : BindingCatalog.Keyboard.FirstOrDefault(k => k.Value == block[p + 3]).Key ?? $"HID {block[p+3]:X2}"; }
                    else if (kind == 1 && block[p+2] == 2) { action = "Rueda"; value = block[p+3] == 1 ? "Arriba" : "Abajo"; }
                    else if (kind == 1) { action = down ? "Ratón ↓" : "Ratón ↑"; value = block[p + 3] switch { 1 => "Left", 2 => "Right", 4 => "Middle", _ => "Mouse" }; }
                    else { action = "OEM"; value = $"{kind:X1}:{block[p+2]:X2}{block[p+3]:X2}"; }
                    draft.Steps.Add(new(action, value, before)); before = (flags & 0xfff) * 10;
                }
            }
            if (before > 0) draft.Steps.Add(new("Pausa", "", before));
            macros.Add(new(i, offset, length, draft));
        }
        return macros;
    }
    public static byte[] Encode(MacroDraft macro)
    {
        if (macro.Cycles is < 1 or > 255 || macro.Name.Length is < 1 or > 100 || macro.Steps.Count == 0) throw new InvalidDataException("La macro necesita pasos, nombre y entre 1 y 255 repeticiones.");
        var frames = new List<byte[]>(); var held = new HashSet<string>();
        void Frame(int ticks, int kind, bool down, byte a, byte b) { var data = new byte[4]; BinaryPrimitives.WriteUInt16LittleEndian(data, (ushort)(ticks | kind << 12 | (down ? 0x8000 : 0))); data[2]=a;data[3]=b;frames.Add(data); }
        foreach (var step in macro.Steps)
        {
            if (step.DelayMs is < 0 or > 60000) throw new InvalidDataException("Pausa de macro fuera de rango.");
            int ticks = (int)Math.Round(step.DelayMs / 10.0, MidpointRounding.AwayFromZero);
            while (ticks > 0) { int part = Math.Min(4095, ticks); Frame(part,0,false,0,0); ticks-=part; }
            if (step.Action == "Pausa") continue;
            if (step.Action == "Rueda")
            { Frame(0,1,true,2,step.Value switch { "Arriba" => 1, "Abajo" => 255, _ => throw new InvalidDataException("Dirección de rueda no válida.") }); continue; }
            bool down = step.Action.EndsWith('↓'); bool up = step.Action.EndsWith('↑');
            if (!down && !up) throw new InvalidDataException("La macro contiene una acción OEM sin convertir.");
            string identity = step.Action.StartsWith("Tecla") ? "K:"+step.Value : "M:"+step.Value;
            if (down ? !held.Add(identity) : !held.Remove(identity)) throw new InvalidDataException("La macro tiene pulsaciones o liberaciones sin pareja.");
            if (step.Action.StartsWith("Tecla"))
            {
                var binding = BindingCatalog.ParseKeyboard(step.Value);
                if (binding.Kind != 2) throw new InvalidDataException("Graba los modificadores y las teclas como pasos separados.");
                Frame(0,2,down,binding.A,binding.B);
            }
            else if (step.Action.StartsWith("Ratón"))
            {
                byte button = step.Value switch { "Left" => 1, "Right" => 2, "Middle" => 4, _ => throw new InvalidDataException("Botón de ratón no admitido en macro.") };
                Frame(0,1,down,1,button);
            }
            else throw new InvalidDataException("Acción de macro no admitida.");
        }
        if (held.Count != 0) throw new InvalidDataException("La macro deja teclas o botones pulsados. Añade sus liberaciones.");
        if (frames.Count > 1400) throw new InvalidDataException("La macro supera el espacio del dispositivo.");
        var name = Encoding.Unicode.GetBytes(macro.Name); var result = new byte[4+frames.Count*4+name.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(result,(ushort)frames.Count);result[2]=(byte)macro.Cycles;result[3]=(byte)macro.Name.Length;
        for(int i=0;i<frames.Count;i++)frames[i].CopyTo(result,4+i*4);name.CopyTo(result,4+frames.Count*4);
        return result;
    }
    public static MacroAllocation Allocate(Snapshot snapshot, MacroDraft macro)
    {
        var records = Read(snapshot.Macros); var data = Encode(macro);
        int used = BinaryPrimitives.ReadUInt16LittleEndian(snapshot.Macros.AsSpan(2)); int index = records.Count, directory = 16 + 2 * index;
        if(index>=MaximumCount)throw new InvalidDataException("La tabla de macros está llena.");
        if (records.Any(r=>directory < r.Offset+r.Length && directory+2 > r.Offset)) throw new InvalidDataException("No se puede ampliar esta tabla OEM sin mover macros existentes. La v0.2 conserva las macros actuales.");
        for(int p=0;p<4;p++) for(int k=0;k<64;k++)
        {
            int address=p*192+k*3;
            if(snapshot.Bindings[address] is 0x70 or 0x71 && snapshot.Bindings[address+1]==index)
                throw new InvalidDataException("Un perfil ya referencia este identificador sin definir. No se añadirá una macro que cambie su comportamiento.");
        }
        int offset = records.Count == 0 ? 16 + MaximumCount * 2 : used;
        if(offset+data.Length>Capacity)throw new InvalidDataException($"No hay espacio para esta macro: necesita {data.Length} bytes y quedan {Capacity-offset}.");
        var expected=snapshot.Macros.ToArray();data.CopyTo(expected,offset);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(directory),(ushort)offset);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(2),(ushort)(offset+data.Length));BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(4),(ushort)(index+1));
        return new(index,offset,data,directory,expected.AsSpan(2,4).ToArray(),expected);
    }
}
