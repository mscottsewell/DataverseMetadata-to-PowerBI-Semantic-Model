using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using DataverseToPowerBI.Core.Models;
using DataverseToPowerBI.XrmToolBox;
using Xunit;

namespace DataverseToPowerBI.Tests
{
    public class PluginSettingsSerializationTests
    {
        [Fact]
        public void PluginSettings_RoundTrip_PreservesExplicitNoFilterViewSelection()
        {
            var settings = new PluginSettings
            {
                SelectedViewIds = new Dictionary<string, string>
                {
                    ["account"] = ""
                }
            };

            var roundTripped = RoundTrip(settings);

            Assert.True(roundTripped.SelectedViewIds.ContainsKey("account"));
            Assert.Equal("", roundTripped.SelectedViewIds["account"]);
        }

        [Fact]
        public void PluginSettings_RoundTrip_PreservesSelectedFieldViewIds()
        {
            var settings = new PluginSettings
            {
                SelectedFieldViewIds = new Dictionary<string, string>
                {
                    ["account"] = "{field-view-id}",
                    ["contact"] = "{another-field-view-id}"
                }
            };

            var roundTripped = RoundTrip(settings);

            Assert.Equal(2, roundTripped.SelectedFieldViewIds.Count);
            Assert.Equal("{field-view-id}", roundTripped.SelectedFieldViewIds["account"]);
            Assert.Equal("{another-field-view-id}", roundTripped.SelectedFieldViewIds["contact"]);
        }

        [Fact]
        public void PluginSettings_RoundTrip_PreservesChoiceSubColumnConfigs()
        {
            var settings = new PluginSettings
            {
                ChoiceSubColumnConfigs = new Dictionary<string, List<SerializedChoiceSubColumnConfig>>
                {
                    ["account"] = new List<SerializedChoiceSubColumnConfig>
                    {
                        new SerializedChoiceSubColumnConfig
                        {
                            AttributeLogicalName = "statuscode",
                            IncludeValueField = true,
                            ValueFieldHidden = true,
                            IncludeLabelField = true,
                            LabelFieldHidden = false
                        }
                    }
                }
            };

            var roundTripped = RoundTrip(settings);

            Assert.True(roundTripped.ChoiceSubColumnConfigs.ContainsKey("account"));
            Assert.Single(roundTripped.ChoiceSubColumnConfigs["account"]);
            var cfg = roundTripped.ChoiceSubColumnConfigs["account"][0];
            Assert.Equal("statuscode", cfg.AttributeLogicalName);
            Assert.True(cfg.IncludeValueField);
            Assert.True(cfg.ValueFieldHidden);
            Assert.True(cfg.IncludeLabelField);
            Assert.False(cfg.LabelFieldHidden);
        }

        [Fact]
        public void PluginSettings_RoundTrip_PreservesCollapsedLookupGroups()
        {
            var settings = new PluginSettings
            {
                CollapsedLookupGroups = new List<string>
                {
                    "account.statecode",
                    "contact.statuscode"
                }
            };

            var roundTripped = RoundTrip(settings);

            Assert.Equal(2, roundTripped.CollapsedLookupGroups.Count);
            Assert.Contains("account.statecode", roundTripped.CollapsedLookupGroups);
            Assert.Contains("contact.statuscode", roundTripped.CollapsedLookupGroups);
        }

        [Fact]
        public void PluginSettings_RoundTrip_PreservesExpandedLookupAttributeHiddenFlag()
        {
            var settings = new PluginSettings
            {
                ExpandedLookups = new Dictionary<string, List<SerializedExpandedLookup>>
                {
                    ["account"] = new List<SerializedExpandedLookup>
                    {
                        new SerializedExpandedLookup
                        {
                            LookupAttributeName = "ownerid",
                            TargetTableLogicalName = "systemuser",
                            TargetTablePrimaryKey = "systemuserid",
                            Attributes = new List<SerializedExpandedLookupAttribute>
                            {
                                new SerializedExpandedLookupAttribute
                                {
                                    LogicalName = "fullname",
                                    DisplayName = "Full Name",
                                    IsHidden = true
                                }
                            }
                        }
                    }
                }
            };

            var roundTripped = RoundTrip(settings);

            Assert.True(roundTripped.ExpandedLookups.ContainsKey("account"));
            var attr = roundTripped.ExpandedLookups["account"][0].Attributes[0];
            Assert.Equal("fullname", attr.LogicalName);
            Assert.True(attr.IsHidden);
        }

        [Fact]
        public void GetChoiceValueFieldDisplayName_UsesSchemaName_WhenPresent()
        {
            var attr = new AttributeMetadata
            {
                LogicalName = "statuscode",
                SchemaName = "StatusCode"
            };

            var result = PluginControl.GetChoiceValueFieldDisplayName(attr);

            Assert.Equal("StatusCode", result);
        }

        [Fact]
        public void GetChoiceValueFieldDisplayName_FallsBackToLogicalName_WhenSchemaNameMissing()
        {
            var attr = new AttributeMetadata
            {
                LogicalName = "statecode",
                SchemaName = null
            };

            var result = PluginControl.GetChoiceValueFieldDisplayName(attr);

            Assert.Equal("statecode", result);
        }

        private static PluginSettings RoundTrip(PluginSettings source)
        {
            using (var ms = new MemoryStream())
            {
                var serializer = new DataContractJsonSerializer(typeof(PluginSettings));
                serializer.WriteObject(ms, source);

                var json = Encoding.UTF8.GetString(ms.ToArray());
                using (var readStream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    return (PluginSettings)serializer.ReadObject(readStream);
                }
            }
        }
    }
}
