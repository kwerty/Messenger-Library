using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reactive.Disposables;

namespace MessengerShell
{
    public static class ConsoleExt
    {

        public static IDisposable WithColor(ConsoleColor color)
        {
            ConsoleColor currentColor = Console.ForegroundColor;

            Console.ForegroundColor = color;

            return Disposable.Create(() =>
            {
                Console.ForegroundColor = currentColor;
            });

        }

    }
}
