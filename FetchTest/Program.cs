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

using System.Collections.Generic;
using System.Diagnostics;
using System;
// GP usings, since we are creating a browser workflow, we include GenerallyPositive.Browser
using GenerallyPositive;
using GenerallyPositive.Browser;
using GenerallyPositive.GPALForm;
using static GenerallyPositive.Enums;

namespace FetchTest
{
    /// <summary>
    /// Calls a site's own API with .Fetch instead of scraping its DOM.<br/>
    /// The browser loads the site first, earning the session the ordinary way, cookies and anti-bot clearance
    /// included, then .Fetch sends the request from inside that page. So it carries the browser's TLS fingerprint,
    /// HTTP/2 framing and header order, which a plain HTTP client cannot copy whatever cookies it holds; that is what
    /// fails at the handshake on an Akamai or PerimeterX protected site.<br/>
    /// <b>NOTE:</b> the site proxies its search API through its own domain, so the path is relative to whatever
    /// origin .GoTo left.<br/>
    /// The request is tokenized: .WithTokensFrom supplies a row per search and the numbered tokens in the body take
    /// their values from its columns.<br/>
    /// Runs from a form so it can use a hidden desktop and Peek. The Log tab shows every event GPAL publishes.
    /// </summary>
    public class Program
    {
        // the browser the current pass is using
        static IBrowser browser { get; set; }

        static bool running { get; set; }

        // Set by Stop. No pass starts after it, and the running pass is ended by closing its browser.
        static bool stopRequested { get; set; }

        // The search request, built once and reused like a Selector. Harrods proxies Algolia through its own domain, so
        // the path is same-origin and needs only the session earned on the homepage. Algolia sends a JSON body as
        // form-urlencoded so the request stays simple and never triggers a CORS preflight.
        static GPALRequest searchRequest = (GPALRequest)GPAL.Request
            .WithPath("/api/search/1/indexes/prod_harrods.com_query_suggestions/query")
            .WithHttpMethod(HttpVerb.Post)
            .WithParameter("x-algolia-agent=Algolia for JavaScript (4.26.0); Browser (lite)")
            .WithContentType(ContentType.Form)
            // Tokens are numbered to match the .WithTokensFrom columns: {0} the search term, {1} the page size. GPAL
            // does substring replacement, not parsing, so where markers go and what the payload is are the workflow's
            // business.
            .WithBody(@"{""query"":""{0}"",""hitsPerPage"":{1},""page"":{page},""facets"":[""*""]}")
            .WithFirstPage(0)           // algolia counts pages from zero
            .WithPageToken("{page}")       // GPAL fills {page}; the row fills the rest
            .CallAfterFetch(ReportSearch)
            .WithName("harrods suggestions");

        // one row per search, one column per token. the same shape a csv file or a database query would arrive in
        static IGPALGrid<string> searchTerms = GPAL.GridForType<string>();

        // ── Run tab ───────────────────────────────────────────────────────────

        static GPALTab runTab = GPAL.Tab.WithName("runTab").WithText("Run").ToGPALObject();

        static GPALTextArea aboutBlurb = (GPALTextArea)GPAL.TextArea
            .WithReadOnly(true)
            .WithHeight(130)
            .WithName("aboutBlurb")
            .WithText("Calls the Harrods search API with Fetch from inside the page, on the browsers and engines checked below. Each browser uses its signed-in profile, or a temporary one if the Profiles tab says so. The browser warms up on the Harrods homepage, then fetches three pages of search suggestions for each of three terms and saves the json beside the program as fetch.results.<browser>.<engine>.<mode>.json. PuppeteerPort and PuppeteerPipe have no Firefox, so those pairs are skipped." + Environment.NewLine + Environment.NewLine +
                      "Every pass runs in a window. Headless adds a second, headless round of the same passes. On a hidden desktop the browsers never appear; Peek shows the running one for a minute, or until Escape. The Log tab shows everything GPAL publishes, with run times at the end.")
            .ToGPALObject();

        static GPALCheckbox hiddenDesktopCheck = GPAL.CheckboxFor("Run on a hidden desktop", true);

