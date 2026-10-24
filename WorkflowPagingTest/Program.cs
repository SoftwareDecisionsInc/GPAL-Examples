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
using GenerallyPositive;
using GenerallyPositive.Browser;
using GenerallyPositive.GPALForm;
using static GenerallyPositive.Enums;

namespace WorkflowPagingTest
{
    // Pages through Hacker News with client workflow loops, Until and While, and returns to stored urls with FromResult
    // by name and by index, through browser.OttoMagicClient on OttoMagic and browser.PuppeteerClient on Puppeteer. Runs
    // from a form so it can run on a hidden desktop and be peeked at. The Log tab shows everything GPAL publishes.
    internal class Program
    {
        // the browser the current pass is using, so Stop can close it
        static IBrowser browser { get; set; }

        static bool running { get; set; }

        // Set by Stop. No new pass starts, and Stop closes the running pass's browser to end it.
        static bool stopRequested { get; set; }

        // ── Run tab ───────────────────────────────────────────────────────────

        static GPALTab runTab = GPAL.Tab.WithName("runTab").WithText("Run").ToGPALObject();

        static GPALTextArea aboutBlurb = (GPALTextArea)GPAL.TextArea
            .WithReadOnly(true)
            .WithHeight(130)
            .WithName("aboutBlurb")
            .WithText("Pages through Hacker News with client workflow loops on each checked browser and engine, each on its own signed-in profile or, per the Profiles tab, a temporary one. Until pages through five pages and names each url, FromResult returns to a stored url by name and by index, and While scrolls until the page reports the bottom. OttoMagic runs them through its REST client, PuppeteerPort through its Puppeteer client. Selenium has neither, so it only logs that. Nothing is written to disk; urls and counts go to the Log tab. PuppeteerPort and PuppeteerPipe have no Firefox, so those pairs are skipped." + Environment.NewLine + Environment.NewLine +
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

        // selenium has neither a REST client nor a Puppeteer client, so its passes publish a notice and run no loops
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
        static void Main(string[] args)
        {
            GPAL
                .WithDriverLocation(System.AppDomain.CurrentDomain.BaseDirectory);

            // DEBUG and DEEPDEBUG are GPAL's internal tracing; leave them out.
            GPAL.WithHandlerEvents(GPALEventType.All & ~(GPALEventType.DEBUG | GPALEventType.DEEPDEBUG))
                .WithExceptionHandler(OnGPALEvent)
                .WithInformationHandler(OnGPALEvent);

            var form = GPAL.Form
                .WithTitle("WorkflowPagingTest  |  every browser and engine")
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

                    // Both clients are tested: OttoMagic engines through browser.OttoMagicClient, an IRESTClient, and
                    // Puppeteer engines through browser.PuppeteerClient. Firefox has no Puppeteer engine, so it skips
                    // those passes.
                    if (BrowserType.FireFox == bt && (AutomationEngine.PuppeteerPort == ae || AutomationEngine.PuppeteerPortHW == ae ||
                        AutomationEngine.PuppeteerPipe == ae || AutomationEngine.PuppeteerPipeHW == ae))
                        continue;

                    string pass = $"{bt} {ae} {mode}";

                    // Null is a temporary profile GPAL creates for the pass and deletes on close.
                    string profile = profiles[bt];

                    SetStatus($"Running [{bt}] [{ae}] [{mode}]{(true == hidden ? " on a hidden desktop" : string.Empty)}...");
                    GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $">>> [{pass}] starting on profile [{profile ?? "temporary"}] <<<");

                    Stopwatch runStopwatch = Stopwatch.StartNew();

                    // A browser and engine pair that throws is reported as its own failure; the rest still run.
                    try
                    {
                        // Held before navigating, so a pass that fails while the browser starts or loads still has a
                        // browser to close.
                        browser = GPAL.Browser
                            .WithBrowserType(bt)
                            .WithUseStealth(StealthType.PatchDriver)
                            .WithAutomationEngine(ae)
                            .WithHiddenDesktop(hidden)
                            .WithProfileDataDirectory(profile)
                            .ToGPALObject();

                        // a hidden desktop now exists, so Peek has something to show
                        if (true == hidden)
                            peekBtn.Enabled = true;

                        // Get opens the page headless, GoTo in a window
                        if (true == asHeadless)
                            browser.Get("https://news.ycombinator.com/news?p=1");
                        else
                            browser.GoTo("https://news.ycombinator.com/news?p=1");

                        // The client matching the pass's engine.
                        if (true == browser.UseOttoMagic)
                            PageWithRestClient(browser, pass);
                        else if (true == browser.UsePuppeteer)
                            PageWithPuppeteerClient(browser, pass);
                        else
                            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"[{pass}] no client workflow loops on Selenium which has neither a REST API nor a Puppeteer client");

                        browser.Close(true);
                    }
                    catch (Exception ex)
                    {
                        GPAL.PublishSimpleEvent(GPALEventType.EXCEPTION, $"[{pass}] ended in an exception, going on to the next", ex: ex);

                        // Close this pass's browser so the next pass does not start beside it.
                        try { browser?.Close(true); } catch (Exception) { }
                    }
                    finally
                    {
                        browser = null;

                        runStopwatch.Stop();
                        runtimes[(bt, ae)] = runStopwatch.Elapsed;

                        if (null != profile)
                            EndWhatIsLeftOn(profile, pass);
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
                GPAL.PublishSimpleEvent(GPALEventType.EXCEPTION, $"Could not check what [{who}] left running on [{profile}]", ex: ex);
            }
        }

