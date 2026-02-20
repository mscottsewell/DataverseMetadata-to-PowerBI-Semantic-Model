// =============================================================================
// FeatureFlags.cs - Experimental Feature Toggles
// =============================================================================
// Purpose: Centralized feature flags for experimental features.
// Toggle these to enable/disable features during development and testing
// before they are released to the public.
// =============================================================================

namespace DataverseToPowerBI.XrmToolBox
{
    /// <summary>
    /// Centralized feature flags for experimental features.
    /// Set to true to enable during development/testing.
    /// </summary>
    internal static class FeatureFlags
    {
        /// <summary>
        /// When true, enables the "Expand Lookup" feature that allows users to
        /// flatten related table attributes into the parent table via LEFT OUTER JOIN.
        /// </summary>
        /// <remarks>
        /// EXPERIMENTAL: This feature adds a column to the attributes list with an
        /// expand button for lookup-type attributes. Selected attributes from the
        /// related table appear as grouped items under the lookup.
        /// </remarks>
        internal static bool EnableExpandLookup { get; set; } = true;
    }
}