        // GPAL's own tracing
        static GPALCheckbox debugCheck = GPAL.CheckboxFor("Include DEBUG in the log", true);

        // adds a headless round after the windowed one, it never replaces it
        static GPALCheckbox headlessCheck = GPAL.CheckboxFor("Add headless", false);

        static GPALCheckbox chromeCheck = GPAL.CheckboxFor("Chrome", true);
        static GPALCheckbox edgeCheck = GPAL.CheckboxFor("Edge", true);
        static GPALCheckbox firefoxCheck = GPAL.CheckboxFor("Firefox", true);

        static GPALCheckbox seleniumCheck = GPAL.CheckboxFor("Selenium", true);
        static GPALCheckbox puppeteerCheck = GPAL.CheckboxFor("PuppeteerPort", true);
        static GPALCheckbox puppeteerPipeCheck = GPAL.CheckboxFor("PuppeteerPipe", true);
        static GPALCheckbox ottoMagicCheck = GPAL.CheckboxFor("OttoMagic", false);

        // runs SeleniumHW, PuppeteerPortHW, PuppeteerPipeHW and OttoMagicHW in place of the four above
        static GPALCheckbox hardwareCheck = GPAL.CheckboxFor("Use the hardware variants", false);

        // Three groups side by side: run options, browsers, engines. A splitter holds two, so browsers and engines
        // share a second splitter right of the first group. Declared after the checkboxes because static fields
        // initialize in order.
        static GPALSplitter browsersAndEngines = (GPALSplitter)GPAL.Splitter.HSplit(0.5)
            .WithFormControl(chromeCheck)
            .WithFormControl(edgeCheck)
            .WithFormControl(firefoxCheck)
            .WithFormControl(GPAL.SplitRight)
            .WithFormControl(seleniumCheck)
            .WithFormControl(puppeteerCheck)
            .WithFormControl(puppeteerPipeCheck)
            .WithFormControl(ottoMagicCheck)
            .WithFormControl(hardwareCheck)
            .WithHeight(145)
            .ToGPALObject();

        static GPALSplitter runOptions = (GPALSplitter)GPAL.Splitter.HSplit(0.34)
            .WithFormControl(hiddenDesktopCheck)
            .WithFormControl(debugCheck)
            .WithFormControl(headlessCheck)
            .WithFormControl(GPAL.SplitRight)
            .WithFormControl(browsersAndEngines)
            .WithHeight(145)
            .ToGPALObject();

        static GPALButton runBtn = GPAL.Button
            .WithName("run")
            .WithText("▶ Run every browser and engine")
            .WithCallback<EventHandler>((s, e) => runBtn.Enabled = false)
            .WithCallbackOffUIThread<EventHandler>(OnRun)
            .ToGPALObject();

        static GPALButton stopBtn = GPAL.Button
            .WithEnabled(false)
            .WithName("stop")
            .WithText("⏹ Stop")
            .WithCallback<EventHandler>((s, e) => stopBtn.Enabled = false)
            .WithCallbackOffUIThread<EventHandler>(OnStop)
            .ToGPALObject();

        // shows the hidden desktop for a minute, or until Escape or its Return button. Enabled once a pass has
        // made a hidden desktop.
        static GPALButton peekBtn = GPAL.Button
            .WithEnabled(false)
            .WithName("peek")
            .WithText("👁 Peek")
            .WithCallback<EventHandler>(OnPeek)
            .ToGPALObject();

        static GPALButton exitBtn = GPAL.Button
            .WithName("exit")
            .WithText("🚪 Exit")
            .WithCallback<EventHandler>((s, e) => System.Windows.Forms.Application.Exit())
            .ToGPALObject();

        // ── Profiles tab ──────────────────────────────────────────────────────

        static GPALTab profilesTab = GPAL.Tab.WithName("profilesTab").WithText("Profiles").ToGPALObject();

        // Use profiles off, or a blank folder, runs that browser on a temporary profile GPAL creates and deletes.
        static GPALCheckbox useProfilesCheck = GPAL.CheckboxFor("Use profiles", true);

        static GPALLabel profilesHint = GPAL.LabelFor("A browser with a blank folder, or every browser when Use profiles is off, runs on a temporary profile.");

