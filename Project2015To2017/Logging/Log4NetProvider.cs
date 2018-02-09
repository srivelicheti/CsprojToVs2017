﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Project2015To2017.Logging
{
    public class Log4NetProvider:ILoggerProvider
    {
        private IDictionary<string, ILogger> _loggers
            = new Dictionary<string, ILogger>();

        private readonly string _loggerRepo;

        public Log4NetProvider(string loggerRepo)
        {
            _loggerRepo = loggerRepo;
        }

        public ILogger CreateLogger(string name)
        {
            if (!_loggers.ContainsKey(name))
            {
                lock (_loggers)
                {
                    // Have to check again since another thread may have gotten the lock first
                    if (!_loggers.ContainsKey(name))
                    {
                        _loggers[name] = new Log4NetAdapter(_loggerRepo, name);
                    }
                }
            }
            return _loggers[name];
        }

        public void Dispose()
        {
            _loggers.Clear();
        }
    }
}
