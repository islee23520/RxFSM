using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace RxFSM
{
    public static class FSMUniTaskExtensions
    {
        // ── Await a specific state ───────────────────────────────────────────────

        public static UniTask ToUniTask<TState>(
            this IFSM<TState> sm,
            TState targetState,
            CancellationToken ct = default)
            where TState : Enum
        {
            if (ct.IsCancellationRequested)
                return UniTask.FromCanceled(ct);

            var tcs = new UniTaskCompletionSource();
            IDisposable enterHandle = null;
            CancellationTokenRegistration ctReg = default;

            void Cleanup()
            {
                ctReg.Dispose();
                enterHandle?.Dispose();
                sm.OnDisposed -= OnFSMDisposed;
            }

            void OnFSMDisposed()
            {
                Cleanup();
                tcs.TrySetCanceled();
            }

            enterHandle = sm.EnterState(targetState, (prev, trg) =>
            {
                Cleanup();
                tcs.TrySetResult();
            });

            sm.OnDisposed += OnFSMDisposed;

            if (ct.CanBeCanceled)
                ctReg = ct.Register(() =>
                {
                    Cleanup();
                    tcs.TrySetCanceled(ct);
                });

            return tcs.Task;
        }

        // ── Await any state matching a predicate ────────────────────────────────

        public static UniTask ToUniTask<TState>(
            this IFSM<TState> sm,
            Func<(TState Current, object Trigger), bool> predicate,
            CancellationToken ct = default)
            where TState : Enum
        {
            if (ct.IsCancellationRequested)
                return UniTask.FromCanceled(ct);

            var tcs = new UniTaskCompletionSource();
            IDisposable enterHandle = null;
            CancellationTokenRegistration ctReg = default;

            void Cleanup()
            {
                ctReg.Dispose();
                enterHandle?.Dispose();
                sm.OnDisposed -= OnFSMDisposed;
            }

            void OnFSMDisposed()
            {
                Cleanup();
                tcs.TrySetCanceled();
            }

            enterHandle = sm.EnterState((cur, prev, trg) =>
            {
                if (!predicate((cur, trg))) return;
                Cleanup();
                tcs.TrySetResult();
            });

            sm.OnDisposed += OnFSMDisposed;

            if (ct.CanBeCanceled)
                ctReg = ct.Register(() =>
                {
                    Cleanup();
                    tcs.TrySetCanceled(ct);
                });

            return tcs.Task;
        }
    }
}
