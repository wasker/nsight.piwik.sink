using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.Encoders;

namespace Nsight.Piwik.Sink.Tests
{
    [TestClass]
    public class PiwikSinkTests
    {
        private Core.Impl.TelemetryProvider telemetryProvider;

        private Mock<Api.IPiwikApi> mockApi;

        private PiwikSinkOptions defaultOptions;

        [TestInitialize]
        public void Initialize()
        {
            telemetryProvider = new Core.Impl.TelemetryProvider();
            mockApi = new Mock<Api.IPiwikApi>(MockBehavior.Strict);

            defaultOptions = new PiwikSinkOptions()
            {
                AppHostName = "apphost",
                Api = mockApi.Object,
                TelemetrySource = telemetryProvider
            };
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CtorThrows_OptionsNotProvided()
        {
            new PiwikSink(null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CtorThrows_InvalidOptions()
        {
            var options = new PiwikSinkOptions()
            {
                AppHostName = "test",
                TelemetrySource = Mock.Of<Core.Impl.ITelemetrySource>()
            };

            new PiwikSink(options);
        }

        [TestMethod]
        public void Ctor_DefaultConfiguration()
        {
            var sink = new PiwikSink(defaultOptions);

            Assert.AreEqual(new Uri("app://apphost/"), sink.BaseAppUrl);
            Assert.IsNull(sink.Session);
            Assert.IsNull(sink.EnvironmentInfo);
            Assert.IsNotNull(sink.Location);
            Assert.IsNull(sink.Location.CurrentUrl);
            Assert.IsNull(sink.Location.ReferrerUrl);
        }

        [TestMethod]
        public async Task BeginSession_SetsProperty()
        {
            var sink = new PiwikSink(defaultOptions);

            var session = new Core.SessionInfo()
            {
                FirstVisit = new DateTimeOffset(2000, 2, 3, 4, 5, 6, TimeSpan.FromHours(7)),
                LastVisit = new DateTimeOffset(2001, 7, 8, 9, 0, 1, TimeSpan.FromHours(2)),
                UniqueVisitorId = "unique ID",
                UserId = "id@user.com",
                VisitsCount = 5
            };

            await telemetryProvider.Activity.BeginSessionAsync(session);

            var visitorIdHash =
                Hex.ToHexString(DigestUtilities.CalculateDigest("MD5", Encoding.UTF8.GetBytes(session.UniqueVisitorId)));

            Assert.IsNotNull(sink.Session);
            Assert.AreEqual(session.FirstVisit.Value, sink.Session.FirstVisit.Value);
            Assert.AreEqual(session.LastVisit.Value, sink.Session.LastVisit.Value);
            Assert.AreEqual(visitorIdHash, sink.Session.UniqueVisitorId);
            Assert.AreEqual(session.UserId, sink.Session.UserId);
            Assert.AreEqual(session.VisitsCount, sink.Session.VisitsCount);
        }

        [TestMethod]
        public async Task BeginSession_AllowsNullValues()
        {
            var sink = new PiwikSink(defaultOptions);

            await telemetryProvider.Activity.BeginSessionAsync(new Core.SessionInfo());

            Assert.IsNotNull(sink.Session);
            Assert.IsFalse(sink.Session.FirstVisit.HasValue);
            Assert.IsFalse(sink.Session.LastVisit.HasValue);
            Assert.IsNull(sink.Session.UniqueVisitorId);
            Assert.IsNull(sink.Session.UserId);
            Assert.AreEqual(0U, sink.Session.VisitsCount);
        }

        [TestMethod]
        public async Task EndSession_ResetsSession()
        {
            var sink = new PiwikSink(defaultOptions);

            var session = new Core.SessionInfo();

            await telemetryProvider.Activity.BeginSessionAsync(session);
            Assert.IsNotNull(sink.Session);

            await telemetryProvider.Activity.EndSessionAsync(session);
            Assert.IsNull(sink.Session);
        }

        [TestMethod]
        public async Task SetEnvironmentInfo_SetsProperty()
        {
            var sink = new PiwikSink(defaultOptions);

            var environment = new Core.EnvironmentInfo()
            {
                DeviceName = "device name",
                DeviceType = "device type",
                OperatingSystem = "operating system",
                DeviceScreen = new Core.ScreenResolution()
                {
                    Width = 123,
                    Height = 456,
                    Dpi = 789
                }
            };

            await telemetryProvider.Activity.SetEnvironmentInfoAsync(environment);

            Assert.IsNotNull(sink.EnvironmentInfo);
            Assert.AreEqual(environment.DeviceName, sink.EnvironmentInfo.DeviceName);
            Assert.AreEqual(environment.DeviceType, sink.EnvironmentInfo.DeviceType);
            Assert.AreEqual(environment.OperatingSystem, sink.EnvironmentInfo.OperatingSystem);
            Assert.IsNotNull(sink.EnvironmentInfo.DeviceScreen);
            Assert.AreEqual(environment.DeviceScreen.Width, sink.EnvironmentInfo.DeviceScreen.Width);
            Assert.AreEqual(environment.DeviceScreen.Height, sink.EnvironmentInfo.DeviceScreen.Height);
            Assert.AreEqual(environment.DeviceScreen.Dpi, sink.EnvironmentInfo.DeviceScreen.Dpi);
        }

        [TestMethod]
        public async Task SetEnvironmentInfo_AllowsNullValues()
        {
            var sink = new PiwikSink(defaultOptions);

            await telemetryProvider.Activity.SetEnvironmentInfoAsync(new Core.EnvironmentInfo());

            Assert.IsNotNull(sink.EnvironmentInfo);
            Assert.IsNull(sink.EnvironmentInfo.DeviceName);
            Assert.IsNull(sink.EnvironmentInfo.DeviceType);
            Assert.IsNull(sink.EnvironmentInfo.OperatingSystem);
            Assert.IsNull(sink.EnvironmentInfo.DeviceScreen);
        }

        [TestMethod]
        public void ReportAction_MinimalInformation()
        {
            var sink = new PiwikSink(defaultOptions);

            Api.PiwikEventInfo calledEventInfo = null;
            mockApi
                .Setup(api => api.ReportEventAsync(It.IsAny<Api.PiwikEventInfo>()))
                .Returns(Task.FromResult(true))
                .Callback<Api.PiwikEventInfo>(ei => calledEventInfo = ei);

            var actionInfo = new Core.ActionInfo()
            {
                Name = "Test"
            };
            telemetryProvider.Activity.ReportActionAsync(actionInfo);

            Assert.IsNotNull(calledEventInfo);
            Assert.AreEqual("Unknown", calledEventInfo.Category);
            Assert.AreEqual("Test", calledEventInfo.Action);
            Assert.AreEqual("Test", calledEventInfo.Name);
            Assert.AreEqual(sink.BaseAppUrl.ToString(), calledEventInfo.Url);
            Assert.IsNull(calledEventInfo.ReferrerUrl);
            Assert.AreSame(sink.Session, calledEventInfo.Session);
            Assert.AreSame(sink.EnvironmentInfo, calledEventInfo.EnvironmentInfo);
        }

        [TestMethod]
        public void ReportAction_AllInformation()
        {
            var sink = new PiwikSink(defaultOptions);

            Api.PiwikEventInfo calledEventInfo = null;
            mockApi
                .Setup(api => api.ReportEventAsync(It.IsAny<Api.PiwikEventInfo>()))
                .Returns(Task.FromResult(true))
                .Callback<Api.PiwikEventInfo>(ei => calledEventInfo = ei);

            sink.Location.CurrentUrl = new Uri(sink.BaseAppUrl, "some/url");
            sink.Location.ReferrerUrl = new Uri(sink.BaseAppUrl, "another/url");

            var actionInfo = new Core.ActionInfo()
            {
                Name = "Test",
                Category = "Some category",
                Verb = "Some verb"
            };
            telemetryProvider.Activity.ReportActionAsync(actionInfo);

            Assert.IsNotNull(calledEventInfo);
            Assert.AreEqual("Some category", calledEventInfo.Category);
            Assert.AreEqual("Some verb", calledEventInfo.Action);
            Assert.AreEqual("Test", calledEventInfo.Name);
            Assert.AreEqual(sink.Location.CurrentUrl.ToString(), calledEventInfo.Url);
            Assert.AreEqual(sink.Location.ReferrerUrl.ToString(), calledEventInfo.ReferrerUrl);
            Assert.AreSame(sink.Session, calledEventInfo.Session);
            Assert.AreSame(sink.EnvironmentInfo, calledEventInfo.EnvironmentInfo);
        }

        [TestMethod]
        public async Task ReportView_MinimalInformation()
        {
            var sink = new PiwikSink(defaultOptions);

            Api.PiwikViewInfo calledViewInfo = null;
            mockApi
                .Setup(api => api.ReportViewAsync(It.IsAny<Api.PiwikViewInfo>()))
                .Returns(Task.FromResult(true))
                .Callback<Api.PiwikViewInfo>(vi => calledViewInfo = vi);

            await telemetryProvider.Activity.ReportViewAsync(new Core.ViewInfo());

            Assert.IsNotNull(calledViewInfo);
            Assert.AreEqual(sink.BaseAppUrl.ToString(), calledViewInfo.Url);
            Assert.IsNull(calledViewInfo.ViewName);
            Assert.IsNull(calledViewInfo.ReferrerUrl);
            Assert.IsFalse(calledViewInfo.ViewTime.HasValue);
            Assert.AreSame(sink.Session, calledViewInfo.Session);
            Assert.AreSame(sink.EnvironmentInfo, calledViewInfo.EnvironmentInfo);
        }

        [TestMethod]
        public async Task ReportView_AllInformation()
        {
            var sink = new PiwikSink(defaultOptions);

            Api.PiwikViewInfo calledViewInfo = null;
            mockApi
                .Setup(api => api.ReportViewAsync(It.IsAny<Api.PiwikViewInfo>()))
                .Returns(Task.FromResult(true))
                .Callback<Api.PiwikViewInfo>(vi => calledViewInfo = vi);

            var viewInfo = new Core.ViewInfo()
            {
                AbsolutePath = "/some/url",
                Time = TimeSpan.FromSeconds(5),
                Title = "view title"
            };
            await telemetryProvider.Activity.ReportViewAsync(viewInfo);

            Assert.IsNotNull(calledViewInfo);
            Assert.AreEqual(new Uri(sink.BaseAppUrl, "some/url").ToString(), calledViewInfo.Url);
            Assert.AreEqual("view title", calledViewInfo.ViewName);
            Assert.IsNull(calledViewInfo.ReferrerUrl);
            Assert.AreEqual(TimeSpan.FromSeconds(5), calledViewInfo.ViewTime);
            Assert.AreSame(sink.Session, calledViewInfo.Session);
            Assert.AreSame(sink.EnvironmentInfo, calledViewInfo.EnvironmentInfo);
        }

        [TestMethod]
        public async Task ReportView_UpdatesLocation()
        {
            var sink = new PiwikSink(defaultOptions);

            mockApi
                .Setup(api => api.ReportViewAsync(It.IsAny<Api.PiwikViewInfo>()))
                .Returns(Task.FromResult(true));

            Assert.IsNull(sink.Location.CurrentUrl);
            Assert.IsNull(sink.Location.ReferrerUrl);

            //  Initial navigation, no referrer.
            await telemetryProvider.Activity.ReportViewAsync(new Core.ViewInfo());
            Assert.IsNotNull(sink.Location.CurrentUrl);
            Assert.AreEqual(sink.BaseAppUrl, sink.Location.CurrentUrl);
            Assert.IsNull(sink.Location.ReferrerUrl);

            //  Navigation after first navigation, referrer must be present.
            var viewInfo = new Core.ViewInfo()
            {
                AbsolutePath = "/some/url"
            };
            await telemetryProvider.Activity.ReportViewAsync(viewInfo);
            Assert.IsNotNull(sink.Location.CurrentUrl);
            Assert.IsNotNull(sink.Location.ReferrerUrl);
            Assert.AreEqual(new Uri(sink.BaseAppUrl, "some/url"), sink.Location.CurrentUrl);
            Assert.AreEqual(sink.BaseAppUrl, sink.Location.ReferrerUrl);
        }

        [TestMethod]
        public void Hex_ConvertsByteBuffer()
        {
            var buffer = new byte[] { 0x12, 0xa, 0xbc, 0xde, 0xf0 };
            Assert.AreEqual("120abcdef0", Hex.ToHexString(buffer));
        }

        [TestMethod]
        public void MD5Digest_CalculatesDigest()
        {
            var buffer = Encoding.ASCII.GetBytes("Hello world!");
            Assert.AreEqual("86fb269d190d2c85f6e0468ceca42a20", Hex.ToHexString(DigestUtilities.CalculateDigest("MD5", buffer)));
        }
    }
}
