using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Diagnostics;

namespace RoboNav
{
    class BigBitmap
    {
        public Bitmap CurrentChunk { get; private set; }
        public BigBitmapHeader FileHeader { get; private set; }
        public BigBitmapInfoHeader FileInfoHeader { get; private set; }
        private MemoryMappedViewAccessor Reader;
        private FileInfo MmfInfo;
        public string FilePath { get; private set; }
        private int Padding;
        private long RowSize;
        private int MemoryMappedPosition;
        private const int MemoryPageSize = 65536;
        private const int MemoryPagesForVirutalMemoryBuffer = 1;
        private long VirtualSize = MemoryPagesForVirutalMemoryBuffer * MemoryPageSize; // must be a multiple of 65536
        private long Offset = 0;
        private MemoryMappedFile Mmf;


        private BigBitmap()
        { }

        private BigBitmap(MemoryMappedViewAccessor Accessor, BigBitmapHeader Header, BigBitmapInfoHeader Info, string filePath)
        {
            Reader = Accessor;
            FileHeader = Header;
            FileInfoHeader = Info;
            FilePath = filePath;
            Initilize();
        }

        public BigBitmap(string filePath)
        {
            if (!File.Exists(filePath))
                throw new Exception();
            FilePath = filePath;

            Mmf = MemoryMappedFile.CreateFromFile(filePath);
            MmfInfo = new FileInfo(filePath);

            if (MmfInfo.Length < VirtualSize)
                VirtualSize = MmfInfo.Length;


            // File.SetAttributes(filePath, FileAttributes.Normal);
            Reader = Mmf.CreateViewAccessor(Offset, VirtualSize);

            if (!Reader.CanRead)
                throw new InvalidOperationException("File cannot be read");

            MemoryMappedPosition = 0;
            FileHeader = BigBitmapHeader.GetHeader(Reader, out MemoryMappedPosition);
            //MemoryMappedPostion = 22
            FileInfoHeader = BigBitmapInfoHeader.GetInfoHeader(Reader, ref MemoryMappedPosition);
            //MemoryMappedPostion = 70
            Initilize();
        }

        private void Initilize()
        {
            RowSize = (long)Math.Floor(((double)FileInfoHeader.BitsPerPixel * FileInfoHeader.BitmapWidth + 31) / 32) * 4;
            Padding = (int)(RowSize - FileInfoHeader.BitmapWidth * 3);
            MmfInfo = new FileInfo(FilePath);
        }

        public unsafe byte[] ReadRawData(int LogicalOffset, int Length)
        {
            byte[] arr = new byte[Length];
            byte* ptr = (byte*)0;
            this.Reader.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            Marshal.Copy(IntPtr.Add(new IntPtr(ptr), LogicalOffset + MemoryMappedPosition), arr, 0, Length);
            this.Reader.SafeMemoryMappedViewHandle.ReleasePointer();
            return arr;
        }

        public unsafe byte[] ReadRawDataByRealOffset(long Offset, long Length)
        {
            byte[] arr = new byte[Length];
            byte* ptr = (byte*)0;
            var off = (int)((Offset) /*% MemoryPageSize*/);
            this.Reader.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            Marshal.Copy(IntPtr.Add(new IntPtr(ptr), off), arr, 0, (int)Length);
            this.Reader.SafeMemoryMappedViewHandle.ReleasePointer();
            return arr;
        }

