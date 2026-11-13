BEGIN TRANSACTION;
DECLARE @UserCount bigint = (SELECT COUNT_BIG(*) FROM [dbo].[AspNetUsers] WITH (TABLOCKX, HOLDLOCK));
IF @UserCount <> 1 OR NOT EXISTS (
    SELECT 1 FROM [dbo].[AspNetUsers] u
    JOIN [dbo].[AspNetUserRoles] ur ON ur.UserId=u.Id
    JOIN [dbo].[AspNetRoles] r ON r.Id=ur.RoleId
    WHERE u.UserName=N'admin' AND u.NormalizedUserName=N'ADMIN' AND r.Name=N'Admin')
    THROW 51000, 'Employee department migration requires the reviewed first Admin account; inspect existing users before proceeding.', 1;
IF EXISTS (SELECT 1 FROM [dbo].[SupplierProfiles]) OR EXISTS (
    SELECT 1 FROM [dbo].[AspNetUserRoles] ur JOIN [dbo].[AspNetRoles] r ON r.Id=ur.RoleId WHERE r.Name=N'Supplier')
    THROW 51001, 'Unexpected supplier account data requires review before department assignment.', 1;
IF (SELECT COUNT(*) FROM [dbo].[Departments] WITH (UPDLOCK, HOLDLOCK) WHERE Name=N'Administration') > 1
    OR EXISTS (SELECT 1 FROM [dbo].[Departments] WHERE Name=N'Administration'
        AND Name COLLATE Latin1_General_100_BIN2 <> N'Administration')
    THROW 51002, 'Conflicting Administration departments require review; no record will be overwritten.', 1;

CREATE TABLE [EmployeeDepartments] (
    [UserId] nvarchar(450) NOT NULL,
    [DepartmentId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeeDepartments] PRIMARY KEY ([UserId]),
    CONSTRAINT [FK_EmployeeDepartments_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [FK_EmployeeDepartments_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id])
);

CREATE INDEX [IX_EmployeeDepartments_DepartmentId] ON [EmployeeDepartments] ([DepartmentId]);

DECLARE @DepartmentId uniqueidentifier = (SELECT Id FROM [dbo].[Departments] WHERE Name=N'Administration');
IF @DepartmentId IS NULL
BEGIN
    SET @DepartmentId=NEWID();
    INSERT INTO [dbo].[Departments] (Id,Name) VALUES (@DepartmentId,N'Administration');
END;
INSERT INTO [dbo].[EmployeeDepartments] (UserId,DepartmentId)
    SELECT Id,@DepartmentId FROM [dbo].[AspNetUsers] WHERE NormalizedUserName=N'ADMIN' AND UserName=N'admin';

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261113001222_EmployeeDepartmentAssignment', N'10.0.15');

COMMIT;
GO

