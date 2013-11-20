using System;
using System.Collections.Generic;
using System.ComponentModel; // For Win32Excption;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Symbols;
using Dia2Lib;
using Address = System.UInt64;
using Utilities;
using System.Reflection;

namespace Symbols
{
    /// <summary>
    /// A symbol reader represents something that can FIND pdbs (either on a symbol server or via a symbol path)
    /// Its job is to find a full path a PDB.  Then you can use OpenSymbolFile to get a SymbolReaderModule and do more. 
    /// </summary>
    public unsafe class SymbolReader : IDisposable
    {
        /// <summary>
        /// Opens a new SymbolReader.   All diagnostics messages about symbol lookup go to 'log'.  
        /// </summary>
        public SymbolReader(TextWriter log, string nt_symbol_path = null)
        {
            SymbolPath = nt_symbol_path;
            if (SymbolPath == null)
                SymbolPath = SymPath._NT_SYMBOL_PATH;
            log.WriteLine("Created SymbolReader with SymbolPath {0}", nt_symbol_path);

            // TODO FIX NOW.  the code below does not support probing a file extension directory.  
            // we work around this by adding more things to the symbol path
            var symPath = new SymPath(SymbolPath);
            var newSymPath = new SymPath();
            foreach (var symElem in symPath.Elements)
            {
                newSymPath.Add(symElem);
                if (!symElem.IsSymServer)
                {
                    var probe = Path.Combine(symElem.Target, "dll");
                    if (Directory.Exists(probe))
                        newSymPath.Add(probe);
                    probe = Path.Combine(symElem.Target, "exe");
                    if (Directory.Exists(probe))
                        newSymPath.Add(probe);
                }
            }
            var newSymPathStr = newSymPath.ToString();
            // log.WriteLine("Morphed Symbol Path: {0}", newSymPathStr);

            this.m_log = log;

            SymbolReaderNativeMethods.SymOptions options = SymbolReaderNativeMethods.SymGetOptions();
            SymbolReaderNativeMethods.SymSetOptions(
            SymbolReaderNativeMethods.SymOptions.SYMOPT_DEBUG |
                // SymbolReaderNativeMethods.SymOptions.SYMOPT_DEFERRED_LOADS |
            SymbolReaderNativeMethods.SymOptions.SYMOPT_LOAD_LINES |
            SymbolReaderNativeMethods.SymOptions.SYMOPT_EXACT_SYMBOLS |
            SymbolReaderNativeMethods.SymOptions.SYMOPT_UNDNAME
            );

            m_currentProcess = Process.GetCurrentProcess();  // Only here to insure processHandle does not die.  TODO get on safeHandles. 
            m_currentProcessHandle = m_currentProcess.Handle;

            bool success = SymbolReaderNativeMethods.SymInitializeW(m_currentProcessHandle, newSymPathStr, false);
            if (!success)
            {
                // This captures the GetLastEvent (and has to happen before calling CloseHandle()
                m_currentProcessHandle = IntPtr.Zero;
                throw new Win32Exception();
            }
            m_callback = new SymbolReaderNativeMethods.SymRegisterCallbackProc(this.StatusCallback);
            success = SymbolReaderNativeMethods.SymRegisterCallbackW64(m_currentProcessHandle, m_callback, 0);

            Debug.Assert(success);
        }

