using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace RoboNav
{
    class BitmapHeader
    {
        public string Type { private set; get; }
        public int FileSize { set; get; }
        public int Reserved { set; get; }
        public int Offset { get; private set; }
        public int BitmapInfoHeaderSize { set; get; }

        public BitmapHeader()
        {
            Type = "BM";
            Reserved = 0;
            Offset = 54;
            BitmapInfoHeaderSize = 40;
        }

        public static BitmapHeader GetHeader(byte[] data)
        {
            var header = new BitmapHeader();
            using (var s = new MemoryStream(data))
            using (var reader = new BinaryReader(s))
            {
                header.Type = Encoding.ASCII.GetString(data, 0, 2);
                s.Seek(2, SeekOrigin.Begin);
                header.FileSize = reader.ReadInt32();
                header.Reserved = reader.ReadInt32();
                header.Offset = reader.ReadInt32();
                header.BitmapInfoHeaderSize = reader.ReadInt32();
            }
            return header;
        }

        public static BitmapHeader GetHeader(MemoryMappedViewAccessor Reader,out long MMPosition)
        {
            MMPosition = 0;

            var header = new BitmapHeader();
            header.Type = Encoding.ASCII.GetString(new byte[] { Reader.ReadByte(MMPosition) });
            MMPosition++;

            header.Type += Encoding.ASCII.GetString(new byte[] { Reader.ReadByte(MMPosition) });
            MMPosition++;

            header.FileSize = Reader.ReadInt32(MMPosition);
            MMPosition += 4;

            header.Reserved = Reader.ReadInt32(MMPosition);
            MMPosition += 4;

            header.Offset = Reader.ReadInt32(MMPosition);
            MMPosition += 4;

            header.BitmapInfoHeaderSize = Reader.ReadInt32(MMPosition);
            MMPosition += 4;

            return header;

        }

        public byte[] CreateBitmapHeader()
        {
            using (var s = new MemoryStream())
            using (var writer = new BinaryWriter(s))
            {
                writer.Write(Type[0]);
                writer.Write(Type[1]);
                writer.Write(FileSize);
                writer.Write(Reserved);
                writer.Write(Offset);
                writer.Write(BitmapInfoHeaderSize);
                return s.ToArray();
            }
        }
    }

    class BigBitmapHeader
    {
        public string Type { private set; get; }
        public long FileSize { set; get; }
        public int Reserved { set; get; }
        public int Offset { get; private set; }
        public int BitmapInfoHeaderSize { set; get; }

        public BigBitmapHeader()
        {
            Type = "BM";
            Reserved = 0;
            Offset = 70;
            BitmapInfoHeaderSize = 48;
        }

        public static BigBitmapHeader GetHeader(byte[] data)
        {
            var header = new BigBitmapHeader();
            using (var s = new MemoryStream(data))
            using (var reader = new BinaryReader(s))
            {
                header.Type = Encoding.ASCII.GetString(data, 0, 2);
                s.Seek(2, SeekOrigin.Begin);
                header.FileSize = reader.ReadInt64();
                header.Reserved = reader.ReadInt32();
                header.Offset = reader.ReadInt32();
                header.BitmapInfoHeaderSize = reader.ReadInt32();
            }
            return header;
        }

        public static BigBitmapHeader GetHeader(MemoryMappedViewAccessor Reader, out int MMPosition)
        {
            MMPosition = 0;

            var header = new BigBitmapHeader();
            header.Type = Encoding.ASCII.GetString(new byte[] { Reader.ReadByte(MMPosition) });
            MMPosition++;

            header.Type += Encoding.ASCII.GetString(new byte[] { Reader.ReadByte(MMPosition) });
            MMPosition++;

            header.FileSize = Reader.ReadInt64(MMPosition);
            MMPosition += 8;

            header.Reserved = Reader.ReadInt32(MMPosition);
            MMPosition += 4;

            header.Offset = Reader.ReadInt32(MMPosition);
            MMPosition += 4;

            header.BitmapInfoHeaderSize = Reader.ReadInt32(MMPosition);
            MMPosition += 4;

            return header;

        }

        public byte[] CreateBigBitmapHeader()
        {
            using (var s = new MemoryStream())
            using (var writer = new BinaryWriter(s))
            {
                writer.Write(Type[0]);
                writer.Write(Type[1]);
                writer.Write(FileSize);
                writer.Write(Reserved);
                writer.Write(Offset);
                writer.Write(BitmapInfoHeaderSize);
                return s.ToArray();
            }
        }
    }
}
