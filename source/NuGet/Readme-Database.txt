SEMANTIC LOGGING APPLICATION BLOCK (SLAB)
http://slab.codeplex.com

Summary: The Semantic Logging Application Block provides a set of destinations (sinks) to persist application events published using a subclass of the EventSource class from the System.Diagnostics.Tracing namespace. Sinks include Azure table storage, SQL Server databases, Elasticsearch, and rolling files with several formats and you can extend the block by creating your own custom formatters and sinks. For the sinks that can store structured data, the block preserves the full structure of the event payload in order to facilitate analysing or processing the logged data.

An out-of-proc Windows Service is available as a separate NuGet package (EnterpriseLibrary.SemanticLogging.Service).

The SQL Server setup scripts for using with the SQL Server sink is available inside this package at: $(SolutionDir)\packages\EnterpriseLibrary.SemanticLogging.Database.1.0.1304.0\scripts\

Updated release notes are available at http://slab.codeplex.com/wikipage?title=SLAB1.1ReleaseNotes

Microsoft patterns & practices
http://microsoft.com/practices
