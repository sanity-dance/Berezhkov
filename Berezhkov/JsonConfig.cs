using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Berezhkov
{
    public abstract class JsonConfig
    {
        protected Dictionary<string, ConfigToken> RequiredConfigTokens { get; set; }
        protected Dictionary<string, ConfigToken> OptionalConfigTokens { get; set; }
        public JObject UserConfig { get; set; }
        public bool ConfigValid { get; set; }
        public class ConfigToken
        {
            public string Name { get; set; }
            public string HelpString { get; set; }
            public string DefaultValue { get; set; }
            public bool ValidToken { get; set; }
            protected Func<JToken,string,bool> ValidationFunction { get; set; }

            public ConfigToken(string inputName, Func<JToken,string,bool> inputValidationFunction, string inputHelpString, string inputDefaultValue=null)
            {
                Name = inputName;
                ValidationFunction = inputValidationFunction;
                HelpString = inputHelpString;
                DefaultValue = inputDefaultValue;
                ValidToken = false;
            }
            public bool Validate(JObject userConfig, bool required)
            {
                if(userConfig.ContainsKey(Name))
                {
                    ValidToken = ValidationFunction(userConfig[Name], Name);
                    if(!ValidToken)
                    {
                        Console.WriteLine(HelpString);
                    }
                }
                else if (required)
                {
                    Console.WriteLine("User config is missing required token " + Name);
                }
                else if (DefaultValue != null)
                {
                    userConfig[Name] = DefaultValue; // THIS MUTATES THE OBJECT PASSED INTO VALIDATE. USE WITH CAUTION.
                    ValidToken = true;
                }
                return ValidToken;
            }
            public override string ToString()
            {
                return Name + ": " + HelpString;
            }
        }

        public override string ToString()
        {
            List<string> configString = new List<string>();
            foreach(var token in RequiredConfigTokens)
            {
                configString.Add(token.Key + ": " + (UserConfig.ContainsKey(token.Key) ? UserConfig[token.Key].ToString() : token.Value.DefaultValue == null ? "" : token.Value.DefaultValue));
            }
            foreach (var token in OptionalConfigTokens)
            {
                configString.Add(token.Key + ": " + (UserConfig.ContainsKey(token.Key) ? UserConfig[token.Key].ToString() : token.Value.DefaultValue == null ? "" : token.Value.DefaultValue));
            }
            return string.Join('\n',configString);
        }

        protected Dictionary<string,ConfigToken> GetDictionary(ConfigToken[] tokenArray)
        {
            Dictionary<string, ConfigToken> newDictionary = new Dictionary<string, ConfigToken>();
            foreach(var token in tokenArray)
            {
                newDictionary.Add(token.Name, token);
            }
            return newDictionary;
        }

        public void GenerateEmptyConfig(string filepath)
        {
            JObject newConfig = new JObject();
            foreach(var token in RequiredConfigTokens)
            {
                newConfig[token.Key] = token.Value.HelpString;
            }
            foreach (var token in OptionalConfigTokens)
            {
                newConfig[token.Key] = token.Value.HelpString;
            }
            File.WriteAllText(filepath, newConfig.ToString());
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
                    T castValue = inputToken.Value<T>();
                    foreach(var constraint in constraints)
                    {
                        if(!constraint(inputToken,tokenName))
                        {
                            validToken = false;
                        }
                    }
                    return validToken;
                }
                catch(FormatException)
                {
                    Console.WriteLine("Token " + tokenName + " with value " + inputToken.ToString() + " is in an invalid format.");
                    return false;
                }
            }
            return ValidationFunction;
        }

        // An example constraint that can be passed into ValidationFactory- it accepts a list of strings and returns a method to check to see if the given
        //      token is in the list of acceptable values.

        protected Func<JToken, string, bool> ConstrainStringValues(List<string> acceptableValues)
        {
            bool InnerMethod(JToken inputToken, string inputName)
            {
                if (!acceptableValues.Contains(inputToken.ToString())) //Returns false if inputString is not in provided list
                {
                    Console.WriteLine("Input " + inputName + " with value " + inputToken.ToString() + " is not valid. Valid values: " + string.Join(',', acceptableValues)); //Tell the user what's wrong and how to fix it.
                    return false;
                }
                return true;
            }
            return InnerMethod;
        }

    }

    public class ExampleConfig : JsonConfig
    {
        string[] AcceptableFruits = { "Grape", "Orange", "Apple" };
        ExampleConfig(JObject inputUserConfig)
        {
            UserConfig = inputUserConfig;
            RequiredConfigTokens = GetDictionary(new ConfigToken[]
                {
                new ConfigToken("Fruit",ValidationFactory<string>(ConstrainStringValues(new List<string>(AcceptableFruits))),"String: A helpful message to the user describing what this token is and why it must be one of these fruits and no others."),
                new ConfigToken("NumberConsumed",ValidationFactory<int>(),"Int: A helpful message to the user describing why they must tell your mysterious program how many fruits they have consumed.")
            });

            ConfigValid = true;

            foreach(var token in RequiredConfigTokens)
            {
                if(!token.Value.Validate(UserConfig,true))
                {
                    ConfigValid = false;
                }
            }
        }
    }
}
