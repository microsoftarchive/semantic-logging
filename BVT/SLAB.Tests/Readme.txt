SEMANTIC LOGGING APPLICATION BLOCK (SLAB) BVT
http://slab.codeplex.com

To run the tests follow these steps:
1. Run as Visual Studio as administrator and then open SemanticLogging.Tests.sln.
2. Run SQL scripts, located at source/scripts.
3. Build the SemanticLogging source code before building the BVT solution.

Some tests are configured to wait for a fixed amount of time until events are written to the SqlDatabase and to the Azure Table.
These tests may fail if the events are not written in time.

Microsoft patterns & practices
http://microsoft.com/practices
