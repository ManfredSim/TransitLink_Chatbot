// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio EchoBot v4.6.2

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.QnA;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using System.Text;
using System.Net.Http;
using Microsoft.Bot.Builder.Dialogs;
using TransitLinkChatbot.Dialogs;
using Microsoft.Extensions.Logging;
using Microsoft.Recognizers.Text;
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Bot.Connector;

namespace TransitLinkChatbot.Bots
{
    public class EchoBot<T> : ActivityHandler where T : Dialog
    {
        protected readonly Dialog Dialog;
        protected readonly BotState ConversationState;
        protected readonly BotState UserState;
        //private readonly IConfiguration _configuration;
        //private readonly IHttpClientFactory _httpClientFactory;
        // Create local Memory Storage.
        private static readonly MemoryStorage _myStorage = new MemoryStorage();

        // Create cancellation token (used by Async Write operation).
        public CancellationToken cancellationToken { get; private set; }

        // Class for storing a log of utterances (text of messages) as a list.
        public class UtteranceLog : IStoreItem
        {
            // A list of things that users have said to the bot
            public List<string> UtteranceList { get; } = new List<string>();

            // The number of conversational turns that have occurred
            public int TurnNumber { get; set; } = 0;

            // Create concurrency control where this is used.
            public string ETag { get; set; } = "*";
        }
        class FollowUpCheckResult
        {
            [JsonProperty("answers")]
            public FollowUpCheckQnAAnswer[] Answers
            {
                get;
                set;
            }
        }

        class FollowUpCheckQnAAnswer
        {
            [JsonProperty("context")]
            public FollowUpCheckContext Context
            {
                get;
                set;
            }
        }

        class FollowUpCheckContext
        {
            [JsonProperty("prompts")]
            public FollowUpCheckPrompt[] Prompts
            {
                get;
                set;
            }
        }

        class FollowUpCheckPrompt
        {
            [JsonProperty("displayText")]
            public string DisplayText
            {
                get;
                set;
            }

            [JsonProperty("qnaId")]
            public string qnaId
            {
                get;
                set;
            }
        }

        public QnAMaker EchoBotQnA { get; private set; }

        public bool isPrompting;
        
        public EchoBot(QnAMakerEndpoint endpoint, ConversationState conversationState, UserState userState, T dialog)
        {
            // connects to QnA Maker endpoint for each turn
            EchoBotQnA = new QnAMaker(endpoint);
            ConversationState = conversationState;
            UserState = userState;
            Dialog = dialog;
        }
        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            await base.OnTurnAsync(turnContext, cancellationToken);

            // Save any state changes that might have occurred during the turn.
            await ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await UserState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            // preserve user input.
            var utterance = turnContext.Activity.Text;
            // make empty local logitems list.
            UtteranceLog logItems = null;

            // see if there are previous messages saved in storage.
            try
            {
                string[] utteranceList = { "UtteranceLog" };
                logItems = _myStorage.ReadAsync<UtteranceLog>(utteranceList).Result?.FirstOrDefault().Value;
            }
            catch
            {
                // Inform the user an error occured.
                await turnContext.SendActivityAsync("Sorry, something went wrong reading your stored messages!");
            }

            // Get the state properties from the turn context.

            var conversationStateAccessors = ConversationState.CreateProperty<ConversationData>(nameof(ConversationData));
            var conversationData = await conversationStateAccessors.GetAsync(turnContext, () => new ConversationData());

            var userStateAccessors = UserState.CreateProperty<UserProfile>(nameof(UserProfile));
            var userProfile = await userStateAccessors.GetAsync(turnContext, () => new UserProfile());

            var qnaOptions = new QnAMakerOptions();
            qnaOptions.ScoreThreshold = 0.8F;
            if (logItems is null)
            {
                // add the current utterance to a new object.
                logItems = new UtteranceLog();
                logItems.UtteranceList.Add(utterance);
                // set initial turn counter to 1.
                logItems.TurnNumber++;

                // Create Dictionary object to hold received user messages.
                var changes = new Dictionary<string, object>();
                {
                    changes.Add("UtteranceLog", logItems);
                }
                try
                {
                    // Save the user message to your Storage.
                    await _myStorage.WriteAsync(changes, cancellationToken);
                }
                catch
                {
                    // Inform the user an error occured.
                    await turnContext.SendActivityAsync("Sorry, something went wrong storing your message!");
                }
            }
            // Else, our Storage already contained saved user messages, add new one to the list.
            else
            {
                // add new message to list of messages to display.
                logItems.UtteranceList.Add(utterance);
                // increment turn counter.
                logItems.TurnNumber++;

                // Create Dictionary object to hold new list of messages.
                var changes = new Dictionary<string, object>();
                {
                    changes.Add("UtteranceLog", logItems);
                };

                try
                {
                    // Save new list to your Storage.
                    await _myStorage.WriteAsync(changes, cancellationToken);
                }
                catch
                {
                    // Inform the user an error occured.
                    await turnContext.SendActivityAsync("Sorry, something went wrong storing your message!");
                }
            }

