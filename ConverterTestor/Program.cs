// =============================================================================
// GPAL - Generally Positive Automation Library
// Copyright © 2026 Software Decisions, Inc. All rights reserved.
//
// This file is part of GPAL.
// Licensed under the Business Source License 1.1
//
// Primary development, architecture, and vision by Michael B. Vederman,
// CEO of Software Decisions, Inc., Texas.
//
// Internal development maintained privately.
// Public releases appear on GitHub: https://github.com/SoftwareDecisionsInc/GPAL.
//
// See LICENSE for full terms, including Additional Use Grant.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using GenerallyPositive;
using GenerallyPositive.GPALForm;

namespace ConverterTestor
{
    class Program
    {
        public class myclass
        {
            public int IntProperty { get; set; } = 666;
            public string StringProperty { get; set; } = "hello world";

        }

        private static myclass MyClass { get; set; } = new myclass();

        // visual studio can't see how these are used, gives comile error 'never assigned will retain default value
        // so disable that warning
#pragma warning disable 0649
        public class RevalidationData
        {
            public string NPI;
            public string FirstName;
            public string LastName;
            public string OrganizationName;
            public string EnrollmentState;
            public string EnrollmentType;
            public string EnrollmentSpecialty;
            public string RevalidationDate;
            public string AdjustedDate;
            public string GetAdjustedDate()
            {
                if (!string.IsNullOrWhiteSpace(AdjustedDate))
                    return AdjustedDate;
                else return RevalidationDate;
            }
        }

        public static List<NPIData> Npis;
        public static List<vClientData> vClients = new List<vClientData>();
        public struct NPIData
        {
            public string ClientID;
            public string ClientName;
            public string ProviderGroupNPI;
            public string ProviderName;
            public string BillingGroupName;
            public string BillingGroupNPI;
            public string ProviderNPI;
        }
        public struct NPIGroup
        {
            public string ClientID;
            public string EntityName;
            public string NPI;
        }
        public class MatchedData
        {
            public string ClientID;
            public string EntityName;
            public string NPI;
            public string OrganizationName;
            public string EnrollmentState;
            public string EnrollmentSpecialty;
            public string RevalidationDate;

            // for converter
            public MatchedData()
            { }

            public MatchedData(
              string client,
              string entity,
              string npi,
              string orgname,
              string enrollmentstate,
              string enrollmentspec,
              string revaldate)
            {
                this.ClientID = client;
                this.EntityName = entity;
                this.NPI = npi;
                this.OrganizationName = orgname;
                this.EnrollmentState = enrollmentstate;
                this.EnrollmentSpecialty = enrollmentspec;
                this.RevalidationDate = revaldate;
            }
        }
        public class Client
        {
            public string ClientID;
            public string DataSourceID;
            public string ClientServiceManager;

            public Client()
            {
                this.ClientID = string.Empty;
                this.DataSourceID = string.Empty;
                this.ClientServiceManager = string.Empty;
            }

            public Client(vClientData vClientData)
            {
                this.ClientID = vClientData.ClientId?.Trim();
                this.DataSourceID = vClientData.SourceSystemName?.Trim();
                this.ClientServiceManager = vClientData.ClientServiceManager?.Trim();
            }
        }
        public class vClientData
        {
            public string ClientId;
            public string Status;
            public string IncludeIncZAR;
            public string ClientServiceManager;
            public string SourceSystemName;
        }

        public static Dictionary<Client, List<MatchedData>> MatchDictionary { get; set; } = new Dictionary<Client, List<MatchedData>>();
        public static Dictionary<Client, List<MatchedData>> FilteredMatchDictionary { get; set; } = new Dictionary<Client, List<MatchedData>>();
        public static Dictionary<Client, List<MatchedData>> RoundTripDictionary = new Dictionary<Client, List<MatchedData>>();

        public class TestClass
        {
            public string Name { get; set; }
            public int Value { get; set; }
            public Dictionary<string, string> Metadata { get; set; }

            public TestClass(string name, int value, Dictionary<string, string> metadata = null)
            {
                Name = name;
                Value = value;
                Metadata = metadata ?? new Dictionary<string, string>();
            }
        }
#pragma warning restore 0649

        static bool running { get; set; }

        // set by Stop: no test section starts after it
        static bool stopRequested { get; set; }

        // the test sections, in the order a run takes them
        static List<string> testNames = new List<string>
        {
            "Class with every data type, round trip and anchors",
            "Excel workbook",
            "Collections to collections",
            "YAML file",
            "XML file",
            "SelectorSet, with the negative tests",
            "HTML file",
            "YAML alias limit",
            "Simple custom class",
            "CSV file",
            "JSON file",
            "Grid input and grid to grid",
            "CSV to grid",
            "TXT and LOG to grid",
            "Extension with no reader",
            "One column list per file",
            "JSON shapes: an object at the top, key/value pairs, a large file",
        };

        // ── Run tab ───────────────────────────────────────────────────────────

        static GPALTab runTab = GPAL.Tab.WithName("runTab").WithText("Run").ToGPALObject();

        static GPALTextArea aboutBlurb = (GPALTextArea)GPAL.TextArea
            .WithReadOnly(true)
            .WithHeight(150)
            .WithName("aboutBlurb")
            .WithText("Tests the GPAL converter: a class holding every data type written to YAML, CSV, JSON, HTML and XML and read back, the YAML anchors and alias limit, collections into collections, SelectorSet files, an Excel workbook, grids, and CSV, JSON, XML, YAML, HTML, TXT, LOG and multi-file CSV input. Some cases are negative tests on purpose, and the SelectorSet and TestClass property warnings they raise are expected. The Tests tab picks which sections run, all of them when none are ticked." + Environment.NewLine + Environment.NewLine +
                      "It reads the files in the TestData folder beside the exe and writes everything it produces to the TestOutput folder beside the exe, which is disposable. The Log tab has everything GPAL says, and each check reports there as a NOTICE when it passes and a FAILURE when it does not.")
            .ToGPALObject();

        // GPAL's DEBUG events.
        static GPALCheckbox debugCheck = GPAL.CheckboxFor("Include DEBUG in the log", true);

        static GPALButton runBtn = GPAL.Button
            .WithName("run")
            .WithText("▶ Run the tests")
            .WithCallback<EventHandler>((s, e) => runBtn.Enabled = false)
            .WithCallbackOffUIThread<EventHandler>(OnRun)
            .ToGPALObject();

        static GPALButton stopBtn = GPAL.Button
            .WithEnabled(false)
            .WithName("stop")
            .WithText("⏹ Stop")
            .WithCallback<EventHandler>(OnStop)
            .ToGPALObject();

        static GPALButton exitBtn = GPAL.Button
            .WithName("exit")
            .WithText("🚪 Exit")
            .WithCallback<EventHandler>((s, e) => System.Windows.Forms.Application.Exit())
            .ToGPALObject();

        // ── Tests tab ─────────────────────────────────────────────────────────

        static GPALTab testsTab = GPAL.Tab.WithName("testsTab").WithText("Tests").ToGPALObject();

        static GPALLabel testsHint = GPAL.LabelFor("Tick the test sections to run. With none ticked, every section runs.");

        // one checkbox per test section, in the order they run
        static List<GPALCheckbox> testChecks = testNames.ConvertAll(name => GPAL.CheckboxFor(name, false));

        // the checkboxes in two columns, the first half on the left
        static GPALSplitter testColumns = TestColumns();

        // ── Log tab ───────────────────────────────────────────────────────────

        static GPALTab logTab = GPAL.Tab.WithName("logTab").WithText("Log").ToGPALObject();

        // The Log tab shows every event GPAL publishes. The status strip shows only what is running now.
        static GPALTextArea logArea = (GPALTextArea)GPAL.TextArea.WithReadOnly(true).WithHeight(420).WithName("log");

