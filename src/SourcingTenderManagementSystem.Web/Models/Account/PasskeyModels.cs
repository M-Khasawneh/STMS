using System.ComponentModel.DataAnnotations;
namespace SourcingTenderManagementSystem.Web.Models.Account;
public sealed class PasskeyEnrollForm {
 [Required,StringLength(80)] public string Name {get;set;}="";
 [Required,DataType(DataType.Password),StringLength(256)] public string CurrentPassword {get;set;}="";
}
public sealed class PasskeyCredentialForm { [Required,StringLength(24000)] public string CredentialJson {get;set;}=""; }
public sealed class PasskeyRemoveForm {
 [Required,StringLength(1400)] public string CredentialId {get;set;}="";
 [Required,DataType(DataType.Password),StringLength(256)] public string CurrentPassword {get;set;}="";
}
public sealed record PasskeyRow(string CredentialId,string Name,DateTimeOffset CreatedAt);
public sealed record PasskeyPage(IReadOnlyList<PasskeyRow> Keys);