        static GPALFileSelector chromeProfile = (GPALFileSelector)GPAL.FileSelector
            .WithSelectFolder(true)
            .WithCompact(true)
            .WithTitle("Chrome profile folder")
            .WithPlaceholder("Chrome profile folder")
            .WithSelectedPath(@"C:\profiles\chrome\signedin")
            .WithName("chromeProfile")
            .ToGPALObject();

        static GPALFileSelector edgeProfile = (GPALFileSelector)GPAL.FileSelector
            .WithSelectFolder(true)
            .WithCompact(true)
            .WithTitle("Edge profile folder")
            .WithPlaceholder("Edge profile folder")
            .WithSelectedPath(@"C:\profiles\edge\signedin")
            .WithName("edgeProfile")
            .ToGPALObject();

        static GPALFileSelector firefoxProfile = (GPALFileSelector)GPAL.FileSelector
            .WithSelectFolder(true)
            .WithCompact(true)
            .WithTitle("Firefox profile folder")
            .WithPlaceholder("Firefox profile folder")
            .WithSelectedPath(@"C:\profiles\firefox\signedin")
            .WithName("firefoxProfile")
            .ToGPALObject();

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

        /// <summary>
        /// Called once per search term with all that term's pages, while the rest are still being fetched. The bodies
        /// are arbitrary JSON, so nothing assumes a shape.
        /// </summary>
        static CallIfStatus ReportSearch(IBrowser browser, List<string> results, IGPALGrid<string> tokens, int tokenIdx)
        {
            foreach (string body in results)
                GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"[{tokens[tokenIdx][0]}] at [{tokens[tokenIdx][1]}] per page returned [{body.Length}] characters.");

            return CallIfStatus.Handled;
        }

        [STAThread]
        static void Main()
        {
            // Hook into the GPAL Information and Exception channel messages. The Log tab shows them.
            GPAL
                .WithPublishToDebug()
                .WithPublishStackTrace()
                //.WithAutoUpdateWebDriver()
                .WithDriverLocation(System.AppDomain.CurrentDomain.BaseDirectory);

            // Leave out DEBUG and DEEPDEBUG; they are GPAL's internal tracing, not for a person to read.
            GPAL.WithHandlerEvents(GPALEventType.All & ~(GPALEventType.DEBUG | GPALEventType.DEEPDEBUG))
                .WithExceptionHandler(OnGPALEvent)
                .WithInformationHandler(OnGPALEvent);

            // Search term, hits per page: three searches, each walked for the pages set below.
            searchTerms.AddRow(new List<string> { "mens shorts", "5" });
            searchTerms.AddRow(new List<string> { "womens boots", "3" });
            searchTerms.AddRow(new List<string> { "handbags", "10" });

            var form = GPAL.Form
                .WithTitle("FetchTest  |  every browser and engine")
                .WithWidth(760)
                .WithHeight(640)
                .WithFormControl(statusBar)

                .WithFormControl(runTab)
                .WithFormControl(aboutBlurb)
                .WithFormControl(runOptions)
                .WithFormControl(GPAL.LabelFor("OttoMagic needs its browser extension and GPALRestAPI installed, both from:"))
                .WithFormControl(GPAL.LinkLabelFor("https://gpal.sditx.com/Ottomagic"))
                .WithFormControl(GPAL.SpacerFor(18))
                .WithFormControl(runBtn)
                .WithFormControl(stopBtn)
                .WithFormControl(peekBtn)
                .WithFormControl(GPAL.SpacerFor(18))
                .WithFormControl(exitBtn)

                .WithFormControl(profilesTab)
                .WithFormControl(useProfilesCheck)
                .WithFormControl(profilesHint)
                .WithFormControl(GPAL.SpacerFor(12))
                .WithFormControl(GPAL.LabelFor("Chrome"))
                .WithFormControl(chromeProfile)
                .WithFormControl(GPAL.LabelFor("Edge"))
                .WithFormControl(edgeProfile)
                .WithFormControl(GPAL.LabelFor("Firefox"))
                .WithFormControl(firefoxProfile)

                .WithFormControl(logTab)
                .WithFormControl(clearLogBtn)
                .WithFormControl(saveLogBtn)
                .WithFormControl(logArea)

                .ToGPALObject();

            form.ShowDialog();

            browser?.Close(true);
        }

