using System;
using System.Media;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace BreakReminder
{
    public sealed class BreakReminderService : IDisposable
    {
        private readonly SoundPlayer _alertSound = new SoundPlayer(@"c:\windows\media\alarm03.wav");
        private CancellationTokenSource _mainLoopTokenSource;
        private CancellationTokenSource _delayTokenSource;
        private int? _breaksPerHour;
        private int? _breakLength;

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
                    if (_mainLoopTokenSource.IsCancellationRequested) break;

                    await Delay();

                    if (_mainLoopTokenSource.IsCancellationRequested) break;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (AggregateException ex)
            {
                ex.Handle(x => x is OperationCanceledException);
            }

            Console.SetCursorPosition(0, 5);
            Console.WriteLine("Exiting...");
        }

        private async Task Delay()
        {
            var now = DateTime.Now;
            var next = CalculateNext(now);

            Console.SetCursorPosition(0, 5);
            Console.WriteLine($"Breaks per hour: {_breaksPerHour}".PadRight(Console.WindowWidth - 2, ' '));
            Console.WriteLine();
            Console.WriteLine($"Next break: {next:hh:mm tt}");
            Console.Title = $"{next:h:mm tt}";

            _delayTokenSource = new CancellationTokenSource();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_mainLoopTokenSource.Token, _delayTokenSource.Token);

            try
            {
                await Task.Delay(next - now, linkedCts.Token);
                _alertSound.Play();
            }
            catch (OperationCanceledException)
            {
                if (_mainLoopTokenSource.IsCancellationRequested) throw;
            }
            catch (AggregateException ex)
            {
                ex.Handle(x => x is OperationCanceledException && !_mainLoopTokenSource.IsCancellationRequested);
            }

            _delayTokenSource = null;
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
            _breakLength += 5;
            if (_breakLength > 15)
                _breakLength = 0;

            Db.SetValue("BreakLength", _breakLength);
            _delayTokenSource.Cancel();
        }

        private void Switch()
        {
            _breaksPerHour = _breaksPerHour % 4 + 1;
            Db.SetValue("BreaksPerHour", _breaksPerHour);
            _delayTokenSource.Cancel();
        }

        private DateTime CalculateNext(DateTime now)
        {
            if (_breaksPerHour == null)
                _breaksPerHour = Db.GetValue("BreaksPerHour", 4);

            var minutesBetweenBreaks = (int)Math.Floor(60 / (decimal)_breaksPerHour);
            var nextBreak = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);

            while (nextBreak < now)
                nextBreak = nextBreak.AddMinutes(minutesBetweenBreaks);

            if (_breakLength == null)
                _breakLength = Db.GetValue("BreakLength", 10);

            if (_breakLength > 0)
                nextBreak = nextBreak.AddMinutes(-_breakLength.Value);

            return nextBreak;
        }
    }
}
