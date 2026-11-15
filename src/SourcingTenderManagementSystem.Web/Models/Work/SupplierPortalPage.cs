using SourcingTenderManagementSystem.Web.Models.Tenders;
using SourcingTenderManagementSystem.Web.Models.Bids;
namespace SourcingTenderManagementSystem.Web.Models.Work;
public sealed record SupplierPortalPage(TenderDetails Tender,InvitationRow Invitation,SupplierBidDetails? Offer,bool CanRespond,bool CanCreate,string? Notice);
