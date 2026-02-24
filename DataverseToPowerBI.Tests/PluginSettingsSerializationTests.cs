using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
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
