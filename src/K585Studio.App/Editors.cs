using K585Studio.Core;

namespace K585Studio.App;

internal static class EditorKeys
{
    internal static string? Name(Keys key)
    {
        if(key is >= Keys.A and <= Keys.Z)return key.ToString();
        if(key is >= Keys.D0 and <= Keys.D9)return ((int)key-(int)Keys.D0).ToString();
        if(key is >= Keys.NumPad0 and <= Keys.NumPad9)return "Num "+((int)key-(int)Keys.NumPad0);
        if(key is >= Keys.F1 and <= Keys.F24)return key.ToString();
        return key switch
        {
            Keys.ControlKey or Keys.LControlKey=>"Ctrl",Keys.RControlKey=>"Rctrl",Keys.ShiftKey or Keys.LShiftKey=>"Shift",Keys.RShiftKey=>"Rshift",
            Keys.Menu or Keys.LMenu=>"Alt",Keys.RMenu=>"Ralt",Keys.LWin=>"Win",Keys.RWin=>"Rwin",Keys.Return=>"Enter",Keys.Escape=>"Esc",
            Keys.Space=>"Espacio",Keys.Back=>"Backspace",Keys.Tab=>"Tab",Keys.CapsLock=>"Caps Lock",Keys.Up=>"Arriba",Keys.Down=>"Abajo",
            Keys.Left=>"Izquierda",Keys.Right=>"Derecha",Keys.Insert=>"Insert",Keys.Delete=>"Delete",Keys.Home=>"Home",Keys.End=>"End",
            Keys.PageUp=>"Page Up",Keys.PageDown=>"Page Down",Keys.PrintScreen=>"Print Screen",Keys.Scroll=>"Scroll Lock",Keys.Pause=>"Pause",
            Keys.NumLock=>"Num Lock",Keys.Multiply=>"Num *",Keys.Divide=>"Num /",Keys.Add=>"Num +",Keys.Subtract=>"Num -",Keys.Decimal=>"Num .",
            Keys.OemMinus=>"-",Keys.Oemplus=>"=",Keys.OemOpenBrackets=>"[",Keys.OemCloseBrackets=>"]",Keys.OemPipe=>"\\",Keys.OemSemicolon=>";",
            Keys.OemQuotes=>"'",Keys.Oemtilde=>"~",Keys.Oemcomma=>",",Keys.OemPeriod=>".",Keys.OemQuestion=>"/",Keys.Apps=>"Menú",_=>null
        };
    }
    internal static void Configure(Form form,int width,int height)
    {
        form.BackColor=Theme.Background;form.ForeColor=Theme.Ink;form.Font=new Font("Segoe UI",10);form.AutoScaleMode=AutoScaleMode.None;
        form.StartPosition=FormStartPosition.CenterParent;form.MinimizeBox=false;form.MaximizeBox=false;
        float scale=form.DeviceDpi/96f;var area=Screen.FromControl(form).WorkingArea;
        form.ClientSize=new Size(Math.Min((int)(width*scale),area.Width-80),Math.Min((int)(height*scale),area.Height-100));
    }
}

