using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.Work;
namespace SourcingTenderManagementSystem.Web.Data;
public interface ISupplierTenderPortal {Task<SupplierPortalPage> ReadAsync(ClaimsPrincipal user,Guid id,CancellationToken ct);}
// Composition only: responses, bids, private downloads and deadline validation remain with their existing services.
public sealed class SupplierTenderPortal(ApplicationDbContext db,TenderManagement tenders,SupplierBidPages pages,TimeProvider clock):ISupplierTenderPortal
{
    public IQueryable<Invitation> OwnedInvitation(Guid tenderId,Guid supplierId)=>db.Invitations.AsNoTracking().Where(i=>db.TenderSuppliers.Any(l=>l.Id==i.TenderSupplierId&&l.TenderId==tenderId&&l.SupplierProfileId==supplierId)&&db.Tenders.Any(t=>t.Id==tenderId&&t.Status!=TenderStatus.Draft));
    public async Task<SupplierPortalPage> ReadAsync(ClaimsPrincipal user,Guid id,CancellationToken ct)
    {
        await pages.RequireSupplierAsync(user,ct);var actor=await tenders.ActorAsync(user);
        BidRules.Require(actor.SupplierId.HasValue,403,"Supplier access is required.");
        var invitation=await OwnedInvitation(id,actor.SupplierId!.Value).SingleOrDefaultAsync(ct);
        BidRules.Require(invitation is not null,404,"Tender not found.");
        var details=await tenders.DetailsAsync(id,actor);
        var supplier=await db.SupplierProfiles.AsNoTracking().SingleAsync(s=>s.Id==actor.SupplierId,ct);
        var bidId=await db.Bids.AsNoTracking().Where(b=>b.TenderId==id&&b.TenderSupplierId==invitation!.TenderSupplierId).Select(b=>(Guid?)b.Id).SingleOrDefaultAsync(ct);
        var offer=bidId.HasValue?await pages.DetailsAsync(user,bidId.Value,ct):null;
        var now=clock.GetUtcNow();var t=details.Tender;
        var expired=now>=t.SubmissionDeadline||invitation!.ExpiresAt.HasValue&&now>=invitation.ExpiresAt||t.Status is not(TenderStatus.Published or TenderStatus.Open);
        bool canCreate=false;string? notice=expired?"The invitation response and bidding period is closed.":null;
        if(offer is null&&invitation.Status==InvitationStatus.Accepted&&!expired){try{_ = await pages.CreateAsync(user,id,null,ct);canCreate=true;}catch(BidException e)when(e.Status is 400 or 409){notice=e.Message;}}
        await pages.RequireSupplierAsync(user,ct);
        return new(details,new(invitation,t,supplier),offer,!expired&&invitation.Status==InvitationStatus.Pending,canCreate,notice);
    }
}
