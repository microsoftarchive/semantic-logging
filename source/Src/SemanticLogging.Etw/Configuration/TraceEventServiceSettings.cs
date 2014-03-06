// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration
{
    using System;
    using System.ComponentModel;
    using System.Globalization;

    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Configuration;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Properties;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

    /// <summary>
    /// Configuration settings for an instance of <see cref="TraceEventService"/> class.
    /// </summary>
    public class TraceEventServiceSettings : INotifyPropertyChanged
    {
        private const int MaxSessionNamePrefix = 200;
        private string sessionNamePrefix;

        /// <summary>
        /// Initializes a new instance of the <see cref="TraceEventServiceSettings" /> class.
        /// </summary>
        public TraceEventServiceSettings()
        {
            this.sessionNamePrefix = Constants.DefaultSessionNamePrefix;
        }

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets or sets the name of the trace event session prefix.
        /// </summary>
        /// <value>
        /// The name of the session prefix.
        /// </value>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0", Justification = "Validated with Guard class")]
        public string SessionNamePrefix
        {
            get
            {
                return this.sessionNamePrefix;
            }

            set
            {
                if (this.sessionNamePrefix != value)
                {
                    Guard.ArgumentNotNullOrEmpty(value, "value");
                    if (value.Length > MaxSessionNamePrefix)
                    {
                        throw new ArgumentOutOfRangeException("value", string.Format(CultureInfo.InvariantCulture, Resources.ExceptionSessionPrefixNameTooLong, MaxSessionNamePrefix));
                    }

                    this.sessionNamePrefix = value;
                    this.NotifyPropertyChanged("SessionNamePrefix");
                }
            }
        }

        /// <summary>
        /// Determines whether the specified <see cref="TraceEventServiceSettings" /> is equal to the current <see cref="TraceEventServiceSettings" />.
        /// </summary>
        /// <param name="obj">The instance to compare with the current instance.</param>
        /// <returns>
        /// True if the specified instance is equal to the current instance; otherwise, false.
        /// </returns>
        public override bool Equals(object obj)
        {
            // Check for null values and compare run-time types.
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            TraceEventServiceSettings s = (TraceEventServiceSettings)obj;

            return this.SessionNamePrefix == s.SessionNamePrefix;
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            return this.SessionNamePrefix.GetHashCode();
        }

        private void NotifyPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
