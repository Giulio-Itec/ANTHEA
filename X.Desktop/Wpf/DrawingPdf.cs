using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace X.Desktop;

/// <summary>Self-contained PDF pages from the same rendered sheets exported as PNG, with no print-driver dependency.</summary>
internal static class DrawingPdf
{
    internal static byte[] Create(IEnumerable<byte[]> pngPages)
    {
        using var output = new MemoryStream(); var offsets = new List<long> { 0 };
        void Write(string s) => output.Write(Encoding.ASCII.GetBytes(s));
        void Object(int id, string data) { offsets.Add(output.Position); Write($"{id} 0 obj\n{data}\nendobj\n"); }
        void Stream(int id, string attributes, byte[] data)
        { offsets.Add(output.Position); Write($"{id} 0 obj\n<< {attributes} /Length {data.Length} >>\nstream\n"); output.Write(data); Write("\nendstream\nendobj\n"); }
        var images = pngPages.Select(bytes =>
        {
            using var stream = new MemoryStream(bytes); var bitmap = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var encoder = new JpegBitmapEncoder { QualityLevel = 96 }; encoder.Frames.Add(bitmap); using var jpg = new MemoryStream(); encoder.Save(jpg);
            return (Data: jpg.ToArray(), Width: bitmap.PixelWidth, Height: bitmap.PixelHeight);
        }).ToArray();
        if (images.Length == 0) throw new ArgumentException("Nessuna tavola da esportare.");
        Write("%PDF-1.4\n"); Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        Object(2, $"<< /Type /Pages /Count {images.Length} /Kids [" + string.Join(" ", Enumerable.Range(0, images.Length).Select(i => $"{3 + 3 * i} 0 R")) + "] >>");
        for (int i = 0; i < images.Length; i++)
        {
            var image = images[i]; int id = 3 + 3 * i;
            // A3 landscape in PDF points, preserving the sheet aspect ratio within the page.
            double width = 1190.551, height = 841.89, drawHeight = width * image.Height / image.Width, bottom = (height - drawHeight) / 2;
            string N(double x) => x.ToString("0.###", CultureInfo.InvariantCulture);
            Object(id, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {N(width)} {N(height)}] /Resources << /XObject << /Sheet {id + 1} 0 R >> >> /Contents {id + 2} 0 R >>");
            Stream(id + 1, $"/Type /XObject /Subtype /Image /Width {image.Width} /Height {image.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode", image.Data);
            Stream(id + 2, "", Encoding.ASCII.GetBytes($"q {N(width)} 0 0 {N(drawHeight)} 0 {N(bottom)} cm /Sheet Do Q"));
        }
        long xref = output.Position; Write($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        foreach (long offset in offsets.Skip(1)) Write(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        Write($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n"); return output.ToArray();
    }
}
