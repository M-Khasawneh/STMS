using System.IO.Compression;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using SkiaSharp;
using SourcingTenderManagementSystem.Web.Data;
namespace SourcingTenderManagementSystem.AuthenticationChecks;

internal static class AccessCopyChecks
{
    internal static async Task Run(string? output)
    {
        var count = 0;
        void Check(bool valid, string label) { if (!valid) throw new Exception(label); count++; }
        void Deny(Action action) { try { action(); throw new Exception("Expected refusal"); } catch (BidException) { count++; } }
        var at = new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "synthetic-viewer-1"), new Claim(ClaimTypes.Name, "Test\nViewer <script>") }, "fixture"));
        var stamp = AccessStamp.Create(principal, at);
        Check(stamp.Name == "TestViewer <script>" && stamp.Label.Contains("synthetic-viewer-1") && stamp.Label.EndsWith("UTC"), "Trusted identity and UTC stamp, controls removed");
        Deny(() => AccessStamp.Create(new ClaimsPrincipal(), at));
        var id = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        var links = new QrLinks("https://stms.example");
        foreach (var controller in new[] { "Tenders", "Requisitions" })
        {
            var link = links.Link(controller, id);
            Check(link == $"https://stms.example/{controller}/Details/{id:D}" && !link.Contains("?"), "QR only contains protected details URL");
            using var png = SKBitmap.Decode(links.Png(controller, id));
            Check(png.Width == png.Height && png.Width > 200 && png.GetPixel(0, 0) == SKColors.White, "Valid PNG QR with quiet zone");
            using var generator = new QRCoder.QRCodeGenerator();
            using var data = generator.CreateQrCode(link, QRCoder.QRCodeGenerator.ECCLevel.Q);
            Check(png.Width == data.ModuleMatrix.Count * 8 && data.ModuleMatrix.Select((row, y) => Enumerable.Range(0, row.Count).All(x => row[x] == (png.GetPixel(x * 8 + 4, y * 8 + 4).Red < 128))).All(x => x), "PNG modules exactly encode canonical link with Q error correction");
        }
        var anotherId = Guid.Parse("22345678-1234-1234-1234-123456789abc");
        Check(links.Link("Tenders", id) != links.Link("Tenders", anotherId) && !links.Png("Tenders", id).SequenceEqual(links.Png("Tenders", anotherId)), "Different records have different canonical links and QR images");
        Check(!links.Png("Tenders", id).SequenceEqual(links.Png("Requisitions", id)), "Same identifier in different modules produces different QR images");
        Check(links.Png("Tenders", id).SequenceEqual(links.Png("Tenders", id)), "A record QR stays stable across repeated requests");
        foreach (var origin in new[] { "http://stms.example", "https://stms.example/folder", "https://user:password@stms.example", "https://stms.example/?x=1", "https://stms.example/#part", "bad" }) Deny(() => new QrLinks(origin).Png("Tenders", id));
        Deny(() => links.Link("../Account", id)); Deny(() => links.Link("Tenders", Guid.Empty));
        var renderer = new PrivateWatermarkedCopies();
        var original = DocumentExtractionChecks.TextPdf(2);
        var hash = SHA256.HashData(original);
        async Task<WatermarkedCopy> Copy(byte[] bytes, string name, AccessStamp user) { await using var source = new BidDocumentDownload(new MemoryStream(bytes), name, "application/octet-stream", bytes.Length); return await renderer.CreateAsync(source, user, default); }
        var pdf = await Copy(original, "../../proposal.pdf", stamp);
        Check(pdf.ContentType == "application/pdf" && pdf.Name == "proposal-watermarked.pdf" && pdf.Bytes.AsSpan().StartsWith("%PDF"u8), "Private worker creates safe PDF and basename");
        using (var parsed = UglyToad.PdfPig.PdfDocument.Open(pdf.Bytes))
        {
            Check(parsed.NumberOfPages == 2, "Every source PDF page preserved");
            Check(parsed.GetPages().All(p => p.GetImages().Any() && string.IsNullOrWhiteSpace(p.Text)), "PDF contains rendered personal-copy images, no original text/active layer");
        }
        var longSource = DocumentExtractionChecks.TextPdf(41);
        var renderWatch = System.Diagnostics.Stopwatch.StartNew();
        var longCopy = await Copy(longSource, "long.pdf", stamp);
        Console.WriteLine($"TIMING: 41-page protected PDF {renderWatch.Elapsed.TotalSeconds:0.00}s; {longCopy.Bytes.Length} bytes; unchanged 45-second worker deadline.");
        using (var longPdf = UglyToad.PdfPig.PdfDocument.Open(longCopy.Bytes)) Check(longPdf.NumberOfPages == 41, "PDFs over forty pages remain downloadable");
        renderWatch.Restart();
        var concurrent = await Task.WhenAll(Copy(longSource,"parallel.pdf",stamp),Copy(longSource,"parallel.pdf",stamp with { UserId="parallel-viewer" }));
        foreach (var copy in concurrent) {
            using var parsed = UglyToad.PdfPig.PdfDocument.Open(copy.Bytes);
            Check(parsed.NumberOfPages==41 && parsed.GetPages().All(p=>p.GetImages().Count()==1 && string.IsNullOrWhiteSpace(p.Text)), "Concurrent copies preserve every flattened page and never release active/source text layers");
        }
        Check(!concurrent[0].Bytes.SequenceEqual(concurrent[1].Bytes), "Concurrent users retain distinct personal copies");
        Console.WriteLine($"TIMING: two concurrent 41-page protected PDFs {renderWatch.Elapsed.TotalSeconds:0.00}s.");
        using (var bitmap = SKBitmap.Decode(DocumentExtractionChecks.Image())) {
            var encode = typeof(DocumentWatermarkWorker).GetMethod("Encode",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!;
            using var encoded = (SKData)encode.Invoke(null,[bitmap])!;
            using var decoded = SKBitmap.Decode(encoded);
            Check(bitmap.Width==decoded.Width && bitmap.Height==decoded.Height && bitmap.Pixels.SequenceEqual(decoded.Pixels), "Fast PNG compression preserves every decoded pixel");
        }
        var mixedBuilder = new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
        mixedBuilder.AddPage(UglyToad.PdfPig.Content.PageSize.A4); mixedBuilder.AddPage(UglyToad.PdfPig.Content.PageSize.A5);
        var mixed = await Copy(mixedBuilder.Build(),"mixed.pdf",stamp);
        using (var parsed = UglyToad.PdfPig.PdfDocument.Open(mixed.Bytes)) Check(parsed.NumberOfPages==2 && parsed.GetPage(1).Width!=parsed.GetPage(2).Width, "Mixed source page sizes remain distinct");
        Check(SHA256.HashData(original).SequenceEqual(hash), "Original PDF unchanged");
        var second = await Copy(original, "proposal.pdf", stamp with { UserId = "synthetic-viewer-2" });
        Check(!pdf.Bytes.SequenceEqual(second.Bytes), "Different authenticated viewers produce different copies");
        foreach (var format in new[] { SKEncodedImageFormat.Png, SKEncodedImageFormat.Jpeg })
        {
            var image = DocumentExtractionChecks.Image(format: format);
            var copy = await Copy(image, format == SKEncodedImageFormat.Png ? "proposal.png" : "proposal.jpg", stamp);
            using var decoded = SKBitmap.Decode(copy.Bytes);
            Check(copy.ContentType == "image/png" && decoded.Width == 1400 && decoded.Height > 280, "Image derivative has visible identity band");
        }
        foreach (var invalid in new[] { Encoding.ASCII.GetBytes("%PDF-1.4\nbroken\n%%EOF"), DocumentExtractionChecks.ScannedPdf(true) })
        {
            try { await Copy(invalid, "invalid.pdf", stamp); throw new Exception("Invalid source leaked"); } catch (BidException e) { Check(e.Status is 409 or 503, "Unsafe/unrenderable source fails closed without returning original"); }
        }
        await using (var oversize = new BidDocumentDownload(new MemoryStream(new byte[5242881]), "large.pdf", "application/pdf", 5242881))
        { try { await renderer.CreateAsync(oversize, stamp, default); throw new Exception("Size bypass"); } catch (BidException e) { Check(e.Status == 409, "Input read bounded before native decoding"); } }
        using (var cancel = new CancellationTokenSource())
        { cancel.Cancel(); await using var source = new BidDocumentDownload(new MemoryStream(original), "a.pdf", "application/pdf", original.Length); try { await renderer.CreateAsync(source, stamp, cancel.Token); throw new Exception("Cancellation ignored"); } catch (OperationCanceledException) { count++; } }
        var report = ReportingChecks.FixtureDocument() with { AccessWatermark = stamp.Label };
        var excel = ReportExports.Excel(report);
        using (var zip = new ZipArchive(new MemoryStream(excel)))
        {
            foreach (var sheet in zip.Entries.Where(e => e.FullName.StartsWith("xl/worksheets/") && e.FullName.EndsWith(".xml")))
            { using var reader = new StreamReader(sheet.Open()); var xml = await reader.ReadToEndAsync(); Check(xml.Contains("synthetic-viewer-1") && xml.Contains("headerFooter") && xml.Contains("mergeCell") && xml.Contains("customHeight") && xml.Contains("&lt;script&gt;"), "Every Excel sheet has escaped visible stamp and print header"); }
        }
        var reportPdf = ReportExports.Pdf(report);
        using (var parsed = UglyToad.PdfPig.PdfDocument.Open(reportPdf)) Check(parsed.GetPages().All(p => p.Text.Contains("synthetic-viewer-1")), "Every exported report PDF page is personalized");
        if (output is not null)
        {
            Directory.CreateDirectory(output);
            await File.WriteAllBytesAsync(Path.Combine(output, "personal-copy.pdf"), pdf.Bytes);
            if (OperatingSystem.IsWindows())
            {
                using var parsed = UglyToad.PdfPig.PdfDocument.Open(pdf.Bytes);
                var first = parsed.GetPage(1);
                using var bitmap = PDFtoImage.Conversion.ToImage(pdf.Bytes, options: new PDFtoImage.RenderOptions(Width: 1000, Height: (int)(1000 * first.Height / first.Width)));
                using var image = SKImage.FromBitmap(bitmap);
                using var png = image.Encode(SKEncodedImageFormat.Png, 100);
                await File.WriteAllBytesAsync(Path.Combine(output, "personal-copy-preview.png"), png.ToArray());
            }
            await File.WriteAllBytesAsync(Path.Combine(output, "request-qr.png"), links.Png("Requisitions", id));
            await File.WriteAllBytesAsync(Path.Combine(output, "stamped-report.pdf"), reportPdf);
            await File.WriteAllBytesAsync(Path.Combine(output, "stamped-report.xlsx"), excel);
        }
        Console.WriteLine($"PASS: {count} database-free QR, private worker, original-integrity and personalized-report checks. Synthetic documents only; no SQL.");
    }
}