        public Bitmap GetChunk(long width, long height, long x, long y)
        {
            if (width * height * 3 >= int.MaxValue)
                throw new InvalidOperationException("Chunk Size is too big");

            if (width + x > FileInfoHeader.BitmapWidth)
                width = FileInfoHeader.BitmapWidth - x;
            if (height + y > FileInfoHeader.BitmapHeight)
                height = FileInfoHeader.BitmapHeight;

            var newRowSize = (int)Math.Floor(((double)FileInfoHeader.BitsPerPixel * width + 31) / 32) * 4;
            var newPadding = (newRowSize - width * 3);
            var Data2Read = (int)(newRowSize - newPadding);

            IEnumerable<byte> Chunk = null;
            byte[] rawData = new byte[Data2Read];
            long startByteIndex = -1;
            int BytesRead = -1;

            for (long i = FileInfoHeader.BitmapHeight - (y + height); i < (FileInfoHeader.BitmapHeight - y); i++)
            {
                startByteIndex = 70 + (x * 3) + (i * RowSize) - 1;

                SeektoPage(startByteIndex);

                BytesRead = Reader.ReadArray<byte>(startByteIndex % MemoryPageSize,
                   rawData, 0, Data2Read);

                if (newPadding != 0)
                    rawData = rawData.Concat(new byte[newPadding]).ToArray();


                if (Chunk == null)
                    if (BytesRead != Data2Read)
                    {
                        Chunk = rawData.SubArray(0, BytesRead);
                        Offset += BytesRead;

                        if (Offset + VirtualSize >= MmfInfo.Length)
                            VirtualSize = MmfInfo.Length - Offset;
                        else
                            VirtualSize = MemoryPagesForVirutalMemoryBuffer * MemoryPageSize;

                        Reader.Dispose();
                        Reader = Mmf.CreateViewAccessor(Offset, VirtualSize);

                        BytesRead += Reader.ReadArray<byte>(Offset % MemoryPageSize, rawData, BytesRead, rawData.Length - BytesRead);
                        if (BytesRead != RowSize)
                        {
                            Chunk = Chunk.Concat(rawData.SubArray(0, BytesRead));
                        }
                        else
                        { Chunk = Chunk.Concat(rawData); }
                    }
                    else
                    { Chunk = rawData; }

                else
                    if (BytesRead != Data2Read)
                    {
                        Chunk = Chunk.Concat(rawData.SubArray(0, BytesRead));
                        Offset += BytesRead;

                        if (Offset + VirtualSize >= MmfInfo.Length)
                            VirtualSize = MmfInfo.Length - Offset;
                        else
                            VirtualSize = MemoryPagesForVirutalMemoryBuffer * MemoryPageSize;

                        Reader.Dispose();
                        Reader = Mmf.CreateViewAccessor(Offset, VirtualSize);

                        BytesRead += Reader.ReadArray<byte>(Offset % MemoryPageSize, rawData, BytesRead, rawData.Length - BytesRead);
                        if (BytesRead != RowSize)
                        {
                            Chunk = Chunk.Concat(rawData.SubArray(0, BytesRead));
                        }
                        else
                        { Chunk = Chunk.Concat(rawData); }
                    }
                    else
                    {
                        Chunk = Chunk.Concat(rawData);
                    }
            }

            var fileHeader = new BitmapHeader() { FileSize = Chunk.Count() + 54 };
            var fileInfoHeader = new BitmapInfoHeader()
            {
                BitmapHeight = (int)height,
                BitmapWidth = (int)width,
                BitsPerPixel = FileInfoHeader.BitsPerPixel,
                ColorPlanes = FileInfoHeader.ColorPlanes,
                HorizantalResolution = FileInfoHeader.HorizantalResolution,
                VerticalResolution = FileInfoHeader.VerticalResolution
            };

            var HeaderData = fileHeader.CreateBitmapHeader();
            HeaderData = HeaderData.Concat(fileInfoHeader.CreateInfoHeaderData(Chunk.Count())).ToArray();
            HeaderData = HeaderData.Concat(Chunk).ToArray();


            using (var ms = new MemoryStream(HeaderData))
            { CurrentChunk = new Bitmap(ms); }

            return CurrentChunk;
        }

        public static BigBitmap CreateBigBitmap(string filePath, long Width, long Height)
        { return CreateBigBitmap(filePath, Width, Height, 197, 197, 0, 0, 0); }