internal sealed class AssignmentDialog : Form
{
    private readonly ComboBox category, value;
    private readonly CheckBox[] modifiers;
    private readonly Label message;
    private readonly IReadOnlyList<MacroDraft> macros;
    internal Assignment? Result{get;private set;}
    internal AssignmentDialog(string key,Assignment? pending,IReadOnlyList<MacroDraft> macros)
    {
        this.macros=macros;Text="Asignación · "+key;EditorKeys.Configure(this,520,480);FormBorderStyle=FormBorderStyle.FixedDialog;
        var stack=Theme.Stack(22);stack.BackColor=Theme.Background;
        stack.Controls.Add(Theme.Label("Asignar "+key,20,true));stack.Controls.Add(Theme.Label("Preparar un cambio no escribe en el teclado.",10));
        category=Theme.Combo(["Teclado","Ratón","Multimedia","Función","Macro","Desactivar"],440);
        value=Theme.Combo([],440);value.MaxDropDownItems=15;stack.Controls.Add(category);stack.Controls.Add(value);
        modifiers=new CheckBox[8];var row=Theme.Row();string[] names=["Ctrl","Shift","Alt","Win","Rctrl","Rshift","Ralt","Rwin"];
        for(int i=0;i<8;i++){modifiers[i]=new CheckBox{Text=names[i],ForeColor=Theme.Ink,AutoSize=true,Width=95,Margin=new Padding(0,4,14,4)};row.Controls.Add(modifiers[i]);}
        stack.Controls.Add(row);
        var capture=Theme.TextBox(440);capture.ReadOnly=true;capture.PlaceholderText="Haz clic aquí y pulsa una combinación";stack.Controls.Add(capture);
        capture.PreviewKeyDown+=(_,e)=>e.IsInputKey=true;
        capture.KeyDown+=(_,e)=>
        {
            string? name=EditorKeys.Name(e.KeyCode);if(name==null)return;
            category.SelectedItem="Teclado";
            for(int i=0;i<modifiers.Length;i++)modifiers[i].Checked=i switch{0=>e.Control,1=>e.Shift,2=>e.Alt,_=>false};
            value.SelectedItem=BindingCatalog.Keyboard.ContainsKey(name)?name:"Solo modificadores";
            e.Handled=true;
        };
        capture.KeyPress+=(_,e)=>e.Handled=true;
        message=Theme.Label("Fn conserva su función. Aplicar modifica solo la tecla del perfil seleccionado.",10);message.MaximumSize=new(440,0);message.ForeColor=Theme.Muted;stack.Controls.Add(message);
        var actions=Theme.Row();var prepare=Theme.Button("Preparar cambio",(_,_)=>
        {
            try{Result=ValidateSelection();DialogResult=DialogResult.OK;Close();}catch(Exception ex){message.Text=ex.Message;message.ForeColor=Theme.Orange;}
        },true);
        actions.Controls.Add(prepare);actions.Controls.Add(Theme.Button("Cancelar",(_,_)=>{DialogResult=DialogResult.Cancel;Close();}));stack.Controls.Add(actions);AcceptButton=prepare;
        category.SelectedIndexChanged+=(_,_)=>Populate();
        Populate();
        if(pending!=null)
        {
            if(category.Items.Contains(pending.Category))category.SelectedItem=pending.Category;
            if(pending.Category=="Teclado")
            {
                try{var binding=BindingCatalog.ParseKeyboard(pending.Value);byte mask=binding.Kind==0xb0?binding.A:binding.A==1?binding.B:(byte)0;for(int i=0;i<8;i++)modifiers[i].Checked=(mask&(1<<i))!=0;
                    value.SelectedItem=binding.Kind==2&&binding.A==1?"Solo modificadores":BindingCatalog.Keyboard.First(p=>p.Value==binding.B).Key;}catch(InvalidDataException){ }
            }
            else if(value.Items.Contains(pending.Value))value.SelectedItem=pending.Value;
        }
        Controls.Add(stack);stack.Scale(new SizeF(DeviceDpi/96f,DeviceDpi/96f));
    }
    private void Populate()
    {
        value.Items.Clear();IEnumerable<string> options=category.Text switch
        {"Teclado"=>new[]{"Solo modificadores"}.Concat(BindingCatalog.Keyboard.Keys),"Ratón"=>BindingCatalog.Mouse.Keys.Concat(new[]{"Doble clic"}),"Multimedia"=>BindingCatalog.Media.Keys,"Función"=>BindingCatalog.Functions.Keys,"Macro"=>macros.Select(m=>m.Name),_=>new[]{"Sin función"}};
        value.Items.AddRange(options.Cast<object>().ToArray());if(value.Items.Count>0)value.SelectedIndex=0;
        foreach(var checkbox in modifiers)checkbox.Enabled=category.Text=="Teclado";
    }
    internal Assignment ValidateSelection()
    {
        string text=value.SelectedItem?.ToString()??"";
        if(category.Text=="Teclado")
        {
            var parts=modifiers.Where(m=>m.Checked).Select(m=>m.Text).ToList();if(text!="Solo modificadores")parts.Add(text);text=string.Join(" + ",parts);
            BindingCatalog.ParseKeyboard(text).Validate();
        }
        else if(category.Text=="Macro"){if(!macros.Any(m=>m.Name==text))throw new InvalidDataException("Crea una macro antes de asignarla.");}
        else if (!(category.Text=="Ratón"&&text=="Doble clic")) BindingCatalog.Encode(new(category.Text,text)).Validate();
        return new(category.Text,text);
    }
}

