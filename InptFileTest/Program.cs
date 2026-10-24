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

using GenerallyPositive;
using System.Collections.Generic;
using System.Diagnostics;
using static GenerallyPositive.Enums;
using GenerallyPositive.Browser;
using GenerallyPositive.GPALForm;
using System;

namespace InputFileTest
{
    /// <summary>
    ///  Simple program that enters text from ebay.words.txt into the search term input then clicks the search button and<br/>
    ///  retrieves the results: description, price, link to seller and link to image<br/>
    ///  then saves the results into a tab delimited file named after the search term used<br/>
    ///  Run from a form, so it can run on a hidden desktop and be peeked at while it does. Everything GPAL publishes
    ///  goes to the Log tab.
    /// </summary>
    public class Program
    {
        public static IGPALGrid<string> retGrid;
        public static int fileNameIdx = 0;

        // the browser, engine and mode of the current pass, added to the output file names so a round does not
        // overwrite the one before it
        static string runTag { get; set; }

        // the browser the current pass is using
        static IBrowser browser { get; set; }

        static bool running { get; set; }

        // Set by Stop. No pass starts after it, and the running pass is ended by closing its browser.
        static bool stopRequested { get; set; }

        // ── Run tab ───────────────────────────────────────────────────────────

        static GPALTab runTab = GPAL.Tab.WithName("runTab").WithText("Run").ToGPALObject();

        static GPALTextArea aboutBlurb = (GPALTextArea)GPAL.TextArea
            .WithReadOnly(true)
            .WithHeight(130)
            .WithName("aboutBlurb")
            .WithText("Fills the eBay search box from ebay.words.txt, one search term at a time, on the browsers and engines checked below, each browser on its own signed-in profile or, as set on the Profiles tab, a temporary one. After each term it clicks Search and reads the description, price, item link and image link from two pages of results. Each term's results are saved beside the program as <term>.<browser>.<engine>.<mode>.txt, and every term's rows together as ebay.all.searches.<browser>.<engine>.<mode>.txt. PuppeteerPort and PuppeteerPipe have no Firefox, so those pairs are skipped." + Environment.NewLine + Environment.NewLine +
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

        // shows the running browser's hidden desktop for a minute, or until Escape or its Return button
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

        [STAThread]
        static void Main()
        {
            // NOTE: it is very important to declare GPAL global options before instantiating any GPAL objects
            GPAL.WithTypingDelay(50)
                .WithDriverLocation(System.AppDomain.CurrentDomain.BaseDirectory);

            // Every exception and information event goes to the Log tab. DEBUG and DEEPDEBUG are GPAL's internal
            // tracing, not for a person to read.
            GPAL.WithHandlerEvents(GPALEventType.All & ~(GPALEventType.DEBUG | GPALEventType.DEEPDEBUG))
                .WithExceptionHandler(OnGPALEvent)
                .WithInformationHandler(OnGPALEvent);

            var form = GPAL.Form
                .WithTitle("InputFileTest  |  every browser and engine")
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

            System.Drawing.Rectangle windowSize = new System.Drawing.Rectangle(10, 10, 1500, 900);

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

                    // puppeteer drives chromium only
                    if (BrowserType.FireFox == bt && (AutomationEngine.PuppeteerPort == ae || AutomationEngine.PuppeteerPortHW == ae ||
                        AutomationEngine.PuppeteerPipe == ae || AutomationEngine.PuppeteerPipeHW == ae))
                        continue;

                    // null is a temporary profile, which GPAL makes for the pass and deletes when it closes
                    string profile = profiles[bt];
                    string onProfile = null == profile ? "a temporary profile" : $"profile [{profile}]";

                    SetStatus($"Running [{bt}] [{ae}] {mode}{(true == hidden ? " on a hidden desktop" : string.Empty)}...");
                    GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"Starting [{bt}] [{ae}] [{mode}] on {onProfile}");

                    Stopwatch runStopwatch = Stopwatch.StartNew();

                    // A browser and engine pair that throws is its own result; the rest still run.
                    try
                    {
                        // Stored before navigating, so a pass that fails while the browser starts still has a browser
                        // to close.
                        browser = GPAL.Browser
                            .WithBrowserType(bt)
                            .WithUseStealth(StealthType.GoogleReferrer | StealthType.PatchDriver | StealthType.DarkMode)
                            .WithAutomationEngine(ae)
                            .WithRespectRobotMetaTags(true)
                            .WithObeyRobotsTxt(true)
                            .WithWindowSize(windowSize)
                            .WithHiddenDesktop(hidden)
                            .WithProfileDataDirectory(profile)   // null means a temporary profile
                            .ToGPALObject();

                        // a hidden desktop exists from here on, so Peek has one to show
                        if (true == hidden)
                            peekBtn.Enabled = true;

                        RunInputFileTest(browser, asHeadless, $"{bt}.{ae}.{mode}");
                        browser.Close(true);
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

        static void RunInputFileTest(IBrowser browser, bool headless, string tag)
        {
            // CallAFterFillIn names its per-term file with this
            runTag = tag;

            GPALFile inputFile = "ebay.words.txt";
            GPALFile masterFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"ebay.all.searches.{tag}.txt");

            // GetGrid ADDS its results into the grid you pass by ref, it does not replace your reference. So by
            // reusing this one grid across every per-term GetGrid call (see CallAFterFillIn), we accumulate the
            // rows from all searches into a single combined dataset. We create it fresh here so each browser
            // run in the bt/ae loop starts with an empty master grid. (Pass null instead if you just want a new
            // grid back per call, or call .WithDedupeData(true) on it to skip duplicate rows.)
            retGrid = GPAL.Grid.ToGPALObject();

            Selector searchSelector = (Selector)GPAL.Selector
                .WithSelectorName("SearchInput")
                .WithCSS("#gh-ac")
                .WithXPath("//*[@id=\"gh-ac\"]")
                .MatchPlaceholder("Search for anything");

            // Get opens the page headless, GoTo in a window
            if (true == headless)
                browser
                    .Get("https://www.ebay.com/")
                    .WithSelector(searchSelector)
                    .WaitFor(10_000)    // eBay can report the page loaded before its header holds the search box
                    .CallAfterFillIn(CallAFterFillIn)
                    .FillInFrom(inputFile);   // multiple tokens
            else
                browser
                    .GoTo("https://www.ebay.com/")
                    .WithSelector(searchSelector)
                    .WaitFor(10_000)    // eBay can report the page loaded before its header holds the search box
                    .CallAfterFillIn(CallAFterFillIn)
                    .FillInFrom(inputFile);   // multiple tokens
                    //. Restore     // TODO: get restore working

            // after every search term has run, retGrid holds all of their rows combined - save the master grid
            retGrid.SaveTo(masterFile);
            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"Accumulated [{retGrid.Count()}] rows across all searches into [{masterFile.Filename}].");
        }

