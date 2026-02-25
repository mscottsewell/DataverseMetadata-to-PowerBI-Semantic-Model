// =============================================================================
// FetchXmlToSqlConverterTests.cs - Unit Tests for FetchXML to SQL Conversion
// =============================================================================
// Purpose: Validates the FetchXML to SQL WHERE clause translation logic.
// Ported from DataverseToPowerBI.PPTB/src/__tests__/FetchXmlToSqlConverter.test.ts
// =============================================================================

using System.Linq;
using DataverseToPowerBI.XrmToolBox.Services;
using Xunit;

namespace DataverseToPowerBI.Tests
{
    public class FetchXmlToSqlConverterTests
    {
        #region Basic Comparison Operators

        [Fact]
        public void ConvertToWhereClause_EqOperator_GeneratesEquals()
        {
            var xml = @"<fetch><entity name=""account""><filter><condition attribute=""name"" operator=""eq"" value=""test"" /></filter></entity></fetch>";
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(xml);
            Assert.Contains("[Base].[name] = 'test'", result.SqlWhereClause);
            Assert.True(result.IsFullySupported);
        }

        [Fact]
        public void ConvertToWhereClause_NeOperator_GeneratesNotEquals()
        {
            var xml = @"<fetch><entity name=""account""><filter><condition attribute=""name"" operator=""ne"" value=""test"" /></filter></entity></fetch>";
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(xml);
            Assert.Contains("[Base].[name] <> 'test'", result.SqlWhereClause);
        }

        [Fact]
        public void ConvertToWhereClause_GtOperator_GeneratesGreaterThan()
        {
            var xml = @"<fetch><entity name=""account""><filter><condition attribute=""revenue"" operator=""gt"" value=""1000"" /></filter></entity></fetch>";
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(xml);
            Assert.Contains("[Base].[revenue] > 1000", result.SqlWhereClause);
        }

        [Fact]
        public void ConvertToWhereClause_IntegerValue_FormattedWithoutQuotes()
        {
            var xml = @"<fetch><entity name=""account""><filter><condition attribute=""statecode"" operator=""eq"" value=""0"" /></filter></entity></fetch>";
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(xml);
            Assert.Contains("[Base].[statecode] = 0", result.SqlWhereClause);
        }

        [Fact]
        public void ConvertToWhereClause_GuidValue_FormattedWithQuotes()
        {
            var xml = @"<fetch><entity name=""account""><filter><condition attribute=""accountid"" operator=""eq"" value=""12345678-1234-1234-1234-123456789012"" /></filter></entity></fetch>";
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(xml);
            Assert.Contains("[Base].[accountid] = '12345678-1234-1234-1234-123456789012'", result.SqlWhereClause);
        }

        #endregion

        #region Null Operators

        [Fact]
        public void ConvertToWhereClause_NullOperator_GeneratesIsNull()
        {
            var xml = @"<fetch><entity name=""account""><filter><condition attribute=""name"" operator=""null"" /></filter></entity></fetch>";
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(xml);
            Assert.Contains("[Base].[name] IS NULL", result.SqlWhereClause);
        }

        [Fact]
        public void ConvertToWhereClause_NotNullOperator_GeneratesIsNotNull()
        {
            var xml = @"<fetch><entity name=""account""><filter><condition attribute=""name"" operator=""not-null"" /></filter></entity></fetch>";
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(xml);
            Assert.Contains("[Base].[name] IS NOT NULL", result.SqlWhereClause);
        }

        #endregion

        #region String Operators

        [Fact]
        public void ConvertToWhereClause_BeginsWithOperator_GeneratesLikePrefix()
        {
            var xml = @"<fetch><entity name=""account""><filter><condition attribute=""name"" operator=""begins-with"" value=""Contoso"" /></filter></entity></fetch>";
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(xml);
            Assert.Contains("[Base].[name] LIKE 'Contoso%'", result.SqlWhereClause);
        }

        [Fact]
        public void ConvertToWhereClause_EndsWithOperator_GeneratesLikeSuffix()
        {
            var xml = @"<fetch><entity name=""account""><filter><condition attribute=""name"" operator=""ends-with"" value=""Inc"" /></filter></entity></fetch>";
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(xml);
            Assert.Contains("[Base].[name] LIKE '%Inc'", result.SqlWhereClause);
        }

