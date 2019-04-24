﻿using System;
using clipr.Core;
using clipr.Triggers;
using clipr.Usage;
using System.Linq;
using clipr.Validation;

namespace clipr
{
    /// <summary>
    /// Static shortcuts for quickly parsing argument lists.
    /// </summary>
    public static class CliParser
    {

        /// <summary>
        /// <para>
        /// Parse the given argument list and return a new object
        /// containing the converted arguments.
        /// </para>
        /// <para>
        /// If parsing fails, error details are immediately written
        /// to the error console and help information is displayed.
        /// </para>
        /// <para>
        /// WARNING: This method may call <see cref="Environment.Exit"/> on
        /// error.
        /// </para>
        /// </summary>
        /// <param name="args">Argument list to parse.</param>
        /// <returns>
        /// A new object containing values parsed from the argument list.
        /// </returns>
        public static TS StrictParse<TS>(string[] args) where TS : class, new()
        {
            var obj = new TS();
            StrictParse(args, obj);
            return obj;
        }

        /// <summary>
        /// <para>
        /// Parse the given argument list.
        /// </para>
        /// <para>
        /// If parsing fails, error details is immediately written
        /// to the error console and help information is displayed.
        /// </para>
        /// <para>
        /// WARNING: This method may call <see cref="Environment.Exit"/> on
        /// error.
        /// </para>
        /// </summary>
        /// <param name="obj">Parsed arguments are stored here.</param>
        /// <param name="args">Argument list to parse.</param>
        public static void StrictParse<TS>(string[] args, TS obj) where TS : class
        {
            var parser = new CliParser<TS>();
            parser.StrictParse(args, obj);
        }

