using System.Drawing;
using System.Drawing.Drawing2D;

namespace LogiBatteryWidget.App.Settings;

/// <summary>Draws a small battery glyph in-process so the app doesn't need a shipped .ico asset.</summary>
public static class TrayIconFactory
{
    public static Icon CreateBatteryIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var outlinePen = new Pen(Color.White, 2.5f);
            using var fillBrush = new SolidBrush(Color.FromArgb(0x30, 0xD1, 0x58));

            var body = new Rectangle(2, 8, 24, 16);
            g.DrawRoundedRectangle(outlinePen, body, 4);
            g.FillRoundedRectangle(fillBrush, Rectangle.Inflate(body, -4, -4), 2);

            // battery nub
            using var nubBrush = new SolidBrush(Color.White);
            g.FillRectangle(nubBrush, 27, 13, 3, 6);
        }

        var handle = bitmap.GetHicon();
        return Icon.FromHandle(handle);
    }

    private static void DrawRoundedRectangle(this Graphics g, Pen pen, Rectangle bounds, int radius)
    {
        using var path = RoundedRectanglePath(bounds, radius);
        g.DrawPath(pen, path);
    }

    private static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle bounds, int radius)
    {
        using var path = RoundedRectanglePath(bounds, radius);
        g.FillPath(brush, path);
    }

    private static GraphicsPath RoundedRectanglePath(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
