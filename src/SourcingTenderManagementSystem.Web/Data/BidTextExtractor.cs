using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SourcingTenderManagementSystem.Web.Models.Analysis;
namespace SourcingTenderManagementSystem.Web.Data;
public sealed class BidTextOptions { public string? LanguageDataPath {get;set;} }
public interface IBidTextExtractor { Task<AiTextResult> ExtractAsync(byte[] bytes,string contentType,CancellationToken ct); }
public sealed class BidTextExtractor(IWebHostEnvironment environment,IOptions<BidTextOptions> options):IBidTextExtractor {
 public async Task<AiTextResult> ExtractAsync(byte[] bytes,string contentType,CancellationToken ct){
  ct.ThrowIfCancellationRequested();
  if(bytes.Length is <=0 or >5242880||contentType is not("application/pdf" or "image/png" or "image/jpeg"))return new("Unreadable",0,[]);
  var folder=Path.GetFullPath(options.Value.LanguageDataPath??Path.Combine(environment.ContentRootPath,"App_Data","Ocr","tessdata"));
  var assembly=typeof(BidTextWorker).Assembly.Location;
  var info=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8};
  // No shell, posted filenames or uploaded executable paths. Only our trusted application assembly runs.
  info.ArgumentList.Add(assembly);info.ArgumentList.Add("--extract-bid-text");info.ArgumentList.Add(contentType);info.ArgumentList.Add(folder);info.Environment["OMP_THREAD_LIMIT"]="1";
  using var process=new Process{StartInfo=info};using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(45));var token=deadline.Token;
  var started=false;Task[] running=[];
  try{
   if(!process.Start())return new("Unavailable",0,[]);
   started=true;
   async Task Send(){await process.StandardInput.BaseStream.WriteAsync(bytes,token);process.StandardInput.Close();}
   async Task<string> Read(StreamReader reader,int maximum){var buffer=new char[2048];var text=new StringBuilder();int n;while((n=await reader.ReadAsync(buffer.AsMemory(),token))>0){if(text.Length+n>maximum)throw new InvalidDataException();text.Append(buffer,0,n);}return text.ToString();}
   var output=Read(process.StandardOutput,100000);var error=Read(process.StandardError,16000);var send=Send();var exit=process.WaitForExitAsync(token);running=[output,error,send,exit];
   while(!exit.IsCompleted){token.ThrowIfCancellationRequested();process.Refresh();if(process.HasExited)break;try{if(process.WorkingSet64>512L*1024*1024)throw new InvalidDataException();}catch(InvalidOperationException)when(process.HasExited){break;}if(output.IsFaulted||error.IsFaulted||send.IsFaulted)throw new InvalidDataException();await Task.Delay(100,token);}
   await Task.WhenAll(send,exit,error);var json=await output;
   if(process.ExitCode!=0)return new("Unavailable",0,[]);
   var result=JsonSerializer.Deserialize<AiTextResult>(json,AiBidRules.Json);return AiDocumentRules.ValidText(result)?result!:new("Unreadable",0,[]);
  }catch(OperationCanceledException)when(!ct.IsCancellationRequested){return new("Timeout",0,[]);}
   catch(Exception e)when(e is System.ComponentModel.Win32Exception or IOException or JsonException or InvalidOperationException){return new("Unavailable",0,[]);}
  finally{if(started){try{if(!process.HasExited)process.Kill(entireProcessTree:true);await process.WaitForExitAsync(CancellationToken.None);}catch(InvalidOperationException){} }deadline.Cancel();try{await Task.WhenAll(running).WaitAsync(TimeSpan.FromSeconds(5));}catch(Exception e)when(e is OperationCanceledException or IOException or InvalidOperationException or TimeoutException){} }
 }
}
