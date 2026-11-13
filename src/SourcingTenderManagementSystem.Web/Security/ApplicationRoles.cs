namespace SourcingTenderManagementSystem.Web.Security;

public static class ApplicationRoles
{
    public const string Admin = "Admin";
    public const string RequestingDepartment = "RequestingDepartment";
    public const string SourcingOfficer = "SourcingOfficer";
    public const string EvaluationCommittee = "TechnicalFinancialEvaluationCommittee";
    public const string Supplier = "Supplier";
    public const string SourcingManager = "SourcingManager";
    public const string TopCommittee = "TopCommittee";

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[]
    {
        Admin, RequestingDepartment, SourcingOfficer, EvaluationCommittee,
        Supplier, SourcingManager, TopCommittee
    });
    public static string[] EmployeeRoles => All.Where(role => role != Supplier).ToArray();
}
