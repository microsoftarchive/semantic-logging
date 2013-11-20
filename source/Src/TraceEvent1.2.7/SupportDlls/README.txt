Some of the TraceEvent.dll functionality uses unmanaged code and thus
needs some additional DLLs to be in the same as TraceEvent.dll.  These
are only needed if you need that particular functionality.

If you use the DiaLib functionality (PDB symbol lookup)
    Interop.Dia2Lib.dll	 
    ARCH\dbghelp.dll 
    ARCH\msdia100.dll 
    ARCH\symsrv.dll  	(If you want PDB symbol server support)

If you use the TraceEventSession.EnableKernelProvider you nee
    ARCH\KernelTraceControl.dll

