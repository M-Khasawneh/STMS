using System.Security.Cryptography;
using System.Text.Json;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Data;

public static class CommitteeRules
{
    public static readonly string[] Positions = ["Sourcing Manager", "Department Representative", "Financial Representative", "Technical Representative"];
    public static bool Manager(BidAccount account) => account.Internal && account.Roles.Contains(ApplicationRoles.SourcingManager);
    public static string[] Selection(CommitteeForm form) => [form.ManagerUserId, form.DepartmentRepresentativeId, form.FinancialRepresentativeId, form.TechnicalRepresentativeId];
    public static bool Effective(CommitteeMember member, DateTimeOffset now) =>
        (member.EffectiveFrom is null || member.EffectiveFrom <= now) && (member.EffectiveTo is null || now < member.EffectiveTo);
    public static void Validate(CommitteeForm form, IReadOnlyList<CommitteeCandidate> candidates, Guid sourceDepartment, bool started)
    {
        var selected = Selection(form);
        BidRules.Require(selected.All(x => !string.IsNullOrWhiteSpace(x)) && selected.Distinct(StringComparer.Ordinal).Count() == 4,
            400, "Choose four different employees, one for each committee position.");
        for (var i = 0; i < selected.Length; i++)
        {
            var candidate = candidates.SingleOrDefault(x => x.Id == selected[i]);
            BidRules.Require(candidate is not null, 400, "A selected employee is no longer eligible.");
            var role = i == 0 ? ApplicationRoles.SourcingManager : ApplicationRoles.EvaluationCommittee;
            BidRules.Require(candidate.Roles.Contains(role), 400, "Choose an employee with the required existing role for each position.");
            if (i == 1) BidRules.Require(candidate.DepartmentId == sourceDepartment, 400, "The department representative must belong to the source requisition's department.");
        }
        BidRules.Require((form.Reason?.Length ?? 0) <= 8000 && (!started || !string.IsNullOrWhiteSpace(form.Reason)),
            400, "A documented reason is required when changing membership after evaluation starts.");
    }
    public static string Version(IEnumerable<CommitteeMember> members, bool started) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
            started, Members = members.OrderBy(x => x.Id).Select(x => new { x.Id, x.UserId, x.Position, x.EffectiveFrom, x.EffectiveTo })
        })));
}