        static void OnRun(object s, EventArgs e)
        {
            if (true == running)
                return;

            running = true;
            stopRequested = false;
            runBtn.Enabled = false;
            stopBtn.Enabled = true;

            bool hidden = hiddenDesktopCheck.Checked;
            bool headless = headlessCheck.Checked;

            // Read the controls once, at the start of the run.
            List<BrowserType> browsers = new List<BrowserType>();
            if (true == chromeCheck.Checked) browsers.Add(BrowserType.Chrome);
            if (true == edgeCheck.Checked) browsers.Add(BrowserType.Edge);
            if (true == firefoxCheck.Checked) browsers.Add(BrowserType.FireFox);

            bool hardware = hardwareCheck.Checked;
            List<AutomationEngine> engines = new List<AutomationEngine>();
            if (true == seleniumCheck.Checked) engines.Add(true == hardware ? AutomationEngine.SeleniumHW : AutomationEngine.Selenium);
            if (true == puppeteerCheck.Checked) engines.Add(true == hardware ? AutomationEngine.PuppeteerPortHW : AutomationEngine.PuppeteerPort);
            if (true == puppeteerPipeCheck.Checked) engines.Add(true == hardware ? AutomationEngine.PuppeteerPipeHW : AutomationEngine.PuppeteerPipe);
            if (true == ottoMagicCheck.Checked) engines.Add(true == hardware ? AutomationEngine.OttoMagicHW : AutomationEngine.OttoMagic);

            // each browser's profile folder, or null for a temporary profile
            bool useProfiles = useProfilesCheck.Checked;
            Dictionary<BrowserType, string> profiles = new Dictionary<BrowserType, string>
            {
                { BrowserType.Chrome, true == useProfiles && false == string.IsNullOrWhiteSpace(chromeProfile.SelectedPath) ? chromeProfile.SelectedPath.Trim() : null },
                { BrowserType.Edge, true == useProfiles && false == string.IsNullOrWhiteSpace(edgeProfile.SelectedPath) ? edgeProfile.SelectedPath.Trim() : null },
                { BrowserType.FireFox, true == useProfiles && false == string.IsNullOrWhiteSpace(firefoxProfile.SelectedPath) ? firefoxProfile.SelectedPath.Trim() : null },
            };

            // DEEPDEBUG stays out either way; raw protocol traffic buries everything else.
            GPAL.WithHandlerEvents(true == debugCheck.Checked
                ? GPALEventType.All & ~GPALEventType.DEEPDEBUG
                : GPALEventType.All & ~(GPALEventType.DEBUG | GPALEventType.DEEPDEBUG));

            try
            {
                RunEverything(hidden, headless, browsers, engines, profiles);
            }
            catch (Exception ex)
            {
                // Each pass catches its own exceptions, so this only catches the run itself failing, and keeps the
                // form usable.
                GPAL.PublishSimpleEvent(GPALEventType.EXCEPTION, "The run ended in an exception", ex: ex);
            }
            finally
            {
                browser = null;
                running = false;
                stopBtn.Enabled = false;
                runBtn.Enabled = true;

                // Each browser's hidden desktop is released when it closes, so none is left to peek at.
                peekBtn.Enabled = false;
                SetStatus(true == stopRequested
                    ? "Stopped. The run times for the passes that ran are at the end of the Log tab."
                    : "Done. The run times are at the end of the Log tab.");
            }
        }

        // No pass starts after this. The running pass is ended by closing its browser: its next step fails, the pass
        // catches it, and the run stops. Closed off the UI thread because closing can take seconds.
        static void OnStop(object s, EventArgs e)
        {
            stopRequested = true;
            stopBtn.Enabled = false;

            SetStatus("Stopping: closing the browser that is running now...");
            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, "Stop pressed, closing the running browser and starting no more passes");

            IBrowser closing = browser;