            // Pass the response entered by the user through the QnA service
            var results = await EchoBotQnA.GetAnswersAsync(turnContext, qnaOptions);
            if (results != null && results.Length > 0)
            {
                // create http client to perform qna query
                var followUpCheckHttpClient = new HttpClient();

                // add QnAAuthKey to Authorization header
                followUpCheckHttpClient.DefaultRequestHeaders.Add("Authorization", "6bdd6777-b320-4e47-90ff-3a59752cf7e4");

                // construct the qna query url
                var url = $"https://qa-service-183656l.azurewebsites.net/qnamaker/knowledgebases/7c8dfc23-28d6-42d0-a149-1036a9526f3b/generateAnswer";

                // post query
                var checkFollowUpJsonResponse = await followUpCheckHttpClient.PostAsync(url, new StringContent("{\"question\":\"" + turnContext.Activity.Text + "\"}", Encoding.UTF8, "application/json")).Result.Content.ReadAsStringAsync();

                //parse result
                var followUpCheckResult = JsonConvert.DeserializeObject<FollowUpCheckResult>(checkFollowUpJsonResponse);

                // initialize reply message containing the default answer
                var reply = MessageFactory.Text(results[0].Answer);

                if (followUpCheckResult.Answers.Length > 0 && followUpCheckResult.Answers[0].Context.Prompts.Length > 0)
                {
                    // if follow-up check contains valid answer and at least one prompt, add prompt text to SuggestedActions using CardAction one by one
                    reply.SuggestedActions = new SuggestedActions();
                    reply.SuggestedActions.Actions = new List<CardAction>();
                    for (int i = 0; i < followUpCheckResult.Answers[0].Context.Prompts.Length; i++)
                    {
                        var promptText = followUpCheckResult.Answers[0].Context.Prompts[i].DisplayText;
                        reply.SuggestedActions.Actions.Add(new CardAction() { Title = promptText, Type = ActionTypes.ImBack, Value = promptText });
                    }
                }
                await turnContext.SendActivityAsync(reply, cancellationToken);
                if (followUpCheckResult.Answers[0].Context.Prompts.Length <= 0)
                {
                    userProfile.IsAsking = "Yes";
                    //Run the Dialog with the new message Activity.
                    await Dialog.RunAsync(turnContext, ConversationState.CreateProperty<DialogState>(nameof(DialogState)), cancellationToken);
                }
            }
            else if (turnContext.Activity.Text.Trim().ToLowerInvariant() == "log")
            {
                // Show user new user message.
                await turnContext.SendActivityAsync($"You have made {logItems.TurnNumber} entries to the TransitLink Chatbot. The list of previous entries made is now: \n\n{string.Join("\n\n", logItems.UtteranceList)}");
                // If no stored messages were found, create and store a new entry.
                userProfile.IsAsking = "No";
            }
            else
            {
                switch (turnContext.Activity.Text.Trim().ToLowerInvariant())
                {
                    case "hi":
                    case "hello":
                    case "hey":
                    case "heya":
                    case "howdy":
                    case "greetings":
                        await turnContext.SendActivityAsync(MessageFactory.Text("Hello, I am the TransitLink Chatbot! I am here to answer your questions about Auto Top-Up, Concession Cards, Standard Tickets, outstanding payment and transport services. How can I help you? "), cancellationToken);
                        break;
                    default:
                        //Run the Dialog with the new message Activity.
                        await Dialog.RunAsync(turnContext, ConversationState.CreateProperty<DialogState>(nameof(DialogState)), cancellationToken);
                        userProfile.IsAsking = "No";
                        break;

                }
            }

        }

        //protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        //{
        //    await SendIntroCardAsync(turnContext, cancellationToken);
        //    var messageTimeOffset = (DateTimeOffset)turnContext.Activity.Timestamp;
        //    string welcomeText = "";
        //    var localMessageTime = messageTimeOffset.ToLocalTime();
        //    if (localMessageTime.Hour < 12)
        //    {
        //        welcomeText = "Good morning, what is your name?";
        //    }
        //    else if (localMessageTime.Hour < 18)
        //    {
        //        welcomeText = "Good afternoon, what is your name?";
        //    }
        //    else
        //    {
        //        welcomeText = "Good evening, what is your name?";
        //    }
        //    foreach (var member in membersAdded)
        //    {
        //        if (member.Id != turnContext.Activity.Recipient.Id)
        //        {
        //            await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText, welcomeText), cancellationToken);
        //        }
        //    }
        //}

