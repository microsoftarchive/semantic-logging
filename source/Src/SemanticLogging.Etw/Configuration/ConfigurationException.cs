// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Etw.Configuration
{
    /// <summary>
    /// Represents one or more errors that occur during loading the <see cref="TraceEventService"/> configuration file.
    /// </summary>
    [Serializable]
    public class ConfigurationException : Exception, ISerializable
    {
        private ReadOnlyCollection<Exception> innerExceptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationException" /> class with a default message.
        /// </summary>
        public ConfigurationException()
            : this(Properties.Resources.TraceEventServiceConfigurationExceptionDefaultMessage)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationException" /> class.
        /// </summary>
        /// <param name="message">The message.</param>
        public ConfigurationException(string message)
            : this(message, new Exception[0])
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationException" /> class.
        /// </summary>
        /// <param name="innerExceptions">The inner exceptions.</param>
        public ConfigurationException(params Exception[] innerExceptions)
            : this(Properties.Resources.TraceEventServiceConfigurationExceptionDefaultMessage, innerExceptions)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationException" /> class 
        /// with references to the inner exceptions that are the cause of this exception.
        /// </summary>
        /// <param name="innerExceptions">The inner exceptions that are the cause of this exception.</param>
        public ConfigurationException(IEnumerable<Exception> innerExceptions)
            : this(Properties.Resources.TraceEventServiceConfigurationExceptionDefaultMessage, innerExceptions)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationException" /> class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        public ConfigurationException(string message, Exception innerException)
            : this(message, new Exception[] { innerException ?? new Exception(message) })
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationException" /> class 
        /// with a message descriptions and the references to the inner exceptions that are the cause of this exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerExceptions">The references to the inner exceptions that are the cause of this exception.</param>
        public ConfigurationException(string message, IEnumerable<Exception> innerExceptions)
            : base(message, ExtractInnerException(innerExceptions))
        {
            var exceptions = new List<Exception>(innerExceptions);
            exceptions.ForEach(e => Guard.ArgumentNotNull(e, "innerException"));

            this.innerExceptions = new ReadOnlyCollection<Exception>(exceptions);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationException" /> class with serialized data.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo"/> info.</param>
        /// <param name="context">The <see cref="StreamingContext"/>.</param>
        /// <exception cref="System.Runtime.Serialization.SerializationException">Deserialization failure.</exception>
        [SecurityCritical]
        protected ConfigurationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            Guard.ArgumentNotNull(info, "info");

            Exception[] list = info.GetValue("InnerExceptions", typeof(Exception[])) as Exception[];

            if (list == null)
            {
                throw new SerializationException("InnerExceptions");
            }

            this.innerExceptions = new ReadOnlyCollection<Exception>(list);
        }

        /// <summary>
        /// Gets a read-only collection of the Exception instances that caused the current configuration exception.
        /// </summary>
        /// <value>
        /// The inner exceptions.
        /// </value>
        public ReadOnlyCollection<Exception> InnerExceptions
        {
            get { return this.innerExceptions; }
        }

        /// <summary>
        /// Gets or sets the configuration file that generated this exception.
        /// </summary>
        /// <value>
        /// The configuration file.
        /// </value>
        public string ConfigurationFile { get; set; }

        /// <summary>
        /// When overridden in a derived class, sets the <see cref="T:System.Runtime.Serialization.SerializationInfo" /> with information about the exception.
        /// </summary>
        /// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo" /> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The <see cref="T:System.Runtime.Serialization.StreamingContext" /> that contains contextual information about the source or destination.</param>
        /// <PermissionSet>
        /// <IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Read="*AllFiles*" PathDiscovery="*AllFiles*" />
        /// <IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Flags="SerializationFormatter" />
        /// </PermissionSet>
        [SecurityCritical]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            Guard.ArgumentNotNull(info, "info");

            base.GetObjectData(info, context);
            Exception[] array = new Exception[this.innerExceptions.Count];
            this.innerExceptions.CopyTo(array, 0);

            info.AddValue("InnerExceptions", array, typeof(Exception[]));
        }

        /// <summary>
        /// Creates and returns a string representation of the current <see cref="ConfigurationException" />. (Overrides <see cref="Exception.ToString()" />.).
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(this.Message);

            if (!string.IsNullOrEmpty(this.ConfigurationFile))
            {
                sb.AppendFormat(Properties.Resources.TraceEventServiceConfigurationFileFormat, this.ConfigurationFile);
                sb.AppendLine();
            }

            for (int i = 0; i < this.innerExceptions.Count; i++)
            {
                ToStringDeep(this.innerExceptions[i], sb);
            }

            return sb.ToString();
        }

        private static void ToStringDeep(Exception exception, StringBuilder sb)
        {
            var ae = exception as AggregateException;
            if (ae != null)
            {
                ae.Flatten().Handle(e => TraverseChilds(e, sb));
                return;
            }

            TraverseChilds(exception, sb);
        }

        private static bool TraverseChilds(Exception exception, StringBuilder sb)
        {
            for (Exception e = exception; e != null; e = e.InnerException)
            {
                sb.AppendLine(ExtendedMessage(e));
            }

            return true;
        }

        private static string ExtendedMessage(Exception e)
        {
            XmlSchemaValidationException xsdValidation = e as XmlSchemaValidationException;

            if (xsdValidation != null)
            {
                return string.Format(CultureInfo.CurrentCulture,
                                     Properties.Resources.XmlSchemaValidationExceptionFormat,
                                     RemoveNamespace(xsdValidation.Message),
                                     Environment.NewLine,
                                     xsdValidation.LineNumber,
                                     xsdValidation.LinePosition);
            }

            return e.Message;
        }

        private static Exception ExtractInnerException(IEnumerable<Exception> innerExceptions)
        {
            Guard.ArgumentNotNull(innerExceptions, "innerExceptions");

            return innerExceptions.FirstOrDefault();
        }

        private static string RemoveNamespace(string message)
        {
            return message.Replace(string.Format(CultureInfo.CurrentCulture, Properties.Resources.RemoveNamespaceFromErrrMessage, Constants.Namespace), string.Empty);
        }
    }
}
