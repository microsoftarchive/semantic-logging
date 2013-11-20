/* NOT SUPPORTED IN SQL AZURE: Both the CREATE DATABASE and DROP DATABASE statements must be in a seperate file.
IF EXISTS (SELECT name FROM master.dbo.sysdatabases WHERE name = N'Logging')
	DROP DATABASE [Logging]
GO
*/

CREATE DATABASE [Logging]
/* NOT SUPPORTED IN SQL AZURE
 COLLATE SQL_Latin1_General_CP1_CI_AS
*/
GO