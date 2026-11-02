// SPDX-License-Identifier: GPL-3.0-only
using System.Text;

namespace EtiketStudio.Core;

public static class Epl2
{
    public static byte[] Calibration => Encoding.ASCII.GetBytes("\nxa\n");
    public static byte[] Configuration => Encoding.ASCII.GetBytes("\nU\n");

    // The EPL2 GW payload is binary, NOT a textual sequence of 0/1 digits or hexadecimal.
    // Zebra's specification defines a set bit as white, and the most significant bit as the left pixel.
    public static byte[] Pack(int width, int height, Func<int, int, bool> isBlack)
    {
        if (width is < 1 or > 832 || height is < 1 or > 2232) throw new DomainException("InvalidRaster");
        int stride = (width + 7) / 8;
        var data = new byte[stride * height];
        Array.Fill(data, (byte)255);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (isBlack(x, y)) data[y * stride + x / 8] &= (byte)~(0x80 >> (x % 8));
        return data;
    }
    private static string Header(LabelTemplate t, AppSettings s)
    {
        Validation.Template(t); Validation.Settings(s);
        // Reset reference and continuous-print mode so earlier applications cannot offset or hold labels.
        return FormattableString.Invariant($"\nR0,0\nq{Validation.Dots(t.Width)}\nQ{Validation.Dots(t.Height)},{Validation.Dots(t.Gap)}\nS{s.Speed}\nD{s.Darkness}\nZT\nO{(s.DirectThermal ? "D" : "")}\nN\n");
    }
    public static byte[] Raster(LabelTemplate t, AppSettings s, byte[] packed, int width, int height, int copies)
    {
        Validation.Copies(copies);
        if (width != Validation.Dots(t.Width) || height != Validation.Dots(t.Height) || packed.Length != (width + 7) / 8 * height)
            throw new DomainException("InvalidRaster");
        // Rotation is already baked into the preview/raster; ZT is intentionally constant.
        using var stream = new MemoryStream();
        stream.Write(Encoding.ASCII.GetBytes(Header(t, s) + $"GW0,0,{(width + 7) / 8},{height},"));
        stream.Write(packed);
        stream.Write(Encoding.ASCII.GetBytes($"\nP{copies}\n"));
        return stream.ToArray();
    }
    public static byte[] NativeDiagnostic(AppSettings settings)
    {
        var label = new LabelTemplate { Width = 50, Height = 30, Gap = 3 };
        var body = "A24,16,0,3,1,1,N,\"ETIKET STUDIO\"\nB24,64,0,1,2,2,88,B,\"123456789012\"\nP1\n";
        return Encoding.ASCII.GetBytes(Header(label, settings) + body);
    }
}
