namespace SampleUtilities.Utilities
{
    public static class ConsoleCancellationTokenSource
    {
        public static CancellationTokenSource Create()
        {
            var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            return cts;
        }
    }
}
