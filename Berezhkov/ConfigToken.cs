using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Berezhkov
{
    public class ConfigToken
    {
        public string TokenName { get; set; } // Name of the token; corresponds to the search term in the user's config.
        public string HelpString { get; set; } // The HelpString is printed to console when the user generates an empty config file and when they enter an invalid value of some kind.
        public string DefaultValue { get; set; } // If the DefaultValue is set and the user's config does not contain a value for this token, the UserConfig JObject stored in the JsonConfig parent will be modified to contain the token with the default value set.
        public bool ContainsValidValue { get; set; } // This keeps track of whether or not the user's config contains a valid value for this token.
        protected Func<JToken, string, bool> ValidationFunction { get; set; } // This function will be executed on the value found in the user config for this token, if a value exists.

        public ConfigToken(string inputName, Func<JToken, string, bool> inputValidationFunction, string inputHelpString, string inputDefaultValue = null)
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
        public bool Validate(JToken tokenValue)
        {
            bool ValidToken = true;
            ValidToken = ValidationFunction(tokenValue, TokenName);
            return ValidToken;
        }
        public override string ToString()
        {
            return TokenName;
        }
    }
}
