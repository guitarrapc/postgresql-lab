using System;
using System.Collections.Generic;

namespace SampleConsole
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<IEnumerable<T>> Buffer<T>(this IEnumerable<T> source, int count)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            IEnumerable<IEnumerable<T>> BufferImpl()
            {
                using (var enumerator = source.GetEnumerator())
                {
                    var list = new List<T>(count);
                    while (enumerator.MoveNext())
                    {
                        list.Add(enumerator.Current);
                        if (list.Count == count)
                        {
                            yield return list;
                            list = new List<T>(count);
                        }
                    }
                    if (list.Count != 0)
                        yield return list;
                }
            }
            return BufferImpl();
        }
    }
}