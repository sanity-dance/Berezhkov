using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Berezhkov;

namespace BerezhkovTesting
{
    public class ExampleConfig : JsonConfig
    {
        string[] AcceptableFruits = { "Grape", "Orange", "Apple" };
        public ExampleConfig(JObject inputUserConfig)
        {
            UserConfig = inputUserConfig;
            RequiredConfigTokens = GetDictionary(new ConfigToken[]
            {
            new ConfigToken("Fruit",ValidationFactory<string>(ConstrainStringValues(new List<string>(AcceptableFruits))),"String: A helpful message to the user describing what this token is and why it must be one of these fruits and no others."),
            new ConfigToken("FruitProperties",ValidationFactory<JObject>(ConstrainJsonTokens(
                new ConfigToken[] {
                new ConfigToken("Ripe",ValidationFactory<bool>(),"Bool: Indicates whether or not the fruit is ripe."),
                new ConfigToken("MarketValue",ValidationFactory<int>(),"Int: Average price of one pound of the fruit in question. Decimals are not allowed because everyone who appends .99 to their prices in order to trick the human brain is insubordinate and churlish.")
                },
                new ConfigToken[] {
                new ConfigToken("Color",ValidationFactory<string>(),"String: Indicates the color of the fruit.")
                })),"Json: Additional properties of the fruit in question."),
            new ConfigToken("NumberConsumed",ValidationFactory<int>(),"Int: A helpful message to the user describing why they must tell your mysterious program how many fruits they have consumed.")
            });

            OptionalConfigTokens = GetDictionary(new ConfigToken[]
            {
            new ConfigToken("NearestMarket",ValidationFactory<string>(),"String: Location of the nearest fruit market. Mutually exclusive with ClosestMarket."),
            new ConfigToken("ClosestMarket",ValidationFactory<string>(),"String: Location of nearest fruit market. Mutually exclusive with NearestMarket.")
            });

            MutuallyExclusiveTokenSets = new List<List<List<ConfigToken>>>();

            MutuallyExclusiveTokenSets.Add(new List<List<ConfigToken>>() { new List<ConfigToken>() { OptionalConfigTokens["NearestMarket"] }, new List<ConfigToken>() { OptionalConfigTokens["ClosestMarket"] } });

            Initialize();
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
            using (var sw = new StringWriter())
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

    class Program
    {
        static void Main(string[] args)
        {
            ExampleConfig example = new ExampleConfig(JObject.Parse(
                @"{
	                'Fruit':'Melon',
                    'FruitProperties':{
                        'MarketValue':15.99
                    },
	                'NumberConsumed':3,
	                'NearestMarket':'Barcelona'
                }"));
            Console.WriteLine("Hello World!");
        }
    }
}
