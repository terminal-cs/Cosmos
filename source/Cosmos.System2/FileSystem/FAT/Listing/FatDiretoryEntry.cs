﻿//#define COSMOSDEBUG

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cosmos.Common.Extensions;
using Cosmos.System.FileSystem.Listing;

namespace Cosmos.System.FileSystem.FAT.Listing
{
    /// <summary>
    /// FatDirectoryEntry class. Represent directory/file.
    /// </summary>
    internal class FatDirectoryEntry : DirectoryEntry
    {
        private readonly uint mEntryHeaderDataOffset;

        private readonly uint mFirstClusterNum;

        /// <summary>
        /// Initializes a new instance of the <see cref="FatDirectoryEntry"/> class.
        /// </summary>
        /// <param name="aFileSystem">The file system that contains the directory entry.</param>
        /// <param name="aParent">The parent directory entry or null if the current entry is the root.</param>
        /// <param name="aFullPath">The full path to the entry.</param>
        /// <param name="aName">The entry name.</param>
        /// <param name="aSize">The size of the entry.</param>
        /// <param name="aFirstCluster">The first cluster of the entry.</param>
        /// <param name="aEntryHeaderDataOffset">The entry header data offset.</param>
        /// <param name="aEntryType">The entry type.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when first cluster smaller then file system root cluster.</exception>
        /// <exception cref="ArgumentNullException">Thrown when aFileSystem is null.</exception>
        /// <exception cref="ArgumentException">Thrown when aFullPath or aName is null or empty.</exception>
        public FatDirectoryEntry(FatFileSystem aFileSystem, FatDirectoryEntry aParent, string aFullPath, string aName, long aSize,
            uint aFirstCluster, uint aEntryHeaderDataOffset, DirectoryEntryTypeEnum aEntryType, bool aNew = false) : base(aFileSystem, aParent, aFullPath, aName, aSize, aEntryType)
        {
            if (aFirstCluster < aFileSystem.RootCluster)
            {
                Global.FileSystemDebugger.SendInternal($"aFirstCluster {aFirstCluster} < aFileSystem.RootCluster {aFileSystem.RootCluster}");
                throw new ArgumentOutOfRangeException(nameof(aFirstCluster));
            }
            Global.FileSystemDebugger.SendInternal("-- FatDirectoryEntry.ctor --");
            Global.FileSystemDebugger.SendInternal("aFullPath: " + aFullPath);
            Global.FileSystemDebugger.SendInternal("aFirstCluster: " + aFirstCluster);
            Global.FileSystemDebugger.SendInternal("aEntryHeaderDataOffset: " + aEntryHeaderDataOffset);
            mFirstClusterNum = aFirstCluster;
            mEntryHeaderDataOffset = aEntryHeaderDataOffset;
            if(aNew && aEntryType == DirectoryEntryTypeEnum.Directory && mEntryHeaderDataOffset == 0)
            {
                InitialiseNewDirectory(aFileSystem);
            }
            Global.FileSystemDebugger.SendInternal("-- ---------------------- --");
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FatDirectoryEntry"/> class of type DirectoryEntryTypeEnum.Directory
        /// </summary>
        /// <param name="aFileSystem">The file system that contains the directory entry.</param>
        /// <param name="aParent">The parent directory entry or null if the current entry is the root.</param>
        /// <param name="aFullPath">The full path to the entry.</param>
        /// <param name="aName">The entry name.</param>
        /// <param name="aSize">The size of the entry.</param>
        /// <param name="aFirstCluster">The first cluster of the entry.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when first cluster smaller then file system root cluster.</exception>
        /// <exception cref="ArgumentNullException">Thrown when aFileSystem is null.</exception>
        /// <exception cref="ArgumentException">Thrown when aFullPath or aName is null or empty.</exception>
        public FatDirectoryEntry(FatFileSystem aFileSystem, FatDirectoryEntry aParent, string aFullPath, string aName, long aSize, uint aFirstCluster, bool aNew = false)
            : base(aFileSystem, aParent, aFullPath, aName, aSize, DirectoryEntryTypeEnum.Directory)
        {
            if (aFirstCluster < aFileSystem.RootCluster)
            {
                throw new ArgumentOutOfRangeException(nameof(aFirstCluster));
            }
            Global.FileSystemDebugger.SendInternal("-- FatDirectoryEntry.ctor --");

            mFirstClusterNum = aFirstCluster;
            Global.FileSystemDebugger.SendInternal("mFirstClusterNum = " + mFirstClusterNum);
            mEntryHeaderDataOffset = 0;
            if (aNew)
            {
                InitialiseNewDirectory(aFileSystem);
            }

            Global.FileSystemDebugger.SendInternal("-- ---------------------- --");
        }

        private void InitialiseNewDirectory(FatFileSystem aFileSystem)
        {
            //Now add the . and .. directory entries
            var dot = new FatDirectoryEntry(aFileSystem, this, mFullPath + "\\.", ".", 0, mFirstClusterNum);
            dot.AllocateDirectoryEntry(".          ", true);

            var dotdot = new FatDirectoryEntry(aFileSystem, this, mFullPath + "\\..", "..", 0, ((FatDirectoryEntry)mParent).mFirstClusterNum, 32, DirectoryEntryTypeEnum.Directory);
            dotdot.AllocateDirectoryEntry("..         ", true);
        }

        /// <summary>
        /// Get FAT table.
        /// </summary>
        /// <returns>An array of cluster numbers for the FAT chain.</returns>
        /// <exception cref="Exception">Thrown when FAT table not found / out of memory / invalid aData size.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the size of the chain is less then zero.</exception>
        /// <exception cref="OverflowException">Thrown when the number of clusters in the FAT entry is greater than Int32.MaxValue</exception>
        /// <exception cref="NotSupportedException">Thrown when FAT type is unknown.</exception>
        /// <exception cref="ArgumentException">Thrown when FAT type is unknown.</exception>
        /// <exception cref="ArgumentNullException">Thrown when aData is null.</exception>
        public uint[] GetFatTable()
        {
            Global.FileSystemDebugger.SendInternal("-- FatDirectoryEntry.GetFatTable --");

            var xFat = ((FatFileSystem)mFileSystem).GetFat(0);
            var vs = xFat?.GetFatChain(mFirstClusterNum, mSize);
            Global.FileSystemDebugger.SendInternal("-- ----------------------------- --");
            return vs;
        }

        /// <summary>
        /// Get file system.
        /// </summary>
        /// <returns>File system.</returns>
        public FatFileSystem GetFileSystem()
        {
            Global.FileSystemDebugger.SendInternal("-- FatDirectoryEntry.GetFileSystem --");

            return (FatFileSystem)mFileSystem;
        }

        /// <summary>
        /// Get file stream.
        /// </summary>
        /// <returns>File stream. null if object is not a file.</returns>
        /// <exception cref="ArgumentNullException">Thrown when this object is null.</exception>
        /// <exception cref="Exception">Thrown when FAT table not found / out of memory.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the size of the chain is less then zero. (Never thrown)</exception>
        /// <exception cref="OverflowException">Thrown when the number of clusters in the FAT entry is greater than Int32.MaxValue</exception>
        public override Stream GetFileStream()
        {
            Global.FileSystemDebugger.SendInternal("-- FatDirectoryEntry.GetFileStream --");

            if (mEntryType == DirectoryEntryTypeEnum.File)
            {
                return new FatStream(this);
            }

            return null;
        }

        /// <summary>
        /// Set name.
        /// </summary>
        /// <param name="aName">A name to set to the entry.</param>
        /// <exception cref="ArgumentException">
        /// <list type="bullet">
        /// <item>Thrown when aName is null or empty string.</item>
        /// <item>aData length is 0.</item>
        /// </list>
        /// </exception>
        /// <exception cref="Exception">
        /// <list type="bullet">
        /// <item>Thrown when entry metadata could not be changed.</item>
        /// <item>Invalid entry type.</item>
        /// <item>Invalid entry data size.</item>
        /// <item>Invalid directory entry type.</item>
        /// </list>
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <list type="bullet">
        /// <item>Thrown when entrys aValue is null.</item>
        /// <item>Thrown when entrys aData is null.</item>
        /// <item>Out of memory.</item>
        /// </list>
        /// </exception>
        /// <exception cref="EncoderFallbackException">Thrown when encoder fallback operation on aValue fails.</exception>
        /// <exception cref="RankException">Thrown on fatal error.</exception>
        /// <exception cref="ArrayTypeMismatchException">Thrown on fatal error.</exception>
        /// <exception cref="InvalidCastException">Thrown when the data in aValue is corrupted.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <list type = "bullet" >
        /// <item>Thrown when the data length is 0 or greater then Int32.MaxValue.</item>
        /// <item>Entrys matadata offset value is invalid.</item>
        /// </list>
        /// </exception>
        public override void SetName(string aName)
        {
            Global.FileSystemDebugger.SendInternal("-- FatDirectoryEntry.SetName --");
            Global.FileSystemDebugger.SendInternal($"aName = {aName}");

            if (string.IsNullOrEmpty(aName))
            {
                throw new ArgumentException("Argument is null or empty", nameof(aName));
            }

            SetDirectoryEntryMetadataValue(FatDirectoryEntryMetadata.ShortName, aName);
            mName = aName;
        }

        /// <summary>
        /// Set the size of the entry.
        /// </summary>
        /// <param name="aSize">The size of the entry.</param>
        /// <exception cref="Exception">
        /// <list type="bullet">
        /// <item>Thrown when trying to change root directory matadata.</item>
        /// <item>data size invalid.</item>
        /// <item>invalid directory entry type.</item>
        /// </list>
        /// </exception>
        /// <exception cref="OverflowException">Thrown when data lenght is greater then Int32.MaxValue.</exception>
        /// <exception cref="ArgumentNullException">
        /// <list type="bullet">
        /// <item>Thrown when entrys aValue is null.</item>
        /// <item>Thrown when entrys aData is null.</item>
        /// <item>Out of memory.</item>
        /// </list>
        /// </exception>
        /// <exception cref="RankException">Thrown on fatal error.</exception>
        /// <exception cref="ArrayTypeMismatchException">Thrown on fatal error.</exception>
        /// <exception cref="InvalidCastException">Thrown when the data in aValue is corrupted.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <list type = "bullet" >
        /// <item>Thrown when the data length is 0 or greater then Int32.MaxValue.</item>
        /// <item>Entrys matadata offset value is invalid.</item>
        /// <item>Thrown when aSize is smaller than 0.</item>
        /// </list>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <list type="bullet">
        /// <item>Thrown when aName is null or empty string.</item>
        /// <item>aData length is 0.</item>
        /// </list>
        /// </exception>
        public override void SetSize(long aSize)
        {
            Global.FileSystemDebugger.SendInternal("FatDirectoryEntry.SetSize:");
            Global.FileSystemDebugger.SendInternal("aSize =");
            Global.FileSystemDebugger.SendInternal(aSize);

            if (aSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(aSize));
            }

            SetDirectoryEntryMetadataValue(FatDirectoryEntryMetadata.Size, aSize);
            mSize = aSize;
        }

