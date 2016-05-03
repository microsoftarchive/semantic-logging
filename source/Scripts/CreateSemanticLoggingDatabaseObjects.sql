SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE TYPE TracesType AS TABLE
(
	[InstanceName] [nvarchar](1000),
	[ProviderId] [uniqueidentifier],
	[ProviderName] [nvarchar](500),
	[EventId] [int],
	[EventKeywords] [bigint],
	[Level] [int],
	[Opcode] [int],
	[Task] [int],
	[Timestamp] [datetimeoffset](7),
	[Version] [int],
	[FormattedMessage] [nvarchar](4000),
	[Payload] [nvarchar](4000),
	[ActivityId] [uniqueidentifier], 
	[RelatedActivityId] [uniqueidentifier],
	[ProcessId] [int],
	[ThreadId] [int]
);

GO

-- The name of this stored procedure should match to the storedProcedureName parameter when creating the sink
CREATE PROCEDURE [dbo].[WriteTraces]
(
  @InsertTraces TracesType READONLY
)
AS
BEGIN
  INSERT INTO [Traces] (
		[InstanceName],
		[ProviderId],
		[ProviderName],
		[EventId],
		[EventKeywords],
		[Level],
		[Opcode],
		[Task],
		[Timestamp],
		[Version],
		[FormattedMessage],
		[Payload],
		[ActivityId],
		[RelatedActivityId],
		[ProcessId],
		[ThreadId]
	)
  SELECT * FROM @InsertTraces;
END

GO

-- The name of this table should match to the tableName parameter when creating the sink
CREATE TABLE [dbo].[Traces](
	[id] [bigint] IDENTITY(1,1) NOT NULL,
	[InstanceName] [nvarchar](1000) NOT NULL,
	[ProviderId] [uniqueidentifier] NOT NULL,
	[ProviderName] [nvarchar](500) NOT NULL,
	[EventId] [int] NOT NULL,
	[EventKeywords] [bigint] NOT NULL,
	[Level] [int] NOT NULL,
	[Opcode] [int] NOT NULL,
	[Task] [int] NOT NULL,
	[Timestamp] [datetimeoffset](7) NOT NULL,
	[Version] [int] NOT NULL,
	[FormattedMessage] [nvarchar](4000) NULL,
	[Payload] [nvarchar](4000) NULL,
	[ActivityId] [uniqueidentifier],
	[RelatedActivityId] [uniqueidentifier],
	[ProcessId] [int],
	[ThreadId] [int],
 CONSTRAINT [PK_Traces] PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF)
)

GO

