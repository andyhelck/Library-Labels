using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;



// june 2020 playing around with proper installer options
// I downloaded an addon to Visual Studio for installer projects but actually there is a one click option
// that seems perfectly suitable. https://www.youtube.com/watch?v=t4BTLdIMYEY
// seems like I need a bona fide security key
// and a website to host this at.
// so the first requirement is to 'sign the ClickOnce manifest' with a certification
// my temporary certification from the other day already expired, and its not 'trusted'.
// I need to 'install' my certificate in microsofts trusted whatever. huh?
// This CA Root Certificate is not trusted
// whatever a certificate is, it must be properly "installed"
// a 'self signed certificate' doesn't have enough juice
// there is a certification chain, hence perhaps the 'root' reference
// this is a web server issue in many google searches
// https://knowledge.digicert.com/solution/SO16297.html#:~:text=A%20certificate%20chain%20is%20an,and%20all%20CA's%20are%20trustworthy.
// https://docs.microsoft.com/en-us/visualstudio/ide/how-to-sign-application-and-deployment-manifests?view=vs-2019
// select from Store isnt what it sounds like. Its not like the App Store, I think it means a cache of known certificates available to this project
// https://docs.microsoft.com/en-us/visualstudio/deployment/clickonce-security-and-deployment?view=vs-2019
// I need to learn about 'Authenticode'
// https://docs.microsoft.com/en-us/visualstudio/deployment/clickonce-and-authenticode?view=vs-2019
// not cheap: https://www.digicert.com/code-signing/microsoft-authenticode.htm


// a note on record numbers
// bib numbers are 7 digits + checkdigit = 8 displayed
// item numbers are 8 digits + checkdigit = 9 displayed
// order numbers are 7 digits + checkdigit = 8 displayed


namespace Library_Labels_Namespace
{
    using Props = Properties.Settings;
    public delegate string StringFunction(string s);


    public partial class Library_Labels_Class : Form
    {
        bool DisablePrinterDebug = !true; // programmer only -- change this here as needed to save labels
        bool groupBoxData_Enabled = false; // I got rid of the box but I need the logic element

        #region Getting Started

        ContextMenuStrip basicRightClickMenu;
        RovingButton[] buttonRovers;
        RovingButton RovingButtonQuickFont1;
        RovingButton RovingButtonQuickFont2;
        RovingButton RovingButtonQuickFont3;
        RovingButton RovingButtonQuickFont4;

        PanelBox spineBox;
        PanelBox pocketBox;
        PanelBox activeBox;
        bool suppressCheckStateChanged = false;
        bool suppressPanelBoxEnter = false;

        DataTable userDataTable;
        List<string> parseErrors;
        DataGridViewRow LastRowLoaded;
        DGV_UserMode userMode = DGV_UserMode.DGV_Empty;

        SimpleClient simpleClient;
        bool simpleClientWorking
        {
            get
            {
                return simpleClient == null ? false : (simpleClient.HasAccess);
            }
        }
        Regex regexBarcode;

        FormSearchAPI formSearchAPI;

        public Library_Labels_Class()
        {
            InitializeComponent();
            insertLogPage();

            groupBoxZebra.Enabled =
            toolStripButtonSelectPrinter.Enabled =
            !DisablePrinterDebug;

            panelZebraEmulator.Visible = false;
            panelPaperSpace.Visible = true;

            if (panelPaperSpace.Size != panelZebraEmulator.Size) "PaperSpace and ZebraEmulator dont match".MsgBox();
            createRovingButtons();
            createBasicRightClickMenu();

            spineBox = new PanelBox(richTextBoxSpine, new Size(190, 285), linkLabelSpineFont, PivotPointOptions.TopRight, panelSpineAlignment, formatCallNumber);
            pocketBox = new PanelBox(richTextBoxPocket, new Size(532, 285), linkLabelPocketFont, PivotPointOptions.None, panelPocketAlignment, formatCallNumber);
            spineBox.TextAlignmentChanged += panelBox_TextAlignmentChanged;
            spineBox.Activity += panelBox_Activity;
            pocketBox.TextAlignmentChanged += panelBox_TextAlignmentChanged;
            pocketBox.Activity += panelBox_Activity;

            getDirectControlDataFromSettings();

            pocketBox.OrientLandscape = true; // our program doesnt (yet) support any other orientation
            getSierraApiControlsFromSettings();
            createBarcodeRegex(Props.Default.BarcodeRegexPattern);

            spineBox.UpdateFontLabel();
            pocketBox.UpdateFontLabel();

            userDataTable = new DataTable(); // part of the data grid view
            parseErrors = new List<string>();

            updateButtonSpineOrientation_Text();
            setGroupBoxVisibility(); // margins or fonts

            sayPrinterName();
            boxActivateAndUpdateFitAndFocus();

            simpleClient = new SimpleClient(
                Props.Default.SierraAPI_Server,
                Props.Default.SierraAPI_Key,
                Props.Default.SierraAPI_Secret,
                false);
            // poop the access code doesn't last forever. Do I need to keep refreshing this somehow? it lasts 60 minutes, then what?
            formSearchAPI = new FormSearchAPI();
            ActiveControl = textBoxSearch;
        }

        void insertLogPage()
        {
            // we don't use the designer to create the Log RTB, instead its defined in the static Log class
            // and then we insert it into our controls. Of course I just copied designer code and change the name

            this.tabPageLog.Controls.Add(Log.RichTextBox);

            Log.RichTextBox.AcceptsTab = true;
            Log.RichTextBox.BackColor = System.Drawing.Color.Silver;
            Log.RichTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            Log.RichTextBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            Log.RichTextBox.Location = new System.Drawing.Point(0, 0);
            Log.RichTextBox.Name = "richTextBoxLog";
            Log.RichTextBox.Size = new System.Drawing.Size(1066, 336);
            Log.RichTextBox.TabIndex = 0;
            Log.RichTextBox.Text = "";

        }



        void createRovingButtons()
        {
            buttonRovers = new RovingButton[]
            {
                new RovingButton(buttonFontBigger, 30, "<", ">"),
                new RovingButton(buttonFontSmaller, 30, "<", ">"),

                RovingButtonQuickFont1 = new RovingButton(buttonRegular, 30, "<<", ">>"),
                RovingButtonQuickFont2 = new RovingButton(buttonNarrow, 30, "<<", ">>"),
                RovingButtonQuickFont3 = new RovingButton(buttonBold, 30, "<<", ">>"),
                RovingButtonQuickFont4 = new RovingButton(buttonBlack, 30, "<<", ">>"),

                new RovingButton(buttonTopMarginMinus, 30, "<<", ">>"),
                new RovingButton(buttonLeftMarginMinus, 30, "<<", ">>"),
                new RovingButton(buttonLeftMarginPlus, 30, "<<", ">>"),
                new RovingButton(buttonTopMarginPlus, 30, "<<", ">>")
            };
        }
        #endregion

        #region panelBox custom events

        void panelBox_Activity(object sender, EventArgs e)
        {
            boxActivateAndUpdateFitAndFocus(sender as PanelBox);
        }

        void panelBox_TextAlignmentChanged(object sender, EventArgs e)
        {
            updateMarginControls();
        }

        #endregion

