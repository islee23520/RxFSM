using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RxFSM
{
    public class Phase6Tester : MonoBehaviour
    {
        // ── Enums / Triggers ─────────────────────────────────────────────────────
        enum TestState { Idle, Walk, Run, Hit }
        readonly struct MoveStarted { }
        readonly struct Damaged     { public readonly float amount; public Damaged(float a) { amount = a; } }

        // ── Counters ─────────────────────────────────────────────────────────────
        int _pass, _fail;
        void Assert(bool c, string label)
        {
            if (c) { Debug.Log($"[PASS] {label}"); _pass++; }
            else   { Debug.LogError($"[FAIL] {label}"); _fail++; }
        }

        // ── Entry ─────────────────────────────────────────────────────────────────
        void Start() => StartCoroutine(RunAll());

        IEnumerator RunAll()
        {
            yield return T6_1_ToUniTaskAwaitsState().ToCoroutine();
            yield return T6_2_ToUniTaskCancellation().ToCoroutine();
            yield return T6_3_R3Connect();
            yield return T6_4_Regression().ToCoroutine();
            PrintFinal();
        }

        void PrintFinal()
        {
            int total = _pass + _fail;
            if (_fail == 0) Debug.Log($"=== Phase 6: {_pass}/{total} passed ===");
            else            Debug.LogError($"=== Phase 6: {_pass}/{total} passed, {_fail} FAILED ===");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // T6.1 — ToUniTask awaits state
        // ─────────────────────────────────────────────────────────────────────────

        async UniTask T6_1_ToUniTaskAwaitsState()
        {
            var sm = FSM.Create<TestState>(TestState.Idle)
                .AddTransition<MoveStarted>(TestState.Idle, TestState.Walk)
                .Build();

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            TriggerAfterDelay(sm, cts.Token).Forget();

            await sm.ToUniTask(TestState.Walk, cts.Token);
            Assert(sm.State == TestState.Walk, "T6.1 — ToUniTask resolves when target state entered");
            sm.Dispose();
        }

        async UniTask TriggerAfterDelay(FSM<TestState> sm, CancellationToken ct)
        {
            await UniTask.Delay(100, cancellationToken: ct);
            sm.Trigger(new MoveStarted());
        }

        // ─────────────────────────────────────────────────────────────────────────
        // T6.2 — ToUniTask cancellation
        // ─────────────────────────────────────────────────────────────────────────

        async UniTask T6_2_ToUniTaskCancellation()
        {
            var sm = FSM.Create<TestState>(TestState.Idle).Build();
            var cts = new CancellationTokenSource();

            var task = sm.ToUniTask(TestState.Walk, cts.Token);
            cts.Cancel();

            bool wasCancelled = false;
            try   { await task; }
            catch (OperationCanceledException) { wasCancelled = true; }

            Assert(wasCancelled, "T6.2 — ToUniTask throws OperationCanceledException on cancel");
            sm.Dispose();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // T6.3 — Observable.Connect
        // ─────────────────────────────────────────────────────────────────────────

        IEnumerator T6_3_R3Connect()
        {
            var sm = FSM.Create<TestState>(TestState.Idle)
                .AddTransition<Damaged>(TestState.Idle, TestState.Hit)
                .Build();

            var subject = new Subject<Damaged>();
            var sub = subject.Connect(sm);

            subject.OnNext(new Damaged(10f));
            Assert(sm.State == TestState.Hit, "T6.3 — IObservable.Connect routes OnNext to FSM");

            sub.Dispose();
            sm.Dispose();
            yield return null;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // T6.4 — Regression
        // ─────────────────────────────────────────────────────────────────────────

        async UniTask T6_4_Regression()
        {
            // Phase 1 basic
            {
                var sm = FSM.Create<TestState>(TestState.Idle)
                    .AddTransition<MoveStarted>(TestState.Idle, TestState.Walk)
                    .Build();
                sm.Trigger(new MoveStarted());
                Assert(sm.State == TestState.Walk, "T6.4/T1 — basic transition");
                sm.Dispose();
            }

            // Phase 5 — IFSM<TState>
            {
                var sm = FSM.Create<TestState>(TestState.Idle)
                    .AddTransition<MoveStarted>(TestState.Idle, TestState.Walk)
                    .Build();
                IFSM<TestState> ifsm = sm;
                bool entered = false;
                ifsm.EnterState((cur, prev) => entered = true);
                ifsm.Trigger(new MoveStarted());
                Assert(entered, "T6.4/T5 — IFSM<TState> exposes EnterState and Trigger");
                ifsm.Dispose();
            }

            // ToUniTask predicate overload
            {
                var sm = FSM.Create<TestState>(TestState.Idle)
                    .AddTransition<MoveStarted>(TestState.Idle,     TestState.Walk)
                    .AddTransition<MoveStarted>(TestState.Walk,     TestState.Run)
                    .Build();

                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                TriggerTwice(sm, cts.Token).Forget();

                await sm.ToUniTask(t => t.Current.Equals(TestState.Run), cts.Token);
                Assert(sm.State == TestState.Run, "T6.4/T6 — ToUniTask predicate overload resolves on Run");
                sm.Dispose();
            }
        }

        async UniTask TriggerTwice(FSM<TestState> sm, CancellationToken ct)
        {
            await UniTask.Delay(50, cancellationToken: ct); sm.Trigger(new MoveStarted()); // →Walk
            await UniTask.Delay(50, cancellationToken: ct); sm.Trigger(new MoveStarted()); // →Run
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Minimal Subject<T> — only System.IObservable<T>, no external library
        // ─────────────────────────────────────────────────────────────────────────

        sealed class Subject<T> : IObservable<T>
        {
            readonly List<IObserver<T>> _observers = new List<IObserver<T>>();

            public IDisposable Subscribe(IObserver<T> observer)
            {
                _observers.Add(observer);
                return Disposable.Create(() => _observers.Remove(observer));
            }

            public void OnNext(T value)
            {
                foreach (var o in _observers.ToArray()) o.OnNext(value);
            }
        }
    }
}
