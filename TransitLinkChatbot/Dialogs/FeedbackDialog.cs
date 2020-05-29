using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TransitLinkChatbot.Dialogs
{
    public class FeedbackDialog : ComponentDialog
    {
        private readonly IStatePropertyAccessor<UserProfile> _QnAAccessor;
        protected readonly BotState ConversationState;
        protected readonly BotState UserState;

        public FeedbackDialog(ConversationState conversationState, UserState userState) : base(nameof(FeedbackDialog))
        {
            _QnAAccessor = userState.CreateProperty<UserProfile>("UserProfile");
            ConversationState = conversationState;
            UserState = userState;

            // This array defines how the Waterfall will execute.
            var waterfallSteps = new WaterfallStep[]
            {
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

        private async Task<DialogTurnResult> FeedbackStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync(nameof(ConfirmPrompt),
            new PromptOptions
            {
                Prompt = MessageFactory.Text("Did my answer help you?"),
                RetryPrompt = MessageFactory.Text("Please select 'Yes' or 'No'."),
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> ConfirmStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var user = await _QnAAccessor.GetAsync(stepContext.Context, () => new UserProfile(), cancellationToken);
            if (stepContext.Context.Activity.Text == "Yes")
            {
                user.IsAsking = "No";
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("I'm glad I am able to help. Thank you for your feedback!"), cancellationToken);
                return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }
            else
            {
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("How may I improve my service?") }, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> SummaryStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var user = await _QnAAccessor.GetAsync(stepContext.Context, () => new UserProfile(), cancellationToken);
            user.IsAsking = "No";
            await stepContext.Context.SendActivityAsync(MessageFactory.Text("I appreciate your answer. Thank you for your feedback!"), cancellationToken);
            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }
    }
}