        public static BigBitmap CreateBigBitmap(string filePath, long Width, long Height, int HorizantalRes, int VerticalRes, byte BackGround_Red, byte BackGround_Green, byte BackGround_Blue)
        {
            if (Width * Height > long.MaxValue - 70)
                throw new ArgumentOutOfRangeException("Width*Height", "Maximum file Size Exceeds Max INT64 Size, use lower Dimension Size or lower BPP (Which is not Supported YET!)");

            var rowSize = (long)Math.Floor(((24 * (double)Width) + 31) / 32) * 4;
            var padding = rowSize - Width * 3;

            BigBitmapHeader header = null;
            BigBitmapInfoHeader headerInfo = null;

            using (var fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                header = new BigBitmapHeader() { FileSize = 70 + Height * ((Width * 3) + padding) };

                var headerData = header.CreateBigBitmapHeader();
                fileStream.Write(headerData, 0, headerData.Length);
                fileStream.Flush();

                headerInfo = new BigBitmapInfoHeader() { BitmapHeight = Height, BitsPerPixel = 24, ColorPlanes = 1, BitmapWidth = Width, HorizantalResolution = HorizantalRes, VerticalResolution = VerticalRes };

                headerData = headerInfo.CreateInfoHeaderData(Height * ((Width * 3) + padding));
                fileStream.Write(headerData, 0, headerData.Length);

                var colorBytes = new byte[] { BackGround_Blue, BackGround_Green, BackGround_Red };
                headerData = new byte[padding];

                fileStream.Flush();

                for (long i = 0; i < Height; i++)
                {
                    for (long k = 0; k < Width; k++)
                        fileStream.Write(colorBytes, 0, colorBytes.Length);
                    //headerData = byte[padding]
                    fileStream.Write(headerData, 0, headerData.Length);
                }

                fileStream.Flush();
                fileStream.Close();
            }


            return new BigBitmap(MemoryMappedFile.CreateFromFile(filePath).CreateViewAccessor(), header, headerInfo, filePath);
        }

        public static BigBitmap CreateBigBitmap(string filePath, long Width, long Height, byte BackGround_Red, byte BackGround_Green, byte BackGround_Blue)
        {
            return CreateBigBitmap(filePath, Width, Height, 197, 197, BackGround_Red, BackGround_Green, BackGround_Blue);
        }

        public static BigBitmap CreateBigBitmap(string filePath, long Width, long Height, Color BackgroundColor)
        {
            return CreateBigBitmap(filePath, Width, Height, 197, 197, BackgroundColor.R, BackgroundColor.G, BackgroundColor.B);
        }

        public static BigBitmap CreateBigBitmapFromBitmap(string filePath)
        {
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                return CreateBigBitmapFromBitmap(fileStream, filePath.Contains('.') ?
                    filePath.Substring(0, (filePath.LastIndexOf('.'))) + ".bbmp" : filePath + ".bbmp");
        }

        public static BigBitmap CreateBigBitmapFromBitmap(Stream Stream, string BigBitmapFilePath)
        {
            using (var Reader = new StreamReader(Stream))
            {
                using (var BigBitmapFile = new FileStream(BigBitmapFilePath, FileMode.Create, FileAccess.ReadWrite))
                {
                    byte[] data = new byte[40];

                    Stream.Read(data, 0, 18);
                    var header = BitmapHeader.GetHeader(data);

                    Stream.Read(data, 0, 36);
                    var infoHeader = BitmapInfoHeader.GetInfoHeader(data);

                    if (infoHeader.BitsPerPixel != 24)
                        throw new InvalidOperationException("Only 24bpp Bitmaps Supported");

                    var bigBitmap = new BigBitmap();

                    bigBitmap.FileHeader = new BigBitmapHeader();
                    bigBitmap.FileHeader.FileSize = header.FileSize;

                    bigBitmap.FileInfoHeader = new BigBitmapInfoHeader();
                    bigBitmap.FileInfoHeader.BitmapHeight = infoHeader.BitmapHeight;
                    bigBitmap.FileInfoHeader.BitmapWidth = infoHeader.BitmapWidth;
                    bigBitmap.FileInfoHeader.BitsPerPixel = infoHeader.BitsPerPixel;
                    bigBitmap.FileInfoHeader.ColorDataSize = infoHeader.ColorDataSize;
                    bigBitmap.FileInfoHeader.ColorPlanes = infoHeader.ColorPlanes;
                    bigBitmap.FileInfoHeader.Colors = infoHeader.Colors;
                    bigBitmap.FileInfoHeader.CompressinoMethod = infoHeader.CompressinoMethod;
                    bigBitmap.FileInfoHeader.HorizantalResolution = infoHeader.HorizantalResolution;
                    bigBitmap.FileInfoHeader.IColors = infoHeader.IColors;
                    bigBitmap.FileInfoHeader.VerticalResolution = infoHeader.VerticalResolution;

                    data = bigBitmap.FileHeader.CreateBigBitmapHeader();
                    BigBitmapFile.Write(data, 0, data.Length);
                    data = bigBitmap.FileInfoHeader.CreateInfoHeaderData(infoHeader.ColorDataSize);
                    BigBitmapFile.Write(data, 0, data.Length);

                    int bytesRead = -1;
                    while ((bytesRead = Stream.Read(data, 0, data.Length)) != 0)
                        BigBitmapFile.Write(data, 0, bytesRead);

                    BigBitmapFile.Flush();
                    BigBitmapFile.Close();

                    return new BigBitmap(BigBitmapFilePath);
                }
            }
        }

