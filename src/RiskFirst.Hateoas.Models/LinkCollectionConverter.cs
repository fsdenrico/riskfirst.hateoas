using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RiskFirst.Hateoas.Models
{
    public class LinkCollectionConverter : JsonConverter<LinkCollection>
    {
        public override LinkCollection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var links = JsonSerializer.Deserialize<Dictionary<string, Link>>(ref reader, options);

            var existingValue = new LinkCollection();

            foreach (var link in links)
            {
                link.Value.Name = link.Key;
                existingValue.Add(link.Value);
            }

            return existingValue;
        }

        public override void Write(Utf8JsonWriter writer, LinkCollection value, JsonSerializerOptions options)
        {
            var links = value?.ToDictionary(x => x.Name, x => x);

            JsonSerializer.Serialize(writer, links);
        }
    }
}
