## What is Semantic Logging?

Semantic Logging (formerly know at the Semantic Logging Application Block or SLAB) is designed by the
[patterns & practices](http://aka.ms/mspnp) team to help .NET developers move from the unstructured
logging approach towards the strongly-typed (semantic) logging approach, making it easier to consume
logging information, especially when there is a large volume of log data to be analyzed. When used
out-of-process, Semantic Logging uses [_Event Tracing for Windows (ETW)_][ETW], a fast, lightweight, strongly
typed, extensible logging system that is built into the Windows operating system.

Semantic Logging enables you to use the [`EventSource`][EventSource] class and semantic log messages in your
applications without moving away from the log formats you are familiar with (such as database, text
file, Azure table storage). Also, you do not need to commit to how you consume events when developing
business logic; you have a unified application-specific API for logging and then you can decide later
whether you want those events to go to ETW or alternative destinations.

## How do I use Semantic Logging?

Official releases are available via [NuGet](http://www.nuget.org/packages/EnterpriseLibrary.SemanticLogging/).
You can also head to [msdn.com](https://msdn.microsoft.com/en-us/library/dn774980.aspx) for additional
information, documentation, videos, and hands-on labs.

## Building

To build the solution, run msbuild.exe from the project’s `build` folder. You'll need to use the
Visual Studio Developer Command Prompt. Some of the unit tests require a SQL database.

## How do I contribute?

Please see [CONTRIBUTING.md](/CONTRIBUTING.md) for more details.

## Release notes

Release notes each [release are available](https://github.com/mspnp/semantic-logging/releases).

[ETW]: https://msdn.microsoft.com/en-us/library/windows/desktop/bb968803(v=vs.85).aspx
[EventSource]: https://msdn.microsoft.com/en-us/library/system.diagnostics.tracing.eventsource%28v=vs.110%29.aspx
