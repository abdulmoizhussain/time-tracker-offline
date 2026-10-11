using System.Drawing;
using System.Drawing.Imaging;

var outputPath = args.Length > 0 ? args[0] : GetDefaultOutputPath();
var directory = Path.GetDirectoryName(outputPath);
if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
{
    Directory.CreateDirectory(directory);
}

var sizes = new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
var images = new List<Bitmap>();

foreach (var size in sizes)
{
    images.Add(DrawStopwatch(size));
}

WriteIco(outputPath, images);
Console.WriteLine($"Icon generated: {outputPath}");

static string GetDefaultOutputPath()
{
    var baseDir = AppContext.BaseDirectory;
    var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "TimeTracker.App", "Assets"));
    return Path.Combine(projectRoot, "TimeTracker.ico");
}

static Bitmap DrawStopwatch(int size)
{
    var bitmap = new Bitmap(size, size);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
    graphics.Clear(Color.Transparent);

    var centerX = size / 2f;
    var centerY = size / 2f;
    var radius = size * 0.35f;
    var thickness = Math.Max(2f, size / 12f);

    using var darkBrush = new SolidBrush(Color.FromArgb(33, 33, 33));
    using var accentBrush = new SolidBrush(Color.FromArgb(0, 150, 200));
    using var pen = new Pen(darkBrush, thickness);
    using var accentPen = new Pen(accentBrush, thickness);

    graphics.DrawEllipse(pen, centerX - radius, centerY - radius, radius * 2, radius * 2);

    var buttonSize = Math.Max(3, size / 10f);
    graphics.FillRectangle(darkBrush, centerX - buttonSize / 2, centerY - radius - buttonSize * 1.5f, buttonSize, buttonSize * 1.2f);
    graphics.FillRectangle(darkBrush, centerX + radius * 0.5f - buttonSize / 2, centerY - radius * 0.9f - buttonSize / 2, buttonSize * 1.2f, buttonSize);

    var angle = -Math.PI / 6;
    var handLength = radius * 0.7f;
    var endX = centerX + (float)(Math.Cos(angle) * handLength);
    var endY = centerY + (float)(Math.Sin(angle) * handLength);
    graphics.DrawLine(accentPen, centerX, centerY, endX, endY);

    graphics.FillEllipse(darkBrush, centerX - thickness / 2, centerY - thickness / 2, thickness, thickness);

    return bitmap;
}

static void WriteIco(string path, List<Bitmap> images)
{
    using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
    using var writer = new BinaryWriter(stream);

    writer.Write((short)0);
    writer.Write((short)1);
    writer.Write((short)images.Count);

    var offset = 6 + images.Count * 16;
    var pngData = new List<byte[]>();

    foreach (var image in images)
    {
        using var ms = new MemoryStream();
        image.Save(ms, ImageFormat.Png);
        var data = ms.ToArray();
        pngData.Add(data);

        writer.Write((byte)image.Width);
        writer.Write((byte)image.Height);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((short)1);
        writer.Write((short)32);
        writer.Write(data.Length);
        writer.Write(offset);

        offset += data.Length;
    }

    foreach (var data in pngData)
    {
        writer.Write(data);
    }
}

