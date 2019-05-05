using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace RoboNav
{
    class BitmapInfoHeader
    {
        public int BitmapWidth { get; set; }
        public int BitmapHeight { get; set; }
        public short ColorPlanes { set; get; }
        public short BitsPerPixel { set; get; }
        public int CompressinoMethod { set; get; }
        public int ColorDataSize { set; get; }
        public int HorizantalResolution { set; get; }
        public int VerticalResolution { set; get; }
        public int Colors { set; get; }
        public int IColors { set; get; }

        public BitmapInfoHeader()
        {
            CompressinoMethod = 0;
            ColorDataSize = 0;
            ColorPlanes = 1;
        }

        public byte[] CreateInfoHeaderData(int ColorDataSize)
        {
            this.ColorDataSize = ColorDataSize;
            using (var s = new MemoryStream())
            using (var writer = new BinaryWriter(s))
            {
                //writer.Write(HeaderSize);
                writer.Write(BitmapWidth);
                writer.Write(BitmapHeight);
                writer.Write(ColorPlanes);
                writer.Write(BitsPerPixel);
                writer.Write(CompressinoMethod);
                writer.Write(ColorDataSize);
                writer.Write(HorizantalResolution);
                writer.Write(VerticalResolution);
                writer.Write(Colors);
                writer.Write(IColors);
                return s.ToArray();
            }
        }

        public static BitmapInfoHeader GetInfoHeader(byte[] data)
        {
            using (var s = new MemoryStream(data))
            using (var reader = new BinaryReader(s))
            {
                var header = new BitmapInfoHeader();

                // HeaderSize = reader.ReadInt32(),
                header.BitmapWidth = reader.ReadInt32();
                header.BitmapHeight = reader.ReadInt32();
                header.ColorPlanes = reader.ReadInt16();
                header.BitsPerPixel = reader.ReadInt16();
                header.CompressinoMethod = reader.ReadInt32();
                header.ColorDataSize = reader.ReadInt32();
                header.HorizantalResolution = reader.ReadInt32();
                header.VerticalResolution = reader.ReadInt32();
                header.Colors = reader.ReadInt32();
                header.IColors = reader.ReadInt32();
                return header;
            }
        }

        public static BitmapInfoHeader GetInfoHeader(MemoryMappedViewAccessor Reader, out long MMPosition)
        {
            MMPosition = 18;
            var header = new BitmapInfoHeader();

            // HeaderSize = reader.ReadInt32(),
            header.BitmapWidth = Reader.ReadInt32(MMPosition);
            MMPosition += 4;

            header.BitmapHeight = Reader.ReadInt32(MMPosition);
            MMPosition += 4;

            header.ColorPlanes = Reader.ReadInt16(MMPosition);
            MMPosition += 2;

            header.BitsPerPixel = Reader.ReadInt16(MMPosition);
            MMPosition += 2;

            header.CompressinoMethod = Reader.ReadInt32(MMPosition);
            MMPosition += 4;

            header.ColorDataSize = Reader.ReadInt32(MMPosition);
            MMPosition += 4;

            header.HorizantalResolution = Reader.ReadInt32(MMPosition);
            MMPosition += 4;

            header.VerticalResolution = Reader.ReadInt32(MMPosition);
            MMPosition += 4;

            header.Colors = Reader.ReadInt32(MMPosition);
            MMPosition += 4;

            header.IColors = Reader.ReadInt32(MMPosition);
            MMPosition += 4;

            return header;

        }
    }

    class BigBitmapInfoHeader
    {
        public long BitmapWidth { get; set; }
        public long BitmapHeight { get; set; }
        public short ColorPlanes { set; get; }
        public short BitsPerPixel { set; get; }
        public int CompressinoMethod { set; get; }
        public long ColorDataSize { set; get; }
        public int HorizantalResolution { set; get; }
        public int VerticalResolution { set; get; }
        public int Colors { set; get; }
        public int IColors { set; get; }

        public BigBitmapInfoHeader()
        {
            CompressinoMethod = 0;
            ColorDataSize = 0;
            ColorPlanes = 1;
        }

        public byte[] CreateInfoHeaderData(long ColorDataSize)
        {
            using (var s = new MemoryStream())
            using (var writer = new BinaryWriter(s))
            {
                //writer.Write(HeaderSize);
                writer.Write(BitmapWidth);
                writer.Write(BitmapHeight);
                writer.Write(ColorPlanes);
                writer.Write(BitsPerPixel);
                writer.Write(CompressinoMethod);
                this.ColorDataSize = ColorDataSize;
                writer.Write(ColorDataSize);
                writer.Write(HorizantalResolution);
                writer.Write(VerticalResolution);
                writer.Write(Colors);
                writer.Write(IColors);
                return s.ToArray();
            }
        }

        public static BigBitmapInfoHeader GetInfoHeader(byte[] data)
        {
            using (var s = new MemoryStream(data))
            using (var reader = new BinaryReader(s))
            {
                return new BigBitmapInfoHeader
                {
                    // HeaderSize = reader.ReadInt32(),
                    BitmapWidth = reader.ReadInt64(),
                    BitmapHeight = reader.ReadInt64(),
                    ColorPlanes = reader.ReadInt16(),
                    BitsPerPixel = reader.ReadInt16(),
                    CompressinoMethod = reader.ReadInt32(),
                    ColorDataSize = reader.ReadInt64(),
                    HorizantalResolution = reader.ReadInt32(),
                    VerticalResolution = reader.ReadInt32(),
                    Colors = reader.ReadInt32(),
                    IColors = reader.ReadInt32()
                };
            }
        }

        public static BigBitmapInfoHeader GetInfoHeader(MemoryMappedViewAccessor Reader, ref int MMPosition)
        {
            //MMPosition = 18;
            var header = new BigBitmapInfoHeader();

            // HeaderSize = reader.ReadInt32(),
            header.BitmapWidth = Reader.ReadInt64(MMPosition);
            MMPosition += 8;

            header.BitmapHeight = Reader.ReadInt64(MMPosition);
            MMPosition += 8;

            header.ColorPlanes = Reader.ReadInt16(MMPosition);
            MMPosition += 2;

            header.BitsPerPixel = Reader.ReadInt16(MMPosition);
            MMPosition += 2;

            header.CompressinoMethod = Reader.ReadInt32(MMPosition);
            MMPosition += 4;

            header.ColorDataSize = Reader.ReadInt64(MMPosition);
            MMPosition += 8;

            header.HorizantalResolution = Reader.ReadInt32(MMPosition);
            MMPosition += 4;

            header.VerticalResolution = Reader.ReadInt32(MMPosition);
            MMPosition += 4;

            header.Colors = Reader.ReadInt32(MMPosition);
            MMPosition += 4;

            header.IColors = Reader.ReadInt32(MMPosition);
            MMPosition += 4;

            return header;

        }
    }
}