        /// <summary>
        /// Allocate directory entry.
        /// </summary>
        /// <param name="aShortName">A short name to set to the entry.</param>
        /// <exception cref="Exception">
        /// <list type="bullet">
        /// <item>Thrown when trying to change root directory matadata.</item>
        /// <item>FAT table not found</item>
        /// <item>data size invalid.</item>
        /// <item>invalid directory entry type.</item>
        /// </list>
        /// </exception>
        /// <exception cref="OverflowException">Thrown when data lenght is greater then Int32.MaxValue.</exception>
        /// <exception cref="EncoderFallbackException">Thrown when encoder fallback operation on aValue fails.</exception>
        /// <exception cref="ArgumentNullException">
        /// <list type="bullet">
        /// <item>Thrown when entrys aValue is null.</item>
        /// <item>Thrown when entrys aData is null.</item>
        /// <item>Out of memory.</item>
        /// </list>
        /// </exception>
        /// <exception cref="RankException">Thrown on fatal error.</exception>
        /// <exception cref="ArrayTypeMismatchException">Thrown on fatal error.</exception>
        /// <exception cref="InvalidCastException">Thrown when the data in aValue is corrupted.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <list type = "bullet" >
        /// <item>Thrown when the data length is 0 or greater then Int32.MaxValue.</item>
        /// <item>Entrys matadata offset value is invalid.</item>
        /// </list>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <list type="bullet">
        /// <item>Thrown when aName is null or empty string.</item>
        /// <item>aData length is 0.</item>
        /// </list>
        /// </exception>
        /// <exception cref="NotSupportedException">Thrown when FAT type is unknown.</exception>
        private void AllocateDirectoryEntry(string aShortName, bool special)
        {
            Global.FileSystemDebugger.SendInternal("-- FatDirectoryEntry.AllocateDirectoryEntry --");
            Global.FileSystemDebugger.SendInternal("aShortName = " + aShortName);
            string xNameString = aShortName;
            if (!special)
            {
                xNameString = GetShortName(aShortName);
            }

            SetDirectoryEntryMetadataValue(FatDirectoryEntryMetadata.ShortName, xNameString);

            if (mEntryType == DirectoryEntryTypeEnum.Directory)
            {
                SetDirectoryEntryMetadataValue(FatDirectoryEntryMetadata.Attributes, FatDirectoryEntryAttributeConsts.Directory);
            }

            // TODO: Add a define for COSMOS so we can skip blocks when running outside.
            // Date and Time
            //uint xDate = ((((uint)RTC.Century * 100 + (uint)RTC.Year) - 1980) << 9) | (uint)RTC.Month << 5 | (uint)RTC.DayOfTheMonth;
            //uint xTime = (uint)RTC.Hour << 11 | (uint)RTC.Minute << 5 | ((uint)RTC.Second / 2);

            //SetDirectoryEntryMetadataValue(FatDirectoryEntryMetadata.CreationDate, xDate);
            //SetDirectoryEntryMetadataValue(FatDirectoryEntryMetadata.ModifiedDate, xDate);
            //SetDirectoryEntryMetadataValue(FatDirectoryEntryMetadata.CreationTime, xTime);
            //SetDirectoryEntryMetadataValue(FatDirectoryEntryMetadata.ModifiedTime, xTime);

            //First cluster
            SetDirectoryEntryMetadataValue(FatDirectoryEntryMetadata.FirstClusterHigh, (ushort)(mFirstClusterNum >> 16));
            SetDirectoryEntryMetadataValue(FatDirectoryEntryMetadata.FirstClusterLow, (ushort)(mFirstClusterNum & 0xFFFF));

            // GetFatTable calls GetFatChain, which "refreshes" the FAT table and clusters
            GetFatTable();
        }

