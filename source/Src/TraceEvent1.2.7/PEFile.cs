//  Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;
using System.Text;

namespace PEFile
{
    public unsafe sealed class PEBuffer : IDisposable
    {
        public PEBuffer(Stream stream, int buffSize = 512)
        {
            m_stream = stream;
            GetBuffer(buffSize);
        }
        public byte* Fetch(int filePos, int size)
        {
            if (size > m_buff.Length)
                GetBuffer(size);
            if (!(m_buffPos <= filePos && filePos + size <= m_buffPos + m_buffLen))
            {
                // Read in the block of 'size' bytes at filePos
                m_buffPos = filePos;
                m_stream.Seek(m_buffPos, SeekOrigin.Begin);
                m_buffLen = 0;
                while (m_buffLen < m_buff.Length)
                {
                    var count = m_stream.Read(m_buff, m_buffLen, size - m_buffLen);
                    if (count == 0)
                        break;
                    m_buffLen += count;
                }
            }
            return &m_buffPtr[filePos - m_buffPos];
        }
        public int Length { get { return m_buffLen; } }
        public void Dispose()
        {
            m_pinningHandle.Free();
        }
        #region private
        private void GetBuffer(int buffSize)
        {
            m_buff = new byte[buffSize];
            fixed (byte* ptr = m_buff)
                m_buffPtr = ptr;
            m_buffLen = 0;
            m_pinningHandle = GCHandle.Alloc(m_buff, GCHandleType.Pinned);
        }

        int m_buffPos;
        int m_buffLen;      // Number of valid bytes in m_buff
        byte[] m_buff;
        byte* m_buffPtr;
        GCHandle m_pinningHandle;
        Stream m_stream;
        #endregion
    }

    unsafe public class PEFile : IDisposable
    {
        public PEFile(string filePath)
        {
            m_stream = File.OpenRead(filePath);
            m_headerBuff = new PEBuffer(m_stream);

            Header = new PEHeader(m_headerBuff.Fetch(0, 512));
            // We did not read in the complete header, Try again using the right sized buffer.  
            if (Header.Size > m_headerBuff.Length)
                Header = new PEHeader(m_headerBuff.Fetch(0, Header.Size));

            if (Header.Size > m_headerBuff.Length)
                throw new InvalidOperationException("Bad PE Header in " + filePath);
        }
        /// <summary>
        /// The Header for the PE file.  This contains the infor in a link /dump /headers 
        /// </summary>
        public PEHeader Header { get; private set; }
        /// <summary>
        /// Looks up the debug signature information in the EXE.   Returns true and sets the parameters if it is found. 
        /// 
        /// If 'first' is true then the first entry is returned, otherwise (by default) the last entry is used 
        /// (this is what debuggers do today).   Thus NGEN images put the IL PDB last (which means debuggers 
        /// pick up that one), but we can set it to 'first' if we want the NGEN PDB.
        /// </summary>
        public bool GetPdbSignature(out string pdbName, out Guid pdbGuid, out int pdbAge, bool first=false)
        {
            pdbName = null;
            pdbGuid = Guid.Empty;
            pdbAge = 0;
            bool ret = false;

            if (Header.DebugDirectory.VirtualAddress != 0)
            {
                var buff = AllocBuff();
                var debugEntries = (IMAGE_DEBUG_DIRECTORY*)FetchRVA(Header.DebugDirectory.VirtualAddress, Header.DebugDirectory.Size, buff);
                Debug.Assert(Header.DebugDirectory.Size % sizeof(IMAGE_DEBUG_DIRECTORY) == 0);
                int debugCount = Header.DebugDirectory.Size / sizeof(IMAGE_DEBUG_DIRECTORY);
                for (int i = 0; i < debugCount; i++)
                {
                    if (debugEntries[i].Type == IMAGE_DEBUG_TYPE.CODEVIEW)
                    {
                        var stringBuff = AllocBuff();
                        var info = (CV_INFO_PDB70*)stringBuff.Fetch((int)debugEntries[i].PointerToRawData, debugEntries[i].SizeOfData);
                        if (info->CvSignature == CV_INFO_PDB70.PDB70CvSignature)
                        {
                            // If there are several this picks the last one.  
                            pdbGuid = info->Signature;
                            pdbAge = info->Age;
                            pdbName = info->PdbFileName;
                            ret = true;
                            if (first)
                                break;  
                        }
                        FreeBuff(stringBuff);
                    }
                }
                FreeBuff(buff);
            }
            return ret;

        }