internal sealed class MacroDialog : Form
{
    private readonly TextBox name,capture;
    private readonly NumericUpDown cycles;
    private readonly DataGridView grid;
    private readonly CheckBox recordDelay;
    private readonly Label message;
    private readonly Guid id;
    private readonly IReadOnlyList<string> otherNames;
    private readonly HashSet<string> heldKeys=[],heldButtons=[];
    private bool recording;
    private long lastEvent;
    internal MacroDraft? Result{get;private set;}
    internal MacroDialog(MacroDraft? original,IReadOnlyList<string> names)
    {
        id=original?.Id??Guid.NewGuid();otherNames=names;Text="Editor de macros · K585 Studio";EditorKeys.Configure(this,1040,750);
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),RowCount=6,ColumnCount=1,BackColor=Theme.Background};
        layout.RowStyles.Add(new(SizeType.Absolute,48));layout.RowStyles.Add(new(SizeType.Absolute,42));layout.RowStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.Absolute,92));layout.RowStyles.Add(new(SizeType.Absolute,70));layout.RowStyles.Add(new(SizeType.Absolute,62));
        layout.Controls.Add(Theme.Label("Editar macro",20,true),0,0);
        var meta=Theme.Row();name=Theme.TextBox(410);name.Text=original?.Name??NextName(names);meta.Controls.Add(name);meta.Controls.Add(Theme.Label("Repeticiones"));cycles=new NumericUpDown{Minimum=1,Maximum=Math.Max(255,original?.Cycles??1),Value=original?.Cycles??1,BackColor=Theme.Surface,ForeColor=Theme.Ink,Width=85};meta.Controls.Add(cycles);layout.Controls.Add(meta,0,1);
        grid=new DataGridView{Dock=DockStyle.Fill,BackgroundColor=Theme.Surface,BorderStyle=BorderStyle.None,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,EnableHeadersVisualStyles=false};
        grid.DefaultCellStyle.BackColor=Theme.Surface;grid.DefaultCellStyle.ForeColor=Theme.Ink;grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(16,60,90);grid.DefaultCellStyle.SelectionForeColor=Theme.Ink;
        grid.ColumnHeadersDefaultCellStyle.BackColor=Theme.Border;grid.ColumnHeadersDefaultCellStyle.ForeColor=Theme.Ink;grid.GridColor=Theme.Border;
        var action=new DataGridViewComboBoxColumn{HeaderText="Acción",FlatStyle=FlatStyle.Flat};action.Items.AddRange("Tecla ↓","Tecla ↑","Ratón ↓","Ratón ↑","Rueda","Pausa","OEM");grid.Columns.Add(action);
        grid.Columns.Add("Value","Tecla / botón");grid.Columns.Add("Delay","Pausa antes (ms)");grid.DataError+=(_,e)=>e.ThrowException=false;
        if(original!=null)foreach(var step in original.Steps)grid.Rows.Add(step.Action,step.Value,step.DelayMs);
        layout.Controls.Add(grid,0,2);
        var actions=Theme.Row();
        actions.Controls.Add(Theme.Button("Añadir paso",(_,_)=>grid.Rows.Add("Pausa","","100")));
        actions.Controls.Add(Theme.Button("↑",(_,_)=>MoveStep(-1)));actions.Controls.Add(Theme.Button("↓",(_,_)=>MoveStep(1)));
        actions.Controls.Add(Theme.Button("Al inicio",(_,_)=>MoveStep(-9999)));actions.Controls.Add(Theme.Button("Al final",(_,_)=>MoveStep(9999)));
        actions.Controls.Add(Theme.Button("Quitar paso",(_,_)=>{if(grid.CurrentRow!=null)grid.Rows.Remove(grid.CurrentRow);}));
        actions.Controls.Add(Theme.Button("Vaciar",(_,_)=>grid.Rows.Clear()));layout.Controls.Add(actions,0,3);
        var recordingRow=Theme.Row();capture=Theme.TextBox(430);capture.ReadOnly=true;capture.PlaceholderText="Zona de grabación (solo mientras tiene el foco)";capture.ShortcutsEnabled=false;capture.ContextMenuStrip=new ContextMenuStrip();recordingRow.Controls.Add(capture);
        recordingRow.Controls.Add(Theme.Button("Grabar",(_,_)=>{recording=true;lastEvent=Environment.TickCount64;capture.Focus();message!.Text="Grabando dentro de esta zona. Cambiar el foco detiene la captura.";}));
        recordingRow.Controls.Add(Theme.Button("Detener",(_,_)=>StopRecording()));recordDelay=new CheckBox{Text="Grabar pausas",Checked=true,AutoSize=true,ForeColor=Theme.Ink};recordingRow.Controls.Add(recordDelay);layout.Controls.Add(recordingRow,0,4);
        capture.PreviewKeyDown+=(_,e)=>e.IsInputKey=true;
        capture.KeyDown+=(_,e)=>{if(recording&&EditorKeys.Name(e.KeyCode) is {} value&&heldKeys.Add(value))Record("Tecla ↓",value);e.Handled=recording;};
        capture.KeyUp+=(_,e)=>{if(recording&&EditorKeys.Name(e.KeyCode) is {} value&&heldKeys.Remove(value))Record("Tecla ↑",value);e.Handled=recording;};
        capture.KeyPress+=(_,e)=>e.Handled=recording;
        capture.MouseDown+=(_,e)=>{if(recording&&heldButtons.Add(e.Button.ToString()))Record("Ratón ↓",e.Button.ToString());};
        capture.MouseUp+=(_,e)=>{if(recording&&heldButtons.Remove(e.Button.ToString()))Record("Ratón ↑",e.Button.ToString());};capture.LostFocus+=(_,_)=>StopRecording();
        capture.MouseWheel+=(_,e)=>{if(recording)Record("Rueda",e.Delta>0?"Arriba":"Abajo");};
        var footer=Theme.Row();message=Theme.Label("El dispositivo usa pausas en pasos de 10 ms y admite 1–255 repeticiones.",9);message.MaximumSize=new Size(530,0);message.ForeColor=Theme.Muted;footer.Controls.Add(message);
        var save=Theme.Button("Guardar macro",(_,_)=>
        {
            try{StopRecording();Result=GetDraft();DialogResult=DialogResult.OK;Close();}catch(Exception ex){message.Text=ex.Message;message.ForeColor=Theme.Orange;}
        },true);footer.Controls.Add(save);footer.Controls.Add(Theme.Button("Cancelar",(_,_)=>{StopRecording();DialogResult=DialogResult.Cancel;Close();}));layout.Controls.Add(footer,0,5);
        layout.SizeChanged+=(_,_)=>{int max=Math.Max(400,layout.ClientSize.Width-layout.Padding.Horizontal);actions.MaximumSize=recordingRow.MaximumSize=footer.MaximumSize=new Size(max,0);};
        Controls.Add(layout);layout.Scale(new SizeF(DeviceDpi/96f,DeviceDpi/96f));FormClosing+=(_,_)=>StopRecording();
    }
    private static string NextName(IReadOnlyList<string> existing){int n=1;while(existing.Contains("Macro "+n,StringComparer.OrdinalIgnoreCase))n++;return "Macro "+n;}
    private void MoveStep(int delta){if(grid.CurrentRow!=null)Reorder(grid.CurrentRow.Index,Math.Clamp(grid.CurrentRow.Index+delta,0,grid.Rows.Count-1));}
    internal void Reorder(int from,int to){if(from==to)return;var row=grid.Rows[from];grid.Rows.RemoveAt(from);grid.Rows.Insert(to,row);grid.CurrentCell=row.Cells[0];}
    internal MacroDraft GetDraft()
    {
        grid.EndEdit();string title=name.Text.Trim();
        if(title.Length is <1 or >100||otherNames.Contains(title,StringComparer.OrdinalIgnoreCase))throw new InvalidDataException("El nombre está vacío, repetido o supera 100 caracteres.");
        var macro=new MacroDraft{Id=id,Name=title,Cycles=(int)cycles.Value};
        foreach(DataGridViewRow row in grid.Rows)
        {
            if(!int.TryParse(row.Cells[2].Value?.ToString(),out int delay)||delay is <0 or >60000)throw new InvalidDataException("Las pausas deben estar entre 0 y 60000 ms.");
            delay=(int)Math.Round(delay/10.0,MidpointRounding.AwayFromZero)*10;
            macro.Steps.Add(new(row.Cells[0].Value?.ToString()??"Pausa",row.Cells[1].Value?.ToString()??"",delay));
        }
        if(macro.Steps.Count>2000)throw new InvalidDataException("La macro supera 2000 pasos locales.");return macro;
    }
    private void Record(string action,string value)
    {
        if(grid.Rows.Count>=1980){StopRecording();return;}long now=Environment.TickCount64;
        grid.Rows.Add(action,value,recordDelay.Checked?(int)Math.Clamp(now-lastEvent,0,60000):0);lastEvent=now;
    }
    private void StopRecording()
    {
        if(!recording)return;recording=false;foreach(var value in heldKeys)grid.Rows.Add("Tecla ↑",value,0);foreach(var value in heldButtons)grid.Rows.Add("Ratón ↑",value,0);heldKeys.Clear();heldButtons.Clear();
    }
}
