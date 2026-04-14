using System.Collections;
using UnityEngine;
using R3;

namespace RxFSM
{
    public class FSMR3ExtensionTests : MonoBehaviour
    {
        enum S { Idle, Walk, Hit, Dead }
        readonly struct MoveStarted { }
        readonly struct Damaged     { public readonly float amount; public Damaged(float a) { amount = a; } }
        readonly struct Killed      { }

        int _pass, _fail;
        void Assert(bool c, string label)
        {
            if (c) { Debug.Log($"[PASS] {label}"); _pass++; }
            else   { Debug.LogError($"[FAIL] {label}"); _fail++; }
        }

        void Start() => StartCoroutine(RunAll());

        IEnumerator RunAll()
        {
            yield return T6_3_Connect_RoutesOnNext();
            yield return T6_3b_Connect_Dispose_StopsRouting();
            yield return T6_3c_Connect_WrongTriggerType_IsNoOp();
            yield return T6_3d_MultipleObservers_SameFSM();
            yield return T6_3e_Connect_FSMDisposed_NoException();
            yield return T6_3f_Subject_CompleteDisposesConnection();
            PrintFinal();
        }

        void PrintFinal()
        {
            int total = _pass + _fail;
            if (_fail == 0) Debug.Log($"=== FSMR3ExtensionTests: {_pass}/{total} passed ===");
            else            Debug.LogError($"=== FSMR3ExtensionTests: {_pass}/{total} passed, {_fail} FAILED ===");
        }

        // ── T6.3 — IObservable.Connect routes OnNext to FSM ──────────────────────

        IEnumerator T6_3_Connect_RoutesOnNext()
        {
            var sm = FSM.Create<S>(S.Idle)
                .AddTransition<Damaged>(S.Idle, S.Hit)
                .Build();

            var subject = new Subject<Damaged>();
            var sub = subject.Connect(sm);

            subject.OnNext(new Damaged(10f));
            Assert(sm.State == S.Hit, "T6.3 — IObservable.Connect routes OnNext to FSM.Evaluate");

            sub.Dispose();
            sm.Dispose();
            yield return null;
        }

        // ── T6.3b — Connect dispose stops routing ─────────────────────────────────

        IEnumerator T6_3b_Connect_Dispose_StopsRouting()
        {
            var sm = FSM.Create<S>(S.Idle)
                .AddTransition<Damaged>(S.Idle, S.Hit)
                .Build();

            var subject = new Subject<Damaged>();
            var sub = subject.Connect(sm);

            sub.Dispose(); // unsubscribe before OnNext
            subject.OnNext(new Damaged(10f));
            Assert(sm.State == S.Idle, "T6.3b — after Connect dispose, OnNext is no-op");

            sm.Dispose();
            yield return null;
        }

        // ── T6.3c — Emitting wrong trigger type does not transition ───────────────

        IEnumerator T6_3c_Connect_WrongTriggerType_IsNoOp()
        {
            var sm = FSM.Create<S>(S.Idle)
                .AddTransition<MoveStarted>(S.Idle, S.Walk)
                .Build();

            // Connect a Damaged observable — no Damaged transition from Idle
            var subject = new Subject<Damaged>();
            using var sub = subject.Connect(sm);

            subject.OnNext(new Damaged(5f)); // no matching rule
            Assert(sm.State == S.Idle, "T6.3c — trigger with no matching rule is no-op");

            sm.Dispose();
            yield return null;
        }

        // ── T6.3d — Multiple observers connected to same FSM ─────────────────────

        IEnumerator T6_3d_MultipleObservers_SameFSM()
        {
            var sm = FSM.Create<S>(S.Idle)
                .AddTransition<Damaged>    (S.Idle, S.Hit)
                .AddTransition<MoveStarted>(S.Idle, S.Walk)
                .Build();

            var damagedSubject = new Subject<Damaged>();
            var moveSubject    = new Subject<MoveStarted>();

            using var sub1 = damagedSubject.Connect(sm);
            using var sub2 = moveSubject.Connect(sm);

            damagedSubject.OnNext(new Damaged(10f));
            Assert(sm.State == S.Hit, "T6.3d-a — first observable transitions to Hit");

            sm.ForceTransitionTo(S.Idle);
            moveSubject.OnNext(new MoveStarted());
            Assert(sm.State == S.Walk, "T6.3d-b — second observable transitions to Walk");

            sm.Dispose();
            yield return null;
        }

        // ── T6.3e — OnNext after FSM disposed: no exception ──────────────────────

        IEnumerator T6_3e_Connect_FSMDisposed_NoException()
        {
            var sm = FSM.Create<S>(S.Idle)
                .AddTransition<Damaged>(S.Idle, S.Hit)
                .Build();

            var subject = new Subject<Damaged>();
            using var sub = subject.Connect(sm);

            sm.Dispose();

            bool threw = false;
            try { subject.OnNext(new Damaged(10f)); }
            catch { threw = true; }

            Assert(!threw, "T6.3e — OnNext after FSM disposed does not throw");
            yield return null;
        }

        // ── T6.3f — Subject.OnCompleted disposes connection ──────────────────────

        IEnumerator T6_3f_Subject_CompleteDisposesConnection()
        {
            var sm = FSM.Create<S>(S.Idle)
                .AddTransition<Damaged>(S.Idle, S.Hit)
                .Build();

            var subject = new Subject<Damaged>();
            var sub = subject.Connect(sm);

            subject.OnCompleted(); // complete the source

            // After source completes, further emission should be no-op
            subject.OnNext(new Damaged(10f));
            Assert(sm.State == S.Idle, "T6.3f — after Subject.OnCompleted, OnNext is no-op");

            sub.Dispose();
            sm.Dispose();
            yield return null;
        }
    }
}
