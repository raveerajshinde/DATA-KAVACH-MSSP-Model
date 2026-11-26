-- Optional: create the database manually (the installer does this automatically).
-- Run in SQL Server Management Studio as a sysadmin, then run install.ps1 with -DatabaseName DataKavach.
IF DB_ID(N'DataKavach') IS NULL CREATE DATABASE [DataKavach];
GO
-- Schema objects are created by the application's migration runner (dotnet DataKavach.Api.dll --migrate),
-- which records applied scripts in dbo.SchemaVersions. 0001_initial_SqlServer.sql is provided for review.