        /// <summary>
        /// Add directory entry.
        /// </summary>
        /// <param name="aName">A name of the directory entry.</param>
        /// <param name="aEntryType">A type of the directory entry.</param>
        /// <returns>FatDirectoryEntry.</returns>
        /// <exception cref="ArgumentNullException">Thrown on memory error.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown on memory error / unknown directory entry type.</exception>
        /// <exception cref="OverflowException">Thrown when data lenght is greater then Int32.MaxValue.</exception>
        /// <exception cref="Exception">Thrown when data size invalid / invalid directory entry type / memory error.</exception>
        /// <exception cref="ArgumentException">Thrown on memory error.</exception>
        /// <exception cref="DecoderFallbackException">Thrown on memory error.</exception>
        /// <exception cref="RankException">Thrown on fatal error.</exception>
        /// <exception cref="ArrayTypeMismatchException">Thrown on fatal error.</exception>
        /// <exception cref="InvalidCastException">Thrown on memory error.</exception>
        public FatDirectoryEntry AddDirectoryEntry(string aName, DirectoryEntryTypeEnum aEntryType)
        {
            Global.FileSystemDebugger.SendInternal("-- FatDirectoryEntry.AddDirectoryEntry --");
            Global.FileSystemDebugger.SendInternal("aName = " + aName);
            Global.FileSystemDebugger.SendInternal($"aEntryType = {(uint)aEntryType}");

            if (aEntryType == DirectoryEntryTypeEnum.Directory || aEntryType == DirectoryEntryTypeEnum.File)
            {
                string xShortName = aName;
                uint[] xDirectoryEntriesToAllocate = null;

                #region Long Name

                if ((aEntryType == DirectoryEntryTypeEnum.File && aName.Contains(".") && (aName.Substring(0, aName.LastIndexOf('.')).Contains(".")
                        || aName.Substring(0, aName.IndexOf('.')).Length > 8 || aName.Substring(aName.IndexOf('.') + 1).Length > 3)) ||
                    (aEntryType == DirectoryEntryTypeEnum.Directory && aName.Length > 11))
                {
                    string xLongName = aName;

                    int xLastPeriodPosition = aName.LastIndexOf('.');

                    string xExt = "";

                    //Only take the name until the first dot
                    if (xLastPeriodPosition + 1 > 0 && xLastPeriodPosition + 1 < aName.Length)
                    {
                        xExt = xShortName.Substring(xLastPeriodPosition + 1);
                    }

                    //Remove all whitespaces and dots (except final)
                    for (int i = xShortName.Length - 1; i > 0; i--)
                    {
                        char xChar = xShortName[i];

                        if (char.IsWhiteSpace(xChar) || (xChar == '.' && i != xLastPeriodPosition))
                        {
                            xShortName.Remove(i, 1);
                        }
                    }

                    char[] xInvalidShortNameChars = new char[] { '"', '*', '+', ',', '.', '/', ':', ';', '<', '=', '>', '?', '[', '\\', ']', '|' };

                    //Remove all invalid characters
                    foreach (char xInvalidChar in xInvalidShortNameChars)
                    {
                        xShortName.Replace(xInvalidChar, '_');
                    }

                    int n = 1;
                    List<FatDirectoryEntry> xDirectoryEntries = ReadDirectoryContents(true);
                    string[] xShortFilenames = new string[xDirectoryEntries.Count];

                    for (int i = 0; i < xDirectoryEntries.Count; i++)
                    {
                        xShortFilenames[i] = xDirectoryEntries[i].mName;
                    }

                    string xNameTry = "";

                    bool xTest = false;

                    do
                    {
                        xNameTry = (xShortName.Substring(0, 7 - n.ToString().Length) + "~" + n).ToUpperInvariant();

                        if (!string.IsNullOrEmpty(xExt))
                        {
                            xNameTry += '.' + xExt.ToUpperInvariant();
                        }

                        n++;

                        xTest = false;

                        foreach (string name in xShortFilenames)
                        {
                            if (name == xNameTry)
                            {
                                xTest = true;
                                break;
                            }
                        }
                    }
                    //TODO: Array.TrySZIndexOf plug is not being recognized; to use the generic version of IndexOf, just remove the cast to Array
                    //while (Array.IndexOf((Array)xShortFilenames, xNameTry) != -1);
                    while (xTest);

                    xShortName = xNameTry;

                    uint xChecksum = CalculateChecksum(GetShortName(xShortName));

                    int xNumEntries = (int)Math.Ceiling(xLongName.Length / 13d);

                    char[] xLongNameWithPad = new char[xNumEntries * 13];

                    xLongNameWithPad[xLongNameWithPad.Length - 1] = (char)0xFFFF;
                    Array.Copy(xLongName.ToCharArray(), xLongNameWithPad, xLongName.Length);

                    xDirectoryEntriesToAllocate = GetNextUnallocatedDirectoryEntries(xNumEntries + 1);

                    for (int i = xNumEntries - 1; i >= 0; i--)
                    {
                        uint xEntry = xDirectoryEntriesToAllocate[xNumEntries - i - 1];

                        SetLongFilenameEntryMetadataValue(xEntry, FatDirectoryEntryMetadata.LongFilenameEntryMetadata.SequenceNumberAndAllocationStatus, (i + 1) | (i == xNumEntries - 1 ? 1 << 6 : 0));
                        SetLongFilenameEntryMetadataValue(xEntry, FatDirectoryEntryMetadata.LongFilenameEntryMetadata.Attributes, FatDirectoryEntryAttributeConsts.LongName);
                        SetLongFilenameEntryMetadataValue(xEntry, FatDirectoryEntryMetadata.LongFilenameEntryMetadata.Checksum, xChecksum);

                        var a1 = new string(xLongNameWithPad, i * 13, 5);
                        var a2 = new string(xLongNameWithPad, i * 13 + 5, 6);
                        var a3 = new string(xLongNameWithPad, i * 13 + 11, 2);

                        SetLongFilenameEntryMetadataValue(xEntry, FatDirectoryEntryMetadata.LongFilenameEntryMetadata.LongName1, a1);
                        SetLongFilenameEntryMetadataValue(xEntry, FatDirectoryEntryMetadata.LongFilenameEntryMetadata.LongName2, a2);
                        SetLongFilenameEntryMetadataValue(xEntry, FatDirectoryEntryMetadata.LongFilenameEntryMetadata.LongName3, a3);
                    }
                }

                #endregion Long Name

                string xFullPath = Path.Combine(mFullPath, aName);
                uint xFirstCluster = ((FatFileSystem)mFileSystem).GetFat(0).GetNextUnallocatedFatEntry();
                uint xEntryHeaderDataOffset = xDirectoryEntriesToAllocate == null ? GetNextUnallocatedDirectoryEntry() : xDirectoryEntriesToAllocate[xDirectoryEntriesToAllocate.Length - 1];

                Global.FileSystemDebugger.SendInternal("xFullPath = " + xFullPath);
                Global.FileSystemDebugger.SendInternal("xFirstCluster = " + xFirstCluster);
                Global.FileSystemDebugger.SendInternal("xEntryHeaderDataOffset = " + xEntryHeaderDataOffset);
                Global.FileSystemDebugger.SendInternal("xShortName = " + xShortName);

                ((FatFileSystem)mFileSystem).Write(xFirstCluster, new byte[((FatFileSystem)mFileSystem).BytesPerCluster]); // clear the cluster where directory info will be stored

                var xNewEntry = new FatDirectoryEntry((FatFileSystem)mFileSystem, this, xFullPath, aName, 0, xFirstCluster, xEntryHeaderDataOffset, aEntryType, true);

                xNewEntry.AllocateDirectoryEntry(xShortName, false);

                return xNewEntry;
            }

            throw new ArgumentOutOfRangeException(nameof(aEntryType), "Unknown directory entry type.");
        }

        /// <summary>
        /// Check if given entry is a root directory.
        /// </summary>
        /// <returns>True if it is root directory.</returns>
        private bool IsRootDirectory() => mParent == null ? true : false;

        /// <summary>
        /// Delete directory entry.
        /// </summary>
        /// <exception cref="NotImplementedException">Thrown when given entry type is unknown.</exception>
        /// <exception cref="Exception">
        /// <list type="bullet">
        /// <item>Thrown when tring to delete root directory.</item>
        /// <item>directory entry type is invalid.</item>
        /// <item>data size invalid.</item>
        /// <item>FAT table not found.</item>
        /// <item>out of memory.</item>
        /// </list>
        /// </exception>
        /// <exception cref="OverflowException">Thrown when data lenght is greater then Int32.MaxValue.</exception>
        /// <exception cref="ArgumentNullException">
        /// <list type="bullet">
        /// <item>Thrown when aData is null.</item>
        /// <item>Out of memory.</item>
        /// </list>
        /// </exception>
        /// <exception cref="RankException">Thrown on fatal error.</exception>
        /// <exception cref="ArrayTypeMismatchException">Thrown on fatal error.</exception>
        /// <exception cref="InvalidCastException">Thrown when the data in aData is corrupted.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <list type = "bullet" >
        /// <item>Thrown when the data length is 0 or greater then Int32.MaxValue.</item>
        /// <item>Entrys matadata offset value is invalid.</item>
        /// </list>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <list type="bullet">
        /// <item>aData length is 0.</item>
        /// </list>
        /// </exception>
        /// <exception cref="NotSupportedException">Thrown when FAT type is unknown.</exception>
        public void DeleteDirectoryEntry()
        {
            Global.FileSystemDebugger.SendInternal("-- FatDirectoryEntry.DeleteDirectoryEntry --");

            if (mEntryType == DirectoryEntryTypeEnum.Unknown)
            {
                throw new NotImplementedException();
            }

            if (IsRootDirectory())
            {
                throw new Exception("Root directory can not be deleted");
            }

            var xData = ((FatDirectoryEntry)mParent).GetDirectoryEntryData();

            if (mEntryHeaderDataOffset > 32)
            {
                var xEntryOffset = mEntryHeaderDataOffset - 32;

                Global.FileSystemDebugger.SendInternal("xEntryOffset: " + xEntryOffset);

                while (xData[xEntryOffset + 11] == FatDirectoryEntryAttributeConsts.LongName)
                {
                    xData[xEntryOffset] = FatDirectoryEntryAttributeConsts.UnusedOrDeletedEntry;
                    xEntryOffset -= 32;
                }
            }

            ((FatDirectoryEntry)mParent).SetDirectoryEntryData(xData);

            SetDirectoryEntryMetadataValue(FatDirectoryEntryMetadata.FirstByte, FatDirectoryEntryAttributeConsts.UnusedOrDeletedEntry);

            // GetFatTable calls GetFatChain, which "refreshes" the FAT table and clusters
            GetFatTable();
        }

