using System;
using System.Threading;

namespace RxFSM
{
    public interface IStateCtsHandle : IDisposable
    {
        CancellationToken Token { get; }
    }

    internal sealed class StateCtsHandle : IStateCtsHandle
    {
        private readonly CancellationTokenSource _cts = new();
        private IDisposable _subscription;
        private bool _disposed;

        public CancellationToken Token => _cts.Token;

        internal void SetSubscription(IDisposable sub)
        {
            if (_disposed) { sub?.Dispose(); return; }
            _subscription = sub;
        }

        internal void Fire()
        {
            if (_disposed) return;
            try { _cts.Cancel(); } catch (ObjectDisposedException) { }
            _subscription?.Dispose();
            _subscription = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _subscription?.Dispose();
            _subscription = null;
            try { _cts.Cancel(); } catch (ObjectDisposedException) { }
            _cts.Dispose();
        }
    }

    public static class FSMCtsExtensions
    {
        /// <summary>
        /// 다음번 <paramref name="state"/> 진입 시 토큰이 cancel되는 1회용 핸들.
        /// 사용자가 먼저 Dispose해도 안전하며, Cancel 후에도 핸들 Dispose는 필요합니다 (using 권장).
        /// </summary>
        public static IStateCtsHandle EnterStateCts<TState>(this FSM<TState> fsm, TState state)
            where TState : Enum
        {
            if (fsm == null) throw new ArgumentNullException(nameof(fsm));
            var handle = new StateCtsHandle();
            var sub = fsm.EnterState(state, (_, _) => handle.Fire());
            handle.SetSubscription(sub);
            return handle;
        }

        /// <summary>
        /// 다음번 <paramref name="state"/> 퇴장 시 토큰이 cancel되는 1회용 핸들.
        /// 사용자가 먼저 Dispose해도 안전하며, Cancel 후에도 핸들 Dispose는 필요합니다 (using 권장).
        /// </summary>
        public static IStateCtsHandle ExitStateCts<TState>(this FSM<TState> fsm, TState state)
            where TState : Enum
        {
            if (fsm == null) throw new ArgumentNullException(nameof(fsm));
            var handle = new StateCtsHandle();
            var sub = fsm.ExitState(state, (_, _) => handle.Fire());
            handle.SetSubscription(sub);
            return handle;
        }
    }
}