        public ResourceNode GetResources()
        {
            if (Header.ResourceDirectory.VirtualAddress == 0 || Header.ResourceDirectory.Size < sizeof(IMAGE_RESOURCE_DIRECTORY))
                return null;
            var ret = new ResourceNode("", Header.FileOffsetOfResources, this, false, true);
            return ret;
        }
        public string GetRT_MANIFEST()
        {
            var resources = GetResources();
            var manifest = ResourceNode.GetChild(ResourceNode.GetChild(resources, "RT_MANIFEST"), "1");
            if (manifest == null)
                return null;
            if (!manifest.IsLeaf && manifest.Children.Count == 1)
                manifest = manifest.Children[0];

            var buff = AllocBuff();
            byte* bytes = manifest.FetchData(0, manifest.DataLength, buff);
            string ret = null;
            using (var stream = new UnmanagedMemoryStream(bytes, manifest.DataLength))
            using (var textReader = new StreamReader(stream))
                ret = textReader.ReadToEnd();
            FreeBuff(buff);
            return ret;
        }
        public FileVersionInfo GetFileVersionInfo()
        {
            var resources = GetResources();
            var versionNode = ResourceNode.GetChild(ResourceNode.GetChild(resources, "Version"), "1");
            if (versionNode == null)
                return null;
            if (!versionNode.IsLeaf && versionNode.Children.Count == 1)
                versionNode = versionNode.Children[0];


            var buff = AllocBuff();
            byte* bytes = versionNode.FetchData(0, versionNode.DataLength, buff);
            var ret =  new FileVersionInfo(bytes, versionNode.DataLength);

            FreeBuff(buff);
            return ret;
        }

        public void Dispose()
        {
            m_stream.Close();
            m_headerBuff.Dispose();
            if (m_freeBuff != null)
                m_freeBuff.Dispose();
        }
        #region private
        PEBuffer m_headerBuff;
        PEBuffer m_freeBuff;
        FileStream m_stream;

        internal byte* FetchRVA(int rva, int size, PEBuffer buffer)
        {
            return buffer.Fetch(Header.RvaToFileOffset(rva), size);
        }
        internal PEBuffer AllocBuff()
        {
            var ret = m_freeBuff;
            if (ret == null)
                return new PEBuffer(m_stream);
            m_freeBuff = null;
            return ret;
        }
        internal void FreeBuff(PEBuffer buffer)
        {
            m_freeBuff = buffer;
        }
        #endregion
    };

    unsafe public class ResourceNode
    {
        public string Name { get; private set; }
        public bool IsLeaf { get; private set; }

        // If IsLeaf is true
        public int DataLength { get { return m_dataLen; } }
        public byte* FetchData(int offsetInResourceData, int size, PEBuffer buff)
        {
            return buff.Fetch(m_dataFileOffset + offsetInResourceData, size);
        }
        public FileVersionInfo GetFileVersionInfo()
        {
            var buff = m_file.AllocBuff();
            byte* bytes = FetchData(0, DataLength, buff);
            var ret = new FileVersionInfo(bytes, DataLength);
            m_file.FreeBuff(buff);
            return ret;
        }

        public override string ToString()
        {
            StringWriter sw = new StringWriter();
            ToString(sw, "");
            return sw.ToString();
        }

        static public ResourceNode GetChild(ResourceNode node, string name)
        {
            if (node == null)
                return null;
            foreach (var child in node.Children)
                if (child.Name == name)
                    return child;
            return null;
        }

