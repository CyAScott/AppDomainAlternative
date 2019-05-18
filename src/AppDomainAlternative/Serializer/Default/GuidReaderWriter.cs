using System;
using System.IO;
using System.Threading.Tasks;

namespace AppDomainAlternative.Serializer.Default
{
    internal static class GuidReaderWriter
    {
        public static async Task<Guid> ReadGuid(this BinaryReader reader)
        {
            var guid = new byte[16];
            var guidIndex = 0;
            while (guidIndex < guid.Length)
            {
                guidIndex += await reader.BaseStream.ReadAsync(guid, guidIndex, guid.Length - guidIndex).ConfigureAwait(false);
            }
            return new Guid(guid);
        }

        public static void Write(this BinaryWriter writer, Guid value, bool writeHeader = true)
        {
            if (writeHeader)
            {
                writer.Write((byte)ObjectType.Guid);
            }
            writer.BaseStream.Write(value.ToByteArray(), 0, 16);
        }
    }
}
