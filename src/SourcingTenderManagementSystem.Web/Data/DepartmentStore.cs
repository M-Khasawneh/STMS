using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.Web.Data;

public interface IDepartmentStore
{
    Task<IReadOnlyList<Department>> ListAsync(CancellationToken cancellationToken);
    Task<Department?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task CreateAsync(string name, CancellationToken cancellationToken);
    Task<bool> RenameAsync(Guid id, string name, CancellationToken cancellationToken);
}

public sealed class DepartmentStore(ApplicationDbContext database) : IDepartmentStore
{
    public async Task<IReadOnlyList<Department>> ListAsync(CancellationToken cancellationToken) =>
        await database.Departments.AsNoTracking().OrderBy(department => department.Name)
            .ThenBy(department => department.Id).ToListAsync(cancellationToken);

    public Task<Department?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        database.Departments.AsNoTracking().SingleOrDefaultAsync(department => department.Id == id, cancellationToken);

    public async Task CreateAsync(string name, CancellationToken cancellationToken)
    {
        database.Departments.Add(new Department { Id = Guid.NewGuid(), Name = name });
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RenameAsync(Guid id, string name, CancellationToken cancellationToken)
    {
        var department = await database.Departments.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (department is null) return false;
        // Update only the existing row's name; retain its key and all relationships.
        department.Name = name;
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return false; } // Removed after lookup.
        return true;
    }
}
