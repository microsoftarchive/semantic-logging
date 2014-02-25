SEMANTIC LOGGING APPLICATION BLOCK (SLAB) BVT
http://slab.codeplex.com

To run the tests follow these steps:
1. Run as Visutal Studio as administrator and open then SemanticLogging.Tests.sln.
2. Run SQL scripts, located at source/scritps.
3. This test fails due to a bug in SLAB: WhenWrongTableNameExceptionsAreRoutedToErrorEventSource
4. This test passes when run individually: WhenNoArgEventIsLogged
5. Build the SemanticLoogging source code before building the BVT solution.

Some tests are configured to wait for a fixed amuont of time until events are written to the SqlDatabase and to the Windows Azure Table.
These tests may fail if the events are not written in time.

Microsoft patterns & practices
http://microsoft.com/practices
