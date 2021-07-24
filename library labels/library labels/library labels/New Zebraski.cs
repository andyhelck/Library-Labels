using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Library_Labels_Namespace
{
    #region Custom Bitmap Classes


    public class CompactBitmap // This is a 2D array of bytes. Each byte represents a single monochrome pixel. 00 is black and FF is white
    {
        public Point Location; // allow direct access to field so that .Offset method will work
        private byte[,] map;
        public int Height { get { return map.GetUpperBound(0) + 1; } }
        public int Width { get { return map.GetUpperBound(1) + 1; } }
        public int X { get { return Location.X; } }
        public int Y { get { return Location.Y; } }
        public byte[,] Map { get { return map; } }


        private CompactBitmap(Point location, byte[,] map) { this.Location = location; this.map = map; }

        public CompactBitmap(PackedBitmap p) // poop test me
        {
            Location = p.Location;
            map = new byte[p.Height, p.RowPixels];     // may be larger than the original map that gave rise to the packed
            byte mask = 0x80;
            for (int row = 0; row < p.Height; row++)
            {
                int column = 0;
                for (int i = 0; i < p.RowBytes; i++)
                {
                    byte data = p.Map[row, i];
                    for (int x = 0; x < 8; x++)
                    {
                        int foo = data & mask;
                        bool bar = foo == 0;
                        this.map[row, column++] = (data & mask) == 0 ? (byte)0xff : (byte)0x00;
                        mask = (byte)(mask >> 1);
                    }
                    mask = 0x80;
                }
            }
        }

        public CompactBitmap(Point location, Bitmap bitmap, Int32 backColor)
        {
            // the windows Bitmap uses 4 bytes per pixel.
            // copy the bitmapdata to a simpler form, 1 byte per pixel. 
            Location = location;
            int width = bitmap.Width;
            int height = bitmap.Height;
            Rectangle bitmapRectangle = new Rectangle(0, 0, width, height);
            BitmapData bitmapData = bitmap.LockBits(bitmapRectangle, ImageLockMode.ReadOnly, bitmap.PixelFormat);
            if (bitmapData.Stride < 0) throw new Exception("negative stride. what to do?");

            map = new byte[height, width];
            for (int row = 0; row < height; row++)
                for (int column = 0; column < width; column++)
                {
                    int offset = (row * width) + column;
                    int pixel = Marshal.ReadInt32(bitmapData.Scan0, (offset) << 2);
                    if (pixel == backColor || pixel == 0) map[row, column] = 0xff; // white is the color of the label. no print black!
                    // pixel == 0 actually happens. its not black its like unassigned? poop?
                    else map[row, column] = 0; // pixel on is black is zero
                }
            bitmap.UnlockBits(bitmapData);
        }


        public Bitmap ToBitmap(Color foreColor, Color backColor, PixelFormat pixelFormat = PixelFormat.Format32bppArgb)
        {
            // create a Windows Bitmap the same size as our own map
            Bitmap bitmap = new Bitmap(Width, Height, pixelFormat);
            for (int row = 0; row < Height; row++)
                for (int col = 0; col < Width; col++)
                    bitmap.SetPixel(col, row, map[row, col] == 0x00 ? foreColor : backColor);
            return bitmap;
        }

        internal Rectangle GetPixelBounds()
        {
            // returns a rectangle relative to the start of the bitmap.
            int colMin = Width;
            int colMax = 0;
            int rowMin = Height;
            int rowMax = 0;

            for (int row = 0; row < Height; row++)
                for (int col = 0; col < Width; col++)
                    if (map[row, col] == 0x00) // if the one byte pixel is black
                    {
                        if (col < colMin) colMin = col;
                        if (col > colMax) colMax = col;
                        if (row < rowMin) rowMin = row;
                        if (row > rowMax) rowMax = row;
                    }
            return new Rectangle(colMin, rowMin, (colMax - colMin) + 1, (rowMax - rowMin) + 1); // correct
        }

        internal CompactBitmap Clip(Rectangle limits) // poop the limits rectangle must be contained.
        {
            byte[,] clipped = new byte[limits.Height, limits.Width];
            for (int row = limits.Top, i = 0; row < limits.Bottom; row++, i++)
            {
                for (int col = limits.Left, j = 0; col < limits.Right; col++, j++)
                    clipped[i, j] = map[row, col];
            }
            return new CompactBitmap(this.Location.Plus(limits.Location), clipped);
        }

        internal CompactBitmap Rotate()
        {
            int height = Height;
            int width = Width;
            byte[,] rotated = new byte[width, height];  // reversed
            for (int row = 0, rev = height - 1; row < height; row++, rev--)
                for (int col = 0; col < width; col++)
                    rotated[col, rev] = map[row, col];
            return new CompactBitmap(this.Location.Rotate(), rotated);
        }

        public bool SameAs(CompactBitmap other)
        {
            if (this.Location != other.Location) return false;
            if (this.Height != other.Height) return false;
            if (this.Width != other.Width) return false;
            for (int i = 0; i < Height; i++)
                for (int j = 0; j < Width; j++)
                    if (map[i, j] != other.map[i, j]) return false;
            return true;
        }

        public override string ToString()
        {
            return "CompactBitmap Dump {0} x {1} (height x width)".WithArgs(Height, Width);
        }

    }




    public class PackedBitmap
    {
        private Point location;
        private byte[,] map;

        public int Height { get { return map.GetUpperBound(0) + 1; } }
        public int RowBytes { get { return map.GetUpperBound(1) + 1; } }
        public int RowPixels { get { return RowBytes << 3; } }
        public int X { get { return location.X; } }
        public int Y { get { return location.Y; } }
        public Point Location { get { return location; } set { location = value; } }
        public byte[,] Map { get { return map; } }


        public PackedBitmap(CompactBitmap compact)
        {
            this.location = compact.Location;

            int height = compact.Height;
            int width = compact.Width;
            Log.AppendSuccess($"BitPack height {height} x rowBytes {width}");

            int rowBytes = (width + 8) >> 3;
            int rowPixels = rowBytes << 3; // so yes after clipping the rules of bit packing means we will have extra pixels
            Log.AppendSuccess($"bitPack rowBytes {rowBytes} and rowPixels {rowPixels}");
            this.map = new byte[height, rowBytes];

            byte data = 0; byte mask = 0x80;

            for (int row = 0; row < height; row++)
            {
                int c = 0;
                for (int column = 0; column < rowPixels; column++)
                {
                    if (column >= width || compact.Map[row, column] == 0xff) data |= mask;
                    mask = (byte)(mask >> 1);
                    if (mask == 0)
                    {
                        mask = 0x80;
                        this.map[row, c++] = data;
                        data = 0;
                    }
                }
            }
        }

        public bool SameAs(PackedBitmap other)
        {
            if (this.location != other.Location) return false;
            if (this.Height != other.Height) return false;
            if (this.RowBytes != other.RowBytes) return false;
            for (int i = 0; i < this.Height; i++)
                for (int j = 0; j < this.RowBytes; j++)
                    if (this.map[i, j] != other.map[i, j]) return false;
            return true;
        }

        public PackedBitmap(int x, int y, byte[,] map) // unused
        {
            this.location = new Point(x, y);
            this.map = map;
        }

        public byte[] ToByte()
        {
            byte[] d = new byte[this.Height * this.RowBytes];
            int i = 0;
            for (int r = 0; r < this.Height; r++) for (int c = 0; c < this.RowBytes; c++) d[i++] = this.map[r, c];
            return d;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("PackedBitmap Information Dump");
            sb.AppendFormat("Location   {0}", this.location).AppendLine();
            sb.AppendFormat("Height {0}", this.Height).AppendLine();
            sb.AppendFormat("RowBytes {0}, RowPixels {1}", this.RowBytes, this.RowPixels).AppendLine();
            return sb.ToNuked();
        }
    }


    #endregion


    public sealed class nuZebraski
    {
        // singleton pattern static initialization
        // https://msdn.microsoft.com/en-us/library/ff650316.aspx

        private static readonly nuZebraski instance = new nuZebraski();

        public static nuZebraski Instance
        {
            get { return instance; }
        }

        private List<byte[]> zebra_commands;
        private List<Object> eObjects = new List<Object>();
        System.Text.ASCIIEncoding ascii;

        nuZebraski()
        {
            ascii = new System.Text.ASCIIEncoding();
        }


        #region public methods
        public void StartNewCommand()
        {
            zebra_commands = new List<byte[]>();
            eObjects = new List<Object>();
            string s = "\nN\nZB\n"; // N performs a Clear Image Buffer, ZB instructs the printer to print upside down and backwards
            zebra_commands.Add(ascii.GetBytes(s));
        }
        public void FactoryResetCommand()
        {
            string s = "^default\n";
            zebra_commands.Add(ascii.GetBytes(s));
        }

        public void SetPageSize(int labelWidth, int labelHeight, int gapHeight)
        {
            zebra_commands = new List<byte[]>();
            string s;
            s = "q{0}\nQ{1},{2}\nrN\n".WithArgs(labelWidth, labelHeight, gapHeight); // set width, disable double buffering
            //s = "rN\n"; // disable double buffering
            // seems to me we just need the paperspace cooridinates in pixels and thats about it
            zebra_commands.Add(ascii.GetBytes(s));
        }
        // poop so there are 2 commands that might apply, as well as turning off the printupside down feature
        // Q Set Form Length
        // Q100,22
        // 100 would be the label length (height)
        // 22 would be the gap between labels (height)

        // q Set Label Width
        // q200 where 200 would be the width of the label
        // there is some info in the manual about left aligned and desktop printers
        // I think our label printer is clearly a center aligned printer.

        // there is another command R which is an alternative to q for center aligned printers.
        // R Set Reference Point
        // R50,7
        // 50 sets the left margin




        public void AsciiTextCommand(int x, int y, string s)
        {
            int rotation = 0;
            int font = 4;
            int hMult = 1;
            int vMult = 1;
            char reverse = 'N';

            string ss = string.Format("A{0},{1},{2},{3},{4},{5},{6},\"{7}\"\n", x, y, rotation, font, hMult, vMult, reverse, s);
            zebra_commands.Add(ascii.GetBytes(ss));
        }

        public void AsciiTextCommand(Point P, string s) // unused
        {
            int rotation = 0;
            int font = 4;
            int hMult = 1;
            int vMult = 1;
            char reverse = 'N';

            string ss = string.Format("A{0},{1},{2},{3},{4},{5},{6},\"{7}\"\n", P.X, P.Y, rotation, font, hMult, vMult, reverse, s);
            zebra_commands.Add(ascii.GetBytes(ss));
        }

        public void AutosenseCommand()
        {
            string s = "xa\n";
            zebra_commands.Add(ascii.GetBytes(s));
        }

        public void DiagonalLineCommand(Point P, int h_length, int h_endpoint, int v_endpoint) // unused
        {
            string s = string.Format("LS{0},{1},{2},{3},{4}\n", P.X, P.Y, h_length, h_endpoint, v_endpoint);
            zebra_commands.Add(ascii.GetBytes(s));
        }

        public void StraightLineCommand(Point P, int wide, int high) // unused
        {
            string s = string.Format("LO{0},{1},{2},{3}\n", P.X, P.Y, wide, high);
            zebra_commands.Add(ascii.GetBytes(s));
        }

        // or poop draw this as a bitmap and add it to our resources?
        // so could we implement this as a graphics drawing to ech of our 2 alignment panels
        // hide the buttons as needed, and then will our existing bitmap extraction do it?
        // so the command send test pattern will quickly alter the controls by hiding and writing
        // directly to the panels graphcis, then pull out the image as a bitmap and send that to the printer and emulator?
        public void BoxCommand(Control control, int thick) // poop change 1/4/16 2 axis panel adjustment
        {
            string b = string.Format("X{0},{1},{2},{3},{4}\n", control.Bounds.Left, control.Bounds.Top, thick, control.Bounds.Right, control.Bounds.Bottom);
            // poop check. in Windows, Right and Bottom are points NOT IN your rectangle. What about EPL? we need to check this too be sure EPL doesnt consider
            // right and bottom to be points inside the rectangle
            zebra_commands.Add(ascii.GetBytes(b));
        }


        public void SendCommandsToPrinter(string printername)
        {
            Log.AppendInformation("SendCommandsToPrinter");
            foreach (byte[] b in zebra_commands) NativeMethods.SendBytesToPrinter(printername, b);
            NativeMethods.SendBytesToPrinter(printername, ascii.GetBytes("P1\n"));
        }


        public void AddTextBoxGraphicCommand(CompactBitmap compactBitmap)
        {
            PackedBitmap packedBitmap = new PackedBitmap(compactBitmap);
            string s = string.Format("GW{0},{1},{2},{3}\n", packedBitmap.X, packedBitmap.Y, packedBitmap.RowBytes, packedBitmap.Height);

            zebra_commands.Add(ascii.GetBytes(s));
            zebra_commands.Add(packedBitmap.ToByte());
            zebra_commands.Add(ascii.GetBytes("\n"));
        }
        public void AddEmulatorObject(Object o)
        {
            eObjects.Add(o); // for emulation purposes
        }
        #endregion


        internal void PaintEmulator(PaintEventArgs e)
        {
            Rectangle clipRectangle = e.ClipRectangle; // poop do we need this?
            Graphics graphics = e.Graphics;

            foreach (Object o in eObjects)
            {
                Rectangle? panelShape = o as Rectangle?;
                CompactBitmap compact = o as CompactBitmap;
                if (panelShape != null)
                {

                    graphics.FillRectangle(new SolidBrush(Color.White), (Rectangle)panelShape);
                }
                else if (compact != null)
                {
                    graphics.DrawImage(compact.ToBitmap(Color.Black, Color.LightGray), compact.Location);
                }
            }
        }

    }

}
