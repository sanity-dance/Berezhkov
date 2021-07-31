using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Berezhkov;

namespace BerezhkovTesting
{
    public class ExampleConfig : SchemaController
    {
        string[] AcceptableFruits = { "Grape", "Orange", "Apple" };
        public ExampleConfig(JObject inputUserConfig)
        {
            UserConfig = inputUserConfig;
            RequiredConfigTokens.UnionWith(new HashSet<ConfigToken>
            {
                new ConfigToken("Fruit",ValidationFactory<string>(ConstrainStringValues(AcceptableFruits)),"String: A helpful message to the user describing what this token is and why it must be one of these fruits and no others."),
                new ConfigToken("FruitProperties",ValidationFactory<JObject>(ConstrainJsonTokens(
                    new ConfigToken[] {
                        new ConfigToken("Ripe",ValidationFactory<bool>(),"Bool: Indicates whether or not the fruit is ripe."),
                        new ConfigToken("MarketValue",ValidationFactory<int>(ConstrainNumericValue((0,5),(10,15))),"Int: Average price of one pound of the fruit in question. Decimals are not allowed because everyone who appends .99 to their prices in order to trick the human brain is insubordinate and churlish.")
                    },
                    new ConfigToken[] {
                        new ConfigToken("Color",ValidationFactory<string>(),"String: Indicates the color of the fruit.")
                    })),"Json: Additional properties of the fruit in question."),
                new ConfigToken("NumberConsumed",ValidationFactory<int>(ConstrainNumericValue(0)),"Int: A helpful message to the user describing why they must tell your mysterious program how many fruits they have consumed.")
             });

            OptionalConfigTokens.UnionWith(new HashSet<ConfigToken>
            {
                new ConfigToken("NearestMarket",ValidationFactory<string>(),"String: Location of the nearest fruit market. Mutually exclusive with ClosestMarket."),
                new ConfigToken("MohsHardnessRatingsOfRecentFruitPurchases",ValidationFactory<JArray>(ApplyConstraintsToAllArrayValues<double>(ConstrainNumericValue(0,5.5)),ConstrainArrayCount(1,4)),"Array: Array of doubles between 0 and 5.5.")
            });

            Initialize();
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
            ExampleConfig badExample1 = new ExampleConfig(JObject.Parse(
                @"{
	                'Fruit':'Melon',
                    'FruitProperties':{
                        'Ripe':'no',
                        'MarketValue':7,
                        'Omfed':true
                    },
	                'NumberConsumed':3,
	                'NearestMarket':'Barcelona',
                    'YouThoughtItWasARealTokenButItWasMe':'DIO'
                }"));

            ExampleConfig badExample2 = new ExampleConfig(JObject.Parse(
                @"{
	                'Fruit':'Apple',
                    'FruitProperties':{
                        'Ripe':false,
                        'MarketValue':3
                    },
	                'NumberConsumed':3,
                    'MohsHardnessRatingsOfRecentFruitPurchases':[2.5, 3.8, 9.5, 'eight', 0.1, 3.8, 17]
                }"));

            Console.WriteLine(string.Join("\n", badExample2.ErrorList));
        }
    }
}
