using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TransitLinkChatbot.Classes;

namespace TransitLinkChatbot.Dialogs
{
    public class FindNearestDialog : ComponentDialog
    {
        private readonly IStatePropertyAccessor<FindNearest> _MainAccessor;
        private readonly IStatePropertyAccessor<FindNearestDestination> _FindNearestAccessor;
        protected readonly BotState ConversationState;
        protected readonly BotState UserState;

        public partial class FindNearest
        {
            [JsonProperty("results")]
            public results[] results { get; set; }

            public string Latitude { get; set; }

            public string Longitude { get; set; }
        }

        public partial class results
        {
            [JsonProperty("geometry")]
            public geometry geometry { get; set; }
        }

        public partial class geometry
        {
            [JsonProperty("location")]
            public location location { get; set; }
        }

        public partial class location
        {
            [JsonProperty("lat")]
            public double latitude { get; set; }

            [JsonProperty("lng")]
            public double longitude { get; set; }
        }

        public partial class NearestMoneyChanger
        {
            [JsonProperty("data")]
            public data[] data { get; set; }
        }

        public partial class NearestNationalPark
        {
            [JsonProperty("data")]
            public data[] data { get; set; }
        }

        public partial class data
        {
            [JsonProperty("name")]
            public string name { get; set; }

            [JsonProperty("location")]
            public coordinates location { get; set; }

            [JsonProperty("address")]
            public address address { get; set; }
        }

        public partial class coordinates
        {
            [JsonProperty("latitude")]
            public double latitude { get; set; }

            [JsonProperty("longitude")]
            public double longitude { get; set; }
        }

        public partial class address
        {
            [JsonProperty("addressLine1")]
            public string addressLine1 { get; set; }

            [JsonProperty("addressLine2")]
            public string addressLine2 { get; set; }

            [JsonProperty("postalCode")]
            public string postalCode { get; set; }
        }

        public class MoneyChangerDetail
        {
            // A list of things that users have said to the bot
            public List<string> MoneyChangerDetails { get; } = new List<string>();
        }

        public class NationalParkDetail
        {
            // A list of things that users have said to the bot
            public List<string> NationalParkDetails { get; } = new List<string>();
        }

