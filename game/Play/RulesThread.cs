using System;
using System.Collections.Concurrent;
using System.Threading;
using Godot;

namespace Game.Play
{
    // THE RULES RUN ON A THREAD OF THEIR OWN, and this is why: the player's dice are thrown on the
    // tray, and the tray is physics - it takes a second or two, on the main thread, frame by frame.
    // The rules ask for a roll in the middle of an attack and cannot carry on until the dice land.
    // So the rules run here, one job at a time, and block while the table throws; the main thread
    // keeps drawing, and answers when the felt settles.
    //
    // Everything the rules touch is touched only from this thread while a job runs. The screens read
    // the rules' state between jobs (Busy is false), never during one.
    public sealed class RulesThread : IDisposable
    {
        readonly BlockingCollection<Action> _jobs = new BlockingCollection<Action>();
        readonly Thread _thread;
        int _running;

        public RulesThread()
        {
            _thread = new Thread(Loop) { IsBackground = true, Name = "rules" };
            _thread.Start();
        }

        public bool Busy => Volatile.Read(ref _running) > 0 || _jobs.Count > 0;

        public bool OnIt => Thread.CurrentThread == _thread;

        // the last thing that went wrong in a job; the table shows it rather than freezing
        public Exception Failed { get; private set; }

        // a job, and what to do on the main thread when it is done
        public void Post(Action job, Action then = null)
        {
            Interlocked.Increment(ref _running);

            _jobs.Add(() =>
            {
                try
                {
                    job();
                }
                catch (Exception problem)
                {
                    Failed = problem;
                    MainQueue.Post(() => GD.PushError("rules: " + problem));
                }
                finally
                {
                    Interlocked.Decrement(ref _running);
                }

                if (then != null) MainQueue.Post(then);
            });
        }

        void Loop()
        {
            foreach (Action job in _jobs.GetConsumingEnumerable()) job();
        }

        public void Dispose() => _jobs.CompleteAdding();
    }

    // THE MAIN THREAD'S INBOX: what the rules thread wants done where Godot lives - throw the dice,
    // show a question, move a mini. Drained once a frame by the MainQueue node the table adds.
    public partial class MainQueue : Node
    {
        static readonly ConcurrentQueue<Action> Inbox = new ConcurrentQueue<Action>();

        static int _mainThread = -1;

        public static bool OnMain => Thread.CurrentThread.ManagedThreadId == _mainThread;

        public static void Post(Action what)
        {
            if (what != null) Inbox.Enqueue(what);
        }

        // from the rules thread: do this on the main thread, and wait for its answer
        public static T Ask<T>(Func<Action<T>, bool> start)
        {
            if (OnMain) throw new InvalidOperationException("asked the main thread from the main thread");

            T answer = default;
            using var answered = new ManualResetEventSlim(false);

            Post(() =>
            {
                bool started = start(value =>
                {
                    answer = value;
                    answered.Set();
                });

                if (!started) answered.Set();
            });

            answered.Wait();
            return answer;
        }

        public override void _EnterTree() => _mainThread = Thread.CurrentThread.ManagedThreadId;

        public override void _Process(double delta)
        {
            // a bounded number a frame, so a flood of presentation never stalls the frame
            for (int i = 0; i < 64 && Inbox.TryDequeue(out Action what); i++)
            {
                try
                {
                    what();
                }
                catch (Exception problem)
                {
                    GD.PushError("main: " + problem);
                }
            }
        }
    }
}