        protected override async Task OnConversationUpdateActivityAsync(ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            var messageTimeOffset = (DateTimeOffset)turnContext.Activity.Timestamp;
            string welcomeText = "";
            var localMessageTime = messageTimeOffset.ToLocalTime();
            if (localMessageTime.Hour < 12)
            {
                welcomeText = "Good morning, how can I be of your service today?";
            }
            else if (localMessageTime.Hour < 18)
            {
                welcomeText = "Good afternoon, how can I be of your service today?";
            }
            else
            {
                welcomeText = "Good evening, how can I be of your service today?";
            }
            //foreach (var member in membersAdded)
            //{
            //    if (member.Id != turnContext.Activity.Recipient.Id)
            //    {
            //        await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText, welcomeText), cancellationToken);
            //    }
            //}
            if (turnContext.Activity.MembersAdded[0].Name == turnContext.Activity.Recipient.Name)
            {
                await SendIntroCardAsync(turnContext, cancellationToken);
                await ActionCardAsync(turnContext, cancellationToken);
                await turnContext.SendActivityAsync(MessageFactory.Text("Enter 'log' to view your latest messages."), cancellationToken);
                await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText), cancellationToken);
            }
        }

        private async Task SendIntroCardAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var card = new HeroCard();
            card.Title = "Welcome to the TransitLink Chatbot!";
            card.Text = @"You may ask me about EZ-Link cards, concession cards, bus fares, our e-services and more! Please ask me anything and I will try my best to provide an answer.";
            card.Images = new List<CardImage>() { new CardImage("https://mir-s3-cdn-cf.behance.net/project_modules/disp/2cc65516711978.5603adb74ee58.jpg") };
            card.Buttons = new List<CardAction>()
            {
                new CardAction(ActionTypes.OpenUrl, "Visit our website.", "https://lh3.googleusercontent.com/proxy/04gSylUaUrUjFLGcLu1DcDEnvIP_6P98DBtTe1d9SwYCSeYzv8aEFaZz9Xs_bS7dYuq6eJfar6Hd-Knyvc5oG0DILKsrJm4RwrN35_j9_GgPxROeixzMGZ7gklcdM-OSSrzT2GPVXMBBdCDWDjFLlWo", "Visit our website.", "Visit our website.", "https://www.transitlink.com.sg/"),
            };

            var response = MessageFactory.Attachment(card.ToAttachment());
            await turnContext.SendActivityAsync(response, cancellationToken);
        }

        private async Task ActionCardAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var card = new HeroCard();
            card.Text = @"Below are some frequently asked questions: ";
            card.Buttons = new List<CardAction>()
            {
                new CardAction(ActionTypes.ImBack, "How can I top-up my card?", null, "How can I top-up my card?", "How can I top-up my card?", "How can I top-up my card?", null),
                new CardAction(ActionTypes.ImBack, "How can I apply for a concession card?", null, "How can I apply for a concession card?", "How can I apply for a concession card?", "How can I apply for a concession card?", null),
                new CardAction(ActionTypes.ImBack, "Where can I find an Add Value Machine?", null, "Where can I find an Add Value Machine?", "Where can I find an Add Value Machine?", "Where can I find an Add Value Machine?", null),
                new CardAction(ActionTypes.ImBack, "Where can I purchase the Standard Ticket?", null, "Where can I purchase the Standard Ticket?", "Where can I purchase the Standard Ticket?", "Where can I purchase the Standard Ticket?", null),
                new CardAction(ActionTypes.ImBack, "How do I pay the outstanding amount on my card?", null, "How do I pay the outstanding amount on my card?", "How do I pay the outstanding amount on my card?", "How do I pay the outstanding amount on my card?", null),
            };

            var response = MessageFactory.Attachment(card.ToAttachment());
            await turnContext.SendActivityAsync(response, cancellationToken);

            var card2 = new HeroCard();
            card2.Text = @"Alternatively, I can help you with the following actions: ";
            card2.Buttons = new List<CardAction>()
            {
                new CardAction(ActionTypes.ImBack, "Ask for directions", null, "Ask for directions", "Ask for directions", "Ask for directions", null),
                new CardAction(ActionTypes.ImBack, "Top up EZ-Link", null, "Top up EZ-Link", "Top up EZ-Link", "Top up EZ-Link", null),
                new CardAction(ActionTypes.ImBack, "Check bus arrival", null, "Check bus arrival", "Check bus arrival", "Check bus arrival", null),
                new CardAction(ActionTypes.ImBack, "Find nearest amenity", null, "Find nearest amenity", "Find nearest amenity", "Find nearest amenity", null),
                new CardAction(ActionTypes.ImBack, "Provide feedback", null, "Provide feedback", "Provide feedback", "Provide feedback", null)
            };

            var response2 = MessageFactory.Attachment(card2.ToAttachment());
            await turnContext.SendActivityAsync(response2, cancellationToken);
        }
    }
}
