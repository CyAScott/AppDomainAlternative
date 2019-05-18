using System;
using System.IO;

namespace AppDomainAlternative.Serializer.Default
{
    internal static class TimeSpanReaderWriter
    {
        public static TimeSpan ReadTimeSpan(this BinaryReader reader) => new TimeSpan(reader.ReadInt64());

        public static void Write(this BinaryWriter writer, TimeSpan value, bool writeHeader = true)
        {
            if (writeHeader)
            {
                writer.Write((byte)ObjectType.TimeSpan);
            }
            writer.Write(value.Ticks);
        }
    }
}
