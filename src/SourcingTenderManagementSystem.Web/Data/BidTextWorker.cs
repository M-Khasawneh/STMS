using System.Text.Json;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using TesseractOCR;
using TesseractOCR.Enums;
using SourcingTenderManagementSystem.Web.Models.Analysis;
namespace SourcingTenderManagementSystem.Web.Data;

// Private CLI mode of this same application. Never starts MVC, Identity or a database connection.
// Parent sends verified bytes over stdin and enforces process deadline/output/memory limits.
public static class BidTextWorker {
 public const int MaxPages=10,MaxText=1200;
 public static async Task<int> RunAsync(string[] args){
  Console.OutputEncoding=new System.Text.UTF8Encoding(false);
  AiTextResult result;
  try{
   if(args.Length!=3||args[1] is not("application/pdf" or "image/png" or "image/jpeg"))throw new InvalidDataException();
   using var memory=new MemoryStream();var input=Console.OpenStandardInput();var chunk=new byte[8192];int read;
   while((read=await input.ReadAsync(chunk))>0){if(memory.Length+read>5*1024*1024)throw new InvalidDataException();memory.Write(chunk,0,read);}
   var bytes=memory.ToArray();if(bytes.Length==0)throw new InvalidDataException();result=Extract(bytes,args[1],args[2]);
  }catch(Exception e)when(e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or InvalidOperationException){result=new("Unavailable",0,[]);}
   catch(Exception){result=new("Unreadable",0,[]);}
  // Do not log file contents, parser exception strings or physical paths.
  await Console.Out.WriteAsync(JsonSerializer.Serialize(result,AiBidRules.Json));return 0;
 }
 public static AiTextResult Extract(byte[] bytes,string contentType,string languagePath){
  Engine? engine=null;
  try{
   Engine Ocr(){
    if(!File.Exists(Path.Combine(languagePath,"eng.traineddata"))||!File.Exists(Path.Combine(languagePath,"ara.traineddata")))throw new InvalidOperationException("OCR language data unavailable");
    return engine??=new Engine(languagePath,"eng+ara",EngineMode.LstmOnly);
   }
   AiTextPage ReadImage(byte[] image,int number){
    using var encoded=SKData.CreateCopy(image);using var codec=SKCodec.Create(encoded);
    if(codec is null||codec.Info.Width<=0||codec.Info.Height<=0||codec.Info.Width>6000||codec.Info.Height>6000||(long)codec.Info.Width*codec.Info.Height>12000000)throw new InvalidDataException();
    using var pix=TesseractOCR.Pix.Image.LoadFromMemory(image);using var page=Ocr().Process(pix);
    var text=Clean(page.Text??"");return new(number,"OCR",text[..Math.Min(text.Length,MaxText)],text.Length>MaxText,Math.Clamp(page.MeanConfidence,0,1));
   }
   if(contentType is "image/png" or "image/jpeg"){var p=ReadImage(bytes,1);return new(string.IsNullOrWhiteSpace(p.Text)?"NoText":p.Truncated?"Partial":"Complete",1,[p]);}
   if(contentType!="application/pdf"||!bytes.AsSpan().StartsWith("%PDF-"u8))throw new InvalidDataException();
   using var pdf=PdfDocument.Open(bytes);var pages=new List<AiTextPage>();
   for(int number=1;number<=Math.Min(pdf.NumberOfPages,MaxPages);number++){
    var page=pdf.GetPage(number);var text=Clean(ContentOrderTextExtractor.GetText(page));
    if(text.Count(char.IsLetterOrDigit)>=24&&!page.GetImages().Any()){pages.Add(new(number,"PDF text",text[..Math.Min(text.Length,MaxText)],text.Length>MaxText,null));continue;}
    if(page.Width<=0||page.Height<=0||page.Width>20000||page.Height>20000)throw new InvalidDataException();
    var scale=Math.Min(150d/72,Math.Min(2000d/page.Width,2600d/page.Height));
    if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
    using var bitmap=PDFtoImage.Conversion.ToImage(bytes,number-1,options:new PDFtoImage.RenderOptions(Width:Math.Max(1,(int)(page.Width*scale)),Height:Math.Max(1,(int)(page.Height*scale)),WithAnnotations:false,WithFormFill:false));
    using var image=SKImage.FromBitmap(bitmap);using var data=image.Encode(SKEncodedImageFormat.Png,100);var ocr=ReadImage(data.ToArray(),number);
    // Keep a small native text layer if OCR found nothing; never claim that all visual content was examined.
    if(text.Length>0&&!string.IsNullOrWhiteSpace(ocr.Text)){
     var combined="PDF text: "+text+"\nOCR: "+ocr.Text;pages.Add(new(number,"PDF text + OCR",combined[..Math.Min(combined.Length,MaxText)],ocr.Truncated||combined.Length>MaxText,ocr.Confidence));
    }else pages.Add(string.IsNullOrWhiteSpace(ocr.Text)&&text.Length>0?new(number,"PDF text",text[..Math.Min(text.Length,MaxText)],text.Length>MaxText,null):ocr);
   }
   return new(pages.All(p=>string.IsNullOrWhiteSpace(p.Text))?"NoText":pdf.NumberOfPages>MaxPages||pages.Any(p=>p.Truncated)?"Partial":"Complete",pdf.NumberOfPages,pages);
  }finally{engine?.Dispose();}
 }
 private static string Clean(string text)=>new(text.Where(c=>!char.IsControl(c)||c is '\n' or '\t').Take(100000).ToArray());
}
