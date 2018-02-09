using log4net;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project2015To2017.Logging
{
    public static class SecurityExtensions
    {
        static readonly log4net.Core.Level authLevel = new log4net.Core.Level(50000, "NOTICE");
        private static readonly Type ThisDeclaringType = typeof(SecurityExtensions);
        public static void Auth(this ILog log, string message)
        {
            log.Logger.Log(ThisDeclaringType, log4net.Core.Level.Notice, message, null);
        }

        public static void AuthFormat(this ILog log, string message, params object[] args)
        {
            string formattedMessage = string.Format(message, args);
            log.Logger.Log(ThisDeclaringType, log4net.Core.Level.Notice, formattedMessage, null);
        }

        public static void LogError(this ILogger logger, string message, Exception ex)
        {
            logger.LogError(message + " Exception :  " + ex.ToString());
        }
    }
}