        [Fact]
        public void ConvertToWhereClause_SingleQuotesInValue_AreEscaped()
        {
            var xml = @"<fetch><entity name=""account""><filter><condition attribute=""name"" operator=""eq"" value=""O'Brien"" /></filter></entity></fetch>";
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(xml);
            Assert.Contains("O''Brien", result.SqlWhereClause);
        }

        #endregion

        #region Date Operators

        [Fact]
        public void ConvertToWhereClause_TodayOperator_IncludesTimezoneAdjustment()
        {
            var result = new FetchXmlToSqlConverter(utcOffsetHours: -6).ConvertToWhereClause(
                @"<fetch><entity name=""account""><filter><condition attribute=""createdon"" operator=""today"" /></filter></entity></fetch>");
            Assert.Contains("DATEADD(hour, -6", result.SqlWhereClause);
            Assert.Contains("GETUTCDATE()", result.SqlWhereClause);
            Assert.True(result.IsFullySupported);
        }

        [Fact]
        public void ConvertToWhereClause_ThisYearOperator_UsesDatePart()
        {
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(
                @"<fetch><entity name=""account""><filter><condition attribute=""createdon"" operator=""this-year"" /></filter></entity></fetch>");
            Assert.Contains("DATEPART(year", result.SqlWhereClause);
        }

        [Fact]
        public void ConvertToWhereClause_LastXDaysOperator_UsesDateDiff()
        {
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(
                @"<fetch><entity name=""account""><filter><condition attribute=""createdon"" operator=""last-x-days"" value=""30"" /></filter></entity></fetch>");
            Assert.Contains("DATEDIFF(day", result.SqlWhereClause);
            Assert.True(result.IsFullySupported);
        }

        [Fact]
        public void ConvertToWhereClause_LastMonthOperator_UsesPreviousMonthRange()
        {
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(
                @"<fetch><entity name=""account""><filter><condition attribute=""createdon"" operator=""last-month"" /></filter></entity></fetch>");
            Assert.Contains("DATEDIFF(month, 0", result.SqlWhereClause);
            Assert.Contains("- 1, 0)", result.SqlWhereClause);
            Assert.Contains("< DATEADD(month, DATEDIFF(month, 0", result.SqlWhereClause);
            Assert.True(result.IsFullySupported);
        }

        [Fact]
        public void ConvertToWhereClause_ThisMonthOperator_UsesCurrentMonthRange()
        {
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(
                @"<fetch><entity name=""account""><filter><condition attribute=""createdon"" operator=""this-month"" /></filter></entity></fetch>");
            Assert.Contains("DATEADD(month, DATEDIFF(month, 0", result.SqlWhereClause);
            Assert.Contains("+ 1, 0)", result.SqlWhereClause);
            Assert.True(result.IsFullySupported);
        }

        [Fact]
        public void ConvertToWhereClause_NextMonthOperator_UsesNextMonthRange()
        {
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(
                @"<fetch><entity name=""account""><filter><condition attribute=""createdon"" operator=""next-month"" /></filter></entity></fetch>");
            Assert.Contains("+ 1, 0)", result.SqlWhereClause);
            Assert.Contains("+ 2, 0)", result.SqlWhereClause);
            Assert.True(result.IsFullySupported);
        }

        [Fact]
        public void ConvertToWhereClause_DateOnlyBehavior_DoesNotApplyTimezoneAdjustmentToColumn()
        {
            var behaviors = new System.Collections.Generic.Dictionary<string, string>
            {
                ["cai_allocationperiod_start"] = "DateOnly"
            };

            var result = new FetchXmlToSqlConverter(utcOffsetHours: -6, dateTimeBehaviors: behaviors).ConvertToWhereClause(
                @"<fetch><entity name=""account""><filter><condition attribute=""cai_allocationperiod_start"" operator=""last-month"" /></filter></entity></fetch>");

            Assert.DoesNotContain("DATEADD(hour, -6, [Base].[cai_allocationperiod_start])", result.SqlWhereClause);
            Assert.Contains("[Base].[cai_allocationperiod_start]", result.SqlWhereClause);
            Assert.True(result.IsFullySupported);
        }

