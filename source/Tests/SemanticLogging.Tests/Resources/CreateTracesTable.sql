CREATE TABLE [dbo].[Traces] (
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
	-- only used for testing, not in prod schema
	[ExtraColumn] [uniqueidentifier] NULL,
	[ExtraColumn2] [uniqueidentifier] NULL,
 CONSTRAINT [PK_Traces] PRIMARY KEY CLUSTERED 
	(
		[id] ASC
	) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF)
)
