using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

var output = args.Length > 0 ? args[0] : Path.Combine("..", "..", "src", "app.ico");
SaveIcon(output, 16, 32, 48, 256);
Console.WriteLine($"Created {output}");

static void SaveIcon(string path, params int[] sizes)
{
    var images = sizes.Select(CreateBitmap).ToList();
    var directory = Path.GetDirectoryName(path);
    if (!string.IsNullOrWhiteSpace(directory))
    {
        Directory.CreateDirectory(directory);
    }

    using var stream = File.Create(path);
    using var writer = new BinaryWriter(stream);
    writer.Write((ushort)0);
    writer.Write((ushort)1);
    writer.Write((ushort)images.Count);

    var offset = 6 + 16 * images.Count;
    var pngData = new List<byte[]>();
    foreach (var image in images)
    {
        using var ms = new MemoryStream();
        image.Save(ms, ImageFormat.Png);
        pngData.Add(ms.ToArray());
    }

    for (var i = 0; i < images.Count; i++)
    {
        var size = images[i].Width;
        writer.Write((byte)(size >= 256 ? 0 : size));
        writer.Write((byte)(size >= 256 ? 0 : size));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write((uint)pngData[i].Length);
        writer.Write((uint)offset);
        offset += pngData[i].Length;
    }

    foreach (var data in pngData)
    {
        writer.Write(data);
    }

    foreach (var image in images)
    {
        image.Dispose();
    }
}

static Bitmap CreateBitmap(int size)
{
    var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.SmoothingMode = SmoothingMode.AntiAlias;
    graphics.Clear(Color.Transparent);

    var margin = Math.Max(1, size / 16);
    using (var background = new SolidBrush(Color.FromArgb(36, 41, 46)))
    {
        graphics.FillEllipse(background, margin, margin, size - margin * 2, size - margin * 2);
    }

    var dot = Math.Max(2, size * 3 / 10);
    var offset = (size - dot) / 2;
    using (var accent = new SolidBrush(Color.FromArgb(88, 166, 255)))
    {
        graphics.FillEllipse(accent, offset, offset, dot, dot);
    }

    return bitmap;
}
