using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks
{
    /// <summary>
    /// Defines the formatting of the payload data.
    /// </summary>
    public enum PayloadFormatting
    {
        /// <summary>
        /// Json Formatting.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "Needs Review.")]
        Json,

        /// <summary>
        /// Xml Formatting.
        /// </summary>
        Xml
    }
}