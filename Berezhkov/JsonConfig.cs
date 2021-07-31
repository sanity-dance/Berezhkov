using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Berezhkov
{
    public abstract class JsonConfig
    {
        // In child config definitions, RequiredConfigTokens and OptionalConfigTokens are set by passing a list of ConfigTokens to the GetDictionary method.
        private readonly HashSet<ConfigToken> _RequiredConfigTokens = new HashSet<ConfigToken>();
        protected HashSet<ConfigToken> RequiredConfigTokens { get { return _RequiredConfigTokens; } set { _RequiredConfigTokens.UnionWith(value); } }
        private readonly HashSet<ConfigToken> _OptionalConfigTokens = new HashSet<ConfigToken>();
        protected HashSet<ConfigToken> OptionalConfigTokens { get { return _OptionalConfigTokens; } set { _OptionalConfigTokens.UnionWith(value); } }
        public List<string> ErrorList { get; set; } = new List<string>();
        public JObject UserConfig { get; set; }
        public bool Valid { get; set; } = true;
        
        /// <summary>
        /// Checks UserConfig against RequiredConfigTokens and OptionalConfigTokens. If name and type are provided, the message "Validation for [type] [name] failed." will be added to ErrorList on validation failure.
        /// </summary>
        protected virtual void Initialize(string name = null, string type = null)
        {
            Initialize(RequiredConfigTokens, OptionalConfigTokens, UserConfig, name, type);
        }

        /// <summary>
        /// Checks UserConfig against the ConfigToken HashSets required and optional. If name and type are provided, the message "Validation for [type] [name] failed." will be added to ErrorList on validation failure.
        /// </summary>
        /// <param name="required">Collection of ConfigToken objects that must be included in UserConfig.</param>
        /// <param name="optional">Collection of ConfigToken objects that can be included in UserConfig.</param>
        protected virtual void Initialize(HashSet<ConfigToken> required, HashSet<ConfigToken> optional, string name = null, string type = null)
        {
            Initialize(required, optional, UserConfig, name, type);
        }

        /// <summary>
        /// Checks config against the ConfigToken HashSets required and optional. If name and type are provided, the message "Validation for [type] [name] failed." will be added to ErrorList on validation failure.
        /// </summary>
        /// <param name="required">Collection of ConfigToken objects that must be included in UserConfig.</param>
        /// <param name="optional">Collection of ConfigToken objects that can be included in UserConfig.</param>
        /// <param name="config">Config object to check against the ConfigToken sets.</param>
        protected virtual void Initialize(HashSet<ConfigToken> required, HashSet<ConfigToken> optional, JObject config, string name = null, string type = null)
        {
            string message = " ";
            if(!(string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(type)))
            {
                message = "Validation for " + type + " " + name + " failed.\n";
            }
            void Invalidate()
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    ErrorList.Add(message);
                }
                Valid = false;
            }
            foreach (var token in required)
            {
                if(!config.ContainsKey(token.TokenName))
                {
                    ErrorList.Add("User config is missing required token " + token.TokenName + "\n" + token.HelpString);
                    Invalidate();
                }
                else if(config[token.TokenName].IsNullOrEmpty())
                {
                    ErrorList.Add("Value of token " + token.TokenName + " is null or empty.");
                    Invalidate();
                }
                else if (!token.Validate(config[token.TokenName]))
                {
                    ErrorList.Add(token.HelpString);
                    Invalidate();
                }
            }
            foreach (var token in optional)
            {
                if(!config.ContainsKey(token.TokenName) && token.DefaultValue != null)
                {
                    config[token.TokenName] = token.DefaultValue; // THIS MUTATES THE USER CONFIG. PASSED INTO VALIDATE. USE WITH CAUTION.
                }
                else if (!token.Validate(config[token.TokenName]))
                {
                    ErrorList.Add(token.HelpString);
                    Invalidate();
                }
            }
            foreach (var property in config)
            {
                if (!required.Select(x => x.TokenName).Contains(property.Key) && !optional.Select(x => x.TokenName).Contains(property.Key))
                {
                    ErrorList.Add("Unrecognized token in input config file: " + property.Key);
                    Invalidate();
                }
            }
        }

        public override string ToString()
        {
            List<string> configString = new List<string>();
            foreach(var token in RequiredConfigTokens)
            {
                configString.Add(token.TokenName + ": " + (UserConfig.ContainsKey(token.TokenName) ? UserConfig[token.TokenName].ToString() : token.DefaultValue == null ? "" : token.DefaultValue));
            }
            foreach (var token in OptionalConfigTokens)
            {
                configString.Add(token.TokenName + ": " + (UserConfig.ContainsKey(token.TokenName) ? UserConfig[token.TokenName].ToString() : token.DefaultValue == null ? "" : token.DefaultValue));
            }
            return string.Join('\n',configString);
        }

        protected Dictionary<string,ConfigToken> GetDictionary(ConfigToken[] tokenArray)
        {
            Dictionary<string, ConfigToken> newDictionary = new Dictionary<string, ConfigToken>();
            foreach(var token in tokenArray)
            {
                newDictionary.Add(token.TokenName, token);
            }
            return newDictionary;
        }

        // The following method can be called from child config classes as part of a static void method that instantiates an empty example of the current config file in order to generate an empty config file. Example below.

        protected void GenerateEmptyConfig(string filepath)
        {
            JObject newConfig = new JObject();
            foreach(var token in RequiredConfigTokens)
            {
                newConfig[token.TokenName] = token.HelpString;
            }
            foreach (var token in OptionalConfigTokens)
            {
                newConfig[token.TokenName] = token.HelpString;
            }
            FileInfo file = new FileInfo(filepath);
            file.Directory.Create();
            File.WriteAllText(file.FullName, newConfig.ToString());
        }

        /*
        
        The following method will be used to produce ValidationFunction members for ConfigTokens. It serves two purposes: First, to enforce type checking by ensuring
        that the given JToken can be parsed as type T, and second, to apply any additional constraints the developer requires.

        */
        public Func<JToken,string,bool> ValidationFactory<T>(params Func<JToken,string,bool>[] constraints)
        {
            bool ValidationFunction(JToken inputToken, string tokenName)
            {
                bool validToken = true;
                try
                {
                    if(inputToken.IsNullOrEmpty())
                    {
                        ErrorList.Add("The value of token " + tokenName + " is empty or null.");
                        validToken = false;
                    }
                    else
                    {
                        T castValue = inputToken.Value<T>();
                        foreach(var constraint in constraints)
                        {
                            if(!constraint(inputToken,tokenName))
                            {
                                validToken = false;
                            }
                        }
                    }
                    return validToken;
                }
                catch(FormatException)
                {
                    ErrorList.Add("Token " + tokenName + " with value " + inputToken.ToString() + " is in an invalid format.");
                    return false;
                }
            }
            return ValidationFunction;
        }

        // An example constraint that can be passed into ValidationFactory- it accepts a list of strings and returns a method to check to see if the given token is in the list of acceptable values.

        protected Func<JToken, string, bool> ConstrainStringValues(List<string> acceptableValues)
        {
            bool InnerMethod(JToken inputToken, string inputName)
            {
                if (!acceptableValues.Contains(inputToken.ToString())) //Returns false if inputString is not in provided list
                {
                    ErrorList.Add("Input " + inputName + " with value " + inputToken.ToString() + " is not valid. Valid values: " + string.Join(',', acceptableValues)); // Tell the user what's wrong and how to fix it.
                    return false;
                }
                return true;
            }
            return InnerMethod;
        }

        // This constraint method allows nesting of Json objects inside one another without resorting to defining additional config types.

        protected Func<JToken, string, bool> ConstrainJsonTokens(params ConfigToken[] requiredTokens)
        {
            bool InnerMethod(JToken inputToken, string inputName)
            {
                JObject inputJson = (JObject)inputToken;
                Initialize(requiredTokens.ToHashSet(), new HashSet<ConfigToken>(), inputJson, inputName, "value of token");
                return Valid;
            }
            return InnerMethod;
        }

        protected Func<JToken, string, bool> ConstrainJsonTokens(ConfigToken[] requiredTokens, ConfigToken[] optionalTokens)
        {
            bool InnerMethod(JToken inputToken, string inputName)
            {
                JObject inputJson = (JObject)inputToken;
                Initialize(requiredTokens.ToHashSet(), optionalTokens.ToHashSet(), inputJson, inputName, "value of token");
                return Valid;
            }
            return InnerMethod;
        }
    }
}
