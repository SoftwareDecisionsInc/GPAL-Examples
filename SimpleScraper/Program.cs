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
using System.Windows.Forms;
using System;
// GP usings, since we are creating a browser workflow, we include GenerallyPositive.Browser
using GenerallyPositive;
using GenerallyPositive.Browser;
using GenerallyPositive.GPALForm;
using static GenerallyPositive.Enums;

namespace SimpleScraper
{
    /// <summary>
    /// Simple program to enter search terms on ebay and retrieve the results (image link, item link, description and price) into a tab delimited file.<br/>
    /// <b>NOTE:</b> Using the .MatchText on the selector provides a simple filter to limit the results. Using two filters provides a way to filter on two selectors and combine the results.<br/>
    /// Runs from a form so it can run on a hidden desktop and be peeked at. The Log tab shows everything GPAL
    /// publishes.
    /// </summary>
    public class Program
    {
        static IGPALGrid<string> retGrid;

        // the browser the current pass is using, so Stop can close it
        static IBrowser browser { get; set; }

        static bool running { get; set; }

        // Set by Stop. No new pass starts, and Stop closes the running pass's browser to end it.
        static bool stopRequested { get; set; }

        // the selectors are built once in Main and used by every pass
        static Selector nextPageButton;
        static Selector resultCard;
        static Selector imageColumn;
        static Selector descriptionColumn;
        static Selector linkColumn;
        static Selector priceColumn;
        static Selector searchButton;
        static Selector inputSearchSelector;

        // ── Run tab ───────────────────────────────────────────────────────────

        static GPALTab runTab = GPAL.Tab.WithName("runTab").WithText("Run").ToGPALObject();

        static GPALTextArea aboutBlurb = (GPALTextArea)GPAL.TextArea
            .WithReadOnly(true)
            .WithHeight(130)
            .WithName("aboutBlurb")
            .WithText("Searches eBay for Dog Treats on each checked browser and engine, each on its own signed-in profile or, per the Profiles tab, a temporary one. Each pass reads the description, price, item link and image link of the first 10 results on each of three pages and saves them tab delimited beside the program as results.<browser>.<engine>.<mode>.txt. PuppeteerPort and PuppeteerPipe have no Firefox, so those pairs are skipped." + Environment.NewLine + Environment.NewLine +
                      "Every pass runs in a window. Headless adds a second, headless round. With Hidden desktop ticked the browsers never appear on screen; Peek shows the running one for a minute, or until Escape. The Log tab shows everything GPAL publishes, with run times at the end.")
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

        // Three groups side by side: run options, browsers, engines. A splitter holds two panels, so browsers and
        // engines share one, right of the run options. Declared after the checkboxes because static fields initialize
        // in order.
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

        // shows the hidden desktop for a minute, or until Escape or its Return button
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

        // Off, or a blank folder, runs that browser on a temporary profile GPAL creates and deletes.
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

        // Every event GPAL publishes. The status strip shows only the current step.
        static GPALTextArea logArea = (GPALTextArea)GPAL.TextArea.WithReadOnly(true).WithHeight(420).WithName("log");

        // Every log line since the last Clear Log, for Save Log. The Log box keeps only the last 2000, so a long run
        // loses its start there.
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
            // Hook into the GPAL Information and Exception channel messages. The Log tab shows them.
            // In your application you can examine the messages, take action on them or log them using your favorite logging solution.
            GPAL
                .WithPublishToDebug()
                .WithAutoUpdateWebDriver()
                //.WithExceptionHandler(ExceptionEventHandler)    // Get an event anytime an exception occurs
                //.WithInformationHandler(ExceptionEventHandler)  // Information event logging and handling
                .WithTypingDelay(50)                // 100 ms [default] - GPAL picks a random number up to this number as the intercharacter typing delay,
                .WithImageMatchingPercentage(85)    // 80 [default] - Tighten up the image matching percentage - now uses opencv
                .WithSimulateMouseMovement(true)            // false [default] - move the mouse like a human when running this workflow. With hardware GPAL will jump the mouse to each element, or not even move the mouse if using Selenium or javascript.
                .WithDriverLocation(System.AppDomain.CurrentDomain.BaseDirectory);