        public static CallIfStatus CallAFterFillIn(IBrowser myBrowser, IGPALGrid<string> tokens, int tokenIdx)
        {
            // name the file after the search term just entered, and the browser, engine and mode of the pass
            GPALFile fileList = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{tokens[tokenIdx][0]}.{runTag}.txt");

            Selector nextPageButton = @"//a[contains(@class, 'pagination__next icon-link')]";
            nextPageButton.WithSelectorName("NextPageButton"); // our friendly name for Information and Exception channel message - if not supplied: selector1, selector2, etc are assigned as they are added to the workflow

            // the image link
            Selector imageColumn = "//img[contains(@class, 's-card__image')]";
            imageColumn.WithSelectorName("Image");

            // the item description must match 'Find Me!' to be included in results we want
            Selector descriptionColumn = @"//span[contains(@class, 'su-styled-text primary default')]";
            descriptionColumn.WithSelectorName("Description");

            // the item page link
            Selector linkColumn = "//a[contains(@class, 's-card__link')]";
            linkColumn.WithSelectorName("Link");

            // the item price, we only want items priced 12.12 in our list
            Selector priceColumn = @"//span[contains(@class, 'su-styled-text primary bold large-1 s-card__price')]";
            priceColumn.WithSelectorName("Price");

            // the search button, we click this to initiate the search. we make sure the button text is "Search"
            // of course the match items are redundant, but show they work
            Selector searchButton = "#gh-search-btn";
            searchButton.WithSelectorName("SearchButton").MatchText("Search");

            myBrowser
                .WithSelector(searchButton)
                //.WithHardware
                //.WithJavaScript
                .LeftClick()

                .WithSelector(descriptionColumn)
                .WithSelector(priceColumn)
                .WithSelector(linkColumn)
                .WithSelector(imageColumn)
                .WithAllThatMatch()
                .WithNextPageButton(nextPageButton)
                //.WithHardware
                //.WithJavaScript
                .WithPages(2)
                // GetGrid appends THIS term's rows into the shared retGrid (passed by ref), so retGrid keeps
                // growing across every search term - it never replaces our reference. The SaveTo below still
                // writes only this term's results (from the workflow's own grid) to the per-term file; the
                // accumulated master grid is saved once in RunInputFileTest after all terms complete.
                .GetGrid(ref retGrid)

                //.WithHeader("Description")
                //.WithHeader("Price")
                //.WithHeader("Item Link")
                //.WithHeader("Image Link")
                .SaveTo(fileList);

            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"File saved to [{fileList.ReturnFilenames[0]}].");

            return CallIfStatus.Handled;
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

        // puts the running browser's hidden desktop on the screen for a minute, or until Escape or the Return button on it
        static void OnPeek(object s, EventArgs e)
        {
            string failed = GPAL.HiddenDesktop.Peek(60_000);

            SetStatus(null == failed
                ? "Showing the hidden desktop for a minute. Press Escape or click Return to come back sooner."
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
