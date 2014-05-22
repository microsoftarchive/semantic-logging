SEMANTIC LOGGING APPLICATION BLOCK (SLAB) BVT
http://slab.codeplex.com

To run the tests follow these steps:
1. Run as Visual Studio as administrator and open then SemanticLogging.Tests.sln.
2. Run SQL scripts, located at source/scripts.
3. Build the SemanticLoogging source code before building the BVT solution.
4. The WindowsServiceFixture relies on having the Semantic Logging Service manually installed.  To do this Build BVT Solution and from a command prompt with administrator priviledges run SemanticLogging-svc.exe -i from the output folder (i.e. BVT\SLAB.Tests\SemanticLogging.OutProc.Tests\bin\[Release|Debug]\SemanticLogging-svc.exe -i)

Some tests are configured to wait for a fixed amount of time until events are written to the SqlDatabase and to the Windows Azure Table.
These tests may fail if the events are not written in time.

Microsoft patterns & practices
http://microsoft.com/practices
