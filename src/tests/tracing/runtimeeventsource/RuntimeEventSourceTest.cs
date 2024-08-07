// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Threading;
using System.Threading.Tasks;

AppContext.SetSwitch("appContextSwitch", true);
AppDomain.CurrentDomain.SetData("appContextBoolData", true); // Not loggeed, bool key
AppDomain.CurrentDomain.SetData("appContextBoolAsStringData", "true");
AppDomain.CurrentDomain.SetData("appContextStringData", "myString"); // Not logged, string does not parse as bool
AppDomain.CurrentDomain.SetData("appContextSwitch", "false"); // should not override the SetSwitch above

// Create an EventListener.
using (var myListener = new RuntimeEventListener())
{
    if (myListener.Verify())
    {
        Console.WriteLine("Test passed");
        return 100;
    }
    else
    {
        Console.WriteLine($"Test Failed - did not see one or more of the expected runtime counters.");
        Console.WriteLine("Observed events: ");
        foreach (var (k, v) in myListener.ObservedEvents)
        {
            Console.WriteLine("Event: " + k + " " + v);
        }
        return 1;
    }
}

public class RuntimeEventListener : EventListener
{
    internal int observedProcessorCount = -1;
    internal readonly Dictionary<string, bool> ObservedEvents = new Dictionary<string, bool>();

    private static readonly string[] s_expectedEvents = new[] {
        "appContextSwitch",
        "appContextBoolAsStringData",
        "RuntimeHostConfigSwitch", // Set in the project file
    };

    private static readonly string[] s_unexpectedEvents = new[] {
        "appContextBoolData",
        "appContextStringData",
    };

    protected override void OnEventSourceCreated(EventSource source)
    {
        if (source.Name.Equals("System.Runtime"))
        {
            EnableEvents(source, EventLevel.Informational, (EventKeywords)3 /* RuntimeEventSource.Keywords.AppContext | RuntimeEventSource.Keywords.ProcessorCount */);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        // Check AppContext switches
        if (eventData is { EventName: "LogAppContextSwitch",
                           Payload: { Count: 2 } })
        {
            var switchName = (string)eventData.Payload[0];
            ObservedEvents[switchName] = ((int)eventData.Payload[1]) == 1;
            return;
        }
        else if (eventData is { EventName: "ProcessorCount",
                                Payload: { Count: 1 } })
        {
            observedProcessorCount = (int)eventData.Payload[0];
        }
    }

    public bool Verify()
    {
        foreach (var key in s_expectedEvents)
        {
            if (!ObservedEvents[key])
            {
                Console.WriteLine($"Could not find key {key}");
                return false;
            }
            else
            {
                Console.WriteLine($"Saw {key}");
            }
        }

        foreach (var key in s_unexpectedEvents)
        {
            if (ObservedEvents.ContainsKey(key))
            {
                Console.WriteLine($"Should not have seen {key}");
                return false;
            }
        }
        if (observedProcessorCount != Environment.ProcessorCount)
        {
            Console.WriteLine($"Expected {Environment.ProcessorCount}, but got {observedProcessorCount}");
            return false;
        }
        return true;
    }
}