        // puts the hidden desktop on the screen for a minute, or until Escape or the Return button on it
        static void OnPeek(object s, EventArgs e)
        {
            string failed = GPAL.HiddenDesktop.Peek(60_000);

            SetStatus(null == failed
                ? "Showing the hidden desktop for a minute. Press Escape or click Return to come back sooner."
                : $"Cannot show the hidden desktop: [{failed}]");
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
        /// The same paging, FromResult and scrolling through browser.OttoMagicClient, which is an IRESTClient
        /// and only exists on the OttoMagic engines.
        /// </summary>
        /// <param name="browser">A browser already on the first page</param>
        /// <param name="pass">Browser, engine and headless state, for the lines this writes</param>
        static void PageWithRestClient(IBrowser browser, string pass)
        {
        IRESTClient client = browser.OttoMagicClient.WithName("Pager").ToGPALObject();

        // Until: page through a fixed number of pages, collecting each page's URL
        int page = 0;
        const int maxPages = 5;

        GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"[{pass}]  Until: paging through [{maxPages}] pages, naming each page url");

        client
            .WithWorkflow(c => c
                .GoTo($"https://news.ycombinator.com/news?p={++page}")
                .AndThen()
                .CheckNetworkIdle()
                .AndThen()
                .WithResultName("pageUrl")
                .GetCurrentUrl()
                // Execute<string>() retrieves the result as a string.
                .Execute<string>())
            .Until(() => page >= maxPages);

        foreach (var url in client.GetExecutionResults("pageUrl"))
            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"[{url}]");

        GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"[{pass}]  FromResult by name: going back to the last url stored under [pageUrl]");

        // FromResult: the workflow never holds the url. Name the endpoint, then take the url from a result the paging
        // loop stored, by name and by index. By name takes the last result stored under it, so this lands on the last
        // page. Held in a local because ClearResults below empties the client's results.
        string landedByName = client
            .WithEndpoint(ApiEndpoint.GoTo)
            .WithUrlFromResult("pageUrl")
            .AndThen()
            .CheckNetworkIdle()
            .AndThen()
            .GetCurrentUrl()
            .Execute<string>();

        GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"[{pass}]  FromResult by index: storing page 1 at index [0], going to page 3, then back to index [0]");

        // By index is a position in everything the client stored, named or not, and every AndThen stores one. Cleared
        // on page 1 and given one named call, so index 0 is page 1. Going to page 3 first means landing on page 1 can
        // only come from the stored result.
        client
            .GoTo("https://news.ycombinator.com/news?p=1")
            .AndThen()
            .CheckNetworkIdle()
            .Execute();

        client.ClearResults();

        client
            .WithResultName("firstLanding")
            .GetCurrentUrl()
            .Execute<string>();

        client
            .GoTo("https://news.ycombinator.com/news?p=3")
            .AndThen()
            .CheckNetworkIdle()
            .Execute();

        string landedByIndex = client
            .WithEndpoint(ApiEndpoint.GoTo)
            .WithUrlFromResult(0)
            .AndThen()
            .CheckNetworkIdle()
            .AndThen()
            .GetCurrentUrl()
            .Execute<string>();

        GPAL.PublishSimpleEvent(GPALEventType.NOTICE,
            $"FromResult by name landed on [{landedByName}], by index [{landedByIndex}]. The stored urls were page 5 by name and page 1 by index.");

        GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"[{pass}]  While: scrolling until the page says it is at the bottom");

        // While: scroll until the page reports it's at the bottom, priming "atBottom"
        // before the loop exists since the condition is checked before the first pass
        browser.GoTo("https://news.ycombinator.com/news");

        client
            .ScrollWindowByVertical(800)
            .AndThen()
            .CheckNetworkIdle()
            .AndThen()
            .WithResultName("atBottom")
            .IsEndOfPage()
            // Execute<bool>() retrieves the result as a bool.
            .Execute<bool>();

        // The paging steps are done, and they stay on the client until cleared.
        client
            .ClearWorkflows()
            .WithWorkflow(c => c
                .ScrollWindowByVertical(800)
                .AndThen()
                .CheckNetworkIdle()
                .AndThen()
                .WithResultName("atBottom")
                .IsEndOfPage()
                // Execute<bool>() retrieves the result as a bool.
                .Execute<bool>())
            .While(() => false == (bool)client.GetExecutionResults()["atBottom"]);

        int scrollPasses = client.GetExecutionResults("atBottom").Count;
        GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"Reached the bottom after [{scrollPasses}] scroll passes.");

        }

        /// <summary>
        /// The same paging, FromResult and scrolling through browser.PuppeteerClient. Each call is made on the client,
        /// not chained, because the narrowed interfaces drop WithResultName after an endpoint, and the steps take a
        /// PuppeteerClient, not a RESTClient.
        /// </summary>
        /// <param name="browser">A browser already on the first page</param>
        /// <param name="pass">Browser, engine and headless state, for the lines this writes</param>
        static void PageWithPuppeteerClient(IBrowser browser, string pass)
        {
            IPuppeteerClient client = browser.PuppeteerClient;

            // Until: page through a fixed number of pages, collecting each page url
            int page = 0;
            const int maxPages = 5;

            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"[{pass}]  Until: paging through [{maxPages}] pages, naming each page url");

            client.ClearResults();

            client
                .ClearWorkflows()
                .WithWorkflow(c =>
                {
                    c.GoTo($"https://news.ycombinator.com/news?p={++page}")
                        .AndThen()
                        .CheckNetworkIdle()
                        .AndThen()
                        .WithResultName("pageUrl")
                        .GetCurrentUrl()
                        .Execute<string>();
                })
                .Until(() => page >= maxPages);

            foreach (var url in client.GetExecutionResults("pageUrl"))
                GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"[{url}]");

            // FromResult: the workflow never holds the url. By name takes the last result stored under it, so this
            // lands on the last page.
            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"[{pass}]  FromResult by name: going back to the last url stored under [pageUrl]");

            client.WithEndpoint(ApiEndpoint.GoTo).WithUrlFromResult("pageUrl").AndThen().CheckNetworkIdle().Execute();

            string landedByName = client.GetCurrentUrl().Execute<string>();

            // By index is a position in everything the client stored, named or not. Cleared on page 1 and given one
            // named call, so index 0 is page 1. Going to page 3 first means landing on page 1 can only come from the
            // stored result.
            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"[{pass}]  FromResult by index: storing page 1 at index [0], going to page 3, then back to index [0]");

            client.GoTo("https://news.ycombinator.com/news?p=1").AndThen().CheckNetworkIdle().Execute();

            client.ClearResults();

            client
                .WithResultName("firstLanding")
                .GetCurrentUrl()
                .Execute<string>();

            client.GoTo("https://news.ycombinator.com/news?p=3").AndThen().CheckNetworkIdle().Execute();

            client.WithEndpoint(ApiEndpoint.GoTo).WithUrlFromResult(0).AndThen().CheckNetworkIdle().Execute();

            string landedByIndex = client.GetCurrentUrl().Execute<string>();

            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"FromResult by name landed on [{landedByName}], by index [{landedByIndex}]. The stored urls were page 5 by name and page 1 by index.");

            // While: scroll until the page reports the bottom. The condition reads the last stored answer, so it is
            // primed once before the loop.
            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"[{pass}]  While: scrolling until the page says it is at the bottom");

            browser.GoTo("https://news.ycombinator.com/news");

            client.ClearResults();

            client
                .WithResultName("atBottom")
                .IsEndOfPage()
                .Execute<bool>();

            client
                .ClearWorkflows()
                .WithWorkflow(c =>
                {
                    c.ScrollWindowByVertical(800)
                        .AndThen()
                        .CheckNetworkIdle()
                        .AndThen()
                        .WithResultName("atBottom")
                        .IsEndOfPage()
                        .Execute<bool>();
                })
                .WhileLoopTimeout(30_000)
                .While(() => false == (bool)client.GetExecutionResults()["atBottom"]);

            int scrollPasses = client.GetExecutionResults("atBottom").Count;

            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"Reached the bottom after [{scrollPasses}] scroll passes.");
        }

    }
}
