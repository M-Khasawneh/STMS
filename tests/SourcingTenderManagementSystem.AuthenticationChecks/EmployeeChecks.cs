using System.Net;
using System.Text.RegularExpressions;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

public static class EmployeeChecks
{
    public static async Task RunAsync(HttpClient admin, HttpClient session, MemoryUsers users, TestEligibility eligibility, MemoryEmployeeData employeeData, string password)
    {
        var initialCount = users.Users.Count;
        var departmentId = employeeData.Departments.Keys.Single();
        var protectedUser = users.Users.Values.Single(user => user.UserName == "admin");
        var protectedId = protectedUser.Id;
        var supplierId = users.Users.Values.Single(user => user.UserName == "supplier").Id;
        var paths = new[] { "/Employees", "/Employees/Create", $"/Employees/Edit/{protectedId}" };
        foreach (var path in paths) await Expect(admin, path, HttpStatusCode.Redirect, "/Account/Login");
        var token = await Token(admin, "/Account/Login");
        await ExpectPost("/Employees/Create", Creation("blocked", ApplicationRoles.Admin, token), HttpStatusCode.Redirect, "/Account/Login");
        await ExpectPost($"/Employees/Edit/{protectedId}", new() { ["Role"] = ApplicationRoles.Supplier, ["__RequestVerificationToken"] = token }, HttpStatusCode.Redirect, "/Account/Login");
        await ExpectPost($"/Employees/SetEnabled/{protectedId}", new() { ["enabled"] = "false", ["__RequestVerificationToken"] = token }, HttpStatusCode.Redirect, "/Account/Login");
        await Login(admin, "employee", true);
        foreach (var path in paths) await Expect(admin, path, HttpStatusCode.Redirect, "/Account/AccessDenied");
        token = await Token(admin, "/");
        await ExpectPost("/Employees/Create", Creation("blocked", ApplicationRoles.Admin, token), HttpStatusCode.Redirect, "/Account/AccessDenied");
        await ExpectPost($"/Employees/Edit/{protectedId}", new() { ["Role"] = ApplicationRoles.Supplier, ["__RequestVerificationToken"] = token }, HttpStatusCode.Redirect, "/Account/AccessDenied");
        await ExpectPost($"/Employees/SetEnabled/{protectedId}", new() { ["enabled"] = "false", ["__RequestVerificationToken"] = token }, HttpStatusCode.Redirect, "/Account/AccessDenied");
        Check(users.Users.Count == initialCount && !EmployeeManagement.IsDisabled(users.Users[protectedId]), "unauthorized employee management preserves users and access");
        Check(!(await admin.GetStringAsync("/Workspace")).Contains("href=\"/Employees\""), "employee navigation is Admin-only");
        await Logout(admin);
        await Login(admin, "department-admin", true);
        var list = await admin.GetAsync("/Employees");
        Check(list.StatusCode == HttpStatusCode.OK && list.Headers.CacheControl?.NoStore == true, "Admin employee list renders without caching");
        var html = await list.Content.ReadAsStringAsync();
        Check(html.Contains("Protected Admin") && !html.Contains($"/Employees/Edit/{supplierId}"), "list identifies protected Admin and excludes supplier accounts");
        var create = await admin.GetStringAsync("/Employees/Create");
        Check(create.Contains("data-val-required") && create.Contains("autocomplete=\"new-password\"") && !create.Contains("value=\"Supplier\""), "create view validates input and offers only employee roles");
        await ExpectPost("/Employees/Create", Creation("no-csrf", ApplicationRoles.Admin, null), HttpStatusCode.BadRequest);
        token = await Token(admin, "/Employees/Create");
        var required = Creation("", "", token); required["Password"] = required["ConfirmPassword"] = "";
        var response = await Post("/Employees/Create", required);
        Check(response.StatusCode == HttpStatusCode.OK && (await response.Content.ReadAsStringAsync()).Contains("field is required"), "required employee fields are validated server-side");
        await InvalidCreate("bad-role", ApplicationRoles.Supplier, "Select existing employee roles.");
        await InvalidCreate("bad-role", "UnapprovedRole", "Select existing employee roles.");
        await InvalidCreate("bad-role", "Admin,SourcingOfficer", "Select existing employee roles.");
        await InvalidCreate("employee", ApplicationRoles.SourcingOfficer, "already taken");
        var weak = Creation("weak-password", ApplicationRoles.SourcingOfficer, token); weak["Password"] = weak["ConfirmPassword"] = "short";
        Check((await Post("/Employees/Create", weak)).StatusCode == HttpStatusCode.OK && !users.Users.Values.Any(user => user.UserName == "weak-password"), "Identity password validation prevents weak-password persistence");
        var mismatch = Creation("mismatch", ApplicationRoles.SourcingOfficer, token); mismatch["ConfirmPassword"] = "";
        var mismatchHtml = await (await Post("/Employees/Create", mismatch)).Content.ReadAsStringAsync();
        Check(!mismatchHtml.Contains(password) && !users.Users.Values.Any(user => user.UserName == "mismatch"), "password confirmation failure never echoes or saves credentials");
        var invalidEmail = Creation("bad-email", ApplicationRoles.SourcingOfficer, token); invalidEmail["Email"] = "invalid";
        Check((await Post("/Employees/Create", invalidEmail)).StatusCode == HttpStatusCode.OK && !users.Users.Values.Any(user => user.UserName == "bad-email"), "email syntax is validated");
        var noDepartment = Creation("missing-department", ApplicationRoles.SourcingOfficer, token);
        noDepartment["DepartmentId"] = "";
        var missingDepartmentResponse = await Post("/Employees/Create", noDepartment);
        Check((await missingDepartmentResponse.Content.ReadAsStringAsync()).Contains("Select a department."), "department is required on create");
        noDepartment["DepartmentId"] = Guid.NewGuid().ToString();
        var badDepartmentResponse = await Post("/Employees/Create", noDepartment);
        Check((await badDepartmentResponse.Content.ReadAsStringAsync()).Contains("Select an existing department.") && users.Users.Count == initialCount,
            "nonexistent department IDs are rejected before creating an account");
        users.FailNextUpdate = true;
        await ExpectPost("/Employees/Create", Creation("atomic-failure", ApplicationRoles.SourcingOfficer, token), HttpStatusCode.OK);
        Check(users.Users.Count == initialCount && !users.Users.Values.Any(user => user.UserName == "atomic-failure"), "failed role assignment rolls back user creation");

        var creation = Creation("managed-employee", ApplicationRoles.SourcingOfficer, token);
        creation["Roles[1]"] = ApplicationRoles.EvaluationCommittee;
        creation["Id"] = protectedId; creation["LockoutEnd"] = DateTimeOffset.MaxValue.ToString("O");
        await ExpectPost("/Employees/Create", creation, HttpStatusCode.Redirect, "/Employees");
        var managed = users.Users.Values.Single(user => user.UserName == "managed-employee");
        var id = managed.Id;
        Check(id != protectedId && managed.PasswordHash is not null && managed.PasswordHash != password && !EmployeeManagement.IsDisabled(managed), "Identity hashes the password and ignores injected account state");
        Check(users.Roles[id].SetEquals(new[] { ApplicationRoles.SourcingOfficer.ToUpperInvariant(), ApplicationRoles.EvaluationCommittee.ToUpperInvariant() }), "multiple chosen employee roles are persisted");
        Check(employeeData.Assignments[id] == departmentId, "employee creation persists exactly one department assignment");
        html = await admin.GetStringAsync("/Employees");
        Check(!html.Contains(managed.PasswordHash!) && !html.Contains(password) && html.Contains("managed-employee"), "list shows persisted account without password data");
        var editPath = $"/Employees/Edit/{id}";
        token = await Token(admin, editPath);
        await ExpectPost(editPath, new() { ["Email"] = "changed@example.invalid" }, HttpStatusCode.BadRequest);
        var update = new Dictionary<string, string> { ["Email"] = "changed@example.invalid", ["PhoneNumber"] = "+962 6 555 0100", ["Roles[0]"] = ApplicationRoles.SourcingManager, ["UpdateRoles"] = "true",
            ["Id"] = protectedId, ["UserName"] = "renamed", ["__RequestVerificationToken"] = token };
        await ExpectPost(editPath, update, HttpStatusCode.Redirect, "/Employees");
        Check(users.Users[id].Email == update["Email"] && users.Users[id].PhoneNumber == update["PhoneNumber"] && users.Users[id].UserName == "managed-employee",
            "permitted contact fields persist and account username stays unchanged");
        Check(users.Roles[id].SetEquals(new[] { ApplicationRoles.SourcingManager.ToUpperInvariant() }) && users.Users[protectedId].UserName == "admin", "role replacement targets only the route-selected employee");
        update["Roles[0]"] = ApplicationRoles.Supplier; update["Email"] = "should-not-persist@example.invalid";
        await ExpectPost(editPath, update, HttpStatusCode.OK);
        Check(users.Users[id].Email == "changed@example.invalid", "invalid role edit preserves existing contact data");
        var protectedPath = $"/Employees/Edit/{protectedId}";
        var protectedToken = await Token(admin, protectedPath);
        await ExpectPost(protectedPath, new() { ["Roles[0]"] = ApplicationRoles.SourcingOfficer, ["UpdateRoles"] = "true", ["__RequestVerificationToken"] = protectedToken }, HttpStatusCode.OK);
        await ExpectPost($"/Employees/SetEnabled/{protectedId}", new() { ["enabled"] = "false", ["__RequestVerificationToken"] = protectedToken }, HttpStatusCode.OK);
        Check(users.Roles[protectedId].SetEquals(new[] { "ADMIN" }) && !EmployeeManagement.IsDisabled(users.Users[protectedId]), "first Admin cannot be demoted or disabled through crafted requests");

        users.Roles[id].Add(ApplicationRoles.SourcingOfficer.ToUpperInvariant());
        update["Roles[0]"] = ApplicationRoles.Admin; update["UpdateRoles"] = "false";
        await ExpectPost(editPath, update, HttpStatusCode.Redirect, "/Employees");
        Check(users.Roles[id].Count == 2 && !users.Roles[id].Contains("ADMIN"), "role assignments are preserved unless explicitly changed");
        update["Roles[0]"] = ApplicationRoles.SourcingManager; update["Roles[1]"] = ApplicationRoles.TopCommittee; update["UpdateRoles"] = "true";
        await ExpectPost(editPath, update, HttpStatusCode.Redirect, "/Employees");
        Check(users.Roles[id].SetEquals(new[] { "SOURCINGMANAGER", "TOPCOMMITTEE" }), "explicit changes replace the full multiple-role selection");
        update.Remove("Roles[1]");
        update["UpdateRoles"] = "false";
        update["Email"] = "changed@example.invalid";
        users.Roles[id].Remove(ApplicationRoles.SourcingOfficer.ToUpperInvariant());

        var alternateDepartment = Guid.NewGuid();
        employeeData.Departments[alternateDepartment] = new() { Id = alternateDepartment, Name = "Operations" };
        update["DepartmentId"] = alternateDepartment.ToString();
        await ExpectPost(editPath, update, HttpStatusCode.Redirect, "/Employees");
        Check(employeeData.Assignments[id] == alternateDepartment && employeeData.Assignments[protectedId] == departmentId,
            "department edits target only the selected employee");
        update["DepartmentId"] = Guid.NewGuid().ToString();
        await ExpectPost(editPath, update, HttpStatusCode.OK);
        Check(employeeData.Assignments[id] == alternateDepartment, "nonexistent department edit preserves the existing relationship");
        update["DepartmentId"] = alternateDepartment.ToString();

        var signIn = await Login(session, "managed-employee", true);
        var oldCookie = signIn.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("__Host-SourcingTender.Auth=")).Split(';')[0];
        await Expect(session, "/Protection/Employee", HttpStatusCode.OK);
        token = await Token(admin, editPath);
        await ExpectPost($"/Employees/SetEnabled/{id}", new() { ["enabled"] = "false", ["__RequestVerificationToken"] = token }, HttpStatusCode.Redirect, "/Employees");
        Check(EmployeeManagement.IsDisabled(users.Users[id]), "disabled status persists in existing Identity lockout fields");
        await Expect(session, "/Protection/Employee", HttpStatusCode.Redirect, "/Account/Login");
        await Login(session, "managed-employee", false);
        await ExpectPost($"/Employees/SetEnabled/{id}", new() { ["enabled"] = "true", ["__RequestVerificationToken"] = token }, HttpStatusCode.Redirect, "/Employees");
        Check(!EmployeeManagement.IsDisabled(users.Users[id]), "enabling restores account eligibility");
        using (var replay = new HttpRequestMessage(HttpMethod.Get, "/Protection/Employee"))
        {
            replay.Headers.TryAddWithoutValidation("Cookie", oldCookie);
            Check((await session.SendAsync(replay)).StatusCode == HttpStatusCode.Redirect, "old session remains revoked after re-enabling");
        }
        await Login(session, "managed-employee", true);
        update["Roles[0]"] = ApplicationRoles.TopCommittee; update["UpdateRoles"] = "true";
        await ExpectPost(editPath, update, HttpStatusCode.Redirect, "/Employees");
        await Expect(session, "/Protection/Employee", HttpStatusCode.Redirect, "/Account/Login");
        Check(users.Roles[id].SetEquals(new[] { ApplicationRoles.TopCommittee.ToUpperInvariant() }), "role updates persist and invalidate existing sessions");

