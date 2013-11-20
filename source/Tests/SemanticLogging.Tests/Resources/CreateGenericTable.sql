CREATE TABLE [dbo].[Traces](
	[id] [bigint] IDENTITY(1,1) NOT NULL,
	[InstanceName] [nvarchar](1000) NOT NULL,
	[ProviderId] [char](36) NOT NULL,
	[ProviderName] [nvarchar](500) NOT NULL,
	[EventId] [int] NOT NULL,
	[EventKeywords] [bigint] NOT NULL,
	[Level] [int] NOT NULL,
	[Opcode] [int] NOT NULL,
	[Task] [int] NOT NULL,
	[Timestamp] [datetime2] NOT NULL,
	[Version] [int] NOT NULL,
	[FormattedMessage] [nvarchar](4000) NULL,
	[Payload] [nvarchar](4000) NULL,
 CONSTRAINT [PK_Traces] PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF)
)