        /// <summary>
        /// Retrieves a <see cref="List{T}"/> of <see cref="FatDirectoryEntry"/> objects that represent the Directory Entries inside this Directory
        /// </summary>
        /// <returns>Returns a <see cref="List{T}"/> of the Directory Entries inside this Directory</returns>
        /// <exception cref="OverflowException">Thrown when data lenght is greater then Int32.MaxValue.</exception>
        /// <exception cref="Exception">Thrown when data size invalid / invalid directory entry type.</exception>
        /// <exception cref="ArgumentException">Thrown on memory error.</exception>
        /// <exception cref="ArgumentNullException">Thrown on memory error.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown on memory error.</exception>
        /// <exception cref="DecoderFallbackException">Thrown on memory error.</exception>
        public List<FatDirectoryEntry> ReadDirectoryContents(bool aReturnShortFilenames = false)
        {
            Global.FileSystemDebugger.SendInternal("-- FatDirectoryEntry.ReadDirectoryContents --");

            var xData = GetDirectoryEntryData();
            var xResult = new List<FatDirectoryEntry>();
            FatDirectoryEntry xParent = this;

            //TODO: Change xLongName to StringBuilder
            string xLongName = "";
            string xName;

            for (uint i = 0; i < xData.Length; i += 32)
            {
                byte xAttrib = xData[i + 11];
                byte xStatus = xData[i];

                if (xAttrib == FatDirectoryEntryAttributeConsts.LongName)
                {
                    byte xType = xData[i + 12];

                    if (aReturnShortFilenames)
                    {
                        continue;
                    }

                    if (xStatus == FatDirectoryEntryAttributeConsts.UnusedOrDeletedEntry)
                    {
                        Global.FileSystemDebugger.SendInternal("<DELETED> : Attrib = " + xAttrib + ", Status = " + xStatus);
                        continue;
                    }

                    if (xType == 0)
                    {
                        if ((xStatus & 0x40) > 0)
                        {
                            xLongName = "";
                        }

                        //TODO: Check LDIR_Ord for ordering and throw exception
                        // if entries are found out of order.
                        // Also save buffer and only copy name if a end Ord marker is found.
                        string xLongPart = Encoding.Unicode.GetString(xData, (int)i + 1, 10);

                        // We have to check the length because 0xFFFF is a valid Unicode codepoint.
                        // So we only want to stop if the 0xFFFF is AFTER a 0x0000. We can determin
                        // this by also looking at the length. Since we short circuit the or, the length
                        // is rarely evaluated.
                        if (BitConverter.ToUInt16(xData, (int)i + 14) != 0xFFFF || xLongPart.Length == 5)
                        {
                            xLongPart = xLongPart + Encoding.Unicode.GetString(xData, (int)i + 14, 12);

                            if (BitConverter.ToUInt16(xData, (int)i + 28) != 0xFFFF || xLongPart.Length == 11)
                            {
                                xLongPart = xLongPart + Encoding.Unicode.GetString(xData, (int)i + 28, 4);
                            }
                        }

                        xLongName = xLongPart + xLongName;
                        //TODO: LDIR_Chksum
                    }
                }
                else
                {
                    xName = xLongName;

                    if (xStatus == 0x00)
                    {
                        Global.FileSystemDebugger.SendInternal("<EOF> : Attrib = " + xAttrib + ", Status = " + xStatus);
                        break;
                    }

                    switch (xStatus)
                    {
                        case 0x05:
                            // Japanese characters - We dont handle these
                            break;

                        case 0x2E:
                            // Dot entry
                            continue;
                        case FatDirectoryEntryAttributeConsts.UnusedOrDeletedEntry:
                            // Empty slot, skip it
                            continue;
                        default:
                            int xTest = xAttrib & (FatDirectoryEntryAttributeConsts.Directory | FatDirectoryEntryAttributeConsts.VolumeID);

                            if (xStatus >= 0x20)
                            {
                                if (xLongName.Length > 0)
                                {
                                    // Leading and trailing spaces are to be ignored according to spec.
                                    // Many programs (including Windows) pad trailing spaces although it
                                    // it is not required for long names.
                                    // As per spec, ignore trailing periods
                                    xName = xLongName.Trim(new char[] { '\0', '\uffff' }).Trim();

                                    //If there are trailing periods
                                    int nameIndex = xName.Length - 1;

                                    if (xName[nameIndex] == '.')
                                    {
                                        //Search backwards till we find the first non-period character
                                        for (; nameIndex > 0; nameIndex--)
                                        {
                                            if (xName[nameIndex] != '.')
                                            {
                                                break;
                                            }
                                        }

                                        //Substring to remove the periods
                                        xName = xName.Substring(0, nameIndex + 1);
                                    }

                                    xLongName = "";
                                }
                                else
                                {
                                    if (xTest == 0)
                                    {
                                        string xEntry = Encoding.ASCII.GetString(xData, (int)i, 11);
                                        xName = xEntry.Substring(0, 8).TrimEnd();
                                        string xExt = xEntry.Substring(8, 3).TrimEnd();

                                        if (xExt.Length > 0)
                                        {
                                            xName = xName + "." + xExt;
                                        }
                                    }
                                    else
                                    {
                                        xName = Encoding.ASCII.GetString(xData, (int)i, 11).TrimEnd();
                                    }
                                }
                            }

                            uint xFirstCluster = (uint)(BitConverter.ToUInt16(xData, (int)i + 20) << 16 | BitConverter.ToUInt16(xData, (int)i + 26));
                            if (xTest == 0)
                            {
                                uint xSize = BitConverter.ToUInt32(xData, (int)i + 28);

                                if (xSize == 0 && xName.Length == 0)
                                {
                                    continue;
                                }

                                string xFullPath = Path.Combine(mFullPath, xName);
                                var xEntry = new FatDirectoryEntry((FatFileSystem)mFileSystem, xParent, xFullPath, xName, xSize, xFirstCluster, i, DirectoryEntryTypeEnum.File);
                                xResult.Add(xEntry);
                                Global.FileSystemDebugger.SendInternal(xEntry.mName + " - " + xEntry.mSize + " bytes");
                            }
                            else if (xTest == FatDirectoryEntryAttributeConsts.Directory)
                            {
                                string xFullPath = Path.Combine(mFullPath, xName);
                                uint xSize = BitConverter.ToUInt32(xData, (int)i + 28);
                                var xEntry = new FatDirectoryEntry((FatFileSystem)mFileSystem, xParent, xFullPath, xName, xSize, xFirstCluster, i, DirectoryEntryTypeEnum.Directory);
                                Global.FileSystemDebugger.SendInternal(xEntry.mName + " <DIR> " + xEntry.mSize + " bytes : Attrib = " + xAttrib + ", Status = " + xStatus);
                                xResult.Add(xEntry);
                            }
                            else if (xTest == FatDirectoryEntryAttributeConsts.VolumeID)
                            {
                                Global.FileSystemDebugger.SendInternal("<VOLUME ID> : Attrib = " + xAttrib + ", Status = " + xStatus);
                            }
                            else
                            {
                                Global.FileSystemDebugger.SendInternal("<INVALID ENTRY> : Attrib = " + xAttrib + ", Status = " + xStatus);
                            }
                            break;
                    }
                }
            }
            Global.FileSystemDebugger.SendInternal("-- --------------------------------------- --");

            return xResult;
        }

        /// <summary>
        /// Get volume id
        /// </summary>
        /// <returns>FatDirectoryEntry.</returns>
        /// <exception cref="Exception">Thrown when trying to access VolumeId out of Root Directory / data size invalid / invalid directory entry type.</exception>
        /// <exception cref="OverflowException">Thrown when data lenght is greater then Int32.MaxValue.</exception>
        /// <exception cref="ArgumentException">Thrown on memory error.</exception>
        /// <exception cref="DecoderFallbackException">Thrown on memory error.</exception>
        /// <exception cref="ArgumentNullException">Thrown on memory error / FileSystem is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown on memory error.</exception>
        public FatDirectoryEntry FindVolumeId()
        {
            if (!IsRootDirectory())
            {
                throw new Exception("VolumeId can be found only in Root Directory");
            }

            Global.FileSystemDebugger.SendInternal("-- FatDirectoryEntry.FindVolumeId --");

            var xData = GetDirectoryEntryData();
            FatDirectoryEntry xParent = this;

            FatDirectoryEntry xResult = null;
            for (uint i = 0; i < xData.Length; i = i + 32)
            {
                byte xAttrib = xData[i + 11];

                //if ((xAttrib & FatDirectoryEntryAttributeConsts.VolumeID) != FatDirectoryEntryAttributeConsts.VolumeID)
                if (xAttrib != FatDirectoryEntryAttributeConsts.VolumeID)
                    continue;

                Global.FileSystemDebugger.SendInternal("VolumeID Found");
                /* The Label in FAT could be only a shortName (limited to 11 characters) so it is more easy */
                string xName = Encoding.ASCII.GetString(xData, (int)i, 11);
                xName = xName.TrimEnd();

                string xFullPath = Path.Combine(mFullPath, xName);
                /* Probably can be OK to hardcode 0 here */
                uint xSize = BitConverter.ToUInt32(xData, (int)i + 28);
                //uint xFirstCluster = (uint)(xData.ToUInt16(i + 20) << 16 | xData.ToUInt16(i + 26));
                uint xFirstCluster = xParent.mFirstClusterNum;

                Global.FileSystemDebugger.SendInternal($"VolumeID Found xName {xName} xFullPath {xFullPath} xSize {xSize} xFirstCluster {xFirstCluster}");

                xResult = new FatDirectoryEntry((FatFileSystem)mFileSystem, xParent, xFullPath, xName, xSize, xFirstCluster, i, DirectoryEntryTypeEnum.File);
                break;
            }

            if (xResult == null)
                Global.FileSystemDebugger.SendInternal($"VolumeID not found, returning null");

            return xResult;
        }

