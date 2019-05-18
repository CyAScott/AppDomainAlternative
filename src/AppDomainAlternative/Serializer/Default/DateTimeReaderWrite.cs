using System;
using System.IO;

namespace AppDomainAlternative.Serializer.Default
{
    internal static class DateTimeReaderWrite
    {
        public static DateTime ReadDateTime(this BinaryReader reader)
        {
            var kind = (DateTimeKind)reader.ReadByte();
            var ticks = reader.ReadInt64();
            return new DateTime(ticks, kind);
        }

        public static void Write(this BinaryWriter writer, DateTime value, bool writeHeader = true)
        {
            if (writeHeader)
            {
                writer.Write((byte)ObjectType.DateTime);
            }
            writer.Write((byte)value.Kind);
            writer.Write(value.Ticks);
        }
    }
}
