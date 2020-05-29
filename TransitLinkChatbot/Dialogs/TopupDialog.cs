using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TransitLinkChatbot.Dialogs
{
    public class TopupDialog : ComponentDialog
    {
        private readonly IStatePropertyAccessor<Topup> _TopupAccessor;
        protected readonly BotState ConversationState;
        protected readonly BotState UserState;
        public static string DialogType { get; set; }

        public TopupDialog(ConversationState conversationState, UserState userState)
            : base(nameof(TopupDialog))
        {
            _TopupAccessor = userState.CreateProperty<Topup>("Topup");
            ConversationState = conversationState;
            UserState = userState;

            // This array defines how the Waterfall will execute.
            var waterfallSteps = new WaterfallStep[]
            {
                NRICStepAsync,
                CardStepAsync,
                CardNumberStepAsync,
                ValueStepAsync,
                ConfirmationStepAsync,
                SummaryStepAsync
            };

            // Add named dialogs to the DialogSet. These names are saved in the dialog state.
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), waterfallSteps));
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));
            AddDialog(new NumberPrompt<long>(nameof(NumberPrompt<long>)));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private static async Task<DialogTurnResult> NRICStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("What's your NRIC number?") }, cancellationToken);
        }

        private static async Task<DialogTurnResult> CardStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["nric"] = (string)stepContext.Result;
            if (((string)stepContext.Result).Length != 9)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("The NRIC number you typed in is not valid. Please try again."), cancellationToken);
                return await stepContext.ReplaceDialogAsync(nameof(TopupDialog), null, cancellationToken);
            }
            else
            {
                return await stepContext.PromptAsync(nameof(ChoicePrompt),
                new PromptOptions
                {
                    Prompt = MessageFactory.Text($"How would you like to top-up?"),
                    RetryPrompt = MessageFactory.Text($"Please select a payment method."),
                    Choices = ChoiceFactory.ToChoices(new List<string> { "VISA", "Mastercard", "American Express", "Cancel" }),
                }, cancellationToken);
            }
        }

        private static async Task<DialogTurnResult> CardNumberStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["card"] = ((FoundChoice)stepContext.Result).Value;
            if (((FoundChoice)stepContext.Result).Value == "Cancel")
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Transaction cancelled."), cancellationToken);
                return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }
            else
            {
                return await stepContext.PromptAsync(nameof(NumberPrompt<long>), new PromptOptions {
                    Prompt = MessageFactory.Text("What's your card number?"),
                    RetryPrompt = MessageFactory.Text("Please enter a number.")
                }, cancellationToken);
            }
        }

        private static async Task<DialogTurnResult> ValueStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["cardNo"] = (long)stepContext.Result;
            return await stepContext.PromptAsync(nameof(NumberPrompt<long>), new PromptOptions {
                Prompt = MessageFactory.Text("How much would you like to top-up? (in SGD)"),
                RetryPrompt = MessageFactory.Text("Please enter a number.")
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> ConfirmationStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["value"] = (long)stepContext.Result;
            var topup = await _TopupAccessor.GetAsync(stepContext.Context, () => new Topup(), cancellationToken);
            topup.NRIC = (string)stepContext.Values["nric"];
            topup.CardType = (string)stepContext.Values["card"];
            topup.CardNo = ((long)stepContext.Values["cardNo"]).ToString();
            topup.Value = ((long)stepContext.Values["value"]).ToString();
            return await stepContext.PromptAsync(nameof(ChoicePrompt),
            new PromptOptions
            {
                Prompt = MessageFactory.Text($"NRIC: {topup.NRIC} \n\n Card No: {topup.CardNo} \n\nYou are about to add ${topup.Value} to your EZ-Link card via {topup.CardType}. Please check that the details are correct. \n\nAre you sure you would like to proceed?"),
                Choices = ChoiceFactory.ToChoices(new List<string> { "Confirm", "Cancel" }),
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> SummaryStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["confirmation"] = ((FoundChoice)stepContext.Result).Value;
            var topup = await _TopupAccessor.GetAsync(stepContext.Context, () => new Topup(), cancellationToken);
            topup.Confirmation = (string)stepContext.Values["confirmation"];
            var msg = "";

            if (topup.Confirmation == "Confirm")
            {
                msg = $"Transaction successful! ${topup.Value} has been added to your card!";
            }
            else
            {
                msg = "Transaction cancelled.";
            }

            await stepContext.Context.SendActivityAsync(MessageFactory.Text(msg), cancellationToken);
            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }
    }
}
