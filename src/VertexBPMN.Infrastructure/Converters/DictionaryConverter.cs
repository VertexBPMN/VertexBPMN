namespace VertexBPMN.Infrastructure.Converters;


using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

public class DictionaryConverter : ValueConverter<Dictionary<string, object>, string>
{
    public DictionaryConverter()
        : base(
            d => JsonSerializer.Serialize(d, (JsonSerializerOptions)null),
            s => JsonSerializer.Deserialize<Dictionary<string, object>>(s, (JsonSerializerOptions)null))
    {
    }
}