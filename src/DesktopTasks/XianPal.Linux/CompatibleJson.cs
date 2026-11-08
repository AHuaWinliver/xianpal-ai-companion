// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XianPal.Linux;

// Preserve the storage shape and untyped dictionary values used by shared services.
internal sealed class CompatibleJson
{
    public object DeserializeObject(string value) => Deserialize<object>(value);
    public int MaxJsonLength { get; set; } = 2 * 1024 * 1024;
    public int RecursionLimit { get; set; } = 64;

    private JsonSerializerOptions Options()
    {
        var options = new JsonSerializerOptions { IncludeFields = true, MaxDepth = RecursionLimit };
        options.Converters.Add(new ObjectConverter());
        return options;
    }

    public T Deserialize<T>(string text)
    {
        if (text.Length > MaxJsonLength) throw new ArgumentException("JSON 超过长度限制。");
        try { return JsonSerializer.Deserialize<T>(text, Options()); }
        catch (JsonException error) { throw new ArgumentException("JSON 无效。", error); }
    }

    public string Serialize<T>(T value)
    {
        string text = JsonSerializer.Serialize(value, Options());
        if (text.Length > MaxJsonLength) throw new InvalidOperationException("JSON 超过长度限制。");
        return text;
    }

    private sealed class ObjectConverter : JsonConverter<object>
    {
        public override object Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.StartObject => JsonSerializer.Deserialize<Dictionary<string, object>>(ref reader, options),
                JsonTokenType.StartArray => JsonSerializer.Deserialize<object[]>(ref reader, options),
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => reader.TryGetInt32(out int integer) ? (object)integer : reader.GetDouble(),
                JsonTokenType.True => true,
                JsonTokenType.False => false,
                JsonTokenType.Null => null,
                _ => throw new JsonException("Unexpected JSON value.")
            };
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            if (value.GetType() == typeof(object))
            {
                writer.WriteStartObject();
                writer.WriteEndObject();
            }
            else JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}