        // Every Log tab line since the last Clear Log. The box keeps only the last 2000 lines, which drops the start of
        // a long run; this keeps all of it for Save Log.
        static List<string> fullLog = new List<string>();

        static GPALButton clearLogBtn = GPAL.Button
            .WithName("clearLog")
            .WithText("🧹 Clear Log")
            .WithCallback<EventHandler>(OnClearLog)
            .ToGPALObject();

        // writes every line since the last Clear Log, not only the 2000 the box is showing
        static GPALButton saveLogBtn = GPAL.Button
            .WithName("saveLog")
            .WithText("💾 Save Log")
            .WithCallback<EventHandler>(OnSaveLog)
            .ToGPALObject();

        static GPALStatusStrip statusBar = GPAL.StatusStrip
            .WithText("Ready")
            .WithName("status")
            .ToGPALObject();

        [STAThread]
        static void Main(string[] args)
        {
            GPAL.GPALSettings.Clear();
            GPAL.LoadSettings();
            GPAL.WithPublishStackTrace(false);

            // Leave out DEBUG and DEEPDEBUG; they are GPAL's internal tracing, not for a person to read.
            GPAL.WithHandlerEvents(Enums.GPALEventType.All & ~(Enums.GPALEventType.DEBUG | Enums.GPALEventType.DEEPDEBUG))
                .WithExceptionHandler(OnGPALEvent)
                .WithInformationHandler(OnGPALEvent);

            var form = GPAL.Form
                .WithTitle("ConverterTestor  |  every input and output the converter handles")
                .WithWidth(760)
                .WithHeight(640)
                .WithFormControl(statusBar)

                .WithFormControl(runTab)
                .WithFormControl(aboutBlurb)
                .WithFormControl(debugCheck)
                .WithFormControl(GPAL.SpacerFor(18))
                .WithFormControl(runBtn)
                .WithFormControl(stopBtn)
                .WithFormControl(GPAL.SpacerFor(18))
                .WithFormControl(exitBtn)

                .WithFormControl(testsTab)
                .WithFormControl(testsHint)
                .WithFormControl(testColumns)

                .WithFormControl(logTab)
                .WithFormControl(clearLogBtn)
                .WithFormControl(saveLogBtn)
                .WithFormControl(logArea)

                .ToGPALObject();

            form.ShowDialog();
        }

        static void OnRun(object s, EventArgs e)
        {
            if (true == running)
                return;

            running = true;
            stopRequested = false;
            runBtn.Enabled = false;
            stopBtn.Enabled = true;

            // The ticked sections, or all when none is ticked.
            List<int> selected = new List<int>();
            for (int i = 0; i < testChecks.Count; i++)
                if (true == testChecks[i].Checked)
                    selected.Add(i);
            if (0 == selected.Count)
                for (int i = 0; i < testNames.Count; i++)
                    selected.Add(i);

            // DEEPDEBUG stays out either way: raw protocol traffic buries everything else
            GPAL.WithHandlerEvents(true == debugCheck.Checked
                ? Enums.GPALEventType.All & ~Enums.GPALEventType.DEEPDEBUG
                : Enums.GPALEventType.All & ~(Enums.GPALEventType.DEBUG | Enums.GPALEventType.DEEPDEBUG));

            try
            {
                RunTests(selected);
            }
            catch (Exception ex)
            {
                // Each section catches its own, so this catches only the run itself failing, and keeps the form
                // usable.
                GPAL.PublishSimpleEvent(Enums.GPALEventType.EXCEPTION, "The run ended in an exception", ex: ex);
            }
            finally
            {
                running = false;
                stopBtn.Enabled = false;
                runBtn.Enabled = true;
                SetStatus(true == stopRequested
                    ? "Stopped. The results for the sections that ran are on the Log tab."
                    : "Done. The results are on the Log tab.");
            }
        }

        // no test section starts after this; the one running finishes first
        static void OnStop(object s, EventArgs e)
        {
            stopRequested = true;
            stopBtn.Enabled = false;

            SetStatus("Stopping: the section running now finishes first...");
            GPAL.PublishSimpleEvent(Enums.GPALEventType.NOTICE, "Stop pressed, starting no more test sections");
        }

        // the test checkboxes in two columns, the first half on the left and the rest on the right
        static GPALSplitter TestColumns()
        {
            int half = (testChecks.Count + 1) / 2;
            IAllowSplitterControlSettings columns = GPAL.Splitter.HSplit(0.5);

            for (int i = 0; i < testChecks.Count; i++)
            {
                if (half == i)
                    columns = columns.WithFormControl(GPAL.SplitRight);
                columns = columns.WithFormControl(testChecks[i]);
            }

            GPALSplitter retVal = (GPALSplitter)columns.WithHeight(30 * half + 10).ToGPALObject();

            return retVal;
        }