        // These routines find a PDB based on something (either an DLL or a pdb 'signature')
        /// <summary>
        /// Finds the symbol file for 'exeFilePath' that exists on the current machine (we open
        /// it to find the needed info).   Uses the SymbolReader.SymbolPath (including Symbol servers) to 
        /// look up the PDB, and will download the PDB to the local cache if necessary.  
        /// 
        /// This routine looks in the EXEFile to find the PDB Guid signature and then calls FindSymbolFilePath
        /// 
        /// returns null if the pdb can't be found.  
        /// </summary>
        public string FindSymbolFilePathForModule(string dllFilePath)
        {
            Debug.Assert(!IsDisposed);
            try
            {
                dllFilePath = BypassSystem32FileRedirection(dllFilePath);
                if (File.Exists(dllFilePath))
                {
                    using (var peFile = new PEFile.PEFile(dllFilePath))
                    {
                        string pdbName;
                        Guid pdbGuid;
                        int pdbAge;
                        // TODO we get the NGEN pdb if we can.  Is this what we want in general?
                        if (peFile.GetPdbSignature(out pdbName, out pdbGuid, out pdbAge, true))
                        {
                            string fileVersionString = null;
                            var fileVersion = peFile.GetFileVersionInfo();
                            if (fileVersion != null)
                                fileVersionString = fileVersion.FileVersion;

                            // TODO FIX NOW should this be here? 
                            m_log.WriteLine("Exe {0} has pdb {1} GUID {2} age {3}", dllFilePath, pdbName, pdbGuid, pdbAge);
                            return FindSymbolFilePath(pdbName, pdbGuid, pdbAge, dllFilePath, fileVersionString);
                        }
                        else
                            m_log.WriteLine("File does not have a codeview debug signature.");
                    }
                }
                else
                    m_log.WriteLine("File does not exist.");
            }
            catch (Exception e)
            {
                m_log.WriteLine("Failure opening PE file: {0}", e.Message);
            }

            m_log.WriteLine("[Failed to find PDB file for {0}]", dllFilePath);
            return null;
        }
        /// <summary>
        /// Find the complete PDB path, given just the simple name (filename + pdb extension) as well as its 'signature', 
        /// which uniquely identifies it (on symbol servers).   Uses the SymbolReader.SymbolPath (including Symbol servers) to 
        /// look up the PDB, and will download the PDB to the local cache if necessary.  
        /// 
        /// A Guid of 0, means 'unknown' and will match the first PDB that matches simple name.  Thus it is unsafe. 
        /// 
        /// Returns null if the PDB could  not be found
        /// 
        /// </summary>
        /// <param name="pdbSimpleName">The name of the PDB file (we only use the file name part)</param>
        /// <param name="pdbIndexGuid">The GUID that is embedded in the DLL in the debug information that allows matching the DLL and the PDB</param>
        /// <param name="pdbIndexAge">Tools like BBT transform a DLL into another DLL (with the same GUID) the 'pdbAge' is a small integers
        /// that indicates how many transformations were done</param>
        /// <param name="dllFilePath">If you know the path to the DLL for this pdb add it here.  That way we can probe next to the DLL
        /// for the PDB file.</param>
        /// <param name="fileVersion">This is an optional string that identifies the file version (the 'Version' resource information.  
        /// It is used only to provided better error messages for the log.</param>
        public string FindSymbolFilePath(string pdbSimpleName, Guid pdbIndexGuid, int pdbIndexAge,
            string dllFilePath = null, string fileVersion = "")
        {
            Debug.Assert(!IsDisposed);

            SymbolReaderNativeMethods.SymFindFileInPathProc FindSymbolFileCallBack = delegate(string fileName, IntPtr context)
            {
                Debug.Assert(context == IntPtr.Zero);
                Guid fileGuid = Guid.Empty;
                int fileAge = 0;
                int dummy = 0;
                if (!SymbolReaderNativeMethods.SymSrvGetFileIndexesW(fileName, ref fileGuid, ref fileAge, ref dummy, 0))
                {
                    m_log.WriteLine("Failed to look up PDB signature for {0}.", fileName);
                    return true;        // continue searching.  
                }
                bool matched = (pdbIndexGuid == fileGuid && pdbIndexAge == fileAge);
                if (!matched)
                {
                    if (pdbIndexGuid == Guid.Empty)
                    {
                        m_log.WriteLine("No PDB Guid provided, assuming an unsafe PDB match for {0}", fileName);
                        matched = true;
                    }
                    else
                        m_log.WriteLine("PDB File {0} has Guid {1} age {2} != Desired Guid {3} age {4}",
                            fileName, fileGuid, fileAge, pdbIndexGuid, pdbIndexAge);
                }
                return !matched;        // you return false when you match, true to continue searching
            };

            StringBuilder pdbFullPath = new StringBuilder(260);
            bool foundPDB = SymbolReaderNativeMethods.SymFindFileInPathW(m_currentProcessHandle,
                null,                  // Search path
                pdbSimpleName,
                ref pdbIndexGuid,          // ID (&GUID)
                pdbIndexAge,               // ID 2
                0,                    // ID 3
                SymbolReaderNativeMethods.SSRVOPT_GUIDPTR,  // Flags
                pdbFullPath,    // output FilePath
                FindSymbolFileCallBack,
                IntPtr.Zero);   // Context for callback 
            string pdbPath = null;
            if (foundPDB)
            {
                pdbPath = pdbFullPath.ToString();
                goto Success;
            }

            // TODO is ONLY looking in the cache the right policy?   Hmmm...
            if ((Flags & SymbolReaderFlags.CacheOnly) == 0)
            {
                // We check these last because they may be hostile PDBs and we have to ask the user about them.
                if (dllFilePath != null)        // Check next to the file. 
                {
                    string pdbPathCandidate = Path.ChangeExtension(dllFilePath, ".pdb");

                    // Also try the symbols.pri\retail\dll convention that windows and devdiv use
                    if (!File.Exists(pdbPathCandidate))
                        pdbPathCandidate = Path.Combine(
                            Path.GetDirectoryName(dllFilePath), @"symbols.pri\retail\dll\" +
                            Path.GetFileNameWithoutExtension(dllFilePath) + ".pdb");

                    if (File.Exists(pdbPathCandidate))
                    {
                        if (!FindSymbolFileCallBack(pdbPathCandidate, IntPtr.Zero))
                        {
                            if (CheckSecurity(pdbPathCandidate))
                            {
                                pdbPath = pdbPathCandidate;
                                goto Success;
                            }
                        }
                    }
                }

                // If the pdbPath is a full path, see if it exists 
                if (pdbSimpleName.IndexOf('\\') > 0 && File.Exists(pdbSimpleName))
                {
                    if (!FindSymbolFileCallBack(pdbSimpleName, IntPtr.Zero))
                    {
                        if (CheckSecurity(pdbSimpleName))
                        {
                            pdbPath = pdbSimpleName;
                            goto Success;
                        }
                    }
                }
                // TODO does this belong here?
                m_log.WriteLine("Last chance, looking for PDB in private builds.");
                pdbPath = FindPdbInPrivateBuilds(pdbSimpleName, fileVersion, FindSymbolFileCallBack);
                if (pdbPath != null)
                    goto Success;
            }

            string where = "";
            if ((Flags & SymbolReaderFlags.CacheOnly) != 0)
                where = " in local cache";
            m_log.WriteLine("Failed to find PDB {0}{1}.\r\n    GUID {2} Age {3} Version {4}",
                pdbSimpleName, where, pdbIndexGuid, pdbIndexAge, fileVersion);
            return null;
        Success:
            m_log.WriteLine("Successfully found PDB {0}\r\n    GUID {1} Age {2} Version {3}", pdbPath, pdbIndexGuid, pdbIndexAge, fileVersion);
            // If the PDB is on a network share, copy it to the local sym
            return CacheFileLocally(pdbPath, pdbIndexGuid, pdbIndexAge);
        }

        // Once you have a file path to a PDB file, you can open it with this method
        /// <summary>
        /// Given the path name to a particular PDB file, load it so that you can resolve symbols in it.  
        /// </summary>
        /// <param name="symbolFilePath">The name of the PDB file to open.</param>
        /// <returns>The SymbolReaderModule that represents the information in the symbol file (PDB)</returns>
        public SymbolModule OpenSymbolFile(string symbolFilePath)
        {
            Debug.Assert(!IsDisposed);
            var ret = new SymbolModule(this, symbolFilePath);
            return ret;
        }

        // Various state that controls symbol and source file lookup.  
        /// <summary>
        /// The symbol path used to look up PDB symbol files.   Set when the reader is initialized.  
        /// </summary>
        public string SymbolPath { get; set; }
        /// <summary>
        /// The paths used to look up source files.  defaults to _NT_SOURCE_PATH.  
        /// </summary>
        public string SourcePath
        {
            get
            {
                if (m_SourcePath == null)
                {
                    m_SourcePath = Environment.GetEnvironmentVariable("_NT_SOURCE_PATH");
                    if (m_SourcePath == null)
                        m_SourcePath = "";
                }
                return m_SourcePath;
            }
            set
            {
                m_SourcePath = value;
                m_parsedSourcePath = null;
            }
        }
        /// <summary>
        /// Where symbols are downloaded if needed.   Derived from symbol path
        /// </summary>
        public string SymbolCacheDirectory
        {
            get
            {
                if (m_SymbolCacheDirectory == null)
                    m_SymbolCacheDirectory = new SymPath(SymbolPath).DefaultSymbolCache;
                return m_SymbolCacheDirectory;
            }
            set
            {
                m_SymbolCacheDirectory = value;
            }
        }
        /// <summary>
        /// The place where source is downloaded from a source server.  
        /// </summary>
        public string SourceCacheDirectory
        {
            get
            {
                if (m_SourceCacheDirectory == null)
                    m_SourceCacheDirectory = Path.Combine(Environment.GetEnvironmentVariable("TEMP"), "SrcCache");
                return m_SourceCacheDirectory;
            }
            set
            {
                m_SourceCacheDirectory = value;
            }
        }
        /// <summary>
        /// Is this symbol reader limited to just the local machine cache or not?
        /// </summary>
        public SymbolReaderFlags Flags { get; set; }
        /// <summary>
        /// Cache even the unsafe pdbs to the SymbolCacheDirectory.   TODO: is this a hack?
        /// </summary>
        public bool CacheUnsafePdbs;
        /// <summary>
        /// We call back on this when we find a PDB by probing in 'unsafe' locations (like next to the EXE or in the Built location)
        /// If this function returns true, we assume that it is OK to use the PDB.  
        /// </summary>
        public Func<string, bool> SecurityCheck { get; set; }
        /// <summary>
        /// A place to log additional messages 
        /// </summary>
        public TextWriter Log { get { return m_log; } }