        /// <summary>
        /// Make an attempt to parse the arguments and return true if
        /// parsing was successful. If parsing completed, the
        /// <paramref name="obj"/> parameter will contain the constructed
        /// and parsed object. If parsing fails, the object will be null.
        /// </summary>
        /// <typeparam name="TS"></typeparam>
        /// <param name="args">Argument list to parse.</param>
        /// <param name="obj">Newly created destination object or null if parsing failed.</param>
        /// <returns>True if parsing succeeded.</returns>
        public static bool TryParse<TS>(string[] args, out TS obj) where TS : class, new()
        {
            obj = null;
            var tmpObj = new TS();
            if (TryParse(args, tmpObj))
            {
                obj = tmpObj;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Make an attempt to parse the arguments and return true if
        /// parsing was successful. If it returns false, the destination
        /// object may be left in an incomplete state.
        /// </summary>
        /// <typeparam name="TS"></typeparam>
        /// <param name="obj">Destination object.</param>
        /// <param name="args">Argument list to parse.</param>
        /// <returns>True if parsing succeeded.</returns>
        public static bool TryParse<TS>(string[] args, TS obj) where TS : class
        {
            var parser = new CliParser<TS>();
            return parser.TryParse(args, obj);
        }

        /// <summary>
        /// Parse the given argument list and return a new object
        /// containing the converted arguments.
        /// </summary>
        /// <param name="args">Argument list to parse.</param>
        /// <returns>
        /// A new object containing values parsed from the argument list.
        /// </returns>
        public static ParseResult<TS> Parse<TS>(string[] args) where TS : class, new()
        {
            var obj = new TS();
            return Parse(args, obj);
        }

        /// <summary>
        /// Parse the given argument list.
        /// </summary>
        /// <param name="args">Argument list to parse.</param>
        /// <param name="obj">Parsed arguments will be store here.</param>
        public static ParseResult<TS> Parse<TS>(string[] args, TS obj) where TS : class
        {
            var parser = new CliParser<TS>();
            return parser.Parse(args, obj);
        }
    }

    /// <summary>
    /// Parses a set of argument strings into an object.
    /// </summary>
    /// <typeparam name="TConf">Object type being deserialized.</typeparam>
    public class CliParser<TConf> where TConf : class
    {
        public IParseValidator<TConf> Validator { get; set; }

        internal const int ErrorExitCode = 2;

        private IParserConfig Config { get; set; }

        private ParserSettings<TConf> Settings { get; set; }

        private bool _suppressAttributeIntegrityCheck = false;

        /// <summary>
        /// Create a new parser with the default usage generator.
        /// </summary>
        public CliParser()
            : this(ParserSettings<TConf>.Default)
        {
        }

        public CliParser(AutomaticHelpGenerator<TConf> help)
            : this(new ParserSettings<TConf> { HelpGenerator = help })
        {
        }

        /// <summary>
        /// Create a new parser with a set of options and the
        /// default usage generator.
        /// </summary>
        /// <param name="settings">Extra options for the parser.</param>
        public CliParser(ParserSettings<TConf> settings)
        {
            Settings = settings;
            Validator = AttributeValidator<TConf>.Default;
        }

        /// <summary>
        /// Generates a new parser from the fluent builder.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="options"></param>
        internal CliParser(ParserConfig config, ParserSettings<TConf> options, IParseValidator<TConf> validator)
        {
            Config = config;
            Settings = options;
            Validator = validator;
        }

        /// <summary>
        /// Suppress the default behavior of validating attribute integrity.
        /// This will speed up start time if you know the Options class is built
        /// correctly, but if a mistake has been made, you may receive an unhelpful
        /// error when parsing. 
        /// </summary>
        public void SuppressAttributeIntegrityCheck()
        {
            _suppressAttributeIntegrityCheck = true;
        }

        /// <summary>
        /// Checks the configuration type TConf for any attribute issues.
        /// </summary>
        /// <returns></returns>
        public Exception[] PerformAttributeIntegrityCheck()
        {
            var checker = new IntegrityChecker();
            return checker.EnsureAttributeIntegrity<TConf>(Settings)
                .Concat(checker.EnsureVerbIntegrity<TConf>(Settings))
                .Concat(checker.EnsureTriggerIntegrity(Settings.HelpGenerator, Settings.VersionGenerator, Settings.CustomTriggers))
                .ToArray();
        }

        /// <summary>
        /// Checks the configuration type TConf for any attribute issues.
        /// </summary>
        /// <returns></returns>
        public void PerformAttributeIntegrityCheckOrThrow()
        {
            var errs = PerformAttributeIntegrityCheck();
            if (errs.Any())
            {
                throw new AggregateException(errs);
            }
        }

        public IParserConfig BuildConfig()
        {
            if (Config != null)
            {
                return Config;
            }
            return Config = new AttributeParserConfig<TConf>(
                null /* This is the root */,
                Settings,
                Settings.CustomTriggers
                    .Concat(new ITerminatingTrigger[] { Settings.HelpGenerator, Settings.VersionGenerator }));
        }
        

        /// <summary>
        /// <para>
        /// Parses the given argument list.
        /// </para>
        /// <para>
        /// If parsing fails, error details will immediately be written
        /// to the error console and help information will be displayed.
        /// </para>
        /// <para>
        /// WARNING: This method may call <see cref="Environment.Exit"/> on
        /// error.
        /// </para>
        /// </summary>
        /// <param name="args">Argument list to parse.</param>
        /// <param name="obj">Object to fill with parsed data.</param>
        /// <returns>The object parsed from the argument list.</returns>
        public ParseResult<TConf> StrictParse(string[] args, TConf obj)
        {
            var result = Parse(args, obj);
            return result.Handle(
                val => result,
                trig => result,
                errs =>
                {
                    var tw = Settings.OutputWriter;
                    if (tw != null)
                    {
                        if (Settings.HelpGenerator != null)
                        {
                            tw.WriteLine(Settings.HelpGenerator.GetUsage(Config));
                        }
                        foreach (var err in errs)
                        {
                            tw.WriteLine(err);
                        }
                    }
                    Environment.Exit(ErrorExitCode);
                    throw new InvalidOperationException();
                });
        }

        /// <summary>
        /// Make an attempt to parse the arguments and return true if
        /// parsing was successful. If it returns false, the destination
        /// object may be left in an incomplete state.
        /// </summary>
        /// <param name="args">Argument list to parse.</param>
        /// <param name="obj">Object to fill with parsed data.</param>
        /// <returns>True if parsing succeeded.</returns>
        public bool TryParse(string[] args, TConf obj)
        {
            // TODO should triggers be handled automatically in this case?
            return Parse(args, obj).Handle(
                val => true,
                trig => true,
                errs => false);
        }

        /// <summary>
        /// Parse the given argument list.
        /// </summary>
        /// <exception cref="ParseException">
        /// An error happened while parsing.
        /// </exception>
        /// <param name="args">Argument list to parse.</param>
        /// <param name="obj">Object to fill with parsed data.</param>
        public ParseResult<TConf> Parse(string[] args, TConf obj)
        {
            if (!_suppressAttributeIntegrityCheck)
            {
                var result = PerformAttributeIntegrityCheck();
                if (result.Any())
                {
                    return new ParseResult<TConf>(result);
                }
            }
            var conf = BuildConfig();
            return new ParsingContext<TConf>(obj, conf, Validator).Parse(args);
        }

        /// <summary>
        /// Display the complete usage and help information from the assigned
        /// HelpGenerator.
        /// </summary>
        public void DisplayHelp()
        {
            if (Settings.HelpGenerator != null)
            {
                if (Settings.OutputWriter != null)
                {
                    Settings.OutputWriter.WriteLine(
                        Settings.HelpGenerator.GetHelp(BuildConfig()));
                }
            }
        }
    }
}