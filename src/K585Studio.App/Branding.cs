namespace K585Studio.App;

internal static class Branding
{
    internal static Icon LoadIcon()
    {
        using var stream = typeof(Branding).Assembly.GetManifestResourceStream("K585Studio.App.Assets.app.ico")
            ?? throw new InvalidDataException("Falta el icono de K585 Studio.");
        using var source = new Icon(stream, 32, 32);
        return (Icon)source.Clone();
    }

    internal static Image LoadImage()
    {
        using var stream = typeof(Branding).Assembly.GetManifestResourceStream("K585Studio.App.Assets.app-icon.png")
            ?? throw new InvalidDataException("Falta la imagen de K585 Studio.");
        using var source = Image.FromStream(stream);
        return new Bitmap(source);
    }
}