        public void DrawLine(Point Start, Point Destination, Color LineColor)
        { DrawLine(Start, Destination, LineColor, 1); }

        public void DrawLine(int Start_X, int Start_Y, int Destination_X, int Destination_Y, Color LineColor)
        { DrawLine(new Point(Start_X, Start_Y), new Point(Destination_X, Destination_Y), LineColor, 1); }

        public void DrawLine(int Start_X, int Start_Y, int Destination_X, int Destination_Y, Color LineColor, int LineWidth)
        { DrawLine(new Point(Start_X, Start_Y), new Point(Destination_X, Destination_Y), LineColor, LineWidth); }

        public void DrawLine(Point Start, Point Destination, Color LineColor, int LineWidth)
        { DrawLine(Start.X, Start.Y, Destination.X, Destination.Y, LineWidth, LineColor); }

        public void DrawLine(long Start_X, long Start_Y, long Destination_X, long Destination_Y, int LineWidth, Color LineColor)
        {
            if ((Start_X > FileInfoHeader.BitmapWidth || Start_X < 0) || (Start_Y > FileInfoHeader.BitmapHeight || Start_Y < 0))
                throw new ArgumentOutOfRangeException("Start");

            if ((Destination_X > FileInfoHeader.BitmapWidth || Destination_X < 0) || (Destination_Y > FileInfoHeader.BitmapHeight || Destination_Y < 0))
                throw new ArgumentOutOfRangeException("Destination");

            if (Start_X == Destination_X && Start_Y == Destination_Y)
                SetPixel(Start_X, Start_Y, LineColor);

            else if (Start_X == Destination_X) //Vertical Line
            {
                long i = Start_Y > Destination_Y ? Start_Y : Destination_Y;
                long j = Start_Y < Destination_Y ? Start_Y : Destination_Y;
                j = FileInfoHeader.BitmapHeight - j;

                long pos = -1;
                for (long k = FileInfoHeader.BitmapHeight - i - 1; k < j; k++)
                {
                    pos = 70 + k * RowSize + Start_X * 3;

                    SeektoPage(pos);
                    Reader.Write(pos % MemoryPageSize, LineColor.B);

                    SeektoPage(pos + 1);
                    Reader.Write((pos + 1) % MemoryPageSize, LineColor.G);

                    SeektoPage(pos + 1);
                    Reader.Write((pos + 2) % MemoryPageSize, LineColor.R);
                }
            }
            else if (Start_Y == Destination_Y) // Horizantal Line
            {
                long i = Start_X < Destination_X ? Start_X : Destination_X;
                long j = Start_X > Destination_X ? Start_X : Destination_X;
                long pos = -1;

                for (; i <= j; i++)
                {
                    pos = 70 + i * 3 + ((FileInfoHeader.BitmapHeight - Start_Y - 1) * RowSize);

                    SeektoPage(pos);
                    Reader.Write(pos % MemoryPageSize, LineColor.B);

                    SeektoPage(pos + 1);
                    Reader.Write((pos + 1) % MemoryPageSize, LineColor.G);

                    SeektoPage(pos + 2);
                    Reader.Write((pos + 2) % MemoryPageSize, LineColor.R);
                }
            }
            else // NONE
            {
                //Draw Line Using Bresenham Line algorithm
                long dx = Math.Abs(Destination_X - Start_X), sx = Start_X < Destination_X ? 1 : -1;
                long dy = Math.Abs(Destination_Y - Start_Y), sy = Start_Y < Destination_Y ? 1 : -1;
                long err = (dx > dy ? dx : -dy) / 2, e2;

                while (!(Start_X == Destination_X && Start_Y == Destination_Y))
                {
                    SetPixel(Start_X, Start_Y, LineColor);
                    e2 = err;
                    if (e2 > -dx) { err -= dy; Start_X += sx; }
                    if (e2 < dy) { err += dx; Start_Y += sy; }
                }
            }
        }