        // The files, objects and converter all sections share are made first, then the selected sections run in order,
        // each in its own try/catch so one that throws is logged and the rest still run.
        static void RunTests(List<int> selected)
        {
            GPALDatabase database = (GPALDatabase)GPAL.Database
                .WithServerName(@"localhost")
                .WithDatabaseName("GPAL")
                .WithDatabaseType(Enums.DatabaseType.SQLServer)
                .WithTableName("TestInput")
                .WithRowCount(4);

            string columnNames = "col1, col2, col3";

            GPALFile inCSVFile = GPAL.File
                .WithFileName(TestData("sample.csv"))
                .WithColumnNames(columnNames)
                .WithFirstLineIsColumnNames()
                .WithDelimiter(',')
                .ToGPALObject();

            IGPALConverter converter = GPAL.Converter.RegisterKnownStructType(typeof(RecursiveStruct)).RegisterKnownStructType(typeof(InnerContainer)).ToGPALObject();
            // TODO
            // IGPALGrid<string> outGrid = GPAL.Grid.ToGPALObject();

            //converter
            //    .WithInput(inCSVFile)
            //    .SaveTo(outGrid);

            //var value = outGrid[3][2];

            GPALFile inSelectorsJsonlFile = TestData("selectors.json");

            GPALFile outSelectorsJsonlFile = GPAL.File
                .WithFileName(TestOutput("outSelectors.json"))
                .WithOverwriteFile(false)   // so when we call .Next, we get a new filename, otherwise we will just overwrite the one filename
                .ToGPALObject();

            GPALFile outAllDataTypesFile = TestOutput("outAllDataTypes.yaml");

            GPALFile inYamlFile = TestData("sample.yaml");

            GPALFile inXmlFile = TestData("sample.xml");

            GPALFile inJsonlFile = TestData("sample.json");

            GPALFile inCsvFile = TestData("sample.csv");

            GPALFile inHtmlFile = TestData("sample.html");

            GPALFile outYamlFile = GPAL.File
                .WithFileName(TestOutput("outsample.yaml"))
                .WithOverwriteFile(false)   // so when we call .Next, we get a new filename, otherwise we will just overwrite the one filename
                .ToGPALObject();

            GPALFile outXmlFile = GPAL.File
                .WithFileName(TestOutput("outsample.xml"))
                .WithOverwriteFile(false)   // so when we call .Next, we get a new filename, otherwise we will just overwrite the one filename
                .ToGPALObject();

            GPALFile outJsonFile = GPAL.File
                .WithFileName(TestOutput("outsample.json"))
                .WithOverwriteFile(false)   // so when we call .Next, we get a new filename, otherwise we will just overwrite the one filename
                .ToGPALObject();

            GPALFile outCsvFile = GPAL.File
                .WithFileName(TestOutput("outsample.csv"))
                .WithOverwriteFile(false)   // so when we call .Next, we get a new filename, otherwise we will just overwrite the one filename
                .WithFirstLineIsColumnNames()
                .ToGPALObject();

            GPALFile outHtmlFile = GPAL.File
                .WithFileName(TestOutput("outsample.html"))
                .WithOverwriteFile(false)   // so when we call .Next, we get a new filename, otherwise we will just overwrite the one filename
                .ToGPALObject();

            Npis = new List<NPIData>();
            List<RevalidationData> RevalidationDataList = new List<RevalidationData>();
            List<RevalidationData> adjustedRevalidationDataList = new List<RevalidationData>();   // assigned below only when the large-dataset test is enabled

        // List<TestClass> selectorSets = new List<TestClass>();   // negative testing - nothing is filled, just empty object5s

        List<SelectorSet> selectorSets = new List<SelectorSet>();   // negative testing - nothing is filled, just empty object5s
            //            Dictionary<dynamic, SelectorSet> selectorSets = new Dictionary<dynamic, SelectorSet>();
            //            Queue<SelectorSet> selectorSets = new Queue<SelectorSet>();
            //            Stack<SelectorSet> selectorSets = new Stack<SelectorSet>();
            //            HashSet<SelectorSet> selectorSets = new HashSet<SelectorSet>();

            var inDict = new Dictionary<dynamic, TestClass>
            {
                { "item1", new TestClass("First", 10, new Dictionary<string, string> { { "tag", "a" } }) },
                { 2, new TestClass("Second", 20, new Dictionary<string, string> { { "tag", "b" } }) }
            };

            var inList = new List<TestClass>
            {
                new TestClass("First", 10, new Dictionary<string, string> { { "tag", "a" } }),
                new TestClass("Second", 20, new Dictionary<string, string> { { "tag", "b" } })
            };

            var inQueue = new Queue<TestClass>();
            inQueue.Enqueue(new TestClass("First", 10, new Dictionary<string, string> { { "tag", "a" } }));
            inQueue.Enqueue(new TestClass("Second", 20, new Dictionary<string, string> { { "tag", "b" } }));

            var inStack = new Stack<TestClass>();
            inStack.Push(new TestClass("First", 10, new Dictionary<string, string> { { "tag", "a" } }));
            inStack.Push(new TestClass("Second", 20, new Dictionary<string, string> { { "tag", "b" } }));

            var inHash = new HashSet<TestClass>
            {
                new TestClass("First", 10, new Dictionary<string, string> { { "tag", "a" } }),
                new TestClass("Second", 20, new Dictionary<string, string> { { "tag", "b" } })
            };

            // Output for Dictionary<dynamic, SelectorSet>
            var outputDict = new Dictionary<dynamic, TestClass>();

            // Output for List<SelectorSet>
            var outputList = new List<TestClass>();

            // Output for Queue<SelectorSet>
            var outputQueue = new Queue<TestClass>();

            // Output for Stack<SelectorSet>
            var outputStack = new Stack<TestClass>();

            // Output for HashSet<SelectorSet>
            var outputHash = new HashSet<TestClass>();


            List<string> listSelectors = new List<string>();

            GPALFile revalidationFileCsv = GPAL.File
                .WithFileName("Revalidation_Entities_" + DateTime.Now.ToString("MMddyyyyhhmm") + ".csv")
                .WithColumnsEnclosedInQuotes(true)
                .WithColumnName("Client ID")
                .WithColumnName("Entity Name")
                .WithColumnName("NPI Number")
                .WithColumnName("Organization Name")
                .WithColumnName("Enrollment State Code")
                .WithColumnName("Enrollment Specialty")
                .WithColumnName("Revalidation Date")
                .ToGPALObject();

            GPALFile revalidationFileYaml = GPAL.File
                .WithFileName("Revalidation_Entities_" + DateTime.Now.ToString("MMddyyyyhhmm") + ".yaml")
                .WithColumnName("Client ID")
                .WithColumnName("Entity Name")
                .WithColumnName("NPI Number")
                .WithColumnName("Organization Name")
                .WithColumnName("Enrollment State Code")
                .WithColumnName("Enrollment Specialty")
                .WithColumnName("Revalidation Date")
                .ToGPALObject();

            GPALFile revalidationFileJson = GPAL.File
                .WithFileName("Revalidation_Entities_" + DateTime.Now.ToString("MMddyyyyhhmm") + ".json")
                .WithColumnName("Client ID")
                .WithColumnName("Entity Name")
                .WithColumnName("NPI Number")
                .WithColumnName("Organization Name")
                .WithColumnName("Enrollment State Code")
                .WithColumnName("Enrollment Specialty")
                .WithColumnName("Revalidation Date")
                .ToGPALObject();

            AllDataTypes allDataTypes = new AllDataTypes();
            allDataTypes.Initialize();  // init with data

            goto here;
        here:
            // one entry per name in testNames, in the same order
            List<Action> sections = new List<Action>
            {
                // ---------------------------------------------------------------- class with every data type
                () =>
                {
                    // INPUT: CLASS all datatypes custom class
                    converter
                        .RegisterKnownStructType(typeof(RecursiveStruct))
                        .WithInput(allDataTypes)
                        .SaveTo(outAllDataTypesFile)
                        .SaveTo(outCsvFile.Next)
                        .SaveTo(outJsonFile.Next)
                        .SaveTo(outHtmlFile.Next)
                        .SaveTo(outXmlFile.Next);

                    // INPUT: ALL DATA TYPES file just written
                    AllDataTypes emptyAllDataTypes = new AllDataTypes();  // empty class to fill

                    converter
                        .RegisterKnownStructType(typeof(InnerContainer))
                        .RegisterKnownStructType(typeof(RecursiveStruct))
                        .WithInput(outAllDataTypesFile)
                        .SaveTo(ref emptyAllDataTypes);

                    // what the class wrote out, which the round trip below is compared against
                    string writtenOut = System.IO.File.ReadAllText(outAllDataTypesFile.Last);

                    // ---------------------------------------------------------------- yaml back to the class
                    // Everything but graphs with back references must survive the trip. The reader's dictionary cannot
                    // say "this node is that node", so an alias comes back as a copy. Those members are named here.
                    string[] cyclicMembers = { "stringTree", "rootRecursiveStruct", "recursiveStructArray",
                                               "containerWithPayload", "containerList", "strangePayload",
                                               "entityWithComplexValue", "tupleInsanity" };

                    GPALFile roundTripFile = TestOutput("outAllDataTypes.roundtrip.yaml");

                    converter
                        .RegisterKnownStructType(typeof(InnerContainer))
                        .RegisterKnownStructType(typeof(RecursiveStruct))
                        .WithInput(emptyAllDataTypes)
                        .SaveTo(roundTripFile);

                    Dictionary<string, string> wroteMembers = TopLevelMembers(writtenOut);
                    Dictionary<string, string> backMembers = TopLevelMembers(System.IO.File.ReadAllText(roundTripFile.Last));

                    List<string> lost = new List<string>();

                    // Masking would hide a timestamp that came back in another form, so these two are compared as
                    // written. They are lists of DateTime, one UTC, and the round trip must keep the Kind.
                    Dictionary<string, string> wroteRaw = TopLevelMembersRaw(writtenOut);
                    Dictionary<string, string> backRaw = TopLevelMembersRaw(System.IO.File.ReadAllText(roundTripFile.Last));

                    foreach (string exact in new[] { "dateTimeListArrayField", "nullableDateTimeListArrayField" })
                        if (false == backRaw.ContainsKey(exact) || backRaw[exact] != wroteRaw[exact])
                            lost.Add(exact + " (kind or offset changed)");

                    foreach (KeyValuePair<string, string> member in wroteMembers)
                    {
                        if (true == cyclicMembers.Contains(member.Key))
                            continue;

                        if (false == backMembers.ContainsKey(member.Key) || backMembers[member.Key] != member.Value)
                            lost.Add(member.Key);
                    }

                    GPAL.PublishSimpleEvent(0 == lost.Count ? Enums.GPALEventType.NOTICE : Enums.GPALEventType.FAILURE,
                        $"YAML to CLASS: [{wroteMembers.Count - cyclicMembers.Length}] of [{wroteMembers.Count}] members compared, [{lost.Count}] did not survive the trip{(0 == lost.Count ? "" : ": " + string.Join(", ", lost))}. [{cyclicMembers.Length}] members hold back references and are excluded, see the note on cycles");

                    // ---------------------------------------------------------------- the anchors and aliases themselves
                    // The round trip above cannot speak for the cyclic members, since an alias comes back as a copy and
                    // they are excluded. They are checked here against the document and the class, not a saved copy of
                    // last run's output, which would be this output stored twice and go stale on the first change.
                    AnchorsAndAliases(writtenOut, out List<string> anchors, out List<string> aliases);

                    List<string> notation = new List<string>();

                    // An alias naming a missing anchor, or an anchor nothing refers to, makes the document unreadable.
                    foreach (string alias in aliases.Distinct())
                        if (false == anchors.Contains(alias))
                            notation.Add($"alias [*{alias}] has no anchor");

                    foreach (string anchorName in anchors.Distinct())
                        if (false == aliases.Contains(anchorName))
                            notation.Add($"anchor [&{anchorName}] is never referred to");

                    // What the cyclic notation replaced. Finding it means the writer stopped following the graph.
                    if (true == writtenOut.Contains("Recursion to ["))
                        notation.Add("the output carries \"Recursion to [...]\" placeholders instead of aliases");

                    // Members known to hold a back reference must produce a reference.
                    foreach (string cyclic in cyclicMembers)
                        if (true == wroteMembers.ContainsKey(cyclic)
                            && false == HasAnchorOrAlias(wroteMembers[cyclic]))
                            notation.Add($"[{cyclic}] holds a back reference but emitted neither an anchor nor an alias");

                    GPAL.PublishSimpleEvent(0 == notation.Count ? Enums.GPALEventType.NOTICE : Enums.GPALEventType.FAILURE,
                        $"YAML anchors: [{anchors.Distinct().Count()}] anchors and [{aliases.Count}] references, every one resolved, across [{cyclicMembers.Length}] members holding back references{(0 == notation.Count ? "" : ". FAILED: " + string.Join("; ", notation))}");
                },

                // ---------------------------------------------------------------- excel workbook
                () =>
                {
                    GPALFile ExcelInFile = GPAL.File.WithFileName(MakeWorkbook()).WithFirstLineIsColumnNames(true).ToGPALObject();
                    GPALFile ExcelOutFile = System.IO.Path.Combine(TestOutputDirectory(), "outWorkbook.xlsx");

                    converter.WithInput(ExcelInFile).SaveTo(ExcelOutFile);
                },

                // ---------------------------------------------------------------- collections to collections
                () =>
                {
                    converter
                        .WithInput(inDict)
                        .SaveTo(ref outputDict);

                    converter
                        .SaveTo(ref outputList);

                    converter
                        .SaveTo(ref outputQueue);

                    converter
                        .SaveTo(ref outputStack);

                    converter
                        .SaveTo(ref outputHash);

                    outputDict.Clear();
                    outputList.Clear();
                    outputQueue.Clear();
                    outputStack.Clear();
                    outputHash.Clear();

                    converter
                        .WithInput(inList)
                        .SaveTo(ref outputDict);

                    converter
                        .SaveTo(ref outputList);

                    converter
                        .SaveTo(ref outputQueue);

                    converter
                        .SaveTo(ref outputStack);

                    converter
                        .SaveTo(ref outputHash);

                    outputDict.Clear();
                    outputList.Clear();
                    outputQueue.Clear();
                    outputStack.Clear();
                    outputHash.Clear();

                    converter
                        .WithInput(inQueue)
                        .SaveTo(ref outputDict);

                    converter
                        .SaveTo(ref outputList);

                    converter
                        .SaveTo(ref outputQueue);

                    converter
                        .SaveTo(ref outputStack);

                    converter
                        .SaveTo(ref outputHash);

                    outputDict.Clear();
                    outputList.Clear();
                    outputQueue.Clear();
                    outputStack.Clear();
                    outputHash.Clear();

                    converter
                        .WithInput(inStack)
                        .SaveTo(ref outputDict);

                    converter
                        .SaveTo(ref outputList);

                    converter
                        .SaveTo(ref outputQueue);

                    converter
                        .SaveTo(ref outputStack);

                    converter
                        .SaveTo(ref outputHash);

                    outputDict.Clear();
                    outputList.Clear();
                    outputQueue.Clear();
                    outputStack.Clear();
                    outputHash.Clear();

                    converter
                        .WithInput(inHash)
                        .SaveTo(ref outputDict);

                    converter
                        .SaveTo(ref outputList);

                    converter
                        .SaveTo(ref outputQueue);

                    converter
                        .SaveTo(ref outputStack);

                    converter
                        .SaveTo(ref outputHash);

                // TODO: Test again
                //converter.WithInput(database).SaveTo(outJsonlFile);
                },

                // ---------------------------------------------------------------- yaml file
                () =>
                {
                    converter
                        .WithInput(inYamlFile)
                        .WithFirstLineHasColumnNames(true)
                        .SaveTo(outCsvFile.Next)
                        .SaveTo(outJsonFile.Next)
                        .SaveTo(outHtmlFile.Next)
                        .SaveTo(outYamlFile.Next)
                        .SaveTo(ref listSelectors)
                        .SaveTo(outXmlFile.Next);
                },

                // ---------------------------------------------------------------- xml file
                () =>
                {
                    //  INPUT: XML sample sweater info
                    converter
                        .WithInput(inXmlFile)
                        .SaveTo(outCsvFile.Next)
                        .SaveTo(outJsonFile.Next)
                        .SaveTo(outHtmlFile.Next)
                        .SaveTo(outXmlFile.Next)
                        .SaveTo(outYamlFile.Next);
                },

                // ---------------------------------------------------------------- selector sets, with the negative tests
                () =>
                {
                // INPUT SelectorSet
                    converter
                        .WithInput(inSelectorsJsonlFile)
                        .SaveTo(ref selectorSets);

                    SelectorSet[] selectorSets2 = new SelectorSet[4];

                    converter
                        .WithInput(inSelectorsJsonlFile)
                        .SaveTo(ref selectorSets2);

                    converter
                        .WithInput(selectorSets)
                        //.SaveTo(outSelectorsJsonlFile)
                        .SaveTo(outHtmlFile.Next)
                        .SaveTo(outXmlFile.Next);

                    converter
                        .WithInput(selectorSets2)
                        .SaveTo(outSelectorsJsonlFile.Next);

                    TestClass[] nonSelectorSets = new TestClass[4];    // negative testing, no data filled, just empty objects
                    converter
                        .WithInput(inSelectorsJsonlFile)
                        .SaveTo(ref nonSelectorSets);
                },

                // ---------------------------------------------------------------- html file
                () =>
                {
                    // INPUT: HTML alien heat album streamer
                    converter
                        .WithInput(inHtmlFile)
                        .SaveTo(outCsvFile.Next)
                        .SaveTo(outJsonFile.Next)
                        .SaveTo(outHtmlFile.Next)
                        .SaveTo(outXmlFile.Next)
                        .SaveTo(outYamlFile.Next);
                },

                // ---------------------------------------------------------------- the alias limit
                () =>
                {
                    // The one place size changes what the converter writes. At or past the limit a shared reference is
                    // written out again instead of as an alias, because the pre-pass that finds shared references
                    // stalls Serialize on a large flat collection. A cycle keeps its aliases at any size: YamlDotNet
                    // cannot write one without them and throws MaximumRecursionLevelReached. All three cases are in
                    // memory; the cutoff is under test, not a file.
                    // Run at a limit of 50, not the default 5000: the rule is the same at any size, and a 5000-node
                    // cycle takes minutes to write. The configured limit is put back at the end.
                    int configuredLimit = GPAL.GPALSettings.YamlAliasLimit;
                    const int aliasLimit = 50;

                    GPAL.WithYamlAliasLimit(aliasLimit);

                    // The list is a node as well as each entry, so each item count is one less than its node count.
                    foreach (int graphNodes in new[] { aliasLimit - 1, aliasLimit + 1 })
                    {
                        // A class, because the shared thing must be a reference. A struct in a list N times is N
                        // copies, nothing to alias.
                        WeirdPayload shared = new WeirdPayload { CorrelationId = Guid.NewGuid() };
                        List<WeirdPayload> graph = new List<WeirdPayload>();

                        for (int item = 0; item < graphNodes - 1; item++)
                            graph.Add(shared);

                        GPALFile limitFile = System.IO.Path.Combine(TestOutputDirectory(), $"outAliasLimit.{graphNodes}.yaml");

                        converter.WithInput(graph).SaveTo(limitFile);

                        AnchorsAndAliases(System.IO.File.ReadAllText(limitFile.Last), out List<string> limitAnchors, out List<string> limitAliases);

                        bool below = graphNodes < aliasLimit;
                        bool asExpected = below ? 0 < limitAliases.Count : 0 == limitAliases.Count;

                        GPAL.PublishSimpleEvent(true == asExpected ? Enums.GPALEventType.NOTICE : Enums.GPALEventType.FAILURE,
                            $"ALIAS LIMIT: [{graphNodes}] nodes is {(true == below ? "below" : "at or past")} the limit of [{aliasLimit}] and holds no cycle, so aliases are {(true == below ? "kept" : "dropped")}. Written with [{limitAnchors.Count}] anchors and [{limitAliases.Count}] references");
                    }

                    // past the limit and cyclic. the limit must not apply, or this throws instead of writing
                    WeirdPayload cyclicShared = new WeirdPayload { CorrelationId = Guid.NewGuid() };
                    cyclicShared.SelfReference = cyclicShared;

                    List<WeirdPayload> cyclicGraph = new List<WeirdPayload>();

                    for (int item = 0; item < aliasLimit; item++)
                        cyclicGraph.Add(cyclicShared);

                    GPALFile cyclicFile = System.IO.Path.Combine(TestOutputDirectory(), "outAliasLimit.cyclic.yaml");

                    converter.WithInput(cyclicGraph).SaveTo(cyclicFile);

                    bool cyclicWritten = System.IO.File.Exists(cyclicFile.Last);

                    AnchorsAndAliases(true == cyclicWritten ? System.IO.File.ReadAllText(cyclicFile.Last) : "",
                        out List<string> cyclicAnchors, out List<string> cyclicAliases);

                    GPAL.PublishSimpleEvent(true == cyclicWritten && 0 < cyclicAliases.Count ? Enums.GPALEventType.NOTICE : Enums.GPALEventType.FAILURE,
                        $"ALIAS LIMIT, CYCLIC: [{aliasLimit + 1}] nodes is past the limit but holds a cycle, so aliases are kept. Written with [{cyclicAnchors.Count}] anchors and [{cyclicAliases.Count}] references");

                    // and 0 says never drop them, whatever the size
                    GPAL.WithYamlAliasLimit(0);

                    GPALFile neverFile = System.IO.Path.Combine(TestOutputDirectory(), "outAliasLimit.never.yaml");

                    converter.WithInput(cyclicGraph).SaveTo(neverFile);

                    AnchorsAndAliases(System.IO.File.ReadAllText(neverFile.Last), out List<string> neverAnchors, out List<string> neverAliases);

                    GPAL.WithYamlAliasLimit(configuredLimit);

                    GPAL.PublishSimpleEvent(0 < neverAliases.Count ? Enums.GPALEventType.NOTICE : Enums.GPALEventType.FAILURE,
                        $"ALIAS LIMIT, ZERO: a limit of 0 keeps aliases at [{aliasLimit + 1}] nodes. Written with [{neverAnchors.Count}] anchors and [{neverAliases.Count}] references");
                },

                // ---------------------------------------------------------------- simple custom class
                () =>
                {
                    // INPUT: CLASS simple custom class, inDict above also uses the custom TestClass
                    converter
                        .WithInput(MyClass)
                        .WithFirstLineHasColumnNames(true)
                        .SaveTo(outCsvFile.Next)
                        .SaveTo(outXmlFile.Next)
                        .SaveTo(outJsonFile.Next)
                        .SaveTo(outYamlFile.Next)
                        .SaveTo(outHtmlFile.Next);
                },

                // ---------------------------------------------------------------- csv file
                () =>
                {
                    // INPUT: 3 columnd headers, 3 values = 1, "2", three
                    converter
                        .WithInput(inCsvFile)   // todo,parse out nesting using .
                        .WithFirstLineHasColumnNames(true)
                        .SaveTo(outCsvFile.Next)
                        .SaveTo(outJsonFile.Next)
                        .AppendTo(outXmlFile.Next)
                        .SaveTo(outYamlFile.Next)
                        .SaveTo(outHtmlFile.Next);
                },

                // ---------------------------------------------------------------- json file
                () =>
                {
                    // INPUT: JSON sample donuts info
                    converter
                        .WithInput(inJsonlFile)
                        .SaveTo(outCsvFile.Next)  // add .next to go to the next filename or generate a next filename
                        .SaveTo(outJsonFile.Next)
                        .SaveTo(outHtmlFile.Next)
                        .SaveTo(outXmlFile.Next)
                        .SaveTo(outYamlFile.Next);
                },

                // ---------------------------------------------------------------- grid input and grid to grid
                () =>
                {
                    // INPUT: GRID - scraped rows like BookList produces. Verifies: one dictionary per source, not per
                    // row; caller columns via WithColumnName/s, which override the source and backfill what the caller
                    // omits; column names as metadata (a header row is written only because outCsvFile set
                    // WithFirstLineIsColumnNames); and converter reuse across chained SaveTo.
                    IGPALGrid<string> bookGrid = GPAL.Grid.ToGPALObject();
                    bookGrid.AddRow(new List<string> { "The Hobbit", "Tolkien", "12.99" });
                    bookGrid.AddRow(new List<string> { "Dune", "Herbert", "9.50" });
                    bookGrid.AddRow(new List<string> { "Neuromancer", "Gibson", "8.75" });

                    // caller supplies 2 of 3 column names; the 3rd is generated (a grid has no source names)
                    converter
                        .WithInput(bookGrid)
                        .WithColumnNames(new List<string> { "Title", "Author" })
                        .SaveTo(outCsvFile.Next)
                        .SaveTo(outJsonFile.Next)
                        .SaveTo(outYamlFile.Next)
                        .SaveTo(outXmlFile.Next)
                        .SaveTo(outHtmlFile.Next);

                    // full column set built one name at a time
                    converter
                        .WithInput(bookGrid)
                        .WithColumnName("Title")
                        .WithColumnName("Author")
                        .WithColumnName("Price")
                        .SaveTo(outCsvFile.Next)
                        .SaveTo(outJsonFile.Next);

                    // no columns supplied - headerless grid, still one dictionary per source
                    converter
                        .WithInput(bookGrid)
                        .SaveTo(outCsvFile.Next)
                        .SaveTo(outJsonFile.Next);

                    // INPUT: GRID -> OUTPUT: GRID. A grid asked for as a grid is a copy of the input, so there is
                    // nothing to eyeball and the check reports itself. Checks the rows and columns match and the values
                    // survive, then writes the copy out beside the rest.
                    IGPALGrid<string> copiedGrid = GPAL.Grid.ToGPALObject();

                    converter
                        .WithInput(bookGrid)
                        .SaveTo(ref copiedGrid);

                    bool sameShape = bookGrid.Rows == copiedGrid.Rows && bookGrid.Columns == copiedGrid.Columns;
                    bool sameValues = sameShape;

                    for (int rowIdx = 0; rowIdx < bookGrid.Rows && true == sameValues; rowIdx++)
                        sameValues = string.Join(",", bookGrid[rowIdx]) == string.Join(",", copiedGrid[rowIdx]);

                    GPAL.PublishSimpleEvent(true == sameValues ? Enums.GPALEventType.NOTICE : Enums.GPALEventType.FAILURE,
                        $"GRID to GRID: [{copiedGrid.Rows}] of [{bookGrid.Rows}] rows and [{copiedGrid.Columns}] of [{bookGrid.Columns}] columns came back, values {(true == sameValues ? "match" : "do not match")}");

                    converter
                        .WithInput(copiedGrid)
                        .SaveTo(outCsvFile.Next);
                },

                // ---------------------------------------------------------------- csv file to grid
                () =>
                {
                    // INPUT: CSV FILE -> OUTPUT: GRID. A file read into a grid keeps its shape on disk. Tests the
                    // object-output path with non-grid input. sample.csv is one row of three columns.
                    IGPALGrid<string> fromCsvGrid = GPAL.Grid.ToGPALObject();

                    converter
                        .WithInput(inCSVFile)
                        .SaveTo(ref fromCsvGrid);

                    // .WithFirstLineIsColumnNames says the first line names the columns. A grid carries its headers as
                    // row zero, as GetCompareResults and GetSearchResults do, so a csv with a header and one data row
                    // is two grid rows.
                    bool csvShape = 2 == fromCsvGrid.Rows && 3 == fromCsvGrid.Columns;
                    bool csvHeader = 2 == fromCsvGrid.Rows && "column1,column2,column3" == string.Join(",", fromCsvGrid[0]);
                    bool csvValues = 2 == fromCsvGrid.Rows && "1,2,three" == string.Join(",", fromCsvGrid[1]);

                    GPAL.PublishSimpleEvent(true == csvShape && true == csvHeader && true == csvValues ? Enums.GPALEventType.NOTICE : Enums.GPALEventType.FAILURE,
                        $"CSV to GRID: [{fromCsvGrid.Rows}] rows and [{fromCsvGrid.Columns}] columns came back, expected [2] and [3]. Header row reads [{(0 < fromCsvGrid.Rows ? string.Join(",", fromCsvGrid[0]) : "")}], data row reads [{(1 < fromCsvGrid.Rows ? string.Join(",", fromCsvGrid[1]) : "")}]");
                },

                // ---------------------------------------------------------------- text files read as one column
                () =>
                {
                    // A .txt or .log has nothing to split on, so each line is one row of one column. The final newline
                    // ends the last line rather than adding an empty one; a blank line inside the file is a row.
                    IGPALGrid<string> txtGrid = GPAL.Grid.ToGPALObject();

                    converter
                        .WithInput((GPALFile)TestData("sample.txt"))
                        .SaveTo(ref txtGrid);

                    bool txtShape = 3 == txtGrid.Rows && 1 == txtGrid.Columns;
                    bool txtFirst = 0 < txtGrid.Rows && "first line of plain text" == txtGrid[0][0];
                    bool txtLast = 3 == txtGrid.Rows && "third line" == txtGrid[2][0];

                    GPAL.PublishSimpleEvent(true == txtShape && true == txtFirst && true == txtLast ? Enums.GPALEventType.NOTICE : Enums.GPALEventType.FAILURE,
                        $"TXT to GRID: [{txtGrid.Rows}] rows and [{txtGrid.Columns}] columns came back, expected [3] and [1], last row reads [{(2 < txtGrid.Rows ? txtGrid[2][0] : "")}]");

                    IGPALGrid<string> logGrid = GPAL.Grid.ToGPALObject();

                    converter
                        .WithInput((GPALFile)TestData("sample.log"))
                        .SaveTo(ref logGrid);

                    bool logShape = 5 == logGrid.Rows && 1 == logGrid.Columns;
                    bool logBlank = 5 == logGrid.Rows && "" == logGrid[2][0];

                    GPAL.PublishSimpleEvent(true == logShape && true == logBlank ? Enums.GPALEventType.NOTICE : Enums.GPALEventType.FAILURE,
                        $"LOG to GRID: [{logGrid.Rows}] rows and [{logGrid.Columns}] columns came back, expected [5] and [1], the blank line is row 3 and reads [{(2 < logGrid.Rows ? logGrid[2][0] : "?")}]");
                },

                // ---------------------------------------------------------------- an extension with no reader
                () =>
                {
                    // Nothing reads a .dat, so tokenizing warns and returns false instead of reporting an unopened file
                    // as read.
                    IGPALGrid<string> datGrid = GPAL.Grid.ToGPALObject();

                    converter
                        .WithInput((GPALFile)TestData("sample.dat"))
                        .SaveTo(ref datGrid);

                    GPAL.PublishSimpleEvent(0 == datGrid.Rows ? Enums.GPALEventType.NOTICE : Enums.GPALEventType.FAILURE,
                        $"UNREADABLE EXTENSION: a .dat gave back [{datGrid.Rows}] rows, expected [0], and a warning naming the file should be above this line");
                },

                // ---------------------------------------------------------------- one column list per file
                () =>
                {
                    // two files with different headers. without .WithUseSameColumnNamesForAllFiles each file is read
                    // with the list given for it, in file order
                    GPALFile multiFiles = GPAL.File
                        .WithFileName(TestData("multi.*.csv"))
                        .WithColumnNames("id, name")
                        .WithColumnNames("code, city, zip")
                        .WithDelimiter(',')
                        .WithFirstLineIsColumnNames()
                        .ToGPALObject();

                    GPALFile outMultiFile = GPAL.File
                        .WithFileName(TestOutput("outmulti.csv"))
                        .WithOverwriteFile(false)
                        .WithFirstLineIsColumnNames()
                        .ToGPALObject();

                    IGPALGrid<string> multiGrid = GPAL.Grid.ToGPALObject();

                    converter
                        .WithInput(multiFiles)
                        .SaveTo(ref multiGrid)
                        .SaveTo(outMultiFile.Next);

                    // file one is id,name over two rows and file two is code,city,zip over two rows, each with its own
                    // header line, so both files read means six rows
                    bool multiRows = 6 == multiGrid.Rows;

                    for (int row = 0; row < multiGrid.Rows; row++)
                        GPAL.PublishSimpleEvent(Enums.GPALEventType.INFO, $"PER FILE COLUMNS row [{row}]: [{string.Join(",", multiGrid[row])}]");

                    GPAL.PublishSimpleEvent(true == multiRows ? Enums.GPALEventType.NOTICE : Enums.GPALEventType.FAILURE,
                        $"PER FILE COLUMNS: [{multiFiles.Filenames.Count}] files read into [{multiGrid.Rows}] grid rows, expected [6], written to [{string.Join(", ", outMultiFile.ReturnFilenames)}]");
                },

                // ---------------------------------------------------------------- json shapes
                () =>
                {
                    // Each JSON file is written back out as JSON, YAML and XML, and what comes out is compared with
                    // what went in, not just written. object-root.json has an object at the top, nulls and an id that
                    // looks like a number. keyvalue-pairs.json is a list of {Key, Value} pairs whose keys are objects,
                    // the shape a dictionary keyed by a class is written as.
                    var reader = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue };

                    foreach (string name in new[] { "object-root.json", "keyvalue-pairs.json" })
                    {
                        string inPath = TestData(name);
                        object original = reader.DeserializeObject(System.IO.File.ReadAllText(inPath));
                        string stem = System.IO.Path.GetFileNameWithoutExtension(name);

                        GPALFile jsonOut = TestOutput($"out.{stem}.json");
                        GPALFile yamlOut = TestOutput($"out.{stem}.yaml");
                        GPALFile xmlOut = TestOutput($"out.{stem}.xml");

                        converter
                            .WithInput((GPALFile)inPath)
                            .SaveTo(jsonOut)
                            .SaveTo(yamlOut)
                            .SaveTo(xmlOut);

                        // JSON to JSON is the same data, nothing added around it and nothing lost
                        object backAgain = reader.DeserializeObject(System.IO.File.ReadAllText(jsonOut.Filename));
                        bool sameJson = SameData(original, backAgain);

                        GPAL.PublishSimpleEvent(true == sameJson ? Enums.GPALEventType.NOTICE : Enums.GPALEventType.FAILURE,
                            $"JSON SHAPES [{name}] to JSON: the data {(true == sameJson ? "matches" : "does not match")} the input, written to [{jsonOut.Filename}]");

                        // YAML and XML: every value in the input is in the output, and nothing in the output is a .NET
                        // type name standing in for data
                        List<string> values = new List<string>();
                        LeafValues(original, values);

                        foreach (GPALFile written in new[] { yamlOut, xmlOut })
                        {
                            string text = System.IO.File.ReadAllText(written.Filename);
                            bool isXml = written.Filename.EndsWith(".xml");
                            List<string> missing = values.Where(v => false == text.Contains(true == isXml ? System.Security.SecurityElement.Escape(v) : v)).ToList();
                            bool typeNames = text.Contains("System.Collections");

                            GPAL.PublishSimpleEvent(0 == missing.Count && false == typeNames ? Enums.GPALEventType.NOTICE : Enums.GPALEventType.FAILURE,
                                $"JSON SHAPES [{name}] to {(true == isXml ? "XML" : "YAML")}: [{values.Count - missing.Count}] of [{values.Count}] input values found{(0 < missing.Count ? $", missing [{string.Join(", ", missing.Take(5))}]" : string.Empty)}{(true == typeNames ? ", and a .NET type name is written in place of data" : string.Empty)}, written to [{written.Filename}]");
                        }
                    }

                    // An object at the top is the data itself. The reader keeps it under a "root" key of its own,
                    // which must not reach the output: no root: in YAML, and one <root>, the document's, in XML.
                    string objectYaml = System.IO.File.ReadAllText(TestOutput("out.object-root.yaml"));
                    string objectXml = System.IO.File.ReadAllText(TestOutput("out.object-root.xml"));
                    int xmlRoots = (objectXml.Length - objectXml.Replace("<root>", string.Empty).Length) / "<root>".Length;
                    bool noYamlRoot = false == objectYaml.TrimStart().StartsWith("root:");

                    GPAL.PublishSimpleEvent(true == noYamlRoot && 1 == xmlRoots ? Enums.GPALEventType.NOTICE : Enums.GPALEventType.FAILURE,
                        $"JSON SHAPES [object-root.json]: YAML {(true == noYamlRoot ? "starts with the data" : "starts with root:")}, XML has [{xmlRoots}] <root> elements, expected [1]");

                    // A large file with nulls, timed. Every exception thrown and caught inside a conversion stops a
                    // debugger, so they are counted as well as the time.
                    StringBuilder large = new StringBuilder("[");

                    for (int record = 0; record < 3_000; record++)
                    {
                        large.Append(0 == record ? "{" : ",{");

                        for (int field = 0; field < 25; field++)
                            large.Append($"{(0 == field ? string.Empty : ",")}\"field{field}\":{(0 == field % 3 ? "null" : $"\"{record:D4}-{field}\"")}");

                        large.Append("}");
                    }

                    large.Append("]");

                    int thrown = 0;
                    EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> count = (o, ev) => thrown++;
                    GPALFile largeOut = TestOutput("out.large.json");
                    Stopwatch timed = Stopwatch.StartNew();

                    AppDomain.CurrentDomain.FirstChanceException += count;

                    converter
                        .WithInput(large.ToString())
                        .SaveTo(largeOut);

                    AppDomain.CurrentDomain.FirstChanceException -= count;
                    timed.Stop();

                    bool sameLarge = SameData(reader.DeserializeObject(large.ToString()), reader.DeserializeObject(System.IO.File.ReadAllText(largeOut.Filename)));

                    GPAL.PublishSimpleEvent(true == sameLarge && 0 == thrown ? Enums.GPALEventType.NOTICE : Enums.GPALEventType.FAILURE,
                        $"JSON SHAPES large: [3000] records of [25] fields, a third of them null, [{large.Length:N0}] characters, took [{timed.ElapsedMilliseconds}] ms. The data {(true == sameLarge ? "matches" : "does not match")} the input. [{thrown}] exceptions were thrown and caught inside it");
                },
            };