            // DEBUG and DEEPDEBUG are GPAL's internal tracing; leave them out.
            GPAL.WithHandlerEvents(GPALEventType.All & ~(GPALEventType.DEBUG | GPALEventType.DEEPDEBUG))
                .WithExceptionHandler(OnGPALEvent)
                .WithInformationHandler(OnGPALEvent);

            retGrid = GPAL.Grid.ToGPALObject();   // the data we retrieve from eBay we also save into a variable we can work with

            // In order to get multiple pages of data, GPAL must know which element is used to get the next page of results,
            // GPAL has a unique feature where you can define multiple selector paths and GPAL will search using them in the order you define,
            // If not elements are found with the first selector, the next one is attempted until results are found, or selector paths are exhausted,
            // Combine this with a Match requirement or Match function, you can examine elements yourself and determine which one's you want to use.
            nextPageButton = @"//a[contains(@class, 'pagination__next icon-link')]";
            nextPageButton.WithHardware.WithSelectorName("NextPageButton"); // our friendly name for Information and Exception channel message - if not supplied: selector1, selector2, etc are assigned as they are added to the workflow

            // One result card. Every real item li has an id like item1ef8a83f34 and ads do not, so this picks only
            // items. .InElement keeps every match, so the four selectors below are searched inside each card, and each
            // card gives one cell per column even when it lacks the item. That keeps a card with two prices from
            // shifting the rows below.
            resultCard = "//li[starts-with(@id, 'item')]";
            resultCard.WithSelectorName("ResultCard");

            // the image link, relative to the card
            imageColumn = ".//img[contains(@class, 's-card__image')]";
            imageColumn.WithSelectorName("Image");

            // the item description, relative to the card
            descriptionColumn = @".//span[contains(@class, 'su-styled-text primary default')]";
            descriptionColumn.WithSelectorName("Description");

            // the item page link, relative to the card
            linkColumn = ".//a[contains(@class, 's-card__link')]";
            linkColumn.WithSelectorName("Link");

            // the item price, relative to the card
            priceColumn = @".//span[contains(@class, 'su-styled-text primary bold large-1 s-card__price')]";
            priceColumn.WithSelectorName("Price");

            // the search button, we click this to initiate the search. we make sure the button text is "Search"
            // of course the match items are redundant, but show they work
            searchButton = "#gh-search-btn";
            searchButton.WithSelectorName("SearchButton").MatchText("Search");

            // the search term input element, we make sure it has a placehold of "Search for anything" in order to work with it
            inputSearchSelector = "#gh-ac"; //  GPAL.Selector.WithCSS("#gh-ac").WithXPath("//*[@id=\"gh-ac\"]").WithSelectorName("SearchInput").MatchPlaceholder("Search for anything").ToGPALObject();
            inputSearchSelector.WithSelectorName("SearchInput");

            var form = GPAL.Form
                .WithTitle("SimpleScraper  |  every browser and engine")
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

            // Read the form once, at the start of the run.
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

            // DEEPDEBUG is always left out; its raw protocol traffic buries everything else.
            GPAL.WithHandlerEvents(true == debugCheck.Checked
                ? GPALEventType.All & ~GPALEventType.DEEPDEBUG
                : GPALEventType.All & ~(GPALEventType.DEBUG | GPALEventType.DEEPDEBUG));

            try
            {
                RunEverything(hidden, headless, browsers, engines, profiles);
            }
            catch (Exception ex)
            {
                // Each pass catches its own exceptions, so this only sees the run loop failing. The form stays
                // usable.
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

        // No new pass starts. The running pass is ended by closing its browser; its next step fails, the pass catches
        // that, and the run stops. Closed off the UI thread because closing can take seconds.
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
                GPAL.PublishSimpleEvent(GPALEventType.EXCEPTION, "Could not close the running browser on Stop; it closes when the pass ends", ex: ex);
            }
        }

        static void RunEverything(bool hidden, bool headless, List<BrowserType> browsers, List<AutomationEngine> engines, Dictionary<BrowserType, string> profiles)
        {
            // Ends any browser an earlier run left holding a profile. Temporary profiles are new each pass and never
            // locked.
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

                    // One results file per browser, engine and mode, so a round does not overwrite the last.
                    GPALFile saveFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"results.{bt}.{ae}.{mode}.txt");