        await ExpectPost($"/Employees/SetEnabled/{id}", new() { ["enabled"] = "false" }, HttpStatusCode.BadRequest);
        await ExpectPost($"/Employees/SetEnabled/{id}", new() { ["__RequestVerificationToken"] = token }, HttpStatusCode.BadRequest);
        await ExpectPost($"/Employees/SetEnabled/{id}", new() { ["enabled"] = "invalid", ["__RequestVerificationToken"] = token }, HttpStatusCode.BadRequest);
        await Expect(admin, $"/Employees/SetEnabled/{id}", HttpStatusCode.MethodNotAllowed);
        await Expect(admin, "/Employees/Edit/missing", HttpStatusCode.NotFound);
        await Expect(admin, "/Employees/Edit", HttpStatusCode.NotFound);
        await Expect(admin, $"/Employees/Edit/{supplierId}", HttpStatusCode.NotFound);
        await ExpectPost($"/Employees/SetEnabled/{supplierId}", new() { ["enabled"] = "false", ["__RequestVerificationToken"] = token }, HttpStatusCode.NotFound);
        eligibility.SupplierProfileUsers.Add(id);
        await Expect(admin, editPath, HttpStatusCode.NotFound);
        Check(!(await admin.GetStringAsync("/Employees")).Contains($"/Employees/Edit/{id}"), "supplier-profile accounts are excluded even without the Supplier role");
        eligibility.SupplierProfileUsers.Remove(id);
        await Expect(admin, $"/Employees/Delete/{id}", HttpStatusCode.NotFound);
        await Logout(admin);
        Console.WriteLine("PASS: employee HTTPS MVC checks with real Identity and in-memory transactions; no live account changes.");

