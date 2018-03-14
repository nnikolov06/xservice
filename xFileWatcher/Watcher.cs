using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json.Linq;
using xUtils;
using Newtonsoft.Json.Schema;

namespace xFileWatcher
{
	[Export(typeof(xService.IServiceExtension))]
	[ExportMetadata("Service","FileWatcher")]
	public class Watcher : xService.IServiceExtension
	{
		private FileSystemWatcher fsWatcher = null;
		
		private const string PropertiesConfigSchema = @"{
		  'type': 'object',
		  'properties': {
		    'buffersize': {
		      'type': 'integer',
		      'required': true,
		    },
		    'collectoutput': {
		      'type': 'string',
		      'required': true,
		    },
		    'watchedfolder': {
		      'type': 'string',
		      'required': true,
		    },
		    'watchedfilter': {
		      'type': 'string',
		      'required': true,
		    },
		    'executable': {
		      'type': 'string',
		      'required': true,
		    },
		    'commandseed': {
		      'type': 'string',
		      'required': true,
		    },
		    'script': {
		      'type': 'string',
		      'required': true,
		    },
		    'magickpath': {
		      'type': 'string',
		      'required': true,
		    },
		    'outputpath': {
		      'type': 'string',
		      'required': true,
		    },
		    'identificationbarcode': {
		      'type': 'string',
		      'required': true,
		    },
		    'heightcoeff': {
		      'type': 'string',
		      'required': true,
		    },
		    'margincoeff': {
		      'type': 'string',
		      'required': true,
		    },
		    'unrecognizedpath': {
		      'type': 'string',
		      'required': true,
		    },
		    'finaldestination': {
		      'type': 'string',
		      'required': true,
		    }
		  }
		}";
		
		private int buffersize = 8192;
		private string collectOutput = string.Empty;
		private string command = string.Empty;
		private string watchedfolder = string.Empty;
		private string watchedfilter = string.Empty;
		private string executable = string.Empty;
		private List<KeyValuePair<string, string>> argumentsList = null;
		
		
		public bool StartServiceExtension(string configJson) {
			argumentsList = new List<KeyValuePair<string, string>>();
			if(configJson!=string.Empty) {
				try {
					JsonSchema schema = JsonSchema.Parse(PropertiesConfigSchema);
					JObject properties = JObject.Parse(configJson);
				if(properties.IsValid(schema)){
					JsonTextReader settingsReader = new JsonTextReader(new StringReader(configJson));
				
					while(settingsReader.Read()) {
						if(settingsReader.TokenType == JsonToken.String) {
							switch (settingsReader.Path) {
								case "buffersize":
									buffersize = (int)settingsReader.Value;
									break;	
								case "collectoutput":
									collectOutput = settingsReader.Value.ToString();
									break;	
								case "commandseed":
									command = settingsReader.Value.ToString();
									break;
								case "watchedfolder":
									watchedfolder = settingsReader.Value.ToString();
									break;
								case "watchedfilter":
									watchedfilter = settingsReader.Value.ToString();
									break;
								case "executable":
									executable = settingsReader.Value.ToString();
									break;
								default:
									KeyValuePair<string, string> newArgument = new KeyValuePair<string, string>(settingsReader.Path, settingsReader.Value.ToString());
									argumentsList.Add(newArgument);
									break;
							}
						}
					} 
					settingsReader.Close();
					fsWatcher = new FileSystemWatcher();
					fsWatcher.Path = watchedfolder;
					fsWatcher.Filter = watchedfilter;
					fsWatcher.NotifyFilter = NotifyFilters.FileName;
					
					fsWatcher.InternalBufferSize = buffersize;
					fsWatcher.EnableRaisingEvents = true;
					fsWatcher.Created += OnCreated;
					return true;
				} else {
					return false;
				}
				} catch (Exception ex){
					EventLogUtils.LogSystemEvent("xService","Application", String.Format("{0}{1}{2}", this.GetServiceName(), Environment.NewLine, ex.ToString()), EventLogEntryType.Error);
					return false;
				}
			} else {
				return false;
			}
		}
		