        // If IsLeaf is false
        public List<ResourceNode> Children
        {
            get
            {
                if (m_Children == null && !IsLeaf)
                {
                    var buff = m_file.AllocBuff();
                    var resourceStartFileOffset = m_file.Header.FileOffsetOfResources;

                    IMAGE_RESOURCE_DIRECTORY* resourceHeader = (IMAGE_RESOURCE_DIRECTORY*)buff.Fetch(
                        m_nodeFileOffset, sizeof(IMAGE_RESOURCE_DIRECTORY));

                    int totalCount = resourceHeader->NumberOfNamedEntries + resourceHeader->NumberOfIdEntries;
                    int totalSize = totalCount * sizeof(IMAGE_RESOURCE_DIRECTORY_ENTRY);

                    IMAGE_RESOURCE_DIRECTORY_ENTRY* entries = (IMAGE_RESOURCE_DIRECTORY_ENTRY*)buff.Fetch(
                        m_nodeFileOffset + sizeof(IMAGE_RESOURCE_DIRECTORY), totalSize);

                    var nameBuff = m_file.AllocBuff();
                    m_Children = new List<ResourceNode>();
                    for (int i = 0; i < totalCount; i++)
                    {
                        var entry = &entries[i];
                        string entryName = null;
                        if (m_isTop)
                            entryName = IMAGE_RESOURCE_DIRECTORY_ENTRY.GetTypeNameForTypeId(entry->Id);
                        else 
                            entryName = entry->GetName(nameBuff, resourceStartFileOffset);
                        Children.Add(new ResourceNode(entryName, resourceStartFileOffset + entry->DataOffset, m_file, entry->IsLeaf));
                    }
                    m_file.FreeBuff(nameBuff);
                    m_file.FreeBuff(buff);
                }
                return m_Children;
            }
        }

        #region private
        private void ToString(StringWriter sw, string indent)
        {
            sw.Write("{0}<ResourceNode", indent);
            sw.Write(" Name=\"{0}\"", Name);
            sw.Write(" IsLeaf=\"{0}\"", IsLeaf);

            if (IsLeaf)
            {
                sw.Write("DataLength=\"{0}\"", DataLength);
                sw.WriteLine("/>");
            }
            else
            {
                sw.Write("ChildCount=\"{0}\"", Children.Count);
                sw.WriteLine(">");
                foreach (var child in Children)
                    child.ToString(sw, indent + "  ");
                sw.WriteLine("{0}</ResourceNode>", indent);
            }
        }

        internal ResourceNode(string name, int nodeFileOffset, PEFile file, bool isLeaf, bool isTop=false)
        {
            m_file = file;
            m_nodeFileOffset = nodeFileOffset;
            m_isTop = isTop;
            IsLeaf = isLeaf;
            Name = name;

            if (isLeaf)
            {
                var buff = m_file.AllocBuff();
                IMAGE_RESOURCE_DATA_ENTRY* dataDescr = (IMAGE_RESOURCE_DATA_ENTRY*)buff.Fetch(nodeFileOffset, sizeof(IMAGE_RESOURCE_DATA_ENTRY));

                m_dataLen = dataDescr->Size;
                m_dataFileOffset = file.Header.RvaToFileOffset(dataDescr->RvaToData);
                var data = FetchData(0, m_dataLen, buff);
                m_file.FreeBuff(buff);
            }
        }

        private PEFile m_file;
        private int m_nodeFileOffset;
        private List<ResourceNode> m_Children;
        private bool m_isTop;
        private int m_dataLen;
        private int m_dataFileOffset;
        #endregion
    }

    public unsafe class FileVersionInfo
    {
        // TODO incomplete, but this is all I need.  
        public string FileVersion { get; private set; }  
        #region private 
        internal FileVersionInfo(byte* data, int dataLen)
        {
            FileVersion = "";
            if (dataLen <= 0x5c)
                return;

            // See http://msdn.microsoft.com/en-us/library/ms647001(v=VS.85).aspx
            byte* stringInfoPtr = data + 0x5c;   // Gets to first StringInfo

            // TODO hack, search for FileVersion string ... 
            string dataAsString = new string((char*) stringInfoPtr, 0, (dataLen-0x5c) / 2);

            string fileVersionKey = "FileVersion";
            int fileVersionIdx = dataAsString.IndexOf(fileVersionKey);
            if (fileVersionIdx >= 0)
            {
                int valIdx = fileVersionIdx + fileVersionKey.Length;
                for(;;)
                {
                    valIdx++;
                    if (valIdx >= dataAsString.Length)
                        return;
                    if (dataAsString[valIdx] != (char) 0)
                        break;
                }
                int varEndIdx = dataAsString.IndexOf((char) 0, valIdx);
                if (varEndIdx < 0)
                    return;
                FileVersion = dataAsString.Substring(valIdx, varEndIdx-valIdx);
            }
        }

        #endregion
    }