        /// <summary>
        /// Note that all SymbolReaderModules returned by 'OpenSymbolFile' become invalid after disposing of the SymbolReader.  
        /// </summary>
        /// 
        public void Dispose()
        {
            if (m_currentProcessHandle != IntPtr.Zero)
            {
                // Can't do this in the finalizer as the handle may not be valid then.  
                SymbolReaderNativeMethods.SymCleanup(m_currentProcessHandle);
                m_currentProcessHandle = IntPtr.Zero;
                m_currentProcess.Close();
                m_currentProcess = null;
            }
        }
        public bool IsDisposed { get { return m_currentProcess == null; } }
        ~SymbolReader()
        {
            // TODO FIX NOW, need to call SymCleanup, but I need m_currentProcessHandle to be valid. 
        }

        #region private

        private string m_SourcePath;
        internal List<string> ParsedSourcePath
        {
            get
            {
                if (m_parsedSourcePath == null)
                {
                    m_parsedSourcePath = new List<string>();
                    foreach (var path in SourcePath.Split(';'))
                    {
                        var normalizedPath = path.Trim();
                        if (normalizedPath.EndsWith(@"\"))
                            normalizedPath = normalizedPath.Substring(0, normalizedPath.Length - 1);
                        if (Directory.Exists(normalizedPath))
                            m_parsedSourcePath.Add(normalizedPath);
                        else
                            m_log.WriteLine("Path {0} in source path does not exist, skipping.", normalizedPath);
                    }
                }
                return m_parsedSourcePath;
            }
        }
        internal List<string> m_parsedSourcePath;

        private bool CheckSecurity(string pdbName)
        {
            if (SecurityCheck == null)
            {
                m_log.WriteLine("Found PDB {0}, however this is in an unsafe location.", pdbName);
                m_log.WriteLine("If you trust this location, place this directory the symbol path to correct this.");
                return false;
            }
            if (!SecurityCheck(pdbName))
            {
                m_log.WriteLine("Found PDB {0}, but failed securty check.", pdbName);
                return false;
            }
            return true;
        }

        /// <summary>
        /// This is an optional routine.  It is already the case that if you find a PDB on a symbol server
        /// that it will be cached locally, however if you find it on a network path by NOT using a symbol
        /// server, it will be used in place.  This is annoying, and this routine makes up for this by
        /// mimicking this behavior.  Basically if pdbPath is not a local file name, it will copy it to
        /// the local symbol cache and return the local path. 
        /// </summary>
        private string CacheFileLocally(string pdbPath, Guid pdbGuid, int pdbAge)
        {
            try
            {
                var fileName = Path.GetFileName(pdbPath);

                // Use SymSrv conventions in the cache if the Guid is non-zero, otherwise we simply place it in the cache.  
                var localPdbDir = SymbolCacheDirectory;
                if (pdbGuid != Guid.Empty)
                {
                    var pdbPathPrefix = Path.Combine(SymbolCacheDirectory, fileName);
                    // There is a non-trivial possibility that someone puts a FILE that is named what we want the dir to be.  
                    if (File.Exists(pdbPathPrefix))
                    {
                        // If the pdb path happens to be the SymbolCacheDir (a definate possibility) then we would
                        // clobber the source file in our attempt to set up the target.  In this case just give up
                        // and leave the file as it was.  
                        if (string.Compare(pdbPath, pdbPathPrefix, StringComparison.OrdinalIgnoreCase) == 0)
                            return pdbPath;
                        m_log.WriteLine("Removing file {0} from symbol cache to make way for symsrv files.", pdbPathPrefix);
                        File.Delete(pdbPathPrefix);
                    }

                    localPdbDir = Path.Combine(pdbPathPrefix, pdbGuid.ToString("N") + pdbAge.ToString());
                }
                else if (!CacheUnsafePdbs)
                    return pdbPath;

                if (!Directory.Exists(localPdbDir))
                    Directory.CreateDirectory(localPdbDir);

                var localPdbPath = Path.Combine(localPdbDir, fileName);
                var fileExists = File.Exists(localPdbPath);
                if (!fileExists || File.GetLastWriteTimeUtc(localPdbPath) != File.GetLastWriteTimeUtc(pdbPath))
                {
                    if (fileExists)
                        m_log.WriteLine("WARNING: overwriting existing file {0}.", localPdbPath);

                    m_log.WriteLine("Copying {0} to local cache {1}", pdbPath, localPdbPath);
                    File.Copy(pdbPath, localPdbPath, true);
                }
                return localPdbPath;
            }
            catch (Exception e)
            {
                m_log.WriteLine("Error trying to update local PDB cache {0}", e.Message);
            }
            return pdbPath;
        }

        private bool StatusCallback(
            IntPtr hProcess,
            SymbolReaderNativeMethods.SymCallbackActions ActionCode,
            ulong UserData,
            ulong UserContext)
        {
            bool ret = false;
            switch (ActionCode)
            {
                case SymbolReaderNativeMethods.SymCallbackActions.CBA_SRCSRV_INFO:
                case SymbolReaderNativeMethods.SymCallbackActions.CBA_DEBUG_INFO:
                    var line = new String((char*)UserData).Trim();
                    m_log.WriteLine(Regex.Replace(line, @"\p{C}+", String.Empty));
                    ret = true;
                    break;
                default:
                    // messages.Append("STATUS: Code=").Append(ActionCode).AppendLine();
                    break;
            }
            return ret;
        }

        /// <summary>
        /// We may be a 32 bit app which has File system redirection turned on
        /// Morph System32 to SysNative in that case to bypass file system redirection         
        /// </summary>
        internal string BypassSystem32FileRedirection(string path)
        {
            var winDir = Environment.GetEnvironmentVariable("WinDir");
            if (winDir != null)
            {
                var system32 = Path.Combine(winDir, "System32");
                if (path.StartsWith(system32, StringComparison.OrdinalIgnoreCase))
                {
                    if (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") != null)
                    {
                        var sysNative = Path.Combine(winDir, "Sysnative");
                        var newPath = Path.Combine(sysNative, path.Substring(system32.Length + 1));
                        if (File.Exists(newPath))
                            path = newPath;
                    }
                }
            }
            return path;
        }

        private static bool? m_WinBuildsExist;
        private static bool WinBuildsExist
        {
            get
            {
                if (!m_WinBuildsExist.HasValue)
                    m_WinBuildsExist = SymPath.ComputerNameExists("winbuilds");
                return m_WinBuildsExist.Value;

            }
        }
        private static bool? m_CpvsbuildExist;
        private static bool CpvsbuildExist
        {
            get
            {
                if (!m_CpvsbuildExist.HasValue)
                    m_CpvsbuildExist = SymPath.ComputerNameExists("cpvsbuild");
                return m_CpvsbuildExist.Value;

            }
        }
        /// <summary>
        /// This function is useful only internally in Microsoft.  It is useful for builds that are not 
        /// published on the symbol server.   It knows about windows conventions as well as devdiv conventions
        /// </summary>
        private string FindPrivateBuildPdbSearchPath(string fileVersion, string arch)
        {
            // See if it is a windows version style 
            // FileVersion     6.2.7996.0 (fbl_pac_dev2.110504-2214)
            // \\winbuilds\release\FBL_PAC_DEV2\7996.0.110504-2214\x86fre\bin\Indexes
            // \\winbuilds\release\FBL_PAC_DEV2\7996.0.110504-2214\x86fre\symbols.pri\retail\dll
            Match m = Regex.Match(fileVersion, @"(\d+)\.(\d+)\.(\d+)\.(\d+).*\((\w+)\.([\d-]+)\)");
            if (m.Success && !fileVersion.Contains("win7_"))
            {

                if (!WinBuildsExist)
                {
                    m_log.WriteLine("WinBuilds for arch {0} not accessable.  No private build lookup done.", arch);
                    return null;
                }

                var dailyNum = m.Groups[3].Value;
                var rebuildNum = m.Groups[4].Value;
                var branch = m.Groups[5].Value;
                var time = m.Groups[6].Value;
                var buildType = "fre";

                var dropPath = string.Format(@"\\winbuilds\release\{0}\{1}.{2}.{3}\{4}{5}",
                    branch,
                    dailyNum, rebuildNum, time,
                    arch, buildType);

                if (!Directory.Exists(dropPath))
                {
                    m_log.WriteLine("Windows Drop Path {0} does not exist", dropPath);
                    return null;
                }

                /*
                var symServPath = Path.Combine(dropPath, @"bin\Indexes");
                if (Directory.Exists(symServPath))
                    return "SRV*" + symServPath;
                */
                var rawPdbs = Path.Combine(dropPath, @"symbols.pri\retail");
                if (Directory.Exists(rawPdbs))
                {
                    m_log.WriteLine("Found Windows symbols {0}.", rawPdbs);
                    return rawPdbs;
                }
                else
                    m_log.WriteLine("Could not pdbs at {0}.", dropPath);
            }

            // See if it is a devdiv version style 
            // FileVersion     11.0.40506.2 built by: MAIN
            // \\cpvsbuild\drops\dev11\Main\raw\40506.02\binaries.x86ret\Symbols.pri\retail\exe\devenv.pdb
            m = Regex.Match(fileVersion, @"(\d+)\.(\d+)\.(\d+)\.(\d+).*built by: *(\w+)");
            if (m.Success)
            {
                if (!CpvsbuildExist)
                {
                    m_log.WriteLine("Cpvsbuild for arch {0} not accessable.  No private build lookup done.", arch);
                    return null;
                }
                m_log.WriteLine("File version {0} matches devdiv schema.", fileVersion);

                var releaseNum = m.Groups[1].Value;
                var dailyNum = m.Groups[3].Value;
                var rebuildNum = m.Groups[4].Value;
                var branch = m.Groups[5].Value;
                var buildType = "ret";

                var dropPath = string.Format(@"\\cpvsbuild\drops\dev{0}\{1}\raw\{2}.{3}\binaries.{4}{5}",
                    releaseNum,
                    branch,
                    dailyNum, rebuildNum.PadLeft(2, '0'),
                    arch, buildType);

                var rawPdbs = Path.Combine(dropPath, @"symbols.pri\retail");
                if (Directory.Exists(rawPdbs))
                {
                    m_log.WriteLine("Found DevDiv symbols {0}.", rawPdbs);
                    return rawPdbs;
                }
                else
                    m_log.WriteLine("Could not find private build {0}.", dropPath);

            }
            return null;
        }
        internal string FindPdbInPrivateBuilds(string pdbSimpleName, string fileVersion, SymbolReaderNativeMethods.SymFindFileInPathProc FindSymbolFileCallBack)
        {
            if (string.IsNullOrEmpty(fileVersion))
                return null;

            var arches = new string[] { "x86", "amd64" };
            foreach (var arch in arches)
            {
                var buildPath = FindPrivateBuildPdbSearchPath(fileVersion, arch);
                if (buildPath == null)
                    continue;

                pdbSimpleName = Path.GetFileNameWithoutExtension(pdbSimpleName);
                var fullPath = Path.Combine(buildPath, @"dll\" + pdbSimpleName + ".pdb");
                if (!File.Exists(fullPath))
                {
                    fullPath = Path.Combine(buildPath, @"exe\" + pdbSimpleName + ".pdb");
                    if (!File.Exists(fullPath))
                        continue;
                }

                if (FindSymbolFileCallBack(fullPath, IntPtr.Zero))
                    continue;

                return fullPath;
            }
            return null;
        }

        internal Process m_currentProcess;      // keep to insure currentProcessHandle stays alive
        internal IntPtr m_currentProcessHandle; // TODO really need to get on safe handle plan 
        internal SymbolReaderNativeMethods.SymRegisterCallbackProc m_callback;
        internal TextWriter m_log;

        private string m_SymbolCacheDirectory;
        private string m_SourceCacheDirectory;
        #endregion
    }

    /// <summary>
    /// A symbolReaderModule represents a single PDB.   You get one from SymbolReader.OpenSymbolFile
    /// It is effecively a managed interface to the Debug Interface Access (DIA) see 
    /// http://msdn.microsoft.com/en-us/library/x93ctkx8.aspx for more.   I have only exposed what
    /// I need, and the interface is quite large (and not super pretty).  
    /// </summary>
    public unsafe class SymbolModule
    {
        /// <summary>
        /// The path name to the PDB itself
        /// </summary>
        public string PdbPath { get { return m_pdbPath; } }
        /// <summary>
        /// This is the EXE associated with the Pdb.  It may be null or an invalid path.  It is used
        /// to help look up source code (it is implicitly part of the Source Path search) 
        /// </summary>
        public string ExePath { get; set; }
        /// <summary>
        /// Finds a (method) symbolic name for a given relative virtual address of some code.  
        /// Returns an empty string if a name could not be found. 
        /// </summary>
        public string FindNameForRva(uint rva)
        {
            System.Threading.Thread.Sleep(0);           // Allow cancelation.  
            if (m_symbolsByAddr == null)
                return "";
            IDiaSymbol symbol = m_symbolsByAddr.symbolByRVA(rva);
            if (symbol == null)
            {
                Debug.WriteLine(string.Format("Warning: address 0x{0:x} not found.", rva));
                return "";
            }
            var ret = symbol.name;
            if (ret == null)
            {
                Debug.WriteLine(string.Format("Warning: address 0x{0:x} had a null symbol name.", rva));
                return "";
            }
            if (symbol.length == 0)
            {
                Debug.WriteLine(string.Format("Warning: address 0x{0:x} symbol {1} has length 0", rva, ret));
                return "";
            }
            // TODO FIX NOW, should not need to do this hand-unmangling.
            if (ret.Contains("@"))
            {
                // TODO relativel inefficient.  
                string unmangled = null;
                symbol.get_undecoratedNameEx(0x1000, out unmangled);
                if (unmangled != null)
                    ret = unmangled;

                if (ret.StartsWith("@"))
                    ret = ret.Substring(1);
                if (ret.StartsWith("_"))
                    ret = ret.Substring(1);
                var atIdx = ret.IndexOf('@');
                if (0 < atIdx)
                    ret = ret.Substring(0, atIdx);
            }

            m_lastSymbolNameRet = ret;
            return ret;
        }

        /// <summary>
        /// Fetches the source location (line number and file), given the relative virtual address (RVA)
        /// of the location in the executable.  
        /// </summary>
        public SourceLocation SourceLocationForRva(uint rva)
        {
            m_reader.m_log.WriteLine("SourceLocationForRva: looking up RVA {0:x} ", rva);

            uint fetchCount;
            IDiaEnumLineNumbers sourceLocs;
            m_session.findLinesByRVA(rva, 0, out sourceLocs);
            IDiaLineNumber sourceLoc;
            sourceLocs.Next(1, out sourceLoc, out fetchCount);
            if (fetchCount == 0)
            {
                m_reader.m_log.WriteLine("SourceLocationForRva: No lines for RVA {0:x} ", rva);
                return null;
            }
            var buildTimeSourcePath = sourceLoc.sourceFile.fileName;
            var lineNum = (int)sourceLoc.lineNumber;

            var sourceFile = new SourceFile(this, sourceLoc.sourceFile);
            var sourceLocation = new SourceLocation(sourceFile, (int)sourceLoc.lineNumber);
            return sourceLocation;
        }
        /// <summary>
        /// Managed code is shipped as IL, so RVA to NATIVE mapping can't be placed in the PDB. Instead
        /// what is placed in the PDB is a mapping from a method's meta-data token and IL offset to source
        /// line number.  Thus if you have a metadata token and IL offset, you can again get a source location
        /// </summary>
        public SourceLocation SourceLocationForManagedCode(uint methodMetaDataToken, int ilOffset)
        {
            m_reader.m_log.WriteLine("SourceLocationForManagedCode: Looking up method token {0:x} ilOffset {1:x}", methodMetaDataToken, ilOffset);

            IDiaSymbol methodSym;
            m_session.findSymbolByToken(methodMetaDataToken, SymTagEnum.SymTagFunction, out methodSym);
            if (methodSym == null)
            {
                m_reader.m_log.WriteLine("SourceLocationForManagedCode: No symbol for token {0:x} ilOffset {1:x}", methodMetaDataToken, ilOffset);
                return null;
            }

            uint fetchCount;
            IDiaEnumLineNumbers sourceLocs;
            IDiaLineNumber sourceLoc;

            // TODO FIX NOW, this code here is for debugging only turn if off when we are happy.  
            m_session.findLinesByRVA(methodSym.relativeVirtualAddress, (uint)(ilOffset + 256), out sourceLocs);
            for (int i = 0; ;i++)
            {
                sourceLocs.Next(1, out sourceLoc, out fetchCount);
                if (fetchCount == 0)
                    break;
                if (i == 0)
                    m_reader.m_log.WriteLine("SourceLocationForManagedCode: source file: {0}", sourceLoc.sourceFile.fileName);
                m_reader.m_log.WriteLine("SourceLocationForManagedCode: ILOffset {0:x} -> line {1}",
                    sourceLoc.relativeVirtualAddress - methodSym.relativeVirtualAddress, sourceLoc.lineNumber);
            } // End TODO FIX NOW debugging code

            // For managed code, the 'RVA' is a 'cumulative IL offset' (amount of IL bytes before this in the module)
            // Thus you find the line number of a particular IL offset by adding the offset within the method to
            // the cumulative IL offset of the start of the method.  
            m_session.findLinesByRVA(methodSym.relativeVirtualAddress + (uint)ilOffset, 256, out sourceLocs);
            sourceLocs.Next(1, out sourceLoc, out fetchCount);
            if (fetchCount == 0)
            {
                m_reader.m_log.WriteLine("SourceLocationForManagedCode: No lines for token {0:x} ilOffset {1:x}", methodMetaDataToken, ilOffset);
                return null;
            }

            var sourceFile = new SourceFile(this, sourceLoc.sourceFile);
            int lineNum;
            // FEEFEE is some sort of illegal line number that is returned some time,  It is better to ignore it.  
            // and take the next valid line
            for(;;)
            {
                lineNum = (int)sourceLoc.lineNumber;
                if (lineNum != 0xFEEFEE)
                    break;
                sourceLocs.Next(1, out sourceLoc, out fetchCount);
                if (fetchCount == 0)
                    break;
            }

            var sourceLocation = new SourceLocation(sourceFile, lineNum);
            return sourceLocation;
        }
        /// <summary>
        /// Returns a list of all source files referenced in the PDB
        /// </summary>
        public IEnumerable<SourceFile> AllSourceFiles()
        {

            IDiaEnumTables tables;
            m_session.getEnumTables(out tables);

            IDiaEnumSourceFiles sourceFiles;
            IDiaTable table = null;
            uint fetchCount = 0;
            for (; ; )
            {
                tables.Next(1, ref table, ref fetchCount);
                if (fetchCount == 0)
                    return null;
                sourceFiles = table as IDiaEnumSourceFiles;
                if (sourceFiles != null)
                    break;
            }

            var ret = new List<SourceFile>();
            IDiaSourceFile sourceFile = null;
            for (; ; )
            {
                sourceFiles.Next(1, out sourceFile, out fetchCount);
                if (fetchCount == 0)
                    break;
                ret.Add(new SourceFile(this, sourceFile));
            }
            return ret;
        }

        /// <summary>
        /// The PdbIndex a unique identifier that is used to relate the DLL and its PDB.   
        /// </summary>
        public Guid PdbIndexGuid { get { return m_session.globalScope.guid; } }
        public int PdbIndexAge { get { return (int)m_session.globalScope.age; } }

        /// <summary>
        /// The symbol reader this SymbolModule was created from.  
        /// </summary>
        public SymbolReader SymbolReader { get { return m_reader; } }

        #region private
        // TODO FIX NOW use or remove
        public enum NameSearchOptions
        {
            nsNone,
            nsfCaseSensitive = 0x1,
            nsfCaseInsensitive = 0x2,
            nsfFNameExt = 0x4,                  // treat as a file path
            nsfRegularExpression = 0x8,         // * and ? wildcards
            nsfUndecoratedName = 0x10,          // A undecorated name is the name you see in the source code.  
        };
        // TODO FIX NOW REMOVE
        public IEnumerable<string> FindChildrenNames()
        {
            return FindChildrenNames(m_session.globalScope);
        }
        // TODO FIX NOW REMOVE
        public IEnumerable<string> FindChildrenNames(IDiaSymbol scope,
            string name = null, NameSearchOptions searchOptions = NameSearchOptions.nsNone)
        {
            var syms = FindChildren(m_session.globalScope, name, searchOptions);
            var ret = new List<string>();
            foreach (var sym in syms)
                ret.Add(sym.name);
            return ret;
        }
        // TODO FIX NOW REMOVE
        private IEnumerable<IDiaSymbol> FindChildren(IDiaSymbol scope,
            string name = null, NameSearchOptions searchOptions = NameSearchOptions.nsNone)
        {
            IDiaEnumSymbols symEnum;
            m_session.findChildren(scope, SymTagEnum.SymTagNull, name, (uint)searchOptions, out symEnum);
            uint fetchCount;

            var ret = new List<IDiaSymbol>();
            for (; ; )
            {
                IDiaSymbol sym;

                symEnum.Next(1, out sym, out fetchCount);
                if (fetchCount == 0)
                    break;

                SymTagEnum symTag = (SymTagEnum)sym.symTag;
                Debug.WriteLine("Got " + sym.name + " symTag  " + symTag + " token " + sym.token.ToString("x"));
                if (symTag == SymTagEnum.SymTagFunction)
                {
                    if (sym.token != 0)
                    {
                        var sourceLocation = SourceLocationForManagedCode(sym.token, 0);
                        if (sourceLocation != null)
                            Debug.WriteLine("Got Line " + sourceLocation.LineNumber + " file " + sourceLocation.SourceFile);
                    }
                }

                if (symTag == SymTagEnum.SymTagCompiland)
                {
                    var children = (List<IDiaSymbol>)FindChildren(sym, name, searchOptions);
                    Debug.WriteLine("got " + children.Count + " children");
                }

                ret.Add(sym);
            }
            return ret;
        }

        internal SymbolModule(SymbolReader reader, string pdbFilePath)
        {
            m_pdbPath = pdbFilePath;
            this.m_reader = reader;

            IDiaDataSource source = DiaLoader.GetDiaSourceObject();
            source.loadDataFromPdb(pdbFilePath);
            source.openSession(out m_session);
            m_session.getSymbolsByAddr(out m_symbolsByAddr);
        }

        internal void LogManagedInfo(string pdbName, Guid pdbGuid, int pdbAge)
        {
            // Simply rember this if we decide we need it for source server support
            m_managedPdbName = pdbName;
            m_managedPdbGuid = pdbGuid;
            m_managedPdbAge = pdbAge;
        }

        // returns the path of the PDB that has source server information in it (which for NGEN images is the PDB for the managed image)
        internal string SourceServerPdbPath
        {
            get
            {
                if (m_managedPdbName == null)
                    return PdbPath;
                if (!m_managedPdbPathAttempted)
                {
                    m_managedPdbPathAttempted = true;
                    m_managedPdbPath = m_reader.FindSymbolFilePath(m_managedPdbName, m_managedPdbGuid, m_managedPdbAge);
                }
                if (m_managedPdbPath == null)
                    return PdbPath;
                return m_managedPdbPath;
            }
        }
        private string m_managedPdbName;
        private Guid m_managedPdbGuid;
        private int m_managedPdbAge;
        private string m_managedPdbPath;
        private bool m_managedPdbPathAttempted;

        // As a speed optimization we check the last symbol we got 
        IDiaSymbol m_lastSymbol;
        string m_lastSymbolName;
        string m_lastSymbolNameRet;
        uint m_lastSymbolStartRva;
        uint m_lastSymbolEndRva;
        uint m_lastSymbolRva;
        bool m_lastSymbolVerified;

        private string CheckCache(uint rva)
        {
            if (m_lastSymbol == null)
                return null;
            if (!m_lastSymbolVerified)
            {
                // If we are not close, don't bother.  
                if (!(m_lastSymbolRva < rva && rva <= m_lastSymbolRva + 0x100))
                {
                    m_lastSymbol = null;
                    return null;
                }
                m_lastSymbolStartRva = m_lastSymbol.relativeVirtualAddress;
                m_lastSymbolEndRva = m_lastSymbolStartRva + (uint)m_lastSymbol.length;
                for (; ; )
                {
                    if (!(m_lastSymbolStartRva <= rva && rva < m_lastSymbolEndRva))
                    {
                        m_lastSymbol = null;
                        return null;
                    }

                    var testSym = m_symbolsByAddr.symbolByRVA(m_lastSymbolEndRva - 1);
                    Debug.WriteLine(string.Format("Validating {0} start 0x{1:x} end 0x{2:x}", m_lastSymbolName, m_lastSymbolStartRva, m_lastSymbolEndRva));
                    if (testSym == null)
                    {
                        m_lastSymbol = null;
                        return null;
                    }
                    if (testSym.name == m_lastSymbolName)
                    {
                        Debug.WriteLine("Hit Cache after validation.");
                        m_lastSymbolVerified = true;
                        return m_lastSymbolNameRet;
                    }
                    Debug.WriteLine(string.Format("Had to adjust symbol {0} from 0x{1:x} down to rva 0x{2:x}",
                        m_lastSymbolName, m_lastSymbolEndRva, testSym.relativeVirtualAddress));

                    if (m_lastSymbolEndRva <= testSym.relativeVirtualAddress)
                    {
                        Debug.WriteLine("We did not reduce the range!   Giving up.");
                        m_lastSymbol = null;
                        return null;
                    }
                    m_lastSymbolEndRva = testSym.relativeVirtualAddress;
                }
            }

            if (m_lastSymbolStartRva <= rva && rva < m_lastSymbolEndRva)
            {
                Debug.WriteLine("Hit Cache.");
                return m_lastSymbolNameRet;
            }
            return null;
        }

        internal SymbolReader m_reader;
        IDiaSession m_session;
        IDiaEnumSymbolsByAddr m_symbolsByAddr;
        string m_pdbPath;

        #endregion

        // TODO use or remove. 
        internal void GetSourceServerStream()
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().ManifestModule.FullyQualifiedName);

            var pdbStrExe = Path.Combine(dir, "pdbstr.exe");
            var cmd = Command.Run(Command.Quote(pdbStrExe) + "-s:srcsrv -p:" + m_pdbPath);
            string output = cmd.Output;
        }

    }

    [Flags]
    public enum SymbolReaderFlags
    {
        /// <summary>
        /// No options this is the common case, where you want to look up everything you can. 
        /// </summary>
        None = 0,
        /// <summary>
        /// Only fetch the PDB if it lives in the symbolCacheDirectory (is local an is generated).  
        /// This will generate NGEN pdbs unless the NoNGenPDBs flag is set. 
        /// </summary>
        CacheOnly = 1,
        /// <summary>
        /// No NGEN PDB generation.  
        /// </summary>
        NoNGenPDB = 2,
    }

    /// <summary>
    /// A source file represents a source file from a PDB.  This is not just a string
    /// because the file has a build time path, a checksum, and it needs to be 'smart'
    /// to copy down the file if requested.  
    /// </summary>
    public class SourceFile
    {
        public string BuildTimeFilePath { get; private set; }
        public bool HasChecksum { get { return m_hash != null; } }
        /// <summary>
        /// This may fetch things from the source server, and thus can be very slow, which is why it is not a property. 
        /// </summary>
        /// <returns></returns>
        public string GetSourceFile()
        {
            var log = m_symbolModule.m_reader.m_log;

            // Did we build on this machine?  
            if (File.Exists(BuildTimeFilePath))
            {
                if (ChecksumMatches(BuildTimeFilePath))
                {
                    log.WriteLine("Found in build location.");
                    return BuildTimeFilePath;
                }
            }

            // Try the source server 
            var ret = GetSourceFromSrcServer();
            if (ret != null)
            {
                log.WriteLine("Got source from source server.");
                return ret;
            }

            // Try _NT_SOURCE_PATH
            var locations = m_symbolModule.m_reader.ParsedSourcePath;
            log.WriteLine("_NT_SOURCE_PATH={0}", m_symbolModule.m_reader.SourcePath);

            // If we know the exe path, add that to the search path.   
            if (m_symbolModule.ExePath != null)
            {
                var exeDir = Path.GetDirectoryName(m_symbolModule.ExePath);
                if (Directory.Exists(exeDir))
                {
                    // Add directories up the path, we stop somewhat arbitrarily at 3 
                    for (int i = 0; i < 3; i++)
                    {
                        locations.Insert(0, exeDir);
                        log.WriteLine("Adding Exe path {0}", exeDir);

                        exeDir = Path.GetDirectoryName(exeDir);
                        if (exeDir == null)
                            break;
                    }
                }
            }

            var curIdx = 0;
            for (; ; )
            {
                var sepIdx = BuildTimeFilePath.IndexOf('\\', curIdx);
                if (sepIdx < 0)
                    break;
                curIdx = sepIdx + 1;
                var tail = BuildTimeFilePath.Substring(sepIdx);

                foreach (string location in locations)
                {
                    var probe = location + tail;
                    log.WriteLine("Probing {0}", probe);
                    if (File.Exists(probe))
                    {
                        if (ChecksumMatches(probe))
                        {
                            log.WriteLine("Success {0}", probe);
                            return probe;
                        }
                        else
                            log.WriteLine("Found file {0} but checksum mismatches", probe);
                    }
                }
            }

            log.WriteLine("[Could not find source for {0}]", BuildTimeFilePath);
            return null;
        }

        #region private
        private string GetSourceFromSrcServer()
        {
            string ret = null;
            lock (this)
            {
                // To allow for cancelation we run this on another thread
                // This is a hack until I can stop using the ugly non-thread safe APIs.  
                if (s_sourceServerCommandInProgress)
                {
                    while (s_sourceServerCommandInProgress)
                        System.Threading.Thread.Sleep(100);
                }
                s_sourceServerCommandInProgress = true;
                var thread = new System.Threading.Thread(delegate()
                {

                    ret = m_GetSourceFromSrcServer(BuildTimeFilePath);
                    s_sourceServerCommandInProgress = false;
                });
                thread.Start();

                // wait, allowing cancelation.  
                while (s_sourceServerCommandInProgress)
                    System.Threading.Thread.Sleep(100);
            }
            return ret;
        }

        private bool ChecksumMatches(string filePath)
        {
            if (!HasChecksum)
                return true;

            byte[] checksum = ComputeHash(filePath);
            if (checksum.Length != m_hash.Length)
                return false;
            for (int i = 0; i < checksum.Length; i++)
                if (checksum[i] != m_hash[i])
                    return false;
            return true;
        }

        private static bool s_sourceServerCommandInProgress;

        unsafe private string m_GetSourceFromSrcServer(string buildTimeSourcePath)
        {
            // Currently we are very inefficient loading and unloading constantly.  
            ulong imageBase = 0x10000;
            var sb = new StringBuilder(260);

            var reader = m_symbolModule.m_reader;

            reader.m_log.WriteLine("[Searching source server for {0}]", buildTimeSourcePath);
            ulong imageBaseRet = SymbolReaderNativeMethods.SymLoadModuleExW(
                reader.m_currentProcessHandle, IntPtr.Zero, m_symbolModule.SourceServerPdbPath, null, (ulong)imageBase, 0, null, 0);

            // TODO FIX NOW is this a hack?  
            // Allow it to find exes next to the current assembly

            var origPath = Environment.GetEnvironmentVariable("PATH");
            var newPath = origPath;

            // TODO FIX NOW search exhaustively. (ELFIX)
            var progFiles = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            if (progFiles == null)
                progFiles = Environment.GetEnvironmentVariable("ProgramFiles");
            if (progFiles != null)
            {
                var VSDir = Path.Combine(progFiles, @"Microsoft Visual Studio 11.0\Common7\IDE");
                if (!Directory.Exists(VSDir))
                    VSDir = Path.Combine(progFiles, @"Microsoft Visual Studio 10.0\Common7\IDE");

                var tfexe = Path.Combine(VSDir, "tf.exe");
                if (File.Exists(tfexe))
                    newPath = VSDir + ";" + newPath;
            }

            var curAssembly = System.Reflection.Assembly.GetExecutingAssembly();
            var curAssemblyDir = Path.GetDirectoryName(curAssembly.ManifestModule.FullyQualifiedName);
            reader.m_log.WriteLine("Adding {0} to the path", curAssemblyDir);
            newPath = curAssemblyDir + ";" + newPath;
            Environment.SetEnvironmentVariable("PATH", newPath);

            var setHomeRet = SymbolReaderNativeMethods.SymSetHomeDirectoryW(reader.m_currentProcessHandle, reader.SourceCacheDirectory);
            Debug.Assert(!(setHomeRet == IntPtr.Zero));

            var ret = SymbolReaderNativeMethods.SymGetSourceFileW(reader.m_currentProcessHandle, imageBase, IntPtr.Zero, buildTimeSourcePath, sb, sb.Capacity);
            SymbolReaderNativeMethods.SymUnloadModule64(reader.m_currentProcessHandle, imageBase);

            // TODO worry about exceptions.  
            Environment.SetEnvironmentVariable("PATH", origPath);

            if (!ret)
            {
                reader.m_log.WriteLine("Source Server for {0} failed", buildTimeSourcePath);
                return null;
            }

            var retVal = sb.ToString();
            reader.m_log.WriteLine("Source Server downloaded {0}", retVal);
            return retVal;
        }

        unsafe internal SourceFile(SymbolModule module, IDiaSourceFile sourceFile)
        {
            m_symbolModule = module;
            BuildTimeFilePath = sourceFile.fileName;

            // 0 No checksum present.
            // 1 CALG_MD5 checksum generated with the MD5 hashing algorithm.
            // 2 CALG_SHA1 checksum generated with the SHA1 hashing algorithm.
            m_hashType = sourceFile.checksumType;
            if (m_hashType != 1 && m_hashType != 0)
            {
                // TODO does anyone use SHA1?   
                Debug.Assert(false, "Unknown hash type");
                m_hashType = 0;
            }
            if (m_hashType != 0)
            {
                // MD5 is 16 bytes
                // SHA1 is 20 bytes  
                m_hash = new byte[16];

                uint bytesFetched;
                fixed (byte* bufferPtr = m_hash)
                    sourceFile.get_checksum((uint)m_hash.Length, out bytesFetched, out *bufferPtr);
                Debug.Assert(bytesFetched == 16);
            }
        }

        private byte[] ComputeHash(string filePath)
        {
            System.Security.Cryptography.MD5CryptoServiceProvider crypto = new System.Security.Cryptography.MD5CryptoServiceProvider();
            using (var fileStream = File.OpenRead(filePath))
                return crypto.ComputeHash(fileStream);
        }

        SymbolModule m_symbolModule;
        uint m_hashType;
        byte[] m_hash;
        #endregion
    }

    /// <summary>
    /// A SourceLocation represents a point in the source code.  That is the file and the line number.  
    /// </summary>
    public class SourceLocation
    {
        public SourceFile SourceFile { get; private set; }
        public int LineNumber { get; private set; }
        #region private
        internal SourceLocation(SourceFile sourceFile, int lineNumber)
        {
            SourceFile = sourceFile;
            LineNumber = lineNumber;
        }
        #endregion
    }
}

#region private classes
namespace Dia2Lib
{
    /// <summary>
    /// The DiaLoader class knows how to load the msdia100.dll (the Debug Access Interface) (see docs at
    /// http://msdn.microsoft.com/en-us/library/x93ctkx8.aspx), without it being registered as a COM object.
    /// Basically it just called the DllGetClassObject interface directly.
    /// 
    /// It has one public method 'GetDiaSourceObject' which knows how to create a IDiaDataSource object. 
    /// From there you can do anything you need.  
    /// </summary>
    internal static class DiaLoader
    {
        /// <summary>
        /// Load the msdia100 dll and get a IDiaDataSource from it.  This is your gateway to PDB reading.   
        /// </summary>
        public static IDiaDataSource GetDiaSourceObject()
        {
            var diaSourceClassGuid = new Guid("{B86AE24D-BF2F-4AC9-B5A2-34B14E4CE11D}");
            var comClassFactory = (IClassFactory)DllGetClassObject(diaSourceClassGuid, typeof(IClassFactory).GUID);

            object comObject = null;
            Guid iDataDataSourceGuid = typeof(IDiaDataSource).GUID;
            comClassFactory.CreateInstance(null, ref iDataDataSourceGuid, out comObject);
            return (comObject as IDiaDataSource);
        }
        #region private
        [ComImport, ComVisible(false), Guid("00000001-0000-0000-C000-000000000046"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IClassFactory
        {
            void CreateInstance([MarshalAs(UnmanagedType.Interface)] object aggregator,
                                ref Guid refiid,
                                [MarshalAs(UnmanagedType.Interface)] out object createdObject);
            void LockServer(bool incrementRefCount);
        }

        // Methods
        [return: MarshalAs(UnmanagedType.Interface)]
        [DllImport("msdia100.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
        internal static extern object DllGetClassObject(
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid);

        #endregion
    }
}
#endregion
