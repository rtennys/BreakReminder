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
        private Func<DateTime, DateTime> _calculateNext = CalculateNextForThreeTimesHourly;

        public void Run()
        {
            _mainLoopTokenSource = new CancellationTokenSource();

            try
            {
                var assemblyName = Assembly.GetEntryAssembly().GetName();

                Console.WriteLine();
                Console.WriteLine($"{assemblyName.Name} {assemblyName.Version}");

                Console.WriteLine();
                Console.WriteLine("<Q>uit, <P>lay configured alert sound, or <S>witch break frequency");

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

                    await Delay().ConfigureAwait(false);

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
        }

        private async Task Delay()
        {
            var now = DateTime.Now;
            var next = _calculateNext(now);

            Console.SetCursorPosition(0, 5);
            Console.WriteLine($"Reminding {(_calculateNext == CalculateNextForTwiceHourly ? "twice" : "three times")} an hour".PadRight(Console.WindowWidth - 2, ' '));
            Console.WriteLine();
            Console.WriteLine($"Next alert: {next:hh:mm tt}");
            Console.Title = $"{next:h:mm tt}";

            using (_delayTokenSource = new CancellationTokenSource())
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_mainLoopTokenSource.Token, _delayTokenSource.Token))
            {
                try
                {
                    await Task.Delay(next - now, linkedCts.Token).ConfigureAwait(false);
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
            }

            _delayTokenSource = null;
        }

        private void HandleKeyPresses()
        {
            while (true)
            {
                switch (Console.ReadKey(true).Key)
                {
                    case ConsoleKey.A:
                        SystemSounds.Asterisk.Play();
                        break;

                    case ConsoleKey.B:
                        SystemSounds.Beep.Play();
                        break;

                    case ConsoleKey.E:
                        SystemSounds.Exclamation.Play();
                        break;

                    case ConsoleKey.H:
                        SystemSounds.Hand.Play();
                        break;

                    case ConsoleKey.P:
                        _alertSound.Play();
                        break;

                    case ConsoleKey.S:
                        Switch();
                        break;

                    case ConsoleKey.Escape:
                    case ConsoleKey.C:
                    case ConsoleKey.Q:
                    case ConsoleKey.X:
                        return;
                }
            }
        }

        private void Switch()
        {
            if (_calculateNext == CalculateNextForTwiceHourly)
                _calculateNext = CalculateNextForThreeTimesHourly;
            else
                _calculateNext = CalculateNextForTwiceHourly;

            _delayTokenSource?.Cancel();
        }

        private static DateTime CalculateNextForTwiceHourly(DateTime now)
        {
            var topOfTheHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);

            if (now.Minute >= 55)
                topOfTheHour = topOfTheHour.AddHours(1);

            var next = now.Minute < 25 || now.Minute >= 55
                ? topOfTheHour.AddMinutes(25)
                : topOfTheHour.AddMinutes(55);

            return next;
        }

        private static DateTime CalculateNextForThreeTimesHourly(DateTime now)
        {
            var topOfTheHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);

            if (now.Minute >= 55)
                topOfTheHour = topOfTheHour.AddHours(1);

            if (now.Minute < 15 || now.Minute >= 55)
                return topOfTheHour.AddMinutes(15);

            if (now.Minute < 35)
                return topOfTheHour.AddMinutes(35);

            return topOfTheHour.AddMinutes(55);
        }
    }
}