    unsafe public class PEHeader : IDisposable
    {
        /// <summary>
        /// Returns a PEHeader for pointer in memory.  It does NO validity checking. 
        /// </summary>
        /// <param name="startOfPEFile"></param>
        public PEHeader(IntPtr startOfPEFile) : this((void*)startOfPEFile) { }
        public PEHeader(void* startOfPEFile)
        {
            this.dosHeader = (IMAGE_DOS_HEADER*)startOfPEFile;
            this.ntHeader = (IMAGE_NT_HEADERS*)((byte*)startOfPEFile + dosHeader->e_lfanew);
            this.sections = (IMAGE_SECTION_HEADER*)(((byte*)this.ntHeader) + sizeof(IMAGE_NT_HEADERS) + ntHeader->FileHeader.SizeOfOptionalHeader);
        }

        /// <summary>
        /// The total size, including section array of the the PE header.  
        /// </summary>
        public int Size
        {
            get
            {
                return VirtualAddressToRva(this.sections) + sizeof(IMAGE_SECTION_HEADER) * ntHeader->FileHeader.NumberOfSections;
            }
        }

        public int VirtualAddressToRva(void* ptr)
        {
            return (int)((byte*)ptr - (byte*)this.dosHeader);
        }
        public void* RvaToVirtualAddress(int rva)
        {
            return ((byte*)this.dosHeader) + rva;
        }
        public int RvaToFileOffset(int rva)
        {
            for (int i = 0; i < ntHeader->FileHeader.NumberOfSections; i++)
            {

                if (sections[i].VirtualAddress <= rva && rva < sections[i].VirtualAddress + sections[i].VirtualSize)
                    return (int)sections[i].PointerToRawData + (rva - (int)sections[i].VirtualAddress);
            }
            throw new InvalidOperationException("Illegal RVA 0x" + rva.ToString("x"));
        }

        /// <summary>
        /// PEHeader pins a buffer, if you wish to eagerly dispose of this, it can be done here.  
        /// </summary>
        public void Dispose()
        {
            if (pinningHandle.IsAllocated)
                pinningHandle.Free();
            dosHeader = null;
            ntHeader = null;
        }

        public bool IsPE64 { get { return OptionalHeader32->Magic == 0x20b; } }
        public bool IsManaged { get { return ComDescriptorDirectory.VirtualAddress != 0; } }

        // fields of code:IMAGE_NT_HEADERS
        public uint Signature { get { return ntHeader->Signature; } }

        // fields of code:IMAGE_FILE_HEADER
        public MachineType Machine { get { return (MachineType)ntHeader->FileHeader.Machine; } }
        public ushort NumberOfSections { get { return ntHeader->FileHeader.NumberOfSections; } }
        public int TimeDateStampSec { get { return (int) ntHeader->FileHeader.TimeDateStamp; } }
        public DateTime TimeDateStamp
        {
            get
            {
                return TimeDateStampToDate(TimeDateStampSec);
            }
        }
        public static DateTime TimeDateStampToDate(int timeDateStampSec)
        {
                // Convert seconds from Jan 1 1970 to DateTime ticks.  
                // The 621356004000000000L represents Jan 1 1970 as DateTime 100ns ticks.  
                DateTime ret = new DateTime((long)timeDateStampSec * 10000000 + 621356004000000000L, DateTimeKind.Utc).ToLocalTime();

                // From what I can tell TimeDateSec does not take into account daylight savings time when
                // computing the UTC time. Because of this we adjust here to get the proper local time.  
                if (ret.IsDaylightSavingTime())
                    ret = ret.AddHours(-1.0);
                return ret;
        }

        public ulong PointerToSymbolTable { get { return ntHeader->FileHeader.PointerToSymbolTable; } }
        public ulong NumberOfSymbols { get { return ntHeader->FileHeader.NumberOfSymbols; } }
        public ushort SizeOfOptionalHeader { get { return ntHeader->FileHeader.SizeOfOptionalHeader; } }
        public ushort Characteristics { get { return ntHeader->FileHeader.Characteristics; } }