        /// <summary>
        /// Create volume id.
        /// </summary>
        /// <param name="name">A name of the entry.</param>
        /// <returns>Volume ID.</returns>
        /// <exception cref="ArgumentNullException">Thrown on memory error.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown on memory error / unknown directory entry type.</exception>
        /// <exception cref="OverflowException">Thrown when data lenght is greater then Int32.MaxValue.</exception>
        /// <exception cref="Exception">Thrown when called on a directory other then root / data size invalid / invalid directory entry type / memory error.</exception>
        /// <exception cref="ArgumentException">Thrown on memory error.</exception>
        /// <exception cref="DecoderFallbackException">Thrown on memory error.</exception>
        /// <exception cref="RankException">Thrown on fatal error.</exception>
        /// <exception cref="ArrayTypeMismatchException">Thrown on fatal error.</exception>
        /// <exception cref="InvalidCastException">Thrown on memory error.</exception>
        public FatDirectoryEntry CreateVolumeId(string name)
        {
            if (!IsRootDirectory())
            {
                throw new Exception("VolumeId can be created only in Root Directory");
            }

            // VolumeId is really a special type of File with attribute 'VolumeID' set
            var VolumeId = AddDirectoryEntry(name, DirectoryEntryTypeEnum.File);
            VolumeId.SetDirectoryEntryMetadataValue(FatDirectoryEntryMetadata.Attributes, FatDirectoryEntryAttributeConsts.VolumeID);

            return VolumeId;
        }

        /// <summary>
        /// Tries to find an empty space for a directory entry and returns the offset to that space if successful, otherwise throws an exception.
        /// </summary>
        /// <returns>Returns the offset to the next unallocated directory entry.</returns>
        /// <exception cref="OverflowException">Thrown when data lenght is greater then Int32.MaxValue.</exception>
        /// <exception cref="Exception">Thrown when unallocated memory block not found / invalid directory entry type.</exception>
        private uint GetNextUnallocatedDirectoryEntry()
        {
            Global.FileSystemDebugger.SendInternal("-- FatDirectoryEntry.GetNextUnallocatedDirectoryEntry --");

            var xData = GetDirectoryEntryData();
            for (uint i = 0; i < xData.Length; i += 32)
            {
                uint x1 = BitConverter.ToUInt32(xData, (int)i);
                uint x2 = BitConverter.ToUInt32(xData, (int)i + 8);
                uint x3 = BitConverter.ToUInt32(xData, (int)i + 16);
                uint x4 = BitConverter.ToUInt32(xData, (int)i + 24);
                if (x1 == 0 && x2 == 0 && x3 == 0 && x4 == 0)
                {
                    Global.FileSystemDebugger.SendInternal("Returning i =" + i);
                    Global.FileSystemDebugger.SendInternal("-- -------------------------------------------------- --");
                    return i;
                }
            }

            // TODO: What should we return if no available entry is found. - Update Method description above.
            throw new Exception("Failed to find an unallocated directory entry.");
        }

        /// <summary>
        /// Tries to find an empty space for the specified number of directory entries and returns an array of offsets to those spaces if successful, otherwise throws an exception.
        /// </summary>
        /// <param name="aEntryCount">The number of entried to allocate.</param>
        /// <returns>Returns an array of offsets to the next unallocated directory entries.</returns>
        /// <exception cref="OverflowException">Thrown when data lenght is greater then Int32.MaxValue.</exception>
        /// <exception cref="Exception">Thrown when requested memory block of the size of aEntryCount not found / invalid directory entry type.</exception>
        private uint[] GetNextUnallocatedDirectoryEntries(int aEntryCount)
        {
            Global.FileSystemDebugger.SendInternal("-- FatDirectoryEntry.GetNextUnallocatedDirectoryEntry --");

            var xData = GetDirectoryEntryData();
            int xCount = 0;
            uint[] xEntries = new uint[aEntryCount];

            for (uint i = 0; i < xData.Length; i += 32)
            {
                uint x1 = BitConverter.ToUInt32(xData, (int)i);
                uint x2 = BitConverter.ToUInt32(xData, (int)i + 8);
                uint x3 = BitConverter.ToUInt32(xData, (int)i + 16);
                uint x4 = BitConverter.ToUInt32(xData, (int)i + 24);
                if (x1 == 0 && x2 == 0 && x3 == 0 && x4 == 0)
                {
                    xEntries[xCount] = i;
                    xCount++;

                    if (aEntryCount == xCount)
                    {
                        return xEntries;
                    }
                }
                else
                {
                    xCount = 0;
                }
            }

            // TODO: What should we return if no available entry is found. - Update Method description above.
            throw new Exception($"Failed to find {aEntryCount} unallocated directory entries.");
        }

        /// <summary>
        /// Get directory entry data.
        /// </summary>
        /// <returns>byte array.</returns>
        /// <exception cref="OverflowException">Thrown when data lenght is greater then Int32.MaxValue.</exception>
        /// <exception cref="Exception">Thrown when data size invalid / invalid directory entry type.</exception>
        private byte[] GetDirectoryEntryData()
        {
            Global.FileSystemDebugger.SendInternal("-- FatDirectoryEntry.GetDirectoryEntryData --");
            Global.FileSystemDebugger.SendInternal("mFirstClusterNum:" + mFirstClusterNum);

            if (mEntryType != DirectoryEntryTypeEnum.Unknown)
            {
                byte[] xData;
                ((FatFileSystem)mFileSystem).Read(mFirstClusterNum, out xData);
                Global.FileSystemDebugger.SendInternal("-- --------------------------------------- --");
                return xData;
            }

            throw new Exception("Invalid directory entry type");
        }

        /// <summary>
        /// Set directory entry data.
        /// </summary>
        /// <param name="aData">A data to set to the directory entry.</param>
        /// <exception cref="Exception">Thrown when directory entry type is invalid.</exception>
        /// <exception cref="OverflowException">Thrown when data lenght is greater then Int32.MaxValue.</exception>
        /// <exception cref="ArgumentNullException">
        /// <list type="bullet">
        /// <item>Thrown when aData is null.</item>
        /// <item>Out of memory.</item>
        /// </list>
        /// </exception>
        /// <exception cref="RankException">Thrown on fatal error.</exception>
        /// <exception cref="ArrayTypeMismatchException">Thrown on fatal error.</exception>
        /// <exception cref="InvalidCastException">Thrown when the data in aData is corrupted.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <list type = "bullet" >
        /// <item>Thrown when the data length is 0 or greater then Int32.MaxValue.</item>
        /// <item>Entrys matadata offset value is invalid.</item>
        /// </list>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <list type="bullet">
        /// <item>aData length is 0.</item>
        /// </list>
        /// </exception>
        private void SetDirectoryEntryData(byte[] aData)
        {
            Global.FileSystemDebugger.SendInternal("-- FatDirectoryEntry.SetDirectoryEntryData(byte) --");
            Global.FileSystemDebugger.SendInternal("aData.Length = " + aData.Length);

            if (aData == null)
            {
                throw new ArgumentNullException(nameof(aData));
            }

            if (aData.Length == 0)
            {
                throw new ArgumentException("aData does not contain any data.", nameof(aData));
            }

            if (mEntryType != DirectoryEntryTypeEnum.Unknown)
            {
                ((FatFileSystem)mFileSystem).Write(mFirstClusterNum, aData);
            }
            else
            {
                throw new Exception("Invalid directory entry type");
            }
        }

