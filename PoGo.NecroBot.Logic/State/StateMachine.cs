#region using directives

using System;
using System.Threading;
using System.Threading.Tasks;
using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.Tasks;
using PoGo.NecroBot.Logic.Common;
using PokemonGo.RocketAPI.Exceptions;

#endregion

namespace PoGo.NecroBot.Logic.State
{
    public class StateMachine
    {
        private IState _initialState;

        public Task AsyncStart(IState initialState, Session session)
        {
            return Task.Run(() => Start(initialState, session));
        }

        public void SetFailureState(IState state)
        {
            _initialState = state;
        }

        public async Task Start(IState initialState, Session session)
        {
            var state = initialState;

            // We need a CTS to be able to cancel task at all
            // All cancelling through the tasks originates from here
            var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;

            do
            {
                try
                {
                    state = await state.Execute(session, cancellationToken);

                    // Exit the bot if both catching and looting has reached its limits
                    if ((UseNearbyPokestopsTask.PokestopLimitReached || UseNearbyPokestopsTask.PokestopTimerReached) &&
                        (CatchPokemonTask.CatchPokemonLimitReached || CatchPokemonTask.CatchPokemonTimerReached))
                    {
                        session.EventDispatcher.Send(new ErrorEvent
                        {
                            Message = session.Translation.GetTranslation(TranslationString.ExitDueToLimitsReached)
                        });

                        cts.Cancel();

                        // A bit rough here; works but can be improved
                        Thread.Sleep(10000);
                        state = null;
                        cts.Dispose();
                        Environment.Exit(0);
                    }
                }
                catch (InvalidResponseException)
                {
                    session.EventDispatcher.Send(new ErrorEvent {Message = "Niantic Servers unstable, throttling API Calls."});
                }
                catch (OperationCanceledException)
                {
                    session.EventDispatcher.Send(new ErrorEvent {Message = "Current Operation was canceled."});
                    state = _initialState;
                }
                catch (MinimumClientVersionException ex)
                {
                    // We need to terminate the client.
                    session.EventDispatcher.Send(new ErrorEvent
                    {
                        Message = session.Translation.GetTranslation(TranslationString.MinimumClientVersionException, ex.CurrentApiVersion.ToString(), ex.MinimumClientVersion.ToString())
                    });

                    Logger.Write(session.Translation.GetTranslation(TranslationString.ExitNowAfterEnterKey, LogLevel.Error));
                    Console.ReadKey();
                    System.Environment.Exit(1);
                }
                catch (Exception ex)
                {
                    session.EventDispatcher.Send(new ErrorEvent {Message = "Pokemon Servers might be offline / unstable. Trying again..."});
                    Thread.Sleep(1000);
                    session.EventDispatcher.Send(new ErrorEvent { Message = "Error: " + ex });
                    state = _initialState;
                }
            } while (state != null);
        }
    }
}