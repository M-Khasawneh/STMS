BEGIN TRANSACTION;
CREATE TABLE [AspNetUserPasskeys] (
    [CredentialId] varbinary(1024) NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [Data] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_AspNetUserPasskeys] PRIMARY KEY ([CredentialId]),
    CONSTRAINT [FK_AspNetUserPasskeys_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id])
);

CREATE INDEX [IX_AspNetUserPasskeys_UserId] ON [AspNetUserPasskeys] ([UserId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261115034155_IdentityPasskeyStorage', N'10.0.15');

COMMIT;
GO

