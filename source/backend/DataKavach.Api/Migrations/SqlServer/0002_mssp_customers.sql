-- MSSP model: customer portal users are bound to one organisation
ALTER TABLE [Users] ADD [UserType] NVARCHAR(20) NULL, [OrganizationId] BIGINT NULL
-- go
UPDATE [Users] SET [UserType] = 'Provider' WHERE [UserType] IS NULL
-- go
CREATE INDEX [IX_Users_OrganizationId] ON [Users] ([OrganizationId])
