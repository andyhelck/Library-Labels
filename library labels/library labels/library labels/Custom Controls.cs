using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Library_Labels_Namespace
{
    [System.ComponentModel.DesignerCategory("")]

    #region ColorTabs
    public class ColorTabControl : TabControl
    {
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            TabPage page = this.TabPages[e.Index];
            e.Graphics.FillRectangle(new SolidBrush(page.BackColor), e.Bounds); // okay this gives us the pretty rainbow effect by grabbing the backcolor.

            Rectangle paddedBounds = e.Bounds;
            int yOffset = (e.State == DrawItemState.Selected) ? -2 : 1;
            paddedBounds.Offset(1, yOffset);
            TextRenderer.DrawText(e.Graphics, page.Text, Font, paddedBounds, page.ForeColor);
            base.OnDrawItem(e);
        }
    }

    #endregion

    #region Zebra Panel Emulator

    public class PanelZebraEmulator : Panel
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            nuZebraski.Instance.PaintEmulator(e);
            base.OnPaint(e);
        }
    }
    #endregion

    #region Zoom Free Rich TextBox

    public class FixedZoomRichTextBox : RichTextBox
    {
        //[System.Runtime.InteropServices.DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
        //static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        const int WM_MOUSEWHEEL = 0x020A;
        const int EM_SETZOOM = 0x04E1;

        // this control disables the control scroll wheel to prevent the user
        // from zooming the window and throwing off my margin controls
        // but why are we doing this? the wheel is really easy to use.
        // I bet its my measure string that is the problem...do we need
        // to call it with the zoom factor? well in fact we just scale the result
        // and that works fine. but our font size label is not updating and it flicers
        // an aweful lot. lets keep this turned off for not. 12/31/15.
        // but it is pretty cute if you have the right mouse.

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_MOUSEWHEEL)
            {
                if (Control.ModifierKeys == Keys.Control)
                    NativeMethods.SendMessage(this.Handle, EM_SETZOOM, UIntPtr.Zero, IntPtr.Zero);
            }
        }
    }


    #endregion

    #region PanelBox
    public class PanelBox : IDisposable // poop implement Idisposable according to code analysis
    {

        #region getting started
        // original panel sizes spine 190x285, pocket 532x285
        // original panel loci  spine 119,16   pocket 522,16

        Panel panel;
        RichTextBox textBox;
        PivotPointOptions pivot;
        LinkLabel labelFont;
        PanelBoxStyle style;
        bool autoMargin;
        bool orientLandscape;
        bool active = false;
        Panel alignmentPanel;
        ContextMenuStrip rightClickMenu;
        StringFunction callNumberFormatter;

        private Size borderSize; // like 2,2 for 3d border

        private Rectangle clientPortrait; // external coords
        private Rectangle clientLandscape; // in external coordinates
        // the panel itself will be larger or smaller depending on if there
        // need to be borders, we expand so as o keep the clientArea fixed
        bool overflow = false;


        public PanelBox(RichTextBox textBox, Size referenceSize, LinkLabel labelFont, PivotPointOptions pivot, Panel alignmentPanel, StringFunction callNumberFormatter)
        {
            this.textBox = textBox;
            if (!textBox.Size.Equals(referenceSize))
            {
                Log.AppendError($"PanelBox Error: Size mismatch {textBox.Size.ToString()} vs {referenceSize.ToString()}");
                textBox.Size = referenceSize;
            }
            panel = (Panel)textBox.Parent;
            this.labelFont = labelFont;
            this.pivot = pivot;
            this.callNumberFormatter = callNumberFormatter;
            this.alignmentPanel = alignmentPanel;

            textBox.Dock = DockStyle.None;
            if (textBox.BorderStyle != BorderStyle.None) "PanelBox Error, why are textbox borders on?".MsgBox();
            if (panel.BorderStyle == BorderStyle.None) "PanelBox Error, why are panel borders off?".MsgBox();
            textBox.MouseDown += panelBox_MouseDown;
            panel.MouseDown += panelBox_MouseDown;
            textBox.KeyDown += panelBox_KeyDown;
            textBox.KeyPress += panelBox_KeyPress;
            labelFont.LinkClicked += labelFont_LinkClicked;

            getClientRectanglesAndStyles();
            createRightClickMenu();
        }


        void panelBox_MouseDown(object sender, MouseEventArgs e)
        {
            //"\n{0} mousedown event fires".WithArgs(Name).Log();
            Focus();
        }

        void labelFont_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OnActivity(EventArgs.Empty);
            FontDialog fontDialog = new FontDialog();
            fontDialog.Font = Font;
            DialogResult result = DialogResult.Cancel;
            try
            {
                result = fontDialog.ShowDialog();
            }
            catch (ArgumentException a)
            {
                "Sorry, this font is not suported {0}".WithArgs(a.Message).MsgBox();
                return;
            }
            if (result == DialogResult.OK) Font = fontDialog.Font; // updates the fit and the label
        }



        private void getClientRectanglesAndStyles()
        {
            // okay don't assume the panel from the designer is portrait or landscape, it could be either
            borderSize = (panel.Size - panel.ClientRectangle.Size).Divide(2);
            Rectangle clientDrawn = new Rectangle(panel.Location + borderSize, panel.ClientRectangle.Size);
            Rectangle clientOther = clientDrawn; // something to start with anyway. If None selected, this is OK

            if (pivot != PivotPointOptions.None)
            {
                clientOther.Size = clientOther.Size.Rotate(); // okay thats easy

                int difference = clientDrawn.Width - clientDrawn.Height; // negative if drawn as portrait, positive if drawn landscape

                if (pivot == PivotPointOptions.TopLeft) {; } // yes that is easy
                else if (pivot == PivotPointOptions.TopRight) clientOther.Location += new Size(difference, 0);
                else if (pivot == PivotPointOptions.BottomLeft) clientOther.Location += new Size(0, -difference);
                else if (pivot == PivotPointOptions.BottomRight) clientOther.Location += new Size(difference, -difference);

            }

            if (clientDrawn.Size.IsTall())
            {
                clientPortrait = clientDrawn;
                clientLandscape = clientOther;
            }
            else
            {
                clientLandscape = clientDrawn;
                clientPortrait = clientOther;
            }

            style = new PanelBoxStyle();
            style.borderStyleActive = panel.BorderStyle; // from the designer
            style.borderStyleInActive = BorderStyle.None; // my decision

            style.backColorPanelActive_AutoMargins = textBox.BackColor; // SystemColors.Window;
            style.backColorPanelActive_ManualMargins = panel.BackColor; // Color.WhiteSmoke;
            style.backColorPanelInactive = panel.BackColor;             // Color.WhiteSmoke;

            style.backColorTextboxActive = textBox.BackColor;           // SystemColors.Window;        
            style.backColorInactive = panel.BackColor;                  // Color.WhiteSmoke;

            style.foreColorTextFits = textBox.ForeColor;                // SystemColors.WindowText;
            style.foreColorTextOverflow = Color.Crimson;                // my decision
            style.foreColorInactive = panel.ForeColor;                  // SystemColors.ControlDark;
        }


        #endregion

        #region display the panel and textbox

        private void setPanelSizeAndLocation()
        {
            panel.Bounds = orientLandscape ? clientLandscape : clientPortrait;
            if (active)
            {
                Rectangle bounds = panel.Bounds;
                bounds.Inflate(borderSize);
                panel.Bounds = bounds;
            }

            //"setPanelSizeAndLocation {0}".WithArgs(Name).Log();

            //"\nsetPanelSizeAndLocation\nCompute Bounds {0} {1}".WithArgs(orientLandscape ? "landscape" : "portrait", active ? "active" : "inactive").Log();
            //"Panel Bounds   {0}".WithArgs(panel.Bounds).Log();
            //"Panel Client   {0}".WithArgs(panel.ClientRectangle).Log();
            //"TextBox Bounds {0}".WithArgs(textBox.Bounds).Log();
        }

        public void SetBordersBackColor()
        {
            if (active) SetActiveBordersBackColor();
            else setInActiveBorderBackColor();
        }
        private void SetActiveBordersBackColor()
        {
            panel.Visible = false;
            panel.BorderStyle = style.borderStyleActive;
            setPanelSizeAndLocation();
            SetBackColorActiveAutoMargin();
            textBox.BackColor = style.backColorTextboxActive;
            panel.Visible = true;
        }
        public void SetBackColorActiveAutoMargin()
        {
            panel.BackColor = autoMargin ?
            style.backColorPanelActive_AutoMargins : style.backColorPanelActive_ManualMargins;
        }
        private void setInActiveBorderBackColor()
        {
            panel.Visible = false;
            panel.BorderStyle = style.borderStyleInActive;
            setPanelSizeAndLocation();
            panel.BackColor = textBox.BackColor = style.backColorInactive;
            panel.Visible = true;
        }


        // okay, the foreground color changes a lot. here it is

        public void SetForeColor()
        {
            if (active) setActiveForeColor();
            else setInActiveForeColor();
        }

        private void setActiveForeColor()
        {
            panel.ForeColor = textBox.ForeColor =
                overflow ? style.foreColorTextOverflow : style.foreColorTextFits;
        }

        private void setInActiveForeColor()
        {
            panel.ForeColor = textBox.ForeColor = style.foreColorInactive;
        }


        #endregion

        #region Copy, Paste, Cut, Right Click Menu
        private void panelBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                if (e.KeyCode == Keys.X || e.KeyCode == Keys.V)
                    textBox.TextChanged += textBox_TextChanged;
            }
            if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back) textBox.TextChanged += textBox_TextChanged;
        }

        private void panelBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            //"keyPress Event {0}".WithArgs(Name).Log();
            textBox.TextChanged += textBox_TextChanged;
        }

        private void textBox_TextChanged(object sender, EventArgs e)
        {
            textBox.TextChanged -= textBox_TextChanged;
            UpdateFit();
        }

        private void createRightClickMenu()
        {
            rightClickMenu = new ContextMenuStrip();
            rightClickMenu.Name = "rightClickMenu";
            rightClickMenu.Items.Add(new ToolStripMenuItem("Special Paste", null, SpecialPasteMenuItem_Click));
            rightClickMenu.Items.Add(new ToolStripSeparator());
            rightClickMenu.Items.Add(new ToolStripMenuItem("Select All", null, SelectAllMenuItem_Click));
            rightClickMenu.Items.Add(new ToolStripSeparator());
            rightClickMenu.Items.Add(new ToolStripMenuItem("Cut", null, CutMenuItem_Click));
            rightClickMenu.Items.Add(new ToolStripMenuItem("Copy", null, CopyMenuItem_Click));
            rightClickMenu.Items.Add(new ToolStripMenuItem("Paste", null, PasteMenuItem_Click));
            rightClickMenu.Items.Add(new ToolStripSeparator());
            rightClickMenu.Items.Add(new ToolStripMenuItem("Delete", null, DeleteMenuItem_Click));
            rightClickMenu.Items.Add(new ToolStripSeparator());
            rightClickMenu.Items.Add(new ToolStripMenuItem("Center", null, CenterAlignMenuItem_Click));
            rightClickMenu.Items.Add(new ToolStripMenuItem("Left Align", null, LeftAlignMenuItem_Click));

            rightClickMenu.Opening += rightClickMenu_Opening;
            panel.ContextMenuStrip = textBox.ContextMenuStrip = rightClickMenu;
        }

        void rightClickMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            bool anyText = Text.Length > 0;
            bool anySelection = SelectionLength > 0;
            bool leftAligned = TextAlignment == HorizontalAlignment.Left;
            bool centerAligned = TextAlignment == HorizontalAlignment.Center;
            //"updateFancyRightClickMenu {0}, anyText {1}, anySelection {2}, LeftAligned {3}, Center Aligned {4}"
            //    .WithArgs(Name, anyText, anySelection, leftAligned, centerAligned).Log();

            foreach (ToolStripItem item in rightClickMenu.Items)
            {
                string text = item.Text;
                if (text == "Select All") item.Enabled = anyText;
                else if (text == "Cut" || text == "Copy" || text == "Delete") item.Enabled = anySelection;
                else if (text == "Left Align") item.Enabled = !leftAligned;
                else if (text == "Center") item.Enabled = !centerAligned;

            }
            e.Cancel = false;
        }

        void SelectAllMenuItem_Click(object sender, EventArgs e)
        {
            textBox.SelectAll();
        }
        void CutMenuItem_Click(object sender, EventArgs e)
        {
            textBox.Cut();
            UpdateFit();
        }
        void CopyMenuItem_Click(object sender, EventArgs e)
        {
            textBox.Copy();
        }
        void PasteMenuItem_Click(object sender, EventArgs e)
        {
            textBox.Paste();
            UpdateFit();
        }
        void DeleteMenuItem_Click(object sender, EventArgs e) // textboxes, richtextboxes and panelboxes
        {
            textBox.SelectedText = string.Empty;
            UpdateFit();
        }

        void SpecialPasteMenuItem_Click(object sender, EventArgs e)
        {
            Text = callNumberFormatter(Clipboard.GetText());
        }

        void CenterAlignMenuItem_Click(object sender, EventArgs e)
        {
            textBox.Visible = false;
            TextAlignment = HorizontalAlignment.Center;
            UpdateFit("center align");
            OnTextAlignmentChanged(EventArgs.Empty);
            textBox.Visible = true;
        }
        void LeftAlignMenuItem_Click(object sender, EventArgs e)
        {
            textBox.Visible = false;
            TextAlignment = HorizontalAlignment.Left;
            UpdateFit("left align");
            textBox.Visible = true;
            OnTextAlignmentChanged(EventArgs.Empty);
        }

        #endregion

        #region Margin Commands

        public void adjustLeftMargin(int delta)    // manual control over margins
        {
            Point TextOrigin = textBox.Location;
            TextOrigin.X += delta;

            if (TextOrigin.X < 0) TextOrigin.X = 0;
            else if (TextOrigin.X >= panel.Width) TextOrigin.X = panel.Width;

            textBox.Location = TextOrigin;
            textBox.Size = panel.Size - new Size(TextOrigin);

            UpdateFit("margin command");
        }

        public void adjustTopMargin(int delta)    // typ -1 or 1
        {
            Panel panel = (Panel)textBox.Parent;
            Point TextOrigin = textBox.Location;
            TextOrigin.Y += delta;

            if (TextOrigin.Y < 0) TextOrigin.Y = 0;
            else if (TextOrigin.Y >= panel.Height) TextOrigin.Y = panel.Height;

            textBox.Location = TextOrigin;
            textBox.Size = panel.Size - new Size(TextOrigin);
            UpdateFit("margin command");
        }
        #endregion

        #region fit text into the panelbox

        public void UpdateFit(string tag = "unknown")
        {
            textBox.Size = calculateAutoSize();
            Rectangle pixelBounds = bitmapTextSize(); // rectangle inside texbox
            // start with existing location and new calculated size
            // assign the size to the actual textbox, we need its pixel reality
            int leftMargin, topMargin;

            if (autoMargin)
            {
                int leftDistance = (panel.ClientRectangle.Width - pixelBounds.Width) / 2;
                int topDistance = (panel.ClientRectangle.Height - pixelBounds.Height) / 2;

                leftMargin = leftDistance - pixelBounds.X;
                topMargin = topDistance - pixelBounds.Y;
                if (TextAlignment == HorizontalAlignment.Center) leftMargin = 0;
                // okay, well is this part of the problem? we set our leftmargin to zero. it still 
            }
            else
            {
                // manual mode is easy. Leave the margins alone. unless we need to fix them
                leftMargin = textBox.Location.X;
                topMargin = textBox.Location.Y;
                if (TextAlignment == HorizontalAlignment.Center || leftMargin < 0) leftMargin = 0;
                if (topMargin < 0) topMargin = 0;
            }
            textBox.Location = new Point(leftMargin, topMargin);
            Rectangle panelLimits = PrintableArea_TextBoxCoords(); // this is good.

            overflow = !panelLimits.Contains(pixelBounds);
            SetForeColor();
            //"Update Fit {0} {1}: autofit? {2} textalignment {3}".WithArgs(Name, tag, autoMargin, TextAlignment).Log();
            //"  result: textbox.bounds {0}, overflow? {1} ".WithArgs(textBox.Bounds, overflow).Log();
        }

        public Size calculateAutoSize() // 1/3/16 new update fit approach, see above
        {
            Graphics graphics = textBox.CreateGraphics();
            Font SelectionFont = textBox.SelectionFont;
            Size panelSize = panel.ClientRectangle.Size;
            if (SelectionFont == null) return panelSize;

            Size graphicSize = graphics.MeasureString(textBox.Text, SelectionFont).ToSize();
            graphics.Dispose();

            Size inflatedSize = new Size(graphicSize.Width, graphicSize.Height * 2);
            Size stretchedSize = inflatedSize.ExpandToAtLeast(panelSize);

            if (textBox.WordWrap) stretchedSize = stretchedSize.RestrictInWidth(panelSize);

            //"calculateAutoSize {0} -> {1} -> {2} [ps {3}]".WithArgs(graphicSize, inflatedSize, stretchedSize, panelSize).Log();
            return stretchedSize;
        }

        #endregion

        #region data to the printer

        private Rectangle PrintableArea_TextBoxCoords() // used for autofit() and printing.
        {
            Rectangle PA_TBC = new Rectangle(textBox.Location.Negate(), panel.ClientRectangle.Size);
            Rectangle printableArea = PA_TBC.TrimToFitContainer(textBox.Size);
            //"\nPrintableArea_TextBoxCoords\nbefore trimming  {0} with {1}\nafter trimming {2}"
            //.WithArgs(PA_TBC, textBox.Size, printableArea).Log();
            return printableArea;
        }


        private Rectangle bitmapTextSize()
        {
            Bitmap fullsize = WIN32_API_DrawToBitmap();
            CompactBitmap compactBitmap = new CompactBitmap(new Point(0, 0), fullsize, (Int32)textBox.BackColor.ToArgb());
            return compactBitmap.GetPixelBounds();
        }

        public CompactBitmap DrawToCompactBitmap()
        {
            //"Draw to Zebra Mapp Textbox is {0} x {1}".WithArgs(textBox.Width, textBox.Height).Log();

            CompactBitmap compactBitmap = new CompactBitmap(
                textBox.Location, WIN32_API_DrawToBitmap(), (Int32)textBox.BackColor.ToArgb());

            //"Draw to Zebra Mapp o is {0} x {1} height x width".WithArgs(compactBitmap.Height, compactBitmap.Width).Log();

            Rectangle pixelBounds = compactBitmap.GetPixelBounds();
            //"Draw to Zebra Mapp pixelBounds is {0}".WithArgs(pixelBounds).Log(); // these are bounds relative to the original control

            Rectangle limits = PrintableArea_TextBoxCoords(); // this is good as well
            //"Draw to Zebra Mapp Printable Panel limits is {0}".WithArgs(limits).Log();

            limits.Intersect(pixelBounds);
            //"Draw to Zebra Mapp limits pixelbound intersection is {0}".WithArgs(limits).Log();
            CompactBitmap clipped = compactBitmap.Clip(limits);

            if (RotatePrint)
            {
                clipped = clipped.Rotate();
            }
            //Log.AppendSuccess($"Draw to Zebra Mapp rotated is {clipped.Height} x {clipped.Width} height x width");
            clipped.Location.Offset(AlignmentBounds.Location);
            return clipped;
        }



        #endregion

        #region Draw to Bitmap

        private Bitmap WIN32_API_DrawToBitmap()
        {
            // Native DrawToBitmap is not properly supported for the RichTextBox, it just draws the border
            // testing shows this gets all the data in the textbox, even if it doesnt show on the screen
            int width = textBox.ClientRectangle.Width;
            int height = textBox.ClientRectangle.Height;

            Bitmap bmp = new Bitmap(width, height); // Format32bppArgb default
            const double inch = 14.4;

            using (Graphics gr = Graphics.FromImage(bmp))
            {
                IntPtr hDC = gr.GetHdc();
                Win32_API.FORMATRANGE fmtRange;
                Win32_API.RECT rect;
                IntPtr fromAPI;
                rect.top = 0; rect.left = 0;
                rect.bottom = (int)(bmp.Height + (bmp.Height * (bmp.HorizontalResolution / 100)) * inch);
                rect.right = (int)(bmp.Width + (bmp.Width * (bmp.VerticalResolution / 100)) * inch);

                fmtRange.chrg.cpMin = 0;
                fmtRange.chrg.cpMax = -1;
                fmtRange.hdc = hDC;
                fmtRange.hdcTarget = hDC;

                fmtRange.rc = rect;
                fmtRange.rcPage = rect;
                UIntPtr wParam = new UIntPtr(1);
                IntPtr lParam = Marshal.AllocCoTaskMem(Marshal.SizeOf(fmtRange));
                Marshal.StructureToPtr(fmtRange, lParam, false);

                fromAPI = NativeMethods.SendMessage(textBox.Handle, Win32_API.EM_FORMATRANGE, wParam, lParam);

                Marshal.FreeCoTaskMem(lParam);
                fromAPI = NativeMethods.SendMessage(textBox.Handle, Win32_API.EM_FORMATRANGE, wParam, new IntPtr(0));
                gr.ReleaseHdc(hDC);
            }
            return bmp;
        }

        #endregion

        #region Properties and simple Methods

        public void UpdateFontLabel()
        {
            this.labelFont.Text = Font.ToText();
        }
        public void Focus()
        {
            if (!textBox.Focused) textBox.Focus();
        }
        public bool OrientLandscape
        {
            get { return orientLandscape; }
            set
            {
                orientLandscape = value;
                setPanelSizeAndLocation();
            }
        }

        public Font Font
        {
            get { return textBox.Font; }
            set
            {
                textBox.Font = value;
                //"panelBox.Font() property changed to {0}".WithArgs(textBox.Font.ToText()).Log();
                UpdateFit();
                UpdateFontLabel();
                return;
            }
        }

        public bool AutoMargin
        {
            get { return autoMargin; }
            set
            {
                autoMargin = value;
                //SetBackColorActiveAutoMargin();
                //UpdateFit("automargin");
            }
        }

        public bool RotatePrint { get { return alignmentPanel.Size.IsWide() ^ OrientLandscape; } }
        public Rectangle AlignmentBounds { get { return alignmentPanel.Bounds; } }
        public int SelectionLength { get { return textBox.SelectionLength; } }
        public Point Margin
        {
            get { return textBox.Location; }
            set { textBox.Location = value; }
        }
        public HorizontalAlignment TextAlignment
        {
            get { return GetTextAlignment(false); }
            set { RestoreTextAlignment(value); }
        }
        private HorizontalAlignment savedTextAlignment;
        public bool WordWrap { get { return textBox.WordWrap; } set { textBox.WordWrap = value; } }


        internal HorizontalAlignment GetTextAlignment(bool keep = false)
        {
            HorizontalAlignment value;
            //textBox.SelectAll();
            value = textBox.SelectionAlignment;
            //textBox.DeselectAll();
            if (keep) savedTextAlignment = value;
            return value;
        }
        internal void SetTextAlignment()
        {
            textBox.SelectAll();
            textBox.SelectionAlignment = savedTextAlignment;
            textBox.DeselectAll();
        }
        internal void RestoreTextAlignment(HorizontalAlignment value)
        {
            textBox.SelectAll();
            textBox.SelectionAlignment = value;
            textBox.DeselectAll();
        }

        public string Text
        {
            get
            {
                return textBox.Text;
            }
            set
            {
                // poop do we need to preserve selection here?
                // 
                GetTextAlignment(true); // keep the variable handy
                textBox.Text = value;
                SetTextAlignment();
            }
        }

        public bool CanAdjustLeftMarginPlus { get { return textBox.Location.X < panel.Width - textBox.Font.Height; } }
        public bool CanAdjustLeftMarginMinus { get { return textBox.Location.X > 0; } }
        public bool CanAdjustTopMarginPlus { get { return textBox.Location.Y < panel.Height - textBox.Font.Height; } }
        public bool CanAdjustTopMarginMinus { get { return textBox.Location.Y > 0; } }

        public bool Active { get { return active; } set { active = value; } }
        public Color ForeColor { get { return textBox.ForeColor; } }
        public string Name { get { return panel.Name; } }

        #endregion

        #region custom events

        public event EventHandler TextAlignmentChanged;

        protected virtual void OnTextAlignmentChanged(EventArgs e)
        {
            EventHandler handler = this.TextAlignmentChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public event EventHandler Activity;

        protected virtual void OnActivity(EventArgs e)
        {
            EventHandler handler = this.Activity;
            if (handler != null)
            {
                handler(this, e);
            }
        }
        #endregion

        #region Dispose
        public void Dispose()
        {
            rightClickMenu.Dispose();
            GC.SuppressFinalize(this);
        }
        #endregion
    }
    #endregion

    #region Pivot Enumeration

    public enum PivotPointOptions
    {
        None,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    #endregion

    #region PanelBoxStyle
    public struct PanelBoxStyle
    {
        public BorderStyle borderStyleActive;
        public BorderStyle borderStyleInActive;

        public Color backColorPanelActive_AutoMargins;
        public Color backColorPanelActive_ManualMargins;
        public Color backColorPanelInactive;

        public Color backColorTextboxActive;
        public Color backColorInactive;

        public Color foreColorTextFits;
        public Color foreColorTextOverflow;
        public Color foreColorInactive;
    }
    #endregion

    #region Custom Buttons
    public class RovingButton
    {
        Button button;
        int jump;
        string leftArrow;
        string rightArrow;

        string baseText;
        Point location;
        int direction = 0; // -1, 0, 1

        public RovingButton(Button button, int jump = 10, string leftArrow = "", string rightArrow = "")
        {
            this.button = button;
            this.jump = jump;
            this.leftArrow = leftArrow;
            this.rightArrow = rightArrow;
            baseText = button.Text.Trim();
            location = button.Location;
        }
        public void SetBaseText(string s)
        {
            baseText = s;
            if (direction < 0) LookLeft();
            if (direction > 0) LookRight();
            else DontLook();
        }

        public void LookLeft()
        {
            button.Text = "{1}{0}".WithArgs(baseText, leftArrow);
            button.Location = location + new Size(-jump, 0);
            button.Enabled = true;
        }
        public void DontLook()
        {
            button.Text = baseText;
            button.Location = location;
            button.Enabled = false;
        }
        public void LookRight()
        {
            button.Text = "{0}{1}".WithArgs(baseText, rightArrow);
            button.Location = location + new Size(jump, 0);
            button.Enabled = true;
        }
    }
    #endregion

}
