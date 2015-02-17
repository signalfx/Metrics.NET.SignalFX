﻿
using Metrics.Reports;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Metrics.SignalFx.Helpers;

namespace Metrics.SignalFx
{
    /// <summary>
    /// A Builder used for end-user Extension methods in setting up the SignalFuse reporting mechanisms
    /// </summary>
    public class SignalFxReporterBuilder
    {
        private static readonly string DEFAULT_URI = "https://api.signalfuse.com";
        private static readonly int MAX_DATAPOINTS_PER_MESSAGE = 10000;
        private static readonly string INSTANCE_ID_DIMENSION = "InstanceId";

        private MetricsReports reports;
        private string apiToken;
        private TimeSpan interval;
        private IDictionary<string, string> defaultDimensions = new Dictionary<string, string>();
        private string baseURI = DEFAULT_URI;
        private int maxDatapointsPerMessage = MAX_DATAPOINTS_PER_MESSAGE;
        private string defaultSource;

        /// <summary>
        /// The hidden internal constructor
        /// </summary>
        internal SignalFxReporterBuilder(MetricsReports reports, string apiToken, TimeSpan interval)
        {
            this.reports = reports;
            this.apiToken = apiToken;
            this.interval = interval;
        }

        /// <summary>
        /// Set up the default dimensions that go out with reports coming from reporters that the builder creates
        /// </summary>
        /// <param name="defaultDimensions">The dimensions that should go out with the reports</param>
        /// <returns>this</returns>
        public SignalFxReporterBuilder WithDefaultDimensions(IDictionary<string, string> defaultDimensions)
        {
            this.defaultDimensions = defaultDimensions;
            return this;
        }

        /// <summary>
        /// Set the base URI that the constructed reporter will send to
        /// </summary>
        /// <param name="baseURI">The base URI that the constructed reporter will send to</param>
        /// <returns>this</returns>
        public SignalFxReporterBuilder WithBaseURI(String baseURI)
        {
            this.baseURI = baseURI;
            return this;
        }

        /// <summary>
        /// Set the limit for the number of data points that can be contained in each message being reported
        /// </summary>
        /// <param name="maxDatapointsPerMessage">The maximum number of data points that can be in each message</param>
        /// <returns>this</returns>
        public SignalFxReporterBuilder WithMaxDatapointsPerMessage(int maxDatapointsPerMessage)
        {
            this.maxDatapointsPerMessage = maxDatapointsPerMessage;
            return this;
        }

        /// <summary>
        /// Tell the reporter to use the NetBios name as the source for reported messages
        /// </summary>
        /// <returns>this</returns>
        public SignalFxReporterBuilder WithNetBiosNameSource()
        {
            return WithSource(System.Environment.MachineName);
        }

        /// <summary>
        /// Tell the reporter to use the reverse lookup DNS name as the source for reported messages
        /// </summary>
        /// <remarks>
        /// Note that this requires that the DNS Servers as configured on the system under which the code 
        /// is running have valid PTR records for the system. Otherwise, it will simply fall back to the
        /// NetBIOS name of the system.
        /// </remarks>
        /// <returns>this</returns>
        public SignalFxReporterBuilder WithDNSSource()
        {
            return WithSource(System.Net.Dns.GetHostName());
        }

        /// <summary>
        /// Tell the reporter to use the FQDN per the Windows IP Helper API
        /// </summary>
        /// <returns>this</returns>
        public SignalFxReporterBuilder WithFQDNSource()
        {
            string domainName = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().DomainName;
            string hostName = System.Net.Dns.GetHostName();

            if (!hostName.EndsWith(domainName))  // if hostname does not already include domain name
            {
                hostName += "." + domainName;   // add the domain name part
            }
            return WithSource(hostName);
        }

        /// <summary>
        /// Tell the reporter to use magic AWS REST address to get the AWS instance Id
        /// </summary>
        /// <returns>this</returns>
        public SignalFxReporterBuilder WithAWSInstanceIdDimension(IWebRequestor awsRequestor = null)
        {
            if (awsRequestor == null)
            {
                awsRequestor = new WebRequestor("http://169.254.169.254/latest/meta-data/instance-id")
                    .WithTimeout(1000*60)
                    .WithMethod("GET");
            }

            using (awsRequestor)
            {
                using (var resp = awsRequestor.Send())
                {
                    string source = new StreamReader(resp).ReadToEnd();
                    defaultDimensions[INSTANCE_ID_DIMENSION] = source;
                    return this;
                }
            }
        }

        /// <summary>
        /// Set up the source that goes out with reports coming from reporters that the builder creates
        /// </summary>
        /// <param name="defaultSource">The source to use in reports</param>
        /// <returns>this</returns>
        public SignalFxReporterBuilder WithSource(string defaultSource)
        {
            this.defaultSource = defaultSource;
            return this;
        }

        /// <summary>
        /// Buidl the actual reporter
        /// </summary>
        /// <returns></returns>
        public MetricsReports Build()
        {
            return reports.WithReport(new SignalFxReport(new SignalFxReporter(baseURI, apiToken), defaultSource, defaultDimensions, maxDatapointsPerMessage), interval);
        }
    }
}
