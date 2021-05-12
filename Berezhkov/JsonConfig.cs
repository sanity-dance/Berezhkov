using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Berezhkov
{
    public abstract class JsonConfig
    {
        // In child config definitions, RequiredConfigTokens and OptionalConfigTokens are set by passing a list of ConfigTokens to the GetDictionary method.
        protected Dictionary<string, ConfigToken> RequiredConfigTokens { get; set; }
        protected Dictionary<string, ConfigToken> OptionalConfigTokens { get; set; }
        public JObject UserConfig { get; set; }
        public bool ConfigValid { get; set; }
        public class ConfigToken
        {
            public string TokenName { get; set; } // Name of the token; corresponds to the search term in the user's config.
            public string HelpString { get; set; } // The HelpString is printed to console when the user generates an empty config file and when they enter an invalid value of some kind.
            public string DefaultValue { get; set; } // If the DefaultValue is set and the user's config does not contain a value for this token, the UserConfig JObject stored in the JsonConfig parent will be modified to contain the token with the default value set.
            public bool ContainsValidValue { get; set; } // This keeps track of whether or not the user's config contains a valid value for this token.
            protected Func<JToken,string,bool> ValidationFunction { get; set; } // This function will be executed on the value found in the user config for this token, if a value exists.

            public ConfigToken(string inputName, Func<JToken,string,bool> inputValidationFunction, string inputHelpString, string inputDefaultValue=null)
            {
                TokenName = inputName;
                ValidationFunction = inputValidationFunction;
                HelpString = inputHelpString;
                DefaultValue = inputDefaultValue;
            }
            /*
            
            When Validate is called on a config token, it searches the JObject userConfig for a token with its current Name.

            If such a token is found, ValidationFunction() is executed. The value found and the token's name are passed as parameters.

            The function passed to a constructor should ideally be something created by ValidationFactory<T>(), which will enforce type checking on the user input and execute any additional Func<JToken, string, bool> passed to the ValidationFactory function.

            Example below.

            */
            public bool Validate(JObject userConfig, bool required)
            {
                bool ValidToken = true;
                ContainsValidValue = false;
                if(userConfig.ContainsKey(TokenName))
                {
                    ValidToken = ValidationFunction(userConfig[TokenName], TokenName);
                    if(!ValidToken)
                    {
                        Console.WriteLine(HelpString);
                    }
                    else
                    {
                        ContainsValidValue = true;
                    }
                }
                // Note about required vs optional token handling; ValidToken keeps track of whether or not the user's config should still be valid after the Validate function is over. If an optional token is missing, that is not a good reason to mark the user's config invalid.
                else if (required)
                {
                    Console.WriteLine("User config is missing required token " + TokenName);
                    ValidToken = false;
                }
                else if (DefaultValue != null)
                {
                    userConfig[TokenName] = DefaultValue; // THIS MUTATES THE OBJECT PASSED INTO VALIDATE. USE WITH CAUTION.
                    ValidToken = true;
                    ContainsValidValue = true;
                }
                return ValidToken;
            }
            public override string ToString()
            {
                return TokenName + ": " + HelpString;
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
                newConfig[token.Key] = token.Value.HelpString;
            }
            foreach (var token in OptionalConfigTokens)
            {
                newConfig[token.Key] = token.Value.HelpString;
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
                        Console.WriteLine("The value of token " + tokenName + " is empty or null.");
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
                    Console.WriteLine("Token " + tokenName + " with value " + inputToken.ToString() + " is in an invalid format.");
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
                    Console.WriteLine("Input " + inputName + " with value " + inputToken.ToString() + " is not valid. Valid values: " + string.Join(',', acceptableValues)); // Tell the user what's wrong and how to fix it.
                    return false;
                }
                return true;
            }
            return InnerMethod;
        }

    }

    public static class JTokenExtension
    {
        public static bool IsNullOrEmpty(this JToken token)
        {
        return (token == null) ||
               (token.Type == JTokenType.Array && !token.HasValues) ||
               (token.Type == JTokenType.Object && !token.HasValues) ||
               (token.Type == JTokenType.String && token.ToString() == String.Empty) ||
               (token.Type == JTokenType.Null) ||
               (token.Type == JTokenType.Undefined);
        }
    }

    public class ExampleConfig : JsonConfig
    {
        string[] AcceptableFruits = { "Grape", "Orange", "Apple" };
        public ExampleConfig(JObject inputUserConfig)
        {
            UserConfig = inputUserConfig;
            RequiredConfigTokens = GetDictionary(new ConfigToken[]
                {
                new ConfigToken("Fruit",ValidationFactory<string>(ConstrainStringValues(new List<string>(AcceptableFruits))),"String: A helpful message to the user describing what this token is and why it must be one of these fruits and no others."),
                new ConfigToken("NumberConsumed",ValidationFactory<int>(),"Int: A helpful message to the user describing why they must tell your mysterious program how many fruits they have consumed.")
            });

            OptionalConfigTokens = GetDictionary(new ConfigToken[]
            {
                new ConfigToken("NearestMarket",ValidationFactory<string>(),"String: Location of the nearest fruit market.")
            });

            ConfigValid = true;

            foreach(var token in RequiredConfigTokens)
            {
                if(!token.Value.Validate(UserConfig,true))
                {
                    ConfigValid = false;
                }
            }
            foreach(var token in OptionalConfigTokens)
            {
                if(!token.Value.Validate(UserConfig,false))
                {
                    ConfigValid = false;
                }
            }
        }

        /*
        
        This silliness requires some explanation.

        We could have made GenerateEmptyConfig() a static void method in JsonConfig, but the issue is that RequiredConfigTokens and OptionalConfigTokens would need to be static as well, which creates issues with
        other methods inherited from JsonConfig. Issues can also happen when instantiating multiple JsonConfig objects.

        Therefore, we need to instantiate an empty config (which would normally generate a warning, but we suppress it by rerouting console output temporarily) and run GenerateEmptyConfig from there.

        The below method allows a user to execute ExampleConfig.GenerateEmptyFruitConfig("custompath/customfilename.json"), which will generate the following json file:

        {
            "Fruit":"String: A helpful message to the user describing what this token is and why it must be one of these fruits and no others.",
            "NumberConsumed":"Int: A helpful message to the user describing why they must tell your mysterious program how many fruits they have consumed.",
            "NearestMarket","Optional: String: Location of the nearest fruit market."
        }

        */

        public static void GenerateEmptyFruitConfig(string outputPath)
        {

            TextWriter original = Console.Out;
            using(var sw = new StringWriter())
            {
                Console.SetOut(sw);
                ExampleConfig tempConfig = new ExampleConfig(JObject.Parse(""));
                tempConfig.GenerateEmptyConfig(outputPath);
            }

            Console.SetOut(original);
        }
    }

    /*
    
    Below is example console output if the user tried to pass in the config { "Fruit":"Watermelon","NumberConsumed":"bleventeen" }

    Input Fruit with value Watermelon is not valid. Valid values: Grape,Orange,Apple
    String: A helpful message to the user describing what this token is and why it must be one of these fruits and no others.
    Token NumberConsumed with value bleventeen is in an invalid format.
    Int: A helpful message to the user describing why they must tell your mysterious program how many fruits they have consumed.

    */
}