        // fields of code:IMAGE_OPTIONAL_HEADER32 (or code:IMAGE_OPTIONAL_HEADER64)
        // these first ones don't depend on whether we are PE or PE64
        public ushort Magic { get { return OptionalHeader32->Magic; } }
        public byte MajorLinkerVersion { get { return OptionalHeader32->MajorLinkerVersion; } }
        public byte MinorLinkerVersion { get { return OptionalHeader32->MinorLinkerVersion; } }
        public uint SizeOfCode { get { return OptionalHeader32->SizeOfCode; } }
        public uint SizeOfInitializedData { get { return OptionalHeader32->SizeOfInitializedData; } }
        public uint SizeOfUninitializedData { get { return OptionalHeader32->SizeOfUninitializedData; } }
        public uint AddressOfEntryPoint { get { return OptionalHeader32->AddressOfEntryPoint; } }
        public uint BaseOfCode { get { return OptionalHeader32->BaseOfCode; } }

        // These depend on the whether you are PE32 or PE64
        public ulong ImageBase { get { if (IsPE64) return OptionalHeader64->ImageBase; else return OptionalHeader32->ImageBase; } }
        public uint SectionAlignment { get { if (IsPE64) return OptionalHeader64->SectionAlignment; else return OptionalHeader32->SectionAlignment; } }
        public uint FileAlignment { get { if (IsPE64) return OptionalHeader64->FileAlignment; else return OptionalHeader32->FileAlignment; } }
        public ushort MajorOperatingSystemVersion { get { if (IsPE64) return OptionalHeader64->MajorOperatingSystemVersion; else return OptionalHeader32->MajorOperatingSystemVersion; } }
        public ushort MinorOperatingSystemVersion { get { if (IsPE64) return OptionalHeader64->MinorOperatingSystemVersion; else return OptionalHeader32->MinorOperatingSystemVersion; } }
        public ushort MajorImageVersion { get { if (IsPE64) return OptionalHeader64->MajorImageVersion; else return OptionalHeader32->MajorImageVersion; } }
        public ushort MinorImageVersion { get { if (IsPE64) return OptionalHeader64->MinorImageVersion; else return OptionalHeader32->MinorImageVersion; } }
        public ushort MajorSubsystemVersion { get { if (IsPE64) return OptionalHeader64->MajorSubsystemVersion; else return OptionalHeader32->MajorSubsystemVersion; } }
        public ushort MinorSubsystemVersion { get { if (IsPE64) return OptionalHeader64->MinorSubsystemVersion; else return OptionalHeader32->MinorSubsystemVersion; } }
        public uint Win32VersionValue { get { if (IsPE64) return OptionalHeader64->Win32VersionValue; else return OptionalHeader32->Win32VersionValue; } }
        public uint SizeOfImage { get { if (IsPE64) return OptionalHeader64->SizeOfImage; else return OptionalHeader32->SizeOfImage; } }
        public uint SizeOfHeaders { get { if (IsPE64) return OptionalHeader64->SizeOfHeaders; else return OptionalHeader32->SizeOfHeaders; } }
        public uint CheckSum { get { if (IsPE64) return OptionalHeader64->CheckSum; else return OptionalHeader32->CheckSum; } }
        public ushort Subsystem { get { if (IsPE64) return OptionalHeader64->Subsystem; else return OptionalHeader32->Subsystem; } }
        public ushort DllCharacteristics { get { if (IsPE64) return OptionalHeader64->DllCharacteristics; else return OptionalHeader32->DllCharacteristics; } }
        public ulong SizeOfStackReserve { get { if (IsPE64) return OptionalHeader64->SizeOfStackReserve; else return OptionalHeader32->SizeOfStackReserve; } }
        public ulong SizeOfStackCommit { get { if (IsPE64) return OptionalHeader64->SizeOfStackCommit; else return OptionalHeader32->SizeOfStackCommit; } }
        public ulong SizeOfHeapReserve { get { if (IsPE64) return OptionalHeader64->SizeOfHeapReserve; else return OptionalHeader32->SizeOfHeapReserve; } }
        public ulong SizeOfHeapCommit { get { if (IsPE64) return OptionalHeader64->SizeOfHeapCommit; else return OptionalHeader32->SizeOfHeapCommit; } }
        public uint LoaderFlags { get { if (IsPE64) return OptionalHeader64->LoaderFlags; else return OptionalHeader32->LoaderFlags; } }
        public uint NumberOfRvaAndSizes { get { if (IsPE64) return OptionalHeader64->NumberOfRvaAndSizes; else return OptionalHeader32->NumberOfRvaAndSizes; } }

