using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Blink;

/// <summary>
/// Draws Blink's artwork at runtime: a round sleeping face (outline, two closed
/// eyes, and a rising "zZz"). This is the single source of truth for both the
/// tray icon (<see cref="CreateSleepyEye"/>) and the app/window icon
/// (<see cref="SaveIco"/>), so they always match.
/// </summary>
public static class IconFactory
{
    // Artwork is authored on a 64x64 grid and scaled to whatever size is requested.
    private const float Grid = 64f;

    /// <summary>Creates the tray icon.</summary>
    public static Icon CreateSleepyEye(bool paused = false)
    {
        using var bmp = CreateBitmap(64, paused);
        var hicon = bmp.GetHicon();
        try
        {
            // Clone into a managed Icon so we can free the native handle immediately.
            using var tmp = Icon.FromHandle(hicon);
            return (Icon)tmp.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hicon);
        }
    }

    /// <summary>Writes a multi-resolution .ico (16-256px) for the app/window icon.</summary>
    public static void SaveIco(string path)
    {
        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
        var frames = sizes.Select(s =>
        {
            using var bmp = CreateBitmap(s, paused: false);
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png); // PNG-compressed frames (Win Vista+)
            return (size: s, png: ms.ToArray());
        }).ToArray();

        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);

        bw.Write((short)0);             // reserved
        bw.Write((short)1);             // type = icon
        bw.Write((short)frames.Length); // image count

        int offset = 6 + 16 * frames.Length;
        foreach (var frame in frames)
        {
            bw.Write((byte)(frame.size >= 256 ? 0 : frame.size)); // width  (0 => 256)
            bw.Write((byte)(frame.size >= 256 ? 0 : frame.size)); // height (0 => 256)
            bw.Write((byte)0);   // palette count
            bw.Write((byte)0);   // reserved
            bw.Write((short)1);  // color planes
            bw.Write((short)32); // bits per pixel
            bw.Write(frame.png.Length);
            bw.Write(offset);
            offset += frame.png.Length;
        }
        foreach (var frame in frames)
            bw.Write(frame.png);
    }

    private static Bitmap CreateBitmap(int size, bool paused)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        DrawFace(g, size, paused);
        return bmp;
    }

    private static void DrawFace(Graphics g, int size, bool paused)
    {
        float f = size / Grid;
        var color = paused ? Color.FromArgb(150, 160, 175) : Color.FromArgb(74, 155, 239);

        // Face outline.
        using (var ring = new Pen(color, 4f * f))
            g.DrawEllipse(ring, 8 * f, 13 * f, 45 * f, 45 * f);

        // Two closed eyes: gentle domes (peak in the middle) that read as "asleep".
        using (var eye = new Pen(color, 3.5f * f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            DrawDome(g, eye, f, left: 15, right: 27, baseline: 39, peak: 31); // left eye
            DrawDome(g, eye, f, left: 34, right: 46, baseline: 39, peak: 31); // right eye
        }

        // Rising "zZz" above the face, small to large.
        using (var z = new Pen(color, 3f * f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
        {
            DrawZ(g, z, f, x: 30, y: 21, w: 5, h: 5);
            DrawZ(g, z, f, x: 37, y: 13, w: 6, h: 6);
            DrawZ(g, z, f, x: 45, y: 4, w: 8, h: 8);
        }
    }

    private static void DrawDome(Graphics g, Pen pen, float f, float left, float right, float baseline, float peak)
    {
        float dx = (right - left) * 0.28f;
        g.DrawBezier(pen,
            new PointF(left * f, baseline * f),
            new PointF((left + dx) * f, peak * f),
            new PointF((right - dx) * f, peak * f),
            new PointF(right * f, baseline * f));
    }

    private static void DrawZ(Graphics g, Pen pen, float f, float x, float y, float w, float h)
    {
        var tl = new PointF(x * f, y * f);
        var tr = new PointF((x + w) * f, y * f);
        var bl = new PointF(x * f, (y + h) * f);
        var br = new PointF((x + w) * f, (y + h) * f);
        g.DrawLines(pen, new[] { tl, tr, bl, br }); // top bar, diagonal, bottom bar
    }
}

internal static class NativeMethods
{
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    public static extern bool DestroyIcon(IntPtr handle);
}
