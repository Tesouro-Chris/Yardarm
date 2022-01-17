using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace RootNamespace.Serialization.Json
{
    public class JsonTypeSerializer : ITypeSerializer
    {
        public static string[] SupportedMediaTypes => new [] { "application/json" };

        public HttpContent Serialize<T>(T value, string mediaType, ISerializationData? serializationData = null)
        {
            var typeInfo = (JsonTypeInfo<T>?) ModelSerializerContext.Default.GetTypeInfo(typeof(T));
            if (typeInfo == null)
            {
                throw new InvalidOperationException($"Metadata for type '{typeof(T).FullName}' was not provided to the serializer.");
            }

            return new JsonContent<T>(value, typeInfo);
        }

        public async ValueTask<T> DeserializeAsync<T>(HttpContent content, ISerializationData? serializationData = null) =>
            (T)(await content.ReadFromJsonAsync(typeof(T), ModelSerializerContext.Default).ConfigureAwait(false))!;
    }
}
