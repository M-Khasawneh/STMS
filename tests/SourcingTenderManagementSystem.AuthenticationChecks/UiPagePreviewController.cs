using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models;
using SourcingTenderManagementSystem.Web.Models.Account;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.Requisitions;
using SourcingTenderManagementSystem.Web.Models.Suppliers;
using SourcingTenderManagementSystem.Web.Models.Tenders;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.AuthenticationChecks;
// Render-only test fixtures for real Razor views whose production controllers depend directly on SQL.
// This controller is never part of the production web assembly. It adds no application routes/features.
[Authorize(Policy="SignedInAccount")]
public sealed class UiPagePreviewController:Controller
{
    // Sample image only for visual fixtures; production QR authorization stays in its real controllers.
    [HttpGet]public IActionResult Qr([FromServices] QrLinks codes,string module="Requisitions",Guid? recordId=null)=>File(codes.Png(module,recordId??Guid.Parse("11111111-1000-4000-8000-000000000002")),"image/png");
    [HttpGet]public IActionResult Pdf()=>File(DocumentExtractionChecks.TextPdf(),"application/pdf","synthetic.pdf");
    [HttpGet]public async Task<IActionResult> Page(string name,string state="populated")
    {
        var now=DateTimeOffset.UtcNow;var empty=state=="empty";
        var dept=Guid.Parse("11111111-1000-4000-8000-000000000001");var id=Guid.Parse("11111111-1000-4000-8000-000000000002");
        var user=User.Identity?.Name??"requester";
        var q=new PurchaseRequisition{Id=id,Title="Network infrastructure renewal",CreatedByUserId="requester",DepartmentId=dept,Status=PurchaseRequestStatus.Draft,Priority="High",TargetDate=DateOnly.FromDateTime(now.AddDays(30).DateTime),Reason="Replace end-of-life equipment.",CreatedAt=now};
        var item=new PurchaseRequisitionItem{Id=Guid.NewGuid(),PurchaseRequisitionId=id,Description="Managed network switch",Quantity=12.5m,Specifications="24 ports, three-year warranty and installation."};
        var supplier=new SupplierProfile{Id=Guid.NewGuid(),UserId="supplier",CompanyName="Synthetic supply company",RegistrationNumber="QA-REG-001",TaxNumber="QA-TAX-001",ContactEmail="supplier@example.test",ContactName="Test contact",ContactPhone="0790000000",Status=SupplierStatus.Approved};
        var actor=new TenderActor(user,User.IsInRole(ApplicationRoles.SourcingOfficer),User.IsInRole(ApplicationRoles.Admin),User.IsInRole(ApplicationRoles.SourcingManager),User.IsInRole(ApplicationRoles.RequestingDepartment),User.IsInRole(ApplicationRoles.Supplier)?supplier.Id:null);
        var t=new Tender{Id=id,Title="Enterprise network procurement",Description="Supply the requested network equipment with the specified warranty.",Terms="Include the required technical documentation and itemized pricing.",Status=TenderStatus.Draft,CreatedByUserId="officer",SubmissionStart=now.AddDays(1),SubmissionDeadline=now.AddDays(15)};
        var criterion=new EvaluationCriterion{Id=Guid.NewGuid(),TenderId=id,Name="Synthetic technical criterion",Description="Presentation fixture only",Kind=EvaluationKind.Technical,Weight=100};
        if(name=="Tenders/Create")q.Status=PurchaseRequestStatus.Approved;
        if(name is "Tenders/Inbox" or "Tenders/Invitation")t.Status=TenderStatus.Published;
        if(name.StartsWith("Requisitions/")&&Enum.TryParse<PurchaseRequestStatus>(state,out var requestState))q.Status=requestState;
        if(name.StartsWith("Tenders/")&&Enum.TryParse<TenderStatus>(state,out var tenderState))t.Status=tenderState;
        if(name.StartsWith("Suppliers/")&&Enum.TryParse<SupplierStatus>(state,out var supplierState))supplier.Status=supplierState;
        var details=new TenderDetails(t,q,empty?[]:[item],empty?[]:[criterion],"synthetic-version",actor);
        var invitation=new InvitationRow(new Invitation{Id=Guid.NewGuid(),Status=InvitationStatus.Pending,SentAt=now},t,supplier);
        var requestActor=new RequisitionActor(user,dept,User.IsInRole(ApplicationRoles.RequestingDepartment),User.IsInRole(ApplicationRoles.SourcingOfficer),User.IsInRole(ApplicationRoles.Admin),User.IsInRole(ApplicationRoles.Supplier));
        object? model=null;
        var known=new HashSet<string>{"Requisitions/Index","Requisitions/Create","Requisitions/Edit","Requisitions/Details","Tenders/Index","Tenders/Create","Tenders/Edit","Tenders/Details","Tenders/Criteria","Tenders/Inbox","Tenders/Invitation","Tenders/Invitations","Suppliers/Index","Suppliers/Details","Suppliers/Status","Profile/Index","Passkeys/Index","Committees/Edit","Shared/Error","Shared/WorkspaceProblem","Account/AccessDenied","UnifiedReports/Review"};
        if(name.EndsWith("/Problem",StringComparison.Ordinal)){
            var segments=name.Split('/');var c=segments[0];if(segments.Length!=2||!new[]{"AiAnalysis","Bids","Clarifications","Committees","Compliance","Evaluations","Passkeys","Reports","ReviewBids","Tenders","TopCommittee","UnifiedReports"}.Contains(c))return NotFound();
            model="This action is unavailable in the current workflow state. Reopen the record to review its current status.";
        }else if(!known.Contains(name))return NotFound();
        var parts=name.Split('/');RouteData.Values["controller"]=parts[0];RouteData.Values["action"]=parts[1];
        ViewData["Actor"]=actor;ViewData["Details"]=details;ViewData["DepartmentId"]=dept;
        if(name=="Requisitions/Index")model=new RequisitionList(empty?[]:[q],requestActor);
        if(name is "Requisitions/Create" or "Requisitions/Edit")model=new RequisitionForm{Id=parts[1]=="Edit"?id:null,Title=parts[1]=="Edit"?q.Title:"",Reason=parts[1]=="Edit"?q.Reason:"",Priority="Medium",DepartmentName="Technology department",Today=DateOnly.FromDateTime(TenderTime.Local(now)),TargetDate=q.TargetDate,Items=[new(){Id=parts[1]=="Edit"?item.Id:null,Description=parts[1]=="Edit"?item.Description:"",Specifications=parts[1]=="Edit"?item.Specifications:"",Quantity=1}],Version="synthetic-version"};
        if(name=="Requisitions/Details")model=new RequisitionDetails(q,[item],[],[],"Technology department","Request owner","synthetic-version",requestActor);
        if(name=="Tenders/Index")model=empty?Array.Empty<Tender>():new[]{t};
        if(name is "Tenders/Create" or "Tenders/Edit")model=new TenderForm{Id=parts[1]=="Edit"?id:null,RequisitionId=q.Id,Title=parts[1]=="Edit"?t.Title:"",Description=parts[1]=="Edit"?t.Description:"",Terms=parts[1]=="Edit"?t.Terms:"",Start=TenderTime.Local(t.SubmissionStart),Deadline=TenderTime.Local(t.SubmissionDeadline),EligibleRequests=empty?[]:[q],Version="synthetic-version"};
        if(name=="Tenders/Details")model=details;
        if(name=="Tenders/Criteria")model=new CriteriaForm{Version="synthetic-version",Items=empty?[]:[new(){Id=criterion.Id,Name=criterion.Name,Description=criterion.Description,Kind=criterion.Kind,Weight=criterion.Weight}]};
        if(name=="Tenders/Inbox")model=empty?Array.Empty<InvitationRow>():new[]{invitation};
        if(name=="Tenders/Invitation")model=invitation;
        if(name=="Tenders/Invitations"){t.Status=TenderStatus.Published;model=new TenderInvitations(details,empty?[]:[invitation],empty?[]:[supplier]);}
        if(name=="Suppliers/Index")model=empty?Array.Empty<SupplierProfile>():new[]{supplier};
        if(name is "Suppliers/Details" or "Suppliers/Status")model=new SupplierReviewViewModel{Supplier=supplier,ExpectedStatus=supplier.Status,Documents=empty?[]:[new(){Id=Guid.NewGuid(),SupplierProfileId=supplier.Id,DocumentType="Company Registration Certificate",OriginalName="company.pdf",Size=1024,UploadedAt=now,ExpiresOn=DateOnly.FromDateTime(now.AddDays(12).DateTime)}]};
        if(name=="UnifiedReports/Review")model=await new UnifiedReportPresentationFixture().PageAsync(User,Guid.Parse(UnifiedReportPresentationFixture.SubmittedId));
        if(name=="Profile/Index")model=new ProfilePage{UserName=user,Contact=new(){Email="account@example.test",PhoneNumber="0790000000"}};
        if(name=="Passkeys/Index")model=new PasskeyPage(empty?[]:[new("fixture-key","Device passkey",now)]);
        if(name=="Committees/Edit")model=new CommitteePage(t,dept,new(){Version="synthetic-version"},false,empty?[]:[new("manager","Sourcing manager",dept,[ApplicationRoles.SourcingManager]),new("department","Department representative",dept,[ApplicationRoles.EvaluationCommittee]),new("financial","Financial representative",dept,[ApplicationRoles.EvaluationCommittee]),new("technical","Technical representative",dept,[ApplicationRoles.EvaluationCommittee])],[],[]);
        if(name=="Shared/Error")model=new ErrorViewModel{RequestId="synthetic-request"};
        if(name=="Shared/WorkspaceProblem")model="The requested view could not be loaded. Review your filters and try again.";
        return View("~/Views/"+name+".cshtml",model);
    }
}
