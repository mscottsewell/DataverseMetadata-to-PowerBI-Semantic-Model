// ===================================================================================
// SemanticModelDataModels.cs - XrmToolBox-Specific Data Models
// ===================================================================================
//
// PURPOSE:
// Defines data models specific to the XrmToolBox plugin that are not shared with
// the Core library. These models support star-schema relationship configuration
// and serialization for settings persistence.
//
// SHARED MODELS:
// DateTableConfig and DateTimeFieldConfig are imported from Core.Models to avoid
// duplication and type conflicts. All other table, attribute, and solution models
// come from the Core.Models namespace.
//
// MODELS DEFINED HERE:
//
// ExportRelationship:
//   Represents a many-to-one relationship between tables in the star schema.
//   - SourceTable: The "many" side (fact or dimension with lookup)
//   - SourceAttribute: The lookup field name
//   - TargetTable: The "one" side (dimension table)
//   - IsActive: Whether this is the active relationship (one per target)
//   - IsSnowflake: True for dimension-to-parent-dimension relationships
//   - AssumeReferentialIntegrity: True if lookup is required (performance hint)
//
// SERIALIZATION:
// All models use DataContract/DataMember attributes for JSON serialization
// via DataContractJsonSerializer, which is compatible with .NET Framework 4.6.2.
//
// ===================================================================================

using System;
using System.Collections.Generic;

namespace DataverseToPowerBI.XrmToolBox.Models
{
    // NOTE: DateTableConfig and DateTimeFieldConfig are imported from Core.Models
    // to avoid duplication and type conflicts

    [System.Runtime.Serialization.DataContract]
    public class ExportRelationship
    {
        [System.Runtime.Serialization.DataMember]
        public string SourceTable { get; set; } = "";       // Fact or Dimension table (Many side)
        [System.Runtime.Serialization.DataMember]
        public string SourceAttribute { get; set; } = "";   // Lookup attribute
        [System.Runtime.Serialization.DataMember]
        public string TargetTable { get; set; } = "";       // Dimension table (One side)
        [System.Runtime.Serialization.DataMember]
        public string DisplayName { get; set; } = "";
        [System.Runtime.Serialization.DataMember]
        public bool IsActive { get; set; } = true;
        [System.Runtime.Serialization.DataMember]
        public bool IsSnowflake { get; set; } = false;      // True if Dimension->ParentDimension
        [System.Runtime.Serialization.DataMember]
        public bool AssumeReferentialIntegrity { get; set; } = false;  // True if lookup field is required
    }
}
