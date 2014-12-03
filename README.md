#Metrics.NET.SignalFX
## What is the SignalFX Reporter for Metrics.NET

The Metrics.NET library provides a way of instrumenting applications with custom metrics (timers, histograms, counters etc) that can be reported in various ways and can provide insights on what is happening inside a running application.

This assembly provides a mechanism to report the metrics gathered by Metrics.NET to SignalFuse.

##Sending Diemnsions
In order to send diemnsions to SignalFuse with Metrics.NET you use the MetricTags object. MetricTags are currently a list of strings. To send a dimension just add a string that looks like "key=value" to the MetricTags object you use to initialize your metrics. E.g

```csharp
//Setup counters for API usage
public void setupCounters(string env) {
    this,loginAPICount = Metric.Counter("api.use", Unit.Calls, new MetricTags("environment="+env, "api_type=login"));
    this.purchaseAPICount = Metric.Counter("api.use", Unit.Calls, new MetricTags("environment="+env, "api_type=purchase"));
}
```
This will allow you to see all of of your api.use metrics together or split it out by environment or by api_type.

##Configuring the SignalFxReporter
To configure Metrics.Net to report you need to set up two things
 - Your SignalFX API token
 - The default source

###Your SignalFX API Token
Your API SignalFX API token is available if you click on your avatar in the SignalFuse UI.

###Default source name
When reporting to SignalFuse we need to associate the reported metrics to a "source". Some choices are:
 - NetBIOS Name
 - DNS Name
 - FQDN
 - Custom Source

###AWS Integration
If your code will be running on an AWS instance and you have integrated SignalFuse with AWS. You can configure the Metrics.Net.SignalFX reporter to send the instance id as one of the dimensions so that you can use the discovered AWS instance attributes to filter and group metrics.

###Default Dimensions
If there are dimeensions that you wish to send on all the metrics that you report to SignalFuse. You can configure a set of "default dimensions" when you configure the SignalFXReporter

###C# Configuration
####Basic Configuration
```csharp
// Configure with NetBios Name as the default source
 Metric.Config.WithReporting(report => 
      report.WithSignalFx("<your API token>", TimeSpan.FromSeconds(5)).WithNetBiosNameSource());
```
```csharp
// Configure with DNS Name as the default source
Metric.Config.WithReporting(report => 
     report.WithSignalFx("<your API token>", TimeSpan.FromSeconds(5)).WithDNSNameSource());
```
```csharp
// Configure with FQDN as the default source
Metric.Config.WithReporting(report => 
     report.WithSignalFx("<your API token>", TimeSpan.FromSeconds(5)).WithFQDNSource());
```
```csharp
// Configure with custom source name
Metric.Config.WithReporting(report => 
     report.WithSignalFx("<your API token>", TimeSpan.FromSeconds(5)).WithSource("<source name>"));
```

####AWS Integration
```csharp
// Add AWS Integration
Metric.Config.WithReporting(report =>
     report.WithSignalFx("<your API token>", TimeSpan.FromSeconds(10)).WithAWSInstanceIdDimension().WithNetBiosNameSource());
```

####Default Dimensions
```csharp
// Add default Dimensions
IDictionary<string, string> defaultDims = new Dictionary<string, string>();
defaultDims["environment"] = "prod";
defaultDims["serverType"] = "API";
Metric.Config.WithReporting(report =>
     report.WithSignalFx("<your API token>", defaultDims, TimeSpan.FromSeconds(10)).WithAWSInstanceIdDimension().WithNetBiosNameSource());
```

###App.Config Configuration
It is also possible to use App.Config to configure the SignalFxReporter.

To configure via App.Config use the following code to initialize your Metrics:
```csharp
Metric.Config.WithReporting(report => report.WithSignalFxFromAppConfig());
```
####Basic Configuration
You need to set the following keys:
 - Metrics.SignalFx.APIToken - You SignalFuse token
 - Metrics.SignalFx.Interval.Seconds - How often the reporter should report (in seconds)
 - Metrics.SignalFx.Source.Type - How you would like to configure the default source. Your choices are:
  - netbios
  - dns
  - fqdn
  - custom - If you specify this you must also specify the "Metrics.SignalFx.Source.Value" key to specify the custom source.
E.g
```xml
<appSettings>
 <add key="Metrics.SignalFx.APIToken" value="myapitoken"/>
 <add key="Metrics.SignalFx.Interval.Seconds" value="5"/>
 <add key="Metrics.SignalFx.Source.Type" value="netbios"/>
</appSettings>
```
####AWS Integration
If you wish to turn on the AWS Integration add the key 'Metrics.SignalFx.AWSIntegration' with a value of 'true'.

###Default Dimensions
First you need to setup configSections. So that the configuration system understands the XML elements.
```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
 <configSections>
  <sectionGroup name="SignalFx">
   <section name="DefaultDimensions" type="System.Configuration.DictionarySectionHandler"/>
 </sectionGroup>
</configSections>
```

To add default dimensions:
```xml
<SignalFx>
 <DefaultDimensions>
  <add key="environment" value="prod"/>
  <add key="serverType" value="API"/>
 </DefaultDimensions>
</SignalFx>
```