        #region Intercept Windows Command Keys
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.P))
            {
                doPrintJob();
                focusControl("print");
                return true;
            }
            else if (keyData == (Keys.Control | Keys.F))
            {
                focusControl("CTRL-F"); // change color and focus to textBoxSearch
                return true;
            }
            else if (keyData == Keys.Tab)
            {
                if (myColorTabControl.SelectedTab == tabPagePreview)
                {
                    if (activeBox == spineBox) boxActivateAndUpdateFitAndFocus(pocketBox);
                    else boxActivateAndUpdateFitAndFocus();
                }
                return true;
            }
            Control c = this.ActiveControl;
            if (c as TextBoxBase == null) System.Media.SystemSounds.Beep.Play();

            return base.ProcessCmdKey(ref msg, keyData);
        }
        #endregion

        #region Cut Copy Paste for simple (rich)textboxes


        private void createBasicRightClickMenu()
        {
            basicRightClickMenu = new ContextMenuStrip();
            basicRightClickMenu.Items.Add(new ToolStripMenuItem("Select All", null, SelectAllMenuItem_Click));
            basicRightClickMenu.Items.Add(new ToolStripSeparator());
            basicRightClickMenu.Items.Add(new ToolStripMenuItem("Cut", null, CutMenuItem_Click));
            basicRightClickMenu.Items.Add(new ToolStripMenuItem("Copy", null, CopyMenuItem_Click));
            basicRightClickMenu.Items.Add(new ToolStripMenuItem("Paste", null, PasteMenuItem_Click));
            basicRightClickMenu.Items.Add(new ToolStripSeparator());
            basicRightClickMenu.Items.Add(new ToolStripMenuItem("Delete", null, DeleteMenuItem_Click));
            basicRightClickMenu.Items.Add(new ToolStripMenuItem("Parse & Load Table", null, ReLoadMenuItem_Click));
            basicRightClickMenu.Opening += basicRightClickMenu_Opening;


            textBoxSearch.ContextMenuStrip =

            textBoxDataDelimiter.ContextMenuStrip =
            textBoxDataQualifier.ContextMenuStrip =
            textBoxDataRFD.ContextMenuStrip =
            textBoxFirstColumnName.ContextMenuStrip =
            textBoxDefaultRFD.ContextMenuStrip =

            richTextBoxRawData.ContextMenuStrip =
            //richTextBoxLog.ContextMenuStrip =
            Log.RichTextBox.ContextMenuStrip =

            //textBoxLabelDataFile.ContextMenuStrip =

            basicRightClickMenu;
        }

        void basicRightClickMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ContextMenuStrip menu = sender as ContextMenuStrip;
            TextBoxBase tbb = menu.SourceControl as TextBoxBase;

            // preserve the selection and length in case this is the log textbox
            bool anyText = Text.Length > 0;
            bool anySelection = tbb.SelectionLength > 0;
            foreach (ToolStripItem item in menu.Items)
            {
                string text = item.Text;
                if (text == "Select All") item.Enabled = anyText;
                else if (text == "Cut" || text == "Copy" || text == "Delete") item.Enabled = anySelection;
                if (text == "Parse & Load Table") item.Visible = (tbb == richTextBoxRawData); 
            }
            e.Cancel = false;
        }


        TextBoxBase wutTextBoxBase(object sender)
        {
            if (sender is ToolStripItem)
            {
                //"wutTBB yes sender is ToolStripItem".Log();
                if ((sender as ToolStripItem).Owner is ContextMenuStrip)
                {
                    ContextMenuStrip menuStrip = (sender as ToolStripItem).Owner as ContextMenuStrip;
                    //"wutTBB yes sender has an owner who is menustrip".Log();
                    if (menuStrip.SourceControl is TextBoxBase) return menuStrip.SourceControl as TextBoxBase;
                    foreach (Control child in menuStrip.SourceControl.Controls)
                        if (child is TextBoxBase) return child as TextBoxBase; ;
                    return null;
                }
                else "wutTBB NO sender is not owned by menustrip".MsgBox();
            }
            return null;
        }

        void SelectAllMenuItem_Click(object sender, EventArgs e)
        {
            TextBoxBase tbb = wutTextBoxBase(sender);
            if (tbb != null) tbb.SelectAll();
        }
        void CutMenuItem_Click(object sender, EventArgs e)
        {
            TextBoxBase tbb = wutTextBoxBase(sender);
            if (tbb != null) tbb.Cut();
        }
        void CopyMenuItem_Click(object sender, EventArgs e)
        {
            TextBoxBase tbb = wutTextBoxBase(sender);
            bool anyText = Text.Length > 0;
            bool anySelection = tbb.SelectionLength > 0;
            if (tbb != null) tbb.Copy();
        }

        void PasteMenuItem_Click(object sender, EventArgs e)
        {
            TextBoxBase tbb = wutTextBoxBase(sender);
            if (tbb != null) tbb.Paste(); // for simple textboxes
        }

        void DeleteMenuItem_Click(object sender, EventArgs e) // textboxes, richtextboxes and panelboxes
        {
            TextBoxBase tbb = wutTextBoxBase(sender);
            if (tbb != null) tbb.SelectedText = string.Empty;
        }

        void ReLoadMenuItem_Click(object sender, EventArgs e)
        {
            TextBoxBase tbb = wutTextBoxBase(sender);
            if (tbb != null)
            {
                if (parseRawDataToTable(richTextBoxRawData.Lines, userDataTable, parseErrors))
                {
                    loadDGVfromDataTable(userDataTable, dataGridViewPrintAndPreview);
                    loadDGVfromDataTable(userDataTable, dataGridViewFileImportSettings);
                    focusControl("data file loaded");
                    "Data Table Updated".MsgBox(MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
        #endregion

        #region Main Menu and File Loading

        private void Library_Labels_Class_Shown(object sender, EventArgs e)
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1) /*loaded = */openLabelDataFile(args[1]);
        }

        private bool openLabelDataFile(string filename) // if successful, sets textbox
        // called from label browse. which opens the file. which doesnt care if its bullshit.
        // called on startup (form shown) either from the checkbox or from command line args
        {
            if (File.Exists(filename))
            {
                this.UseWaitCursor = true;
                this.Cursor = Cursors.WaitCursor;
                this.Enabled = false;
                loadRawText(filename);
                if (parseRawDataToTable(richTextBoxRawData.Lines, userDataTable, parseErrors))
                {
                    loadDGVfromDataTable(userDataTable, dataGridViewPrintAndPreview);
                    loadDGVfromDataTable(userDataTable, dataGridViewFileImportSettings);
                    focusControl("data file loaded");
                }
                this.Enabled = true;
                this.UseWaitCursor = false;
                this.Cursor = Cursors.Default;
            }
            else
            {
                "Data File Not Found\n{0}".WithArgs(filename).MsgBox(MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return false;
        }

        private void toolStripButtonFileOpen_Click(object sender, EventArgs e) // top menu button. Open the file. Ask to save name
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.Title = "Open File";
            openFileDialog1.InitialDirectory = Environment.SpecialFolder.Desktop.ToString();
            openFileDialog1.CheckFileExists = true;
            openFileDialog1.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
            openFileDialog1.FilterIndex = 1;
            openFileDialog1.RestoreDirectory = true;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                this.UseWaitCursor = true;
                this.Cursor = Cursors.WaitCursor;
                openLabelDataFile(openFileDialog1.FileName);
                // poop we really should check that this worked before offering to save the file
                //if (checkBoxLoadLabelDataAutomatically.Checked && openFileDialog1.FileName != textBoxLabelDataFile.Text)
                //{
                //    DialogResult result = "File Opened. Do you want to save the name and open automatically?"
                //        .MsgBox(MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                //    if (result == DialogResult.Yes) textBoxLabelDataFile.Text = openFileDialog1.FileName;
                //}
            }
            this.UseWaitCursor = false;
            this.Cursor = Cursors.Default;
        }

        void buttonChoosePrinter_Click(object sender, EventArgs e)
        {
            this.UseWaitCursor = true;
            this.Cursor = Cursors.WaitCursor;
            PrintDialog printDialog = new PrintDialog();
            printDialog.PrinterSettings = new PrinterSettings();


            printDialog.PrinterSettings.PrinterName = Props.Default.PrinterName;
            if (DialogResult.OK == printDialog.ShowDialog(this))
            {
                Props.Default.PrinterName = printDialog.PrinterSettings.PrinterName;
                if (printDialog.PrinterSettings.PrintToFile) Props.Default.PrinterName = "emulate";
            }
            sayPrinterName();
            this.UseWaitCursor = false;
            this.Cursor = Cursors.Default;
        }

        void sayPrinterName()
        {
            buttonPrint.Text = "Print\n{0}".WithArgs(DisablePrinterDebug ? "(emulate mode)" : Props.Default.PrinterName);
        }

        #endregion

        #region edit box margins, manual and automatic control

        private void buttonLeftMarginPlus_Click(object sender, EventArgs e)
        {
            activeBox.adjustLeftMargin(5);
            updateMarginControls();

        }

        private void buttonLeftMarginMinus_Click(object sender, EventArgs e)
        {
            activeBox.adjustLeftMargin(-5);
            updateMarginControls();

        }

        private void buttonTopMarginPlus_Click(object sender, EventArgs e)
        {
            activeBox.adjustTopMargin(5);
            updateMarginControls();
        }

        private void buttonTopMarginMinus_Click(object sender, EventArgs e)
        {
            activeBox.adjustTopMargin(-5);
            updateMarginControls();

        }


        private void checkBoxAutoMargins_CheckStateChanged(object sender, EventArgs e)
        {
            if (suppressCheckStateChanged) return;

            //"\nautoMargin toggled by user: {0}".WithArgs(checkBoxAutoMargins.Checked).Log();
            activeBox.AutoMargin = checkBoxAutoMargins.Checked;
            activeBox.SetBackColorActiveAutoMargin();
            activeBox.UpdateFit("autoMargin CheckBox Changed");
            updateMarginControls();
        }

        private void setCheckBoxAutoMarginState(bool value) // allows value to be set without triggering event above
        {
            suppressCheckStateChanged = true;
            checkBoxAutoMargins.Checked = activeBox.AutoMargin;
            suppressCheckStateChanged = false;
        }

        private void updateMarginControls()
        {
            if (activeBox == null)
            {
                checkBoxAutoMargins.Enabled =
                buttonLeftMarginPlus.Enabled =
                buttonLeftMarginMinus.Enabled =
                buttonTopMarginPlus.Enabled =
                buttonTopMarginMinus.Enabled =
                false;
            }
            else
            {
                checkBoxAutoMargins.Enabled = true;
                setCheckBoxAutoMarginState(activeBox.AutoMargin);

                bool CanLeft = !activeBox.AutoMargin && activeBox.TextAlignment != HorizontalAlignment.Center;
                bool CanTop = !activeBox.AutoMargin;

                buttonLeftMarginPlus.Enabled = CanLeft && activeBox.CanAdjustLeftMarginPlus;
                buttonLeftMarginMinus.Enabled = CanLeft && activeBox.CanAdjustLeftMarginMinus;

                buttonTopMarginPlus.Enabled = CanTop && activeBox.CanAdjustTopMarginPlus;
                buttonTopMarginMinus.Enabled = CanTop && activeBox.CanAdjustTopMarginMinus;
            }
        }

        #endregion

        #region Settings

        // In this program, we have 2 kinds of settings that we treat rather differently
        // Our Direct controls, like the Spine and Pocket textboxes are controlled by the user, and these directly effect the printed labels
        // So these values are only saved in Properties when we Open() and Close() the form
        // also included in this is all the formatting settings for when we open a CSV file

        // On the other hand, our Buffered settings get saved whenever a textbox or checkbox changes, and the code works with the Properties
        // this gives us a little distance to validate input, as well as being closer to a "model/view" paradigm

        void getDirectControlDataFromSettings() // Open()
        {
            // 2 fields for orientation makes sense to use the controls directly
            spineBox.OrientLandscape = Props.Default.orientSpineLandscape;
            pocketBox.OrientLandscape = Props.Default.orientPocketLandscape; // not used, but along for the ride

            // 2 fields for font. Access the rtb directly rather than the panelBox property
            richTextBoxSpine.Font = Props.Default.SpineFont;
            richTextBoxPocket.Font = Props.Default.PocketFont;

            // 4 for margins
            // okay these trigger an update fit. what needs to happen to suppress that?
            spineBox.AutoMargin = Props.Default.SpineAutoMargin;
            pocketBox.AutoMargin = Props.Default.PocketAutoMargin;
            spineBox.Margin = Props.Default.SpineMargin;
            pocketBox.Margin = Props.Default.PocketMargin;

            // 2 fields for text horizontal alignment
            spineBox.TextAlignment = Props.Default.SpineTextAlignment;
            pocketBox.TextAlignment = Props.Default.PocketTextAlignment;

            // 2 fields for printer alignment
            panelSpineAlignment.Location = Props.Default.SpineAlignmentPanelLocation;
            panelPocketAlignment.Location = Props.Default.PocketAlignmentPanelLocation;
            // 12 settings 
            
            
            
            // 13 settings just to read the data
            checkBoxFillPocketDGV.Checked = Props.Default.FillPocketFromDGV;
            checkBoxCleanFields.Checked = Props.Default.SierraCleanFields;
            checkBoxExpandEscapeChars.Checked = Props.Default.ExpandEscapeChars;
            checkBoxWrapPocketBox.Checked = richTextBoxPocket.WordWrap = Props.Default.WordWrapPocketBox;

            radioButtonSpecifyDQR.Checked = Props.Default.SpecifyDelimitersAndQualifier;
            radioButtonSpecifyFCN.Checked = Props.Default.SpecifyFirstColumnName;

            radioButtonFirstRepeatedFieldOnly.Checked = Props.Default.FilterFirstRepeatOnly;
            radioButtonIncludeAllRepeatedFields.Checked = Props.Default.FilterIncludeAllRepeats;

            textBoxDataDelimiter.Text = Props.Default.DataDelimiter;
            textBoxDataQualifier.Text = Props.Default.DataQualifier;
            textBoxDataRFD.Text = Props.Default.DataRepeatFieldDelimiter;

            textBoxFirstColumnName.Text = Props.Default.DataFirstColumnName;
            textBoxDefaultRFD.Text = Props.Default.DataDefaultRFD;

            checkBoxRemoveSubFieldIndicators.Checked = Props.Default.RemoveSubFieldIndicators;
            checkBoxStackSpaces.Checked = Props.Default.StackSpaces;
            checkBoxStackDots.Checked = Props.Default.StackDots;
            checkBoxUnStackAmpersands.Checked = Props.Default.UnStackAmpersands;
            checkBoxWrapSpineBox.Checked = richTextBoxSpine.WordWrap = Props.Default.WordWrapSpineBox;


            textBoxQuickFontName1.Text = Props.Default.QuickFontName1;
            textBoxQuickFontName2.Text = Props.Default.QuickFontName2;
            textBoxQuickFontName3.Text = Props.Default.QuickFontName3;
            textBoxQuickFontName4.Text = Props.Default.QuickFontName4;

            linkLabelQuickFont1.Text = Props.Default.QuickFont1.ToText();
            linkLabelQuickFont2.Text = Props.Default.QuickFont2.ToText();
            linkLabelQuickFont3.Text = Props.Default.QuickFont3.ToText();
            linkLabelQuickFont4.Text = Props.Default.QuickFont4.ToText();


        }

        void putDirectControlDataToSettings() // Close()
        {
            // 2 fields for orientation
            Props.Default.orientSpineLandscape = spineBox.OrientLandscape;
            Props.Default.orientPocketLandscape = pocketBox.OrientLandscape;

            // 2 fields for font. Use panelBox or rtb property it doesnt matter
            Props.Default.SpineFont = spineBox.Font;
            Props.Default.PocketFont = pocketBox.Font;

            // 4 fields for margins
            Props.Default.SpineAutoMargin = spineBox.AutoMargin;
            Props.Default.PocketAutoMargin = pocketBox.AutoMargin;
            Props.Default.SpineMargin = spineBox.Margin;
            Props.Default.PocketMargin = pocketBox.Margin;

            // 2 fields for text text alignment
            Props.Default.SpineTextAlignment = spineBox.TextAlignment;
            Props.Default.PocketTextAlignment = pocketBox.TextAlignment;
 
            // 2 for printer alignment
            Props.Default.SpineAlignmentPanelLocation = panelSpineAlignment.Location;
            Props.Default.PocketAlignmentPanelLocation = panelPocketAlignment.Location;
            // 12 settings 

            
            
            // 13 fields just to read the data in from CSV file!
            Props.Default.SpecifyDelimitersAndQualifier = radioButtonSpecifyDQR.Checked;
            Props.Default.SpecifyFirstColumnName = radioButtonSpecifyFCN.Checked;

            Props.Default.FillPocketFromDGV = checkBoxFillPocketDGV.Checked;
            Props.Default.SierraCleanFields = checkBoxCleanFields.Checked;
            Props.Default.ExpandEscapeChars = checkBoxExpandEscapeChars.Checked;
            Props.Default.WordWrapPocketBox = checkBoxWrapPocketBox.Checked;

            Props.Default.FilterFirstRepeatOnly = radioButtonFirstRepeatedFieldOnly.Checked;
            Props.Default.FilterIncludeAllRepeats = radioButtonIncludeAllRepeatedFields.Checked;

            Props.Default.DataDelimiter = textBoxDataDelimiter.Text;
            Props.Default.DataQualifier = textBoxDataQualifier.Text;
            Props.Default.DataRepeatFieldDelimiter = textBoxDataRFD.Text;

            Props.Default.DataFirstColumnName = textBoxFirstColumnName.Text;
            Props.Default.DataDefaultRFD = textBoxDefaultRFD.Text;

            Props.Default.RemoveSubFieldIndicators = checkBoxRemoveSubFieldIndicators.Checked;
            Props.Default.StackSpaces = checkBoxStackSpaces.Checked;
            Props.Default.StackDots = checkBoxStackDots.Checked;
            Props.Default.UnStackAmpersands = checkBoxUnStackAmpersands.Checked;
            Props.Default.WordWrapSpineBox = checkBoxWrapSpineBox.Checked;

            Props.Default.QuickFontName1 = textBoxQuickFontName1.Text;
            Props.Default.QuickFontName2 = textBoxQuickFontName2.Text;
            Props.Default.QuickFontName3 = textBoxQuickFontName3.Text;
            Props.Default.QuickFontName4 = textBoxQuickFontName4.Text;

            // poop we need to save the font itself!

        }

        void getSierraApiControlsFromSettings() // Open() and as needed
        {
            // I want to be able to validate these fields, and perhaps offer rollback if they don't work
            textBoxSierraAPI_ServerAddress.Text = Props.Default.SierraAPI_Server;
            textBoxSierraAPI_Key.Text = Props.Default.SierraAPI_Key;
            textBoxSierraAPI_Secret.Text = Props.Default.SierraAPI_Secret;
            textBoxBarcodeRegex.Text = Props.Default.BarcodeRegexPattern;
            
        }


        void putSierraApiControlsToSettings() // Close() and as needed
        {
            // I want to be able to validate these fields, and perhaps offer rollback if they don't work
            Props.Default.SierraAPI_Server = textBoxSierraAPI_ServerAddress.Text;
            Props.Default.SierraAPI_Key = textBoxSierraAPI_Key.Text;
            Props.Default.SierraAPI_Secret = textBoxSierraAPI_Secret.Text;
            Props.Default.BarcodeRegexPattern = textBoxBarcodeRegex.Text;
        }

        private bool ApiSettingsSaved()
        {
            return (Props.Default.SierraAPI_Server == textBoxSierraAPI_ServerAddress.Text
                && Props.Default.SierraAPI_Key == textBoxSierraAPI_Key.Text
                && Props.Default.SierraAPI_Secret == textBoxSierraAPI_Secret.Text
                && Props.Default.BarcodeRegexPattern == textBoxBarcodeRegex.Text);
        }





        void Library_Labels_FormClosing(object sender, FormClosingEventArgs e)
        {
            putDirectControlDataToSettings();
            if (!ApiSettingsSaved())
            {
                DialogResult dr = "There are unsaved changes in the Sierra API Settings.\nDo you wish to save before closing the program?".MsgBox(MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr == DialogResult.Yes) putSierraApiControlsToSettings();
            }
            Props.Default.Save();
        }

        #endregion

        #region tab control

        private void tabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            focusControl("tab page changed");
        }

        #endregion

        #region GroupBox Switcheroo
        // our Margin Edit and Font Preset controls share the same space on the form
        private void setGroupBoxVisibility()
        {
            if (Props.Default.showFontStyles)
            {
                groupBoxFontStyle.Visible = true; ;
                groupBoxMargins.Visible = false; ;
            }
            else
            {
                groupBoxFontStyle.Visible = false; ;
                groupBoxMargins.Visible = true; ;
            }
        }

        private void linkLabelMargins_Click(object sender, EventArgs e)
        {
            Props.Default.showFontStyles = false;
            setGroupBoxVisibility();
        }

        private void linkLabelFontStyle_Click(object sender, EventArgs e)
        {
            Props.Default.showFontStyles = true;
            setGroupBoxVisibility();
        }

        #endregion

        #region Active TextBox

        void updateButtonSpineOrientation_Text()
        {
            //"updateButtonSpineOrientation_Text".Log();
            if (spineBox.OrientLandscape) buttonSpineOrientation.Text = "Portrait";
            else buttonSpineOrientation.Text = "Landscape";
        }

        void buttonSpineOrientation_Click(object sender, EventArgs e)
        {
            //"\norient button clicked".WithArgs().Log();
            suppressPanelBoxEnter = true;
            richTextBoxSpine.Visible = false;
            spineBox.OrientLandscape = !spineBox.OrientLandscape;
            updateButtonSpineOrientation_Text();
            boxActivateAndUpdateFitAndFocus(spineBox);
            richTextBoxSpine.Visible = true;
            suppressPanelBoxEnter = false;
        }



        void spineBox_Enter(object sender, EventArgs e)
        {
            if (suppressPanelBoxEnter) return;
            boxActivateAndUpdateFitAndFocus(spineBox);
        }


        void pocketBox_Enter(object sender, EventArgs e)
        {
            if (suppressPanelBoxEnter) return;
            boxActivateAndUpdateFitAndFocus(pocketBox);
        }

        void lookLeft()
        {
            foreach (RovingButton rover in buttonRovers) rover.LookLeft();
        }
        void lookRight()
        {
            foreach (RovingButton rover in buttonRovers) rover.LookRight();
        }
        void dontLook()
        {
            foreach (RovingButton rover in buttonRovers) rover.DontLook();
        }


        // two high level calls to activate boxes
        void boxesDeactivate()
        {
            if (setBoxesInactive()) boxBorders();
        }

        void boxActivateAndUpdateFitAndFocus(PanelBox panelBox = null)
        {
            // called at startup, spinebox_Enter and pocketbox_Enter
            if (panelBox == null) panelBox = spineBox;
            if (setActiveBox(panelBox)) boxBorders();

            activeBox.UpdateFit("boxActivateAndUpdateFitAndFocus");
            activeBox.Focus();
        }

        // these just change the logic, no actual display
        bool setActiveBox(PanelBox panelBox) // never null
        {
            bool result = activeBox != panelBox;
            activeBox = panelBox;
            activeBox.Active = true;

            if (activeBox == spineBox) pocketBox.Active = false; ;
            if (activeBox == pocketBox) spineBox.Active = false;
            return result;
        }

        bool setBoxesInactive()
        {
            bool result = spineBox.Active || pocketBox.Active;

            activeBox = null;
            pocketBox.Active = false;
            spineBox.Active = false;
            return result;
        }

        private void boxBorders()
        {
            //"boxBorders {0}".WithArgs(activeBox == null ? "null" : activeBox.Name).Log();

            spineBox.SetBordersBackColor();
            if (!spineBox.Active) spineBox.SetForeColor();

            pocketBox.SetBordersBackColor();
            if (!pocketBox.Active) pocketBox.SetForeColor();

            if (activeBox == spineBox) lookLeft();
            else if (activeBox == pocketBox) lookRight();
            else if (activeBox == null) dontLook();
            updateMarginControls();
        }


        #endregion

        #region Font Control

        // changing the font is one of many actions that can trigger
        // textChanged. One reason I don't subscribe to that event any more.

        // so I just moved the font picker into the control because it already encapsulates
        // the LinkLabel. But these controls are shared between the two panel boxes
        // can they be encapsulated anyway? would I define the whole middle panel
        // as a user control, and pass the whole thing to both panel boxes?

        // or should I have left the handlers here?

        void buttonFontBigger_Click(object sender, EventArgs e)
        {
            int rounded = Convert.ToInt32(activeBox.Font.Size);
            float newFontSize = rounded + 1;
            //"\nbutton bigger click about to assign a new font".Log();
            // this gave an exception, I am guessing activeBox was null
            // which happens if we deselect both boxes. but in that case
            // these buttons should be disabled. Can we reproduce the error?
            // who disables this button? well nothing much happens to this button
            // by name. Instead its part of an array of rovingbuttons which get handled
            // en masse by just a handful of routines. lookLeft, lookRight set the enable,
            // DontLook clears it.
            // so the logic seems empecable, if DontLook is being called correctly.
            // well those three all get called in just one spot, boxborders by checking
            // the activeBox. so I suppose if some routine is setting a box to active
            // AND NOT CALLING box border, that could be our problem
            activeBox.Font = activeBox.Font.Resize(newFontSize);
        }

        void buttonFontSmaller_Click(object sender, EventArgs e)
        {
            int rounded = Convert.ToInt32(activeBox.Font.Size);
            float newFontSize = rounded - 1;
            if (newFontSize > 0) activeBox.Font = activeBox.Font.Resize(newFontSize);
        }



        // so poop when the textboxes change in the setup screen, we need to transfer
        // the names into the button text.
        private void buttonRegular_Click(object sender, EventArgs e)
        {
            activeBox.Font = new Font("Arial", activeBox.Font.Size, FontStyle.Regular);
            activeBox.Font = new Font(Props.Default.QuickFont1.FontFamily, activeBox.Font.Size, Props.Default.QuickFont1.Style);
        }

        private void buttonNarrow_Click(object sender, EventArgs e)
        {
            activeBox.Font = new Font("Arial Narrow", activeBox.Font.Size, FontStyle.Bold);
            activeBox.Font = new Font(Props.Default.QuickFont2.FontFamily, activeBox.Font.Size, Props.Default.QuickFont2.Style);
        }

        private void buttonBold_Click(object sender, EventArgs e)
        {
            activeBox.Font = new Font("Arial", activeBox.Font.Size, FontStyle.Bold);
            activeBox.Font = new Font(Props.Default.QuickFont3.FontFamily, activeBox.Font.Size, Props.Default.QuickFont3.Style);
        }

        private void buttonBlack_Click(object sender, EventArgs e)
        {
            activeBox.Font = new Font("Arial Black", activeBox.Font.Size, FontStyle.Bold);
            activeBox.Font = new Font(Props.Default.QuickFont4.FontFamily, activeBox.Font.Size, Props.Default.QuickFont4.Style);
        }

        float printedFontSize(Font textBoxFont) // unused
        {
            float textBoxDpi = 96.0F;
            float zebraPrinterDpi = 203.0F;

            float printedFontSize = textBoxFont.Size * textBoxDpi / zebraPrinterDpi;
            return printedFontSize;
        }

        #endregion

        #region Fitting the Text

        private void checkBoxWrapSpineBox_CheckedChanged(object sender, EventArgs e)
        {
            spineBox.WordWrap = checkBoxWrapSpineBox.Checked;
        }

        private void checkBoxWrapPocketBox_CheckedChanged(object sender, EventArgs e)
        {
            pocketBox.WordWrap = checkBoxWrapPocketBox.Checked;
        }


        #endregion

        #region Raw Data
        #endregion

        #region Data Format Settings

        private void radioButtonsFormat_CheckedChanged(object sender, EventArgs e)
        {
            RadioButton rb = sender as RadioButton;
            if (rb == radioButtonSpecifyDQR)
            {
                if (rb.Checked)
                {
                    labelDelimiter.Enabled = true;
                    labelQualifier.Enabled = true;
                    labelRFD.Enabled = true;
                    textBoxDataDelimiter.Enabled = true;
                    textBoxDataQualifier.Enabled = true;
                    textBoxDataRFD.Enabled = true;
                }
                else
                {
                    labelDelimiter.Enabled = false;
                    labelQualifier.Enabled = false;
                    labelRFD.Enabled = false;
                    textBoxDataDelimiter.Enabled = false;
                    textBoxDataQualifier.Enabled = false;
                    textBoxDataRFD.Enabled = false;
                }
            }
            else if (rb == radioButtonSpecifyFCN)
            {
                if (rb.Checked)
                {
                    labelFirstColumnHeader.Enabled = true;
                    labelDefaultRFD.Enabled = true;
                    textBoxFirstColumnName.Enabled = true;
                    textBoxDefaultRFD.Enabled = true;
                }
                else
                {
                    labelFirstColumnHeader.Enabled = false;
                    labelDefaultRFD.Enabled = false;
                    textBoxFirstColumnName.Enabled = false;
                    textBoxDefaultRFD.Enabled = false;
                }
            }
        }
        private void checkBoxFillPocketDGV_CheckedChanged(object sender, EventArgs e) 
        {
            if (checkBoxFillPocketDGV.Checked) fillBoxes(dataGridViewPrintAndPreview);
            else pocketBox.Text = string.Empty; // just this once
        }


        // poop could these all be tool tips?

        private void buttonHelpSDQ_Click(object sender, EventArgs e)
        {
            @"    This software is designed to work with
simple tabular data in many different formats.
Knowing what that format is makes it possible
for the program to correctly print your labels.

    You may directly specify the delimiter and
text qualifier used in your data files. This 
option will work if you always use the same 
format when you construct your data. There
is also a repeated field delimiter you must
specify.

    The data in the first column is expected
to be call numbers and will be printed
on the smaller of the two labels"
                        .MsgBox(MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        private void buttonHelpFCH_Click(object sender, EventArgs e)
        {
            @"    If the format of the data is likely
to vary then there is an alternate method
that the program can use to interpret the data.

    The first line of the imported data is
assumed to be a header row. If you can exactly
specify the header text of the first column then
the program can almost always figure out the
various delimiters for you.

    In addition, specify your system'p default 
'repeated field delimiter'. For Millennium and Sierra,
this is semi-colon."
                                    .MsgBox(MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void buttonHelpFilter_Click(object sender, EventArgs e)
        {
            @"    Occasionally your data may have repeated
fields. It'p your option whether to print all of
them (on seperate lines) or to ingore the extra
ones.

    This applies to data after the first column,
which will be printed each to its own line on
the larger of the two labels."
                                    .MsgBox(MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void buttonHelpStacking_Click(object sender, EventArgs e)
        {
            @"    When Call Number data is to be printed
the program offers a couple of options about
splitting the call number onto seperate lines.

The data you import will have its call number
on a single line. We use 4 simple rules to
determine where to insert line breaks."
                                    .MsgBox();
        }


        private void buttonReLoad_Click(object sender, EventArgs e)
        {
            if (parseRawDataToTable(richTextBoxRawData.Lines, userDataTable, parseErrors))
            {
                loadDGVfromDataTable(userDataTable, dataGridViewPrintAndPreview);
                loadDGVfromDataTable(userDataTable, dataGridViewFileImportSettings);
                focusControl("data file loaded");
                "Data Table Updated".MsgBox(MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // poop enhancement
        // it would be cool if we reordered the textbox after parseing to show the lines that passed and the lines
        // that failed. think about how to do this...maybe just with color, maybe with sorting. I want to resist
        // turning this into a massive fix your crappy data editor. But if they were going to open Notepad to try
        // to figure out what went wrong, we can do as well as that. So a button called NextException would not seem too 
        // extravagant.

        #endregion

        #region Printer AlignmentBounds

        void buttonSpineLeft_Click(object sender, EventArgs e)
        {
            if (panelSpineAlignment.Location.X > 0)
                panelSpineAlignment.Location += new Size(-1, 0);
        }

        void buttonSpineRight_Click(object sender, EventArgs e)
        {
            if (panelSpineAlignment.Location.X < 100)
                panelSpineAlignment.Location += new Size(+1, 0);
        }

        void buttonSpineUp_Click(object sender, EventArgs e)
        {
            if (panelSpineAlignment.Location.Y > 0)
                panelSpineAlignment.Location += new Size(0, -1);
        }

        void buttonSpineDown_Click(object sender, EventArgs e)
        {
            if (panelSpineAlignment.Location.Y < 20)
                panelSpineAlignment.Location += new Size(0, +1);
        }

        void buttonPocketLeft_Click(object sender, EventArgs e)
        {
            if (panelPocketAlignment.Location.X > 200)
                panelPocketAlignment.Location += new Size(-1, 0);
        }

        void buttonPocketRight_Click(object sender, EventArgs e)
        {
            if (panelPocketAlignment.Location.X < 300)
                panelPocketAlignment.Location += new Size(+1, 0);
        }
        void buttonPocketUp_Click(object sender, EventArgs e)
        {
            if (panelPocketAlignment.Location.Y > 0)
                panelPocketAlignment.Location += new Size(0, -1);
        }

        void buttonPocketDown_Click(object sender, EventArgs e)
        {
            if (panelPocketAlignment.Location.Y < 20)
                panelPocketAlignment.Location += new Size(0, +1);
        }

        void panelSpineAlignment_LocationChanged(object sender, EventArgs e)
        {
            updatePanelSpineAlignmentLabel();
        }

        void panelPocketAlignment_LocationChanged(object sender, EventArgs e)
        {
            updatePanelPocketAlignmentLabel();
        }

        // poop now it seems that a vertical position of zero actually looks pretty good.
        // how to I make that display correctly as a zero, send a zero to the printer
        // and have the alignment panel nicely centered in a panel that is larger?
        // I think  we would make the .Alignment of the panelBox a calculated amount
        // and probably its going to need to find its container (or have it passed explicityly
        // how do I find container client rectangle

        void updatePanelSpineAlignmentLabel()
        {
            labelSpineAlignment.Text = "{0}".WithArgs(panelSpineAlignment.Location.ToText());
        }

        void updatePanelPocketAlignmentLabel()
        {
            labelPocketAlignment.Text = "{0}".WithArgs(panelPocketAlignment.Location.ToText());
        }



        void buttonPrintTestPattern_Click(object sender, EventArgs e)
        {
            // poop this should draw the graphic in the rich text boxes.
            // would this be allowed? Or would I have to swap out my rtb'p for a couple
            // of picturebox controls? Or maybe an blank control. This grab the graphic
            // and write to it sounds fairly low level. I suppose I could interrupt the
            // paint event for the rtb and directly draw the rectangles. Hey can I do something
            // with control.borderStyle? .borderWidth? that would sure be handy!
            // http://www.codeproject.com/Tips/388405/Draw-a-Border-around-any-Csharp-Winform-Control


            // does this add any adjustments at all? No it sends the 4 corners of the rectangle
            // with the exact values we see on the screen.
            nuZebraski.Instance.StartNewCommand();
            nuZebraski.Instance.BoxCommand(panelSpineAlignment, 20);
            nuZebraski.Instance.BoxCommand(panelPocketAlignment, 20);
            nuZebraski.Instance.SendCommandsToPrinter(Props.Default.PrinterName);
        }

        void buttonAutoSense_Click(object sender, EventArgs e)
        {
            nuZebraski.Instance.StartNewCommand();
            nuZebraski.Instance.AutosenseCommand();
            nuZebraski.Instance.SendCommandsToPrinter(Props.Default.PrinterName);
        }

        private void buttonFactoryDefaults_Click(object sender, EventArgs e)
        {
            // the manual kind of advises caution using this, but its not so bad.
            DialogResult proceed = "Are you sure you want to restore the Printer to Factory Defaults?\nBe sure to run AutoSense command afterwards"
                .MsgBox(MessageBoxButtons.OKCancel, MessageBoxIcon.Asterisk);
            if (proceed == DialogResult.OK)
            {
                nuZebraski.Instance.StartNewCommand();
                nuZebraski.Instance.FactoryResetCommand();
                nuZebraski.Instance.SendCommandsToPrinter(Props.Default.PrinterName);
            }
        }

        private void buttonSetPageSize_Click(object sender, EventArgs e)
        {
            // 3-11/16 * 203
            // 3-13/16 * 203 for the full width of the backing paper = 774 poop try this. or just the paperpanelwidth = 774 done this before
            int labelWidth = 774; // 745;
            int labelHeight = 285; // 1.4 * 203
            int gapHeight = 20; // (1.5 * 203) - 285
            // 3-11/16 * 203 or just a bit less call it 745
            nuZebraski.Instance.StartNewCommand();
            nuZebraski.Instance.SetPageSize(labelWidth, labelHeight, gapHeight); // poop test
            nuZebraski.Instance.SendCommandsToPrinter(Props.Default.PrinterName);
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

        void reCenterAlignmentPanels() // unused
        {
            panelSpineAlignment.Location = new Point(
            panelSpineAlignment.Location.X,
            (panelPaperSpace.Size.Height - panelSpineAlignment.Height) / 2); // y-axis doesn't matter, center it for aesthetics

            panelPocketAlignment.Location = new Point(
            panelPocketAlignment.Location.X,
            (panelPaperSpace.Size.Height - panelPocketAlignment.Height) / 2); // ditto
        }

        private void tabPageAlignment_Enter(object sender, EventArgs e)
        {
            updatePanelSpineAlignmentLabel();
            updatePanelPocketAlignmentLabel();
        }

        // show the emulated last print job in this same paperspacepanel
        private void buttonEmulate_MouseDown(object sender, MouseEventArgs e)
        {
            panelZebraEmulator.Visible = true;
            panelPaperSpace.Visible = false;
        }

        private void buttonEmulate_MouseUp(object sender, MouseEventArgs e)
        {
            panelZebraEmulator.Visible = false;
            panelPaperSpace.Visible = true;
        }

        #endregion

        #region GroupBox DataView


        private void dataGridView_Click(object sender, EventArgs e)
        {
            focusControl("DGV clicked"); // poop does this care which tab page we're on? I have it set to print and preview only
        }


        private void groupBoxData_EnabledChanged() //(object sender, EventArgs e)
        {
            // Set RowHeadersDefaultCellStyle.SelectionBackColor so that its default 
            // value won't override DataGridView.DefaultCellStyle.SelectionBackColor.
            //dataGridView.RowHeadersDefaultCellStyle.SelectionBackColor = Color.Empty;


            if (groupBoxData_Enabled) // || dataGridViewPrintAndPreview.Enabled) // use standard windows colors
            {
                // Set the selection background color for all the cells.
                dataGridViewPrintAndPreview.DefaultCellStyle.SelectionBackColor = SystemColors.Highlight;
                dataGridViewPrintAndPreview.DefaultCellStyle.SelectionForeColor = SystemColors.HighlightText;

                setDGVActiveBackColor();
                dataGridViewPrintAndPreview.RowsDefaultCellStyle.ForeColor = SystemColors.ControlText;

                dataGridViewPrintAndPreview.BorderStyle = BorderStyle.FixedSingle;
            }
            else // disabled, go gray
            {
                // Set the selection background color for all the cells.
                dataGridViewPrintAndPreview.DefaultCellStyle.SelectionBackColor = Color.Gray;
                dataGridViewPrintAndPreview.DefaultCellStyle.SelectionForeColor = Color.DarkGray;

                // Set the background color for all rows 
                dataGridViewPrintAndPreview.RowsDefaultCellStyle.BackColor = Color.Gray;
                dataGridViewPrintAndPreview.RowsDefaultCellStyle.ForeColor = Color.DarkGray;

                dataGridViewPrintAndPreview.BorderStyle = BorderStyle.None;
            }
        }

        private void setDGVActiveBackColor()
        {
            if (userMode == DGV_UserMode.DGV_Sequential) dataGridViewPrintAndPreview.RowsDefaultCellStyle.BackColor = Color.LightBlue;
            else if (userMode == DGV_UserMode.DGV_SearchRandom) dataGridViewPrintAndPreview.RowsDefaultCellStyle.BackColor = Color.LightGreen;
            else dataGridViewPrintAndPreview.RowsDefaultCellStyle.BackColor = Color.Salmon; // poop should this ever happen?

        }
        void dataGridView_CurrentCellChanged(object sender, EventArgs e)
        {
            //"current cell changed Event".Log();
            // could be filled as the result of next button
            // or a search or a click or loading the file

            gotoRawData();
            fillBoxes(dataGridViewPrintAndPreview);
        }

        // okay poop I guess we could provide the other service. double cliking in the
        // textbox could take us to the gridview row that corresponds.



        private void gotoRawData()
        {
            if (dataGridViewPrintAndPreview.CurrentCell == null) return;
            int row = dataGridViewPrintAndPreview.CurrentCell.RowIndex;

            int startLine = richTextBoxRawData.GetFirstCharIndexFromLine(row + 1);
            int endLine = richTextBoxRawData.Text.IndexOf("\n", startLine);
            if (endLine < 0) endLine = richTextBoxRawData.Text.Length;
            int length = endLine - startLine;

            richTextBoxRawData.Select(startLine, length);
            richTextBoxRawData.ScrollToCaret();
        }

        void dataGridView_ColumnDisplayIndexChanged(object sender, DataGridViewColumnEventArgs e)
        {
            if (dataGridViewPrintAndPreview.CurrentCell == null) return;

            if (myColorTabControl.SelectedTab == tabPagePreview)
            {
                fillBoxes(dataGridViewPrintAndPreview);
            }
        }

        bool moreDataGridViewItems()
        {
            if (dataGridViewPrintAndPreview.CurrentRow == null) return false;
            return dataGridViewPrintAndPreview.CurrentRow.Index + 1 < dataGridViewPrintAndPreview.Rows.Count;
        }

        void nextDataGridViewItem()
        {
            if (dataGridViewPrintAndPreview.CurrentRow != null)
            {
                int selection = dataGridViewPrintAndPreview.CurrentRow.Index;
                selection++;
                if (selection < dataGridViewPrintAndPreview.Rows.Count)
                    dataGridViewPrintAndPreview.CurrentCell = dataGridViewPrintAndPreview.Rows[selection].Cells[0];
            }
        }

        #endregion

        #region GroupBox Search and Print

        private void buttonNext_Click(object sender, EventArgs e)
        {
            if (moreDataGridViewItems())
            {
                nextDataGridViewItem(); // this fires the event that fills
                //focusControl("next DGV item loaded");
            }
        }

        private void textBoxSearch_Enter(object sender, EventArgs e)
        {
            focusControl("search box enter");
        }

        private void textBoxSearch_TextChanged(object sender, EventArgs e)
        {
            focusControl("search box text changed");
        }


        // historically we have signalled a failed search by clearing the two print boxes
        // deselecting the grid view
        // and leaving selected data in the searchbox
        // successful searches would clear the searchbox, populate the print boxes, and select a row in the grid.

        // suppose now that we have a successful API search. again, clear the searchbox and populate the printboxes
        // but we should not select anything in the grid view obviously


            // poop does this logic handle the possibility of sierra api not working?
        void buttonSearch_Click(object sender, EventArgs e) // also on Enter key press
        {
            string searchKey = textBoxSearch.Text.ToLower();
            if (searchKey == "") return;
            bool isBarcode = (regexBarcode == null) ? true : regexBarcode.IsMatch(searchKey);

            // data table should be searched first. return if found
            if (dataGridViewPrintAndPreview.Rows.Count > 0) 
            {
                foreach (DataGridViewRow row in dataGridViewPrintAndPreview.Rows)
                    foreach (DataGridViewCell cell in row.Cells)
                        if (cell.FormattedValue.ToString().ToLower() == searchKey)
                        {
                            dataGridViewPrintAndPreview.CurrentCell = cell;
                            cell.Selected = true; // this will trigger the transfer of data to the textboxes
                            focusControl("search datagrid success");
                            return;
                        }
                dataGridViewPrintAndPreview.ClearSelection();
                dataGridViewPrintAndPreview.CurrentCell = null;

                DialogResult dialogResult = DialogResult.Cancel; // set up to fall through
                if (simpleClientWorking && isBarcode) dialogResult = $"Could not locate {searchKey} in Data Table.\nSearch Database with APIs?".MsgBox(MessageBoxButtons.OKCancel, MessageBoxIcon.Question);

                if (dialogResult == DialogResult.Cancel)
                {
                    focusControl("search datagrid failure");
                    return;
                }

                // else fall through and try the API search. We need a working client and a valid barcode to bother trying...
            }


            if (isBarcode)
            {
                if (simpleClientWorking)
                {
                    formSearchAPI.searchBarcode(searchKey, simpleClient, Props.Default.BarcodeQuery);
                    formSearchAPI.ShowDialog(this);
                    string[] queryData = formSearchAPI.queryData;

                    if (queryData != null)
                    {
                        fillBoxes(queryData);
                        focusControl("search API success");
                        return;
                    }
                    focusControl("search API failed");
                    return;

                }
                else
                {
                    focusControl("search API failed");
                    $"Sorry, Sierra API Searching disabled (check settings)".MsgBox(MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            // is not in barcode format
                focusControl("search API failed");
                // search failed response
                $"Search String '{searchKey}' Not in expected format for Barcode lookup with Sierra APIs".MsgBox(MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
        }




        void buttonPrint_Click(object sender, EventArgs e)
        {
            doPrintJob();
            focusControl("print");
        }

        private void doPrintJob()
        {
            bool printSpine = richTextBoxSpine.Text.HasDarkChars();
            bool printPocket = richTextBoxPocket.Text.HasDarkChars();

            if (printSpine || printPocket)
            {
                nuZebraski.Instance.StartNewCommand();
                if (printSpine) queuePanelBox(spineBox);
                if (printPocket) queuePanelBox(pocketBox);
                if (!DisablePrinterDebug) nuZebraski.Instance.SendCommandsToPrinter(Props.Default.PrinterName);
            }

            else System.Media.SystemSounds.Hand.Play();
            if (LastRowLoaded != null)
                LastRowLoaded.DefaultCellStyle.BackColor = SystemColors.ControlLight;
        }
        private void queuePanelBox(PanelBox pb)
        {
            nuZebraski.Instance.AddEmulatorObject(pb.AlignmentBounds);

            CompactBitmap textboxImage = pb.DrawToCompactBitmap();
            nuZebraski.Instance.AddEmulatorObject(textboxImage);
            nuZebraski.Instance.AddTextBoxGraphicCommand(textboxImage);
            return;
        }

        #endregion

        #region Focus Control







        enum DGV_UserMode
        {
            DGV_Empty,    // no data loaded
            DGV_Sequential,    // start at top, step through with Next button
            DGV_SearchRandom     // use Search button, Next is disabled

            // reenter Sequential mode if user clicks to highlight and load a row
            // enter SearchRandom mode if text is entered in search box or search button hit again
            // what about Enter key...haven't we somehow assigned that to Search? that is a SearchRandom promotion

            // poop the usermode was invented to try to optimize the flow of focus depending on what we think the user is doing
            // thats more complex now as we add the SierraApi technology
            // NoData used to mean Oak was typing the call number directly into the textbox, now we have the possibility of looking in the database.
            // NO DATA and NO API is oak mode, there is no point searching or sayng next
            // NO DATA yes api means we have the search but not the next button
            // yes data no api
            // DATA_sequential and DATA_random as before

            // so do I fold the api stuff into the mode? or keep it as a binary switch?
        }






        private void focusControl(string sender)
        {
            Log.AppendSuccess($"focusControl {sender}");

            switch (sender)
            {
                case "tab page changed":
                    setPrintAndDataBoxesEnableState();
                    if (myColorTabControl.SelectedTab == tabPagePreview)
                    {
                        if (activeBox != null) activeBox.Focus();
                        else boxActivateAndUpdateFitAndFocus(); // will get spinebox
                    }
                    else if (myColorTabControl.SelectedTab == tabPageLog)
                    {
                        Log.RichTextBox.Focus();
                    }
                    break;
                case "data file loaded": // poop! Alison would want to focus on the active box, Oak would want the search box selected
                    // dare we try to guess the type of user from the size of the file?
                case "DGV clicked":
                    setUserMode(DGV_UserMode.DGV_Sequential);
                    boxActivateAndUpdateFitAndFocus(activeBox);
                    break;
                case "print":
                    if (userMode == DGV_UserMode.DGV_SearchRandom) textBoxSearch.Focus();
                    else boxActivateAndUpdateFitAndFocus(activeBox);
                    break;

                case "search box enter":
                case "CTRL-F":
                    setUserMode(DGV_UserMode.DGV_SearchRandom);
                    break;
                case "search box text changed":
                    buttonSearch.Enabled = textBoxSearch.TextLength > 0;
                    break;

                case "search datagrid success":
                    textBoxSearch.Text = string.Empty;
                    break;

                case "search API success":
                    textBoxSearch.Text = string.Empty;
                    break;
                case "search datagrid failed":
                case "search API failed":
                    spineBox.Text = pocketBox.Text = "";
                    System.Media.SystemSounds.Hand.Play();
                    boxesDeactivate();
                    textBoxSearch.Select(0, textBoxSearch.Text.Length);
                    textBoxSearch.Focus();
                    break;
            }
        }

        private void setUserMode(DGV_UserMode m)
        {
            userMode = m;
            setDGVActiveBackColor();
            setPrintAndDataBoxesEnableState();
        }

        private void setDataControls(bool value)
        {
            Log.AppendInformation($"setDataControls {value}");
            groupBoxData_Enabled =
            buttonNext.Enabled =
            textBoxSearch.Enabled = value;
            buttonSearch.Enabled = value && textBoxSearch.TextLength > 0;
            groupBoxData_EnabledChanged();

        }

        private void setPrintAndDataBoxesEnableState()
        {
            if (myColorTabControl.SelectedTab == tabPagePreview)
            {
                if (userMode == DGV_UserMode.DGV_Empty)
                {
                    Log.AppendInformation("setPrintAndDataBoxesEnableState user mode empty");
                    groupBoxData_Enabled = false;
                    buttonNext.Enabled = false;
                    buttonSearch.Enabled = simpleClientWorking;
                    textBoxSearch.Enabled = simpleClientWorking;
                }
                else if (userMode == DGV_UserMode.DGV_Sequential)
                {
                    groupBoxData_Enabled = true;
                    buttonNext.Enabled = true;
                    textBoxSearch.Enabled = true;
                    buttonSearch.Enabled = false;
                }
                else if (userMode == DGV_UserMode.DGV_SearchRandom)
                {
                    groupBoxData_Enabled = true;
                    buttonNext.Enabled = false;
                    textBoxSearch.Enabled = true;
                    buttonSearch.Enabled = textBoxSearch.TextLength > 0;
                }
                groupBoxData_EnabledChanged();
                buttonPrint.Enabled = true;
            }
            else if (myColorTabControl.SelectedTab == tabPageAlignment)
            {
                setDataControls(false);
                buttonPrint.Enabled = true;
            }

            else if (myColorTabControl.SelectedTab == tabPageFileImportSettings)
            {
                setDataControls(true);
                buttonPrint.Enabled = false;
            }
            //else if (myColorTabControl.SelectedTab == tabPageRawData)
            //{
            //    setDataControls(true);
            //    buttonPrint.Enabled = false;
            //}
            else if (myColorTabControl.SelectedTab == tabPageLog)
            {
                setDataControls(false); buttonPrint.Enabled = false;
            }
            else if (myColorTabControl.SelectedTab == tabPageHelp)
            {
                setDataControls(false); buttonPrint.Enabled = false;
            }
            //else if (myColorTabControl.SelectedTab == tabPageHelpZebra)
            //{
            //    setDataControls(false); buttonPrint.Enabled = false;
            //}
        }

        #endregion

        #region Parsing Data, GridView and Files

        int[] columnIndices(DataGridViewColumnCollection columns)
        {
            int[] columnIndices = new int[columns.Count];
            for (int i = 0; i < columns.Count; i++)
                columnIndices[columns[i].DisplayIndex] = i;
            return columnIndices;
        }
        void loadRawText(string fileName)
        {
            richTextBoxRawData.Text = string.Empty;

            if (!File.Exists(fileName))
                throw new Exception("File Not Found: {0}".WithArgs(fileName));

            else using (StreamReader sr = new StreamReader(fileName))
                {
                    richTextBoxRawData.Text = sr.ReadToEnd().Nuke();
                }
        }

        bool parseRawDataToTable(string[] rawData, DataTable dataTable, List<string> exceptions)
        {
            dataTable.Rows.Clear();
            dataTable.Columns.Clear();
            exceptions.Clear();
            if (rawData.Length == 0) return false;

            CsvParser parser;
            try
            {
                String header = rawData[0];
                // the parser needs to be able to tweak the header 
                // see fixHeaderLine()

                if (radioButtonSpecifyDQR.Checked)
                {
                    parser = new CsvParser(
                        textBoxDataDelimiter.Text,
                        textBoxDataQualifier.Text,
                        textBoxDataRFD.Text,
                        ref header);
//                    if (!parser.wellFormed)
//                    {
//                        @"Error: Data does not appear to match the specified format.
//Correct the format settings or consider
//using First Column Name approach".MsgBox(); 
//                        return false;
//                    }
                }

                else if (radioButtonSpecifyFCN.Checked)
                {
                    parser = new CsvParser( // okay this will throw an exception. Poop!
                        textBoxFirstColumnName.Text,
                        textBoxDefaultRFD.Text,
                        ref header);
                    //if (!parser.wellFormed) // poop do we get here or does an exception bump us out first?
                    //{
                    //    "Error: Internal 45VU9".MsgBox();
                    //    return false;
                    //}
                }

                else { throw new Exception("Data Format Radio Button Confusion"); }


                string[] headerCells = parser.ParseLine(header, false); // may return null...anybody care poop?

                if (headerCells.Length == 0) return false;

                foreach (string columnName in headerCells)
                {
                    dataTable.Columns.Add(columnName);
                }

                for (int i = 1; i < rawData.Length; i++)
                {
                    string line = rawData[i];
                    if (line == "") continue;
                    string[] cells = parser.ParseLine(line, radioButtonFirstRepeatedFieldOnly.Checked);
                    if (cells == null) exceptions.Add(line); // okay this I like. What happens to these exceptions?
                    else dataTable.Rows.Add(cells);
                }
                return true;
            }
            catch (Exception x)
            {
                "Exception {0}".WithArgs(x).MsgBox();
                //"Exception {0}".WithArgs(x.Message).MsgBox();
                return false;
            }

        }

        void loadDGVfromDataTable(DataTable dataTable, DataGridView dgv)
        {
            if (dgv.DataSource != dataTable)
            {
                dgv.DataSource = null;
                dgv.Columns.Clear();
                dgv.DataSource = dataTable;
            }
            // dont clear the rows.
            //dgv.Columns.Clear();
            //dgv.DataSource = dataTable;
            dgv.CancelEdit();
            dgv.ClearSelection();
            if (dataGridViewPrintAndPreview.Rows.Count > 0) dataGridViewPrintAndPreview.Rows[0].Selected = true; ;
            return;
        }


        #endregion

        #region Fill Boxes


        void fillBoxes(string[] tableData)
        {
            int nColumns = tableData.Length;
            if (nColumns < 2) $"fillBoxes Error nColumns {nColumns}".MsgBox();
            Log.AppendSuccess($"fillboxes {tableData[0]}\n");
            spineBox.Text = formatCallNumber(tableData[0]);

            if (checkBoxFillPocketDGV.Checked)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 1; i < nColumns; i++) // skipping the first cell in this row, which is spinelabel data
                    sb.AppendLine(tableData[i]);

                string text = sb.ToNuked().TrimEnd();
                if (checkBoxExpandEscapeChars.Checked) text = text.ExpandEscapeChars(); // conversion \n
                pocketBox.Text = text;
            }

            spineBox.UpdateFit("fillboxes");
            pocketBox.UpdateFit("fillboxes");
        }




        void fillBoxes(DataGridView dGV) 
        {
            string[] tableData = new string[] { "", "" }; // empty boxes


            if (dGV.CurrentCell != null)
            {
                DataGridViewRow dgvRow = dGV.CurrentRow;
                DataGridViewCellCollection dgvCells = dgvRow.Cells;
                int nColumns = dGV.Columns.Count;

                if (nColumns > 0)
                {
                    tableData = new string[nColumns];

                    int[] indices = columnIndices(dGV.Columns);

                    tableData[0] = formatCallNumber(dgvCells[indices[0]].FormattedValue.ToString());

                    for (int i = 1; i < indices.Length; i++) // skipping the first cell in this row, which is spinelabel data
                    {
                        string name = dGV.Columns[indices[i]].HeaderText;
                        string field = dgvCells[indices[i]].FormattedValue.ToString();
                        if (name == "TITLE" || name == "245_a") field = field.TruncateSierraTitle();
                        tableData[i] = field;
                    }
                }
                LastRowLoaded = dgvRow; // last as in most recently. not necessarily the end of the list

            }
            fillBoxes(tableData);
        }


        string formatCallNumber(string s)
        {
            Match m = Regex.Match(s, @"^.*?([^\t]+)$");
            if (m.Success) s = m.Groups[1].Value;
            if (checkBoxRemoveSubFieldIndicators.Checked) s = Regex.Replace(s, @"\|.", " ");
            if (checkBoxStackSpaces.Checked) s = s.Replace(" ", "\n");
            if (checkBoxStackDots.Checked) s = s.Replace(".", ".\n");
            if (checkBoxUnStackAmpersands.Checked) s = s.Replace("\n&", " &");
            return s;
        }


        #endregion

        #region Sierra API

        bool createBarcodeRegex(string pattern)
        {
            regexBarcode = null;
            if (pattern.Length > 0)
            {
                try
                {
                    regexBarcode = new Regex(pattern);
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }
            return true;
        }

        // not a setting
        private void checkBoxShowSecret_CheckedChanged(object sender, EventArgs e)
        {
            textBoxSierraAPI_Secret.UseSystemPasswordChar = !checkBoxShowSecret.Checked;
        }

        private void textBoxBarcodeRegex_TextChanged(object sender, EventArgs e)
        {
            barcodeRegexValidate();
            sierraApiButtonEnables();
        }

        private void barcodeRegexValidate()
        {
            bool validPattern = createBarcodeRegex(textBoxBarcodeRegex.Text);
            textBoxBarcodeRegex.ForeColor = validPattern ? SystemColors.WindowText : Color.Red;
        }




        private void textBoxSierraAPI_ServerAddress_TextChanged(object sender, EventArgs e)
        {
            sierraApiButtonEnables();
        }


        private void textBoxSierraAPI_Secret_TextChanged(object sender, EventArgs e)
        {
            sierraApiButtonEnables();
        }

        private void textBoxSierraAPI_Key_TextChanged(object sender, EventArgs e)
        {
            sierraApiButtonEnables();
        }


        private void buttonTestSierraAPI_Click(object sender, EventArgs e)
        {
            UseWaitCursor = true;
            // here we are testing the credentials, so we use the textbox data
            SimpleClient testClient = new SimpleClient(
                textBoxSierraAPI_ServerAddress.Text,
                textBoxSierraAPI_Key.Text,
                textBoxSierraAPI_Secret.Text,
                true);
            UseWaitCursor = false;
            if (!testClient.HasClient)
                "Oh Goodness! Please check the Server Address and try again!".MsgBox(MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else
            {
                if (!testClient.HasAccess)
                    "So Sorry! Please check the Credentials and try again!".MsgBox(MessageBoxButtons.OK, MessageBoxIcon.Warning);
                else
                {
                    if (!testClient.HasItemPermissions && !testClient.HasBibPermissions)
                        "Jeepers! These credentials don't have Item or Bib permissions!".MsgBox(MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    else if (!testClient.HasItemPermissions)
                        "Bummer! These credentials don't have Item permissions!".MsgBox(MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    else if (!testClient.HasBibPermissions)
                        "What a Drag! These credentials don't have Bib permissions!".MsgBox(MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    else
                    "Hoorah! we can access the Sierra APIs with these credentials".MsgBox(MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }




        private void myColorTabControl_Deselecting(object sender, TabControlCancelEventArgs e)
        {
            if (e.TabPage == tabPageSierraAPI && !ApiSettingsSaved())
            {
                    DialogResult dr = "There are unsaved changes in the Sierra API Settings.\nDo you wish to save before leaving this page?".MsgBox(MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr == DialogResult.Yes) saveSierraApiSettings();
            }
        }
        private void myColorTabControl_Selected(object sender, TabControlEventArgs e)
        {
            if (e.TabPage == tabPageSierraAPI)
            {
                sierraApiButtonEnables();
                barcodeRegexValidate();
            }
        }

        private void buttonSaveSierraApiSettings_Click(object sender, EventArgs e)
        {
            saveSierraApiSettings();
        }


        private void buttonRestoreSierraApiSettings_Click(object sender, EventArgs e)
        {
            restoreSierraApiSettings();
        }


        private void saveSierraApiSettings()
        {
            putSierraApiControlsToSettings();
            sierraApiButtonEnables();
        }

        private void restoreSierraApiSettings()
        {
            getSierraApiControlsFromSettings();
            sierraApiButtonEnables();
        }

        private void sierraApiButtonEnables()
        {
            buttonSaveSierraApiSettings.Enabled = buttonRestoreSierraApiSettings.Enabled = !ApiSettingsSaved();
        }

        #endregion

        private void buttonHelpQuickFonts_Click(object sender, EventArgs e)
        {
            "poop say something about quick fonts".MsgBox();
        }

        private void textBoxQuickFontName1_TextChanged(object sender, EventArgs e)
        {
            RovingButtonQuickFont1.SetBaseText(textBoxQuickFontName1.Text);
        }

        private void textBoxQuickFontName2_TextChanged(object sender, EventArgs e)
        {
            RovingButtonQuickFont2.SetBaseText(textBoxQuickFontName2.Text);
        }

        private void textBoxQuickFontName3_TextChanged(object sender, EventArgs e)
        {
            RovingButtonQuickFont3.SetBaseText(textBoxQuickFontName3.Text);
        }

        private void textBoxQuickFontName4_TextChanged(object sender, EventArgs e)
        {
            RovingButtonQuickFont4.SetBaseText(textBoxQuickFontName4.Text);
        }





        // Okay poop here is where we open a font dialog menu
        // so where does the font itself exist? do we use the props or a textbox somewhere?
        // our linklabel is just a display device there is nothing inherently fontish about it.

        private void linkLabelQuickFont1_Click(object sender, EventArgs e)
        {
            FontDialog fontDialog = new FontDialog();
            fontDialog.Font = Props.Default.QuickFont1;
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
            if (result == DialogResult.OK) Props.Default.QuickFont1 = fontDialog.Font;
            // poop we have to update the link label text
            linkLabelQuickFont1.Text = Props.Default.QuickFont1.ToText();
        }

        private void linkLabelQuickFont2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            FontDialog fontDialog = new FontDialog();
            fontDialog.Font = Props.Default.QuickFont2;
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
            if (result == DialogResult.OK) Props.Default.QuickFont2 = fontDialog.Font;
            linkLabelQuickFont2.Text = Props.Default.QuickFont2.ToText();
        }

        private void linkLabelQuickFont3_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            FontDialog fontDialog = new FontDialog();
            fontDialog.Font = Props.Default.QuickFont3;
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
            if (result == DialogResult.OK) Props.Default.QuickFont3 = fontDialog.Font;
            linkLabelQuickFont3.Text = Props.Default.QuickFont3.ToText();
        }

        private void linkLabelQuickFont4_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            FontDialog fontDialog = new FontDialog();
            fontDialog.Font = Props.Default.QuickFont4;
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
            if (result == DialogResult.OK) Props.Default.QuickFont4 = fontDialog.Font;
            linkLabelQuickFont4.Text = Props.Default.QuickFont4.ToText();
        }
    }
}
