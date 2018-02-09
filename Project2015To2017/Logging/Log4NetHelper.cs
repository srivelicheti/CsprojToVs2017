using log4net;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Project2015To2017.Logging
{
    public static class Log4NetHelper
    {
        public static void ConfigureLog4Net(string appRootPath, string configFileRelativePath, string repositoryName)
        {
            GlobalContext.Properties["appRoot"] = appRootPath;
            //XmlConfigurator.Configure()

            log4net.Config.XmlConfigurator.Configure(LogManager.CreateRepository(repositoryName),
                new FileInfo(Path.Combine(appRootPath, configFileRelativePath)));
        }
    }

    public static class Log4NetAspExtensions
    {
        public static ILoggerFactory AddLog4Net(this ILoggerFactory loggerFactory, string repoName)
        {
           loggerFactory.AddProvider(new Log4NetProvider(repoName));
            return loggerFactory;
        }
    }
}