            foreach (int section in selected)
            {
                if (true == stopRequested)
                    break;

                SetStatus($"Running [{testNames[section]}]...");
                GPAL.PublishSimpleEvent(Enums.GPALEventType.NOTICE, $"Starting [{testNames[section]}]");

                // one section that throws is logged and the rest still run
                try
                {
                    sections[section]();
                }
                catch (Exception ex)
                {
                    GPAL.PublishSimpleEvent(Enums.GPALEventType.EXCEPTION, $"[{testNames[section]}] ended in an exception, going on to the next section", ex: ex);
                }
            }

            goto end;
        end:
            ;
        }

        static void OnGPALEvent(object sender, GPAL.GPALEventArgs e)
        {
            string tag = e.GPALEventType == Enums.GPALEventType.WARNING   ? "WARN"
                       : e.GPALEventType == Enums.GPALEventType.ERROR     ? "ERR "
                       : e.GPALEventType == Enums.GPALEventType.EXCEPTION ? "EXCP"
                       : e.GPALEventType == Enums.GPALEventType.FAILURE   ? "FAIL"
                       : e.GPALEventType == Enums.GPALEventType.NOTICE    ? "NOTE"
                       : e.GPALEventType == Enums.GPALEventType.DEBUG     ? "DBG "
                       : "INFO";
            string line = $"[{e.DateTimeStamp:HH:mm:ss}] [{tag}] {e.Message}";
            if (e.ExceptionRaised != null) line += $"  ({e.ExceptionRaised.Message})";
            Debug.WriteLine(line);

            // events arrive from the run's thread and the ui thread both
            lock (fullLog)
                fullLog.Add(line);

            // AppendLine marshals to the ui thread itself and keeps the last 2000 lines
            if (true == logArea.IsReady)
                logArea.AppendLine(line, 2_000);
        }

        static void OnClearLog(object s, EventArgs e)
        {
            lock (fullLog)
                fullLog.Clear();

            logArea.Text = string.Empty;
        }

        // Asks where to save, then writes every line since the last Clear Log, up to the moment of the click.
        static void OnSaveLog(object s, EventArgs e)
        {
            using (System.Windows.Forms.SaveFileDialog dialog = new System.Windows.Forms.SaveFileDialog
            {
                Title = "Save the log",
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"{System.Diagnostics.Process.GetCurrentProcess().ProcessName}_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            })
            {
                if (System.Windows.Forms.DialogResult.OK == dialog.ShowDialog())
                {
                    string[] lines;

                    lock (fullLog)
                        lines = fullLog.ToArray();

                    System.IO.File.WriteAllLines(dialog.FileName, lines);

                    SetStatus($"Saved [{lines.Length}] log lines to [{dialog.FileName}]");
                }
            }
        }

        static void SetStatus(string text)
        {
            statusBar.Text = text;
        }
        /// <summary>
        /// The yaml with everything that changes on every run replaced, so two runs can be compared.
        /// Timestamps and guids are generated by Initialize and are never the same twice.
        /// </summary>
        /// <summary>
        /// The anchor names the document defines and the ones its aliases refer to.
        /// </summary>
        /// <remarks>
        /// An anchor or alias is always last on its line, because HumanReadableAliasEventEmitter names it at a mapping
        /// or sequence start with the block underneath. Matching only there, not any ampersand, keeps a value that
        /// contains one out of the count.
        /// </remarks>
        /// <param name="yaml">The written document</param>
        /// <param name="anchors">Answers every name defined with an ampersand, in the order written</param>
        /// <param name="aliases">Answers every name referred to with an asterisk, in the order written</param>
        static void AnchorsAndAliases(string yaml, out List<string> anchors, out List<string> aliases)
        {
            anchors = new List<string>();
            aliases = new List<string>();

            foreach (string line in yaml.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None))
            {
                System.Text.RegularExpressions.Match token =
                    System.Text.RegularExpressions.Regex.Match(line, AnchorOrAliasPattern);

                if (false == token.Success)
                    continue;

                if ("&" == token.Groups[1].Value)
                    anchors.Add(token.Groups[2].Value);
                else
                    aliases.Add(token.Groups[2].Value);
            }
        }

        /// <summary>
        /// Whether this member's lines define or refer to an anchor.
        /// </summary>
        /// <param name="memberText">The lines written under one top level member</param>
        /// <returns>True when the member carries a reference of either kind</returns>
        static bool HasAnchorOrAlias(string memberText)
        {
            AnchorsAndAliases(memberText, out List<string> anchors, out List<string> aliases);

            return 0 < anchors.Count || 0 < aliases.Count;
        }

        /// <summary>
        /// A line whose whole remainder is an anchor or an alias: optionally a sequence dash, optionally a key,
        /// then the sigil and the name and nothing else.
        /// </summary>
        const string AnchorOrAliasPattern = @"^\s*(?:-\s+)?(?:[\w.]+:)?\s*([&*])([A-Za-z_]\w*)\s*$";

        /// <summary>
        /// Where this run writes its output: a folder beside the exe, created if missing.
        /// </summary>
        /// <remarks>
        /// Output stays with the build that made it, so two builds cannot read each other's leftovers and a clean
        /// build has nothing to mistake for a result.
        /// </remarks>
        /// <returns>The full path of the output directory</returns>
        // The workbook the Excel round trip reads, written here with GPAL's Excel API so the test carries nobody's
        // data. Two sheets, because reading only the first is the mistake worth catching.
        static string MakeWorkbook()
        {
            GPALFile made = System.IO.Path.Combine(TestOutputDirectory(), "inWorkbook.xlsx");

            IGPALGrid<string> records = GPAL.GridForType<string>();

            records.AddRow(new List<string> { "Name", "Tally", "WhenAt" });

            for (int row = 1; row <= 9; row++)
                records.AddRow(new List<string> { $"row {row}", (row * 3).ToString(), new DateTime(2026, 1, row).ToString("yyyy-MM-dd") });

            IGPALGrid<string> totals = GPAL.GridForType<string>();

            totals.AddRow(new List<string> { "What", "HowMany" });
            totals.AddRow(new List<string> { "rows", "9" });
            totals.AddRow(new List<string> { "sheets", "2" });

            GPAL.Excel.ToGPALObject()
                .WithNewFile(made)
                .WithSheet(1)
                .InputFrom(records)
                .WithNewSheet("Totals")
                .InputFrom(totals)
                .SaveTo(made)
                .Close(true);

            return made.Last;
        }

        // Whether two parsed JSON documents hold the same data: the same keys and values in objects, the same items in
        // the same order in lists. Values are compared as text, so 3 and "3" read the same; the converter writes what
        // it read, so a type change shows up elsewhere.
        static bool SameData(object a, object b)
        {
            bool retVal;

            if (a is IDictionary<string, object> aObject && b is IDictionary<string, object> bObject)
                retVal = aObject.Count == bObject.Count && aObject.All(pair => bObject.ContainsKey(pair.Key) && SameData(pair.Value, bObject[pair.Key]));
            else if (a is System.Collections.IList aList && b is System.Collections.IList bList)
                retVal = aList.Count == bList.Count && Enumerable.Range(0, aList.Count).All(i => SameData(aList[i], bList[i]));
            else
                retVal = a?.ToString() == b?.ToString();

            return retVal;
        }

        // every value in a parsed JSON document, nulls left out, so an output can be searched for each one
        static void LeafValues(object node, List<string> into)
        {
            if (node is IDictionary<string, object> asObject)
                foreach (object child in asObject.Values)
                    LeafValues(child, into);
            else if (node is System.Collections.IList asList)
                foreach (object child in asList)
                    LeafValues(child, into);
            else if (null != node)
                into.Add(node.ToString());
        }

        // the files this test reads, shipped with the project and copied next to the exe, so a run needs
        // nothing on the machine it runs on
        static string TestData(string name)
        {
            return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", name);
        }

        // everything it writes, beside the exe and disposable
        static string TestOutput(string name)
        {
            return System.IO.Path.Combine(TestOutputDirectory(), name);
        }

        static string TestOutputDirectory()
        {
            string retVal = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestOutput");

            if (false == System.IO.Directory.Exists(retVal))
                System.IO.Directory.CreateDirectory(retVal);

            return retVal;
        }

        static string[] Mask(string yaml)
        {
            string masked = System.Text.RegularExpressions.Regex.Replace(yaml,
                @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})?", "<timestamp>");

            masked = System.Text.RegularExpressions.Regex.Replace(masked,
                @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", "<guid>");

            return masked.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None);
        }

        /// <summary>
        /// The top level members with nothing masked, for the ones that have to match to the character.
        /// </summary>
        static Dictionary<string, string> TopLevelMembersRaw(string yaml)
        {
            Dictionary<string, string> retVal = new Dictionary<string, string>();
            string name = null;
            List<string> body = new List<string>();

            foreach (string line in yaml.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None))
            {
                System.Text.RegularExpressions.Match member =
                    System.Text.RegularExpressions.Regex.Match(line, @"^([A-Za-z_]\w*):(.*)$");

                if (true == member.Success)
                {
                    if (null != name)
                        retVal[name] = string.Join(Environment.NewLine, body);

                    name = member.Groups[1].Value;
                    body = new List<string> { member.Groups[2].Value };
                }
                else if (null != name)
                    body.Add(line);
            }

            if (null != name)
                retVal[name] = string.Join(Environment.NewLine, body);

            return retVal;
        }

        /// <summary>
        /// The yaml split into its top level members, each one the lines under a name written at column zero.
        /// Comparing member by member says which one failed rather than which line, and the run-to-run values
        /// are masked first so a timestamp is not read as a difference.
        /// </summary>
        static Dictionary<string, string> TopLevelMembers(string yaml)
        {
            Dictionary<string, string> retVal = new Dictionary<string, string>();
            string name = null;
            List<string> body = new List<string>();

            foreach (string line in Mask(yaml))
            {
                System.Text.RegularExpressions.Match member =
                    System.Text.RegularExpressions.Regex.Match(line, @"^([A-Za-z_]\w*):(.*)$");

                if (true == member.Success)
                {
                    if (null != name)
                        retVal[name] = string.Join(Environment.NewLine, body);

                    name = member.Groups[1].Value;
                    body = new List<string> { member.Groups[2].Value };
                }
                else if (null != name)
                    body.Add(line);
            }

            if (null != name)
                retVal[name] = string.Join(Environment.NewLine, body);

            return retVal;
        }

        static void NPIMatch(IEnumerable<IGrouping<NPIGroup, NPIData>> NPIGroups, List<RevalidationData> revalidations, Dictionary<Client, List<MatchedData>> matchList)
        {
            foreach (var item in NPIGroups
                .Where(x => x.Key.NPI != "")
                .Join(revalidations,
                np => np.Key.NPI,
                revalNPI => revalNPI.NPI,
                (np, revalNPI) => new MatchedData(np.Key.ClientID, np.Key.EntityName, np.Key.NPI, revalNPI.OrganizationName, revalNPI.EnrollmentState, revalNPI.EnrollmentSpecialty, revalNPI.GetAdjustedDate()))
                .Where(md => !string.IsNullOrWhiteSpace(md.RevalidationDate)))
            {
                KeyValuePair<Client, List<MatchedData>> clientData = matchList.FirstOrDefault(x => x.Key.ClientID == item.ClientID);
                if (clientData.Key == null)
                    clientData = matchList.First(x => x.Key.ClientID == string.Empty);

                clientData.Value.Add(item);
            }
        }
    }
}

