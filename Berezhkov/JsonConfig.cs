using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Berezhkov
{
    public abstract class JsonConfig
    {
        public Dictionary<string, ConfigToken> RequiredConfigTokens { get; set; }
        public Dictionary<string, ConfigToken> OptionalConfigTokens { get; set; }
        public class ConfigToken
        {
            string Name { get; set; }
            string HelpString { get; set; }
            string DefaultValue { get; set; }
            bool ValidToken { get; set; }
            Func<JToken,string,bool> ValidationFunction { get; set; }

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
                return ValidToken;
            }
        }

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
    }
}
