using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TransitLinkChatbot.Classes;

namespace TransitLinkChatbot.Dialogs
{
    public class MainDialog : ComponentDialog
    {
        private readonly IStatePropertyAccessor<Travel> _MainAccessor;
        private readonly IStatePropertyAccessor<UserProfile> _QnAAccessor;
        private readonly IStatePropertyAccessor<FindNearestDestination> _FindNearestAccessor;
        protected readonly BotState ConversationState;
        protected readonly BotState UserState;

        public bool reprompt;

        public string destination_type { get; set; }

        public MainDialog(ConversationState conversationState, UserState userState)
            : base(nameof(MainDialog))
        {
            _MainAccessor = userState.CreateProperty<Travel>("Travel");
            _QnAAccessor = userState.CreateProperty<UserProfile>("UserProfile");
            _FindNearestAccessor = userState.CreateProperty<FindNearestDestination>("FindNearestDestination");
            ConversationState = conversationState;
            UserState = userState;

            // This array defines how the Waterfall will execute.
            var waterfallSteps = new WaterfallStep[]
            {
                ChooseDialogStepAsync,
                ToLocationStepAsync,
                FromLocationStepAsync,
                TransportStepAsync,
                GetMapStepAsync,
                SummaryStepAsync,
            };

            // Add named dialogs to the DialogSet. These names are saved in the dialog state.
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), waterfallSteps));
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));
            AddDialog(new TopupDialog(conversationState, userState));
            AddDialog(new FeedbackDialog(conversationState, userState));
            AddDialog(new BusArrivalDialog(conversationState, userState));
            AddDialog(new FindNearestDialog(conversationState, userState));
            AddDialog(new eFeedbackDialog(conversationState, userState));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> ChooseDialogStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var user = await _QnAAccessor.GetAsync(stepContext.Context, () => new UserProfile(), cancellationToken);
            var findNearest = await _FindNearestAccessor.GetAsync(stepContext.Context, () => new FindNearestDestination(), cancellationToken);
            if (user.IsAsking == "Yes")
            {
                return await stepContext.ReplaceDialogAsync(nameof(FeedbackDialog), null, cancellationToken);
            }
            else if (stepContext.Context.Activity.Text.ToLowerInvariant() == "ask for directions" || reprompt == true)
            {
                return await stepContext.NextAsync(null, cancellationToken);
            }
            else if (stepContext.Context.Activity.Text.ToLowerInvariant() == "top up ez-link")
            {
                return await stepContext.ReplaceDialogAsync(nameof(TopupDialog), null, cancellationToken);
            }
            else if (stepContext.Context.Activity.Text.ToLowerInvariant() == "check bus arrival")
            {
                return await stepContext.ReplaceDialogAsync(nameof(BusArrivalDialog), null, cancellationToken);
            }
            else if (stepContext.Context.Activity.Text.ToLowerInvariant() == "find nearest amenity")
            {
                return await stepContext.ReplaceDialogAsync(nameof(FindNearestDialog), null, cancellationToken);
            }
            else if (stepContext.Context.Activity.Text.ToLowerInvariant() == "find nearest money changer")
            {
                findNearest.destinationType = "money changer";
                return await stepContext.ReplaceDialogAsync(nameof(FindNearestDialog), null, cancellationToken);
            }
            else if (stepContext.Context.Activity.Text.ToLowerInvariant() == "find nearest park")
            {
                findNearest.destinationType = "national park";
                return await stepContext.ReplaceDialogAsync(nameof(FindNearestDialog), null, cancellationToken);
            }
            else if (stepContext.Context.Activity.Text.ToLowerInvariant() == "find nearest restaurant")
            {
                findNearest.destinationType = "restaurant";
                return await stepContext.ReplaceDialogAsync(nameof(FindNearestDialog), null, cancellationToken);
            }
            else if (stepContext.Context.Activity.Text.ToLowerInvariant() == "find nearest hotel")
            {
                findNearest.destinationType = "hotel";
                return await stepContext.ReplaceDialogAsync(nameof(FindNearestDialog), null, cancellationToken);
            }
            else if (stepContext.Context.Activity.Text.ToLowerInvariant() == "provide feedback")
            {
                return await stepContext.ReplaceDialogAsync(nameof(eFeedbackDialog), null, cancellationToken);
            }
            else
            {
                reprompt = false;
                await stepContext.PromptAsync(nameof(ChoicePrompt),
                new PromptOptions
                {

                    Prompt = MessageFactory.Text("Sorry, I do not understand what you mean by that. What would you like us to help you with?"),
                    Choices = new List<Choice>
                    {
                        new Choice
                        {
                            Value = "Ask for directions",
                            Synonyms = new List<string>
                            {
                                "I want to go somewhere",
                                "I am looking for directions",
                            },
                        },

                        new Choice
                        {
                            Value = "Top up EZ-Link",
                            Synonyms = new List<string>
                            {
                                "I want to top up my EZ-Link card",
                                "I want to top up my card",
                                "I want to top up my concession card"
                            },
                        },

                        new Choice
                        {
                            Value = "Check bus arrival",
                            Synonyms = new List<string>
                            {
                                "I want to check bus timings",
                                "I want to check bus timing",
                                "I want to check bus stop"
                            },
                        },

                        new Choice
                        {
                            Value = "Find nearest amenity",
                            Synonyms = new List<string>
                            {
                                "I am looking for the nearest place",
                                "I am looking for the nearest places",
                                "I am looking for the nearest amenity",
                                "I am looking for the nearest amenities",
                                "Nearest place",
                                "Nearest places",
                                "Nearest amenity",
                                "Nearest amenities"
                            }
                        },

                        new Choice
                        {
                            Value = "Provide feedback",
                            Synonyms = new List<string>
                            {
                                "I would like to provide my feedback",
                            }
                        }
                    },
                }, cancellationToken);
                return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }
        }

        private async Task<DialogTurnResult> ToLocationStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var findNearest = await _FindNearestAccessor.GetAsync(stepContext.Context, () => new FindNearestDestination(), cancellationToken);
            if (stepContext.Context.Activity.Text.ToLowerInvariant() == "ask for directions" || reprompt == true)
            {
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("Where would you like to go to?") }, cancellationToken);
            }
            else if (stepContext.Context.Activity.Text.ToLowerInvariant() == "top up ez-link")
            {
                return await stepContext.BeginDialogAsync(nameof(TopupDialog), null, cancellationToken);
            }
            else if (stepContext.Context.Activity.Text.ToLowerInvariant() == "check bus arrival")
            {
                return await stepContext.ReplaceDialogAsync(nameof(BusArrivalDialog), null, cancellationToken);
            }
            else if (stepContext.Context.Activity.Text.ToLowerInvariant() == "find nearest amenity")
            {
                return await stepContext.ReplaceDialogAsync(nameof(FindNearestDialog), null, cancellationToken);
            }
            else if (stepContext.Context.Activity.Text.ToLowerInvariant() == "find nearest money changer")
            {
                findNearest.destinationType = "money changer";
                return await stepContext.ReplaceDialogAsync(nameof(FindNearestDialog), null, cancellationToken);
            }
            else if (stepContext.Context.Activity.Text.ToLowerInvariant() == "find nearest park")
            {
                findNearest.destinationType = "national park";
                return await stepContext.ReplaceDialogAsync(nameof(FindNearestDialog), null, cancellationToken);
            }
            else if (stepContext.Context.Activity.Text.ToLowerInvariant() == "find nearest restaurant")
            {
                findNearest.destinationType = "restaurant";
                return await stepContext.ReplaceDialogAsync(nameof(FindNearestDialog), null, cancellationToken);
            }
            else if (stepContext.Context.Activity.Text.ToLowerInvariant() == "find nearest hotel")
            {
                findNearest.destinationType = "hotel";
                return await stepContext.ReplaceDialogAsync(nameof(FindNearestDialog), null, cancellationToken);
            }
            else if (stepContext.Context.Activity.Text.ToLowerInvariant() == "provide feedback")
            {
                return await stepContext.ReplaceDialogAsync(nameof(eFeedbackDialog), null, cancellationToken);
            }
            else
            {
                reprompt = false;
                return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }
        }

        private async Task<DialogTurnResult> FromLocationStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["to_location"] = (string)stepContext.Result;
            if (stepContext.Values["to_location"] != null)
            {
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("Where are you currently located at?") }, cancellationToken);
            }
            else
            {
                reprompt = false;
                return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }
        }

        private async Task<DialogTurnResult> TransportStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // WaterfallStep always finishes with the end of the Waterfall or with another dialog; here it is a Prompt Dialog.
            // Running a prompt here means the next WaterfallStep will be run when the user's response is received.
            stepContext.Values["from_location"] = (string)stepContext.Result;
            if (stepContext.Values["from_location"] != null)
            {
                return await stepContext.PromptAsync(nameof(ChoicePrompt),
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("How would you like to go to your destination?"),
                    RetryPrompt = MessageFactory.Text("Please select a mode of transportation."),
                    Choices = ChoiceFactory.ToChoices(new List<string> { "Car", "Bicycle", "Bus/MRT", "Walk", "Any of the following" }),
                }, cancellationToken);
            }
            else
            {
                reprompt = false;
                return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }
        }

        private async Task<DialogTurnResult> GetMapStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string travelMode = ((FoundChoice)stepContext.Result).Value;
            string mode = "";
            if (((FoundChoice)stepContext.Result).Value == "Car")
            {
                travelMode = "&travelmode=driving";
                mode = " via driving";
            }
            else if (((FoundChoice)stepContext.Result).Value == "Bicycle")
            {
                travelMode = "&travelmode=bicycling";
                mode = " via cycling";
            }
            else if (((FoundChoice)stepContext.Result).Value == "Bus/MRT")
            {
                travelMode = "&travelmode=transit";
                mode = " via transit";
            }
            else if (((FoundChoice)stepContext.Result).Value == "Walk")
            {
                travelMode = "&travelmode=walking";
                mode = " via walking";
            }
            else
            {
                travelMode = "";
            }
            stepContext.Values["transport"] = travelMode;
            var travel = await _MainAccessor.GetAsync(stepContext.Context, () => new Travel(), cancellationToken);
            var msg = "";

            travel.Transport = (string)stepContext.Values["transport"];
            travel.ToLocation = (string)stepContext.Values["to_location"];
            travel.FromLocation = (string)stepContext.Values["from_location"];

            string origin = travel.FromLocation.Replace(' ', '+');
            origin = origin.Replace(",", "%2C");
            string destination = travel.ToLocation.Replace(' ', '+');
            destination = destination.Replace(",", "%2C");

            string url = $"https://www.google.com/maps/dir/?api=1&origin=" + origin + "&destination=" + destination + travelMode;

            WebRequest request = WebRequest.Create(url);

            WebResponse response = request.GetResponse();

            Stream data = response.GetResponseStream();

            StreamReader reader = new StreamReader(data);

            // json-formatted string from maps api
            string responseFromServer = reader.ReadToEnd();

            response.Close();

            msg = $"Click this [link]({url}) to get directions from {travel.FromLocation} to {travel.ToLocation}{mode}.";

            await stepContext.Context.SendActivityAsync(MessageFactory.Text(msg), cancellationToken);
            return await stepContext.PromptAsync(nameof(ChoicePrompt),
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("Did you get what you were looking for?"),
                    RetryPrompt = MessageFactory.Text("Please select 'Yes' or 'No'."),
                    Choices = ChoiceFactory.ToChoices(new List<string> { "Yes", "No" }),
                }, cancellationToken);
        }

        private async Task<DialogTurnResult> SummaryStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (((FoundChoice)stepContext.Result).Value == "Yes")
            {
                reprompt = false;
                var msg = $"Thank you for your time!";
                await stepContext.Context.SendActivityAsync(MessageFactory.Text(msg), cancellationToken);
                return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }
            else if (((FoundChoice)stepContext.Result).Value == "No")
            {
                reprompt = true;
                return await stepContext.ReplaceDialogAsync(nameof(MainDialog), null, cancellationToken);
            }
            else
            {
                reprompt = false;
                return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }
        }
    }
}
