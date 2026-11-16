using System.Text.Json;
using System.Text;
using SkiaSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using SourcingTenderManagementSystem.Web.Models.Analysis;
namespace SourcingTenderManagementSystem.Web.Data;

public sealed record WatermarkInput(string Type, string Base64, AccessStamp Stamp, bool ValidateOnly = false);
// Same-application private worker. It never initializes MVC/Identity/database services.
public static class DocumentWatermarkWorker
{
    public static async Task<int> RunAsync()
    {
        try
        {
            using var input = new MemoryStream(); using var stdin = Console.OpenStandardInput(); var buffer = new byte[8192]; int n; while ((n = await stdin.ReadAsync(buffer)) > 0) { if (input.Length + n > 7500000) return 1; input.Write(buffer, 0, n); }
            var request = JsonSerializer.Deserialize<WatermarkInput>(input.ToArray(), AiBidRules.Json)!; var bytes = Convert.FromBase64String(request.Base64); if (bytes.Length is <= 0 or > 5242880 || request.Stamp.Label.Length > 800) return 1;
            var output = request.ValidateOnly ? Validate(bytes, request.Type) : Render(bytes, request.Type, request.Stamp); await Console.OpenStandardOutput().WriteAsync(output); return 0;
        }
        catch { return 1; }
    }
    private static SKBitmap RenderPage(byte[] bytes, int page, PDFtoImage.RenderOptions options)
    {
        if (OperatingSystem.IsWindows()) return PDFtoImage.Conversion.ToImage(bytes, page, options: options);
        if (OperatingSystem.IsLinux()) return PDFtoImage.Conversion.ToImage(bytes, page, options: options);
        if (OperatingSystem.IsMacOS()) return PDFtoImage.Conversion.ToImage(bytes, page, options: options);
        throw new PlatformNotSupportedException();
    }
    private static byte[] Validate(byte[] bytes, string type)
    {
        if (type == "application/pdf")
        {
            if (!(OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())) throw new PlatformNotSupportedException();
            using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
            if (pdf.NumberOfPages <= 0) throw new InvalidDataException();
            foreach (var page in pdf.GetPages())
            {
                if (page.Width <= 0 || page.Height <= 0 || page.Width > 20000 || page.Height > 20000) throw new InvalidDataException();
                // Use the same native renderer as protected downloads, with a tiny bounded surface.
                using var decoded = RenderPage(bytes, page.Number - 1,
                    options: new PDFtoImage.RenderOptions(Width: 64, Height: 64, WithAnnotations: true, WithFormFill: true));
                if (decoded.Width <= 0 || decoded.Height <= 0) throw new InvalidDataException();
            }
        }
        else
        {
            using var encoded = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(encoded);
            if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0 || codec.Info.Width > 6000 || codec.Info.Height > 6000 || (long)codec.Info.Width * codec.Info.Height > 12000000) throw new InvalidDataException();
            using var decoded = new SKBitmap(codec.Info);
            if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success) throw new InvalidDataException();
        }
        return [1];
    }
    public static byte[] Render(byte[] bytes, string type, AccessStamp stamp)
    {
        if (type is "image/png" or "image/jpeg") return Image(bytes, stamp);
        if (type != "application/pdf") throw new InvalidDataException();
        if (!(OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())) throw new PlatformNotSupportedException();
        using var source = UglyToad.PdfPig.PdfDocument.Open(bytes); if (source.NumberOfPages <= 0) throw new InvalidDataException();
        using var doc = new PdfDocument(); doc.Info.Title = "STMS personal watermarked copy";
        for (int i = 1; i <= source.NumberOfPages; i++)
        {
            var original = source.GetPage(i); if (original.Width <= 0 || original.Height <= 0 || original.Width > 20000 || original.Height > 20000) throw new InvalidDataException(); var scale = Math.Min(150d / 72, Math.Min(2000d / original.Width, 2600d / original.Height));
            using var bitmap = RenderPage(bytes, i - 1, options: new PDFtoImage.RenderOptions(Width: Math.Max(1, (int)(original.Width * scale)), Height: Math.Max(1, (int)(original.Height * scale)), WithAnnotations: true, WithFormFill: true));
            using var sourceImage = SKImage.FromBitmap(bitmap); using var png = sourceImage.Encode(SKEncodedImageFormat.Png, 100); var marked = Image(png.ToArray(), stamp); using var stream = new MemoryStream(marked); using var image = XImage.FromStream(stream); var page = doc.AddPage(); page.Width = XUnit.FromPoint(original.Width); page.Height = XUnit.FromPoint(original.Width * image.PixelHeight / image.PixelWidth); using var graphics = XGraphics.FromPdfPage(page); graphics.DrawImage(image, 0, 0, page.Width.Point, page.Height.Point);
        }
        using var output = new MemoryStream(); doc.Save(output, false); if (output.Length > 40 * 1024 * 1024) throw new InvalidDataException(); return output.ToArray();
    }
    private static byte[] Image(byte[] bytes, AccessStamp stamp)
    {
        using var encoded = SKData.CreateCopy(bytes); using var codec = SKCodec.Create(encoded); if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0 || codec.Info.Width > 6000 || codec.Info.Height > 6000 || (long)codec.Info.Width * codec.Info.Height > 12000000) throw new InvalidDataException(); using var original = SKBitmap.Decode(bytes);
        var width = Math.Max(original.Width, 800); var scale = (double)width / original.Width; var height = (int)(original.Height * scale); if ((long)width * height > 16000000) throw new InvalidDataException();
        using var typeface = SKTypeface.FromFamilyName("Arial");
        using var footerFont = new SKFont(typeface, Math.Clamp(width / 70f, 11, 22));
        var lines = new List<string>(); var current = "";
        foreach (var rune in stamp.Label.EnumerateRunes())
        {
            var candidate = current + rune;
            if (current.Length > 0 && footerFont.MeasureText(candidate) > width - 40) { lines.Add(current); current = rune.ToString(); }
            else current = candidate;
        }
        lines.Add(current);
        var lineHeight = (int)Math.Ceiling(footerFont.Size * 1.4);
        var band = Math.Max(90, lines.Count * lineHeight + 30);
        using var canvasImage = new SKBitmap(width, height + band); using var canvas = new SKCanvas(canvasImage); canvas.Clear(SKColors.White); canvas.DrawBitmap(original, new SKRect(0, 0, width, height), new SKSamplingOptions(SKFilterMode.Linear));
        using var font = new SKFont(typeface, Math.Clamp(width / 38f, 18, 42)); using var paint = new SKPaint { Color = new SKColor(21, 43, 64, 65), IsAntialias = true };
        for (int y = 160; y < height; y += 360) { canvas.Save(); canvas.Translate(width / 2f, y); canvas.RotateDegrees(-22); var text = stamp.Mark; var size = font.MeasureText(text); canvas.DrawText(text, -size / 2, 0, SKTextAlign.Left, font, paint); canvas.Restore(); }
        paint.Color = new SKColor(21, 43, 64); int line = 0; foreach (var text in lines) { canvas.DrawText(text, 20, height + 25 + line++ * lineHeight, SKTextAlign.Left, footerFont, paint); }
        using var final = SKImage.FromBitmap(canvasImage); using var data = final.Encode(SKEncodedImageFormat.Png, 100); return data.ToArray();
    }
}