        Dictionary<string, string> Creation(string name, string role, string? csrf)
        {
            var values = new Dictionary<string, string> { ["UserName"] = name, ["Roles[0]"] = role, ["Password"] = password, ["ConfirmPassword"] = password };
            if (csrf is not null) values["__RequestVerificationToken"] = csrf;
            return values;
        }
        async Task InvalidCreate(string name, string role, string expected)
        {
            var result = await Post("/Employees/Create", Creation(name, role, token));
            var body = await result.Content.ReadAsStringAsync();
            Check(result.StatusCode == HttpStatusCode.OK && body.Contains(expected) && !body.Contains(password), "invalid employee creation is safely rejected");
        }
        async Task<HttpResponseMessage> Login(HttpClient browser, string name, bool succeeds)
        {
            var result = await browser.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["UserName"] = name, ["Password"] = password, ["__RequestVerificationToken"] = await Token(browser, "/Account/Login") }));
            Check(succeeds ? result.StatusCode == HttpStatusCode.Redirect : result.StatusCode == HttpStatusCode.OK && (await result.Content.ReadAsStringAsync()).Contains("Unable to sign in."), "employee sign-in respects account status");
            return result;
        }
        async Task Logout(HttpClient browser)
        {
            var result = await browser.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = await Token(browser, "/") }));
            Check(result.StatusCode == HttpStatusCode.Redirect, "employee-management test session ends");
        }
        Task<HttpResponseMessage> Post(string path, Dictionary<string, string> values)
        {
            if ((path == "/Employees/Create" || path.StartsWith("/Employees/Edit/")) && !values.ContainsKey("DepartmentId"))
                values["DepartmentId"] = departmentId.ToString();
            return admin.PostAsync(path, new FormUrlEncodedContent(values));
        }
        async Task ExpectPost(string path, Dictionary<string, string> values, HttpStatusCode status, string? location = null)
        {
            var result = await Post(path, values);
            Check(result.StatusCode == status && (location is null || result.Headers.Location?.OriginalString.Contains(location) == true), $"employee POST {path} returns {(int)status}");
        }
    }

    private static async Task<string> Token(HttpClient browser, string path)
    {
        var response = await browser.GetAsync(path);
        if (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location?.OriginalString == "/Workspace") response = await browser.GetAsync("/Workspace");
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!match.Success) throw new InvalidOperationException("Missing employee test antiforgery token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
    private static async Task Expect(HttpClient browser, string path, HttpStatusCode status, string? location = null)
    {
        var result = await browser.GetAsync(path);
        Check(result.StatusCode == status && (location is null || result.Headers.Location?.OriginalString.Contains(location) == true), $"employee GET {path} returns {(int)status}");
    }
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + description);
        Console.WriteLine("PASS: " + description);
    }
}
