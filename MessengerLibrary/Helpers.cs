using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reactive.Subjects;
using System.Reactive;
using System.Reactive.Linq;

public static class ObservableExtensions
{

    public static IObservable<TSource> TakeUntil<TSource>(
            this IObservable<TSource> source, Func<TSource, bool> predicate)
    {
        return Observable.Create<TSource>(o => source.Subscribe(x =>
            {
                o.OnNext(x);
                if (predicate(x))
                    o.OnCompleted();
            },
            o.OnError,
            o.OnCompleted
        ));
    }

}

public static class StringExtensions
{

    public static string[] Split(this string source, string separator)
    {
        return source.Split(new string[] { separator }, StringSplitOptions.None);
    }

}

public static class ByteExt
{

    public static byte[] Concat(byte[] arg1, byte[] arg2)
    {

        byte[] result = new byte[arg1.Length + arg2.Length];

        Buffer.BlockCopy(arg1, 0, result, 0, arg1.Length);
        Buffer.BlockCopy(arg2, 0, result, arg1.Length, arg2.Length);

        return result;

    }

    public static bool Split(byte[] src, byte[] separator, out byte[] out1, out byte[] out2)
    {

        out1 = new byte[0];
        out2 = new byte[0];

        int i = IndexOf(src, separator);

        if (i == -1)
            return false;

        out1 = new byte[i];
        out2 = new byte[src.Length - (i + separator.Length)];

        Buffer.BlockCopy(src, 0, out1, 0, i);
        Buffer.BlockCopy(src, i + separator.Length, out2, 0, src.Length - (i + separator.Length));

        return true;

    }

    public static int IndexOf(byte[] src, byte[] value)
    {

        for (var i = 0; i < src.Length; ++i)
            if (Compare(src, i, value, 0, value.Length))
                return i;

        return -1;
    }

    public static bool Compare(byte[] arg1, int offset1, byte[] arg2, int offset2, int count)
    {

        for (int i = 0; i < count; ++i)
            if (offset1 + i >= arg1.Length || offset2 + i >= arg2.Length || arg1[offset1 + i] != arg2[offset2 + i])
                return false;

        return true;
    }

}