        public FindNearestDialog(ConversationState conversationState, UserState userState) : base(nameof(FindNearestDialog))
        {
            _MainAccessor = userState.CreateProperty<FindNearest>("FindNearest");
            _FindNearestAccessor = userState.CreateProperty<FindNearestDestination>("FindNearestDestination");
            ConversationState = conversationState;
            UserState = userState;

            // This array defines how the Waterfall will execute.
            var waterfallSteps = new WaterfallStep[]
            {
                LocationStepAsync,
                CoordinatesStepAsync,
                ResultStepAsync,
                SummaryStepAsync
            };

            // Add named dialogs to the DialogSet. These names are saved in the dialog state.
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), waterfallSteps));
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new NumberPrompt<int>(nameof(NumberPrompt<int>)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        public bool hasResults = false;

        private async Task<DialogTurnResult> LocationStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("Where are you currently located at?") }, cancellationToken);
        }

        private async Task<DialogTurnResult> CoordinatesStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string address = (string)stepContext.Result;
            address = address.Replace(" ", "%20");
            string url = $"https://maps.googleapis.com/maps/api/geocode/json?address=" + address + ",&key=AIzaSyBi0_TZegOvOHwrvgiXk4wvZvNfwNSoHn0";

            var followUpCheckHttpClient = new HttpClient();

            var checkFollowUpJsonResult = followUpCheckHttpClient.GetAsync(url).Result.Content.ReadAsAsync<object>().Result;

            var followUpCheckResults = JsonConvert.DeserializeObject<FindNearest>(checkFollowUpJsonResult.ToString());

            var findNearest = await _FindNearestAccessor.GetAsync(stepContext.Context, () => new FindNearestDestination(), cancellationToken);

            string latitude = "";

            string longitude = "";

            for (int i = 0; i < followUpCheckResults.results.Length; i++)
            {
                latitude = followUpCheckResults.results[0].geometry.location.latitude.ToString();
                longitude = followUpCheckResults.results[0].geometry.location.longitude.ToString();
            }

            if (!string.IsNullOrEmpty(latitude) && !string.IsNullOrEmpty(longitude))
            {
                stepContext.Values["lat"] = latitude;
                stepContext.Values["lng"] = longitude;
                if (!string.IsNullOrEmpty(findNearest.destinationType))
                {
                    return await stepContext.NextAsync(null, cancellationToken);
                }
                else
                {
                    return await stepContext.PromptAsync(nameof(ChoicePrompt),
                    new PromptOptions
                    {
                        Prompt = MessageFactory.Text("Select a destination type."),
                        RetryPrompt = MessageFactory.Text("Please select a destination type."),
                        Choices = ChoiceFactory.ToChoices(new List<string> { "Money Changer", "Park", "Restaurant", "Hotel", "Cancel" }),
                    }, cancellationToken);
                }
            }
            else
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Location is invalid. Please try again."), cancellationToken);
                return await stepContext.ReplaceDialogAsync(nameof(FindNearestDialog), null, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> ResultStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string url = "";
            var coordinates = await _MainAccessor.GetAsync(stepContext.Context, () => new FindNearest(), cancellationToken);
            coordinates.Latitude = (string)stepContext.Values["lat"];
            coordinates.Longitude = (string)stepContext.Values["lng"];
            string address = coordinates.Latitude + "%2C" + coordinates.Longitude;
            var moneychangerdetails = new MoneyChangerDetail();
            var nationalparkdetails = new NationalParkDetail();

            var followUpCheckHttpClient = new HttpClient();
            var value = "";

            var findNearest = await _FindNearestAccessor.GetAsync(stepContext.Context, () => new FindNearestDestination(), cancellationToken);

            if (string.IsNullOrEmpty(findNearest.destinationType))
            {
                value = ((FoundChoice)stepContext.Result).Value;
            }
            else
            {
                if (findNearest.destinationType == "money changer")
                {
                    value = "Money Changer";
                }
                else if (findNearest.destinationType == "restaurant")
                {
                    value = "Restaurant";
                }
                else if (findNearest.destinationType == "hotel")
                {
                    value = "Hotel";
                }
                else
                {
                    value = "Park";
                }
            }

            if (value == "Money Changer")
            {
                url = $"https://tih-api.stb.gov.sg/money-changer/v1?location=" + address + "&apikey=jtAPh35dQAKPzRR26cOmhWAQjya5oHTb";

                var checkFollowUpJsonResult = followUpCheckHttpClient.GetAsync(url).Result.Content.ReadAsAsync<object>().Result;

                var followUpCheckResults = JsonConvert.DeserializeObject<NearestMoneyChanger>(checkFollowUpJsonResult.ToString());

                for (int i = 0; i < followUpCheckResults.data.Length; i++)
                {
                    string name = followUpCheckResults.data[i].name.ToString();
                    string addressline1 = followUpCheckResults.data[i].address.addressLine1.ToString();
                    string addressline2 = followUpCheckResults.data[i].address.addressLine2.ToString();
                    string postalcode = followUpCheckResults.data[i].address.postalCode.ToString();
                    double latitude = Convert.ToDouble(followUpCheckResults.data[i].location.latitude);
                    double longitude = Convert.ToDouble(followUpCheckResults.data[i].location.longitude);
                    double latitude_diff = latitude - Convert.ToDouble(coordinates.Latitude);
                    double longitude_diff = longitude - Convert.ToDouble(coordinates.Longitude);
                    var a = (Math.Sin(latitude_diff / 2) * Math.Sin(latitude_diff / 2) +
                        Math.Cos(latitude)) * Math.Cos(Convert.ToDouble(coordinates.Latitude)) *
                        Math.Sin(longitude_diff / 2) * Math.Sin(longitude_diff / 2);
                    var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
                    decimal d = 6371 * Convert.ToDecimal(c);
                    string measurement = "";
                    string distance = "";
                    if (d < 1)
                    {
                        d = d * 1000;
                        measurement = "m";
                        distance = Math.Round(d, 0).ToString() + measurement;
                    }
                    else
                    {
                        measurement = "km";
                        distance = Math.Round(d, 1).ToString() + measurement;
                    }
                    string str = "**Name:** " + name + "\n\n**Address:** " + addressline1 + " " + addressline2 + " Singapore " + postalcode + "\n\n**Distance from Your Location:** " + distance;
                    moneychangerdetails.MoneyChangerDetails.Add(str);
                }

                if (followUpCheckResults.data.Any())
                {
                    hasResults = true;
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Here are the money changers we found that are near you: \n\n\n\n{string.Join("\n\n------\n\n", moneychangerdetails.MoneyChangerDetails)}"), cancellationToken);
                    return await stepContext.PromptAsync(nameof(ChoicePrompt),
                    new PromptOptions
                    {
                        Prompt = MessageFactory.Text("Did you get what you were looking for?"),
                        RetryPrompt = MessageFactory.Text("Please select 'Yes' or 'No'."),
                        Choices = ChoiceFactory.ToChoices(new List<string> { "Yes", "No" }),
                    }, cancellationToken);
                }

                else
                {
                    hasResults = false;
                    return await stepContext.PromptAsync(nameof(ChoicePrompt),
                    new PromptOptions
                    {
                        Prompt = MessageFactory.Text("There are no money changers available near you. Would you like to try again?"),
                        RetryPrompt = MessageFactory.Text("Please select 'Yes' or 'No'."),
                        Choices = ChoiceFactory.ToChoices(new List<string> { "Yes", "No" }),
                    }, cancellationToken);  
                }
            }
            else if (value == "Park")
            {
                url = $"https://tih-api.stb.gov.sg/national-park/v1?location=" + address + "&apikey=jtAPh35dQAKPzRR26cOmhWAQjya5oHTb";

                var checkFollowUpJsonResult = followUpCheckHttpClient.GetAsync(url).Result.Content.ReadAsAsync<object>().Result;

                var followUpCheckResults = JsonConvert.DeserializeObject<NearestNationalPark>(checkFollowUpJsonResult.ToString());

                for (int i = 0; i < followUpCheckResults.data.Length; i++)
                {
                    string name = followUpCheckResults.data[i].name.ToString();
                    double latitude = Convert.ToDouble(followUpCheckResults.data[i].location.latitude);
                    double longitude = Convert.ToDouble(followUpCheckResults.data[i].location.longitude);
                    double latitude_diff = latitude - Convert.ToDouble(coordinates.Latitude);
                    double longitude_diff = longitude - Convert.ToDouble(coordinates.Longitude);
                    var a = (Math.Sin(latitude_diff / 2) * Math.Sin(latitude_diff / 2) +
                        Math.Cos(latitude)) * Math.Cos(Convert.ToDouble(coordinates.Latitude)) *
                        Math.Sin(longitude_diff / 2) * Math.Sin(longitude_diff / 2);
                    var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
                    decimal d = 6371 * Convert.ToDecimal(c);
                    string measurement = "";
                    string distance = "";
                    if (d < 1)
                    {
                        d = d * 1000;
                        measurement = "m";
                        distance = Math.Round(d, 0).ToString() + measurement;
                    }
                    else
                    {
                        measurement = "km";
                        distance = Math.Round(d, 1).ToString() + measurement;
                    }
                    string str = "**Name:** " + name + "\n\n**Distance from Your Location:** " + distance;
                    nationalparkdetails.NationalParkDetails.Add(str);
                }

                if (followUpCheckResults.data.Any())
                {
                    hasResults = true;
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Here are the parks we found that are near you: \n\n\n\n{string.Join("\n\n------\n\n", nationalparkdetails.NationalParkDetails)}"), cancellationToken);
                    return await stepContext.PromptAsync(nameof(ChoicePrompt),
                    new PromptOptions
                    {
                        Prompt = MessageFactory.Text("Did you get what you were looking for?"),
                        RetryPrompt = MessageFactory.Text("Please select 'Yes' or 'No'."),
                        Choices = ChoiceFactory.ToChoices(new List<string> { "Yes", "No" }),
                    }, cancellationToken);
                }

                else
                {
                    hasResults = false;
                    return await stepContext.PromptAsync(nameof(ChoicePrompt),
                    new PromptOptions
                    {
                        Prompt = MessageFactory.Text("There are no parks available near you. Would you like to try again?"),
                        RetryPrompt = MessageFactory.Text("Please select 'Yes' or 'No'."),
                        Choices = ChoiceFactory.ToChoices(new List<string> { "Yes", "No" }),
                    }, cancellationToken);
                }
            }
            else if (value == "Restaurant")
            {
                url = $"https://www.google.com/maps/search/Restaurants/@" + address;

                WebRequest request = WebRequest.Create(url);

                WebResponse response = request.GetResponse();

                Stream data = response.GetResponseStream();

                StreamReader reader = new StreamReader(data);

                // json-formatted string from maps api
                string responseFromServer = reader.ReadToEnd();

                response.Close();

                var msg = $"Click this [link]({url}) to get the nearest restaurants to you.";

                hasResults = true;

                await stepContext.Context.SendActivityAsync(MessageFactory.Text(msg), cancellationToken);
                return await stepContext.PromptAsync(nameof(ChoicePrompt),
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("Did you get what you were looking for?"),
                    RetryPrompt = MessageFactory.Text("Please select 'Yes' or 'No'."),
                    Choices = ChoiceFactory.ToChoices(new List<string> { "Yes", "No" }),
                }, cancellationToken);
            }
            else if (value == "Hotel")
            {
                url = $"https://www.google.com/maps/search/Hotels/@" + address;

                WebRequest request = WebRequest.Create(url);

                WebResponse response = request.GetResponse();

                Stream data = response.GetResponseStream();

                StreamReader reader = new StreamReader(data);

                // json-formatted string from maps api
                string responseFromServer = reader.ReadToEnd();

                response.Close();

                var msg = $"Click this [link]({url}) to get the nearest hotels to you.";

                hasResults = true;

                await stepContext.Context.SendActivityAsync(MessageFactory.Text(msg), cancellationToken);
                return await stepContext.PromptAsync(nameof(ChoicePrompt),
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("Did you get what you were looking for?"),
                    RetryPrompt = MessageFactory.Text("Please select 'Yes' or 'No'."),
                    Choices = ChoiceFactory.ToChoices(new List<string> { "Yes", "No" }),
                }, cancellationToken);
            }
            else
            {
                findNearest.destinationType = "";
                var msg = $"Thank you for your time!";
                await stepContext.Context.SendActivityAsync(MessageFactory.Text(msg), cancellationToken);
                return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }
        }

        private async Task<DialogTurnResult> SummaryStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var findNearest = await _FindNearestAccessor.GetAsync(stepContext.Context, () => new FindNearestDestination(), cancellationToken);
            findNearest.destinationType = "";
            if (hasResults == true)
            {
                if (((FoundChoice)stepContext.Result).Value == "Yes")
                {
                    var msg = $"Thank you for your time!";
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text(msg), cancellationToken);
                    return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
                }
                else
                {
                    return await stepContext.ReplaceDialogAsync(nameof(FindNearestDialog), null, cancellationToken);
                }
            }
            else
            {
                if (((FoundChoice)stepContext.Result).Value == "Yes")
                {
                    return await stepContext.ReplaceDialogAsync(nameof(FindNearestDialog), null, cancellationToken);
                }
                else
                {
                    var msg = $"Thank you for your time!";
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text(msg), cancellationToken);
                    return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
                }
            }
        }
    }
}