        /// <summary>
        /// Set directory entry metadata value.
        /// </summary>
        /// <param name="aEntryMetadata">A entry metadata</param>
        /// <param name="aValue">A byte value</param>
        /// <exception cref="Exception">
        /// <list type="bullet">
        /// <item>Thrown when trying to change root directory matadata.</item>
        /// <item>data size invalid.</item>
        /// <item>invalid directory entry type.</item>
        /// </list>
        /// </exception>
        /// <exception cref="OverflowException">Thrown when data lenght is greater then Int32.MaxValue.</exception>
        /// <exception cref="ArgumentNullException">
        /// <list type="bullet">
        /// <item>Thrown when entrys aValue is null.</item>
        /// <item>Thrown when entrys aData is null.</item>
        /// <item>Out of memory.</item>
        /// </list>
        /// </exception>
        /// <exception cref="RankException">Thrown on fatal error.</exception>
        /// <exception cref="ArrayTypeMismatchException">Thrown on fatal error.</exception>
        /// <exception cref="InvalidCastException">Thrown when the data in aValue is corrupted.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <list type = "bullet" >
        /// <item>Thrown when the data length is 0 or greater then Int32.MaxValue.</item>
        /// <item>Entrys matadata offset value is invalid.</item>
        /// </list>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <list type="bullet">
        /// <item>Thrown when aName is null or empty string.</item>
        /// <item>aData length is 0.</item>
        /// </list>
        /// </exception>
        internal void SetDirectoryEntryMetadataValue(FatDirectoryEntryMetadata aEntryMetadata, byte aValue)
        {
            Global.FileSystemDebugger.SendInternal(" -- FatDirectoryEntry.SetDirectoryEntryMetadataValue(uint) --");
            Global.FileSystemDebugger.SendInternal("aEntryMetadata = " + aEntryMetadata.DataOffset);
            Global.FileSystemDebugger.SendInternal("aValue = " + aValue);

            if (IsRootDirectory())
            {
                throw new Exception("Root directory metadata can not be changed using the file stream.");
            }

            var xData = ((FatDirectoryEntry)mParent).GetDirectoryEntryData();

            if (xData.Length > 0)
            {
                uint xOffset = mEntryHeaderDataOffset + aEntryMetadata.DataOffset;
                xData[xOffset] = aValue;
                ((FatDirectoryEntry)mParent).SetDirectoryEntryData(xData);
            }
        }

        /// <summary>
        /// Set directory entry metadata value.
        /// </summary>
        /// <param name="aEntryMetadata">A entry metadata</param>
        /// <param name="aValue">A ushort value</param>
        /// <exception cref="Exception">
        /// <list type="bullet">
        /// <item>Thrown when trying to change root directory matadata.</item>
        /// <item>data size invalid.</item>
        /// <item>invalid directory entry type.</item>
        /// </list>
        /// </exception>
        /// <exception cref="OverflowException">Thrown when data lenght is greater then Int32.MaxValue.</exception>
        /// <exception cref="ArgumentNullException">
        /// <list type="bullet">
        /// <item>Thrown when entrys aValue is null.</item>
        /// <item>Thrown when entrys aData is null.</item>
        /// <item>Out of memory.</item>
        /// </list>
        /// </exception>
        /// <exception cref="RankException">Thrown on fatal error.</exception>
        /// <exception cref="ArrayTypeMismatchException">Thrown on fatal error.</exception>
        /// <exception cref="InvalidCastException">Thrown when the data in aValue is corrupted.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <list type = "bullet" >
        /// <item>Thrown when the data length is 0 or greater then Int32.MaxValue.</item>
        /// <item>Entrys matadata offset value is invalid.</item>
        /// </list>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <list type="bullet">
        /// <item>Thrown when aName is null or empty string.</item>
        /// <item>aData length is 0.</item>
        /// </list>
        /// </exception>
        internal void SetDirectoryEntryMetadataValue(FatDirectoryEntryMetadata aEntryMetadata, ushort aValue)
        {
            Global.FileSystemDebugger.SendInternal(" -- FatDirectoryEntry.SetDirectoryEntryMetadataValue(uint) --");
            Global.FileSystemDebugger.SendInternal("aEntryMetadata = " + aEntryMetadata.DataOffset);
            Global.FileSystemDebugger.SendInternal("aValue = " + aValue);

            if (IsRootDirectory())
            {
                throw new Exception("Root directory metadata can not be changed using the file stream.");
            }

            var xData = ((FatDirectoryEntry)mParent).GetDirectoryEntryData();

            if (xData.Length > 0)
            {
                Global.FileSystemDebugger.SendInternal("mEntryHeaderDataOffset = " + mEntryHeaderDataOffset);
                var xValue = new byte[aEntryMetadata.DataLength];
                xValue.SetUInt16(0, aValue);
                uint offset = mEntryHeaderDataOffset + aEntryMetadata.DataOffset;
                Array.Copy(xValue, 0, xData, offset, aEntryMetadata.DataLength);
                ((FatDirectoryEntry)mParent).SetDirectoryEntryData(xData);
            }
            Global.FileSystemDebugger.SendInternal(" -- ------------------------------------------------------ --");
        }

        /// <summary>
        /// Set directory entry metadata value.
        /// </summary>
        /// <param name="aEntryMetadata">A entry metadata</param>
        /// <param name="aValue">A uint value</param>
        /// <exception cref="Exception">
        /// <list type="bullet">
        /// <item>Thrown when trying to change root directory matadata.</item>
        /// <item>data size invalid.</item>
        /// <item>invalid directory entry type.</item>
        /// </list>
        /// </exception>
        /// <exception cref="OverflowException">Thrown when data lenght is greater then Int32.MaxValue.</exception>
        /// <exception cref="ArgumentNullException">
        /// <list type="bullet">
        /// <item>Thrown when entrys aValue is null.</item>
        /// <item>Thrown when entrys aData is null.</item>
        /// <item>Out of memory.</item>
        /// </list>
        /// </exception>
        /// <exception cref="RankException">Thrown on fatal error.</exception>
        /// <exception cref="ArrayTypeMismatchException">Thrown on fatal error.</exception>
        /// <exception cref="InvalidCastException">Thrown when the data in aValue is corrupted.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <list type = "bullet" >
        /// <item>Thrown when the data length is 0 or greater then Int32.MaxValue.</item>
        /// <item>Entrys matadata offset value is invalid.</item>
        /// </list>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <list type="bullet">
        /// <item>Thrown when aName is null or empty string.</item>
        /// <item>aData length is 0.</item>
        /// </list>
        /// </exception>
        internal void SetDirectoryEntryMetadataValue(FatDirectoryEntryMetadata aEntryMetadata, uint aValue)
        {
            Global.FileSystemDebugger.SendInternal(" -- FatDirectoryEntry.SetDirectoryEntryMetadataValue(uint) --");
            Global.FileSystemDebugger.SendInternal("aEntryMetadata = " + aEntryMetadata.DataOffset);
            Global.FileSystemDebugger.SendInternal("aValue = " + aValue);

            if (IsRootDirectory())
            {
                throw new Exception("Root directory metadata can not be changed using the file stream.");
            }

            var xData = ((FatDirectoryEntry)mParent).GetDirectoryEntryData();

            if (xData.Length > 0)
            {
                var xValue = new byte[aEntryMetadata.DataLength];
                xValue.SetUInt32(0, aValue);
                uint offset = mEntryHeaderDataOffset + aEntryMetadata.DataOffset;
                Array.Copy(xValue, 0, xData, offset, aEntryMetadata.DataLength);
                ((FatDirectoryEntry)mParent).SetDirectoryEntryData(xData);
            }
        }

