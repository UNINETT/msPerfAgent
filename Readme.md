# MS Performance Agent #

# About #

- https://github.com/graphite-project
- http://graphite.readthedocs.io/en/latest/overview.html

Windows Performance Monitor is a common solution to view performance counters on MS Windows platforms (client/server).
Graphite is a excelent tool to view Windows Performance Counters, where you can store metrics for longer time and create nice dashboards.

## Background ##

- https://github.com/MattHodge/Graphite-PowerShell-Functions

Feeding windows performance counter metrics to Graphite Carbon could be done with use of the Graphite-Powershell Module, but this is a quite resource intensive and slow task.
Default in Graphite Carbon is to receive metrics each 10 seconds. Depending on number of performance counters using the Graphite Powershell Module feeding data to Graphite Carbon each 10 seconds will not be possible.

Motivation for this code (MS Performance Agent) is:
- Try to create a faster code to feed metrics to Graphite Carbon
- Since we have used the Graphite Powershell Module. Implement so that Graphite Powershell Configuration is reused.
- To learn C#. This is one of my first C# Visual Studio projects. 

Collectd is used on Linux to send metrics to Graphite Carbon. There is a GitHub repository containing "Windows version" of collectd.
- https://github.com/bloomberg/collectdwin

But this has currently no support for sending metrics to Graphite Carbon.

## Technical Architecture ##

C# Visual Studio 2015 Solution containing two projects (msPerfAgentConsole & msPerfAgentService).
"Backend" is the same.

## Configuration ##

Configuring "MS Performance Agent" depends on two XML Files:
- msPerfAgentConfig.xml
- StatsToGraphiteConfig.xml (Same XML file used to configure the Graphite Powershell Module)

### msPerfAgentConfig.xml ###

Path to msPerfAgentConfig.xml is coded in msPerfAgentXml.cs (msPerfAgentPath)

#### MsPerfAgentMode ####

- selected
Performance counters defined in StatsToGraphiteConfig.xml will be used

- all
All counters on the system will be discovered. Performance categories defined in msPerfAgentConfig.xml below ExcludePerfCategory will be excluded from discovery.

- category
Performance counters that belongs to performance counter categories defined in msPerfAgentConfig.xml below PerfCategory will be discovered and included.


## Known bugs ##

- Help me vote on this: https://connect.microsoft.com/VisualStudio/feedback/details/2723276/performancecountercategory-getinstancenames-do-not-return-all-instances

## Disclaimer ##

This is one of my first C# Visual Studio projects. Expect bugs and "bad coding" :)
Setting MsPerfAgentMode configuration to "all" is very resource intensive. Not recomended on production platforms.