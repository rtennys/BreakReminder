using System;

namespace BreakReminder
{
    internal static class Program
    {
        private static void Main()
        {
            try
            {
                using (var service = new BreakReminderService())
                    service.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