        public IMAGE_DATA_DIRECTORY Directory(int idx)
        {
            if (idx >= NumberOfRvaAndSizes)
                return new IMAGE_DATA_DIRECTORY();
            return ntDirectories[idx];
        }
        public IMAGE_DATA_DIRECTORY ExportDirectory { get { return Directory(0); } }
        public IMAGE_DATA_DIRECTORY ImportDirectory { get { return Directory(1); } }
        public IMAGE_DATA_DIRECTORY ResourceDirectory { get { return Directory(2); } }
        public IMAGE_DATA_DIRECTORY ExceptionDirectory { get { return Directory(3); } }
        public IMAGE_DATA_DIRECTORY CertificatesDirectory { get { return Directory(4); } }
        public IMAGE_DATA_DIRECTORY BaseRelocationDirectory { get { return Directory(5); } }
        public IMAGE_DATA_DIRECTORY DebugDirectory { get { return Directory(6); } }
        public IMAGE_DATA_DIRECTORY ArchitectureDirectory { get { return Directory(7); } }
        public IMAGE_DATA_DIRECTORY GlobalPointerDirectory { get { return Directory(8); } }
        public IMAGE_DATA_DIRECTORY ThreadStorageDirectory { get { return Directory(9); } }
        public IMAGE_DATA_DIRECTORY LoadConfigurationDirectory { get { return Directory(10); } }
        public IMAGE_DATA_DIRECTORY BoundImportDirectory { get { return Directory(11); } }
        public IMAGE_DATA_DIRECTORY ImportAddressTableDirectory { get { return Directory(12); } }
        public IMAGE_DATA_DIRECTORY DelayImportDirectory { get { return Directory(13); } }
        public IMAGE_DATA_DIRECTORY ComDescriptorDirectory { get { return Directory(14); } }

        public int FileOffsetOfResources
        {
            get
            {
                if (ResourceDirectory.VirtualAddress == 0)
                    return 0;
                return RvaToFileOffset(ResourceDirectory.VirtualAddress);
            }
        }
        #region private
        private IMAGE_OPTIONAL_HEADER32* OptionalHeader32 { get { return (IMAGE_OPTIONAL_HEADER32*)(((byte*)this.ntHeader) + sizeof(IMAGE_NT_HEADERS)); } }
        private IMAGE_OPTIONAL_HEADER64* OptionalHeader64 { get { return (IMAGE_OPTIONAL_HEADER64*)(((byte*)this.ntHeader) + sizeof(IMAGE_NT_HEADERS)); } }
        private IMAGE_DATA_DIRECTORY* ntDirectories
        {
            get
            {
                if (IsPE64)
                    return (IMAGE_DATA_DIRECTORY*)(((byte*)this.ntHeader) + sizeof(IMAGE_NT_HEADERS) + sizeof(IMAGE_OPTIONAL_HEADER64));
                else
                    return (IMAGE_DATA_DIRECTORY*)(((byte*)this.ntHeader) + sizeof(IMAGE_NT_HEADERS) + sizeof(IMAGE_OPTIONAL_HEADER32));
            }
        }

        private IMAGE_DOS_HEADER* dosHeader;
        private IMAGE_NT_HEADERS* ntHeader;
        private IMAGE_SECTION_HEADER* sections;
        GCHandle pinningHandle;
        #endregion

    }

    public enum MachineType : ushort
    {
        Native = 0,
        X86 = 0x014c,
        ia64 = 0x0200,
        ARM = 0x01c0,
        Amd64 = 0x8664,
    };

    public struct IMAGE_DATA_DIRECTORY
    {
        public int VirtualAddress;
        public int Size;
    }

