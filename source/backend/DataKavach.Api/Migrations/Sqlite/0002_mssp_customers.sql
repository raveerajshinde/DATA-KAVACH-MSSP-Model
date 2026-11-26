ALTER TABLE "Users" ADD COLUMN "UserType" TEXT
-- go
ALTER TABLE "Users" ADD COLUMN "OrganizationId" INTEGER
-- go
UPDATE "Users" SET "UserType" = 'Provider' WHERE "UserType" IS NULL
-- go
CREATE INDEX "IX_Users_OrganizationId" ON "Users" ("OrganizationId")
