BEGIN TRANSACTION;
DROP INDEX [IX_SupplierProfiles_UserId] ON [SupplierProfiles];

CREATE UNIQUE INDEX [IX_SupplierProfiles_UserId] ON [SupplierProfiles] ([UserId]);

CREATE UNIQUE INDEX [IX_SupplierProfiles_RegistrationNumber] ON [SupplierProfiles] ([RegistrationNumber]);

CREATE UNIQUE INDEX [IX_SupplierProfiles_TaxNumber] ON [SupplierProfiles] ([TaxNumber]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261113120000_SupplierRegistrationUniqueness', N'10.0.15');

COMMIT;
GO

