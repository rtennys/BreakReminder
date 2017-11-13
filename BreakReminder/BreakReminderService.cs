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
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public void Run()
        {
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                var assemblyName = Assembly.GetEntryAssembly().GetName();

                Console.WriteLine();
                Console.WriteLine($"{assemblyName.Name} {assemblyName.Version}");

                Console.WriteLine();
                Console.WriteLine("<Q>uit or <P>lay configured alert sound");

                Console.WriteLine();
                Console.WriteLine("Starting...");

                Task.Factory.StartNew(ExecutionLoop, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                HandleKeyPresses();

                Console.WriteLine();
                Console.WriteLine("Stopping...");
                Console.WriteLine();
            }
            finally
            {
                var cts = _cancellationTokenSource;
                _cancellationTokenSource = null;
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
                    if (_cancellationTokenSource.IsCancellationRequested) break;

                    await Delay().ConfigureAwait(false);

                    if (_cancellationTokenSource.IsCancellationRequested) break;

                    _alertSound.Play();
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
            var topOfTheHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);
            if (now.Minute >= 55)
                topOfTheHour = topOfTheHour.AddHours(1);

            var next = now.Minute < 25 || now.Minute >= 55
                ? topOfTheHour.AddMinutes(25)
                : topOfTheHour.AddMinutes(55);

            Console.SetCursorPosition(0, Console.CursorTop - 1);
            Console.WriteLine($"Next alert: {next:hh:mm tt}");
            Console.Title = $"{next:h:mm tt}";

            await Task.Delay(next - now, _cancellationTokenSource.Token).ConfigureAwait(false);
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

                    case ConsoleKey.Escape:
                    case ConsoleKey.C:
                    case ConsoleKey.Q:
                    case ConsoleKey.X:
                        return;
                }
            }
        }
    }
}
