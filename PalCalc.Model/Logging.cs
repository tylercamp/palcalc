using Serilog;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Exceptions;
using Serilog.Templates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public static class Logging
    {
        // https://nblumhardt.com/2021/06/customize-serilog-text-output/
        public static readonly ExpressionTemplate MessageFormat = new ExpressionTemplate(
            "[{@t:HH:mm:ss} {@l:u3} {SourceContext}] {@m}\n{@x}"
        );

        public static void InitCommonFull(LogEventLevel consoleLogLevel = LogEventLevel.Information)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.PalCommon(consoleLogLevel)
                .CreateLogger();
        }
    }

    public static class LoggingExtensions
    {
        public static LoggerConfiguration PalCommon(this LoggerSinkConfiguration config, LogEventLevel consoleLogLevel = LogEventLevel.Information) =>
            config
                .Debug(Logging.MessageFormat, LogEventLevel.Verbose)
                .WriteTo.Console(Logging.MessageFormat, consoleLogLevel)
                .MinimumLevel.Verbose()
                .Enrich.WithExceptionDetails();
    }
}