        [Fact]
        public void ConvertToWhereClause_LinkEntityTimeZoneIndependentBehavior_DoesNotApplyTimezoneAdjustmentToColumn()
        {
            var behaviors = new System.Collections.Generic.Dictionary<string, string>
            {
                ["contact.birthdate"] = "TimeZoneIndependent"
            };

            var xml = @"<fetch><entity name=""account"">
                <link-entity name=""contact"" from=""parentcustomerid"" to=""accountid"" alias=""c"">
                    <filter><condition attribute=""birthdate"" operator=""last-month"" /></filter>
                </link-entity>
            </entity></fetch>";

            var result = new FetchXmlToSqlConverter(utcOffsetHours: -6, dateTimeBehaviors: behaviors).ConvertToWhereClause(xml);

            Assert.DoesNotContain("DATEADD(hour, -6, [c].[birthdate])", result.SqlWhereClause);
            Assert.Contains("[c].[birthdate]", result.SqlWhereClause);
            Assert.True(result.IsFullySupported);
        }

        [Fact]
        public void ConvertToWhereClause_BaseEntityQualifiedDateOnlyBehavior_DoesNotApplyTimezoneAdjustmentToColumn()
        {
            var behaviors = new System.Collections.Generic.Dictionary<string, string>
            {
                ["account.createdon"] = "DateOnly"
            };

            var result = new FetchXmlToSqlConverter(utcOffsetHours: -6, dateTimeBehaviors: behaviors).ConvertToWhereClause(
                @"<fetch><entity name=""account""><filter><condition attribute=""createdon"" operator=""today"" /></filter></entity></fetch>");

            Assert.DoesNotContain("DATEADD(hour, -6, [Base].[createdon])", result.SqlWhereClause);
            Assert.Contains("[Base].[createdon]", result.SqlWhereClause);
            Assert.True(result.IsFullySupported);
        }

        [Fact]
        public void ConvertToWhereClause_DateOnlyBehavior_DoesNotApplyTimezoneAdjustmentToNowBoundary()
        {
            var behaviors = new System.Collections.Generic.Dictionary<string, string>
            {
                ["account.createdon"] = "DateOnly"
            };

            var result = new FetchXmlToSqlConverter(utcOffsetHours: -6, dateTimeBehaviors: behaviors).ConvertToWhereClause(
                @"<fetch><entity name=""account""><filter><condition attribute=""createdon"" operator=""last-month"" /></filter></entity></fetch>");

            Assert.DoesNotContain("DATEADD(hour, -6, GETUTCDATE())", result.SqlWhereClause);
            Assert.Contains("GETUTCDATE()", result.SqlWhereClause);
            Assert.True(result.IsFullySupported);
        }

        [Fact]
        public void ConvertToWhereClause_LinkEntityTimeZoneIndependentBehavior_DoesNotApplyTimezoneAdjustmentToNowBoundary()
        {
            var behaviors = new System.Collections.Generic.Dictionary<string, string>
            {
                ["contact.birthdate"] = "TimeZoneIndependent"
            };

            var xml = @"<fetch><entity name=""account"">
                <link-entity name=""contact"" from=""parentcustomerid"" to=""accountid"" alias=""c"">
                    <filter><condition attribute=""birthdate"" operator=""last-month"" /></filter>
                </link-entity>
            </entity></fetch>";

            var result = new FetchXmlToSqlConverter(utcOffsetHours: -6, dateTimeBehaviors: behaviors).ConvertToWhereClause(xml);

            Assert.DoesNotContain("DATEADD(hour, -6, GETUTCDATE())", result.SqlWhereClause);
            Assert.Contains("GETUTCDATE()", result.SqlWhereClause);
            Assert.True(result.IsFullySupported);
        }

        [Fact]
        public void ConvertToWhereClause_OnOperator_UsesDateCast()
        {
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(
                @"<fetch><entity name=""account""><filter><condition attribute=""createdon"" operator=""on"" value=""2024-01-15"" /></filter></entity></fetch>");
            Assert.Contains("CAST(", result.SqlWhereClause);
            Assert.Contains("AS DATE", result.SqlWhereClause);
        }

        #endregion

        #region List Operators

