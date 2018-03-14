using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace xUtils
{
	public static class EventLogUtils
	{
		public static void LogSystemEvent(string source, string log, string message, EventLogEntryType logType) {
			try {
				if (!EventLog.SourceExists(source))
			        {
			            EventLog.CreateEventSource(source, log);
			        }
				using (EventLog eventLog = new EventLog(log)) 
					{
					    eventLog.Source = source; 
					    eventLog.WriteEntry(message, logType);
					}	
			} catch (Exception ex) {
				if (!EventLog.SourceExists("xUtils"))
			        {
			            EventLog.CreateEventSource("xUtils", "Application");
			        }

				using (EventLog eventLog = new EventLog("Application")) 
					{
					    eventLog.Source = "xUtils"; 
					    eventLog.WriteEntry(ex.ToString(), EventLogEntryType.Error);
					}	
			}
		}
		
	}
}