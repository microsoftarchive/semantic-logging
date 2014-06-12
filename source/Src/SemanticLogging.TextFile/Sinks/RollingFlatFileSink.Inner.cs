// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks
{
    public partial class RollingFlatFileSink
    {
        /// <summary>
        /// A data time provider.
        /// </summary>
        public class DateTimeProvider
        {
            /// <summary>
            /// Gets the current data time.
            /// </summary>
            /// <value>
            /// The current data time.
            /// </value>
            public virtual DateTime CurrentDateTime
            {
                get { return DateTime.Now; }
            }
        }

        /// <summary>
        /// Encapsulates the logic to perform rolls.
        /// </summary>
        /// <remarks>
        /// If no rolling behavior has been configured no further processing will be performed.
        /// </remarks>
        public sealed class StreamWriterRollingHelper : IDisposable
        {
            private DateTimeProvider dateTimeProvider;

            /// <summary>
            /// A tally keeping writer used when file size rolling is configured.<para/>
            /// The original stream writer from the base trace listener will be replaced with
            /// this listener.
            /// </summary>
            private TallyKeepingFileStreamWriter managedWriter;

            private DateTime? nextRollDateTime;

            /// <summary>
            /// The trace listener for which rolling is being managed.
            /// </summary>
            private RollingFlatFileSink owner;

            /// <summary>
            /// A flag indicating whether at least one rolling criteria has been configured.
            /// </summary>
            private bool performsRolling;

            /// <summary>
            /// Initializes a new instance of the <see cref="StreamWriterRollingHelper"/>.
            /// </summary>
            /// <param name="owner">The <see cref="RollingFlatFileSink"/> to use.</param>
            public StreamWriterRollingHelper(RollingFlatFileSink owner)
            {
                this.owner = owner;
                this.dateTimeProvider = new DateTimeProvider();

                this.performsRolling = this.owner.rollInterval != RollInterval.None || this.owner.rollSizeInBytes > 0;
            }

            /// <summary>
            /// Gets or sets current date time provider.
            /// </summary>
            /// <value>
            /// The provider for the current date. Necessary for unit testing.
            /// </value>
            public DateTimeProvider DateTimeProvider
            {
                get { return this.dateTimeProvider; }
                set { this.dateTimeProvider = value; }
            }

            /// <summary>
            /// Gets the next date when date based rolling should occur if configured.
            /// </summary>
            /// <value>
            /// The next date when date based rolling should occur if configured.
            /// </value>
            public DateTime? NextRollDateTime
            {
                get { return this.nextRollDateTime; }
            }

            /// <summary>
            /// Calculates the next roll date for the file.
            /// </summary>
            /// <param name="dateTime">The new date.</param>
            /// <returns>The new date time to use.</returns>
            public DateTime CalculateNextRollDate(DateTime dateTime)
            {
                try
                {
                    switch (this.owner.rollInterval)
                    {
                        case RollInterval.Minute:
                            return dateTime.AddMinutes(1);
                        case RollInterval.Hour:
                            return dateTime.AddHours(1);
                        case RollInterval.Day:
                            return dateTime.AddDays(1);
                        case RollInterval.Week:
                            return dateTime.AddDays(7);
                        case RollInterval.Month:
                            return dateTime.AddMonths(1);
                        case RollInterval.Year:
                            return dateTime.AddYears(1);
                        case RollInterval.Midnight:
                            return dateTime.AddDays(1).Date;
                        default:
                            return DateTime.MaxValue;
                    }
                }
                catch (ArgumentOutOfRangeException)
                {
                    return DateTime.MaxValue;
                }
            }

            /// <summary>
            /// Checks whether rolling should be performed, and returns the date to use when performing the roll.
            /// </summary>
            /// <returns>The date roll to use if performing a roll, or <see langword="null"/> if no rolling should occur.</returns>
            /// <remarks>
            /// Defer request for the roll date until it is necessary to avoid overhead.<para/>
            /// Information used for rolling checks should be set by now.
            /// </remarks>
            public DateTime? CheckIsRollNecessary()
            {
                // check for size roll, if enabled.
                if (this.owner.rollSizeInBytes > 0
                    && (this.managedWriter != null && this.managedWriter.Tally > this.owner.rollSizeInBytes))
                {
                    return this.dateTimeProvider.CurrentDateTime;
                }

                // check for date roll, if enabled.
                DateTime currentDateTime = this.dateTimeProvider.CurrentDateTime;
                if (this.owner.rollInterval != RollInterval.None
                    && (this.nextRollDateTime != null && currentDateTime.CompareTo(this.nextRollDateTime.Value) >= 0))
                {
                    return currentDateTime;
                }

                // no roll is necessary, return a null roll date
                return null;
            }

            /// <summary>
            /// Gets the file name to use for archiving the file.
            /// </summary>
            /// <param name="actualFileName">The actual file name.</param>
            /// <param name="currentDateTime">The current date and time.</param>
            /// <returns>The new file name.</returns>
            public string ComputeArchiveFileName(string actualFileName, DateTime currentDateTime)
            {
                string directory = Path.GetDirectoryName(actualFileName);
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(actualFileName);
                string extension = Path.GetExtension(actualFileName);

                StringBuilder fileNameBuilder = new StringBuilder(fileNameWithoutExtension);
                if (!string.IsNullOrEmpty(this.owner.timestampPattern))
                {
                    fileNameBuilder.Append('.');
                    fileNameBuilder.Append(currentDateTime.ToString(this.owner.timestampPattern, CultureInfo.InvariantCulture));
                }

                if (this.owner.rollFileExistsBehavior == RollFileExistsBehavior.Increment)
                {
                    // look for max sequence for date
                    int newSequence = FindMaxSequenceNumber(directory, fileNameBuilder.ToString(), extension) + 1;
                    fileNameBuilder.Append('.');
                    fileNameBuilder.Append(newSequence.ToString(CultureInfo.InvariantCulture));
                }

                fileNameBuilder.Append(extension);

                return Path.Combine(directory, fileNameBuilder.ToString());
            }

            /// <summary>
            /// Finds the max sequence number for a log file.
            /// </summary>
            /// <param name="directoryName">The directory to scan.</param>
            /// <param name="fileName">The file name.</param>
            /// <param name="extension">The extension to use.</param>
            /// <returns>The next sequence number.</returns>
            public static int FindMaxSequenceNumber(string directoryName, string fileName, string extension)
            {
                string[] existingFiles =
                    Directory.GetFiles(directoryName, string.Format(CultureInfo.InvariantCulture, "{0}*{1}", fileName, extension));

                int maxSequence = 0;
                Regex regex = new Regex(string.Format(CultureInfo.InvariantCulture, @"{0}\.(?<sequence>\d+){1}$", fileName, extension));
                for (int i = 0; i < existingFiles.Length; i++)
                {
                    Match sequenceMatch = regex.Match(existingFiles[i]);
                    if (sequenceMatch.Success)
                    {
                        int currentSequence = 0;

                        string sequenceInFile = sequenceMatch.Groups["sequence"].Value;
                        if (!int.TryParse(sequenceInFile, out currentSequence))
                        {
                            continue; // very unlikely
                        }

                        if (currentSequence > maxSequence)
                        {
                            maxSequence = currentSequence;
                        }
                    }
                }

                return maxSequence;
            }

            /// <summary>
            /// Perform the roll for the next date.
            /// </summary>
            /// <param name="rollDateTime">The roll date.</param>
            internal void PerformRoll(DateTime rollDateTime)
            {
                string actualFileName = ((FileStream)((StreamWriter)this.owner.writer).BaseStream).Name;

                if (this.owner.rollFileExistsBehavior == RollFileExistsBehavior.Overwrite
                    && string.IsNullOrEmpty(this.owner.timestampPattern))
                {
                    // no roll will be actually performed: no timestamp pattern is available, and 
                    // the roll behavior is overwrite, so the original file will be truncated
                    this.owner.writer.Close();
                    File.WriteAllText(actualFileName, string.Empty);
                }
                else
                {
                    // calculate archive name
                    string archiveFileName = this.ComputeArchiveFileName(actualFileName, rollDateTime);

                    // close file
                    this.owner.writer.Close();

                    // move file
                    SafeMove(actualFileName, archiveFileName, rollDateTime);

                    // purge if necessary
                    this.PurgeArchivedFiles(actualFileName);
                }

                this.owner.writer.Dispose();
                this.owner.writer = new TallyKeepingFileStreamWriter(this.owner.file.Open(FileMode.Append, FileAccess.Write, FileShare.Read));

                this.managedWriter = null;
                this.nextRollDateTime = null;
                this.UpdateRollingInformationIfNecessary();
            }

            /// <summary>
            /// Rolls the file if necessary.
            /// </summary>
            public void RollIfNecessary()
            {
                if (!this.performsRolling)
                {
                    // avoid further processing if no rolling has been configured.
                    return;
                }

                if (!this.UpdateRollingInformationIfNecessary())
                {
                    // an error was detected while handling roll information - avoid further processing
                    return;
                }

                DateTime? rollDateTime;
                if ((rollDateTime = this.CheckIsRollNecessary()) != null)
                {
                    this.PerformRoll(rollDateTime.Value);
                }
            }

            /// <summary>
            /// Updates book keeping information necessary for rolling, as required by the specified rolling configuration.
            /// </summary>
            /// <returns>true if update was successful, false if an error occurred.</returns>
            public bool UpdateRollingInformationIfNecessary()
            {
                StreamWriter currentWriter = null;

                // replace writer with the tally keeping version if necessary for size rolling
                if (this.owner.rollSizeInBytes > 0 && this.managedWriter == null)
                {
                    currentWriter = this.owner.writer as StreamWriter;

                    if (currentWriter == null)
                    {
                        // TWTL couldn't acquire the writer - abort
                        return false;
                    }

                    var actualFileName = ((FileStream)currentWriter.BaseStream).Name;

                    currentWriter.Close();

                    FileStream fileStream = null;
                    try
                    {
                        fileStream = File.Open(actualFileName, FileMode.Append, FileAccess.Write, FileShare.Read);
                        this.managedWriter = new TallyKeepingFileStreamWriter(fileStream, GetEncodingWithFallback());
                    }
                    catch (IOException)
                    {
                        // there's a slight chance of error here - abort if this occurs and just let TWTL handle it without attempting to roll
                        return false;
                    }

                    this.owner.writer = this.managedWriter;
                }

                // compute the next roll date if necessary
                if (this.owner.rollInterval != RollInterval.None && this.nextRollDateTime == null)
                {
                    try
                    {
                        // casting should be safe at this point - only file stream writers can be the writers for the owner trace listener.
                        // it should also happen rarely
                        this.nextRollDateTime
                            = this.CalculateNextRollDate(File.GetCreationTime(((FileStream)((StreamWriter)this.owner.writer).BaseStream).Name));
                    }
                    catch (IOException)
                    {
                        this.nextRollDateTime = DateTime.MaxValue; // disable rolling if no date could be retrieved.

                        // there's a slight chance of error here - abort if this occurs and just let TWTL handle it without attempting to roll
                        return false;
                    }
                }

                return true;
            }

            /// <summary>
            /// Releases all resources used by the current instance of the <see cref="StreamWriterRollingHelper"/> class.
            /// </summary>
            public void Dispose()
            {
                using (this.managedWriter) { }
            }

            private static Encoding GetEncodingWithFallback()
            {
                Encoding encoding = (Encoding)new UTF8Encoding(false).Clone();
                encoding.EncoderFallback = EncoderFallback.ReplacementFallback;
                encoding.DecoderFallback = DecoderFallback.ReplacementFallback;
                return encoding;
            }

            private static void SafeMove(string actualFileName, string archiveFileName, DateTime currentDateTime)
            {
                try
                {
                    if (File.Exists(archiveFileName))
                    {
                        File.Delete(archiveFileName);
                    }

                    // take care of tunneling issues http://support.microsoft.com/kb/172190
                    File.SetCreationTime(actualFileName, currentDateTime);
                    File.Move(actualFileName, archiveFileName);
                }
                catch (IOException)
                {
                    // catch errors and attempt move to a new file with a GUID
                    archiveFileName = archiveFileName + Guid.NewGuid().ToString();

                    try
                    {
                        File.Move(actualFileName, archiveFileName);
                    }
                    catch (IOException)
                    {
                    }
                }
            }

            private void PurgeArchivedFiles(string actualFileName)
            {
                if (this.owner.maxArchivedFiles > 0)
                {
                    var directoryName = Path.GetDirectoryName(actualFileName);
                    var fileName = Path.GetFileName(actualFileName);

                    new RollingFlatFilePurger(directoryName, fileName, this.owner.maxArchivedFiles).Purge();
                }
            }
        }
    }
}