        [Fact]
        public void ConvertToWhereClause_InOperator_GeneratesInClause()
        {
            var xml = @"<fetch><entity name=""account""><filter><condition attribute=""statecode"" operator=""in""><value>0</value><value>1</value></condition></filter></entity></fetch>";
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(xml);
            Assert.Contains("[Base].[statecode] IN (0, 1)", result.SqlWhereClause);
        }

        [Fact]
        public void ConvertToWhereClause_NotInOperator_GeneratesNotInClause()
        {
            var xml = @"<fetch><entity name=""account""><filter><condition attribute=""statecode"" operator=""not-in""><value>2</value><value>3</value></condition></filter></entity></fetch>";
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(xml);
            Assert.Contains("[Base].[statecode] NOT IN (2, 3)", result.SqlWhereClause);
        }

        #endregion

        #region Filter Logic

        [Fact]
        public void ConvertToWhereClause_AndFilter_JoinsWithAnd()
        {
            var xml = @"<fetch><entity name=""account""><filter type=""and""><condition attribute=""name"" operator=""eq"" value=""test"" /><condition attribute=""statecode"" operator=""eq"" value=""0"" /></filter></entity></fetch>";
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(xml);
            Assert.Contains(" AND ", result.SqlWhereClause);
        }

        [Fact]
        public void ConvertToWhereClause_OrFilter_JoinsWithOr()
        {
            var xml = @"<fetch><entity name=""account""><filter type=""or""><condition attribute=""name"" operator=""eq"" value=""A"" /><condition attribute=""name"" operator=""eq"" value=""B"" /></filter></entity></fetch>";
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(xml);
            Assert.Contains(" OR ", result.SqlWhereClause);
        }

        [Fact]
        public void ConvertToWhereClause_NestedFilters_CombinesAndOr()
        {
            var xml = @"<fetch><entity name=""account""><filter type=""and""><condition attribute=""statecode"" operator=""eq"" value=""0"" /><filter type=""or""><condition attribute=""name"" operator=""eq"" value=""A"" /><condition attribute=""name"" operator=""eq"" value=""B"" /></filter></filter></entity></fetch>";
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(xml);
            Assert.Contains(" AND ", result.SqlWhereClause);
            Assert.Contains(" OR ", result.SqlWhereClause);
        }

        #endregion

        #region User Context Operators

        [Fact]
        public void ConvertToWhereClause_EqUserIdInTds_SupportsCurrentUser()
        {
            var result = new FetchXmlToSqlConverter(utcOffsetHours: -6, isFabricLink: false).ConvertToWhereClause(
                @"<fetch><entity name=""account""><filter><condition attribute=""ownerid"" operator=""eq-userid"" /></filter></entity></fetch>");
            Assert.Contains("CURRENT_USER", result.SqlWhereClause);
            Assert.True(result.IsFullySupported);
        }

        [Fact]
        public void ConvertToWhereClause_EqUserIdInFabricLink_RejectsAsUnsupported()
        {
            var result = new FetchXmlToSqlConverter(utcOffsetHours: -6, isFabricLink: true).ConvertToWhereClause(
                @"<fetch><entity name=""account""><filter><condition attribute=""ownerid"" operator=""eq-userid"" /></filter></entity></fetch>");
            Assert.Equal("", result.SqlWhereClause);
            Assert.False(result.IsFullySupported);
            Assert.NotEmpty(result.UnsupportedFeatures);
        }

        [Fact]
        public void ConvertToWhereClause_EqUserIdInImportMode_RejectsAsUnsupported()
        {
            var result = new FetchXmlToSqlConverter(utcOffsetHours: -6, isFabricLink: false, isImportMode: true).ConvertToWhereClause(
                @"<fetch><entity name=""account""><filter><condition attribute=""ownerid"" operator=""eq-userid"" /></filter></entity></fetch>");
            Assert.False(result.IsFullySupported);
        }

        #endregion

        #region Link-Entity Filters

        [Fact]
        public void ConvertToWhereClause_LinkEntityFilter_GeneratesExistsSubquery()
        {
            var xml = @"<fetch><entity name=""account"">
                <link-entity name=""contact"" from=""parentcustomerid"" to=""accountid"" alias=""c"">
                    <filter><condition attribute=""firstname"" operator=""eq"" value=""John"" /></filter>
                </link-entity>
            </entity></fetch>";
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(xml);
            Assert.Contains("EXISTS", result.SqlWhereClause);
            Assert.Contains("SELECT 1 FROM [contact]", result.SqlWhereClause);
            Assert.Contains("[c].[firstname] = 'John'", result.SqlWhereClause);
        }