        public void SetPixel(Point Position, Color PixelColor)
        {
            var startByteIndex = 70 + RowSize * (FileInfoHeader.BitmapHeight - Position.Y - 1) + Position.X * 3;

            SeektoPage(startByteIndex);
            Reader.Write(startByteIndex % MemoryPageSize, PixelColor.B);

            SeektoPage(startByteIndex + 1);
            Reader.Write((startByteIndex + 1) % MemoryPageSize, PixelColor.G);

            SeektoPage(startByteIndex + 2);
            Reader.Write((startByteIndex + 2) % MemoryPageSize, PixelColor.R);
        }

        private void SetPixel(long X, long Y, Color PixelColor)
        {
            var startByteIndex = 70 + RowSize * (FileInfoHeader.BitmapHeight - Y - 1) + X * 3;

            SeektoPage(startByteIndex);
            Reader.Write(startByteIndex % MemoryPageSize, PixelColor.B);

            SeektoPage(startByteIndex + 1);
            Reader.Write((startByteIndex + 1) % MemoryPageSize, PixelColor.G);

            SeektoPage(startByteIndex + 2);
            Reader.Write((startByteIndex + 2) % MemoryPageSize, PixelColor.R);
        }

        public void DrawRectangle(Point A, Point B, Color Color, bool Fill)
        {
            DrawLine(A.X, A.Y, B.X, A.Y, Color);
            DrawLine(A.X, A.Y, A.X, B.Y, Color);
            DrawLine(A.X, B.Y, B.X, B.Y, Color);
            DrawLine(B.X, A.Y, B.X, B.Y, Color);

            if (Fill)
            {
                int j = A.Y > B.Y ? A.Y : B.Y;
                int i = A.Y < B.Y ? A.Y : B.Y;

                for (; i < j; i++)
                    DrawLine(A.X, i, B.X, i, Color);
            }
        }

        public void DrawCircle(Point Center, int Radius, Color Color, bool Fill)
        { DrawCircle(Center.X, Center.Y, Radius, Color, Fill); }

