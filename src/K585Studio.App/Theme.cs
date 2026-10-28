using System.Drawing.Drawing2D;

namespace K585Studio.App;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(9, 19, 27), Surface = Color.FromArgb(15, 26, 35),
        Border = Color.FromArgb(43, 62, 77), Ink = Color.FromArgb(230, 240, 255), Muted = Color.FromArgb(167, 192, 218),
        Blue = Color.FromArgb(0, 157, 255), Orange = Color.FromArgb(255, 153, 60);
    public static Label Label(string text, float size = 10, bool bold = false) => new()
    {
        Text = text, ForeColor = Ink, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
        AutoSize = true, MaximumSize = new Size(1000, 0), Margin = new Padding(0, 0, 0, 8), BackColor = Color.Transparent
    };
    public static Button Button(string text, EventHandler? onClick = null, bool primary = false)
    {
        var button = new StudioButton { Text = text, AutoSize = false, Size = new Size(text is "↑" or "↓" ? 44 : 160, 36),
            BackColor = primary ? Blue : Surface, ForeColor = Ink, FlatStyle = FlatStyle.Flat,
            Padding = new Padding(8, 2, 8, 2), Margin = new Padding(0, 4, 8, 4), Cursor = Cursors.Hand };
        button.FlatAppearance.BorderColor = primary ? Blue : Border;
        if (onClick != null) button.Click += onClick;
        return button;
    }
    public static ComboBox Combo(IEnumerable<object> values, int width = 225)
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed, Width = width,
            BackColor = Surface, ForeColor = Ink, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 8, 8) };
        combo.DrawItem += (_, e) =>
        {
            using var brush = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? Color.FromArgb(16, 60, 90) : Surface);
            e.Graphics.FillRectangle(brush, e.Bounds);
            string text = e.Index >= 0 && e.Index < combo.Items.Count ? combo.Items[e.Index]?.ToString() ?? "" : "";
            TextRenderer.DrawText(e.Graphics, text, combo.Font, e.Bounds, combo.Enabled ? Ink : Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            e.DrawFocusRectangle();
        };
        combo.Items.AddRange(values.ToArray());
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        return combo;
    }
    public static FlowLayoutPanel Stack(int padding = 16)
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false, FlowDirection = FlowDirection.TopDown,
            BackColor = Surface, Padding = new Padding(padding) };
        void FitRows()
        {
            foreach (var row in panel.Controls.OfType<FlowLayoutPanel>().Where(r => r.FlowDirection == FlowDirection.LeftToRight))
                row.MaximumSize = new Size(Math.Max(100, panel.ClientSize.Width - panel.Padding.Horizontal - 12), 0);
        }
        panel.SizeChanged += (_, _) => FitRows(); panel.ControlAdded += (_, _) => FitRows();
        return panel;
    }
    public static FlowLayoutPanel Row() => new() { AutoSize = true, WrapContents = true, Margin = Padding.Empty, Padding = Padding.Empty };
    public static TextBox TextBox(int width = 240) => new() { Width = width, BackColor = Background, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 0, 0, 8) };
    public static GraphicsPath Rounded(RectangleF rectangle, float radius = 10)
    {
        var path = new GraphicsPath(); float d = radius * 2;
        path.AddArc(rectangle.X, rectangle.Y, d, d, 180, 90);
        path.AddArc(rectangle.Right - d, rectangle.Y, d, d, 270, 90);
        path.AddArc(rectangle.Right - d, rectangle.Bottom - d, d, d, 0, 90);
        path.AddArc(rectangle.X, rectangle.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
    }
}

internal sealed class StudioButton : Button
{
    private bool hover;
    public StudioButton() => SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new RectangleF(1, 1, Width - 3, Height - 3);
        using var shape = Theme.Rounded(rect, 8 * DeviceDpi / 96f);
        var fill = Enabled ? BackColor : Theme.Surface;
        using var brush = new SolidBrush(hover && Enabled ? ControlPaint.Light(fill, .12f) : fill);
        using var border = new Pen(Enabled ? FlatAppearance.BorderColor : Theme.Border, 1.1f * DeviceDpi / 96f);
        e.Graphics.FillPath(brush, shape); e.Graphics.DrawPath(border, shape);
        TextRenderer.DrawText(e.Graphics, Text, Font, Rectangle.Inflate(ClientRectangle, -6, -3), Enabled ? ForeColor : Theme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5), Theme.Ink, fill);
    }
}
