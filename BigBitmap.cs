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

namespace RoboNav
{
    class BigBitmap
    {
        public Bitmap CurrentChunk { get; private set; }
        public BigBitmapHeader FileHeader { get; private set; }
        public BigBitmapInfoHeader FileInfoHeader { get; private set; }
        private MemoryMappedViewAccessor Reader;
        private int Padding;
        private long RowSize;
        private int MemoryMappedPosition;


        private BigBitmap()
        { }

        private BigBitmap(MemoryMappedViewAccessor Accessor, BigBitmapHeader Header, BigBitmapInfoHeader Info)
        {
            Reader = Accessor;
            FileHeader = Header;
            FileInfoHeader = Info;
            Initilize();
        }

        public BigBitmap(string filePath)
        {
            var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.OpenOrCreate, "any", 0);
            Reader = mmf.CreateViewAccessor();

            var mmf2 = MemoryMappedFile.CreateFromFile(filePath + "1", FileMode.OpenOrCreate, "any1", (int)Math.Pow(2, 30));
            var Reader2 = mmf2.CreateViewAccessor();




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
            this.Reader.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            Marshal.Copy(IntPtr.Add(new IntPtr(ptr), (int)(Offset + MemoryMappedPosition)), arr, 0, (int)Length);
            this.Reader.SafeMemoryMappedViewHandle.ReleasePointer();
            return arr;
        }

        public Bitmap GetChunk(long width, long height, long x, long y)
        {
            if (width + x > FileInfoHeader.BitmapWidth)
                width = FileInfoHeader.BitmapWidth - x;
            if (height + y > FileInfoHeader.BitmapHeight)
                height = FileInfoHeader.BitmapHeight;
            byte[] Chunk = null;

            for (long i = FileInfoHeader.BitmapHeight - (y + height); i < (FileInfoHeader.BitmapHeight - y); i++)
            {
                var rawData = ReadRawDataByRealOffset(70 + x + i * RowSize - 1, width * 3);
                if (rawData.Length % 4 != 0)
                {
                    rawData = rawData.Concat(new byte[rawData.Length % 4]).ToArray();
                }

                if (Chunk == null)
                    Chunk = rawData;
                else
                    Chunk = Chunk.Concat(rawData).ToArray();
            }

            var fileHeader = new BigBitmapHeader() { FileSize = Chunk.Length + 70 };
            var fileInfoHeader = new BigBitmapInfoHeader()
            {
                BitmapHeight = height,
                BitmapWidth = width,
                BitsPerPixel = 24,
                ColorPlanes = 1,
                HorizantalResolution = 197,
                VerticalResolution = 197
            };

            var HeaderData = fileHeader.CreateBigBitmapHeader();
            HeaderData = HeaderData.Concat(fileInfoHeader.CreateInfoHeaderData(Chunk.Length)).Concat(Chunk).ToArray();
            // File.WriteAllBytes("C:\\X\\FT.bmp", HeaderData);

            using (var ms = new MemoryStream(HeaderData))
                CurrentChunk = new Bitmap(ms);

            return CurrentChunk;
        }

        public static BigBitmap CreateBigBitmap(string filePath, long Width, long Height)
        {
            return CreateBigBitmap(filePath, Width, Height, 197, 197, 0, 0, 0);
        }

        public static BigBitmap CreateBigBitmap(string filePath, long Width, long Height, int HorizantalRes, int VerticalRes, byte BackGround_Red, byte BackGround_Green, byte BackGround_Blue)
        {
            if (Width * Height > long.MaxValue - 70)
                throw new ArgumentOutOfRangeException("Width*Height", "Maximum file Size Exceeds Max INT64 Size, use lower Dimension Size or lower BPP (Which is not Supported YET!)");

            var rowSize = (long)Math.Floor(((24 * (double)Width) + 31) / 32) * 4;
            var padding = rowSize - Width * 3;

            BigBitmapHeader header = null;
            BigBitmapInfoHeader headerInfo = null;
            Task.Factory.StartNew(new Action(() =>
            {
                using (var fileStream = new FileStream(filePath, FileMode.Create))
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

                    //   headerData = headerData.Concat(new byte[padding]).ToArray();
                    fileStream.Flush();

                    for (long i = 0; i < Height; i++)
                    {
                        for (long k = 0; k < Width; k++)
                        {
                            fileStream.Write(colorBytes, 0, colorBytes.Length);
                            // if (k % 1000 == 0)
                            // fileStream.Flush();
                        }
                        fileStream.Write(headerData, 0, headerData.Length);
                        //fileStream.Write(headerData, 0, headerData.Length);

                    }

                    fileStream.Flush();
                    fileStream.Close();
                }
            })).Wait();