            try
            {
                closing?.Close(true);
            }
            catch (Exception ex)
            {
                GPAL.PublishSimpleEvent(GPALEventType.EXCEPTION, "Stop could not close the running browser. It closes when the pass ends.", ex: ex);
            }
        }

        static void RunEverything(bool hidden, bool headless, List<BrowserType> browsers, List<AutomationEngine> engines, Dictionary<BrowserType, string> profiles)
        {
            // Ends any browser an earlier run left holding a profile. A temporary profile is new each pass, so
            // nothing holds it.
            foreach (string profile in profiles.Values)
                if (null != profile)
                    EndWhatIsLeftOn(profile, "an earlier run");

            // the windowed round always runs. headless adds a second round after it
            bool[] rounds = true == headless ? new[] { false, true } : new[] { false };

            foreach (bool asHeadless in rounds)
            {
                if (true == stopRequested)
                    break;

                RunRound(hidden, asHeadless, browsers, engines, profiles);
            }
        }

        // one round: every checked browser with every checked engine, windowed or headless
        static void RunRound(bool hidden, bool asHeadless, List<BrowserType> browsers, List<AutomationEngine> engines, Dictionary<BrowserType, string> profiles)
        {
            string mode = true == asHeadless ? "headless" : "windowed";

            // wall-clock runtime per (browser, engine), for the end-of-run table
            var runtimes = new Dictionary<(BrowserType bt, AutomationEngine ae), TimeSpan>();

            foreach (BrowserType bt in browsers)
            {
                if (true == stopRequested)
                    break;

                foreach (AutomationEngine ae in engines)
                {
                    if (true == stopRequested)
                        break;

                    // puppeteer does not drive firefox
                    if (BrowserType.FireFox == bt && (AutomationEngine.PuppeteerPort == ae || AutomationEngine.PuppeteerPortHW == ae ||
                        AutomationEngine.PuppeteerPipe == ae || AutomationEngine.PuppeteerPipeHW == ae))
                        continue;

                    string json = null;

                    // one results file per browser, engine and mode, so a round does not overwrite the one before it
                    GPALFile saveFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"fetch.results.{bt}.{ae}.{mode}.json");

                    // Null means a temporary profile, created for the pass and deleted when the browser closes.
                    string profile = profiles[bt];
                    string onProfile = null == profile ? "a temporary profile" : $"profile [{profile}]";

                    SetStatus($"Running [{bt}] [{ae}] {mode}{(true == hidden ? " on a hidden desktop" : string.Empty)}...");
                    GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"Starting [{bt}] [{ae}] [{mode}] on {onProfile}");

                    Stopwatch runStopwatch = Stopwatch.StartNew();

                    // A browser and engine pair that throws is its own result; the rest still run.
                    try
                    {
                        // Stored before navigating, so a pass that fails while the browser starts or loads still has a
                        // browser to close.
                        browser = GPAL.Browser
                            .WithBrowserType(bt)
                            .WithUseStealth(StealthType.PatchDriver)    // otherwise selenium harrods blocks goto with 'not authorized' BUT the fetches still work.
                            .WithAutomationEngine(ae)
                            .WithHiddenDesktop(hidden)
                            .WithProfileDataDirectory(profile)
                            .WithWaitOnDocumentReady(30_000)     // the session is only earned once the page settles
                            .ToGPALObject();

                        // A hidden desktop exists from here on, so Peek has something to show.
                        if (true == hidden)
                            peekBtn.Enabled = true;

                        // Warm up on the homepage. Going straight at an inner path or the API gets blocked; the
                        // anti-bot cookies are only set by a real navigation. Get opens it headless, GoTo in a window.
                        if (true == asHeadless)
                            browser.Get("https://www.harrods.com/");
                        else
                            browser.GoTo("https://www.harrods.com/");

                        browser
                            .WithTokensFrom(searchTerms)    // one row per search, filling {0} and {1}
                            .WithPages(3)                   // three calls per row, {page} becoming 0, 1, 2
                            .Fetch(searchRequest)           // sent by the page, so it carries the session just earned
                            .SaveTo(ref json)               // one json array, every row's pages, in the order requested
                            ;

                        browser
                            .Close(true);

                        // The response is arbitrary JSON, so nothing assumes a shape. Hand it to the converter when it
                        // suits a grid, a file or a database; .WithInput detects the format.
                        GPAL.Converter
                            .WithInput(json)
                            .SaveTo(saveFile.Next);
                    }
                    catch (Exception ex)
                    {
                        GPAL.PublishSimpleEvent(GPALEventType.EXCEPTION, $"[{bt}] [{ae}] [{mode}] ended in an exception, going on to the next", ex: ex);

                        // Close this pass's browser so the next pass does not start beside it.
                        try { browser?.Close(true); } catch (Exception) { }
                    }
                    finally
                    {
                        browser = null;

                        runStopwatch.Stop();
                        runtimes[(bt, ae)] = runStopwatch.Elapsed;

                        if (null != profile)
                            EndWhatIsLeftOn(profile, $"[{bt}] [{ae}] [{mode}]");
                    }
                }
            }

            GPAL.EmitRuntimeTable($"{(true == hidden ? "Hidden desktop" : "Window")}, {mode}", runtimes);
        }

        // Browsers still running on the profile after a pass closed its browser. One left there locks the profile for
        // every later pass, so it is logged with the pass that leaked it and killed. Only the command line shows the
        // profile, so WMI is queried.
        static void EndWhatIsLeftOn(string profile, string who)
        {
            // Give a browser already shutting down a moment to release its profile lock.
            System.Threading.Thread.Sleep(2_000);

            List<int> leftBehind = new List<int>();

            // Runs from each pass's finally, so a WMI that does not answer is logged and the run goes on.
            try
            {
                using (var search = new System.Management.ManagementObjectSearcher(
                    "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'chrome.exe' OR Name = 'msedge.exe' OR Name = 'firefox.exe'"))
                    foreach (System.Management.ManagementObject found in search.Get())
                    {
                        string commandLine = found["CommandLine"]?.ToString() ?? string.Empty;

                        // Main process only; its children exit with it. GPAL writes the profile with forward slashes on
                        // Chromium's command line, so slashes are normalized before comparing.
                        bool onProfile = commandLine.Replace('/', '\\').IndexOf(profile.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) >= 0;

                        if (true == onProfile && false == commandLine.Contains("--type="))
                            leftBehind.Add(Convert.ToInt32(found["ProcessId"]));
                    }

                if (0 < leftBehind.Count)
                {
                    GPAL.PublishSimpleEvent(GPALEventType.CAUTION, $"[{who}] left [{leftBehind.Count}] browser(s) running on [{profile}], process [{string.Join(", ", leftBehind)}]. Ending them so the next pass can use the profile.");

                    foreach (int pid in leftBehind)
                        using (Process taskkill = Process.Start(new ProcessStartInfo("taskkill", $"/PID {pid} /T /F") { CreateNoWindow = true, UseShellExecute = false }))
                            taskkill?.WaitForExit(5_000);
                }
            }
            catch (Exception ex)
            {
                GPAL.PublishSimpleEvent(GPALEventType.EXCEPTION, $"Could not check what {who} left running on [{profile}]", ex: ex);
            }
        }

        // puts the hidden desktop on the screen for a minute, or until Escape or the Return button on it
        static void OnPeek(object s, EventArgs e)
        {
            string failed = GPAL.HiddenDesktop.Peek(60_000);

            SetStatus(null == failed
                ? "A minute on the hidden desktop. Press Escape or click Return to come back sooner."
                : $"Cannot show the hidden desktop: {failed}");
        }

        static void OnGPALEvent(object sender, GPAL.GPALEventArgs e)
        {
            string tag = e.GPALEventType == GPALEventType.WARNING   ? "WARN"
                       : e.GPALEventType == GPALEventType.ERROR     ? "ERR "
                       : e.GPALEventType == GPALEventType.EXCEPTION ? "EXCP"
                       : e.GPALEventType == GPALEventType.FAILURE   ? "FAIL"
                       : e.GPALEventType == GPALEventType.NOTICE    ? "NOTE"
                       : e.GPALEventType == GPALEventType.DEBUG     ? "DBG "
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
    }
}