        /// <summary>
        /// Set directory entry metadata value.
        /// </summary>
        /// <param name="aEntryMetadata">A entry metadata</param>
        /// <param name="aValue">A long value</param>
        /// <exception cref="Exception">
        /// <list type="bullet">
        /// <item>Thrown when trying to change root directory matadata.</item>
        /// <item>data size invalid.</item>
        /// <item>invalid directory entry type.</item>
        /// </list>
        /// </exception>
        /// <exception cref="OverflowException">Thrown when data lenght is greater then Int32.MaxValue.</exception>
        /// <exception cref="ArgumentNullException">
        /// <list type="bullet">
        /// <item>Thrown when entrys aValue is null.</item>
        /// <item>Thrown when entrys aData is null.</item>
        /// <item>Out of memory.</item>
        /// </list>
        /// </exception>
        /// <exception cref="RankException">Thrown on fatal error.</exception>
        /// <exception cref="ArrayTypeMismatchException">Thrown on fatal error.</exception>
        /// <exception cref="InvalidCastException">Thrown when the data in aValue is corrupted.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <list type = "bullet" >
        /// <item>Thrown when the data length is 0 or greater then Int32.MaxValue.</item>
        /// <item>Entrys matadata offset value is invalid.</item>
        /// </list>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <list type="bullet">
        /// <item>Thrown when aName is null or empty string.</item>
        /// <item>aData length is 0.</item>
        /// </list>
        /// </exception>
        internal void SetDirectoryEntryMetadataValue(FatDirectoryEntryMetadata aEntryMetadata, long aValue)
        {
            Global.FileSystemDebugger.SendInternal("-- FatDirectoryEntry.SetDirectoryEntryMetadataValue(long) --");
            Global.FileSystemDebugger.SendInternal("aEntryMetadata = " + aEntryMetadata.DataOffset);
            Global.FileSystemDebugger.SendInternal("aValue =");
            Global.FileSystemDebugger.SendInternal(aValue);

            if (IsRootDirectory())
            {
                throw new Exception("Root directory metadata can not be changed using the file stream.");
            }

            var xData = ((FatDirectoryEntry)mParent).GetDirectoryEntryData();

            if (xData.Length > 0)
            {
                var xValue = new byte[aEntryMetadata.DataLength];
                xValue.SetUInt32(0, (uint)aValue);
                uint offset = mEntryHeaderDataOffset + aEntryMetadata.DataOffset;
                Global.FileSystemDebugger.SendInternal("offset =");
                Global.FileSystemDebugger.SendInternal(offset);
                Array.Copy(xValue, 0, xData, offset, aEntryMetadata.DataLength);
                ((FatDirectoryEntry)mParent).SetDirectoryEntryData(xData);
            }
        }

        /// <summary>
        /// Set directory entry metadata value.
        /// </summary>
        /// <param name="aEntryMetadata">A entry metadata</param>
        /// <param name="aValue">A string value</param>
        /// <exception cref="Exception">
        /// <list type="bullet">
        /// <item>Thrown when trying to change root directory matadata.</item>
        /// <item>data size invalid.</item>
        /// <item>invalid directory entry type.</item>
        /// </list>
        /// </exception>
        /// <exception cref="OverflowException">Thrown when data lenght is greater then Int32.MaxValue.</exception>
        /// <exception cref="EncoderFallbackException">Thrown when encoder fallback operation on aValue fails.</exception>
        /// <exception cref="ArgumentNullException">
        /// <list type="bullet">
        /// <item>Thrown when entrys aValue is null.</item>
        /// <item>Thrown when entrys aData is null.</item>
        /// <item>Out of memory.</item>
        /// </list>
        /// </exception>
        /// <exception cref="RankException">Thrown on fatal error.</exception>
        /// <exception cref="ArrayTypeMismatchException">Thrown on fatal error.</exception>
        /// <exception cref="InvalidCastException">Thrown when the data in aValue is corrupted.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <list type = "bullet" >
        /// <item>Thrown when the data length is 0 or greater then Int32.MaxValue.</item>
        /// <item>Entrys matadata offset value is invalid.</item>
        /// </list>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <list type="bullet">
        /// <item>Thrown when aName is null or empty string.</item>
        /// <item>aData length is 0.</item>
        /// </list>
        /// </exception>
        internal void SetDirectoryEntryMetadataValue(FatDirectoryEntryMetadata aEntryMetadata, string aValue)
        {
            Global.FileSystemDebugger.SendInternal("-- FatDirectoryEntry.SetDirectoryEntryMetadataValue(string) --");
            Global.FileSystemDebugger.SendInternal("aEntryMetadata = " + aEntryMetadata.DataOffset);
            Global.FileSystemDebugger.SendInternal($"aValue = {aValue}");

            if (IsRootDirectory())
            {
                throw new Exception("Root directory metadata can not be changed using the file stream.");
            }

            var xData = ((FatDirectoryEntry)mParent).GetDirectoryEntryData();

            if (xData.Length > 0)
            {
                var xValue = new byte[aEntryMetadata.DataLength];
                var bValue = Encoding.UTF8.GetBytes(aValue);

                for (int i = 0; i < xValue.Length; i++)
                {
                    if (i < bValue.Length) xValue[i] = bValue[i];
                    else xValue[i] = 32;
                }

                uint offset = mEntryHeaderDataOffset + aEntryMetadata.DataOffset;
                Array.Copy(xValue, 0, xData, offset, aEntryMetadata.DataLength);

                ((FatDirectoryEntry)mParent).SetDirectoryEntryData(xData);
            }
        }

        /// <summary>
        /// Set long filename entry metadata value.
        /// </summary>
        /// <param name="aEntryHeaderDataOffset">A entry header data offset.</param>
        /// <param name="aEntryMetadata">A matadata object.</param>
        /// <param name="aValue">A uint value to set.</param>
        /// <exception cref="OverflowException">Thrown when data lenght is greater then Int32.MaxValue.</exception>
        /// <exception cref="Exception">Thrown when data size invalid / invalid directory entry type.</exception>
        /// <exception cref="ArgumentNullException">
        /// <list type="bullet">
        /// <item>Thrown when aData is null.</item>
        /// <item>Out of memory.</item>
        /// </list>
        /// </exception>
        /// <exception cref="RankException">Thrown on fatal error.</exception>
        /// <exception cref="ArrayTypeMismatchException">Thrown on fatal error.</exception>
        /// <exception cref="InvalidCastException">Thrown when the data in aData is corrupted.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <list type = "bullet" >
        /// <item>Thrown when the data length is 0 or greater then Int32.MaxValue.</item>
        /// <item>Entrys matadata offset value is invalid.</item>
        /// </list>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <list type="bullet">
        /// <item>aData length is 0.</item>
        /// </list>
        /// </exception>
        internal void SetLongFilenameEntryMetadataValue(uint aEntryHeaderDataOffset, FatDirectoryEntryMetadata aEntryMetadata, uint aValue)
        {
            Global.FileSystemDebugger.SendInternal(" -- FatDirectoryEntry.SetLongFilenameEntryMetadataValue(uint) --");
            Global.FileSystemDebugger.SendInternal("aValue = " + aValue);

            var xData = GetDirectoryEntryData();

            if (xData.Length > 0)
            {
                var xValue = new byte[aEntryMetadata.DataLength * 4];
                xValue.SetUInt32(0, aValue);
                uint offset = aEntryHeaderDataOffset + aEntryMetadata.DataOffset;
                Array.Copy(xValue, 0, xData, (int)offset, (int)aEntryMetadata.DataLength * 4);
                SetDirectoryEntryData(xData);
            }
        }

        /// <summary>
        /// Set long filename entry metadata value.
        /// </summary>
        /// <param name="aEntryHeaderDataOffset">A entry header data offset.</param>
        /// <param name="aEntryMetadata">A matadata object.</param>
        /// <param name="aValue">A long value to set.</param>
        /// <exception cref="OverflowException">Thrown when data lenght is greater then Int32.MaxValue.</exception>
        /// <exception cref="Exception">Thrown when data size invalid / invalid directory entry type.</exception>
        /// <exception cref="ArgumentNullException">
        /// <list type="bullet">
        /// <item>Thrown when aData is null.</item>
        /// <item>Out of memory.</item>
        /// </list>
        /// </exception>
        /// <exception cref="RankException">Thrown on fatal error.</exception>
        /// <exception cref="ArrayTypeMismatchException">Thrown on fatal error.</exception>
        /// <exception cref="InvalidCastException">Thrown when the data in aData is corrupted.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <list type = "bullet" >
        /// <item>Thrown when the data length is 0 or greater then Int32.MaxValue.</item>
        /// <item>Entrys matadata offset value is invalid.</item>
        /// </list>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <list type="bullet">
        /// <item>aData length is 0.</item>
        /// </list>
        /// </exception>
        internal void SetLongFilenameEntryMetadataValue(uint aEntryHeaderDataOffset, FatDirectoryEntryMetadata aEntryMetadata, long aValue)
        {
            Global.FileSystemDebugger.SendInternal("-- FatDirectoryEntry.SetLongFilenameEntryMetadataValue(long) --");
            Global.FileSystemDebugger.SendInternal("aValue = " + aValue);

            var xData = GetDirectoryEntryData();

            if (xData.Length > 0)
            {
                Global.FileSystemDebugger.SendInternal("length = " + aEntryMetadata.DataLength);
                var xValue = new byte[aEntryMetadata.DataLength * 4];
                xValue.SetUInt32(0, (uint)aValue);
                uint offset = aEntryHeaderDataOffset + aEntryMetadata.DataOffset;
                Global.FileSystemDebugger.SendInternal("offset = " + offset);
                Array.Copy(xValue, 0, xData, (int)offset, (int)aEntryMetadata.DataLength * 4);
                SetDirectoryEntryData(xData);
            }
        }

