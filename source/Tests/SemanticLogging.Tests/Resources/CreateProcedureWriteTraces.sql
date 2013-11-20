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
		[Payload]
	)
  SELECT * FROM @InsertTraces;
END
