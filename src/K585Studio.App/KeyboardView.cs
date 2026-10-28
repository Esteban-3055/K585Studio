using System.Drawing.Drawing2D;

namespace K585Studio.App;

internal sealed record KeyShape(string Name, RectangleF Bounds, PointF[]? Polygon = null);

internal sealed class KeyboardView : Control
{
    private readonly Image image;
    public event Action<string>? KeySelected;
    public string? SelectedKey { get; set; }
    public IReadOnlySet<string> Modified { get; set; } = new HashSet<string>();
    public static readonly KeyShape[] Keys = MakeKeys();
    public KeyboardView()
    {
        DoubleBuffered = true; BackColor = Theme.Background; Cursor = Cursors.Hand; TabStop = true;
        AccessibleName = "Teclado K585: selecciona una tecla con el ratón o usa el selector del editor";
        using var resource = typeof(KeyboardView).Assembly.GetManifestResourceStream("K585Studio.App.Assets.keyboard.png")!;
        using var original = Image.FromStream(resource); image = new Bitmap(original);
    }
    private RectangleF ImageBounds
    {
        get { float scale = Math.Min((Width - 16f) / image.Width, (Height - 12f) / image.Height); return new((Width - image.Width * scale) / 2, 6, image.Width * scale, image.Height * scale); }
    }
    private GraphicsPath Path(KeyShape key)
    {
        GraphicsPath path;
        if (key.Polygon is { } polygon) { path = new GraphicsPath(); path.AddPolygon(polygon); }
        else path = Theme.Rounded(key.Bounds, 9);
        var bounds = ImageBounds;
        using var transform = new Matrix(bounds.Width / image.Width, 0, 0, bounds.Height / image.Height, bounds.X, bounds.Y);
        path.Transform(transform); return path;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(image, ImageBounds);
        foreach (var key in Keys)
        {
            bool modified = Modified.Contains(key.Name), selected = SelectedKey == key.Name;
            if (!modified && !selected) continue;
            using var path = Path(key);
            using var glow = new Pen(Color.FromArgb(75, modified ? Theme.Orange : Theme.Blue), selected ? 10 : 7);
            using var pen = new Pen(modified ? Theme.Orange : Color.White, selected ? 3 : 2.5f);
            e.Graphics.DrawPath(glow, path); e.Graphics.DrawPath(pen, path);
        }
    }
    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        foreach (var key in Keys) { using var path = Path(key); if (path.IsVisible(e.Location)) { SelectedKey = key.Name; Invalidate(); KeySelected?.Invoke(key.Name); break; } }
    }
    protected override void Dispose(bool disposing) { if (disposing) image.Dispose(); base.Dispose(disposing); }

    private static KeyShape[] MakeKeys()
    {
        var keys = new List<KeyShape>();
        void Key(string name, float x, float y, float w, float h) => keys.Add(new(name, new(x, y, w, h)));
        Key("Esc", 282, 215, 96, 82); Key("F1", 520, 215, 94, 82); Key("F2", 631, 215, 94, 82);
        Key("F3", 742, 215, 94, 82); Key("F4", 853, 215, 94, 82); Key("M · dragón", 966, 215, 102, 82);
        Key("~ · G7", 282, 319, 100, 79);
        for (int i = 0; i < 5; i++) Key((i + 1).ToString(), 398 + i * 105, 319, 88, 79);
        Key("6", 924, 319, 131, 79);
        Key("Tab", 282, 416, 153, 79);
        string[] row2 = ["Q", "W", "E", "R"];
        for (int i = 0; i < row2.Length; i++) Key(row2[i], 452 + i * 105, 416, 88, 79);
        Key("T · G6", 877, 416, 178, 79);
        Key("Caps Lock", 282, 514, 178, 80);
        string[] row3 = ["A", "S", "D", "F", "G"];
        for (int i = 0; i < row3.Length; i++) Key(row3[i], 478 + i * 101, 514, i == 4 ? 173 : 88, 80);
        // G has the wider right edge; F precedes it with a normal cap.
        keys[^1] = new("G", new(890, 514, 165, 80));
        Key("Shift", 282, 612, 217, 80);
        string[] row4 = ["Z", "X", "C", "V", "B"];
        for (int i = 0; i < row4.Length; i++) Key(row4[i], 515 + i * 109, 612, i == 4 ? 101 : 91, 80);
        Key("Ctrl", 282, 711, 127, 81); Key("Fn", 425, 711, 114, 81); Key("Alt", 556, 711, 104, 81);
        Key("N", 675, 711, 102, 81); Key("P", 791, 711, 100, 81);
        keys.Add(new("G2", RectangleF.Empty, [new(109, 309), new(172, 248), new(228, 237), new(229, 372), new(113, 372)]));
        keys.Add(new("G3", RectangleF.Empty, [new(115, 388), new(229, 388), new(229, 499), new(124, 499)]));
        keys.Add(new("G4", RectangleF.Empty, [new(130, 514), new(229, 514), new(229, 624), new(140, 624)]));
        keys.Add(new("G5", RectangleF.Empty, [new(145, 639), new(229, 639), new(229, 764), new(154, 732)]));
        keys.Add(new("G1", RectangleF.Empty, [new(1090, 489), new(1194, 531), new(1223, 628), new(1170, 706), new(1090, 701)]));
        keys.Add(new("Espacio", RectangleF.Empty, [new(1072, 741), new(1150, 750), new(1120, 862), new(994, 937), new(950, 871)]));
        return keys.ToArray();
    }
}
