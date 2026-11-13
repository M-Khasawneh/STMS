BEGIN TRANSACTION;
DROP INDEX [IX_TenderPurchaseRequisitions_PurchaseRequisitionId] ON [TenderPurchaseRequisitions];

DROP INDEX [IX_Invitations_TenderSupplierId] ON [Invitations];

CREATE UNIQUE INDEX [IX_TenderPurchaseRequisitions_PurchaseRequisitionId] ON [TenderPurchaseRequisitions] ([PurchaseRequisitionId]);

CREATE UNIQUE INDEX [IX_TenderPurchaseRequisitions_TenderId] ON [TenderPurchaseRequisitions] ([TenderId]);

CREATE UNIQUE INDEX [IX_TenderSuppliers_TenderId_SupplierProfileId] ON [TenderSuppliers] ([TenderId], [SupplierProfileId]);

CREATE UNIQUE INDEX [IX_Invitations_TenderSupplierId] ON [Invitations] ([TenderSupplierId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261113180000_TenderInvitationIntegrity', N'10.0.15');

COMMIT;
GO

