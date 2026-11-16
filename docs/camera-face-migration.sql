BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116032101_CameraFaceSettings'
)
BEGIN
    CREATE TABLE [FaceCredentials] (
        [UserId] nvarchar(450) NOT NULL,
        [ProtectedTemplate] varbinary(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_FaceCredentials] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_FaceCredentials_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116032101_CameraFaceSettings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261116032101_CameraFaceSettings', N'10.0.15');
END;

COMMIT;
GO
