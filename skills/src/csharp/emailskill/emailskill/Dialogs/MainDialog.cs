﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using EmailSkill.Models;
using EmailSkill.Responses.Main;
using EmailSkill.Responses.Shared;
using EmailSkill.Services;
using Luis;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Builder.Skills;
using Microsoft.Bot.Builder.Skills.Models;
using Microsoft.Bot.Builder.Solutions;
using Microsoft.Bot.Builder.Solutions.Dialogs;
using Microsoft.Bot.Builder.Solutions.Responses;
using Microsoft.Bot.Builder.Solutions.Util;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;

namespace EmailSkill.Dialogs
{
    public class MainDialog : RouterDialog
    {
        private BotSettings _settings;
        private BotServices _services;
        private ResponseManager _responseManager;
        private UserState _userState;
        private ConversationState _conversationState;
        private IStatePropertyAccessor<EmailSkillState> _stateAccessor;

        public MainDialog(
            BotSettings settings,
            BotServices services,
            ResponseManager responseManager,
            ConversationState conversationState,
            UserState userState,
            ForwardEmailDialog forwardEmailDialog,
            SendEmailDialog sendEmailDialog,
            ShowEmailDialog showEmailDialog,
            ReplyEmailDialog replyEmailDialog,
            DeleteEmailDialog deleteEmailDialog,
            IBotTelemetryClient telemetryClient)
            : base(nameof(MainDialog), telemetryClient)
        {
            _settings = settings;
            _services = services;
            _userState = userState;
            _responseManager = responseManager;
            _conversationState = conversationState;
            TelemetryClient = telemetryClient;
            _stateAccessor = _conversationState.CreateProperty<EmailSkillState>(nameof(EmailSkillState));

            AddDialog(forwardEmailDialog ?? throw new ArgumentNullException(nameof(forwardEmailDialog)));
            AddDialog(sendEmailDialog ?? throw new ArgumentNullException(nameof(sendEmailDialog)));
            AddDialog(showEmailDialog ?? throw new ArgumentNullException(nameof(showEmailDialog)));
            AddDialog(replyEmailDialog ?? throw new ArgumentNullException(nameof(replyEmailDialog)));
            AddDialog(deleteEmailDialog ?? throw new ArgumentNullException(nameof(deleteEmailDialog)));

            GetReadingDisplayConfig();
        }

        protected override async Task OnStartAsync(DialogContext dc, CancellationToken cancellationToken = default(CancellationToken))
        {
            // send a greeting if we're in local mode
            await dc.Context.SendActivityAsync(_responseManager.GetResponse(EmailMainResponses.EmailWelcomeMessage));
        }

        protected override async Task RouteAsync(DialogContext dc, CancellationToken cancellationToken = default(CancellationToken))
        {
            var state = await _stateAccessor.GetAsync(dc.Context, () => new EmailSkillState());

            // get current activity locale
            var locale = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var localeConfig = _services.CognitiveModelSets[locale];

            await PopulateStateFromSkillContext(dc.Context);

            // If dispatch result is general luis model
            localeConfig.LuisServices.TryGetValue("email", out var luisService);

            if (luisService == null)
            {
                throw new Exception("The specified LUIS Model could not be found in your Bot Services configuration.");
            }
            else
            {
                var turnResult = EndOfTurn;
                var intent = state.LuisResult?.TopIntent().intent;
                var generalTopIntent = state.GeneralLuisResult?.TopIntent().intent;

                var skillOptions = new EmailSkillDialogOptions
                {
                    SubFlowMode = false
                };

                // switch on general intents
                switch (intent)
                {
                    case emailLuis.Intent.SendEmail:
                        {
                            turnResult = await dc.BeginDialogAsync(nameof(SendEmailDialog), skillOptions);
                            break;
                        }

                    case emailLuis.Intent.Forward:
                        {
                            turnResult = await dc.BeginDialogAsync(nameof(ForwardEmailDialog), skillOptions);
                            break;
                        }

                    case emailLuis.Intent.Reply:
                        {
                            turnResult = await dc.BeginDialogAsync(nameof(ReplyEmailDialog), skillOptions);
                            break;
                        }

                    case emailLuis.Intent.SearchMessages:
                    case emailLuis.Intent.CheckMessages:
                    case emailLuis.Intent.ReadAloud:
                    case emailLuis.Intent.QueryLastText:
                        {
                            turnResult = await dc.BeginDialogAsync(nameof(ShowEmailDialog), skillOptions);
                            break;
                        }

                    case emailLuis.Intent.Delete:
                        {
                            turnResult = await dc.BeginDialogAsync(nameof(DeleteEmailDialog), skillOptions);
                            break;
                        }

                    case emailLuis.Intent.ShowNext:
                    case emailLuis.Intent.ShowPrevious:
                    case emailLuis.Intent.None:
                        {
                            if (intent == emailLuis.Intent.ShowNext
                                || intent == emailLuis.Intent.ShowPrevious
                                || generalTopIntent == General.Intent.ShowNext
                                || generalTopIntent == General.Intent.ShowPrevious)
                            {
                                turnResult = await dc.BeginDialogAsync(nameof(ShowEmailDialog), skillOptions);
                            }
                            else
                            {
                                await dc.Context.SendActivityAsync(_responseManager.GetResponse(EmailSharedResponses.DidntUnderstandMessage));
                                turnResult = new DialogTurnResult(DialogTurnStatus.Complete);
                            }

                            break;
                        }

                    default:
                        {
                            await dc.Context.SendActivityAsync(_responseManager.GetResponse(EmailMainResponses.FeatureNotAvailable));
                            turnResult = new DialogTurnResult(DialogTurnStatus.Complete);

                            break;
                        }
                }

                if (turnResult != EndOfTurn)
                {
                    await CompleteAsync(dc);
                }
            }
        }