        /// <summary>
        /// Set long filename entry metadata value.
        /// </summary>
        /// <param name="aEntryHeaderDataOffset">A entry header data offset.</param>
        /// <param name="aEntryMetadata">A matadata object.</param>
        /// <param name="aValue">A string value to set.</param>
        /// <exception cref="OverflowException">Thrown when data lenght is greater then Int32.MaxValue.</exception>
        /// <exception cref="Exception">Thrown when data size invalid / invalid directory entry type.</exception>
        /// <exception cref="ArgumentNullException">
        /// <list type="bullet">
        /// <item>Thrown when aData is null.</item>
        /// <item>Out of memory.</item>
        /// </list>
        /// </exception>
        /// <exception cref="EncoderFallbackException">Thrown when encoder fallback operation on aValue fails.</exception>
        /// <exception cref="RankException">Thrown on fatal error.</exception>
        /// <exception cref="ArrayTypeMismatchException">Thrown on fatal error.</exception>
        /// <exception cref="InvalidCastException">Thrown when the data in aData is corrupted.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <list type = "bullet" >
        /// <item>Thrown when the data length is 0 or greater then Int32.MaxValue.</item>
        /// <item>Entrys matadata offset value is invalid.</item>
        /// </list>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <list type="bullet">
        /// <item>aData length is 0.</item>
        /// </list>
        /// </exception>
        internal void SetLongFilenameEntryMetadataValue(uint aEntryHeaderDataOffset, FatDirectoryEntryMetadata aEntryMetadata, string aValue)
        {
            Global.FileSystemDebugger.SendInternal("-- FatDirectoryEntry.SetLongFilenameEntryMetadataValue(string) --");
            Global.FileSystemDebugger.SendInternal("aValue = " + aValue);

            var xData = GetDirectoryEntryData();

            if (xData.Length > 0)
            {
                var xValue = Encoding.Unicode.GetBytes(aValue);

                uint offset = aEntryHeaderDataOffset + aEntryMetadata.DataOffset;
                Array.Copy(xValue, 0, xData, (int)offset, (int)aEntryMetadata.DataLength);

                SetDirectoryEntryData(xData);
            }
        }

        /// <summary>
        /// Gets the short filename to be written to the FAT directory entry.
        /// </summary>
        /// <param name="aShortName">The short filename.</param>
        /// <returns>Returns the short filename to be written to the FAT directory entry.</returns>
        /// <exception cref="OverflowException">Thrown on fatal error.</exception>
        internal static string GetShortName(string aShortName)
        {
            char[] xName = new char[11];

            for (int i = 0; i < xName.Length; i++)
            {
                xName[i] = (char)0x20;
            }

            int j = 0;

            for (int i = 0; i < aShortName.Length; i++)
            {
                if (aShortName[i] == '.')
                {
                    i++;
                    j = 8;
                }

                if (i > xName.Length)
                {
                    break;
                }

                xName[j] = aShortName[i];

                j++;
            }

            return new string(xName);
        }

        /// <summary>
        /// Calculates the checksum for a given short filename.
        /// </summary>
        /// <param name="aShortName">The short filename without the extension period.</param>
        /// <returns>Returns the checksum for the given short filename.</returns>
        internal static uint CalculateChecksum(string aShortName)
        {
            uint xChecksum = 0;

            for (int i = 0; i < 11; i++)
            {
                xChecksum = (((xChecksum & 1) << 7) | ((xChecksum & 0xFE) >> 1)) + aShortName[i];
            }

            return xChecksum;
        }

        /// <summary>
        /// Get directory entry size.
        /// </summary>
        /// <param name="DirectoryEntryData">Directory entry data.</param>
        /// <returns>long value.</returns>
        /// <exception cref="ArgumentException">Thrown when DirectoryEntryData array is too short.</exception>
        /// <exception cref="ArgumentNullException">Thrown when DirectoryEntryData array is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown on fatal error.</exception>
        /// <exception cref="OverflowException">Thrown when data lenght is greater then Int32.MaxValue.</exception>
        /// <exception cref="Exception">Thrown when data size invalid.</exception>
        private long GetDirectoryEntrySize(byte[] DirectoryEntryData)
        {
            long xResult = 0;

            for (uint i = 0; i < DirectoryEntryData.Length; i = i + 32)
            {
                byte xAttrib = DirectoryEntryData[i + 11];
                byte xStatus = DirectoryEntryData[i];

                if (xAttrib == FatDirectoryEntryAttributeConsts.LongName)
                {
                    //Global.mFileSystemDebugger.SendInternal($"-- FatDirectoryEntry.GetDirectoryEntrySize() LongName DirEntry skipped!");
                    continue;
                }

                if (xStatus == 0x00)
                {
                    //Global.mFileSystemDebugger.SendInternal("<EOF> : Attrib = " + xAttrib + ", Status = " + xStatus);
                    break;
                }

                switch (xStatus)
                {
                    case 0x05:
                        // Japanese characters - We dont handle these
                        continue;
                    case 0x2E:
                        // Dot entry
                        continue;
                    case FatDirectoryEntryAttributeConsts.UnusedOrDeletedEntry:
                        // Empty slot, skip it
                        continue;

                    default:
                        break;
                }

                int xTest = xAttrib & (FatDirectoryEntryAttributeConsts.Directory | FatDirectoryEntryAttributeConsts.VolumeID);

                switch (xTest)
                {
                    // Normal file
                    case 0:
                        uint xSize = BitConverter.ToUInt32(DirectoryEntryData, (int)i + 28);
                        xResult += xSize;
                        break;

                    case FatDirectoryEntryAttributeConsts.Directory:
                        //Global.mFileSystemDebugger.SendInternal($"-- FatDirectoryEntry.GetDirectoryEntrySize() found directory: recursing!");

                        uint xFirstCluster = (uint)(BitConverter.ToUInt16(DirectoryEntryData, (int)i + 20) << 16 | BitConverter.ToUInt16(DirectoryEntryData, (int)i + 26));
                        byte[] xDirData;
                        ((FatFileSystem)mFileSystem).Read(xFirstCluster, out xDirData);

                        xResult += GetDirectoryEntrySize(xDirData);
                        break;

                    case FatDirectoryEntryAttributeConsts.VolumeID:
                        //Global.mFileSystemDebugger.SendInternal("<VOLUME ID>: skipped");
                        continue;

                    default:
                        //Global.mFileSystemDebugger.SendInternal("<INVALID ENTRY>: skipped");
                        continue;
                }
            }

            //Global.mFileSystemDebugger.SendInternal($"-- FatDirectoryEntry.GetDirectoryEntrySize() is {xResult} bytes");
            return xResult;
        }

        /*
         * Please note that this could become slower and slower as the partion becomes greater this could be optimized in two ways:
         * 1. Compute the value using this function on FS inizialization and write the difference between TotalSpace and the computed
         *    value to the specif field of 'FS Information Sector' of FAT32
         * 2. Compute the value using this function on FS inizialization and write the difference between TotalSpace and the computed
         *    value in a sort of memory cache in VFS itself
         *
         *    In any case if one of this two methods will be used in the future when a file is removed or new data are written on it,
         *    the value on the field should be always updated.
         */

        /// <summary>
        /// Get used space on directory.
        /// </summary>
        /// <returns>long value, space used (bytes)</returns>
        /// <exception cref="ArgumentException">Thrown when directory entry data corrupted.</exception>
        /// <exception cref="ArgumentNullException">Thrown when directory entry data is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown on fatal error.</exception>
        /// <exception cref="OverflowException">Thrown when data lenght is greater then Int32.MaxValue.</exception>
        /// <exception cref="Exception">Thrown when data size invalid.</exception>
        public override long GetUsedSpace()
        {
            Global.FileSystemDebugger.SendInternal($"-- FatDirectoryEntry.GetUsedSpace() on Directory {mName} ---");

            long xResult = 0;

            var xData = GetDirectoryEntryData();

            xResult += GetDirectoryEntrySize(xData);

            Global.FileSystemDebugger.SendInternal($"-- FatDirectoryEntry.GetUsedSpace() is {xResult} bytes");
            return xResult;
        }
    }
}
