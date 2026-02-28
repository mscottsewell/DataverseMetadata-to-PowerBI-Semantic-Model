using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using DataverseToPowerBI.XrmToolBox;
using Xunit;

namespace DataverseToPowerBI.Tests
{
    public class SemanticModelConfigSerializationTests
    {
        [Fact]
        public void SemanticModelConfig_Deserialization_MissingAliasProperty_DefaultsToTrue()
        {
            var json =
                "{" +
                "\"Name\":\"LegacyModel\"," +
                "\"DataverseUrl\":\"https://org.crm.dynamics.com\"," +
                "\"WorkingFolder\":\"C:\\\\Temp\\\\Model\"," +
                "\"TemplatePath\":\"C:\\\\Temp\\\\Template\"" +
                "}";

            var serializer = new DataContractJsonSerializer(typeof(SemanticModelConfig));
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var model = (SemanticModelConfig)serializer.ReadObject(ms)!;

            Assert.True(model.UseDisplayNameRenamesInPowerQuery);
        }

        [Fact]
        public void SemanticModelConfig_Deserialization_ExplicitFalse_IsPreserved()
        {
            var json =
                "{" +
                "\"Name\":\"CustomModel\"," +
                "\"DataverseUrl\":\"https://org.crm.dynamics.com\"," +
                "\"WorkingFolder\":\"C:\\\\Temp\\\\Model\"," +
                "\"TemplatePath\":\"C:\\\\Temp\\\\Template\"," +
                "\"UseDisplayNameAliasesInSql\":false" +
                "}";

            var serializer = new DataContractJsonSerializer(typeof(SemanticModelConfig));
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var model = (SemanticModelConfig)serializer.ReadObject(ms)!;

            Assert.False(model.UseDisplayNameRenamesInPowerQuery);
        }
    }
}

