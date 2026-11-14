BEGIN TRANSACTION;
DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TenderApprovals]') AND [c].[name] = N'DecidedAt');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [TenderApprovals] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [TenderApprovals] ALTER COLUMN [DecidedAt] datetimeoffset NULL;

ALTER TABLE [TenderApprovals] ADD [RoundOpenedAt] datetimeoffset NULL;

ALTER TABLE [TenderApprovals] ADD [RoundClosedAt] datetimeoffset NULL;

ALTER TABLE [TenderApprovals] ADD [RoundCloseReason] nvarchar(max) NULL;

ALTER TABLE [TenderApprovals] ADD [RoundMemberSnapshot] nvarchar(512) NULL;

ALTER TABLE [TenderApprovals] ADD CONSTRAINT [CK_TenderApprovals_TopRound] CHECK (([RoundOpenedAt] IS NULL AND [RoundMemberSnapshot] IS NULL AND [RoundClosedAt] IS NULL AND [RoundCloseReason] IS NULL AND [DecidedAt] IS NOT NULL) OR ([RoundOpenedAt] IS NOT NULL AND [RoundMemberSnapshot] IS NOT NULL AND ISJSON([RoundMemberSnapshot]) = 1 AND [Stage] = N'TopCommittee' AND [CommitteeAssignmentId] IS NOT NULL AND (([RoundClosedAt] IS NULL AND [DecidedAt] IS NULL AND [Decision] = N'RecommendationPending' AND [RoundCloseReason] IS NULL) OR ([RoundClosedAt] IS NOT NULL AND [RoundClosedAt] >= [RoundOpenedAt] AND [RoundCloseReason] IS NOT NULL AND LEN(LTRIM(RTRIM([RoundCloseReason]))) > 0 AND (([Decision] = N'RecommendationPending' AND [DecidedAt] IS NULL) OR ([Decision] IN (N'CommitteeApproved', N'CommitteeRejected', N'ReturnedForReview') AND [DecidedAt] IS NOT NULL AND [DecidedAt] >= [RoundOpenedAt] AND [DecidedAt] <= [RoundClosedAt]))))));

CREATE UNIQUE INDEX [UX_TenderApprovals_OpenTopRound] ON [TenderApprovals] ([TenderId], [Stage]) WHERE [Stage] = N'TopCommittee' AND [RoundOpenedAt] IS NOT NULL AND [RoundClosedAt] IS NULL;

CREATE UNIQUE INDEX [IX_CommitteeVotes_TenderApprovalId_CommitteeMemberId] ON [CommitteeVotes] ([TenderApprovalId], [CommitteeMemberId]);

DROP INDEX [IX_TenderAwards_TenderId] ON [TenderAwards];

CREATE UNIQUE INDEX [IX_TenderAwards_TenderId] ON [TenderAwards] ([TenderId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261114220000_TopCommitteeVotingRounds', N'10.0.15');

COMMIT;
GO