                    // Null is a temporary profile GPAL creates for the pass and deletes on close.
                    string profile = profiles[bt];
                    string onProfile = null == profile ? "a temporary profile" : $"profile [{profile}]";

                    SetStatus($"Running [{bt}] [{ae}] {mode}{(true == hidden ? " on a hidden desktop" : string.Empty)}...");
                    GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"Starting [{bt}] [{ae}] [{mode}] on {onProfile}");

                    Stopwatch runStopwatch = Stopwatch.StartNew();

                    // A browser and engine pair that throws is reported as its own failure; the rest still run.
                    try
                    {
                        // the beginning of our workflow
                        // open edge, load the driver from C:\driverrs, goto ebay, search for "Find me" and save 3 pages of desc, price, image link and item link into a tab delimited file beside the program
                        // Held before navigating, so a pass that fails while the browser starts or loads still has a
                        // browser to close.
                        browser = GPAL.Browser
                            .WithBrowserType(bt)
                            .WithUseStealth(StealthType.PatchDriver)
                            .WithCDPDebugPort(0xdead)
                            .WithOverwriteExistingFile(false)
                            .WithAutomationEngine(ae)
                            .WithWindowSize(new System.Drawing.Rectangle(0, 0, 1300, 1000))
                            .WithHiddenDesktop(hidden)
                            .WithProfileDataDirectory(profile)
                            //.WithWaitOnIdleConnection()
                            .WithWaitOnDocumentReady(30_000)
                            .ToGPALObject();

                        // a hidden desktop now exists, so Peek has something to show
                        if (true == hidden)
                            peekBtn.Enabled = true;

                        // Get opens it headless, GoTo in a window
                        if (true == asHeadless)
                            browser
                                .Get("https://www.ebay.com/");
                        else
                            browser
                                .GoTo("https://www.ebay.com/");

                        browser
                            .WithSelector(inputSearchSelector)
                            .WaitFor(10_000)    // eBay can report the page loaded before its header holds the search box
                            .FillInFrom("Dog Treats")   // this term has multiple page results
                            .LeftClick(searchButton)
                            .WaitFor(2_000);

                        browser
                            .InElement(resultCard)              // every card on the page becomes a root
                            ;
                        browser
                            .WithSelector(descriptionColumn)
                            .WithSelector(priceColumn)
                            .WithSelector(linkColumn)
                            .WithSelector(imageColumn)
                            .WaitFor(1_000)                         // we get an interstitial page 'checking device for access to ebay' so check, then fetch but will do for all, so slower
                            .WithAllThatMatch(10)                  // we want {number} matching results on each page, not just the first element.
                                                                   // values greater than a pages result set indicates TOTAL results (50 repeating elements with 60 will return 60 element)
                                                                   // 50 repeating elements with 5 is the top 5 on {num} pages
                                                                   // default is -1 (all)
                            //.CallIfFound(CallIfFound)           // if any one of the above selectors are found, call this method when they are found. we simply log info to the console
                            //.CallIfNotFound(CallIfNotFound)     // if any one of the above selectors is NOT found, call this method. we simply log info to the console

                            .WithNextPageButton(nextPageButton) // how to get the next page. use .WithInfiniteScroll if you have to go to page end to get more results
                            .WithPages(3)                       // get three pages of results
                            .GetGrid(ref retGrid)               // get the data into our grid (we don't actually do anything with it, but we could
                            .SaveTo(saveFile)   // save the results (extension .txt => tab-delimited)
                            .Close(true); // close the browser and 'true' kill all the running web drivers. web drivers continue to stay running, you should use true when the workflow is done.

                        GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"File saved to [{saveFile.ReturnFilenames[0]}].");
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

        // Finds browsers still running on the profile after a pass closed its browser. A leftover locks the profile for
        // later passes, so each is logged with the pass that leaked it, then ended. The profile shows only on the
        // command line, so WMI is queried.
        static void EndWhatIsLeftOn(string profile, string who)
        {
            // Wait first so a browser already shutting down can release the profile.
            System.Threading.Thread.Sleep(2_000);

            List<int> leftBehind = new List<int>();

            // Runs from each pass's finally, so a WMI failure is logged and the run continues.
            try
            {
                using (var search = new System.Management.ManagementObjectSearcher(
                    "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'chrome.exe' OR Name = 'msedge.exe' OR Name = 'firefox.exe'"))
                    foreach (System.Management.ManagementObject found in search.Get())
                    {
                        string commandLine = found["CommandLine"]?.ToString() ?? string.Empty;

                        // Main process only; its children exit with it. GPAL passes the profile to Chromium with
                        // forward slashes, so normalize slashes before comparing.
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
            // the innermost exception, since an AggregateException's own message names none of the errors it holds
            if (e.ExceptionRaised != null) line += $"  ({e.ExceptionRaised.GetBaseException().Message})";
            Debug.WriteLine(line);

            // Events arrive from both the run thread and the UI thread.
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

        // asks where to save, then writes every line since the last Clear Log. a run still going keeps adding, so
        // what is saved is everything up to the moment of the click
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
        /// Our handler for Information and Exception channel messages. We just log them to the console.<br/>
        /// Information channel messages will not have an ExceptionRaised.<br/>
        /// Information channel messages tell you when a default behavior doesn't work and GPAL does something to ensure the workflow continues.<br/>
        /// Exception messages are generally caught and handled, but sometimes they are fatal. Hear about all of them here.<br/>
        /// GPAL installs an unhandledd exception handler, so you will always get a message about unhandled exceptions.<br/>
        /// GPAL will terminate on unhandled exceptions.
        /// </summary>
        /// <param name="sender">A GPAL object we are doing something with: Selector, Browser, Application, UOW ...</param>
        /// <param name="e">(GPAL.GPALEventArgs) allows us to pass you objects to provide context,</param>
        public static void ExceptionEventHandler(object sender, EventArgs e)
        {
            Debug.WriteLine(((GPAL.GPALEventArgs)e).Message);
            Debug.WriteLine(((GPAL.GPALEventArgs)e).ExceptionRaised?.Message);
        }
        /// <summary>
        /// When a selector finds elements, if defined, this method will be called so you can create a new workflow (see other examples),<br/>
        /// Here we just print out information about the element.<br/>
        /// <b>NOTE:</b> You can define more than one callback and each one will be called in order defined depending upon the return value of the callback ahead of it.<br/>
        /// </summary>
        /// <param name="browser">GPAL Browser object used by this workflow</param>
        /// <param name="foundElements">ALL elements found by the selector</param>
        /// <param name="matchedElements">Only elements which matched the Match criteria</param>
        /// <param name="selector">The GPAL Selector used to find the elements</param>
        /// <returns>
        /// <b>Try the next handler in the chain based upon handler return value:</b><br/>
        /// 0 = not handled, bubble to next handler<br/>
        /// 1 = handled and continue with the program<br/>
        /// -1 = terminate program<br/>
        /// </returns>
        public static CallIfStatus CallIfFound(IBrowser browser, List<IGPALElement> foundElements, List<IGPALElement> matchedElements, Selector selector, bool matchedAll)
        {
            CallIfStatus retVal = CallIfStatus.NotHandled;

            foreach(IGPALElement webElement in foundElements)
            {
                string value = webElement.GetAttribute("value");
                Debug.WriteLine($"<{webElement.TagName}> : {webElement.Text} : {value}");
            }
            return retVal;
        }
        /// <summary>
        /// Called when the selector does not find any elements.<br/>
        /// foundElements and matchedElements will be null.
        /// </summary>
        /// <param name="browser">GPAL Browser object used by this workflow</param>
        /// <param name="foundElements">null</param>
        /// <param name="matchedElements">null</param>
        /// <param name="selector">The selector use to try to find the elements.</param>
        /// <returns>
        /// <b>Try the next handler in the chain based upon handler return value:</b><br/>
        /// 0 = not handled, bubble to next handler<br/>
        /// 1 = handled and continue with the program<br/>
        /// -1 = terminate program<br/>
        /// </returns>
        public static CallIfStatus CallIfNotFound(IBrowser browser, List<IGPALElement> foundElements, List<IGPALElement> matchedElements, Selector selector, bool matchedAll)
        {
            CallIfStatus retVal = CallIfStatus.NotHandled;

            MessageBox.Show($"Unable to find {selector.Name}");

            return retVal;
        }
    }
}

