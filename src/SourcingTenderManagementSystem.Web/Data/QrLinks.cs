using QRCoder;
namespace SourcingTenderManagementSystem.Web.Data;

public sealed class QrLinks(string? baseAddress)
{
    private readonly Uri? origin = Uri.TryCreate(baseAddress, UriKind.Absolute, out var u) && u.Scheme == "https" && u.AbsolutePath == "/" && u.Query == "" && u.Fragment == "" && u.UserInfo == "" ? u : null;
    public string Link(string controller, Guid id) { BidRules.Require(origin is not null, 503, "Configure PublicSite:Origin with the deployed HTTPS origin before generating QR codes."); BidRules.Require(controller is "Requisitions" or "Tenders" && id != Guid.Empty, 400, "Invalid QR record."); return new Uri(origin, $"{controller}/Details/{id:D}").AbsoluteUri; }
    public byte[] Png(string controller, Guid id) => PngByteQRCodeHelper.GetQRCode(Link(controller, id), QRCodeGenerator.ECCLevel.Q, 8);
}
