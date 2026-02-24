using System;

namespace DataverseToPowerBI.Core.Models
{
    /// <summary>
    /// Provides naming helpers for choice-related subfields.
    /// </summary>
    public static class ChoiceFieldNaming
    {
        /// <summary>
        /// Returns the display name for the choice "value" subfield.
        /// Uses schema name when available, otherwise logical name.
        /// </summary>
        public static string GetValueDisplayName(AttributeMetadata attribute)
        {
            if (attribute == null) throw new ArgumentNullException(nameof(attribute));
            return attribute.SchemaName ?? attribute.LogicalName;
        }
    }
}
