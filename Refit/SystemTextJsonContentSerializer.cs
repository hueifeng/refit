﻿using System;
using System.Buffers;
using System.Diagnostics.Contracts;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Refit.Buffers;

namespace Refit
{
    /// <summary>
    /// A <see langword="class"/> implementing <see cref="IContentSerializer"/> using the System.Text.Json APIs
    /// </summary>
    public sealed class SystemTextJsonContentSerializer : IContentSerializer
    {
        /// <summary>
        /// The JSON serialization options to use
        /// </summary>
        private readonly JsonSerializerOptions jsonSerializerOptions;

        /// <summary>
        /// Creates a new <see cref="SystemTextJsonContentSerializer"/> instance
        /// </summary>
        public SystemTextJsonContentSerializer() : this(new JsonSerializerOptions()) { }

        /// <summary>
        /// Creates a new <see cref="SystemTextJsonContentSerializer"/> instance with the specified parameters
        /// </summary>
        /// <param name="jsonSerializerOptions">The serialization options to use for the current instance</param>
        public SystemTextJsonContentSerializer(JsonSerializerOptions jsonSerializerOptions)
        {
            this.jsonSerializerOptions = jsonSerializerOptions;
        }

        /// <inheritdoc/>
        public Task<HttpContent> SerializeAsync<T>(T item)
        {
            using var utf8BufferWriter = new PooledBufferWriter();

            var utf8JsonWriter = new Utf8JsonWriter(utf8BufferWriter);

            JsonSerializer.Serialize(utf8JsonWriter, item, jsonSerializerOptions);

            var content = new StreamContent(utf8BufferWriter.DetachStream())
            {
                Headers =
                {
                    ContentType = new MediaTypeHeaderValue("application/json") { CharSet = Encoding.UTF8.WebName }
                }
            };

            return Task.FromResult<HttpContent>(content);
        }

        /// <inheritdoc/>
        public async Task<T> DeserializeAsync<T>(HttpContent content)
        {
            using var stream = await content.ReadAsStreamAsync().ConfigureAwait(false);

            var buffer = ArrayPool<byte>.Shared.Rent((int)stream.Length);

            try
            {
                var length = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);

                return Deserialize<T>(buffer, length, jsonSerializerOptions);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// Deserializes an item of a specific type from a given UTF8 buffer
        /// </summary>
        /// <typeparam name="T">The type of item to deserialize</typeparam>
        /// <param name="buffer">The input buffer of UTF8 bytes to read</param>
        /// <param name="length">The length of the usable data within <paramref name="buffer"/></param>
        /// <param name="jsonSerializerOptions">The JSON serialization settings to use</param>
        /// <returns>A <typeparamref name="T"/> item deserialized from the UTF8 bytes within <paramref name="buffer"/></returns>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T Deserialize<T>(byte[] buffer, int length, JsonSerializerOptions jsonSerializerOptions)
        {
            var span = new ReadOnlySpan<byte>(buffer, 0, length);
            var utf8JsonReader = new Utf8JsonReader(span);

            return JsonSerializer.Deserialize<T>(ref utf8JsonReader, jsonSerializerOptions);
        }
    }
}
