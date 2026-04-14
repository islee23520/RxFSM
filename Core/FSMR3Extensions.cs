using System;

namespace RxFSM
{
    /// <summary>
    /// Connects any IObservable&lt;TTrigger&gt; (R3, UniRx, System.Reactive, or custom)
    /// to an FSM. Uses only System.IObservable&lt;T&gt; — no external library required.
    /// </summary>
    public static class FSMR3Extensions
    {
        public static IDisposable Connect<TTrigger, TState>(
            this IObservable<TTrigger> source,
            IFSM<TState> sm)
            where TTrigger : struct
            where TState : Enum
        {
            return source.Subscribe(new FsmObserver<TTrigger, TState>(sm));
        }
    }

    internal sealed class FsmObserver<TTrigger, TState> : IObserver<TTrigger>
        where TTrigger : struct
        where TState : Enum
    {
        private readonly IFSM<TState> _sm;
        public FsmObserver(IFSM<TState> sm) { _sm = sm; }
        public void OnNext(TTrigger value)    => _sm.Trigger(value);
        public void OnError(Exception error)  { }
        public void OnCompleted()             { }
    }
}
