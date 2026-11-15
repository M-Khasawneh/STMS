using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.Tenders;
namespace SourcingTenderManagementSystem.AuthenticationChecks;
public static class TenderRuleChecks
{
    public static void Run()
    {
        var now=new DateTimeOffset(2026,11,13,10,0,0,TimeSpan.Zero);
        using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=unused;Database=unused;Integrated Security=True").Options);
        var time=new TestClock(now); var service=new TenderManagement(db,null!,time);
        var tender=new Tender{Status=TenderStatus.Open,SubmissionStart=now,SubmissionDeadline=now.AddHours(1)};
        Check(service.InSubmissionPeriod(tender),"submission window includes its start");time.Utc=now.AddHours(1);Check(!service.InSubmissionPeriod(tender),"exact deadline excludes submissions");time.Utc=now.AddTicks(-1);Check(!service.InSubmissionPeriod(tender),"before-start submissions excluded");
        var local=new DateTime(2026,11,13,13,0,0);Check(TenderTime.Utc(local)==now&&TenderTime.Local(now)==local,"Asia/Amman input round-trips through UTC");
        var boundary=new DateTimeOffset(2026,11,15,22,30,0,TimeSpan.Zero);
        var utcHost=new RequisitionManagement(db,null!,new HostClock(boundary,TimeZoneInfo.Utc));
        var westHost=new RequisitionManagement(db,null!,new HostClock(boundary,TimeZoneInfo.CreateCustomTimeZone("TestHostWest",TimeSpan.FromHours(-8),"West","West")));
        Check(utcHost.Today==new DateOnly(2026,11,16)&&westHost.Today==utcHost.Today,"requisition submission date uses established business timezone, independent of server timezone");
        Check(!TenderManagement.Approved([]),"empty criteria cannot satisfy approval gate");
        var rows=new[]{new EvaluationCriterion{Weight=60,ApprovedByUserId="manager",ApprovedAt=now},new EvaluationCriterion{Weight=40,ApprovedByUserId="manager",ApprovedAt=now}};
        Check(TenderManagement.Approved(rows),"exactly 100 approved percentage points accepted");rows[1].Weight=39.999999m;Check(!TenderManagement.Approved(rows),"weight total is exact, not rounded");rows[1].Weight=40;rows[1].ApprovedAt=null;Check(!TenderManagement.Approved(rows),"partially approved set cannot open a tender");
        var actor=new TenderActor("user",true,true,true,true,null);Check(!actor.Manage&&!actor.Approve,"Admin is read-only with combined roles");
        Console.WriteLine("PASS: tender eligibility, percentage and UTC/deadline rule checks");
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS: "+message);}
    private sealed class HostClock(DateTimeOffset value,TimeZoneInfo zone):TimeProvider {public override DateTimeOffset GetUtcNow()=>value;public override TimeZoneInfo LocalTimeZone=>zone;}
    public sealed class TestClock(DateTimeOffset value):TimeProvider {public DateTimeOffset Utc {get;set;}=value;public override DateTimeOffset GetUtcNow()=>Utc;}
}
