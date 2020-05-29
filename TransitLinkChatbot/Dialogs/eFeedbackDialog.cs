using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TransitLinkChatbot.Dialogs
{
    public class eFeedbackDialog : ComponentDialog
    {
        private readonly IStatePropertyAccessor<UserProfile> _MainAccessor;
        protected readonly BotState ConversationState;
        protected readonly BotState UserState;

        public eFeedbackDialog(ConversationState conversationState, UserState userState) : base(nameof(eFeedbackDialog))
        {
            _MainAccessor = userState.CreateProperty<UserProfile>("UserProfile");
            ConversationState = conversationState;
            UserState = userState;

            // This array defines how the Waterfall will execute.
            var waterfallSteps = new WaterfallStep[]
            {
                NameStepAsync,
                FeedbackStepAsync,
                ConfirmStepAsync,
                SummaryStepAsync
            };

            // Add named dialogs to the DialogSet. These names are saved in the dialog state.
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), waterfallSteps));
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> NameStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("What's your name?") }, cancellationToken);
        }

        private async Task<DialogTurnResult> FeedbackStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["name"] = (string)stepContext.Result;
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text($"Thank you, {(string)stepContext.Values["name"]}. Please enter your feedback or enter 'cancel' to cancel.") }, cancellationToken);
        }

        private async Task<DialogTurnResult> ConfirmStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["feedback"] = (string)stepContext.Result;
            if ((string)stepContext.Result == "cancel")
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Thank you for your time!"), cancellationToken);
                return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }
            else
            {
                return await stepContext.PromptAsync(nameof(ConfirmPrompt),
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("Are you sure you would like to submit your feedback?"),
                    RetryPrompt = MessageFactory.Text("Please select 'Yes' or 'No'."),
                }, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> SummaryStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var user = await _MainAccessor.GetAsync(stepContext.Context, () => new UserProfile(), cancellationToken);
            user.Name = (string)stepContext.Values["name"];
            user.Feedback = (string)stepContext.Values["feedback"];
            if (stepContext.Context.Activity.Text == "Yes")
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Thank you for your feedback!"), cancellationToken);
                return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }
            else
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Thank you for your time!"), cancellationToken);
                return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }
        }
    }
}
