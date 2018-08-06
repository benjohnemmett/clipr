﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using clipr.Arguments;
using clipr.Triggers;
using clipr.Utils;
using clipr.IOC;

namespace clipr.Core
{
    /// <summary>
    /// Configuration for the parser.
    /// </summary>
    public interface IParserConfig
    {
        /// <summary>
        /// The name of the application as a whole.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Description of the application as a whole.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// The (non-whitespace) character separating a long 
        /// argument name from its value.
        /// </summary>
        char LongOptionSeparator { get; }

        /// <summary>
        /// Configuration options for the parser.
        /// </summary>
        IParserSettings Settings { get; }

        /// <summary>
        /// Localization info describing how to localize the application
        /// info/description and defining the default ResourceType
        /// for arguments in this config.
        /// </summary>
        LocalizationInfo LocalizationInfo { get; }

        /// <summary>
        /// The name of the console application (based on the root config)
        /// </summary>
        string ApplicationName { get; }

        /// <summary>
        /// The list of registered triggers.
        /// </summary>
        IEnumerable<ITerminatingTrigger> Triggers { get; }

        /// <summary>
        /// The list of short name arguments.
        /// </summary>
        Dictionary<char, IShortNameArgument> ShortNameArguments { get; } 

        /// <summary>
        /// Retrieve the short character from an argument or throw an exception.
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        char? GetShortName(IShortNameArgument arg);

        /// <summary>
        /// Retrieve the short character from an argument or throw an exception.
        /// </summary>
        /// <param name="arg"></param>
        /// <param name="errorMessage"></param>
        /// <returns></returns>
        char? GetShortName(IShortNameArgument arg, string errorMessage);

        /// <summary>
        /// The list of long name arguments.
        /// </summary>
        Dictionary<string, ILongNameArgument> LongNameArguments { get; } 

        /// <summary>
        /// Retrieve the long name from an argument or throw an exception.
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        string GetLongName(ILongNameArgument arg);

        /// <summary>
        /// Retrieve the long name from an argument or throw an exception.
        /// </summary>
        /// <param name="arg"></param>
        /// <param name="errorMessage"></param>
        /// <returns></returns>
        string GetLongName(ILongNameArgument arg, string errorMessage);

        /// <summary>
        /// List of all positional arguments.
        /// </summary>
        List<IPositionalArgument> PositionalArguments { get; }

        /// <summary>
        /// List of all verbs in the parser.
        /// </summary>
        Dictionary<string, IVerbParserConfig> Verbs { get; }

        /// <summary>
        /// List of methods to be executed after parsing is finished.
        /// </summary>
        List<MethodInfo> PostParseMethods { get; }

        /// <summary>
        /// List of named arguments that are required.
        /// </summary>
        HashSet<string> RequiredNamedArguments { get; }
    }

    internal abstract class ParserConfig : IParserConfig
    {
        public string Name { get; protected set; }

        public string Description { get; protected set; }

        public char LongOptionSeparator { get { return '='; } }

        public IParserSettings Settings { get; private set; }

        public LocalizationInfo LocalizationInfo { get; protected set; }

        public RootApplicationMetadata RootMetadata { get; protected set; }

        public string ApplicationName
        {
            get { return RootMetadata.ApplicationName; }
        }

        public Dictionary<char, IShortNameArgument> ShortNameArguments { get; protected set; }

        public Dictionary<string, ILongNameArgument> LongNameArguments { get; protected set; }

        public List<IPositionalArgument> PositionalArguments { get; protected set; }

        public Dictionary<string, IVerbParserConfig> Verbs { get; protected set; }

        public List<MethodInfo> PostParseMethods { get; protected set; }

        public HashSet<string> RequiredNamedArguments { get; protected set; }

        private List<ITerminatingTrigger> _triggers = new List<ITerminatingTrigger>();
        public IEnumerable<ITerminatingTrigger> Triggers { get { return _triggers; } }

        protected ParserConfig(
            RootApplicationMetadata metadata,
            IParserSettings settings, 
            IEnumerable<ITerminatingTrigger> triggers)
        {
            RootMetadata = metadata;
            Settings = settings;

            if (settings.CaseInsensitive)
            {
                var comparer = StringComparer.OrdinalIgnoreCase;
                
                ShortNameArguments = new Dictionary<char, IShortNameArgument>(new CaseInsensitiveCharComparer());
                LongNameArguments = new Dictionary<string, ILongNameArgument>(comparer);
                Verbs = new Dictionary<string, IVerbParserConfig>(comparer);
            }
            else
            {
                ShortNameArguments = new Dictionary<char, IShortNameArgument>();
                LongNameArguments = new Dictionary<string, ILongNameArgument>();
                Verbs = new Dictionary<string, IVerbParserConfig>();
            }

            PositionalArguments = new List<IPositionalArgument>();           
            PostParseMethods = new List<MethodInfo>();
            RequiredNamedArguments = new HashSet<string>();

            AppendTriggers(triggers);
        }

        public void AppendTriggers(IEnumerable<ITerminatingTrigger> triggers)
        {
            if (triggers == null)
            {
                return;
            }

            foreach (var trigger in triggers.Where(p => p != null))
            {
                var sn = GetShortName(trigger, String.Format(
                    "Trigger '{0}' argument {1} is not a valid short name. {2}",
                    trigger.PluginName, trigger.ShortName,
                    ArgumentValidation.IsAllowedShortNameExplanation));
                if (sn.HasValue)
                {
                    if (ShortNameArguments.ContainsKey(sn.Value))
                    {
                        throw new DuplicateArgumentException(sn.ToString());
                    }
                    ShortNameArguments.Add(sn.Value, trigger);
                }

                var ln = GetLongName(trigger, String.Format(
                    "Trigger '{0}' argument {1} is not a valid long name. {2}",
                    trigger.PluginName, trigger.LongName,
                    ArgumentValidation.IsAllowedLongNameExplanation));
                if (ln != null)
                {
                    if (LongNameArguments.ContainsKey(ln))
                    {
                        throw new DuplicateArgumentException(ln);
                    }
                    LongNameArguments.Add(ln, trigger);
                }
                _triggers.Add(trigger);
            }
        }

        public char? GetShortName(IShortNameArgument arg)
        {
            return GetShortName(arg, String.Format(
                "Short name {0} is not allowed. {1}",
                arg.ShortName,
                ArgumentValidation.IsAllowedShortNameExplanation));
        }

        public char? GetShortName(IShortNameArgument arg, string errorMessage)
        {
            if (arg.ShortName.HasValue)
            {
                if (!ArgumentValidation.IsAllowedShortName(arg.ShortName.Value))
                {
                    throw new ArgumentIntegrityException(errorMessage);
                }
                return arg.ShortName.Value;
            }
            return null;
        }

        public string GetLongName(ILongNameArgument arg)
        {
            return GetLongName(arg, String.Format(
                "Long name {0} is not allowed. {1}",
                arg.LongName,
                ArgumentValidation.IsAllowedLongNameExplanation));
        }

        public string GetLongName(ILongNameArgument arg, string errorMessage)
        {
            if (arg.LongName != null)
            {
                if (arg.LongName.Length < 2)
                {
                    throw new ArgumentIntegrityException(String.Format(
                        "Long argument {0} must have at least two characters.",
                        arg.LongName));
                }
                if (!ArgumentValidation.IsAllowedLongName(arg.LongName))
                {
                    throw new ArgumentIntegrityException(errorMessage);
                }
                return arg.LongName;
            }

            return null;
        }
    }
}
