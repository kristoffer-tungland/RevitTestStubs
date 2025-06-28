using System;
using Autodesk.Revit.DB;
using Xunit;

namespace RevitTestStubs.Tests
{
    public class RevitStubTests
    {
        [Fact]
        public void Element_Returns_Configured_Parameter_With_Correct_GUID()
        {
            var expectedGuid = Guid.NewGuid();

            var parameter = new Parameter(101);
            parameter.Configure.get_Guid = () => expectedGuid;

            var element = new Element(200);
            element.Configure.GetParameter = guid =>
            {
                Assert.Equal(expectedGuid, guid);
                return parameter;
            };

            bool elementDisposed = false;
            bool parameterDisposed = false;

            element.Configure.Dispose = () => elementDisposed = true;
            parameter.Configure.Dispose = () => parameterDisposed = true;

            var retrievedParameter = element.GetParameter(expectedGuid);
            var actualGuid = retrievedParameter.GUID;

            element.Dispose();
            parameter.Dispose();

            Assert.Equal(expectedGuid, actualGuid);
            Assert.Equal(101, retrievedParameter.Id.IntegerValue);
            Assert.Equal(200, element.Id.IntegerValue);
            Assert.True(elementDisposed);
            Assert.True(parameterDisposed);
        }
    }
}
