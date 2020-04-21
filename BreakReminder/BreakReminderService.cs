using System;
using System.Media;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace BreakReminder
{
    public sealed class BreakReminderService : IDisposable
    {
        public BreakReminderService()
        {
            _breaksPerHour = new Lazy<int>(() => Db.GetValue("BreaksPerHour", 1));
            _breakLength = new Lazy<int>(() => Db.GetValue("BreakLength", 10));
        }

        private readonly SoundPlayer _alertSound = new SoundPlayer(@"c:\windows\media\alarm03.wav");
        private CancellationTokenSource _mainLoopTokenSource;
        private CancellationTokenSource _delayTokenSource;
        private Lazy<int> _breaksPerHour;
        private Lazy<int> _breakLength;

        public void Run()
        {
            _mainLoopTokenSource = new CancellationTokenSource();

            try
            {
                var assemblyName = Assembly.GetEntryAssembly()?.GetName() ?? throw new Exception("Assembly.GetEntryAssembly returned null.  I didn't even know that was possible!");

                Console.WriteLine();
                Console.WriteLine($"{assemblyName.Name} {assemblyName.Version}");

                Console.WriteLine();
                Console.WriteLine("<Q>uit, <P>lay configured alert sound, <S>witch break frequency, or <C>hange break length");

                Console.WriteLine();
                Console.WriteLine("Starting...");

                Task.Factory.StartNew(ExecutionLoop, _mainLoopTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                HandleKeyPresses();

                Console.WriteLine();
                Console.WriteLine("Stopping...");
                Console.WriteLine();
            }
            finally
            {
                var cts = _mainLoopTokenSource;
                _mainLoopTokenSource = null;
                using (cts)
                    cts.Cancel();
            }
        }

        public void Dispose()
        {
            _alertSound.Dispose();
        }

        private async Task ExecutionLoop()
        {
            try
            {
                while (true)
                {
                    _delayTokenSource = new CancellationTokenSource();
                    System.Diagnostics.Debug.WriteLine($"IsCancellationRequested: {_delayTokenSource.Token.IsCancellationRequested}");
                    await Delay(_delayTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (AggregateException ex)
            {
                ex.Handle(x => x is OperationCanceledException);
            }
        }

        private async Task Delay(CancellationToken token)
        {
            var now = DateTime.Now;
            var next = CalculateNext(now);

            Console.SetCursorPosition(0, 5);
            Console.WriteLine($"Breaks per hour: {_breaksPerHour}, break length: {_breakLength}".PadRight(Console.WindowWidth - 2, ' '));
            Console.WriteLine();
            Console.WriteLine($"Next break: {next:hh:mm tt}");
            Console.Title = $"{next:h:mm tt}";

            try
            {
                await Task.Delay(next - now, token);
                _alertSound.Play();
            }
            catch (OperationCanceledException)
            {
            }
            catch (AggregateException ex)
            {
                ex.Handle(x => x is OperationCanceledException);
            }
        }

        private void HandleKeyPresses()
        {
            while (true)
            {
                switch (Console.ReadKey(true).Key)
                {
                    case ConsoleKey.P:
                        _alertSound.Play();
                        break;

                    case ConsoleKey.S:
                        Switch();
                        break;

                    case ConsoleKey.C:
                        Change();
                        break;

                    case ConsoleKey.Escape:
                    case ConsoleKey.Q:
                    case ConsoleKey.X:
                        return;
                }
            }
        }

        private void Change()
        {
            var newBreakLength = (_breakLength.Value / 5 + 1) % 3 * 5; // 0, 5, or 10 minute breaks
            _breakLength = new Lazy<int>(() => newBreakLength);

            Db.SetValue("BreakLength", _breakLength.Value);
            _delayTokenSource.Cancel();
        }

        private void Switch()
        {
            var newBreaksPerHour = _breaksPerHour.Value % 4 + 1;
            _breaksPerHour = new Lazy<int>(() => newBreaksPerHour);

            Db.SetValue("BreaksPerHour", _breaksPerHour.Value);
            _delayTokenSource.Cancel();
        }

        private DateTime CalculateNext(DateTime now)
        {
            var nextBreak = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);

            if (_breakLength.Value > 0)
                nextBreak = nextBreak.AddMinutes(-_breakLength.Value);

            var minutesBetweenBreaks = (int)Math.Floor(60 / (decimal)_breaksPerHour.Value);
            while (nextBreak < now)
                nextBreak = nextBreak.AddMinutes(minutesBetweenBreaks);

            return nextBreak;
        }
    }
}