        private async Task PopulateStateFromSkillContext(ITurnContext context)
        {
            // If we have a SkillContext object populated from the SkillMiddleware we can retrieve requests slot (parameter) data
            // and make available in local state as appropriate.
            var accessor = _userState.CreateProperty<SkillContext>(nameof(SkillContext));
            var skillContext = await accessor.GetAsync(context, () => new SkillContext());
            if (skillContext != null)
            {
                if (skillContext.ContainsKey("timezone"))
                {
                    var timezone = skillContext["timezone"];
                    var state = await _stateAccessor.GetAsync(context, () => new EmailSkillState());
                    var timezoneJson = timezone as Newtonsoft.Json.Linq.JObject;

                    // we have a timezone
                    state.UserInfo.Timezone = timezoneJson.ToObject<TimeZoneInfo>();
                }
            }
        }

        protected override async Task CompleteAsync(DialogContext dc, DialogTurnResult result = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            // workaround. if connect skill directly to teams, the following response does not work.
            if (dc.Context.Adapter is IRemoteUserTokenProvider remoteInvocationAdapter || Channel.GetChannelId(dc.Context) != Channels.Msteams)
            {
                var response = dc.Context.Activity.CreateReply();
                response.Type = ActivityTypes.EndOfConversation;

                await dc.Context.SendActivityAsync(response);
            }

            // End active dialog
            await dc.EndDialogAsync(result);
        }

        protected override async Task OnEventAsync(DialogContext dc, CancellationToken cancellationToken = default(CancellationToken))
        {
            switch (dc.Context.Activity.Name)
            {
                case TokenEvents.TokenResponseEventName:
                    {
                        // Auth dialog completion
                        var result = await dc.ContinueDialogAsync();

                        // If the dialog completed when we sent the token, end the skill conversation
                        if (result.Status != DialogTurnStatus.Waiting)
                        {
                            var response = dc.Context.Activity.CreateReply();
                            response.Type = ActivityTypes.EndOfConversation;

                            await dc.Context.SendActivityAsync(response);
                        }

                        break;
                    }
            }
        }

        protected override async Task<InterruptionAction> OnInterruptDialogAsync(DialogContext dc, CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = InterruptionAction.NoAction;

            if (dc.Context.Activity.Type == ActivityTypes.Message)
            {
                // get current activity locale
                var locale = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                var localeConfig = _services.CognitiveModelSets[locale];

                // Update state with email luis result and entities
                var emailLuisResult = await localeConfig.LuisServices["email"].RecognizeAsync<emailLuis>(dc.Context, cancellationToken);
                var state = await _stateAccessor.GetAsync(dc.Context, () => new EmailSkillState());
                state.LuisResult = emailLuisResult;

                // check luis intent
                localeConfig.LuisServices.TryGetValue("general", out var luisService);

                if (luisService == null)
                {
                    throw new Exception("The specified LUIS Model could not be found in your Skill configuration.");
                }
                else
                {
                    var luisResult = await luisService.RecognizeAsync<General>(dc.Context, cancellationToken);
                    state.GeneralLuisResult = luisResult;
                    var topIntent = luisResult.TopIntent().intent;

                    switch (topIntent)
                    {
                        case General.Intent.Cancel:
                            {
                                result = await OnCancel(dc);
                                break;
                            }

                        case General.Intent.Help:
                            {
                                // result = await OnHelp(dc);
                                break;
                            }

                        case General.Intent.Logout:
                            {
                                result = await OnLogout(dc);
                                break;
                            }
                    }
                }
            }

            return result;
        }

        private async Task<InterruptionAction> OnCancel(DialogContext dc)
        {
            await dc.Context.SendActivityAsync(_responseManager.GetResponse(EmailMainResponses.CancelMessage));
            await CompleteAsync(dc);
            await dc.CancelAllDialogsAsync();
            return InterruptionAction.StartedDialog;
        }

        private async Task<InterruptionAction> OnHelp(DialogContext dc)
        {
            await dc.Context.SendActivityAsync(_responseManager.GetResponse(EmailMainResponses.HelpMessage));
            return InterruptionAction.MessageSentToUser;
        }

        private async Task<InterruptionAction> OnLogout(DialogContext dc)
        {
            BotFrameworkAdapter adapter;
            var supported = dc.Context.Adapter is BotFrameworkAdapter;
            if (!supported)
            {
                throw new InvalidOperationException("OAuthPrompt.SignOutUser(): not supported by the current adapter");
            }
            else
            {
                adapter = (BotFrameworkAdapter)dc.Context.Adapter;
            }

            await dc.CancelAllDialogsAsync();

            // Sign out user
            var tokens = await adapter.GetTokenStatusAsync(dc.Context, dc.Context.Activity.From.Id);
            foreach (var token in tokens)
            {
                await adapter.SignOutUserAsync(dc.Context, token.ConnectionName);
            }

            await dc.Context.SendActivityAsync(_responseManager.GetResponse(EmailMainResponses.LogOut));

            return InterruptionAction.StartedDialog;
        }

        private void GetReadingDisplayConfig()
        {
            _settings.Properties.TryGetValue("displaySize", out var maxDisplaySize);

            if (maxDisplaySize != null)
            {
                ConfigData.GetInstance().MaxDisplaySize = int.Parse(maxDisplaySize as string);
            }
        }
    }
}