		public string GetServiceName() {
			return this.GetType().FullName.ToString();
		}
		
		public bool StopServiceExtension() {
			try {
				fsWatcher.EnableRaisingEvents = false;
				fsWatcher.Created -= OnCreated;
				fsWatcher.Dispose();
				return true;
			} catch (Exception ex) {
				EventLogUtils.LogSystemEvent("xService","Application", String.Format("{0}{1}{2}", this.GetServiceName(), Environment.NewLine, ex.ToString()), EventLogEntryType.Error);
				return false;
			}
		}
		
		private void OnCreated(object source, FileSystemEventArgs e) {
			using (Process parseFile = new Process()) {
				List<KeyValuePair<string, string>> temporaryList = new List<KeyValuePair<string, string>>();
				temporaryList = argumentsList.ToList();
				temporaryList.Add(new KeyValuePair<string, string>("PROCESSEDFILE", e.FullPath));
				string parsedCommand = parseCommand(command, temporaryList);
				try {
					FileInfo targetInfo = new FileInfo(e.FullPath);
					WaitForFile(targetInfo);
					parseFile.StartInfo.FileName = executable;
					parseFile.StartInfo.Arguments = parsedCommand;
					using (StreamWriter w = File.AppendText(AppDomain.CurrentDomain.BaseDirectory + "\\log.txt"))
			        {
						w.WriteLine();
						w.WriteLine("Log Entry Watcher: " + String.Format("{0} {1} {2} {3}", DateTime.Now.ToLongTimeString(), DateTime.Now.ToLongDateString(), executable, parsedCommand));
			        }
					parseFile.StartInfo.UseShellExecute = false;
					parseFile.StartInfo.RedirectStandardOutput = true;
					parseFile.Start();
					
					string result = parseFile.StandardOutput.ReadToEnd();
					parseFile.WaitForExit();
					if(collectOutput == "true") {
						using (StreamWriter w = File.AppendText(AppDomain.CurrentDomain.BaseDirectory + "\\log.txt"))
				        {
							w.WriteLine("Log Entry: " + String.Format("{0} {1} {2}",DateTime.Now.ToLongTimeString(), DateTime.Now.ToLongDateString(), result));
				        }					
					}
				} catch(Exception ex) {
					EventLogUtils.LogSystemEvent("xService","Application", String.Format("{0}{1}{2}", GetServiceName(), Environment.NewLine, ex.ToString()), EventLogEntryType.Error);
				} finally {
					if(!parseFile.HasExited)
						parseFile.Kill();
					
					parseFile.Close();
				}
			}
		}
		
		private void WaitForFile(FileInfo file)
		{
		    FileStream stream = null;
		    bool FileReady = false;
		    while(!FileReady)
		    {
		        try
		        {
		            using(stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None)) 
		            { 
		                FileReady = true; 
		            }
		        }
		        catch (IOException)
		        {
		            //Wait until file is available
		        }
		        if(!FileReady) Thread.Sleep(1000);
		    }
		    stream.Close();
		    stream.Dispose();
		}
		
		private string parseCommand(string template, List<KeyValuePair<string, string>> variableData) {
			List<string> lookFor = new List<string>();
			List<string> replaceWith = new List<string>();
			foreach (KeyValuePair<string, string> element in variableData) {
				lookFor.Add("__" + element.Key.ToUpper() + "__");
				replaceWith.Add(element.Value);
			}
			return template.ReplaceAll(lookFor.ToArray(), replaceWith.ToArray());
		}
		
	}
	
	public static class StringExtensions
	{
	    public static string ReplaceAll(this string source, string[] oldValues, string[] newValues)
	    {
	    	if(source != null & oldValues != null & newValues != null) {
	    		string pattern =
	            string.Join("|", oldValues.Select(Regex.Escape).ToArray());
	
	        	return Regex.Replace(source, pattern, m =>
	            {
	                int index = Array.IndexOf(oldValues, m.Value);
	                return newValues[index];
	            });
	    	} else {
	    		return string.Empty;
	    	}    
	    }
	}
}