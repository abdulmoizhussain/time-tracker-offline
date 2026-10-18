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
    // Every pixel is opaque, including the corners, in each ICO frame and the EXE resource.
    graphics.Clear(Color.FromArgb(79, 70, 229)); // App's indigo accent.

    var centerX = size * 0.50f;
    var centerY = size * 0.555f;
    var radius = size * 0.425f;
    var thickness = size * 0.065f;
    using var faceBrush = new SolidBrush(Color.White);
    using var rimBrush = new SolidBrush(Color.FromArgb(30, 27, 75));
    using var tickPen = new Pen(Color.FromArgb(100, 116, 139), size * 0.035f)
    {
        StartCap = System.Drawing.Drawing2D.LineCap.Round,
        EndCap = System.Drawing.Drawing2D.LineCap.Round
    };
    using var hourPen = new Pen(Color.FromArgb(30, 27, 75), thickness)
    {
        StartCap = System.Drawing.Drawing2D.LineCap.Round,
        EndCap = System.Drawing.Drawing2D.LineCap.Round
    };
    using var secondPen = new Pen(Color.FromArgb(79, 70, 229), size * 0.045f)
    {
        StartCap = System.Drawing.Drawing2D.LineCap.Round,
        EndCap = System.Drawing.Drawing2D.LineCap.Round
    };
    // Keep the badge edge-to-edge; a large white dial makes the stopwatch readable at taskbar sizes.
    graphics.FillRectangle(faceBrush, size * 0.455f, size * 0.07f, size * 0.09f, size * 0.09f);
    graphics.FillRectangle(faceBrush, size * 0.36f, size * 0.015f, size * 0.28f, size * 0.065f);
    graphics.FillEllipse(rimBrush, centerX - radius, centerY - radius, radius * 2, radius * 2);
    var faceRadius = radius - size * 0.035f;
    graphics.FillEllipse(faceBrush, centerX - faceRadius, centerY - faceRadius, faceRadius * 2, faceRadius * 2);

    for (var tick = 0; tick < 4; tick++)
    {
        var angle = tick * MathF.PI / 2;
        var inner = faceRadius - size * 0.07f;
        var outer = faceRadius - size * 0.025f;
        graphics.DrawLine(tickPen,
            centerX + MathF.Cos(angle) * inner, centerY + MathF.Sin(angle) * inner,
            centerX + MathF.Cos(angle) * outer, centerY + MathF.Sin(angle) * outer);
    }
    graphics.DrawLine(hourPen, centerX, centerY, size * 0.35f, size * 0.30f);
    graphics.DrawLine(secondPen, centerX, centerY, size * 0.72f, size * 0.34f);
    graphics.FillEllipse(rimBrush, centerX - thickness / 2, centerY - thickness / 2, thickness, thickness);

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

