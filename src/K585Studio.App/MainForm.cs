using System.Diagnostics;
using K585Studio.Core;

namespace K585Studio.App;

internal sealed class MainForm : Form
{
    private readonly Panel content = new() { Dock = DockStyle.Fill, Padding = new Padding(20, 12, 20, 10) };
    private readonly Label connection = Theme.Label("Sin conectar", 10);
    private readonly Label status = Theme.Label("Los cambios se envían solo al pulsar Aplicar.");
    private readonly Button connect;
    private readonly List<Button> navigation = [];
    private readonly CancellationTokenSource lifetime = new();
    private KeyboardSession? session;
    private Snapshot? snapshot;
    private StudioDraft draft = new();
    private int profile = 2;
    private bool busy, closeAfterOperation;
    private readonly Dictionary<int, Lighting> lightDrafts = [];
    private KeyboardView? keyboard;
    private ComboBox? keyPicker;
    private FlowLayoutPanel? inspector;
    private string selectedKey = "W";
    private Button? apply;
    private ComboBox? effect;
    private TrackBar? brightness, speed;
    private CheckBox? colorful;
    private Button? color;
    private Label? effectDescription, brightnessValue, speedValue, lightState;
    private bool populating;
    private ListBox? macroList;
    private DataGridView? macroGrid;
    private TextBox? macroName, capture;
    private NumericUpDown? cycles;
    private CheckBox? recordDelay;
    private bool recording;
    private long lastRecorded;
    private MacroDraft? editingMacro;
    private bool loadingMacro;
    private readonly HashSet<string> recordedKeys = [];
    private readonly bool isolated;