            return new BigBitmap(MemoryMappedFile.CreateFromFile(filePath).CreateViewAccessor(), header, headerInfo);
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
                return CreateBigBitmapFromBitmap(fileStream, filePath.Contains('.') ? filePath.Substring(0, (filePath.LastIndexOf('.'))) + ".bbmp" : filePath + ".bbmp");
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
                    //BigBitmapFile.Dispose();
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
        {
            if ((Start.X > FileInfoHeader.BitmapWidth || Start.X < 0) || (Start.Y > FileInfoHeader.BitmapHeight || Start.Y < 0))
                throw new ArgumentOutOfRangeException("Start");

            if ((Destination.X > FileInfoHeader.BitmapWidth || Destination.X < 0) || (Destination.Y > FileInfoHeader.BitmapHeight || Destination.Y < 0))
                throw new ArgumentOutOfRangeException("Destination");

            if (Start.X == Destination.X && Start.Y == Destination.Y)
                SetPixel(Start, LineColor);

            else if (Start.X == Destination.X) //Vertical Line
            {
                long i = Start.Y > Destination.Y ? Start.Y : Destination.Y;
                long j = Start.Y < Destination.Y ? Start.Y : Destination.Y;
                j = FileInfoHeader.BitmapHeight - j;

                for (long k = FileInfoHeader.BitmapHeight - i - 1; k < j; k++)
                {
                    Reader.Write(70 + k * RowSize + Start.X * 3, LineColor.B);
                    Reader.Write(70 + k * RowSize + 1 + Start.X * 3, LineColor.G);
                    Reader.Write(70 + k * RowSize + 2 + Start.X * 3, LineColor.R);
                }
            }
            else if (Start.Y == Destination.Y) // Horizantal Line
            {
                int i = Start.X < Destination.X ? Start.X : Destination.X;
                int j = Start.X > Destination.X ? Start.X : Destination.X;
                i *= 3;
                j *= 3;
                for (; i <= j; i += 3)
                {
                    Reader.Write(70 + i + ((FileInfoHeader.BitmapHeight - Start.Y - 1) * RowSize), LineColor.B);
                    Reader.Write(70 + i + 1 + ((FileInfoHeader.BitmapHeight - Start.Y - 1) * RowSize), LineColor.G);
                    Reader.Write(70 + i + 2 + ((FileInfoHeader.BitmapHeight - Start.Y - 1) * RowSize), LineColor.R);
                }
            }
            else // NONE
            {
                var delta_X = Destination.X - Start.X;
                var delta_Y = Destination.Y - Start.Y;

                SetPixel(Start, LineColor);
                SetPixel(Destination, LineColor);

                var Slope = (double)delta_Y / delta_X;
                var alpha = Math.Atan(Slope);
                var lineLen = Math.Sqrt(Math.Pow(delta_X, 2) + Math.Pow(delta_Y, 2));

                if (Start.X < Destination.X)
                    for (int i = Start.X; i < Destination.X; i++)
                    {
                        SetPixel(i, ((int)(Slope * (i - Start.X)) + Start.Y), LineColor);
                    }

                if (Start.X > Destination.X)
                    for (int i = Start.X; i > Destination.X; i--)
                    {
                        SetPixel(i, ((int)(Slope * (i - Start.X)) + Start.Y), LineColor);
                    }
            }
        }

        public void SetPixel(Point Position, Color PixelColor)
        {
            Reader.Write(70 + RowSize * (FileInfoHeader.BitmapHeight - Position.Y - 1) + Position.X * 3, PixelColor.B);
            Reader.Write(70 + 1 + RowSize * (FileInfoHeader.BitmapHeight - Position.Y - 1) + Position.X * 3, PixelColor.G);
            Reader.Write(70 + 2 + RowSize * (FileInfoHeader.BitmapHeight - Position.Y - 1) + Position.X * 3, PixelColor.R);
        }

        private void SetPixel(int X, int Y, Color PixelColor)
        {
            Reader.Write(70 + RowSize * (FileInfoHeader.BitmapHeight - Y - 1) + X * 3, PixelColor.B);
            Reader.Write(70 + 1 + RowSize * (FileInfoHeader.BitmapHeight - Y - 1) + X * 3, PixelColor.G);
            Reader.Write(70 + 2 + RowSize * (FileInfoHeader.BitmapHeight - Y - 1) + X * 3, PixelColor.R);
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
        {
            int x = -1;
            int y = -1;

            for (double i = 0; i < 2 * Math.PI; i += 0.05)
            {
                x = (int)(Math.Cos(i) * Radius) + Center.X;
                y = (int)(Math.Sin(i) * Radius) + Center.Y;
                if (x >= FileInfoHeader.BitmapWidth || x < 0 || y < 0 || y >= FileInfoHeader.BitmapHeight)
                    continue;

                SetPixel(x, y, Color);
            }

            if (Fill)
                for (int i = 0; i < Radius; i++)
                    DrawCircle(Center, i, Color, false);


        }

        public Bitmap ConvertToBitmap()
        {
            if (FileHeader.FileSize > int.MaxValue - 70)
                throw new InvalidOperationException("File Size is too big, Try using \"Chunk\" method to get smaller Picture Size");

            if (FileInfoHeader.BitmapHeight > int.MaxValue || FileInfoHeader.BitmapWidth > int.MaxValue || FileInfoHeader.ColorDataSize > int.MaxValue)
                throw new InvalidOperationException("Color Data (Height or Width) is(are) too high, Cant fit one (or more) in \"Int\" DataType");

            Bitmap map = null;

            using (var ms = new MemoryStream())
            using (var writer = new StreamWriter(ms))
            {
                var header = new BitmapHeader();
                header.FileSize = (int)FileHeader.FileSize;

                var headerInfo = new BitmapInfoHeader();
                headerInfo.BitmapHeight = (int)FileInfoHeader.BitmapHeight;
                headerInfo.BitmapWidth = (int)FileInfoHeader.BitmapWidth;
                //headerInfo.ColorDataSize = (int)FileInfoHeader.ColorDataSize;

                writer.Write(header.CreateBitmapHeader());
                writer.Write(headerInfo.CreateInfoHeaderData((int)FileInfoHeader.ColorDataSize));
                for (int i = 0; i < headerInfo.ColorDataSize; i++)
                    writer.Write(Reader.ReadByte(i + 70));

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
                    throw new InvalidOperationException("Color Data (Height or Width) is(are) too high, Cant fit one (or more) in \"Int\" DataType");

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

                for (int i = 0; i < header.FileSize - 38; i++) // no idea where 38 came from (possibly from 54-16=38 but idk why ?!)
                    file.WriteByte(Reader.ReadByte(i + 70));

                file.Flush();
                file.Close();
            }
        }
    }
}
