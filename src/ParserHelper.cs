using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LastPass
{
    static class ParserHelper
    {
        public class Chunk
        {
            public Chunk(string id, byte[] payload)
            {
                Id = id;
                Payload = payload;
            }

            public string Id { get; private set; }
            public byte[] Payload { get; private set; }
        }

        public static EncryptedAccount ParseAccount(Chunk chunk)
        {
            return WithBytes(chunk.Payload, reader => {
                SkipItem(reader);
                var name = ReadItem(reader);
                SkipItem(reader);
                var url = ReadItem(reader).ToUtf8().DecodeHex().ToUtf8();
                SkipItem(reader);
                SkipItem(reader);
                SkipItem(reader);
                var username = ReadItem(reader);
                var password = ReadItem(reader);

                return new EncryptedAccount(name, username, password, url);
            });
        }

        public static Dictionary<string, Chunk[]> ExtractChunks(BinaryReader reader)
        {
            var chunks = new Dictionary<string, List<Chunk>>();
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                var chunk = ReadChunk(reader);
                if (!chunks.ContainsKey(chunk.Id))
                {
                    chunks[chunk.Id] = new List<Chunk>();
                }
                chunks[chunk.Id].Add(chunk);
            }

            return chunks.ToDictionary(i => i.Key, i => i.Value.ToArray());
        }

        public static Chunk ReadChunk(BinaryReader reader)
        {
            // LastPass blob chunk is made up of 4-byte ID, big endian 4-byte size and payload of that size
            // Example:
            //   0000: 'IDID'
            //   0004: 4
            //   0008: 0xDE 0xAD 0xBE 0xEF
            //   000C: --- Next chunk ---

            return new Chunk(ReadId(reader),
                             ReadPayload(reader, ReadSize(reader)));
        }

        public static byte[] ReadItem(BinaryReader reader)
        {
            // An item in an itemized chunk is made up of the big endian size and the payload of that size
            // Example:
            //   0000: 4
            //   0004: 0xDE 0xAD 0xBE 0xEF
            //   0008: --- Next item ---

            return ReadPayload(reader, ReadSize(reader));
        }

        public static void SkipItem(BinaryReader reader)
        {
            // See ReadItem for item description.
            reader.BaseStream.Seek(ReadSize(reader), SeekOrigin.Current);
        }

        public static string ReadId(BinaryReader reader)
        {
            return reader.ReadBytes(4).ToUtf8();
        }

        public static uint ReadSize(BinaryReader reader)
        {
            return reader.ReadUInt32().FromBigEndian();
        }

        public static byte[] ReadPayload(BinaryReader reader, uint size)
        {
            return reader.ReadBytes((int)size);
        }

        public static void WithBytes(byte[] bytes, Action<BinaryReader> action)
        {
            WithBytes(bytes, (reader) => {
                action(reader);
                return 0;
            });
        }

        public static TResult WithBytes<TResult>(byte[] bytes, Func<BinaryReader, TResult> action)
        {
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream))
                return action(reader);
        }
    }
}
