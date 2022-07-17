using System.Numerics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MessageHub.Serialization;

namespace MessageHub.HomeServer.Formatting;

public record class CheckNumberRangeOperation(JsonElement Element);

public static class CanonicalJson
{
    private static readonly BigInteger minNumber = -BigInteger.Pow(2, 53) + 1;
    private static readonly BigInteger maxNumber = BigInteger.Pow(2, 53) - 1;

    private class CanonicalJsonWriter : IDisposable
    {
        private readonly MemoryStream stream;
        private readonly Utf8JsonWriter writer;

        public CanonicalJsonWriter()
        {
            stream = new MemoryStream();
            writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }

        private void WriteObject(JsonElement element, Action<JsonElement> writeElement)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject().OrderBy(property => property.Name))
            {
                writer.WritePropertyName(property.Name);
                writeElement(property.Value);
            }
            writer.WriteEndObject();
        }

        private void WriteArray(JsonElement element, Action<JsonElement> writeElement)
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray())
            {
                writeElement(item);
            }
            writer.WriteEndArray();
        }

        private void WriteNumber(JsonElement element)
        {
            if (!element.TryGetInt64(out long value) || !(minNumber <= value && value <= maxNumber))
            {
                var operation = new CheckNumberRangeOperation(element);
                throw new InvalidOperationException(operation.ToString());
            }
            writer.WriteNumberValue(value);
        }

        private void WriteElement(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                WriteObject(element, WriteElement);
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                WriteArray(element, WriteElement);
            }
            else if (element.ValueKind == JsonValueKind.Number)
            {
                WriteNumber(element);
            }
            else
            {
                element.WriteTo(writer);
            }
            writer.Flush();
        }

        public void Dispose()
        {
            writer.Dispose();
            stream.Dispose();
        }

        public static byte[] WriteBytes(JsonElement element)
        {
            using var writer = new CanonicalJsonWriter();
            writer.WriteElement(element);
            return writer.stream.ToArray();
        }

        public static string WriteJson(JsonElement element)
        {
            var bytes = WriteBytes(element);
            return Encoding.UTF8.GetString(bytes);
        }
    }

    public static string Serialize<T>(T value)
    {
        var element = DefaultJsonSerializer.SerializeToElement(value);
        return CanonicalJsonWriter.WriteJson(element);
    }

    public static byte[] SerializeToBytes<T>(T value)
    {
        var element = DefaultJsonSerializer.SerializeToElement(value);
        return CanonicalJsonWriter.WriteBytes(element);
    }
}