        #endregion

        #region SQL Injection Prevention

        [Fact]
        public void ConvertToWhereClause_AttributeNameWithCloseBracket_EscapesCorrectly()
        {
            // Attribute name containing ] should be escaped as ]] via EscapeSqlIdentifier
            var xml = @"<fetch><entity name=""account""><filter><condition attribute=""test]col"" operator=""eq"" value=""foo"" /></filter></entity></fetch>";
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(xml);
            // The raw ] should not appear unescaped in a column reference
            Assert.DoesNotContain("[test]col]", result.SqlWhereClause);
            Assert.Contains("[test]]col]", result.SqlWhereClause);
        }

        [Fact]
        public void ConvertToWhereClause_ValueWithSqlInjection_EscapesSingleQuotes()
        {
            // Value attempting SQL injection via single-quote breakout
            var xml = @"<fetch><entity name=""account""><filter><condition attribute=""name"" operator=""eq"" value=""'; DROP TABLE account; --"" /></filter></entity></fetch>";
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(xml);
            // The single quote must be doubled so the injection payload stays inside the string literal
            Assert.Contains("''", result.SqlWhereClause);
            // The value must be fully enclosed in quotes — no unescaped single quote should break out
            Assert.DoesNotContain("= '';", result.SqlWhereClause);
        }

        #endregion

        #region FabricLink Mode

        [Fact]
        public void ConvertToWhereClause_FabricLink_SkipsUserContextOperators()
        {
            var xml = @"<fetch><entity name=""account""><filter><condition attribute=""ownerid"" operator=""eq-userid"" /></filter></entity></fetch>";
            var result = new FetchXmlToSqlConverter(utcOffsetHours: 0, isFabricLink: true).ConvertToWhereClause(xml);
            Assert.False(result.IsFullySupported);
            Assert.Contains(result.UnsupportedFeatures, f => f.Contains("eq-userid"));
        }

        [Fact]
        public void ConvertToWhereClause_FabricLink_BasicFilter_Works()
        {
            var xml = @"<fetch><entity name=""account""><filter><condition attribute=""statecode"" operator=""eq"" value=""0"" /></filter></entity></fetch>";
            var result = new FetchXmlToSqlConverter(utcOffsetHours: 0, isFabricLink: true).ConvertToWhereClause(xml);
            Assert.True(result.IsFullySupported);
            Assert.NotEmpty(result.SqlWhereClause);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void ConvertToWhereClause_EmptyFetchXml_ReturnsEmpty()
        {
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause("");
            Assert.Equal("", result.SqlWhereClause);
            Assert.True(result.IsFullySupported);
        }

        [Fact]
        public void ConvertToWhereClause_MissingEntityElement_ReturnsEmpty()
        {
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause("<fetch></fetch>");
            Assert.Equal("", result.SqlWhereClause);
        }

        [Fact]
        public void ConvertToWhereClause_InvalidXml_ReturnsNotSupported()
        {
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause("not xml");
            Assert.False(result.IsFullySupported);
        }

        [Fact]
        public void ConvertToWhereClause_UnknownOperator_LogsAsUnsupported()
        {
            var xml = @"<fetch><entity name=""account""><filter><condition attribute=""name"" operator=""unknown-op"" value=""x"" /></filter></entity></fetch>";
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(xml);
            Assert.False(result.IsFullySupported);
            Assert.Contains(result.UnsupportedFeatures, f => f.Contains("unknown-op"));
        }

        [Fact]
        public void ConvertToWhereClause_CustomTableAlias_UsesProvidedAlias()
        {
            var xml = @"<fetch><entity name=""account""><filter><condition attribute=""name"" operator=""eq"" value=""test"" /></filter></entity></fetch>";
            var result = new FetchXmlToSqlConverter().ConvertToWhereClause(xml, "T1");
            Assert.Contains("[T1].[name] = 'test'", result.SqlWhereClause);
        }

        #endregion
    }
}
