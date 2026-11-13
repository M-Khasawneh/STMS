using System.Net;
using System.Text.RegularExpressions;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

public static class DepartmentChecks
{
    public static async Task RunAsync(HttpClient client, MemoryDepartments store, string password)
    {
        var missing = Guid.NewGuid();
        var paths = new[] { "/Departments", "/Departments/Create", $"/Departments/Edit/{missing}" };
        foreach (var path in paths) await Expect(path, HttpStatusCode.Redirect, "/Account/Login");
        var writes = store.Writes;
        var token = await Token("/Account/Login");
        await ExpectPost("/Departments/Create", "Unauthorized", token, HttpStatusCode.Redirect, "/Account/Login");
        await ExpectPost($"/Departments/Edit/{missing}", "Unauthorized", token, HttpStatusCode.Redirect, "/Account/Login");
        Check(store.Writes == writes && store.Reads == 0, "anonymous department access never reaches persistence");

        await Login("employee");
        foreach (var path in paths) await Expect(path, HttpStatusCode.Redirect, "/Account/AccessDenied");
        token = await Token("/Workspace");
        await ExpectPost("/Departments/Create", "Unauthorized", token, HttpStatusCode.Redirect, "/Account/AccessDenied");
        await ExpectPost($"/Departments/Edit/{missing}", "Unauthorized", token, HttpStatusCode.Redirect, "/Account/AccessDenied");
        Check(store.Writes == writes && store.Reads == 0, "non-Admin cannot read or modify departments");
        var employeeHome = await client.GetStringAsync("/Workspace");
        Check(!employeeHome.Contains("href=\"/Departments\""), "department navigation is absent for non-Admin");
        await Logout();

        await Login("department-admin");
        var adminHome = await client.GetStringAsync("/Workspace");
        Check(adminHome.Contains("href=\"/Departments\""), "Admin can discover department navigation");
        var index = await client.GetAsync("/Departments");
        Check(index.StatusCode == HttpStatusCode.OK && (await index.Content.ReadAsStringAsync()).Contains("No departments have been created."), "Admin department list supports empty state");
        Check(index.Headers.CacheControl?.NoStore == true, "department pages are not cached");
        var create = await client.GetStringAsync("/Departments/Create");
        Check(create.Contains("data-val-required") && create.Contains("data-val-length-max=\"256\"") && create.Contains("jquery.validate"), "create form includes client validation and schema length limit");
        Check(create.Contains("col-12 col-md-8 col-lg-6"), "form uses responsive layout");
        await ExpectPost("/Departments/Create", "No token", null, HttpStatusCode.BadRequest);
        Check(store.Writes == writes, "missing create antiforgery token prevents writes");
        await ExpectPost("/Departments/Create", "Invalid token", "invalid", HttpStatusCode.BadRequest);
        token = await Token("/Departments/Create");
        await Invalid("/Departments/Create", "", token, "Enter a department name.");
        await Invalid("/Departments/Create", "   ", token, "Enter a department name.");
        await Invalid("/Departments/Create", new string('a', 257), token, "256 characters or fewer");
        Check(store.Writes == writes, "invalid create forms do not write data");

        var spoofedId = Guid.NewGuid();
        var created = await client.PostAsync("/Departments/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "  Finance  ", ["Id"] = spoofedId.ToString(), ["UserId"] = "untrusted", ["__RequestVerificationToken"] = token
        }));
        Check(created.StatusCode == HttpStatusCode.Redirect && created.Headers.Location?.OriginalString == "/Departments", "valid Admin create redirects to list");
        var department = store.Departments.Values.Single();
        var departmentId = department.Id;
        Check(department.Name == "Finance" && departmentId != Guid.Empty && departmentId != spoofedId, "create trims name and ignores client-supplied identifiers");
        Check((await client.GetStringAsync("/Departments")).Contains("Department created."), "creation confirmation is shown after redirect");
        var editPath = $"/Departments/Edit/{departmentId}";
        var edit = await client.GetStringAsync(editPath);
        Check(edit.Contains("value=\"Finance\"") && edit.Contains($"action=\"{editPath}\""), "edit form loads the existing department and route identity");
        var previousWrites = store.Writes;
        await ExpectPost(editPath, "No token", null, HttpStatusCode.BadRequest);
        Check(store.Writes == previousWrites && department.Name == "Finance", "missing edit antiforgery token preserves existing data");
        token = await Token(editPath);
        await Invalid(editPath, "  ", token, "Enter a department name.");
        await Invalid(editPath, new string('a', 257), token, "256 characters or fewer");
        Check(store.Writes == previousWrites && department.Name == "Finance", "invalid edit preserves the stored name");

        // An unrelated department and requisition reference must survive a rename.
        var unrelatedId = Guid.NewGuid();
        store.Departments[unrelatedId] = new Department { Id = unrelatedId, Name = "Operations" };
        var requisition = new PurchaseRequisition { DepartmentId = departmentId };
        var updated = await client.PostAsync(editPath, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "Finance & Planning", ["Id"] = unrelatedId.ToString(), ["__RequestVerificationToken"] = token
        }));
        Check(updated.StatusCode == HttpStatusCode.Redirect && store.Departments[departmentId].Name == "Finance & Planning", "Admin edits the route-selected department");
        Check(store.Departments[unrelatedId].Name == "Operations" && department.Id == departmentId && requisition.DepartmentId == departmentId,
            "posted identity cannot select another row; department key and references remain stable");
        Check((await client.GetStringAsync("/Departments")).Contains("Department updated."), "edit confirmation appears after redirect");

        await Expect($"/Departments/Edit/{missing}", HttpStatusCode.NotFound);
        await Expect("/Departments/Edit/invalid-id", HttpStatusCode.NotFound);
        await Expect("/Departments/Edit", HttpStatusCode.NotFound);
        token = await Token(editPath);
        await ExpectPost($"/Departments/Edit/{missing}", "Missing", token, HttpStatusCode.NotFound);
        await ExpectPost("/Departments/Edit/invalid-id", "Invalid", token, HttpStatusCode.NotFound);
        store.DisappearOnRename = true;
        await ExpectPost(editPath, "Concurrent change", token, HttpStatusCode.NotFound);
        store.DisappearOnRename = false;
        Check(store.Departments[departmentId].Name == "Finance & Planning", "missing/concurrently unavailable edit targets are not inserted");

        await ExpectPost(editPath, "<script>alert(1)</script>", token, HttpStatusCode.Redirect, "/Departments");
        var encoded = await client.GetStringAsync("/Departments");
        Check(encoded.Contains("&lt;script&gt;") && !encoded.Contains("<script>alert(1)</script>"), "department names are HTML encoded");
        await ExpectPost("/Departments/Create", new string('a', 256), await Token("/Departments/Create"), HttpStatusCode.Redirect, "/Departments");
        Check(store.Departments.Values.Any(item => item.Name.Length == 256), "name at the existing 256-character limit is accepted");
        await Expect($"/Departments/Delete/{departmentId}", HttpStatusCode.NotFound);
        await ExpectPost($"/Departments/Delete/{departmentId}", "Delete", await Token(editPath), HttpStatusCode.NotFound);
        Check(store.Departments.Count == 3, "no department deletion endpoint is exposed");
        await Logout();
        await Expect("/Departments", HttpStatusCode.Redirect, "/Account/Login");
        Console.WriteLine("PASS: department HTTPS MVC checks; all department changes stayed in memory.");

        async Task Login(string userName)
        {
            var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["UserName"] = userName, ["Password"] = password, ["__RequestVerificationToken"] = await Token("/Account/Login")
            }));
            Check(response.StatusCode == HttpStatusCode.Redirect, "temporary department-test user signs in");
        }
        async Task Logout()
        {
            var response = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = await Token("/Workspace") }));
            Check(response.StatusCode == HttpStatusCode.Redirect, "department-test session signs out");
        }
        async Task<string> Token(string path)
        {
            var html = await client.GetStringAsync(path);
            var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            if (!match.Success) throw new InvalidOperationException("Antiforgery token missing from " + path);
            return WebUtility.HtmlDecode(match.Groups[1].Value);
        }
        async Task Expect(string path, HttpStatusCode status, string? location = null)
        {
            var response = await client.GetAsync(path);
            Check(response.StatusCode == status && (location is null || response.Headers.Location?.OriginalString.Contains(location) == true), $"GET {path} returns {(int)status}");
        }
        async Task ExpectPost(string path, string name, string? csrf, HttpStatusCode status, string? location = null)
        {
            var response = await Post(path, name, csrf);
            Check(response.StatusCode == status && (location is null || response.Headers.Location?.OriginalString.Contains(location) == true), $"POST {path} returns {(int)status}");
        }
        Task<HttpResponseMessage> Post(string path, string name, string? csrf)
        {
            var values = new Dictionary<string, string> { ["Name"] = name };
            if (csrf is not null) values["__RequestVerificationToken"] = csrf;
            return client.PostAsync(path, new FormUrlEncodedContent(values));
        }
        async Task Invalid(string path, string name, string csrf, string expected)
        {
            var response = await Post(path, name, csrf);
            Check(response.StatusCode == HttpStatusCode.OK && (await response.Content.ReadAsStringAsync()).Contains(expected), "invalid department name redisplays validation errors");
        }
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + description);
        Console.WriteLine("PASS: " + description);
    }
}

public sealed class MemoryDepartments : IDepartmentStore
{
    public Dictionary<Guid, Department> Departments { get; } = [];
    public int Reads { get; private set; }
    public int Writes { get; private set; }
    public bool DisappearOnRename { get; set; }
    public Task<IReadOnlyList<Department>> ListAsync(CancellationToken cancellationToken)
    {
        Reads++;
        return Task.FromResult<IReadOnlyList<Department>>(Departments.Values.OrderBy(item => item.Name).ThenBy(item => item.Id).ToList());
    }
    public Task<Department?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        Reads++;
        return Task.FromResult(Departments.GetValueOrDefault(id));
    }
    public Task CreateAsync(string name, CancellationToken cancellationToken)
    {
        Writes++;
        var department = new Department { Id = Guid.NewGuid(), Name = name };
        Departments.Add(department.Id, department);
        return Task.CompletedTask;
    }
    public Task<bool> RenameAsync(Guid id, string name, CancellationToken cancellationToken)
    {
        if (DisappearOnRename || !Departments.TryGetValue(id, out var department)) return Task.FromResult(false);
        Writes++;
        department.Name = name;
        return Task.FromResult(true);
    }
}
