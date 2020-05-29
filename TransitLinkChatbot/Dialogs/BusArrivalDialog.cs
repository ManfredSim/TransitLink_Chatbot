using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransitLinkChatbot.Dialogs
{
    public class BusArrivalDialog : ComponentDialog
    {
        protected readonly BotState ConversationState;
        protected readonly BotState UserState;

        public partial class BusArrival
        {
            [JsonProperty("BusStopCode")]
            public string BusStopCode { get; set; }

            [JsonProperty("Services")]
            public Service[] Services { get; set; }
        }

        public partial class Service
        {
            [JsonProperty("ServiceNo")]
            public string ServiceNo { get; set; }

            [JsonProperty("NextBus")]
            public NextBus NextBus { get; set; }

            [JsonProperty("NextBus2")]
            public NextBus NextBus2 { get; set; }

            [JsonProperty("NextBus3")]
            public NextBus NextBus3 { get; set; }
        }

        public partial class NextBus
        {

            [JsonProperty("EstimatedArrival")]
            public string EstimatedArrival { get; set; }

            [JsonProperty("Load")]
            public string Load { get; set; }

            [JsonProperty("Feature")]
            public string Feature { get; set; }

        }

        public partial class BusStop
        {
            [JsonProperty("data")]
            public data data { get; set; }
        }

        public partial class data
        {
            [JsonProperty("description")]
            public string description { get; set; }
        }

        public class BusDetail
        {
            // A list of things that users have said to the bot
            public List<string> BusDetails { get; } = new List<string>();
        }

        public BusArrivalDialog(ConversationState conversationState, UserState userState) : base(nameof(BusArrivalDialog))
        {
            ConversationState = conversationState;
            UserState = userState;

            // This array defines how the Waterfall will execute.
            var waterfallSteps = new WaterfallStep[]
            {
                BusStepAsync,
                ResultsStepAsync,
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

        private async Task<DialogTurnResult> BusStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("Enter a bus stop code.") }, cancellationToken);
        }

        private async Task<DialogTurnResult> ResultsStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string BusStopCode = (string)stepContext.Result;
            string url = $"http://datamall2.mytransport.sg/ltaodataservice/BusArrivalv2?BusStopCode=" + BusStopCode;
            string url2 = $"https://tih-api.stb.gov.sg/transport/v1/bus_stop/bus_stop/" + BusStopCode + "?apikey=jtAPh35dQAKPzRR26cOmhWAQjya5oHTb";

            string[] busDetails = { };

            var followUpCheckHttpClient = new HttpClient();

            followUpCheckHttpClient.DefaultRequestHeaders.Add("AccountKey", "3H9uO8X2SgiVDILsxXneCA==");

            followUpCheckHttpClient.DefaultRequestHeaders.Add("accept", "application/json");

            var checkFollowUpJsonService = followUpCheckHttpClient.GetAsync(url).Result.Content.ReadAsAsync<object>().Result;

            var followUpCheckServices = JsonConvert.DeserializeObject<BusArrival>(checkFollowUpJsonService.ToString());

            var busdetails = new BusDetail();

            var followUpCheckHttpClient2 = new HttpClient();

            var checkFollowUpJsonService2 = followUpCheckHttpClient2.GetAsync(url2).Result.Content.ReadAsAsync<object>().Result;

            var followUpCheckBusStop = JsonConvert.DeserializeObject<BusStop>(checkFollowUpJsonService2.ToString());

            string description = followUpCheckBusStop.data.description.ToString();

            DateTime now = DateTime.Now;

            for (int i = 0; i < followUpCheckServices.Services.Length; i++)
            {
                string serviceNo = followUpCheckServices.Services[i].ServiceNo.ToString();
                string nextBusArr = followUpCheckServices.Services[i].NextBus.EstimatedArrival.ToString();
                string nextBusLoad = followUpCheckServices.Services[i].NextBus.Load.ToString();
                string nextBusFeature = followUpCheckServices.Services[i].NextBus.Feature.ToString();
                string nextBusArr2 = followUpCheckServices.Services[i].NextBus2.EstimatedArrival.ToString();
                string nextBusLoad2 = followUpCheckServices.Services[i].NextBus2.Load.ToString();
                string nextBusFeature2 = followUpCheckServices.Services[i].NextBus2.Feature.ToString();
                string nextBusArr3 = followUpCheckServices.Services[i].NextBus3.EstimatedArrival.ToString();
                string nextBusLoad3 = followUpCheckServices.Services[i].NextBus3.Load.ToString();
                string nextBusFeature3 = followUpCheckServices.Services[i].NextBus3.Feature.ToString();
                TimeSpan span1, span2, span3;
                string nextBusInMinutes = "";
                string nextBus2InMinutes = "";
                string nextBus3InMinutes = "";
                if (DateTime.TryParse(nextBusArr.ToString(), out DateTime dt))
                {
                    DateTime nextBus = Convert.ToDateTime(nextBusArr);
                    span1 = nextBus.Subtract(now);
                    if (Math.Round(span1.TotalMinutes, 0) < 1)
                    {
                        nextBusInMinutes = "Arr";
                    }
                    else
                    {
                        nextBusInMinutes = Math.Round(span1.TotalMinutes, 0).ToString() + "m";
                    }
                }
                if (DateTime.TryParse(nextBusArr2.ToString(), out dt))
                {
                    DateTime nextBus = Convert.ToDateTime(nextBusArr2);
                    span2 = nextBus.Subtract(now);
                    nextBus2InMinutes = " | " + Math.Round(span2.TotalMinutes, 0).ToString() + "m";
                }
                if (DateTime.TryParse(nextBusArr3.ToString(), out dt))
                {
                    DateTime nextBus = Convert.ToDateTime(nextBusArr3);
                    span3 = nextBus.Subtract(now);
                    nextBus3InMinutes = " | " + Math.Round(span3.TotalMinutes, 0).ToString() + "m";
                }

                string isWheelChair = "";
                string isWheelChair2 = "";
                string isWheelChair3 = "";

                if (nextBusFeature == "WAB")
                {
                    isWheelChair = "*";
                }
                if (nextBusFeature2 == "WAB")
                {
                    isWheelChair2 = "*";
                }
                if (nextBusFeature3 == "WAB")
                {
                    isWheelChair3 = "*";
                }
                string str = "**" + serviceNo + ":** " + nextBusInMinutes + isWheelChair + nextBus2InMinutes + isWheelChair2 + nextBus3InMinutes + isWheelChair3;
                busdetails.BusDetails.Add(str);
            }

            if (followUpCheckServices.Services.Any())
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text($"**Bus Arrival Timings:**\n\n **Bus Stop: {BusStopCode} - {description}**: \n\n{string.Join("\n\n------\n\n", busdetails.BusDetails)} \n\n\n\n\\* *Wheelchair Accessible* \n\n*Last updated: {DateTime.Now.ToLongTimeString()}*"), cancellationToken);
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
                await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Bus stop code {BusStopCode} is either not valid, or there are no bus services available. Please try again."), cancellationToken);
                return await stepContext.ReplaceDialogAsync(nameof(BusArrivalDialog), null, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> SummaryStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (((FoundChoice)stepContext.Result).Value == "Yes")
            {
                var msg = $"Thank you for your time!";
                await stepContext.Context.SendActivityAsync(MessageFactory.Text(msg), cancellationToken);
                return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }
            else if (((FoundChoice)stepContext.Result).Value == "No")
            {
                return await stepContext.ReplaceDialogAsync(nameof(BusArrivalDialog), null, cancellationToken);
            }
            else
            {
                return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }
        }
    }
}
