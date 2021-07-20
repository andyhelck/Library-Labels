using RestSharp;
using System;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;

namespace Library_Labels_Namespace
{
    public partial class FormSearchAPI : Form
    {
        private string barcode;
        private SimpleClient client;
        private string query;

        public string[] queryData { get; set; }
        private Stopwatch stopwatch;


        // poop does this work?
        private void buttonCancel_Click(object sender, EventArgs e)
        {
            bgWorker.CancelAsync();
        }


        public void searchBarcode(string barcode, SimpleClient client, string query)
        {
            this.barcode = barcode;
            this.client = client;
            this.query = query;
            queryData = null;
        }



        bool loaded = false;
        public FormSearchAPI()
        {
            InitializeComponent();
            stopwatch = new Stopwatch();
        }

        // poop we have to test whether the client accesstoken becomes obsolete, and then fix that
        private void FormSearchAPI_Load(object sender, EventArgs e)
        {
            if (!loaded)
            {
                bgWorker.DoWork += bgWorker_DoWork;
                bgWorker.RunWorkerCompleted += bgWorker_RunWorkerCompleted;
                bgWorker.ProgressChanged += bgWorker_ProgressChanged;
                bgWorker.WorkerReportsProgress = true;
                bgWorker.WorkerSupportsCancellation = true;
                loaded = true;
            }
            bgWorker.RunWorkerAsync(); // this sets the train in motion. we can send an argument if we wish...
        }




        // how should we handle the errors? Not finding a barcode is more of a result than an error
        // but timing out on some http loop or not finding a bib record for a known item record
        // is an error.

        // we don't have a 'not found' error other than blanking the spine and pocket boxes
        // I'm thinking true errors could be reported in this form, like "Error XYZ" under a red progress bar
        // poop why does our barcode scanner fill in the searchbox so slowly?



        void bgWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            // we are assuming that the barcode string has passed some kind of parsing test and is at least plausible
            // the json query takes a very long time compared to the other requests.
            bgWorker.ReportProgress(0); Thread.Sleep(400); // Fetch Item Record Number
            if (bgWorker.CancellationPending) { e.Cancel = true; return; }
            bgWorker.ReportProgress(33); // Fetch Item Call Number
            stopwatch.Restart();
            BarcodeToItemID barney = new BarcodeToItemID(barcode, client, query);
            Log.AppendSuccess($"barney {stopwatch.ElapsedMilliseconds}");


            if (bgWorker.CancellationPending) { e.Cancel = true; return; }
            bgWorker.ReportProgress(67); Thread.Sleep(400); // Fetch Bib Title
            if (barney.itemID != "")
            {
                stopwatch.Restart();
                CallNumAndBibsFromItemID calamity = new CallNumAndBibsFromItemID(barney.itemID, client);
                Log.AppendSuccess($"calamity {stopwatch.ElapsedMilliseconds}");

                if (bgWorker.CancellationPending) { e.Cancel = true; return; }
                bgWorker.ReportProgress(100); Thread.Sleep(400); // complete
                if (calamity.callNumber != "")
                {
                    stopwatch.Restart();
                    TitleFromBibID titus = new TitleFromBibID(calamity.bibID, client);
                    Log.AppendSuccess($"titus {stopwatch.ElapsedMilliseconds}");
                    bgWorker.ReportProgress(100);
                    if (titus.title != "") queryData = new string[] { calamity.callNumber, barcode, titus.title };
                    else "titus error".MsgBox();
                }
                else "calamity error".MsgBox();
            }
        }

        class BarcodeToItemID
        {
            // use JSON query to return the item number
            string barcode { get; set; }
            RestRequest request { get; set; }
            IRestResponse result { get; set; }
            string query { get; set; }
            public string itemID { get; set; }
            Regex reggie = new Regex(@"/items/(?<itemID>\d+)", RegexOptions.IgnoreCase);

            Match match { get; set; }


            public BarcodeToItemID(string barcode, SimpleClient client, string query)
            {
                this.barcode = barcode;
                this.query = query.Replace("<barcode>", barcode);
                itemID = "";
                request = client.CreateRestRequest(Branch.items, "/query?offset=0&limit=1", Method.POST);
                request.AddParameter("text/json", this.query, ParameterType.RequestBody);
                result = client.Execute(request);
                /*
                {
                "total": 1,
                "start": 0,
                "entries": [
                    {
                    "link": "https://sierra.marmot.org/iii/sierra-api/v5/items/4862783"
                    }
                ]
                }
                */
                match = reggie.Match(result.Content);
                if (match.Success) itemID = match.GetDatum("itemID");
            }
        }

        class CallNumAndBibsFromItemID
        {
            string itemID { get; set; }
            RestRequest request { get; set; }
            IRestResponse result { get; set; }
            Regex reggie = new Regex(@"""bibIds"":\[""(?<bibID>\d+).*""callNumber"":""(?<callNumber>[^""]+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            Match match { get; set; }
            public string bibID { get; set; }
            public string callNumber { get; set; }


            public CallNumAndBibsFromItemID(string itemID, SimpleClient client)
            {
                this.itemID = itemID;
                request = client.CreateRestRequest(Branch.items, $"/?id={ itemID }&fields=callNumber%2CbibIds", Method.GET);
                result = client.Execute(request);
                // {"total":1,"entries":[{"id":"4862783","bibIds":["2689274"],"callNumber":"FIC KIPLING"}]}                
                bibID = callNumber = "";
                Log.AppendSuccess($"CallNumAndBibsFromItemID {result.Content}\n{reggie.ToString()}");
                match = reggie.Match(result.Content);
                if (match.Success)
                {
                    bibID = match.GetDatum("bibID");
                    callNumber = match.GetDatum("callNumber");
                }
            }
        }

        class TitleFromBibID
        {
            string bibID { get; set; }
            RestRequest request { get; set; }
            IRestResponse result { get; set; }
            Regex reggie = new Regex(@"""title"":""(?<title>[^""]+)", RegexOptions.IgnoreCase);

            Match match { get; set; }
            public string title { get; set; }


            public TitleFromBibID(string bibID, SimpleClient client)
            {
                this.bibID = bibID;
                request = client.CreateRestRequest(Branch.bibs, $"/?id={ bibID }&fields=title", Method.GET);
                result = client.Execute(request);
                // {"total":1,"entries":[{"id":"2689274","title":"The man who would be king, and other stories"}]}
                title = "";
                match = reggie.Match(result.Content);
                if (match.Success) title = match.GetDatum("title");
            }
        }






        void bgWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            int value = e.ProgressPercentage;
            progressBar1.Value = value; // 0 to 100 as set in the designer
            switch (value)
            {
                case 0:
                    label1.Text = $"Fetch Item Record Number via JSON Query";
                    break;

                case 33:
                    label1.Text = $"Look up Item Call Number";
                    break;

                case 67:
                    label1.Text = $"Look up Bib Title";
                    break;

                case 100:
                    label1.Text = $"Complete!";
                    break;
                default:
                    label1.Text = "$Progress Bar Confusion {value}";
                    break;
            }
        }

        void bgWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // First, handle the case where an exception was thrown.
            if (e.Error != null)
            {
                MessageBox.Show(e.Error.Message);
            }
            else if (e.Cancelled)
            {
                Log.AppendInformation("backgroundWorker1_RunWorkerCompleted: e.Cancelled");
            }
            else if (e.Result != null)
            {
                queryData = ((string[])e.Result); // how do we turn this into a return statement?
            }
            Close(); // will this close the form or will it simply hide it?
        }

    }
}