    public MainForm(bool autoConnect = true)
    {
        isolated = !autoConnect;
        // This UI is built dynamically. Scale its pixel geometry once; point-sized fonts already follow monitor DPI.
        AutoScaleMode = AutoScaleMode.None;
        Text = "K585 Studio · v0.1"; StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1400, 900); MinimumSize = new Size(1160, 790);
        BackColor = Theme.Background; ForeColor = Theme.Ink; Font = new Font("Segoe UI", 10);
        DoubleBuffered = true;
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = Theme.Background };
        shell.ColumnStyles.Add(new(SizeType.Absolute, 190)); shell.ColumnStyles.Add(new(SizeType.Percent, 100));
        shell.RowStyles.Add(new(SizeType.Percent, 100)); shell.RowStyles.Add(new(SizeType.Absolute, 37));
        var sidebar = Theme.Stack(12); sidebar.BackColor = Theme.Surface;
        sidebar.Controls.Add(Theme.Label("K585", 25, true));
        sidebar.Controls.Add(Theme.Label("STUDIO  /  v0.1", 9));
        sidebar.Controls.Add(Theme.Label("PERFILES", 9));
        for (int i = 1; i <= 4; i++)
        {
            int p = i;
            var button = Theme.Button($"{i:00}    Perfil {i}" + (i == 1 ? "  🔒" : ""), (_, _) => ShowProfile(p));
            button.Width = 150; button.Height = 46; navigation.Add(button); sidebar.Controls.Add(button);
        }
        sidebar.Controls.Add(Theme.Label("BIBLIOTECA", 9));
        sidebar.Controls.Add(Theme.Button("Macros", (_, _) => ShowMacros()));
        sidebar.Controls.Add(Theme.Button("Actualizaciones", (_, _) => ShowUpdates()));
        sidebar.Controls.Add(Theme.Button("Importar proyecto", (_, _) => ImportProject()));
        sidebar.Controls.Add(Theme.Button("Exportar proyecto", (_, _) => ExportProject()));
        sidebar.Controls.Add(Theme.Label("DISPOSITIVO", 9));
        connection.MaximumSize = new Size(150, 0); connection.ForeColor = Theme.Muted; sidebar.Controls.Add(connection);
        connect = Theme.Button("Conectar / leer", async (_, _) => await ReadKeyboard(), true); sidebar.Controls.Add(connect);
        sidebar.Controls.Add(Theme.Button("Ver respaldos", (_, _) => { Directory.CreateDirectory(Path.Combine(Storage.Root, "Backups")); Open(Path.Combine(Storage.Root, "Backups")); }));
        var protectedText = Theme.Label("Perfil 1 protegido\nSin cambios automáticos", 9); protectedText.ForeColor = Theme.Muted; sidebar.Controls.Add(protectedText);
        foreach (var button in sidebar.Controls.OfType<Button>()) { button.Width = 146; button.Margin = new Padding(0, 4, 0, 4); }
        shell.Controls.Add(sidebar, 0, 0); shell.Controls.Add(content, 1, 0);
        var footer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 7, 8, 0), BackColor = Theme.Surface };
        status.Dock = DockStyle.Fill; status.AutoSize = false; status.ForeColor = Theme.Muted; footer.Controls.Add(status);
        shell.Controls.Add(footer, 0, 1); shell.SetColumnSpan(footer, 2); Controls.Add(shell);
        float dpiScale = DeviceDpi / 96f;
        shell.Scale(new SizeF(dpiScale, dpiScale));
        var working = Screen.FromControl(this).WorkingArea;
        ClientSize = new Size(Math.Min((int)(1400 * dpiScale), working.Width - 80), Math.Min((int)(900 * dpiScale), working.Height - 100));
        MinimumSize = new Size((int)(1160 * dpiScale), (int)(790 * dpiScale));
        if (autoConnect && File.Exists(Storage.DraftFile))
        {
            try { draft = StudioDraft.Load(Storage.DraftFile); }
            catch (Exception ex) { status.Text = "No se cargó el proyecto anterior: " + ex.Message; }
        }
        ShowProfile(2);
        if (autoConnect) Shown += async (_, _) => await ReadKeyboard();
        FormClosing += OnClosing;
    }

    internal int RunUiChecks(string folder)
    {
        // Creates and renders only this application's invisible controls. Never shows or focuses a window,
        // opens HID, records input, or reads/writes the user's project.
        Directory.CreateDirectory(folder);
        var assertions = new List<string>();
        void Assert(bool condition, string text)
        {
            if (!condition) throw new InvalidOperationException("UI check: " + text);
            assertions.Add(text);
        }
        void Render(string name)
        {
            var shell = Controls[0];
            using var host = new Panel { Size = ClientSize, BackColor = Theme.Background };
            Controls.Remove(shell); host.Controls.Add(shell);
            try
            {
                host.CreateControl(); host.PerformLayout();
                using var bitmap = new Bitmap(host.Width, host.Height);
                host.DrawToBitmap(bitmap, new Rectangle(Point.Empty, host.Size));
                bitmap.Save(Path.Combine(folder, name + ".png"));
            }
            finally { host.Controls.Remove(shell); Controls.Add(shell); }
        }
        try
        {
            CreateControl();
            draft = new StudioDraft();
            ShowProfile(2); Render("profile2");
            Assert(keyboard != null && keyboard.Width > 0 && keyboard.Height > 0, "Keyboard has a renderable area");
            Assert(KeyboardView.Keys.Length == 42, "All 42 key regions present");
            Assert(KeyboardView.Keys.Select(k => k.Name).Distinct().Count() == 42, "No duplicate key regions");
            foreach (string key in new[] { "G1", "G2", "G3", "G4", "G5" }) Assert(KeyboardView.Keys.Any(k => k.Name == key), key + " is selectable");
            Assert(apply?.Enabled == false, "Cannot apply before reading a device");
            brightness!.Value = 2; speed!.Value = 3; colorful!.Checked = true;
            Assert(lightDrafts[2].Brightness == 2 && lightDrafts[2].Speed == 1 && lightDrafts[2].Colorful, "Controls modify only a local lighting draft with inverted hardware speed");
            draft.Assign(2, "W", new("Teclado", "X")); ShowProfile(2); Render("profile2-draft");
            Assert(keyboard!.Modified.SetEquals(new[] { "W" }), "Only the changed draft key has an orange overlay");
            ShowProfile(1); Render("profile1");
            Assert(effect?.Enabled == false && brightness?.Enabled == false && speed?.Enabled == false && apply?.Enabled == false, "Profile 1 lighting controls locked");
            Assert(!keyboard!.Modified.Any(), "Profile 1 has no draft overlays");
            Assert(inspector!.Controls.OfType<Button>().All(b => !b.Enabled), "Profile 1 assignment actions locked");
            ShowMacros(); Render("macros-empty");
            Assert(macroGrid != null && macroList != null && draft.Macros.Count == 0, "Empty macro library renders");
            draft.Macros.Add(new MacroDraft { Name = "Test macro", Steps = [new("Tecla ↓", "W", 0), new("Tecla ↑", "W", 150)] });
            ShowMacros(); Render("macros");
            Assert(macroGrid!.Rows.Count == 2, "Macro steps load into editor");
            macroGrid.CurrentCell = macroGrid.Rows[0].Cells[0];
            MoveMacroRow(1); Assert(macroGrid.Rows[0].Cells[0].Value?.ToString() == "Tecla ↑", "Macro row reorder works");
            ShowUpdates(); Render("updates");
            Assert(!recording && session == null, "Headless UI checks never open HID or capture input");
            File.WriteAllText(Path.Combine(folder, "ui-checks.json"), System.Text.Json.JsonSerializer.Serialize(new
            { passed = assertions.Count, dpi = DeviceDpi, width = Width, height = Height, assertions }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(folder, "ui-error.txt"), ex.ToString()); return 1; }
    }

    private void ResetPage()
    {
        StopRecording(); CommitMacro(); editingMacro = null;
        keyboard = null; inspector = null; keyPicker = null; apply = null; effect = null;
        brightness = null; speed = null; colorful = null; color = null; lightState = null;
        macroList = null; macroGrid = null; capture = null;
        foreach (Control child in content.Controls.Cast<Control>().ToArray()) child.Dispose();
        content.Controls.Clear();
    }

    private void ShowProfile(int p)
    {
        if (busy) return;
        profile = p; ResetPage();
        for (int i = 0; i < navigation.Count; i++)
        { navigation[i].BackColor = p == i + 1 ? Color.FromArgb(16, 60, 90) : Theme.Surface; navigation[i].FlatAppearance.BorderColor = p == i + 1 ? Theme.Blue : Theme.Border; }
        var page = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
        page.ColumnStyles.Add(new(SizeType.Percent, 100)); page.ColumnStyles.Add(new(SizeType.Absolute, 290));
        page.RowStyles.Add(new(SizeType.Absolute, 76)); page.RowStyles.Add(new(SizeType.Percent, 100)); page.RowStyles.Add(new(SizeType.Absolute, 246));
        var header = Theme.Stack(0); header.BackColor = Theme.Background; header.AutoScroll = false;
        header.Controls.Add(Theme.Label($"Perfil {p}" + (p == 1 ? " · protegido" : ""), 22, true));
        header.Controls.Add(Theme.Label(p == 1 ? "Solo lectura. Sus teclas, macros e iluminación están bloqueadas." : "Selecciona una tecla para editar su asignación local.", 10));
        page.Controls.Add(header, 0, 0); page.SetColumnSpan(header, 2);
        keyboard = new KeyboardView { Dock = DockStyle.Fill, SelectedKey = selectedKey, Margin = new Padding(0, 0, 14, 12), Modified = draft.Profile(p).Keys.ToHashSet() };
        keyboard.KeySelected += key => { selectedKey = key; if (keyPicker != null) keyPicker.SelectedItem = key; RebuildInspector(); };
        page.Controls.Add(keyboard, 0, 1);
        inspector = Theme.Stack(); inspector.Margin = new Padding(0, 0, 0, 12); page.Controls.Add(inspector, 1, 1);
        var lighting = BuildLighting(); page.Controls.Add(lighting, 0, 2); page.SetColumnSpan(lighting, 2);
        page.Scale(new SizeF(DeviceDpi / 96f, DeviceDpi / 96f));
        content.Controls.Add(page); RebuildInspector(); PopulateLighting();
    }

    private void RebuildInspector()
    {
        if (inspector == null) return;
        foreach (Control child in inspector.Controls.Cast<Control>().ToArray()) child.Dispose();
        inspector.Controls.Clear();
        inspector.Controls.Add(Theme.Label("ASIGNACIÓN", 9));
        keyPicker = Theme.Combo(KeyboardView.Keys.Select(k => (object)k.Name), 240); keyPicker.SelectedItem = selectedKey;
        keyPicker.SelectedIndexChanged += (_, _) =>
        {
            if (keyPicker?.SelectedItem is not string key || key == selectedKey) return;
            selectedKey = key; if (keyboard != null) { keyboard.SelectedKey = key; keyboard.Invalidate(); }
            RebuildInspector();
        };
        inspector.Controls.Add(keyPicker);
        var note = Theme.Label(profile == 1 ? "Este perfil no se puede editar." : "Borrador local · aún no se envía al teclado", 10); note.MaximumSize = new Size(240, 0); note.ForeColor = Theme.Orange; inspector.Controls.Add(note);
        var current = draft.Profile(profile).GetValueOrDefault(selectedKey);
        var category = Theme.Combo(["Teclado", "Ratón", "Multimedia", "Función", "Macro"], 240);
        if (current != null && category.Items.Contains(current.Category)) category.SelectedItem = current.Category;
        inspector.Controls.Add(category);
        var valueHost = Theme.Stack(0); valueHost.Height = 60; valueHost.Width = 240; valueHost.Dock = DockStyle.None;
        inspector.Controls.Add(valueHost);
        Control? editor = null;
        void BuildValue()
        {
            foreach (Control child in valueHost.Controls.Cast<Control>().ToArray()) child.Dispose(); valueHost.Controls.Clear();
            switch (category.Text)
            {
                case "Ratón": editor = Theme.Combo(new object[] { "Botón izquierdo", "Botón derecho", "Botón central", "Doble clic", "Rueda arriba", "Rueda abajo", "Desplazar izquierda", "Desplazar derecha" }); break;
                case "Multimedia": editor = Theme.Combo(Media.Cast<object>()); break;
                case "Función": editor = Theme.Combo(Functions.Cast<object>()); break;
                case "Macro": editor = Theme.Combo(draft.Macros.Select(m => (object)m.Name)); break;
                default:
                    var text = Theme.TextBox(225); text.PlaceholderText = "Pulsa una tecla o combinación";
                    text.KeyDown += (_, e) => { text.Text = e.KeyData.ToString(); e.SuppressKeyPress = true; }; editor = text;
                    break;
            }
            if (editor is ComboBox combo && current != null && combo.Items.Contains(current.Value)) combo.SelectedItem = current.Value;
            if (editor is TextBox box && current != null) box.Text = current.Value;
            valueHost.Controls.Add(editor);
            if (category.Text == "Macro" && draft.Macros.Count == 0) valueHost.Controls.Add(Theme.Label("Crea una macro en la biblioteca.", 9));
        }
        category.SelectedIndexChanged += (_, _) => BuildValue(); BuildValue();
        var save = Theme.Button("Guardar borrador", (_, _) =>
        {
            if (editor == null || string.IsNullOrWhiteSpace(editor.Text)) { status.Text = "Elige una asignación antes de guardar."; return; }
            draft.Assign(profile, selectedKey, new(category.Text, editor.Text)); SaveDraft();
            if (keyboard != null) { keyboard.Modified = draft.Profile(profile).Keys.ToHashSet(); keyboard.Invalidate(); }
            status.Text = $"{selectedKey}: borrador guardado. El borde naranja indica un cambio local pendiente.";
        }, true);
        save.Enabled = profile != 1; category.Enabled = profile != 1; valueHost.Enabled = profile != 1; inspector.Controls.Add(save);
        var clear = Theme.Button("Quitar borrador", (_, _) => { draft.Assign(profile, selectedKey, null); SaveDraft(); if (keyboard != null) { keyboard.Modified = draft.Profile(profile).Keys.ToHashSet(); keyboard.Invalidate(); } RebuildInspector(); });
        clear.Enabled = profile != 1 && current != null; inspector.Controls.Add(clear);
        var detail = Theme.Label("La lectura de bindeos se respalda completa. Su correspondencia con cada tecla aún no está validada para escribirla.", 9);
        detail.MaximumSize = new Size(240, 0); detail.ForeColor = Theme.Muted; inspector.Controls.Add(detail);
        if (selectedKey is "T · G6" or "~ · G7" or "M · dragón")
        { var alias = Theme.Label("El original identifica esta tecla como " + (selectedKey == "T · G6" ? "G6." : selectedKey == "~ · G7" ? "G7." : "M."), 9); alias.MaximumSize = new Size(240, 0); inspector.Controls.Add(alias); }
        // The inspector is rebuilt after the page's initial scaling.
        foreach (Control control in inspector.Controls) control.Scale(new SizeF(DeviceDpi / 96f, DeviceDpi / 96f));
    }

    private Control BuildLighting()
    {
        var panel = Theme.Stack(14); panel.Margin = Padding.Empty;
        var title = Theme.Row(); title.Controls.Add(Theme.Label("ILUMINACIÓN", 10, true));
        lightState = Theme.Label("", 9); lightState.Margin = new Padding(18, 0, 0, 0); lightState.ForeColor = Theme.Orange; title.Controls.Add(lightState); panel.Controls.Add(title);
        var row = Theme.Row();
        effect = Theme.Combo(Effects.All.Cast<object>(), 290); effect.SelectedIndexChanged += (_, _) => CaptureLighting(); row.Controls.Add(effect);
        var rate = Theme.Combo(["125 Hz", "250 Hz", "500 Hz", "1000 Hz"], 125); rate.SelectedIndex = -1; rate.Enabled = false; rate.AccessibleName = "Frecuencia USB global: escritura pendiente de validación";
        row.Controls.Add(Theme.Label("USB global", 9)); row.Controls.Add(rate); row.Controls.Add(Theme.Label("Lectura/edición de Hz pendiente", 9)); panel.Controls.Add(row);
        effectDescription = Theme.Label("", 9); effectDescription.MaximumSize = new Size(950, 0); effectDescription.ForeColor = Theme.Muted; panel.Controls.Add(effectDescription);
        var sliders = Theme.Row();
        brightnessValue = Theme.Label("Brillo", 9); sliders.Controls.Add(brightnessValue);
        brightness = new TrackBar { Minimum = 0, Maximum = 4, TickFrequency = 1, SmallChange = 1, LargeChange = 1, Width = 160, Height = 35, AutoSize = false, BackColor = Theme.Surface, AccessibleName = "Brillo, cinco niveles reales" };
        brightness.ValueChanged += (_, _) => CaptureLighting(); sliders.Controls.Add(brightness);
        speedValue = Theme.Label("Velocidad", 9); sliders.Controls.Add(speedValue);
        speed = new TrackBar { Minimum = 0, Maximum = 4, TickFrequency = 1, SmallChange = 1, LargeChange = 1, Width = 160, Height = 35, AutoSize = false, BackColor = Theme.Surface, AccessibleName = "Velocidad: izquierda lenta, derecha rápida" };
        speed.ValueChanged += (_, _) => CaptureLighting(); sliders.Controls.Add(speed);
        colorful = new CheckBox { Text = "Colorful", AutoSize = true, ForeColor = Theme.Ink, Margin = new Padding(8, 0, 8, 0) }; colorful.CheckedChanged += (_, _) => CaptureLighting(); sliders.Controls.Add(colorful);
        color = Theme.Button("Color fijo", (_, _) =>
        {
            using var dialog = new ColorDialog { Color = color!.BackColor, FullOpen = true };
            if (dialog.ShowDialog(this) == DialogResult.OK) { color.BackColor = dialog.Color; CaptureLighting(); }
        }); sliders.Controls.Add(color); panel.Controls.Add(sliders);
        var actions = Theme.Row();
        apply = Theme.Button("Aplicar iluminación", async (_, _) => await ApplyLighting(), true); actions.Controls.Add(apply);
        actions.Controls.Add(Theme.Button("Descartar ajustes", (_, _) => { lightDrafts.Remove(profile); PopulateLighting(); }));
        var confirmed = new CheckBox { Text = "Firmware de lluvia instalado", ForeColor = Theme.Ink, Checked = draft.RainFirmwareConfirmed, AutoSize = true, Margin = new Padding(8, 10, 0, 0), Enabled = profile != 1 };
        confirmed.CheckedChanged += (_, _) => { draft.RainFirmwareConfirmed = confirmed.Checked; SaveDraft(); UpdateApplyState(); }; actions.Controls.Add(confirmed); panel.Controls.Add(actions);
        return panel;
    }

    private Lighting LightingFor(int p) => lightDrafts.GetValueOrDefault(p) ?? snapshot?.Profile(p) ?? Lighting.Default;
    private void PopulateLighting()
    {
        if (effect == null || brightness == null || speed == null || colorful == null || color == null) return;
        populating = true;
        var light = LightingFor(profile);
        effect.SelectedItem = Effects.All.FirstOrDefault(e => e.Id == light.Mode);
        brightness.Value = Math.Min(4, (int)light.Brightness); speed.Value = 4 - Math.Min(4, (int)light.Speed);
        colorful.Checked = light.Colorful; color.BackColor = Color.FromArgb(light.R, light.G, light.B);
        populating = false; UpdateLightingLabels(); UpdateApplyState();
    }

    private void CaptureLighting()
    {
        if (populating || effect?.SelectedItem is not Effect selected || brightness == null || speed == null || colorful == null || color == null) return;
        var current = LightingFor(profile);
        lightDrafts[profile] = current with { Mode = selected.Id, Brightness = (byte)brightness.Value, Speed = (byte)(4 - speed.Value), Colorful = colorful.Checked, R = color.BackColor.R, G = color.BackColor.G, B = color.BackColor.B };
        UpdateLightingLabels(); UpdateApplyState();
    }

    private void UpdateLightingLabels()
    {
        if (effectDescription == null || brightnessValue == null || speedValue == null || brightness == null || speed == null || color == null || colorful == null) return;
        effectDescription.Text = effect?.SelectedItem is Effect e ? e.Description : $"Modo leído: {LightingFor(profile).Mode}. Sin soporte de edición en esta versión.";
        brightnessValue.Text = $"Brillo {brightness.Value}/4"; speedValue.Text = $"Velocidad {speed.Value + 1}/5";
        color.Text = $"#{color.BackColor.R:X2}{color.BackColor.G:X2}{color.BackColor.B:X2}";
        color.ForeColor = color.BackColor.GetBrightness() > .55f ? Theme.Background : Color.White;
        bool rain = effect?.SelectedItem is Effect { Id: 23 };
        // Only the rain effect has a physically verified parameter contract in this release.
        bool editable = profile != 1 && rain;
        brightness.Enabled = editable; speed.Enabled = editable; colorful.Enabled = editable; color.Enabled = editable && !colorful.Checked;
        effect!.Enabled = profile != 1;
    }

    private void UpdateApplyState()
    {
        if (apply == null || lightState == null) return;
        bool pending = snapshot != null && LightingFor(profile) != snapshot.Profile(profile);
        lightState.Text = profile == 1 ? "Protegido" : profile != 2 ? "Vista y borrador · escritura solo en perfil 2" : snapshot == null ? "Conecta para leer el estado real" : pending ? "Cambios sin aplicar" : "Coincide con la última lectura";
        apply.Enabled = !busy && profile == 2 && snapshot?.ActiveProfile == 2 && session != null && !session.WriteFaulted && pending &&
            effect?.SelectedItem != null && (LightingFor(2).Mode != 23 || draft.RainFirmwareConfirmed);
    }

    private async Task ReadKeyboard()
    {
        await RunOperation(async () =>
        {
            session?.Dispose(); session = null; snapshot = null;
            status.Text = "Leyendo dos veces la configuración completa y guardando un respaldo…";
            session = await Task.Run(() => new KeyboardSession(NativeHid.Open(), Storage.Backup));
            snapshot = await Task.Run(() => session.RefreshAsync(lifetime.Token));
            lightDrafts.Clear();
            connection.Text = $"K585 conectado\nPerfil activo: {snapshot.ActiveProfile}\nLectura doble verificada";
            status.Text = "Lectura verificada y respaldo guardado. Perfil 1, bindeos y macros sin cambios.";
            PopulateLighting();
        });
    }

    private async Task ApplyLighting()
    {
        if (session == null || profile != 2) return;
        var value = LightingFor(2);
        await RunOperation(async () =>
        {
            NativeHid.RequireOtherAppsClosed();
            status.Text = "Respaldando, aplicando al perfil 2 y verificando todos los bloques…";
            snapshot = await Task.Run(() => session.ApplyLightingAsync(2, value, draft.RainFirmwareConfirmed, lifetime.Token));
            lightDrafts.Remove(2); PopulateLighting();
            status.Text = "Iluminación aplicada y leída de vuelta. Perfil 1, bindeos, macros y demás perfiles intactos.";
        });
    }

    private async Task RunOperation(Func<Task> operation)
    {
        if (busy) return;
        busy = true; connect.Enabled = false; content.Enabled = false;
        try { await operation(); }
        catch (OperationCanceledException) { status.Text = "Operación interrumpida. Vuelve a leer antes de aplicar cambios."; }
        catch (Exception ex)
        {
            status.Text = ex.Message;
            if (snapshot == null) connection.Text = "Sin lectura válida";
            MessageBox.Show(this, ex.Message, "K585 Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        finally
        {
            busy = false; connect.Enabled = true; content.Enabled = true; UpdateApplyState();
            if (closeAfterOperation) Close();
        }
    }

    private void SaveDraft()
    {
        if (isolated) return;
        try { draft.Save(Storage.DraftFile); }
        catch (Exception ex) { status.Text = "No se guardó el borrador: " + ex.Message; }
    }
    private void ExportProject()
    {
        if (busy) return; StopRecording(); CommitMacro();
        using var dialog = new SaveFileDialog { Filter = "Proyecto K585 Studio|*.k585.json", FileName = "K585-proyecto.k585.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { draft.Save(dialog.FileName); status.Text = "Proyecto exportado. No contiene el respaldo de bindeos del dispositivo."; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message); }
    }
    private void ImportProject()
    {
        if (busy) return;
        using var dialog = new OpenFileDialog { Filter = "Proyecto K585 Studio|*.k585.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { var imported = StudioDraft.Load(dialog.FileName); StopRecording(); draft = imported; SaveDraft(); ShowProfile(profile); status.Text = "Proyecto importado como borrador local. No se escribió en el teclado."; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message); }
    }

    private static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    private void OnClosing(object? sender, FormClosingEventArgs e)
    {
        StopRecording(); CommitMacro();
        if (busy) { e.Cancel = true; closeAfterOperation = true; status.Text = "Terminando la operación USB antes de cerrar…"; return; }
        lifetime.Cancel(); session?.Dispose(); lifetime.Dispose();
    }

    private static readonly string[] Media = ["Pista anterior", "Pista siguiente", "Silenciar", "Calculadora", "Reproducir / pausa", "Detener", "Volumen −", "Volumen +", "Web: inicio", "Web: buscar", "Web: favoritos", "Web: actualizar", "Web: detener", "Web: adelante", "Web: atrás", "Mi PC", "Correo", "Reproductor multimedia", "Apagar", "Suspender", "Despertar"];
    private static readonly string[] Functions = ["Abrir", "Nuevo", "Deshacer", "Guardar", "Copiar", "Cortar", "Pegar", "Buscar", "Seleccionar todo", "Zoom +", "Zoom −", "Minimizar", "Maximizar", "Word", "Excel", "PowerPoint", "Calendario"];

    private void ShowUpdates()
    {
        if (busy) return; ResetPage();
        var page = Theme.Stack(24); page.BackColor = Theme.Background;
        page.Controls.Add(Theme.Label("Actualizaciones", 24, true));
        page.Controls.Add(Theme.Label("K585 Studio  v0.1.0", 13));
        var result = Theme.Label("Consulta las versiones estables publicadas en GitHub.", 11); result.MaximumSize = new Size(750, 0); page.Controls.Add(result);
        Uri? release = null;
        var open = Theme.Button("Abrir versión en GitHub", (_, _) => { if (release != null) Open(release.AbsoluteUri); }); open.Enabled = false;
        var check = Theme.Button("Buscar actualizaciones", async (_, _) =>
        {
            await RunOperation(async () =>
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                var update = await Updates.CheckAsync(client, lifetime.Token);
                release = update.Release; result.Text = update.Message; open.Enabled = release != null;
            });
        }, true);
        page.Controls.Add(check); page.Controls.Add(open);
        var info = Theme.Label("En v0.1 la instalación es manual desde Releases. El reemplazo automático de la aplicación queda para una versión posterior. Buscar actualizaciones no descarga ni ejecuta archivos.", 10);
        info.MaximumSize = new Size(750, 0); info.ForeColor = Theme.Muted; page.Controls.Add(info);
        page.Controls.Add(Theme.Button("Ver repositorio", (_, _) => Open("https://github.com/" + Updates.Repository)));
        page.Controls.Add(Theme.Label("Funciones habilitadas", 15, true));
        var scope = Theme.Label("• Lectura doble y respaldos completos del dispositivo.\n• Iluminación del perfil 2 y lluvia Serpenteo.\n• Cuatro perfiles con editor visual de borradores.\n• Macros locales e importación/exportación del proyecto.\n• Perfil 1 bloqueado. Bindeos, macros y Hz no se escriben en v0.1.", 11);
        scope.MaximumSize = new Size(800, 0); page.Controls.Add(scope); page.Scale(new SizeF(DeviceDpi / 96f, DeviceDpi / 96f)); content.Controls.Add(page);
    }

    private void ShowMacros()
    {
        if (busy) return; ResetPage();
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        layout.ColumnStyles.Add(new(SizeType.Absolute, 245)); layout.ColumnStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.Absolute, 82)); layout.RowStyles.Add(new(SizeType.Percent, 100));
        var header = Theme.Stack(0); header.BackColor = Theme.Background;
        header.Controls.Add(Theme.Label("Macros", 24, true)); header.Controls.Add(Theme.Label("Biblioteca local · no modifica las macros guardadas en el teclado", 10));
        layout.Controls.Add(header, 0, 0); layout.SetColumnSpan(header, 2);
        var left = Theme.Stack(12);
        macroList = new ListBox { Width = 215, Height = 300, BackColor = Theme.Background, ForeColor = Theme.Ink, BorderStyle = BorderStyle.None, IntegralHeight = false };
        foreach (var macro in draft.Macros) macroList.Items.Add(macro);
        macroList.SelectedIndexChanged += (_, _) => LoadMacro(); left.Controls.Add(macroList);
        left.Controls.Add(Theme.Button("Nueva macro", (_, _) =>
        {
            StopRecording(); CommitMacro(); int n = 1; while (draft.Macros.Any(m => m.Name.Equals("Macro " + n, StringComparison.OrdinalIgnoreCase))) n++;
            var macro = new MacroDraft { Name = "Macro " + n }; draft.Macros.Add(macro); macroList.Items.Add(macro); macroList.SelectedItem = macro; SaveDraft();
        }, true));
        left.Controls.Add(Theme.Button("Eliminar macro local", (_, _) =>
        {
            if (macroList.SelectedItem is not MacroDraft macro) return; StopRecording();
            if (draft.Assignments.Values.Any(map => map.Values.Any(a => a.Category == "Macro" && a.Value == macro.Name))) { status.Text = "Quita primero las asignaciones locales de esta macro."; return; }
            editingMacro = null; draft.Macros.Remove(macro); macroList.Items.Remove(macro); SaveDraft(); LoadMacro();
        }));
        layout.Controls.Add(left, 0, 1);
        var right = Theme.Stack(16); right.Margin = new Padding(12, 0, 0, 0);
        var nameRow = Theme.Row(); macroName = Theme.TextBox(260); nameRow.Controls.Add(macroName);
        cycles = new NumericUpDown { Minimum = 1, Maximum = 999, Width = 72, BackColor = Theme.Background, ForeColor = Theme.Ink };
        nameRow.Controls.Add(Theme.Label("Repeticiones")); nameRow.Controls.Add(cycles); right.Controls.Add(nameRow);
        macroGrid = new DataGridView { Width = 690, Height = 290, BackgroundColor = Theme.Background, BorderStyle = BorderStyle.None,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, EnableHeadersVisualStyles = false };
        macroGrid.DefaultCellStyle.BackColor = Theme.Surface; macroGrid.DefaultCellStyle.ForeColor = Theme.Ink;
        macroGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(16, 60, 90); macroGrid.DefaultCellStyle.SelectionForeColor = Theme.Ink;
        macroGrid.ColumnHeadersDefaultCellStyle.BackColor = Theme.Border; macroGrid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Ink;
        var actionCol = new DataGridViewComboBoxColumn { HeaderText = "Acción", Name = "Action", FlatStyle = FlatStyle.Flat };
        actionCol.Items.AddRange("Tecla ↓", "Tecla ↑", "Ratón ↓", "Ratón ↑", "Rueda", "Pausa"); macroGrid.Columns.Add(actionCol);
        macroGrid.Columns.Add("Value", "Tecla / botón"); macroGrid.Columns.Add("Delay", "Pausa antes (ms)");
        macroGrid.DataError += (_, e) => { e.ThrowException = false; status.Text = "Selecciona una acción de la lista."; };
        right.Controls.Add(macroGrid);
        var actions = Theme.Row();
        actions.Controls.Add(Theme.Button("Añadir", (_, _) => { if (macroList.SelectedItem != null) macroGrid.Rows.Add("Pausa", "", "100"); }));
        actions.Controls.Add(Theme.Button("↑", (_, _) => MoveMacroRow(-1)));
        actions.Controls.Add(Theme.Button("↓", (_, _) => MoveMacroRow(1)));
        actions.Controls.Add(Theme.Button("Al inicio", (_, _) => MoveMacroRow(-9999)));
        actions.Controls.Add(Theme.Button("Al final", (_, _) => MoveMacroRow(9999)));
        actions.Controls.Add(Theme.Button("Quitar paso", (_, _) => { if (macroGrid.CurrentRow != null) macroGrid.Rows.Remove(macroGrid.CurrentRow); }));
        actions.Controls.Add(Theme.Button("Vaciar pasos", (_, _) => macroGrid.Rows.Clear())); right.Controls.Add(actions);
        capture = Theme.TextBox(650); capture.ReadOnly = true; capture.Height = 38; capture.PlaceholderText = "Zona de grabación: escribe aquí después de pulsar Grabar";
        capture.KeyDown += (_, e) => { if (recording) { if (recordedKeys.Add(e.KeyCode.ToString())) AddRecorded("Tecla ↓", e.KeyCode.ToString()); e.Handled = true; } };
        capture.KeyPress += (_, e) => e.Handled = recording;
        capture.KeyUp += (_, e) => { if (recording) { if (recordedKeys.Remove(e.KeyCode.ToString())) AddRecorded("Tecla ↑", e.KeyCode.ToString()); e.Handled = true; } };
        capture.MouseDown += (_, e) => { if (recording) AddRecorded("Ratón ↓", e.Button.ToString()); };
        capture.MouseUp += (_, e) => { if (recording) AddRecorded("Ratón ↑", e.Button.ToString()); };
        capture.LostFocus += (_, _) => StopRecording(); right.Controls.Add(capture);
        var recordingRow = Theme.Row();
        recordingRow.Controls.Add(Theme.Button("Grabar aquí", (_, _) => { if (macroList.SelectedItem == null) { status.Text = "Crea o selecciona una macro."; return; } recording = true; lastRecorded = Environment.TickCount64; capture.Focus(); status.Text = "Grabando solo dentro de la zona de grabación. Salir de ella detiene la captura."; }));
        recordingRow.Controls.Add(Theme.Button("Detener", (_, _) => StopRecording()));
        recordDelay = new CheckBox { Text = "Grabar pausas", ForeColor = Theme.Ink, Checked = true, AutoSize = true }; recordingRow.Controls.Add(recordDelay);
        recordingRow.Controls.Add(Theme.Button("Guardar macro local", (_, _) => { StopRecording(); CommitMacro(); }, true)); right.Controls.Add(recordingRow);
        var explain = Theme.Label("Los límites anteriores son del proyecto local. El espacio disponible en el teclado y la escritura de macros aún no están validados en Studio.", 9); explain.MaximumSize = new Size(650, 0); explain.ForeColor = Theme.Muted; right.Controls.Add(explain);
        right.SizeChanged += (_, _) => { macroGrid.Width = Math.Max(260, right.ClientSize.Width - 42); capture.Width = macroGrid.Width; };
        layout.Controls.Add(right, 1, 1); layout.Scale(new SizeF(DeviceDpi / 96f, DeviceDpi / 96f)); content.Controls.Add(layout);
        if (macroList.Items.Count > 0) macroList.SelectedIndex = 0; else LoadMacro();
    }

    private void LoadMacro()
    {
        if (loadingMacro) return;
        StopRecording(); CommitMacro(); if (macroGrid == null || macroName == null || cycles == null) return;
        macroGrid.Rows.Clear(); var macro = macroList?.SelectedItem as MacroDraft;
        editingMacro = macro;
        macroName.Text = macro?.Name ?? ""; cycles.Value = macro?.Cycles ?? 1;
        macroName.Enabled = cycles.Enabled = macroGrid.Enabled = macro != null;
        if (macro != null) foreach (var step in macro.Steps) macroGrid.Rows.Add(step.Action, step.Value, step.DelayMs.ToString());
    }
    private void CommitMacro()
    {
        if (editingMacro is not MacroDraft macro || macroList == null || macroGrid == null || macroName == null || cycles == null) return;
        try
        {
            macroGrid.EndEdit(); string name = macroName.Text.Trim();
            if (name.Length is < 1 or > 100 || draft.Macros.Any(m => m != macro && m.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("El nombre de macro está vacío o repetido.");
            var steps = new List<MacroStep>();
            foreach (DataGridViewRow row in macroGrid.Rows)
            {
                if (!int.TryParse(row.Cells[2].Value?.ToString(), out int delay) || delay is < 0 or > 60000) throw new InvalidDataException("Las pausas deben estar entre 0 y 60000 ms.");
                steps.Add(new(row.Cells[0].Value?.ToString() ?? "Pausa", row.Cells[1].Value?.ToString() ?? "", delay));
            }
            if (steps.Count > 2000) throw new InvalidDataException("La macro supera 2000 pasos locales.");
            var old = macro.Name; macro.Name = name; macro.Cycles = (int)cycles.Value; macro.Steps = steps;
            foreach (var map in draft.Assignments.Values) foreach (var key in map.Keys.ToArray()) if (map[key] is { Category: "Macro" } a && a.Value == old) map[key] = a with { Value = name };
            int index = macroList.Items.IndexOf(macro);
            loadingMacro = true;
            try { if (index >= 0) macroList.Items[index] = macro; }
            finally { loadingMacro = false; }
            SaveDraft(); status.Text = "Macro guardada en la biblioteca local.";
        }
        catch (Exception ex) { status.Text = "No se guardó la macro: " + ex.Message; }
    }
    private void MoveMacroRow(int delta)
    {
        if (macroGrid?.CurrentRow == null) return;
        int from = macroGrid.CurrentRow.Index, to = Math.Clamp(from + delta, 0, macroGrid.Rows.Count - 1);
        if (from == to) return;
        var row = macroGrid.Rows[from]; macroGrid.Rows.RemoveAt(from); macroGrid.Rows.Insert(to, row); macroGrid.CurrentCell = row.Cells[0];
    }
    private void AddRecorded(string action, string value)
    {
        if (macroGrid == null || macroGrid.Rows.Count >= 1980) { StopRecording(); return; }
        long now = Environment.TickCount64;
        int delay = recordDelay?.Checked == true ? (int)Math.Clamp(now - lastRecorded, 0, 60000) : 0;
        macroGrid.Rows.Add(action, value, delay.ToString()); lastRecorded = now;
    }
    private void StopRecording()
    {
        if (!recording) return;
        recording = false;
        foreach (var key in recordedKeys) macroGrid?.Rows.Add("Tecla ↑", key, "0");
        recordedKeys.Clear();
        status.Text = "Grabación detenida. Guarda la macro local para conservarla.";
    }
}
