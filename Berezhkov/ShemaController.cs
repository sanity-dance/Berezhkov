using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Berezhkov
{
    public abstract class ShemaController
    {
        // In child config definitions, RequiredConfigTokens and OptionalConfigTokens are set by merging a new HashSet of config tokens with the parent.
        /// <summary>
        /// Set of tokens that must exist in the JObject set as UserConfig. Object must be added to; it cannot be replaced.
        /// </summary>
        protected HashSet<ConfigToken> RequiredConfigTokens { get; } = new HashSet<ConfigToken>();
        /// <summary>
        /// Set of tokens that optionally exist in the JObject set as UserConfig.
        /// </summary>
        protected HashSet<ConfigToken> OptionalConfigTokens { get; } = new HashSet<ConfigToken>();
        /// <summary>
        /// Contains all errors generated during validation and the associated HelpStrings of each token that was marked invalid. Should be printed to console or returned as part of an HTTP 400 response.
        /// </summary>
        public List<string> ErrorList { get; set; } = new List<string>();
        /// <summary>
        /// Should be populated with the JObject that is being checked with RequiredConfigTokens and OptionalConfigTokens.
        /// </summary>
        public JObject UserConfig { get; set; }
        /// <summary>
        /// Indicates if any step of validation failed.
        /// </summary>
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
            foreach (var token in required)
            {
                if(!config.ContainsKey(token.TokenName))
                {
                    if(message.IsNullOrEmpty())
                    {
                        ErrorList.Add("User config is missing required token " + token.TokenName + "\n" + token.HelpString);
                    }
                    else
                    {
                        ErrorList.Add(type + " " + name + " is missing required token " + token.TokenName + "\n" + token.HelpString);
                    }
                    Valid = false;
                }
                else if(config[token.TokenName].IsNullOrEmpty())
                {
                    ErrorList.Add("Value of token " + token.TokenName + " is null or empty.");
                    Valid = false;
                }
                else if (!token.Validate(config[token.TokenName]))
                {
                    ErrorList.Add(token.HelpString);
                    Valid = false;
                }
            }
            foreach (var token in optional)
            {
                if(!config.ContainsKey(token.TokenName) && token.DefaultValue != null)
                {
                    config[token.TokenName] = token.DefaultValue; // THIS MUTATES THE USER CONFIG. USE WITH CAUTION.
                }
                else if (config.ContainsKey(token.TokenName) && !token.Validate(config[token.TokenName]))
                {
                    ErrorList.Add(token.HelpString);
                }
            }
            foreach (var property in config)
            {
                if (!required.Select(x => x.TokenName).Contains(property.Key) && !optional.Select(x => x.TokenName).Contains(property.Key))
                {
                    if(message.IsNullOrEmpty())
                    {
                        ErrorList.Add("User config file contains unrecognized token: " + property.Key);
                    }
                    else
                    {
                        ErrorList.Add(type + " " + name + " contains unrecognized token: " + property.Key);
                    }
                    Valid = false;
                }
            }
            if (!Valid && !string.IsNullOrWhiteSpace(message))
            {
                ErrorList.Add(message);
            }
        }

        public override string ToString()
        {
            List<string> configString = new List<string>();
            foreach(var token in RequiredConfigTokens)
            {
                configString.Add(token.TokenName + ": " + (UserConfig.ContainsKey(token.TokenName) ? UserConfig[token.TokenName].ToString() : ""));
            }
            foreach (var token in OptionalConfigTokens)
            {
                configString.Add(token.TokenName + ": " + (UserConfig.ContainsKey(token.TokenName) ? UserConfig[token.TokenName].ToString() : token.DefaultValue ?? ""));
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

        public JObject GenerateEmptyConfig()
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
            return newConfig;
        }

        /*
        
        The following method will be used to produce ValidationFunction members for ConfigTokens. It serves two purposes: First, to enforce type checking by ensuring
        that the given JToken can be parsed as type T, and second, to apply any additional constraints the developer requires.

        */
        /// <summary>
        /// Produces ValidationFunction members for ConfigTokens, which are executed on the corresponding values found in the UserConfig property. It first checks the type of the value with T, then executes all passed constraints on the value.
        /// </summary>
        /// <typeparam name="T">Type that the token value will be cast to.</typeparam>
        /// <param name="constraints">Functions to execute on the token value after cast is successful.</param>
        /// <returns>Composite function of the type cast and all passed constraints. Can be used in the constructor of a ConfigToken.</returns>
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
                    ErrorList.Add("Token " + tokenName + " with value " + inputToken.ToString() + " is an incorrect type. Expected value type: " + typeof(T).ToString());
                    return false;
                }
            }
            return ValidationFunction;
        }

        // An example constraint that can be passed into ValidationFactory- it accepts a list of strings and returns a method to check to see if the given token is in the list of acceptable values.

        #region String Constraints

        /// <param name="acceptableValues">List of values used to build the returned function.</param>
        /// <returns>Function checking to ensure that the value of the passed JToken is one of acceptableValues.</returns>
        protected Func<JToken, string, bool> ConstrainStringValues(params string[] acceptableValues)
        {
            bool InnerMethod(JToken inputToken, string inputName)
            {
                if (!acceptableValues.Contains(inputToken.ToString())) //Returns false if inputString is not in provided list
                {
                    ErrorList.Add("Input " + inputName + " with value " + inputToken.ToString() + " is not valid. Valid values: " + string.Join(", ", acceptableValues)); // Tell the user what's wrong and how to fix it.
                    return false;
                }
                return true;
            }
            return InnerMethod;
        }

        /// <summary>
        /// Creates a function that ensures the full JToken matches the provided pattern string(s).
        /// </summary>
        /// <param name="pattern">Valid Regex pattern(s) used in the returned function.</param>
        /// <returns>Function checking to ensure that the whole JToken matches the provided pattern strings.</returns>
        protected Func<JToken, string, bool> ConstrainStringWithRegexExact(params string[] patterns)
        {
            List<Regex> regexPatterns = new List<Regex>();
            foreach (var pattern in patterns)
            {
                try
                {
                    regexPatterns.Add(new Regex(pattern));
                }
                catch (Exception ex)
                {
                    throw new ArgumentException("Pattern " + pattern + " is not a valid Regex pattern.\n" + ex);
                }
            }
            return ConstrainStringWithRegexExact(regexPatterns.ToArray());
        }

        /// <param name="pattern">Valid Regex pattern(s) used in the returned function.</param>
        /// <returns>Function checking to ensure that the whole JToken matches at least one of the provided pattern strings.</returns>
        protected Func<JToken, string, bool> ConstrainStringWithRegexExact(params Regex[] patterns)
        {
            bool InnerMethod(JToken inputToken, string inputName)
            {
                string inputString = inputToken.ToString();
                bool matchesAtLeastOne = false;
                foreach (var pattern in patterns)
                {
                    if (pattern.IsMatch(inputString))
                    {
                        if (Regex.Replace(inputString, pattern.ToString(), "").Length == 0)
                        {
                            matchesAtLeastOne = true;
                        }
                    }
                }
                if (!matchesAtLeastOne)
                {
                    if (patterns.Length == 1)
                    {
                        ErrorList.Add("Token " + inputName + " with value " + inputToken + " is not an exact match to pattern " + patterns[1]);
                    }
                    else
                    {
                        ErrorList.Add("Token " + inputName + " with value " + inputToken + " is not an exact match to any pattern: " + string.Join<Regex>(" ", patterns));
                    }
                    return false;
                }
                return true;
            }
            return InnerMethod;
        }

        #endregion String Constraints

        #region Numeric Constraints

        /// <summary>
        /// Constrains numeric values with only a lower bound.
        /// </summary>
        /// <param name="lowerBound">Double used as the lower bound in the returned function, inclusive.</param>
        /// <returns>Function checking to ensure that the value of the passed JToken is greater than the provided lower bound.</returns>
        protected Func<JToken, string, bool> ConstrainNumericValue(double lowerBound)
        {
            bool InnerMethod(JToken inputToken, string inputName)
            {
                if((double)inputToken < lowerBound)
                {
                    ErrorList.Add("Token " + inputName + " with value " + inputToken.ToString() + " is less than enforced lower bound " + lowerBound);
                    return false;
                }
                return true;
            }
            return InnerMethod;
        }

        /// <summary>
        /// Constrains numeric values with a lower bound and an upper bound.
        /// </summary>
        /// <param name="lowerBound">Double used as the lower bound in the returned function, inclusive.</param>
        /// <param name="upperBound">Double used as the upper bound in the returned function, inclusive.</param>
        /// <returns>Function checking to ensure that the value of the passed JToken is greater than the provided lower bound.</returns>
        protected Func<JToken, string, bool> ConstrainNumericValue(double lowerBound, double upperBound)
        {
            bool InnerMethod(JToken inputToken, string inputName)
            {
                if ((double)inputToken < lowerBound || (double)inputToken > upperBound)
                {
                    ErrorList.Add("Token " + inputName + " with value " + inputToken.ToString() + " is invalid. Value must be greater than or equal to " + lowerBound + " and less than or equal to " + upperBound);
                    return false;
                }
                return true;
            }
            return InnerMethod;
        }

        /// <summary>
        /// Constrains numeric values using any number of provided domains as tuples in format (lowerBound, upperBound)
        /// </summary>
        /// <param name="domains">(double, double) tuples in format (lowerBound, upperBound) used as possible domains in the returned function, inclusive.</param>
        /// <returns>Function checking to ensure that the value of the passed JToken is within at least one of the provided domains.</returns>
        protected Func<JToken, string, bool> ConstrainNumericValue(params (double, double)[] domains)
        {
            bool InnerMethod(JToken inputToken, string inputName)
            {
                double inputValue = (double)inputToken;
                bool matchesAtLeastOne = false;
                foreach(var domain in domains)
                {
                    if(inputValue >= domain.Item1 && inputValue <= domain.Item2)
                    {
                        matchesAtLeastOne = true;
                    }
                }
                if(!matchesAtLeastOne)
                {
                    ErrorList.Add("Token " + inputName + " with value " + inputValue.ToString() + " is invalid. Value must fall within one of the following domains, inclusive: " + string.Join(" ", domains.Select(x => x.ToString())));
                }
                return matchesAtLeastOne;
            }
            return InnerMethod;
        }

        #endregion Numeric Constraints

        #region JObject Constraints

        // This constraint method allows nesting of Json objects inside one another without resorting to defining additional config types.

        protected Func<JToken, string, bool> ConstrainJsonTokens(params ConfigToken[] requiredTokens)
        {
            bool InnerMethod(JToken inputToken, string inputName)
            {
                JObject inputJson = (JObject)inputToken;
                Initialize(requiredTokens.ToHashSet(), new HashSet<ConfigToken>(), inputJson, inputName, "Value of token");
                return Valid;
            }
            return InnerMethod;
        }

        protected Func<JToken, string, bool> ConstrainJsonTokens(ConfigToken[] requiredTokens, ConfigToken[] optionalTokens)
        {
            bool InnerMethod(JToken inputToken, string inputName)
            {
                JObject inputJson = (JObject)inputToken;
                Initialize(requiredTokens.ToHashSet(), optionalTokens.ToHashSet(), inputJson, inputName, "Value of token");
                return Valid;
            }
            return InnerMethod;
        }

        protected Func<JToken, string, bool> ConstrainPropertyCount(int lowerBound)
        {
            bool InnerMethod(JToken inputToken, string inputName)
            {
                JObject inputJson = (JObject)inputToken;
                if(inputJson.Count < lowerBound)
                {
                    ErrorList.Add("Value of token " + inputName + " is invalid. Value has " + inputJson.Count + " properties, but must have at least " + lowerBound + " properties.");
                    return false;
                }
                return true;
            }
            return InnerMethod;
        }

        protected Func<JToken, string, bool> ConstrainPropertyCount(int lowerBound, int upperBound)
        {
            bool InnerMethod(JToken inputToken, string inputName)
            {
                JObject inputJson = (JObject)inputToken;
                if (inputJson.Count < lowerBound || inputJson.Count > upperBound)
                {
                    ErrorList.Add("Value of token " + inputName + " is invalid. Value has " + inputJson.Count + " properties, but must have at least " + lowerBound + " properties and at most " + upperBound + " properties.");
                    return false;
                }
                return true;
            }
            return InnerMethod;
        }

        #endregion JObject Constraints

        #region JArray Constraints

        protected Func<JToken, string, bool> ConstrainArrayCount(int lowerBound)
        {
            bool InnerMethod(JToken inputToken, string inputName)
            {
                JArray inputArray = (JArray)inputToken;
                if(inputArray.Count < lowerBound)
                {
                    ErrorList.Add("Value of token " + inputName + " contains " + inputArray.Count + " values, but must contain at least " + lowerBound + " values.");
                    return false;
                }
                return true;
            }
            return InnerMethod;
        }

        protected Func<JToken, string, bool> ConstrainArrayCount(int lowerBound, int upperBound)
        {
            bool InnerMethod(JToken inputToken, string inputName)
            {
                JArray inputArray = (JArray)inputToken;
                if (inputArray.Count < lowerBound || inputArray.Count > upperBound)
                {
                    ErrorList.Add("Value of token " + inputName + " contains " + inputArray.Count + " values, but must contain between " + lowerBound + " and " + upperBound + " values.");
                    return false;
                }
                return true;
            }
            return InnerMethod;
        }

        protected Func<JToken, string, bool> ApplyConstraintsToAllArrayValues<T>(params Func<JToken, string, bool>[] constraints)
        {
            bool InnerMethod(JToken inputToken, string inputName)
            {
                JArray inputArray = (JArray)inputToken;
                bool allPassed = true;
                foreach(var value in inputArray)
                {
                    try
                    {
                        T castValue = value.Value<T>();
                        foreach (var constraint in constraints)
                        {
                            if (!constraint(value, "in array " + inputName))
                            {
                                 allPassed = false;
                            }
                        }
                    }
                    catch (FormatException)
                    {
                        ErrorList.Add("Value " + value + " in array " + inputName + " is an incorrect type. Expected value type: " + typeof(T).ToString());
                        allPassed = false;
                    }
                }
                return allPassed;
            }
            return InnerMethod;
        }

        #endregion JArray Constraints
    }
}
