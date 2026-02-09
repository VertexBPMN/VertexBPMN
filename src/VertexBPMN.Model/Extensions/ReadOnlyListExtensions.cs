using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VertexBPMN.Domain.Model.Extensions;

public static class ReadOnlyListExtensions
{
    public static IReadOnlyList<T> Add<T>(this IReadOnlyList<T>? source, T item)
    {
        if (source is null or { Count: 0 })
            return new List<T> { item };

        // Avoid mutating original collection
        if (source is List<T> list)
        {
            var copy = new List<T>(list.Count + 1);
            copy.AddRange(list);
            copy.Add(item);
            return copy;
        }

        var result = new List<T>(source.Count + 1);
        foreach (var x in source) result.Add(x);
        result.Add(item);
        return result;
    }

    public static IReadOnlyList<T> AddRange<T>(this IReadOnlyList<T>? source, params T[] items)
    {
        if (items is null || items.Length == 0) return source ?? Array.Empty<T>();

        if (source is null or { Count: 0 })
            return new List<T>(items);

        var result = new List<T>(source.Count + items.Length);
        foreach (var x in source) result.Add(x);
        result.AddRange(items);
        return result;
    }
}