        public void DrawCircle(long Center_X, long Center_Y, long Radius, Color Color, bool Fill)
        {
            //long x = -1;
            //long y = -1;

            //for (double i = 0; i < 2 * Math.PI; i += 0.001)
            //{
            //    x = (long)(Math.Cos(i) * Radius) + Center_X;
            //    y = (long)(Math.Sin(i) * Radius) + Center_Y;

            //    if (x >= FileInfoHeader.BitmapWidth || x < 0 || y < 0 || y >= FileInfoHeader.BitmapHeight)
            //        continue;

            //    SetPixel(x, y, Color);

            //    if (Fill)
            //        DrawLine(Center_X, Center_Y, x, y, 1, Color);
            //}

            long d = (5 - Radius * 4) / 4;
            long x = 0;
            long y = Radius;

            do
            {
                if (Center_X + x >= 0 && Center_X + x <= FileInfoHeader.BitmapWidth - 1 && Center_Y + y >= 0 && Center_Y + y <= FileInfoHeader.BitmapHeight - 1) SetPixel(Center_X + x, Center_Y + y, Color);
                if (Center_X + x >= 0 && Center_X + x <= FileInfoHeader.BitmapWidth - 1 && Center_Y - y >= 0 && Center_Y - y <= FileInfoHeader.BitmapHeight - 1) SetPixel(Center_X + x, Center_Y - y, Color);
                if (Center_X - x >= 0 && Center_X - x <= FileInfoHeader.BitmapWidth - 1 && Center_Y + y >= 0 && Center_Y + y <= FileInfoHeader.BitmapHeight - 1) SetPixel(Center_X - x, Center_Y + y, Color);
                if (Center_X - x >= 0 && Center_X - x <= FileInfoHeader.BitmapWidth - 1 && Center_Y - y >= 0 && Center_Y - y <= FileInfoHeader.BitmapHeight - 1) SetPixel(Center_X - x, Center_Y - y, Color);
                if (Center_X + y >= 0 && Center_X + y <= FileInfoHeader.BitmapWidth - 1 && Center_Y + x >= 0 && Center_Y + x <= FileInfoHeader.BitmapHeight - 1) SetPixel(Center_X + y, Center_Y + x, Color);
                if (Center_X + y >= 0 && Center_X + y <= FileInfoHeader.BitmapWidth - 1 && Center_Y - x >= 0 && Center_Y - x <= FileInfoHeader.BitmapHeight - 1) SetPixel(Center_X + y, Center_Y - x, Color);
                if (Center_X - y >= 0 && Center_X - y <= FileInfoHeader.BitmapWidth - 1 && Center_Y + x >= 0 && Center_Y + x <= FileInfoHeader.BitmapHeight - 1) SetPixel(Center_X - y, Center_Y + x, Color);
                if (Center_X - y >= 0 && Center_X - y <= FileInfoHeader.BitmapWidth - 1 && Center_Y - x >= 0 && Center_Y - x <= FileInfoHeader.BitmapHeight - 1) SetPixel(Center_X - y, Center_Y - x, Color);
                if (d < 0)
                {
                    d += 2 * x + 1;
                }
                else
                {
                    d += 2 * (x - y) + 1;
                    y--;
                }
                x++;
            } while (x <= y);

            if (Fill)
                for (int i = 0; i < Radius; i++)
                    DrawCircle(Center_X, Center_Y, i, Color, false);
        }

        public Bitmap ConvertToBitmap()
        {
            if (FileHeader.FileSize > int.MaxValue - 70)
                throw new InvalidOperationException("File Size is too big, Try using \"Chunk\" method to get smaller Picture Size");

            if (FileInfoHeader.BitmapHeight > int.MaxValue || FileInfoHeader.BitmapWidth > int.MaxValue || FileInfoHeader.ColorDataSize > int.MaxValue)
                throw new InvalidOperationException("Color Data (Height or Width) is(are) too high, Cant fit one (or more) in \"Int\" DataType");

            Bitmap map = null;

            using (var ms = new MemoryStream())
            {
                var header = new BitmapHeader();
                header.FileSize = (int)FileHeader.FileSize - 16;

                var headerInfo = new BitmapInfoHeader();
                headerInfo.BitmapHeight = (int)FileInfoHeader.BitmapHeight;
                headerInfo.BitmapWidth = (int)FileInfoHeader.BitmapWidth;

                headerInfo.BitsPerPixel = FileInfoHeader.BitsPerPixel;
                headerInfo.ColorPlanes = FileInfoHeader.ColorPlanes;
                headerInfo.HorizantalResolution = FileInfoHeader.HorizantalResolution;
                headerInfo.VerticalResolution = FileInfoHeader.VerticalResolution;

                var data = header.CreateBitmapHeader();
                ms.Write(data, 0, data.Length);

                data = headerInfo.CreateInfoHeaderData((int)FileInfoHeader.ColorDataSize);
                ms.Write(data, 0, data.Length);
                data = null;

                for (int i = 0; i < header.FileSize - 54; i++) // 54 = 70 - 16 
                {
                    SeektoPage(i + 70);
                    ms.WriteByte(Reader.ReadByte(((i + 70) % MemoryPageSize)));
                }
                map = new Bitmap(ms);
            }

            return map;
        }

