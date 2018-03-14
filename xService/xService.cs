using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using xUtils;
using Newtonsoft.Json.Schema;

namespace xService
{
	
	public interface IServiceExtension {
		bool StartServiceExtension(string configPath);
		bool StopServiceExtension();
		string GetServiceName();
	}
	
	public class xService : ServiceBase
	{
		public const string MyServiceName = "xService";
		public const string MyServiceDescription = "Global service wrapper for handling xServiceExtensions";
		
		private const string RootConfigSchema = @"{
		  'type': 'object',
		  'properties': {
		    'extensions': {
		      'type': 'array',
		      'required': true,
		      'items': {
		        'type': 'object'
		      }
		    }
		  }
		}";
		
		private const string ExtensionConfigSchema = @"{
		  'type': 'object',
		  'properties': {
		    'extension': {
		      'type': 'string',
		      'required': true,
		    },
		    'instancename': {
		      'type': 'string',
		      'required': true,
		    },
		    'properties': {
		      'type': 'object',
		      'required': true,
		    }
		  }
		}";
		
		private static string ExtensionAssemblyPath = string.Empty;
		private CompositionContainer _container;
		  
		private struct Extension
		{
			
			[Import(typeof(IServiceExtension))]
			public IServiceExtension serviceExtension;
			public string extensionProperties;
			
			public Extension(string _extensionProperties, IServiceExtension _serviceExtension) {
				extensionProperties = _extensionProperties;
				serviceExtension = _serviceExtension;
			}
		}
        private List<Extension> loadedExtensions;
        
		public xService()
		{
			InitializeComponent();
		}
		
		private void InitializeComponent()
		{
			this.ServiceName = MyServiceName;
		}
		
		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose(bool disposing)
		{
			// TODO: Add cleanup code here (if required)
			base.Dispose(disposing);
		}
		
		/// <summary>
		/// Start this service.
		/// </summary>
		protected override void OnStart(string[] args)
		{
			AggregateCatalog catalog = new AggregateCatalog();
			loadedExtensions = new List<Extension>();
			try
            {
				JsonSchema settingsSchema = JsonSchema.Parse(RootConfigSchema);
				JObject settings = JObject.Parse(new StreamReader(AppDomain.CurrentDomain.BaseDirectory + "\\config.json").ReadToEnd());
				
				if(settings.IsValid(settingsSchema)){
			
					IList<JToken> serviceSettings = settings["extensions"].Children().ToList();
					
					foreach(JToken extensionSettings in serviceSettings) {
						
						JsonSchema serviceSchema = JsonSchema.Parse(ExtensionConfigSchema);
						if(extensionSettings.IsValid(serviceSchema)) {
					
							ExtensionAssemblyPath = extensionSettings["extension"].ToString();
							
							Assembly externalAssembly = Assembly.LoadFrom(AppDomain.CurrentDomain.BaseDirectory + "\\Extensions\\" + ExtensionAssemblyPath);
						
							if(ExtensionAssemblyPath != string.Empty & externalAssembly != null) {
				            	catalog.Catalogs.Add(new AssemblyCatalog(typeof(Program).Assembly));
				            	catalog.Catalogs.Add(new AssemblyCatalog(Assembly.LoadFrom(AppDomain.CurrentDomain.BaseDirectory + "\\Extensions\\" + ExtensionAssemblyPath)));
				            	IServiceExtension extension = externalAssembly.CreateInstance(extensionSettings["instancename"].ToString()) as IServiceExtension;
				            	loadedExtensions.Add(new Extension(extensionSettings["properties"].ToString(), extension));
							} else {
								this.Stop();
							}
						} else {
							EventLogUtils.LogSystemEvent("xService","Application", "Invalid extension configuration.", EventLogEntryType.Error);
						}
					}
					
					if(loadedExtensions.Count>0) {
						_container = new CompositionContainer(catalog);
		                this._container.ComposeParts(this);
		                foreach (Extension loadedServiceExtension in loadedExtensions) {
		                	loadedServiceExtension.serviceExtension.StartServiceExtension(loadedServiceExtension.extensionProperties);
		                	EventLogUtils.LogSystemEvent("xService","Application", String.Format("Loaded: {0}", loadedServiceExtension.serviceExtension.GetServiceName()), EventLogEntryType.Information);
		                }
					} else {
						EventLogUtils.LogSystemEvent("xService","Application", "No extensions to load.", EventLogEntryType.Error);
					}
				
				} else {
					EventLogUtils.LogSystemEvent("xService","Application", "Invalid configuration file.", EventLogEntryType.Error);
				}				
            } catch (Exception ex) {
				EventLogUtils.LogSystemEvent("xService","Application", ex.ToString(), EventLogEntryType.Error);
			}
		}
		
		/// <summary>
		/// Stop this service.
		/// </summary>
		protected override void OnStop()
		{
			if(loadedExtensions.Count>0) {
				foreach (Extension loadedServiceExtension in loadedExtensions) {
					loadedServiceExtension.serviceExtension.StopServiceExtension();
				}
				loadedExtensions.Clear();
			}
			loadedExtensions = null;
		}
	}
}