    #region private classes
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct IMAGE_DOS_HEADER
    {
        public const short IMAGE_DOS_SIGNATURE = 0x5A4D;       // MZ.  
        [FieldOffset(0)]
        public short e_magic;
        [FieldOffset(60)]
        public int e_lfanew;            // Offset to the IMAGE_FILE_HEADER
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct IMAGE_NT_HEADERS
    {
        public uint Signature;
        public IMAGE_FILE_HEADER FileHeader;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct IMAGE_FILE_HEADER
    {
        public ushort Machine;
        public ushort NumberOfSections;
        public uint TimeDateStamp;
        public uint PointerToSymbolTable;
        public uint NumberOfSymbols;
        public ushort SizeOfOptionalHeader;
        public ushort Characteristics;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct IMAGE_OPTIONAL_HEADER32
    {
        public ushort Magic;
        public byte MajorLinkerVersion;
        public byte MinorLinkerVersion;
        public uint SizeOfCode;
        public uint SizeOfInitializedData;
        public uint SizeOfUninitializedData;
        public uint AddressOfEntryPoint;
        public uint BaseOfCode;
        public uint BaseOfData;
        public uint ImageBase;
        public uint SectionAlignment;
        public uint FileAlignment;
        public ushort MajorOperatingSystemVersion;
        public ushort MinorOperatingSystemVersion;
        public ushort MajorImageVersion;
        public ushort MinorImageVersion;
        public ushort MajorSubsystemVersion;
        public ushort MinorSubsystemVersion;
        public uint Win32VersionValue;
        public uint SizeOfImage;
        public uint SizeOfHeaders;
        public uint CheckSum;
        public ushort Subsystem;
        public ushort DllCharacteristics;
        public uint SizeOfStackReserve;
        public uint SizeOfStackCommit;
        public uint SizeOfHeapReserve;
        public uint SizeOfHeapCommit;
        public uint LoaderFlags;
        public uint NumberOfRvaAndSizes;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct IMAGE_OPTIONAL_HEADER64
    {
        public ushort Magic;
        public byte MajorLinkerVersion;
        public byte MinorLinkerVersion;
        public uint SizeOfCode;
        public uint SizeOfInitializedData;
        public uint SizeOfUninitializedData;
        public uint AddressOfEntryPoint;
        public uint BaseOfCode;
        public ulong ImageBase;
        public uint SectionAlignment;
        public uint FileAlignment;
        public ushort MajorOperatingSystemVersion;
        public ushort MinorOperatingSystemVersion;
        public ushort MajorImageVersion;
        public ushort MinorImageVersion;
        public ushort MajorSubsystemVersion;
        public ushort MinorSubsystemVersion;
        public uint Win32VersionValue;
        public uint SizeOfImage;
        public uint SizeOfHeaders;
        public uint CheckSum;
        public ushort Subsystem;
        public ushort DllCharacteristics;
        public ulong SizeOfStackReserve;
        public ulong SizeOfStackCommit;
        public ulong SizeOfHeapReserve;
        public ulong SizeOfHeapCommit;
        public uint LoaderFlags;
        public uint NumberOfRvaAndSizes;
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe internal struct IMAGE_SECTION_HEADER
    {
        public string Name
        {
            get
            {
                fixed (byte* ptr = NameBytes)
                {
                    if (ptr[7] == 0)
                        return System.Runtime.InteropServices.Marshal.PtrToStringAnsi((IntPtr)ptr);
                    else
                        return System.Runtime.InteropServices.Marshal.PtrToStringAnsi((IntPtr)ptr, 8);
                }
            }
        }
        public fixed byte NameBytes[8];
        public uint VirtualSize;
        public uint VirtualAddress;
        public uint SizeOfRawData;
        public uint PointerToRawData;
        public uint PointerToRelocations;
        public uint PointerToLinenumbers;
        public ushort NumberOfRelocations;
        public ushort NumberOfLinenumbers;
        public uint Characteristics;
    };

    struct IMAGE_DEBUG_DIRECTORY
    {
        public int Characteristics;
        public int TimeDateStamp;
        public short MajorVersion;
        public short MinorVersion;
        public IMAGE_DEBUG_TYPE Type;
        public int SizeOfData;
        public int AddressOfRawData;
        public int PointerToRawData;
    };

    enum IMAGE_DEBUG_TYPE
    {
        UNKNOWN = 0,
        COFF = 1,
        CODEVIEW = 2,
        FPO = 3,
        MISC = 4,
        BBT = 10,
    };

    unsafe struct CV_INFO_PDB70
    {
        public const int PDB70CvSignature = 0x53445352; // RSDS in ascii

        public int CvSignature;
        public Guid Signature;
        public int Age;
        public fixed byte bytePdbFileName[1];   // Actually variable sized. 
        public string PdbFileName
        {
            get
            {
                fixed (byte* ptr = bytePdbFileName)
                    return System.Runtime.InteropServices.Marshal.PtrToStringAnsi((IntPtr)ptr);
            }
        }
    };


    /* Resource information */
    // Resource directory consists of two counts, following by a variable length
    // array of directory entries.  The first count is the number of entries at
    // beginning of the array that have actual names associated with each entry.
    // The entries are in ascending order, case insensitive strings.  The second
    // count is the number of entries that immediately follow the named entries.
    // This second count identifies the number of entries that have 16-bit integer
    // Ids as their name.  These entries are also sorted in ascending order.
    //
    // This structure allows fast lookup by either name or number, but for any
    // given resource entry only one form of lookup is supported, not both.
    unsafe struct IMAGE_RESOURCE_DIRECTORY
    {
        public int Characteristics;
        public int TimeDateStamp;
        public short MajorVersion;
        public short MinorVersion;
        public ushort NumberOfNamedEntries;
        public ushort NumberOfIdEntries;
        //  IMAGE_RESOURCE_DIRECTORY_ENTRY DirectoryEntries[];
    };

    //
    // Each directory contains the 32-bit Name of the entry and an offset,
    // relative to the beginning of the resource directory of the data associated
    // with this directory entry.  If the name of the entry is an actual text
    // string instead of an integer Id, then the high order bit of the name field
    // is set to one and the low order 31-bits are an offset, relative to the
    // beginning of the resource directory of the string, which is of type
    // IMAGE_RESOURCE_DIRECTORY_STRING.  Otherwise the high bit is clear and the
    // low-order 16-bits are the integer Id that identify this resource directory
    // entry. If the directory entry is yet another resource directory (i.e. a
    // subdirectory), then the high order bit of the offset field will be
    // set to indicate this.  Otherwise the high bit is clear and the offset
    // field points to a resource data entry.
    unsafe struct IMAGE_RESOURCE_DIRECTORY_ENTRY
    {
        public bool IsStringName { get { return NameOffsetAndFlag < 0; } }
        public int NameOffset { get { return NameOffsetAndFlag & 0x7FFFFFFF; } }

        public bool IsLeaf { get { return (0x80000000 & DataOffsetAndFlag) == 0; } }
        public int DataOffset { get { return DataOffsetAndFlag & 0x7FFFFFFF; } }
        public int Id { get { return 0xFFFF & NameOffsetAndFlag; } }

        private int NameOffsetAndFlag;
        private int DataOffsetAndFlag;

        internal unsafe string GetName(PEBuffer buff, int resourceStartFileOffset)
        {
            if (IsStringName)
            {
                int nameLen = *((ushort*)buff.Fetch(NameOffset + resourceStartFileOffset, 2));
                char* namePtr = (char*)buff.Fetch(NameOffset + resourceStartFileOffset + 2, nameLen);
                return new string(namePtr);
            }
            else
                return Id.ToString();
        }

        internal static string GetTypeNameForTypeId(int typeId)
        {
            switch (typeId)
            {
                case 1:
                    return "Cursor";
                case 2:
                    return "BitMap";
                case 3:
                    return "Icon";
                case 4:
                    return "Menu";
                case 5:
                    return "Dialog";
                case 6:
                    return "String";
                case 7:
                    return "FontDir";
                case 8:
                    return "Font";
                case 9:
                    return "Accelerator";
                case 10:
                    return "RCData";
                case 11:
                    return "MessageTable";
                case 12:
                    return "GroupCursor";
                case 14:
                    return "GroupIcon";
                case 16:
                    return "Version";
                case 19:
                    return "PlugPlay";
                case 20:
                    return "Vxd";
                case 21:
                    return "Aniicursor";
                case 22:
                    return "Aniicon";
                case 23:
                    return "Html";
                case 24:
                    return "RT_MANIFEST";
            }
            return typeId.ToString();
        }
    }

    // Each resource data entry describes a leaf node in the resource directory
    // tree.  It contains an offset, relative to the beginning of the resource
    // directory of the data for the resource, a size field that gives the number
    // of bytes of data at that offset, a CodePage that should be used when
    // decoding code point values within the resource data.  Typically for new
    // applications the code page would be the unicode code page.
    unsafe struct IMAGE_RESOURCE_DATA_ENTRY
    {
        public int RvaToData;
        public int Size;
        public int CodePage;
        public int Reserved;
    };

    #endregion
}
