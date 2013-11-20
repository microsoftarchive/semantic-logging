//     Copyright (c) Microsoft Corporation.  All rights reserved.
using System.Diagnostics;
using Utilities;

// Welcome to the TraceEvent code base. This _README.cs file is your table of contents.
// 
// You will notice that the code is littered with code: qualifiers. If you install the 'hyperAddin' for
// Visual Studio, these qualifers turn into hyperlinks that allow easy cross references. The hyperAddin is
// available on http://www.codeplex.com/hyperAddin
// 
// -------------------------------------------------------------------------------------
// Overview of files
// 
// There are two 'levels' to the Event Tracing for Windows (ETW) logics. The first is the 'raw' interface
// that provide basic parsing that pretty much everyone will want, but just shows a raw event stream ordered
// by time with little embelishment or symbolic information. The next layer (embodied by code:TraceLog)
// presents the data with more 'high level' structure, including, Processes, Threads, LoadedModules, and symbolic
// information. Ultimately it is expected that most users will use code:TraceLog.
//
// Low, level ETW funtionality:
// 
// * file:TraceEventSession.cs - holds code:TraceEventSession, which is class that controls turning data
//     collection on and off.
// 
// * file:TraceEvent.cs - holds code:ETWTraceEventSource which represents a file containing ETW events. This
//     source produces code:TraceEvent which represent individual events. These two classes know how to parse
//     the ETL file and understand the fields of an event that are common to all events (eg time, process ID,
//     thread ID opcode ...), but do not understand the payload of specific events.
//     
// * file:KernelTraceEventParser.cs - holds code:KernelTraceEventParser which represent the stream of kernel
//     events. It understands the payloads for kernel events and define a host of subclasses of
//     code:TraceEvent that represent them (eg code:ProcessTraceData, code:ImageLoadTraceData ...)
//     
// * file:ClrTraceEventSource.cs - holds code:CLRTraceEventParser which represent the stream of Clr events.
//     It understands the payloads for Clr events and define a host of subclasses of code:TraceEvent that
//     represent them (eg code:GCStartTraceEvent, code:AllocationTickTraceEvent ...)
// 
// * file:TraceEventNativeMethods.cs - holds code:TraceEventNativeMethods which hold PINVOKE declarations
//     needed to access OS functionality. It is completely internal.
//
// -------------------------------------------------------------------------------------
// Light level ETW functionality:
// 
// * file:TraceLog.cs - holds code:TraceLog and friends. While the raw ETW events are valuable, they really
//     need additional processing to be really useful. Things like symbolic names for addresses, various
//     links between threads, threads, modules, and eventToStack traces are really needed. This is what
//     code:TraceLog provides. This is likely to be the interface that most people use (when it is complete).
// 
// 
//--------------------------------------------------------------------------------------
//
//General utilties:  see file:utilities/_README.cs 

