using System.Drawing.Drawing2D;
using System.Drawing.Text;
using K585Studio.Core;

namespace K585Studio.App;

internal enum StudioPage { Profile, Macros, Settings, Updates }
internal sealed record StudioHit(string Id, RectangleF Bounds, string Label, bool Enabled = true, int Min = 0, int Max = 0);

internal sealed class StudioCanvas : Control
{
    internal const float ReferenceWidth = 1672, ReferenceHeight = 941;
    private readonly Dictionary<int, Font> fonts = [];
    private readonly Image keyboard;
    private readonly List<StudioHit> hits = [];
    private float scale = 1, dx, dy;
    private string? focusId, dragging;
    internal MainForm Host { get; }
    internal IReadOnlyList<StudioHit> Hits => hits;
    internal RectangleF NoticeBounds => new(306,838,Host.Page==StudioPage.Profile&&Host.Draft.Lighting.ContainsKey(Host.Profile)?380:650,40);
    internal void Render(Graphics graphics) => OnPaint(new PaintEventArgs(graphics,new Rectangle(Point.Empty,Size)));
    internal event Action<string>? Command;
    internal event Action<string, int>? ValueEdited;
    internal event Action? DragTitle;
    internal StudioCanvas(MainForm host)
    {
        Host = host; Dock = DockStyle.Fill; DoubleBuffered = true; TabStop = true;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        BackColor = Theme.Background; AccessibleName = "K585 Studio"; AccessibleRole = AccessibleRole.Pane;
        using var source = typeof(StudioCanvas).Assembly.GetManifestResourceStream("K585Studio.App.Assets.keyboard.png")!;
        using var image = Image.FromStream(source); keyboard = new Bitmap(image);
    }
    private Font F(int size) { if (!fonts.TryGetValue(size, out var font)) fonts[size] = font = new("Segoe UI", size, FontStyle.Regular, GraphicsUnit.Pixel); return font; }
    private void DrawText(Graphics g, string text, float x, float y, int size = 22, Color? color = null, float width = 1200, float height = 55, bool bold = false, bool center = false)
    {
        using var brush = new SolidBrush(color ?? Theme.Ink);
        using var format = new StringFormat { Alignment = center ? StringAlignment.Center : StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.LineLimit };
        if (bold) { using var font = new Font(F(size), FontStyle.Bold); g.DrawString(text, font, brush, new RectangleF(x,y,width,height), format); }
        else g.DrawString(text,F(size),brush,new RectangleF(x,y,width,height),format);
    }
    private static Color Shade(float strength) => Color.FromArgb((int)(9 - 9 * strength), (int)(19 + 138 * strength), (int)(27 + 228 * strength));
    private static void Card(Graphics g, RectangleF rect, bool selected = false, int radius = 16)
    {
        using var shape = Theme.Rounded(rect, radius);
        using var brush = new LinearGradientBrush(rect, selected ? Shade(.25f) : Theme.Surface, selected ? Shade(.14f) : Color.FromArgb(10,20,28), 40);
        using var pen = new Pen(selected ? Theme.Blue : Theme.Border, selected ? 3 : 1.2f);
        g.FillPath(brush,shape);g.DrawPath(pen,shape);
    }
    private void Hit(string id, RectangleF rect, string label, bool enabled = true, int min = 0, int max = 0) => hits.Add(new(id,rect,label,enabled,min,max));
    private void Button(Graphics g, string id, string label, RectangleF rect, bool primary = false, bool enabled = true)
    {
        enabled &= !Host.Busy || id is "close" or "minimize" or "maximize";
        Card(g,rect, primary && enabled,10);
        if (primary && enabled) { using var shape=Theme.Rounded(rect,10); using var brush = new SolidBrush(Color.FromArgb(0,98,198)); g.FillPath(brush,shape); }
        int size=22;while(size>17 && g.MeasureString(label,F(size)).Width > rect.Width-22)size--;
        DrawText(g,label,rect.X+8,rect.Y,size,enabled?Theme.Ink:Color.FromArgb(92,115,138),rect.Width-16,rect.Height,center:true);
        Hit(id,rect,label,enabled);
    }
    private void Field(Graphics g,string id,string value,RectangleF rect,bool enabled=true)
    {
        Card(g,rect,false,10);DrawText(g,value,rect.X+15,rect.Y,21,enabled?Theme.Ink:Theme.Muted,rect.Width-48,rect.Height);
        DrawText(g,"⌄",rect.Right-34,rect.Y-2,24,Theme.Muted,28,rect.Height);Hit(id,rect,value,enabled&&!Host.Busy);
    }
    private void Slider(Graphics g,string id,string label,int value,RectangleF rect,bool enabled=true)
    {
        value=Math.Clamp(value,0,4);
        DrawText(g,label,rect.X,rect.Y-40,21,enabled?Theme.Ink:Theme.Muted,rect.Width,32);
        var track=new RectangleF(rect.X+10,rect.Y+15,rect.Width-20,8);
        using var path=Theme.Rounded(track,4);using var bg=new SolidBrush(Color.FromArgb(31,50,66));g.FillPath(bg,path);
        float x=track.X+track.Width*value/4f;
        if(value>0){using var fill=new SolidBrush(enabled?Theme.Blue:Theme.Border);g.FillRectangle(fill,track.X,track.Y,x-track.X,8);}
        for(int i=0;i<5;i++){using var dot=new SolidBrush(i<=value&&enabled?Theme.Blue:Theme.Border);g.FillEllipse(dot,track.X+i*track.Width/4-3,track.Y+14,6,6);}
        using(var knob=new SolidBrush(enabled?Theme.Blue:Theme.Muted))g.FillEllipse(knob,x-9,track.Y-5,18,18);
        Hit(id,rect,label,enabled&&!Host.Busy,0,4);
    }
    private void Toggle(Graphics g,string id,string label,bool value,RectangleF rect,bool enabled=true)
    {
        DrawText(g,label,rect.X,rect.Y,20,enabled?Theme.Ink:Theme.Muted,rect.Width-64,rect.Height);
        var shape=new RectangleF(rect.Right-56,rect.Y+(rect.Height-26)/2,50,26);
        using var path=Theme.Rounded(shape,13);using var brush=new SolidBrush(value?Color.FromArgb(0,98,198):Theme.Border);g.FillPath(brush,path);
        using var knob=new SolidBrush(value?Theme.Ink:Theme.Muted);g.FillEllipse(knob,shape.X+(value?27:3),shape.Y+3,20,20);
        Hit(id,rect,label,enabled&&!Host.Busy);
    }
    private void Icon(Graphics g,string name,float x,float y,float size,Color? color=null)
    {
        using var pen=new Pen(color??Theme.Muted,2.2f);using var brush=new SolidBrush(color??Theme.Muted);
        if(name=="keyboard")
        {g.DrawRectangle(pen,x,y+size*.15f,size,size*.65f);for(int row=0;row<2;row++)for(int col=0;col<4;col++)g.FillRectangle(brush,x+size*(.12f+col*.2f),y+size*(.28f+row*.2f),size*.1f,size*.08f);g.DrawLine(pen,x+size*.25f,y+size*.7f,x+size*.75f,y+size*.7f);}
        else if(name=="macro")g.DrawLines(pen,new PointF[]{new(x+size*.65f,y),new(x+size*.15f,y+size*.55f),new(x+size*.5f,y+size*.55f),new(x+size*.35f,y+size),new(x+size*.9f,y+size*.4f),new(x+size*.55f,y+size*.4f),new(x+size*.65f,y)});
        else if(name=="update"){g.DrawArc(pen,x,y,size,size,35,285);g.DrawLines(pen,new PointF[]{new(x+size*.73f,y),new(x+size*.94f,y+size*.17f),new(x+size*.97f,y-size*.05f)});g.DrawLine(pen,x+size*.5f,y+size*.24f,x+size*.5f,y+size*.75f);g.DrawLines(pen,new PointF[]{new(x+size*.3f,y+size*.55f),new(x+size*.5f,y+size*.75f),new(x+size*.7f,y+size*.55f)});}
        else if(name=="lock"){g.DrawArc(pen,x+size*.25f,y,size*.5f,size*.6f,180,180);g.DrawRectangle(pen,x+size*.16f,y+size*.35f,size*.68f,size*.6f);}
        else if(name=="settings")
        {var teeth=Enumerable.Range(0,32).Select(i=>{double angle=i*Math.PI/16;double radius=size*(i%4 is 1 or 2?.5:.38);return new PointF(x+size/2+(float)(Math.Cos(angle)*radius),y+size/2+(float)(Math.Sin(angle)*radius));}).ToArray();g.DrawPolygon(pen,teeth);g.DrawEllipse(pen,x+size*.35f,y+size*.35f,size*.3f,size*.3f);}
        else {g.DrawRectangle(pen,x+size*.1f,y+size*.1f,size*.8f,size*.8f);for(int i=0;i<3;i++)g.DrawLine(pen,x+size*.25f,y+size*(.3f+i*.2f),x+size*.75f,y+size*(.3f+i*.2f));}
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.ClearTypeGridFit;
        scale=Math.Min(Width/ReferenceWidth,Height/ReferenceHeight);dx=(Width-ReferenceWidth*scale)/2;dy=(Height-ReferenceHeight*scale)/2;
        g.TranslateTransform(dx,dy);g.ScaleTransform(scale,scale);hits.Clear();
        using(var brush=new SolidBrush(Theme.Background))g.FillRectangle(brush,0,0,ReferenceWidth,ReferenceHeight);
        using(var pen=new Pen(Theme.Border)){g.DrawLine(pen,0,64,1672,64);g.DrawLine(pen,279,64,279,882);g.DrawLine(pen,0,882,1672,882);g.DrawRectangle(pen,1,1,1670,939);}
        Icon(g,"keyboard",24,17,36,Theme.Blue);DrawText(g,"K585 Studio",76,8,25,width:210,bold:true);DrawText(g,"│",288,8,26,Theme.Muted,25);
        DrawText(g,"Redragon K585RGB",328,8,23,Theme.Muted,650);
        DrawText(g,Host.Snapshot==null?"Sin conectar":$"Perfil activo: {Host.Snapshot.ActiveProfile}",1080,10,20,Host.Snapshot==null?Theme.Muted:Color.FromArgb(0,237,111),330);
        Button(g,"minimize","−",new(1450,8,54,45));Button(g,"maximize","□",new(1524,8,54,45));Button(g,"close","×",new(1598,8,54,45));
        var nav=new[]{("profile-1","Perfil 1","keyboard"),("profile-2","Perfil 2","keyboard"),("profile-3","Perfil 3","keyboard"),("profile-4","Perfil 4","keyboard"),("macros","Macros","macro"),("settings","Configuración","settings"),("updates","Actualizaciones","update")};
        for(int i=0;i<nav.Length;i++)
        {
            var (id,name,icon)=nav[i];bool selected=id==(Host.Page==StudioPage.Profile?$"profile-{Host.Profile}":Host.Page.ToString().ToLowerInvariant());
            var rect=new RectangleF(8,85+i*76,262,70);
            if(selected){Card(g,rect,true,12);using var brush=new SolidBrush(Theme.Blue);g.FillRectangle(brush,8,rect.Y+5,5,rect.Height-10);}
            Icon(g,icon,36,rect.Y+18,35,selected?Theme.Ink:Theme.Muted);DrawText(g,name,100,rect.Y,22,selected?Theme.Ink:Theme.Muted,164,rect.Height);
            Hit(id,rect,name,!Host.Busy);
        }
        Card(g,new(20,658,240,174));DrawText(g,"DISPOSITIVO",38,674,17,Theme.Muted,206,28);
        DrawText(g,Host.Snapshot==null?"K585 sin lectura":"Lectura verificada",38,712,21,width:208,height:30);
        DrawText(g,"4 perfiles editables",38,749,18,Theme.Muted,210,26);Button(g,"read","Conectar / leer",new(38,785,204,36),true);
        switch(Host.Page){case StudioPage.Profile:ProfilePage(g);break;case StudioPage.Macros:MacroPage(g);break;case StudioPage.Settings:SettingsPage(g);break;case StudioPage.Updates:UpdatesPage(g);break;}
        using(var brush=new SolidBrush(Host.Busy?Color.FromArgb(246,212,31):Host.Snapshot==null?Theme.Muted:Color.FromArgb(0,237,111)))g.FillEllipse(brush,24,902,18,18);
        DrawText(g,$"K585 Studio v{Updates.Current}  │  "+(Host.Busy?"Operación en curso…":Host.Snapshot==null?"Sin conexión USB":"Configuración leída y respaldada"),60,890,18,Theme.Muted,900,42);
        DrawText(g,$"Editando perfil {Host.Profile}",1290,890,18,Theme.Muted,350,42);
        if(!string.IsNullOrWhiteSpace(Host.Notice))DrawText(g,Host.Notice,NoticeBounds.X,NoticeBounds.Y,18,Host.Error||Host.PendingMacroPreview is {Ready:false}?Theme.Orange:Theme.Muted,NoticeBounds.Width,NoticeBounds.Height);
        if(focusId!=null && hits.FirstOrDefault(h=>h.Id==focusId) is {} focus && Focused){using var pen=new Pen(Theme.Ink,2){DashStyle=DashStyle.Dot};g.DrawRectangle(pen,focus.Bounds.X+3,focus.Bounds.Y+3,focus.Bounds.Width-6,focus.Bounds.Height-6);}
    }
    private void ProfilePage(Graphics g)
    {
        bool editable=true;
        DrawText(g,$"Perfil {Host.Profile}",310,84,34,bold:true,width:550,height:48);
        DrawText(g,"Seleccionarlo lo activa en el teclado · Aplicar guarda los ajustes",310,135,21,Theme.Muted,880,32);
        Card(g,new(304,180,906,425));DrawKeyboard(g,new(326,191,634,402));
        DrawText(g,"K585 RGB",968,210,25,width:218,bold:true,height:36);DrawText(g,"42 teclas",968,257,20,Theme.Muted,215,30);
        DrawText(g,"Naranja",968,323,19,Theme.Orange,215,28);DrawText(g,"Asignación modificada\no cambio pendiente",968,351,18,Theme.Muted,212,72);
        DrawText(g,"M1–M4 y REC\nse conservan como\nbotones del dispositivo.",968,482,18,Theme.Muted,212,88);
        Hit("key-picker",new(326,191,634,402),"Seleccionar una tecla con el teclado",false);
        var light=Host.CurrentLighting;var effect=Effects.All.FirstOrDefault(e=>e.Id==light.Mode);
        bool parameters=editable&&effect!=null;
        Card(g,new(304,624,906,207));DrawText(g,"Iluminación",328,634,23,bold:true,width:500,height:34);
        Field(g,"effect",effect?.ToString()??$"Modo {light.Mode} · sin soporte",new(328,680,430,43),editable);
        Color color=Color.FromArgb(light.R,light.G,light.B);
        Card(g,new(775,680,130,43),false,10);DrawText(g,$"#{light.R:X2}{light.G:X2}{light.B:X2}",809,680,18,parameters&&!light.Colorful?Theme.Ink:Theme.Muted,91,43);
        Hit("color",new(775,680,130,43),"Elegir color fijo",parameters&&!light.Colorful&&!Host.Busy);
        using(var brush=new SolidBrush(color))g.FillEllipse(brush,787,692,16,16);
        Toggle(g,"colorful","Colorful",light.Colorful,new(934,679,250,44),parameters);
        Slider(g,"brightness",light.Brightness<=4?$"Brillo  {light.Brightness}/4":"Brillo · valor sin reconocer",light.Brightness,new(328,775,360,38),parameters);
        Slider(g,"speed",light.Speed<=4?$"Velocidad  {5-light.Speed}/5 · izquierda más lenta":"Velocidad · valor sin reconocer",4-light.Speed,new(766,775,418,38),parameters);
        Card(g,new(1228,180,412,651));
        DrawText(g,"TECLA SELECCIONADA",1252,196,17,Theme.Muted,360,30);DrawText(g,Host.SelectedKey,1252,236,34,width:360,height:48,bold:true);
        DrawText(g,"Asignación en el teclado",1252,301,19,Theme.Muted,360,32);
        Card(g,new(1252,342,364,64));DrawText(g,Host.CurrentAssignment,1266,349,21,width:336,height:50);
        var pending=Host.PendingAssignment;
        DrawText(g,pending==null?"Sin cambios pendientes":"Pendiente: "+pending.Value,1252,419,19,pending==null?Theme.Muted:Theme.Orange,pending==null?360:245,52);
        if(pending!=null)Button(g,"revert-key","Descartar",new(1505,428,111,34),enabled:editable);
        Button(g,"edit-key","Editar asignación",new(1252,483,364,44),true,editable&&!BindingCatalog.Key(Host.SelectedKey).Reserved);
        Button(g,"factory-key","Asignación original de esta tecla",new(1252,539,364,44),enabled:editable&&!BindingCatalog.Key(Host.SelectedKey).Reserved);
        DrawText(g,"Frecuencia USB de este perfil",1252,603,19,Theme.Muted,265,32);
        if(Host.Draft.ReportRates.ContainsKey(Host.Profile))Button(g,"revert-rate","Descartar",new(1510,604,106,32),enabled:editable);
        int? rate=Host.CurrentRate;
        string[] rates=["125 Hz","250 Hz","500 Hz","1 kHz"];
        for(int i=0;i<4;i++)Button(g,"rate-"+i,rates[i],new(1252+i*92,644,84,38),rate==i,editable);
        Button(g,"apply-rate","Aplicar frecuencia",new(1252,696,364,44),enabled:Host.CanWrite&&Host.RatePending);
        Button(g,"apply-key",pending==null?"Aplicar tecla":"Aplicar asignación",new(1252,772,364,44),true,Host.CanWrite&&pending!=null&&Host.PendingMacroPreview?.Ready!=false);
        if(Host.Draft.Lighting.ContainsKey(Host.Profile))Button(g,"revert-light","Descartar iluminación",new(704,842,258,39),enabled:editable);
        Button(g,"apply-light","Aplicar iluminación",new(978,842,232,39),true,Host.CanWrite&&Host.LightPending&&(light.Mode!=23||Host.Draft.RainFirmwareConfirmed));
    }
    private RectangleF KeyboardBounds(RectangleF box)
    {float s=Math.Min(box.Width/keyboard.Width,box.Height/keyboard.Height);return new(box.X+(box.Width-keyboard.Width*s)/2,box.Y+(box.Height-keyboard.Height*s)/2,keyboard.Width*s,keyboard.Height*s);}
    internal static GraphicsPath KeyPath(KeyShape key,RectangleF image,int width,int height)
    {
        GraphicsPath path;
        if(key.Polygon is {} polygon){path=new();path.AddPolygon(polygon);}else path=Theme.Rounded(key.Bounds,9);
        using var matrix=new Matrix(image.Width/width,0,0,image.Height/height,image.X,image.Y);path.Transform(matrix);return path;
    }
    private void DrawKeyboard(Graphics g,RectangleF box)
    {
        var rect=KeyboardBounds(box);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(keyboard,rect);
        foreach(var key in KeyboardView.Keys)
        {
            bool modified=Host.IsModified(key.Name),selected=Host.SelectedKey==key.Name;
            using var path=KeyPath(key,rect,keyboard.Width,keyboard.Height);
            if(modified||selected)
            {
                using var glow=new Pen(Color.FromArgb(65,modified?Theme.Orange:Theme.Blue),8);using var pen=new Pen(modified?Theme.Orange:Theme.Ink,selected?2.4f:1.8f);
                g.DrawPath(glow,path);g.DrawPath(pen,path);
            }
            var bounds=path.GetBounds();Hit("key:"+key.Name,bounds,"Tecla "+key.Name);
        }
    }
    private void MacroPage(Graphics g)
    {
        DrawText(g,"Macros",310,83,34,bold:true,width:800,height:54);DrawText(g,"Biblioteca local · edita copias sin cambiar las macros existentes del teclado",310,130,21,Theme.Muted,1250,32);
        DrawText(g,Host.MacroMemory,310,164,18,Theme.Muted,1310,27);
        Card(g,new(304,195,348,636));Button(g,"macro-new","Nueva macro",new(326,215,303,44),true);
        var macros=Host.Draft.Macros;int visible=Math.Min(6,macros.Count);
        for(int i=0;i<visible;i++)Button(g,"macro-select:"+macros[i].Id,macros[i].Name,new(326,280+i*58,303,46),Host.SelectedMacro==macros[i].Id);
        if(macros.Count==0)DrawText(g,"Crea una macro para\nasignarla a una tecla.",330,300,21,Theme.Muted,295,110);
        if(macros.Count>6)Button(g,"macro-list","Ver todas las macros",new(326,639,303,44));
        Button(g,"macro-device",$"Copiar del teclado ({Host.DeviceMacros.Count})",new(326,710,303,44),enabled:Host.Snapshot!=null&&Host.DeviceMacros.Count>0);
        Button(g,"macro-delete","Eliminar de la biblioteca",new(326,768,303,44),enabled:Host.Macro!=null);
        Card(g,new(674,195,966,636));var macro=Host.Macro;
        DrawText(g,macro?.Name??"Selecciona una macro",698,212,27,width:650,height:44,bold:true);
        if(macro!=null)
        {
            DrawText(g,$"{macro.Steps.Count} pasos  ·  {macro.Cycles} repeticiones",698,263,20,Theme.Muted,650,32);
            Button(g,"macro-edit","Editar / grabar",new(1394,217,222,44),true);
            DrawText(g,"ACCIÓN",704,322,17,Theme.Muted,300,30);DrawText(g,"TECLA / BOTÓN",1010,322,17,Theme.Muted,320,30);DrawText(g,"PAUSA ANTES",1394,322,17,Theme.Muted,220,30);
            int limit=Math.Min(9,macro.Steps.Count);
            for(int i=0;i<limit;i++)
            {
                var step=macro.Steps[i];float y=364+i*37;
                if(i%2==0){using var brush=new SolidBrush(Color.FromArgb(14,30,42));g.FillRectangle(brush,698,y,918,36);}
                DrawText(g,step.Action,710,y,20,width:290,height:35);DrawText(g,step.Value,1010,y,20,width:300,height:35);DrawText(g,step.DelayMs+" ms",1394,y,20,Theme.Muted,220,35);
            }
            if(macro.Steps.Count>9)DrawText(g,$"Y {macro.Steps.Count-9} pasos más en el editor…",710,708,19,Theme.Muted,900,30);
            var preview=Host.MacroPreview!;
            DrawText(g,preview.Message,698,748,20,preview.Ready?Theme.Ink:Theme.Orange,910,68);
        }
        else DrawText(g,"Crea una macro o usa «Copiar del teclado».\nLas copias se editan en la biblioteca local. Leer el teclado\nconserva tus cambios. Asígnalas a una tecla para aplicarlas.",698,330,23,Theme.Muted,900,150);
    }
    private void SettingsPage(Graphics g)
    {
        DrawText(g,"Configuración",310,83,34,bold:true,width:1200,height:54);
        Card(g,new(304,170,650,308));DrawText(g,"Dispositivo y perfiles",328,190,26,bold:true,width:600,height:46);
        DrawText(g,"Perfiles 1–4",328,254,21,width:200,height:40);DrawText(g,"Editables con respaldo y verificación",548,254,20,Color.FromArgb(0,237,111),370,40);
        Toggle(g,"rain-support","Firmware de lluvia instalado",Host.Draft.RainFirmwareConfirmed,new(328,317,602,48));
        DrawText(g,"Actívalo solo en el K585 que ya tiene nuestro modo 23.\nLa aplicación no cambia ni instala firmware.",328,386,20,Theme.Muted,600,65);
        Card(g,new(978,170,662,308));DrawText(g,"Actualizaciones",1002,190,26,bold:true,width:612,height:46);
        Toggle(g,"auto-updates","Buscar al iniciar",Host.Draft.AutoCheckUpdates,new(1002,254,612,50));
        DrawText(g,"Descarga verificada desde GitHub. La instalación\ncomienza únicamente cuando pulses Instalar.",1002,330,21,Theme.Muted,612,90);
        Card(g,new(304,503,1336,328));DrawText(g,"Proyectos y respaldos",328,522,26,bold:true,width:1200,height:46);
        DrawText(g,"La aplicación respalda la configuración completa antes de escribir.\nLos proyectos conservan tus asignaciones, macros y ajustes pendientes.",328,584,22,Theme.Muted,1280,80);
        Button(g,"import","Importar proyecto",new(328,698,260,48));Button(g,"export","Exportar proyecto",new(606,698,260,48));Button(g,"backups","Abrir respaldos",new(884,698,260,48));
        DrawText(g,"Los respaldos pueden contener tus macros personales. Se guardan solo en este equipo.",328,777,19,Theme.Muted,1270,32);
    }
    private void UpdatesPage(Graphics g)
    {
        DrawText(g,"Actualizaciones",310,83,34,bold:true,width:1200,height:54);
        Card(g,new(304,170,1336,330));DrawText(g,"K585 Studio",332,195,29,width:1000,bold:true,height:50);
        DrawText(g,"Versión instalada: "+Updates.Current,332,260,23,Theme.Muted,1000,40);
        DrawText(g,Host.UpdateStatus,332,325,24,Host.Error?Theme.Orange:Theme.Ink,1260,62);
        Button(g,"update-check","Buscar actualizaciones",new(332,424,310,48),true);
        Button(g,"update-download","Descargar",new(660,424,260,48),enabled:Host.AvailableUpdate!=null&&!Host.Busy);
        Button(g,"update-install","Instalar y reiniciar",new(938,424,310,48),true,Host.DownloadedInstaller!=null&&!Host.Busy);
        if(Host.UpdateProgress is {} progress)
        {
            var rect=new RectangleF(332,387,1250,12);using var bg=new SolidBrush(Theme.Border);g.FillRectangle(bg,rect);
            using var fill=new SolidBrush(Theme.Blue);g.FillRectangle(fill,rect.X,rect.Y,rect.Width*progress/100f,rect.Height);
        }
        Card(g,new(304,525,1336,306));DrawText(g,"Versión operativa de escritorio",332,545,26,bold:true,width:1250,height:48);
        DrawText(g,"Iluminación, reasignación de teclas, macros y frecuencia USB en los cuatro perfiles.\nSeleccionar un perfil lo activa. Cada escritura se respalda y se verifica.\nEl instalador es para tu usuario y conserva los proyectos y respaldos.",332,610,23,Theme.Muted,1250,140);
        Button(g,"github","Ver versiones en GitHub",new(332,767,340,44));
    }
    private PointF Reference(Point point) => new((point.X-dx)/scale,(point.Y-dy)/scale);
    internal void Invoke(string id)
    { if(hits.LastOrDefault(h=>h.Id==id) is { Enabled:true }) Command?.Invoke(id); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;Focus();var point=Reference(e.Location);
        var hit=hits.LastOrDefault(h=>h.Enabled&&h.Bounds.Contains(point));
        if(hit==null){if(point.Y<64&&point.X<1430)DragTitle?.Invoke();return;}
        focusId=hit.Id;
        if(hit.Max>hit.Min){dragging=hit.Id;Capture=true;EditSlider(hit,point.X);}else Command?.Invoke(hit.Id);
        Invalidate();
    }
    private void EditSlider(StudioHit hit,float x)
    {var value=(int)Math.Round(Math.Clamp((x-hit.Bounds.X-10)/(hit.Bounds.Width-20),0,1)*(hit.Max-hit.Min))+hit.Min;ValueEdited?.Invoke(hit.Id,value);}
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);var point=Reference(e.Location);
        if(dragging!=null&&hits.FirstOrDefault(h=>h.Id==dragging) is {} hit)EditSlider(hit,point.X);
        Cursor=hits.Any(h=>h.Enabled&&h.Bounds.Contains(point))?Cursors.Hand:Cursors.Default;
    }
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);dragging=null;Capture=false;}
    protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture)dragging=null;}
    protected override bool IsInputKey(Keys keyData) => (keyData&Keys.KeyCode) is Keys.Tab or Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);var enabled=hits.Where(h=>h.Enabled).ToArray();if(enabled.Length==0)return;
        int index=Array.FindIndex(enabled,h=>h.Id==focusId);
        if(e.KeyCode==Keys.Tab){int direction=e.Shift?-1:1;focusId=enabled[(index+direction+enabled.Length)%enabled.Length].Id;e.Handled=true;Invalidate();}
        else if(enabled.FirstOrDefault(h=>h.Id==focusId) is {} hit)
        {
            if(e.KeyCode is Keys.Enter or Keys.Space){Command?.Invoke(hit.Id);e.Handled=true;}
            if(hit.Max>hit.Min&&e.KeyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down)
            {int current=hit.Id=="brightness"?Host.CurrentLighting.Brightness:4-Host.CurrentLighting.Speed;ValueEdited?.Invoke(hit.Id,Math.Clamp(current+(e.KeyCode is Keys.Right or Keys.Up?1:-1),hit.Min,hit.Max));e.Handled=true;}
        }
    }
    protected override AccessibleObject CreateAccessibilityInstance() => new CanvasAccessible(this);
    private sealed class CanvasAccessible(StudioCanvas owner) : ControlAccessibleObject(owner)
    {
        public override int GetChildCount()=>owner.hits.Count;
        public override AccessibleObject? GetChild(int index)=>index>=0&&index<owner.hits.Count?new HitAccessible(owner,owner.hits[index]):null;
    }
    private sealed class HitAccessible(StudioCanvas owner,StudioHit hit) : AccessibleObject
    {
        public override string? Name{get=>hit.Label;set{}}
        public override AccessibleRole Role=>hit.Max>hit.Min?AccessibleRole.Slider:AccessibleRole.PushButton;
        public override AccessibleStates State=>hit.Enabled?AccessibleStates.Focusable:AccessibleStates.Unavailable;
        public override string DefaultAction=>"Activar";
        public override Rectangle Bounds=>owner.RectangleToScreen(new Rectangle((int)(owner.dx+hit.Bounds.X*owner.scale),(int)(owner.dy+hit.Bounds.Y*owner.scale),(int)(hit.Bounds.Width*owner.scale),(int)(hit.Bounds.Height*owner.scale)));
        public override void DoDefaultAction(){if(hit.Enabled)owner.Invoke(hit.Id);}
    }
    protected override void Dispose(bool disposing){if(disposing){keyboard.Dispose();foreach(var font in fonts.Values)font.Dispose();}base.Dispose(disposing);}
}
