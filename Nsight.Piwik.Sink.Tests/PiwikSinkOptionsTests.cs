using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Nsight.Piwik.Sink.Tests
{
    [TestClass]
    public class PiwikSinkOptionsTests
    {
        [TestMethod]
        public void ValidationPasses()
        {
            var options = new PiwikSinkOptions()
            {
                AppHostName = "test",
                Api = Mock.Of<Api.IPiwikApi>(),
                TelemetrySource = Mock.Of<Core.Impl.ITelemetrySource>()
            };

            options.Validate();
            Assert.IsTrue(true, "This test should not fail.");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ValidationThrows_AppHostNameNotSet()
        {
            var options = new PiwikSinkOptions()
            {
                Api = Mock.Of<Api.IPiwikApi>(),
                TelemetrySource = Mock.Of<Core.Impl.ITelemetrySource>()
            };

            options.Validate();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ValidationThrows_ApiNotSet()
        {
            var options = new PiwikSinkOptions()
            {
                AppHostName = "test",
                TelemetrySource = Mock.Of<Core.Impl.ITelemetrySource>()
            };

            options.Validate();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ValidationThrows_TelemetrySourceNotSet()
        {
            var options = new PiwikSinkOptions()
            {
                AppHostName = "test",
                Api = Mock.Of<Api.IPiwikApi>()
            };

            options.Validate();
        }
    }
}
