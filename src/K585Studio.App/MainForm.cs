using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using K585Studio.Core;

namespace K585Studio.App;

internal sealed class MainForm : Form
{
    internal StudioPage Page { get; private set; }
    internal int Profile { get; private set; } = 2;
    internal string SelectedKey { get; private set; } = "W";
    internal StudioDraft Draft { get; private set; } = new();
    internal Snapshot? Snapshot { get; private set; }
    internal bool Busy { get; private set; }
    internal bool Error { get; private set; }
    internal string Status { get; private set; } = "Los cambios se envían únicamente al pulsar Aplicar.";
    internal Guid? SelectedMacro { get; private set; }
    internal MacroDraft? Macro => Draft.Macros.FirstOrDefault(m => m.Id == SelectedMacro);
    internal IReadOnlyList<DeviceMacro> DeviceMacros { get; private set; } = [];
    internal string MacroMemory => MacroLibrary.DescribeMemory(Snapshot);
    internal MacroReadiness? MacroPreview => Macro is {} macro ? MacroLibrary.Preview(macro, Snapshot) : null;
    internal MacroReadiness? PendingMacroPreview => PendingAssignment is { Category: "Macro" } assignment
        ? Draft.Macros.FirstOrDefault(m => m.Name == assignment.Value) is {} macro
            ? MacroLibrary.Preview(macro, Snapshot) : new(false, false, 0, "La macro ya no está en la biblioteca.")
        : null;
    internal InstallerUpdate? AvailableUpdate { get; private set; }
    internal string? DownloadedInstaller { get; private set; }
    internal string UpdateStatus { get; private set; } = "Comprueba si hay una nueva versión disponible.";
    internal int? UpdateProgress { get; private set; }
    internal bool CanWrite => !Busy && Snapshot?.ActiveProfile == Profile && session != null && !session.WriteFaulted;
    internal Lighting CurrentLighting => Draft.Lighting.GetValueOrDefault(Profile) ?? Snapshot?.Profile(Profile) ?? (Profile == 1 ? Lighting.Default with { Mode = 1 } : Lighting.Default);
    private Lighting EditableLighting => CurrentLighting with{Brightness=(byte)Math.Min(4,(int)CurrentLighting.Brightness),Speed=(byte)Math.Min(4,(int)CurrentLighting.Speed),Direction=CurrentLighting.Direction is 0 or 1 or 255?CurrentLighting.Direction:(byte)255};
    internal int? CurrentRate => Draft.ReportRates.TryGetValue(Profile,out int value) ? value : Snapshot != null && Snapshot.Illumination[(Profile-1)*64+15] <= 3 ? Snapshot.Illumination[(Profile-1)*64+15] : null;
    internal bool LightPending => Snapshot != null && CurrentLighting != Snapshot.Profile(Profile);
    internal bool RatePending => Snapshot != null && CurrentRate is {} rate && rate != Snapshot.Illumination[(Profile-1)*64+15];
    internal Assignment? PendingAssignment => Draft.Profile(Profile).GetValueOrDefault(SelectedKey);
    internal string CurrentAssignment => Snapshot == null ? "Conecta para leer la asignación" : BindingCatalog.Describe(BindingCatalog.Read(Snapshot,Profile,SelectedKey),Snapshot.Macros);
    internal string Notice => Error || Busy || Page != StudioPage.Profile ? Status : Snapshot != null && Snapshot.ActiveProfile != Profile
        ? $"Selecciona de nuevo Perfil {Profile} para activarlo en el teclado." : CurrentLighting.Mode == 23 && !Draft.RainFirmwareConfirmed
        ? "Confirma el firmware de lluvia en Configuración." : PendingMacroPreview is {Ready:false} preview ? preview.Message : Status;
    private readonly StudioCanvas canvas;
    private readonly bool isolated;
    private readonly CancellationTokenSource lifetime = new();
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromMinutes(10) };
    private KeyboardSession? session;
    private bool closeAfterOperation, checkingUpdates;

    internal MainForm(bool autoConnect = true)
    {
        isolated = !autoConnect; AutoScaleMode = AutoScaleMode.None; FormBorderStyle = FormBorderStyle.None;
        Text = "K585 Studio · v" + Updates.Current; BackColor = Theme.Background; StartPosition = FormStartPosition.CenterScreen;
        var working = Screen.FromControl(this).WorkingArea; float dpi = DeviceDpi / 96f;
        MinimumSize = new Size(Math.Min((int)(1040*dpi),working.Width-40),Math.Min((int)(620*dpi),working.Height-40));
        Size = new Size(Math.Min((int)(1360*dpi),working.Width-64),Math.Min((int)(766*dpi),working.Height-64));
        canvas = new StudioCanvas(this); Controls.Add(canvas);
        canvas.Command += async id => await HandleCommand(id); canvas.ValueEdited += EditSlider;
        canvas.DragTitle += () => { ReleaseCapture(); SendMessage(Handle,0xa1,new IntPtr(2),IntPtr.Zero); };
        if (!isolated && File.Exists(Storage.DraftFile))
        { try { Draft = StudioDraft.Load(Storage.DraftFile); } catch (Exception ex) { SetStatus("No se cargó el proyecto anterior: " + ex.Message,true); } }
        SelectedMacro = Draft.Macros.FirstOrDefault()?.Id;
        if(autoConnect)Shown += async (_,_) => { await ReadKeyboard(); if(Draft.AutoCheckUpdates&&!IsDisposed)await CheckUpdates(true); };
        FormClosing += ClosingSafely;
    }

    internal bool IsModified(string key) => Draft.Profile(Profile).ContainsKey(key) || Snapshot != null &&
        !BindingCatalog.Read(Snapshot,Profile,key).Bytes().AsSpan().SequenceEqual(BindingCatalog.Key(key).Factory);
    private void SetStatus(string text,bool error=false){Status=text;Error=error;if(!IsDisposed)canvas?.Invalidate();}
    private void SaveProject()
    {
        if(isolated)return;
        try{Draft.Save(Storage.DraftFile);}catch(Exception ex){SetStatus("No se guardó el proyecto: "+ex.Message,true);}
    }
    private void EditSlider(string id,int value)
    {
        if(Busy||!Effects.All.Any(e=>e.Id==CurrentLighting.Mode))return;var current=EditableLighting;
        Draft.Lighting[Profile]=id=="brightness"?current with{Brightness=(byte)value}:current with{Speed=(byte)(4-value)};
        SaveProject();canvas.Invalidate();
    }
    private async Task HandleCommand(string id)
    {
        if(id=="close"){Close();return;}if(id=="minimize"){WindowState=FormWindowState.Minimized;return;}
        if(id=="maximize"){WindowState=WindowState==FormWindowState.Maximized?FormWindowState.Normal:FormWindowState.Maximized;return;}
        if(Busy)return;
        if(id.StartsWith("profile-")){await SelectProfile(int.Parse(id[8..]));return;}
        if(id.StartsWith("key:")){SelectedKey=id[4..];canvas.Invalidate();return;}
        if(id.StartsWith("rate-")){Draft.ReportRates[Profile]=int.Parse(id[5..]);SaveProject();canvas.Invalidate();return;}
        if(id.StartsWith("macro-select:")){SelectedMacro=Guid.Parse(id[13..]);canvas.Invalidate();return;}
        switch(id)
        {
            case "macros":Page=StudioPage.Macros;break;
            case "settings":Page=StudioPage.Settings;break;
            case "updates":Page=StudioPage.Updates;break;
            case "read":await ReadKeyboard();break;
            case "apply-light":await ApplyLighting();break;
            case "apply-rate":await ApplyRate();break;
            case "apply-key":await ApplyKey();break;
            case "edit-key":EditAssignment();break;
            case "revert-key":Draft.Assign(Profile,SelectedKey,null);SaveProject();break;
            case "revert-light":Draft.Lighting.Remove(Profile);SaveProject();break;
            case "revert-rate":Draft.ReportRates.Remove(Profile);SaveProject();break;
            case "factory-key":Draft.Assign(Profile,SelectedKey,new("Original",SelectedKey));SaveProject();break;
            case "effect":ShowEffects();break;
            case "colorful":Draft.Lighting[Profile]=EditableLighting with{Colorful=!CurrentLighting.Colorful};SaveProject();break;
            case "color":ChooseColor();break;
            case "rain-support":Draft.RainFirmwareConfirmed=!Draft.RainFirmwareConfirmed;SaveProject();break;
            case "auto-updates":Draft.AutoCheckUpdates=!Draft.AutoCheckUpdates;SaveProject();break;
            case "macro-new":EditMacro(true);break;
            case "macro-edit":EditMacro(false);break;
            case "macro-delete":DeleteMacro();break;
            case "macro-list":ShowAllMacros();break;
            case "macro-device":ShowDeviceMacros();break;
            case "import":ImportProject();break;
            case "export":ExportProject();break;
            case "backups":Directory.CreateDirectory(Path.Combine(Storage.Root,"Backups"));Open(Path.Combine(Storage.Root,"Backups"));break;
            case "update-check":await CheckUpdates(false);break;
            case "update-download":await DownloadUpdate();break;
            case "update-install":await InstallUpdate();break;
            case "github":Open($"https://github.com/{Updates.Repository}/releases");break;
        }
        if(!IsDisposed)canvas.Invalidate();
    }
    private void ShowEffects()
    {
        var menu=Menu();foreach(var effect in Effects.All)
        {
            var item=new ToolStripMenuItem(effect.ToString()){Checked=effect.Id==CurrentLighting.Mode};
            item.Click+=(_,_)=>{Draft.Lighting[Profile]=EditableLighting with{Mode=effect.Id};SaveProject();canvas.Invalidate();};menu.Items.Add(item);
        }
        menu.Closed+=(_,_)=>menu.Dispose();menu.Show(Cursor.Position);
    }
    private static ContextMenuStrip Menu()=>new(){BackColor=Theme.Surface,ForeColor=Theme.Ink,Font=new Font("Segoe UI",11),ShowImageMargin=false};
    private void ShowAllMacros()
    {
        var menu=Menu();foreach(var macro in Draft.Macros){var item=new ToolStripMenuItem(macro.Name);item.Click+=(_,_)=>{SelectedMacro=macro.Id;canvas.Invalidate();};menu.Items.Add(item);}menu.Closed+=(_,_)=>menu.Dispose();menu.Show(Cursor.Position);
    }
    private void ShowDeviceMacros()
    {
        if(Snapshot==null||DeviceMacros.Count==0)return;
        var menu=Menu();
        foreach(var record in DeviceMacros)
        {
            string name=record.Draft.Name.Length>48?record.Draft.Name[..48]+"…":record.Draft.Name;
            var item=new ToolStripMenuItem($"#{record.Index+1} · {name} · {record.Length} bytes");
            item.Click+=(_,_)=>
            {
                try
                {
                    var copy=MacroLibrary.CopyTo(Draft,record);SelectedMacro=copy.Id;SaveProject();
                    SetStatus("Copia local seleccionada. Editarla conserva la macro almacenada en el teclado.");
                }
                catch(Exception ex){SetStatus(ex.Message,true);}
                canvas.Invalidate();
            };
            menu.Items.Add(item);
        }
        menu.Closed+=(_,_)=>menu.Dispose();menu.Show(Cursor.Position);
    }
    private void ChooseColor()
    {
        var old=EditableLighting;using var dialog=new ColorDialog{FullOpen=true,Color=Color.FromArgb(old.R,old.G,old.B)};
        if(dialog.ShowDialog(this)==DialogResult.OK){Draft.Lighting[Profile]=old with{R=dialog.Color.R,G=dialog.Color.G,B=dialog.Color.B};SaveProject();}
    }
    private void EditAssignment()
    {
        if(BindingCatalog.Key(SelectedKey).Reserved)return;
        var initial=PendingAssignment??(Snapshot==null?null:BindingCatalog.AsAssignment(BindingCatalog.Read(Snapshot,Profile,SelectedKey)));
        using var dialog=new AssignmentDialog(SelectedKey,initial,Draft.Macros);
        if(dialog.ShowDialog(this)==DialogResult.OK){Draft.Assign(Profile,SelectedKey,dialog.Result);SaveProject();SetStatus("Asignación preparada. Pulsa Aplicar asignación para enviarla al teclado.");}
    }
    private void EditMacro(bool create)
    {
        var existing=create?null:Macro;if(!create&&existing==null)return;
        using var dialog=new MacroDialog(existing,Draft.Macros.Where(m=>m.Id!=existing?.Id).Select(m=>m.Name).ToArray());
        if(dialog.ShowDialog(this)!=DialogResult.OK||dialog.Result==null)return;
        var result=dialog.Result;
        if(existing==null)Draft.Macros.Add(result);
        else
        {
            foreach(var map in Draft.Assignments.Values)foreach(var key in map.Keys.ToArray())if(map[key] is {Category:"Macro"} value&&value.Value==existing.Name)map[key]=value with{Value=result.Name};
            Draft.Macros[Draft.Macros.IndexOf(existing)]=result;
        }
        SelectedMacro=result.Id;SaveProject();SetStatus("Macro guardada. Asígnala a una tecla para guardarla en el dispositivo.");
    }
    private void DeleteMacro()
    {
        var macro=Macro;if(macro==null)return;
        if(Draft.Assignments.Values.Any(map=>map.Values.Any(a=>a.Category=="Macro"&&a.Value==macro.Name))){SetStatus("Quita primero las asignaciones pendientes de esta macro.",true);return;}
        Draft.Macros.Remove(macro);SelectedMacro=Draft.Macros.FirstOrDefault()?.Id;SaveProject();SetStatus("Eliminada de la biblioteca local. Las macros del dispositivo se conservan.");
    }
    private async Task ReadKeyboard()
    {
        await Run(async()=>
        {
            SetStatus("Leyendo dos veces la configuración y guardando un respaldo…");session?.Dispose();session=null;Snapshot=null;DeviceMacros=[];
            session=await Task.Run(()=>new KeyboardSession(NativeHid.Open(),Storage.Backup));
            Snapshot=await Task.Run(()=>session.RefreshAsync(lifetime.Token));
            if(Snapshot.ActiveProfile is >=1 and <=4)Profile=Snapshot.ActiveProfile;
            SetStatus("Lectura verificada. Los ajustes pendientes se conservan.");ReadDeviceMacros();
        });
    }
    private async Task SelectProfile(int profile)
    {
        Protocol.RequireEditableProfile(profile);
        if(isolated&&session==null){Profile=profile;Page=StudioPage.Profile;canvas.Invalidate();return;}
        if(session?.WriteFaulted==true){SetStatus("La última escritura quedó sin verificar. Pulsa Conectar / leer antes de cambiar de perfil.",true);return;}
        if(session==null||Snapshot==null)await ReadKeyboard();
        if(session==null||Snapshot==null){Profile=profile;Page=StudioPage.Profile;canvas.Invalidate();return;}
        await Run(async()=>
        {
            if(!isolated)NativeHid.RequireOtherAppsClosed();
            SetStatus($"Activando el perfil {profile} y verificando su configuración…");
            Snapshot=await Task.Run(()=>session.SelectProfileAsync(profile,lifetime.Token));
            Profile=Snapshot.ActiveProfile;Page=StudioPage.Profile;
            SetStatus($"Perfil {profile} activo. Borradores conservados.");
        });
    }
    private void ReadDeviceMacros()
    {
        if(Snapshot==null)return;
        try
        {
            DeviceMacros=HardwareMacros.Read(Snapshot.Macros);
        }
        catch(InvalidDataException ex){DeviceMacros=[];SetStatus("Lectura válida; tabla de macros no editable: "+ex.Message,true);}
    }
    private async Task ApplyLighting()
    {
        if(!CanWrite||session==null)return;int profile=Profile;var light=CurrentLighting;
        await Run(async()=>{NativeHid.RequireOtherAppsClosed();Snapshot=await Task.Run(()=>session.ApplyLightingAsync(profile,light,Draft.RainFirmwareConfirmed,lifetime.Token));Draft.Lighting.Remove(profile);SaveProject();SetStatus($"Iluminación del perfil {profile} aplicada y verificada. Los demás ajustes se conservan.");});
    }
    private async Task ApplyRate()
    {
        if(!CanWrite||session==null||CurrentRate is not {} rate)return;int profile=Profile;
        await Run(async()=>{NativeHid.RequireOtherAppsClosed();Snapshot=await Task.Run(()=>session.ApplyRateAsync(profile,rate,lifetime.Token));Draft.ReportRates.Remove(profile);SaveProject();SetStatus("Frecuencia guardada y leída de vuelta en este perfil.");});
    }
    private async Task ApplyKey()
    {
        if(!CanWrite||session==null||PendingAssignment is not {} pending)return;int profile=Profile;string key=SelectedKey;
        if(PendingMacroPreview is {Ready:false} preview){SetStatus(preview.Message,true);return;}
        await Run(async()=>
        {
            NativeHid.RequireOtherAppsClosed();
            if(pending.Category=="Ratón"&&pending.Value=="Doble clic")
            {
                var gesture=new MacroDraft{Name="Gesto · doble clic",Steps=[new("Ratón ↓","Left",0),new("Ratón ↑","Left",50),new("Ratón ↓","Left",50),new("Ratón ↑","Left",50)]};
                Snapshot=await Task.Run(()=>session.ApplyMacroAsync(profile,key,gesture,lifetime.Token));
            }
            else if(pending.Category=="Macro")
            {
                var macro=Draft.Macros.SingleOrDefault(m=>m.Name==pending.Value)??throw new InvalidDataException("La macro seleccionada ya no existe.");
                Snapshot=await Task.Run(()=>session.ApplyMacroAsync(profile,key,macro,lifetime.Token));
            }
            else
            {
                var binding=pending.Category=="Original"?KeyBinding.Parse(BindingCatalog.Key(key).Factory):BindingCatalog.Encode(pending);
                Snapshot=await Task.Run(()=>session.ApplyBindingAsync(profile,key,binding,lifetime.Token));
            }
            Draft.Assign(profile,key,null);SaveProject();SetStatus($"Asignación del perfil {profile} guardada y verificada. Los demás perfiles se conservan.");ReadDeviceMacros();
        });
    }
    private async Task Run(Func<Task> action)
    {
        if(Busy)return;Busy=true;Error=false;canvas.Invalidate();
        try{await action();}
        catch(OperationCanceledException){SetStatus("Operación interrumpida. Vuelve a leer antes de aplicar.",true);}
        catch(Exception ex){if(ex is IOException&&ex is not InvalidDataException)Snapshot=null;SetStatus(ex.Message,true);if(Page==StudioPage.Updates){UpdateStatus=ex.Message;UpdateProgress=null;}if(!isolated){Directory.CreateDirectory(Storage.Root);File.WriteAllText(Path.Combine(Storage.Root,"last-error.txt"),ex.ToString());}}
        finally{if(session?.WriteFaulted==true)Snapshot=null;if(Snapshot==null)DeviceMacros=[];Busy=false;if(!IsDisposed)canvas.Invalidate();if(closeAfterOperation)Close();}
    }
    private async Task CheckUpdates(bool background)
    {
        if(checkingUpdates||isolated)return;checkingUpdates=true;
        try
        {
            UpdateStatus="Consultando versiones estables de GitHub…";canvas.Invalidate();
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);timeout.CancelAfter(TimeSpan.FromSeconds(25));
            var result=await Updates.CheckAsync(http,timeout.Token);
            if(AvailableUpdate!=result.Installer)DownloadedInstaller=null;
            AvailableUpdate=result.Installer;UpdateStatus=result.Message;
            if(AvailableUpdate is {} update)
            {
                string cached=Path.Combine(Storage.Root,"Updates",Updates.InstallerName(update.Version));
                if(File.Exists(cached))
                {
                    try{await Updates.VerifyFileAsync(update,cached,timeout.Token);DownloadedInstaller=cached;UpdateStatus="Actualización descargada y verificada. Lista para instalar.";}
                    catch(InvalidDataException){DownloadedInstaller=null;UpdateStatus="Hay una versión nueva; vuelve a descargar el archivo guardado.";}
                }
            }
        }
        catch(Exception ex){UpdateStatus="No se pudo comprobar: "+ex.Message;if(!background)SetStatus(UpdateStatus,true);}
        finally{checkingUpdates=false;if(!IsDisposed)canvas.Invalidate();}
    }
    private async Task DownloadUpdate()
    {
        if(AvailableUpdate is not {} update)return;
        await Run(async()=>
        {
            UpdateProgress=0;UpdateStatus="Descargando el instalador…";
            var progress=new Progress<int>(value=>{UpdateProgress=value;if(!IsDisposed)canvas.Invalidate();});
            DownloadedInstaller=await Updates.DownloadAsync(http,update,Path.Combine(Storage.Root,"Updates"),progress,lifetime.Token);
            UpdateProgress=null;UpdateStatus="Descarga completa. Tamaño y SHA-256 verificados.";
        });
    }
    private async Task InstallUpdate()
    {
        if(AvailableUpdate is not {} update||DownloadedInstaller is not {} installer)return;
        await Run(async()=>
        {
            UpdateStatus="Verificando de nuevo la publicación y el instalador…";
            await Updates.ConfirmPublishedAsync(http,update,lifetime.Token);await Updates.VerifyFileAsync(update,installer,lifetime.Token);
            session?.Dispose();session=null;
            var start=new ProcessStartInfo(installer){UseShellExecute=false};
            start.ArgumentList.Add("/SILENT");start.ArgumentList.Add("/NORESTART");start.ArgumentList.Add("/CURRENTUSER");
            start.ArgumentList.Add("/K585WAITPID="+Environment.ProcessId);start.ArgumentList.Add("/K585RESTART=1");
            using var process=Process.Start(start)??throw new IOException("No se pudo iniciar el instalador.");
            closeAfterOperation=true;
        });
    }
    private void ImportProject()
    {
        using var dialog=new OpenFileDialog{Filter="Proyecto K585 Studio|*.k585.json"};if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        try{Draft=StudioDraft.Load(dialog.FileName);SelectedMacro=Draft.Macros.FirstOrDefault()?.Id;SaveProject();SetStatus("Proyecto importado como cambios pendientes. Nada se ha escrito al teclado.");}catch(Exception ex){SetStatus(ex.Message,true);}
    }
    private void ExportProject()
    {
        using var dialog=new SaveFileDialog{Filter="Proyecto K585 Studio|*.k585.json",FileName="K585-proyecto.k585.json"};if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        try{Draft.Save(dialog.FileName);SetStatus("Proyecto exportado con asignaciones, macros e iluminación pendientes.");}catch(Exception ex){SetStatus(ex.Message,true);}
    }
    private static void Open(string target)=>Process.Start(new ProcessStartInfo(target){UseShellExecute=true});
    private void ClosingSafely(object? sender,FormClosingEventArgs e)
    {
        if(Busy){e.Cancel=true;closeAfterOperation=true;SetStatus("Terminando la operación antes de cerrar…");return;}
        lifetime.Cancel();session?.Dispose();session=null;SaveProject();
    }
    protected override void Dispose(bool disposing){if(disposing){session?.Dispose();lifetime.Dispose();http.Dispose();}base.Dispose(disposing);}
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if(m.Msg==0x84&&WindowState==FormWindowState.Normal&&m.Result==new IntPtr(1))
        {
            long raw=m.LParam.ToInt64();var point=PointToClient(new Point((short)(raw&0xffff),(short)((raw>>16)&0xffff)));int edge=Math.Max(6,DeviceDpi/12);
            bool left=point.X<edge,right=point.X>=Width-edge,top=point.Y<edge,bottom=point.Y>=Height-edge;
            int hit=left&&top?13:right&&top?14:left&&bottom?16:right&&bottom?17:left?10:right?11:top?12:bottom?15:1;m.Result=new IntPtr(hit);
        }
    }
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wparam,IntPtr lparam);

    internal int RunUiChecks(string folder)
    {
        Directory.CreateDirectory(folder);canvas.Dock=DockStyle.None;var checks=new List<string>();
        void Assert(bool condition,string label){if(!condition)throw new InvalidOperationException(label);checks.Add(label);}
        void Await(Task task)
        {
            var timer=Stopwatch.StartNew();
            while(!task.IsCompleted){if(timer.Elapsed>TimeSpan.FromSeconds(10))throw new TimeoutException("UI check did not finish.");Application.DoEvents();Thread.Sleep(1);}
            task.GetAwaiter().GetResult();
        }
        void Render(string name,int width=1672,int height=941)
        {
            canvas.Size=new(width,height);Assert(canvas.Width==width&&canvas.Height==height,name+": viewport matches output");using var bitmap=new Bitmap(width,height);using var graphics=Graphics.FromImage(bitmap);
            canvas.Render(graphics);bitmap.Save(Path.Combine(folder,name+".png"));
            Assert(canvas.Hits.All(h=>h.Bounds.Left>=0&&h.Bounds.Top>=0&&h.Bounds.Right<=1672&&h.Bounds.Bottom<=941),name+": all actions inside canvas");
        }
        try
        {
            Draft=new();Render("profile2-offline");
            Assert(canvas.Hits.Count(h=>h.Id.StartsWith("key:"))==42,"All 42 keys selectable");
            Assert(!canvas.Hits.Single(h=>h.Id=="apply-key").Enabled,"No hardware apply without read");
            EditSlider("speed",0);Assert(CurrentLighting.Speed==4,"Left speed is slowest, no device write");
            EditSlider("brightness",2);Assert(CurrentLighting.Brightness==2,"Brightness remains a local draft");
            HandleCommand("revert-light").GetAwaiter().GetResult();Assert(!Draft.Lighting.ContainsKey(2),"Discard lighting removes only local draft");
            Draft.ReportRates[2]=3;HandleCommand("revert-rate").GetAwaiter().GetResult();Assert(!Draft.ReportRates.ContainsKey(2),"Discard rate removes only local draft");
            Draft.Assign(2,"W",new("Teclado","Ctrl + X"));Assert(IsModified("W")&&!IsModified("E"),"Orange overlay scoped to modified key");Render("profile2-pending");
            Await(HandleCommand("profile-1"));Render("profile1-editable");Assert(canvas.Hits.Where(h=>h.Id is "edit-key" or "factory-key" or "brightness" or "speed" or "effect").All(h=>h.Enabled),"Profile 1 edit controls unlocked");
            Assert(!IsModified("W"),"Profile 2 drafts never appear in profile 1");
            EditSlider("brightness",2);Draft.Assign(1,"W",new("Teclado","F5"));Draft.ReportRates[1]=3;
            Assert(Draft.Lighting[1].Brightness==2&&Draft.Profile(1)["W"].Value=="F5"&&Draft.Profile(2)["W"].Value=="Ctrl + X","Profile 1 edits preserve drafts in other profiles");
            Profile=2;Page=StudioPage.Macros;Render("macros-empty");
            Assert(!canvas.Hits.Single(h=>h.Id=="macro-device").Enabled,"Device copy unavailable without a verified read");
            var macro=new MacroDraft{Name="Prueba de edición",Steps=[new("Tecla ↓","W",0),new("Tecla ↑","W",150)]};Draft.Macros.Add(macro);SelectedMacro=macro.Id;Render("macros");
            Assert(Macro==macro,"Macro selection reflects library");
            Assert(MacroPreview is {Ready:false,Bytes:>0},"Offline macro preview does not promise device capacity");
            var memory=new byte[HardwareMacros.Capacity];memory[0]=0xaa;memory[1]=0x55;memory[2]=16;
            var snapshot=new Snapshot(DateTimeOffset.UtcNow,new byte[128],new byte[256],new byte[768],memory,new byte[768]);
            foreach(int p in Enumerable.Range(1,4))(Lighting.Default with{Mode=1}).Bytes().CopyTo(snapshot.Illumination,(p-1)*64);
            foreach(int p in new[]{1,2,3,4})foreach(var key in BindingCatalog.Keys)key.Factory.CopyTo(snapshot.Bindings,(p-1)*192+key.Slot*3);
            var stored=HardwareMacros.Allocate(snapshot,new(){Name="Teclado",Steps=[new("Tecla ↓","E",0),new("Tecla ↑","E",100)]});
            Snapshot=snapshot with{Macros=stored.Expected};ReadDeviceMacros();
            Assert(DeviceMacros.Count==1&&Draft.Macros.Count==1&&Macro==macro,"Reading inventory never imports or overwrites local macros");
            Render("macros-connected");Assert(canvas.Hits.Single(h=>h.Id=="macro-device").Enabled,"Device inventory available after read");
            Assert(MacroPreview is {Ready:true,ReusesRecord:false},"UI reports allocatable local macro");
            var copy=MacroLibrary.CopyTo(Draft,DeviceMacros[0]);SelectedMacro=copy.Id;Render("macros-reused");
            Assert(MacroPreview is {Ready:true,ReusesRecord:true},"UI recognizes reusable device macro");
            var transport=new UiCheckTransport(Snapshot);session=new KeyboardSession(transport,_=>Task.CompletedTask);
            Snapshot=session.RefreshAsync().GetAwaiter().GetResult();string savedDraft=JsonSerializer.Serialize(Draft);
            foreach(int p in new[]{2,3,4,1})
            {
                Await(HandleCommand("profile-"+p));
                Assert(Profile==p&&Snapshot?.ActiveProfile==p&&Page==StudioPage.Profile,"Sidebar selection activates profile "+p+" in simulated device");
                Assert(JsonSerializer.Serialize(Draft)==savedDraft,"Profile navigation preserves all pending edits");
            }
            Assert(transport.Writes==4,"Only four single-byte selection writes; drafts not applied");
            Render("profile1-connected");Assert(canvas.Hits.Where(h=>h.Id is "apply-key" or "apply-light" or "apply-rate").All(h=>h.Enabled),"Apply controls available for connected profile 1");
            Assert(canvas.NoticeBounds.Right<canvas.Hits.Single(h=>h.Id=="revert-light").Bounds.Left,"Status never overlaps discard-lighting button");
            SelectedKey="Fn";Render("fn-reserved");Assert(!canvas.Hits.Single(h=>h.Id=="edit-key").Enabled&&!canvas.Hits.Single(h=>h.Id=="factory-key").Enabled,"Fn stays reserved in editable profile 1");SelectedKey="W";
            transport.TimeoutOnSelection=true;Await(HandleCommand("profile-4"));
            Assert(Error&&Snapshot==null&&Profile==1&&!CanWrite,"Failed selection never claims target profile is active");
            int attempts=transport.Writes;Await(HandleCommand("profile-3"));Assert(transport.Writes==attempts,"Failed selection cannot silently reconnect and retry");
            session.Dispose();session=null;
            Snapshot=null;DeviceMacros=[];
            SetStatus("Los cambios se envían únicamente al pulsar Aplicar.");
            Page=StudioPage.Settings;Render("settings");Page=StudioPage.Updates;Render("updates");
            Page=StudioPage.Profile;Render("profile-1040x660",1040,660);Render("profile-1920x1080",1920,1080);
            using(var dialog=new AssignmentDialog("W",new("Teclado","Ctrl + X"),Draft.Macros))Assert(dialog.ValidateSelection()==new Assignment("Teclado","Ctrl + X"),"Assignment editor preserves combo");
            using(var dialog=new MacroDialog(macro,[])){Assert(dialog.GetDraft().Steps.Count==2,"Macro editor loads steps");dialog.Reorder(0,1);Assert(dialog.GetDraft().Steps[0].Action=="Tecla ↑","Macro reordering works");}
            Assert(session==null&&Snapshot==null&&isolated,"Headless checks do not open HID or save the user's project");
            File.WriteAllText(Path.Combine(folder,"ui-checks.json"),JsonSerializer.Serialize(new{passed=checks.Count,checks},new JsonSerializerOptions{WriteIndented=true}));return 0;
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(folder,"ui-error.txt"),ex.ToString());return 1;}
    }
}
