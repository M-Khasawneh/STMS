using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

public static class FirstAdminChecks
{
    public static async Task RunAsync()
    {
        // Orchestration tests use empty input and a simulated password-creation step.
        // No password is generated/hardcoded, and no database connection is opened.
        using (var test = new Fixture())
        {
            test.Console.IsInteractive = false;
            Check(await test.Run() == 1 && test.Database.Checks == 0 && test.Console.PasswordReads == 0,
                "redirected console is refused before database access or credential input");
        }
        using (var test = new Fixture())
        {
            test.RoleStore.Roles.Clear();
            Check(await test.Run() == 1 && test.Console.PasswordReads == 0 && !test.Database.Began,
                "missing Admin role is refused without creating roles");
        }
        using (var test = new Fixture())
        {
            test.Store.Users["existing"] = new IdentityUser("existing");
            Check(await test.Run() == 1 && test.Console.PasswordReads == 0 && !test.Database.Began && test.Store.Users.Count == 1,
                "any existing user blocks provisioning and remains unchanged");
        }
        using (var test = new Fixture())
        {
            test.Console.MismatchedConfirmation = true;
            Check(await test.Run() == 1 && !test.Database.Began && test.Store.Users.Count == 0,
                "password mismatch causes no database writes");
        }
        using (var test = new Fixture())
        {
            test.Console.CancelPassword = true;
            Check(await test.Run() == 1 && !test.Database.Began && test.Store.Users.Count == 0,
                "cancellation before transaction creates no account");
        }
        using (var test = new Fixture())
        {
            test.Users.SimulatePasswordCreation = false;
            Check(await test.Run() == 1 && test.Database.RolledBack && test.Store.Users.Count == 0,
                "real Identity password validation rejects empty input without persisting an account");
        }
        using (var test = new Fixture())
        {
            test.Database.OnBegin = () => test.Store.Users["other"] = new IdentityUser("other");
            Check(await test.Run() == 1 && test.Users.CreateCalls == 0 && test.Database.RolledBack && test.Store.Users.Count == 1,
                "user appearing during prompts is caught by the locked recheck");
        }
        using (var test = new Fixture())
        {
            test.Database.OnBegin = () => test.RoleStore.Roles.Clear();
            Check(await test.Run() == 1 && test.Users.CreateCalls == 0 && test.Database.RolledBack,
                "Admin role disappearing during prompts is refused");
        }
        using (var test = new Fixture())
        {
            test.Users.FailRoleAssignment = true;
            Check(await test.Run() == 1 && test.Database.RolledBack && test.Store.Users.Count == 0 && test.Store.Roles.Count == 0,
                "role assignment failure rolls back account creation");
        }
        using (var test = new Fixture())
        {
            test.Users.ThrowOnRoleAssignment = true;
            Check(await test.Run() == 1 && test.Database.RolledBack && test.Store.Users.Count == 0 &&
                !test.Console.Output.Any(line => line.Contains("private diagnostic")),
                "exception rolls back without exposing exception details");
        }
        using (var test = new Fixture())
        {
            test.Users.AddUnexpectedRole = true;
            Check(await test.Run() == 1 && test.Database.RolledBack && test.Store.Users.Count == 0 && test.Store.Roles.Count == 0,
                "unexpected additional role causes rollback");
        }
        using (var test = new Fixture())
        {
            test.Database.FailCommit = true;
            Check(await test.Run() == 1 && test.Database.RolledBack && test.Store.Users.Count == 0,
                "commit failure leaves no simulated partial account");
        }
        using (var test = new Fixture())
        {
            test.Options.User.RequireUniqueEmail = true;
            test.Console.Email = string.Empty;
            Check(await test.Run() == 1 && test.Console.EmailReads == 1 && test.Console.PasswordReads == 0,
                "required email is validated before password collection");
        }
        using (var test = new Fixture())
        {
            test.Options.User.RequireUniqueEmail = true;
            test.Console.Email = "admin@example.invalid";
            Check(await test.Run() == 0 && test.Store.Users.Values.Single().Email == test.Console.Email,
                "email is requested when required by existing Identity options");
            Check(!test.Store.Users.Values.Single().EmailConfirmed, "email confirmation is never silently bypassed");
        }
        using (var test = new Fixture())
        {
            Check(await test.Run() == 0 && test.Database.Committed && !test.Database.RolledBack &&
                test.Console.PasswordReads == 2 && test.Console.EmailReads == 0, "matching input commits once; optional email is not invented");
            var user = test.Store.Users.Values.Single();
            Check(user.UserName == "admin" && (await test.Users.GetRolesAsync(user)).SequenceEqual(new[] { ApplicationRoles.Admin }),
                "only the fixed admin username and existing Admin role are used");
            Check(test.RoleStore.Roles.Count == 1, "provisioning does not initialize or recreate roles");
            var reads = test.Console.PasswordReads;
            Check(await test.Run() == 1 && test.Console.PasswordReads == reads && test.Store.Users.Count == 1,
                "second provisioning attempt is refused without modifying the account");
        }
        Console.WriteLine("PASS: first Admin provisioning safeguard checks; no SQL access or persistent test accounts.");
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + description);
        Console.WriteLine("PASS: " + description);
    }

    private sealed class Fixture : IDisposable
    {
        public MemoryUsers Store { get; } = new();
        public MemoryRoles RoleStore { get; } = new();
        public TestConsole Console { get; } = new();
        public IdentityOptions Options { get; } = new();
        public TestUsers Users { get; }
        public RoleManager<IdentityRole> Roles { get; }
        public TestDatabase Database { get; }
        public Fixture()
        {
            Users = new TestUsers(Store);
            Roles = new RoleManager<IdentityRole>(RoleStore, [new RoleValidator<IdentityRole>()],
                new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), NullLogger<RoleManager<IdentityRole>>.Instance);
            var role = new IdentityRole(ApplicationRoles.Admin) { NormalizedName = "ADMIN" };
            RoleStore.Roles[role.Id] = role;
            Database = new TestDatabase(Store);
        }
        public Task<int> Run() => new FirstAdminProvisioner(Users, Roles, Database, Console,
            Microsoft.Extensions.Options.Options.Create(Options)).RunAsync();
        public void Dispose() { Users.Dispose(); Roles.Dispose(); }
    }

    private sealed class TestConsole : IFirstAdminConsole
    {
        public bool IsInteractive { get; set; } = true;
        public bool MismatchedConfirmation { get; set; }
        public bool CancelPassword { get; set; }
        public string Email { get; set; } = string.Empty;
        public int EmailReads { get; private set; }
        public int PasswordReads { get; private set; }
        public List<string> Output { get; } = [];
        public void WriteLine(string message) => Output.Add(message);
        public string ReadLine(string prompt) { EmailReads++; return Email; }
        public string ReadPassword(string prompt)
        {
            PasswordReads++;
            if (CancelPassword) throw new OperationCanceledException();
            // Null is a mismatch sentinel in this fake, never a real credential.
            return MismatchedConfirmation && PasswordReads == 2 ? null! : string.Empty;
        }
    }

    private sealed class TestUsers(MemoryUsers store) : UserManager<IdentityUser>(store,
        Microsoft.Extensions.Options.Options.Create(new IdentityOptions()), new PasswordHasher<IdentityUser>(),
        [new UserValidator<IdentityUser>()], [new PasswordValidator<IdentityUser>()],
        new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!, NullLogger<UserManager<IdentityUser>>.Instance)
    {
        public bool SimulatePasswordCreation { get; set; } = true;
        public bool FailRoleAssignment { get; set; }
        public bool ThrowOnRoleAssignment { get; set; }
        public bool AddUnexpectedRole { get; set; }
        public int CreateCalls { get; private set; }
        public override Task<IdentityResult> CreateAsync(IdentityUser user, string password)
        {
            CreateCalls++;
            // Successful-path tests simulate the password operation with a passwordless
            // memory-only identity; the rejection test runs the real password validator.
            return SimulatePasswordCreation ? base.CreateAsync(user) : base.CreateAsync(user, password);
        }
        public override async Task<IdentityResult> AddToRoleAsync(IdentityUser user, string role)
        {
            if (ThrowOnRoleAssignment) throw new InvalidOperationException("private diagnostic");
            if (FailRoleAssignment) return IdentityResult.Failed(new IdentityError { Code = "TestFailure" });
            var result = await base.AddToRoleAsync(user, role);
            if (AddUnexpectedRole) await base.AddToRoleAsync(user, ApplicationRoles.SourcingOfficer);
            return result;
        }
    }

    private sealed class TestDatabase(MemoryUsers store) : IFirstAdminDatabase
    {
        public int Checks { get; private set; }
        public bool Began { get; private set; }
        public bool Committed { get; set; }
        public bool RolledBack { get; set; }
        public bool FailCommit { get; set; }
        public Action? OnBegin { get; set; }
        public Task<bool> AnyUsersAsync() { Checks++; return Task.FromResult(store.Users.Count > 0); }
        public Task<IFirstAdminTransaction> BeginExclusiveAsync()
        {
            Began = true;
            OnBegin?.Invoke();
            return Task.FromResult<IFirstAdminTransaction>(new TestTransaction(this, store));
        }
    }

    private sealed class TestTransaction(TestDatabase database, MemoryUsers store) : IFirstAdminTransaction
    {
        private readonly Dictionary<string, IdentityUser> originalUsers = new(store.Users);
        private readonly Dictionary<string, HashSet<string>> originalRoles = store.Roles.ToDictionary(pair => pair.Key, pair => new HashSet<string>(pair.Value));
        public Task CommitAsync()
        {
            if (database.FailCommit) throw new InvalidOperationException("private diagnostic");
            database.Committed = true;
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync()
        {
            if (!database.Committed)
            {
                database.RolledBack = true;
                store.Users.Clear();
                store.Roles.Clear();
                foreach (var pair in originalUsers) store.Users[pair.Key] = pair.Value;
                foreach (var pair in originalRoles) store.Roles[pair.Key] = pair.Value;
            }
            return ValueTask.CompletedTask;
        }
    }
}