        public void ConvertAndSaveToBitmap(string Path)
        {
            using (var file = new FileStream(Path, FileMode.Create, FileAccess.ReadWrite))
            {
                if (FileHeader.FileSize > int.MaxValue - 70)
                    throw new InvalidOperationException("File Size is too big, Try using \"Chunk\" method to get smaller Picture Size");

                if (FileInfoHeader.BitmapHeight > int.MaxValue || FileInfoHeader.BitmapWidth > int.MaxValue || FileInfoHeader.ColorDataSize > int.MaxValue)
                    throw new InvalidOperationException("Color Data (Height or Width) is(are) too high, Cant fit one field (or more) in \"Int\" DataType");

                var header = new BitmapHeader();
                header.FileSize = (int)FileHeader.FileSize - 16; // 70-54=16 is difference between our new "bigbitmap" header and original bitmapheader

                var headerInfo = new BitmapInfoHeader();
                headerInfo.BitmapHeight = (int)FileInfoHeader.BitmapHeight;
                headerInfo.BitmapWidth = (int)FileInfoHeader.BitmapWidth;

                headerInfo.BitsPerPixel = FileInfoHeader.BitsPerPixel;
                headerInfo.ColorPlanes = FileInfoHeader.ColorPlanes;
                headerInfo.HorizantalResolution = FileInfoHeader.HorizantalResolution;
                headerInfo.VerticalResolution = FileInfoHeader.VerticalResolution;

                var data = header.CreateBitmapHeader();
                file.Write(data, 0, data.Length);
                data = headerInfo.CreateInfoHeaderData((int)FileInfoHeader.ColorDataSize);
                file.Write(data, 0, data.Length);

                for (int i = 0; i < header.FileSize - 54; i++) // 54 = 70 - 16 
                {
                    SeektoPage(i + 70);
                    file.WriteByte(Reader.ReadByte(((i + 70) % MemoryPageSize)));
                }

                file.Flush();
                file.Close();
            }
        }

        public Color GetPixel(long X, long Y)
        {
            if (X > FileInfoHeader.BitmapWidth || X < 0)
                throw new ArgumentOutOfRangeException("X");
            if (Y > FileInfoHeader.BitmapHeight || Y < 0)
                throw new ArgumentOutOfRangeException("Y");

            var position = (FileInfoHeader.BitmapHeight - Y - 1) * RowSize + X * 3 + 70;

            SeektoPage(position);
            var B = Reader.ReadByte(position % MemoryPageSize);

            SeektoPage(position + 1);
            var G = Reader.ReadByte((position + 1) % MemoryPageSize);

            SeektoPage(position + 2);
            var R = Reader.ReadByte((position + 2) % MemoryPageSize);

            return Color.FromArgb(R, G, B);
        }

        public Color GetPixel(Point Coordinates)
        { return GetPixel(Coordinates.X, Coordinates.Y); }

        private void SeektoPage(long Index)
        {
            if (Offset > Index)
            {
                Offset = (Index / MemoryPageSize) * MemoryPageSize;

                if (Offset + VirtualSize >= MmfInfo.Length)
                    VirtualSize = MmfInfo.Length - Offset;
                else
                    VirtualSize = MemoryPagesForVirutalMemoryBuffer * MemoryPageSize;

                Reader.Dispose();
                Reader = Mmf.CreateViewAccessor(Offset, VirtualSize);
            }
            if (Offset + VirtualSize <= Index)
            {
                Offset = (Index / MemoryPageSize) * MemoryPageSize;

                if (Offset + VirtualSize >= MmfInfo.Length)
                    VirtualSize = MmfInfo.Length - Offset;
                else
                    VirtualSize = MemoryPagesForVirutalMemoryBuffer * MemoryPageSize;

                Reader.Dispose();
                Reader = Mmf.CreateViewAccessor(Offset, VirtualSize);
            }
        }
    }
}
