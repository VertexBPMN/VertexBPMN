namespace VertexBPMN.Infrastructure.Converters;


using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

public class DictionaryConverter : ValueConverter<Dictionary<string, object>, string>
{
    public DictionaryConverter()
        : base(
            dictionary => JsonSerializer.Serialize(dictionary, (JsonSerializerOptions?)null),
            json => JsonSerializer.Deserialize<Dictionary<string, object>>(json, (JsonSerializerOptions?)null)
                    ?? new Dictionary<string, object>())
    {
    }
}
