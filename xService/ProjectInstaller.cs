using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace CsharpFolderWatcher
{
    [RunInstaller(true)]
    public class ProjectInstaller : Installer
    {
	    private ServiceProcessInstaller serviceProcessInstaller;
	    private ServiceInstaller serviceInstaller;
	  
	    public ProjectInstaller()
	    {
	        serviceProcessInstaller = new ServiceProcessInstaller();
	        serviceInstaller = new ServiceInstaller();
	        serviceProcessInstaller.Account = ServiceAccount.LocalService;
	        serviceInstaller.ServiceName = xService.xService.MyServiceName;
	        serviceInstaller.Description = xService.xService.MyServiceDescription;
	        this.Installers.AddRange(new Installer[] { serviceProcessInstaller, serviceInstaller });
	    }
    }
}