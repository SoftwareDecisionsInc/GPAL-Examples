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
using System.IO;
// GP usings, GPALFile and the converter both live in the root namespace
using GenerallyPositive;
using GenerallyPositive.Browser;
using GenerallyPositive.GPALForm;
using static GenerallyPositive.Enums;

namespace GPALFileUrlAndArchiveTest
{
    /// <summary>
    /// Gives a GPALFile a url instead of a path, and keeps numbered copies of what came back.<br/>
    /// The url is fetched to GPAL's temp directory and everything downstream sees an ordinary file, so the
    /// converter, a grid, a database load or anything else taking a GPALFile need not know it came from the web.
    /// Fetching is conditional: with a copy already there, the server is asked whether anything changed, and
    /// "not modified" transfers nothing.<br/>
    /// <b>NOTE:</b> the temp directory is called GPALfilesSafeToDelete because it is. Copy out anything worth
    /// keeping, as the archive half shows.<br/>
    /// Runs from a form so it can use a hidden desktop and Peek. The Log tab shows every event GPAL publishes.
    /// </summary>
    public class Program
    {
        // a small, public, frequently updated file. chrome-for-testing publishes every driver build it knows about
        static string sourceUrl = "https://googlechromelabs.github.io/chrome-for-testing/known-good-versions.json";

        // where the copies worth keeping go, as opposed to the disposable temp directory
        static string archiveDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gpal.archive");

        // Files our own connection cannot have, only the browser's session. the-internet is the Selenium community's
        // practice site; this page is behind basic auth, so an unauthenticated request gets a 401. The site publishes
        // its credentials, admin and admin. NOTE: no file is named, on purpose. The site's filesystem is ephemeral, so
        // uploads last only until the dyno recycles and a name written here would stop existing. The listing is read
        // and a file chosen from it.
        static string sessionUrl = "https://the-internet.herokuapp.com/download_secure";

        // An image proves the fetch carries bytes, not text. Base64 is used because text would put a replacement
        // character wherever a byte is not valid UTF-8, and a txt or md file survives that mistake silently, so it
        // cannot prove the encoding.
        static string[] imageTypes = { ".png", ".jpg", ".jpeg", ".gif" };

        // And something the converter can read, so the file is used, not just downloaded. Often the rotation has none,
        // so it is optional.
        static string[] convertibleTypes = { ".json", ".yaml", ".yml", ".xml" };

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
            .WithText("Gives a GPALFile a url instead of a path. First with no browser: fetches the chrome-for-testing known-good-versions.json to the gpal.working folder beside the program, asks again and gets not modified, archives a numbered copy to the gpal.archive folder beside the program and converts it to yaml there. Then on the browsers and engines checked below, each on its signed-in profile or a temporary one if the Profiles tab says so: signs in to the-internet download_secure page with basic auth, reads its listing, fetches an image and any json, yaml or xml through the browser's session, and archives each as <browser>-<engine>-<mode>-<file> in gpal.archive, converting the readable one to yaml beside it. PuppeteerPort and PuppeteerPipe have no Firefox, so those pairs are skipped." + Environment.NewLine + Environment.NewLine +
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
            // Hook into the GPAL Information and Exception channel messages. The Log tab shows them.
            // DEBUG and DEEPDEBUG are GPAL's own tracing, and not for a log someone reads
            GPAL.WithHandlerEvents(GPALEventType.All & ~(GPALEventType.DEBUG | GPALEventType.DEEPDEBUG))
                .WithExceptionHandler(OnGPALEvent)
                .WithInformationHandler(OnGPALEvent);

            // GPALFile using a url has to have a temoorary directory to fetch the internet file and use it locally
            // WIthTempDirectory() sets the temp directory URL based files are downloaded to
            // Left unset it is a "GPALfilesSafeToDelete" folder beside the exe
            GPAL
                .WithTempDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gpal.working"));

            var form = GPAL.Form
                .WithTitle("GPALFileUrlAndArchiveTest  |  every browser and engine")
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

        // The no-browser part: fetch, fetch again, archive and convert, once per run before any browser pass.
        static void DoFetchAndArchive()
        {
            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"========== 1. GPALFile url FETCH from [{sourceUrl}] - NO BROWSER ==========");

            GPALFile versions = (GPALFile)GPAL.File
                .WithFileName(sourceUrl);

            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"SourceUrl     [{versions.SourceUrl}]");
            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"Filename      [{versions.Filename}]");
            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"DirectoryPart [{versions.DirectoryPart}]");
            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"FilePart      [{versions.FilePart}]");
            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"Bytes         [{new FileInfo(versions.Filename).Length}]");

            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, "========== 2. Ask again for the file using FETCH, nothing changed ==========");

            // same url, and the copy from a moment ago is still there, so this is a conditional request that
            // comes back "not modified" and transfers nothing
            GPALFile again = (GPALFile)GPAL.File
                .WithFileName(sourceUrl);

            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"Filename      [{again.Filename}]");

            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"========== 3. Archive [{again.Filename}] ==========");

            // The destination owns the archive folder and the numbering. Its name comes from the download, so the
            // filename is never written twice.
            GPALFile archive = (GPALFile)GPAL.File
                .WithFileName(Path.Combine(archiveDirectory, versions.FilePart))
                .WithNextFilePattern(NextFilePattern.CounterPadded);

            // run this program repeatedly and the archive keeps growing: .json, _0001.json, _0002.json ...
            versions.CopyTo(archive.Next);

            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, "========== 4. Convert the 'url' GPALFile to Yaml ==========");

            // Nothing here knows or cares that it arrived over http.
            GPAL.Converter
                .WithInput(versions)
                .SaveTo((GPALFile)Path.Combine(archiveDirectory, "known-good-versions.yaml"));

            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"converted to  [{Path.Combine(archiveDirectory, "known-good-versions.yaml")}]\n\n");
        }

        static void RunEverything(bool hidden, bool headless, List<BrowserType> browsers, List<AutomationEngine> engines, Dictionary<BrowserType, string> profiles)
        {
            // A failing no-browser part is logged, and the browser passes still run.
            try
            {
                DoFetchAndArchive();
            }
            catch (Exception ex)
            {
                GPAL.PublishSimpleEvent(GPALEventType.EXCEPTION, "The fetch and archive with no browser ended in an exception, going on to the browser passes", ex: ex);
            }

            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, "========== 5. Secure browser FETCH/downloads (passes credentials) ==========");

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

        /// <summary>
        /// The same GPALFile, for a url our own connection is refused.<br/>
        /// GPAL tries its own connection first, since it is cheap and works for anything public. Only when refused does
        /// the browser take over, bringing the session, cookies and any anti-bot clearance already earned. The request
        /// is sent from inside the page, so nothing navigates, no tab opens, and the workflow's screen is left as it
        /// was.<br/>
        /// One round: every checked browser with every checked engine, windowed or headless.
        /// </summary>
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

                    // Null means a temporary profile, created for the pass and deleted when the browser closes.
                    string profile = profiles[bt];
                    string onProfile = null == profile ? "a temporary profile" : $"profile [{profile}]";

                    SetStatus($"Running [{bt}] [{ae}] {mode}{(true == hidden ? " on a hidden desktop" : string.Empty)}...");
                    GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"Starting [{bt}] [{ae}] [{mode}] on {onProfile}");

                    Stopwatch runStopwatch = Stopwatch.StartNew();

                    // A browser and engine pair that throws is its own result; the rest still run.
                    try
                    {
                        // NOTE: fetch uses Temp directory, browser download uses Download directory
                        // FETCH does adjust if it falls back to the browser to download and copy to the working temp directory
                        // Stored before navigating, so a pass that fails while the browser starts or loads still has a
                        // browser to close. The credentials are sent at the first navigation and are good for the
                        // session, so nothing prompts and the headless passes run unattended.
                        browser = GPAL.Browser
                            .WithBrowserType(bt)
                            .WithUseStealth(StealthType.PatchDriver)
                            .WithAutomationEngine(ae)
                            .WithDownloadLocation(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gpal.downloads"))   // for downloads using the browser - while we are fetching, download in browser is a fallback
                            .WithDriverLocation(System.AppDomain.CurrentDomain.BaseDirectory)
                            .WithHiddenDesktop(hidden)
                            .WithProfileDataDirectory(profile)   // null means a temporary profile
                            .WithCredentials(GPAL.CredentialsFor(CredentialServiceType.None)
                                .WithUsername("admin")
                                .WithPassword("admin")
                                .WithWebAuth(WebAuthType.Basic)
                                .ToGPALObject())
                            .ToGPALObject();

                        // a hidden desktop exists from here on, so Peek has one to show
                        if (true == hidden)
                            peekBtn.Enabled = true;

                        // One navigation lands on the listing, already authenticated. .Get is what makes the browser
                        // headless, so the two halves call different methods.
                        if (true == asHeadless)
                            browser.Get(sessionUrl);
                        else
                            browser.GoTo(sessionUrl);

                        Start(browser, bt, ae, asHeadless);

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

        /// <summary>
        /// One browser, engine and headless state: read the listing, take an image and anything the converter can read,
        /// and keep a copy of each stamped with the pass.
        /// </summary>
        /// <param name="browser">Browser already signed in and sitting on the listing</param>
        /// <param name="bt">Browser type this pass is running</param>
        /// <param name="ae">Automation engine this pass is running</param>
        /// <param name="headless">True when this pass is the headless half</param>
        static void Start(IBrowser browser, BrowserType bt, AutomationEngine ae, bool headless)
        {
            // Read the listing, do not name a file. An anchor's grid cell is its href, not its text, so a grid of every
            // link lists everything on offer this time.
            IGPALGrid<string> links = null;

            browser
                .WithSelector("a")
                .GetGrid(ref links);

            string image = FirstOfType(links, imageTypes);
            string convertible = FirstOfType(links, convertibleTypes);

            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"====> [{bt}] [{ae}] [{(true == headless ? "headless" : "window")}] links found [{links?.Rows}] image [{image ?? "none"}] convertible [{convertible ?? "none"}]");

            if (null != image)
                Archive(Fetch(browser, FileUrl(image)), bt, ae, headless);

            // The rotation often has none of these. Not a failure; this pass just cannot show the file being used after
            // it arrives.
            if (null != convertible)
            {
                GPALFile fetched = Fetch(browser, FileUrl(convertible));
                GPALFile converted = Archive(fetched, bt, ae, headless);

                // And now it is just a file, like anything else the converter is given.
                GPAL.Converter
                    .WithInput(fetched)
                    .SaveTo((GPALFile)Path.ChangeExtension(converted.Filename, ".yaml"));

                GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"Converted to  [{Path.ChangeExtension(converted.Filename, ".yaml")}]");
                GPAL.PublishSimpleEvent(GPALEventType.NOTICE, "====> End of run.\n\n");
            }
        }

        /// <summary>
        /// Turns a link from the listing into an absolute url, since the page gives them relative.
        /// </summary>
        /// <param name="href">The href as the grid reported it</param>
        /// <returns>The absolute url of that file</returns>
        static string FileUrl(string href)
        {
            return sessionUrl + "/" + href.Substring(href.LastIndexOf('/') + 1);
        }

        /// <summary>
        /// Copies the file into the archive under a name carrying the pass, so a directory listing shows which browser,
        /// engine and mode got a file and which came back empty.
        /// </summary>
        /// <param name="fetched">The file the browser just brought back</param>
        /// <param name="bt">Browser type this pass is running</param>
        /// <param name="ae">Automation engine this pass is running</param>
        /// <param name="headless">True when this pass is the headless half</param>
        /// <returns>The archived copy</returns>
        static GPALFile Archive(GPALFile fetched, BrowserType bt, AutomationEngine ae, bool headless)
        {
            GPALFile archived = Path.Combine(archiveDirectory, $"{bt}-{ae}-{(true == headless ? "headless" : "window")}-{fetched.FilePart}");

            // .Next so a second run of the same pass keeps both copies rather than writing over the first
            fetched.CopyTo(archived.Next);

            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"Archived [{archived.Filename}]");

            return archived;
        }

        /// <summary>
        /// The first link in <paramref name="links"/> whose url ends in one of <paramref name="extensions"/>.
        /// </summary>
        /// <param name="links">Grid of hrefs, one per row</param>
        /// <param name="extensions">Extensions worth having, in order of preference</param>
        /// <returns>The url, or null when the listing has nothing of those types this time</returns>
        static string FirstOfType(IGPALGrid<string> links, string[] extensions)
        {
            string found = null;

            // Extensions are the outer loop on purpose, so the answer is the most wanted type present, not whichever
            // link appears first.
            foreach (string extension in extensions)
                for (int row = 0; null == found && row < (links?.Rows ?? 0); row++)
                    if (true == links[row, 0]?.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                        found = links[row, 0];

            return found;
        }

        /// <summary>
        /// Hands a url to a GPALFile and says what came back.
        /// </summary>
        /// <param name="browser">Browser whose session can reach the url</param>
        /// <param name="url">Url of the file to fetch</param>
        /// <returns>The GPALFile, whose Filename is the local copy</returns>
        static GPALFile Fetch(IBrowser browser, string url)
        {
            // .WithBrowser has to come first, since .WithFileName is where the fetching happens
            GPALFile behindTheSession = (GPALFile)GPAL.File
                .WithBrowser(browser)
                .WithFileName(url);

            bool arrived = File.Exists(behindTheSession.Filename);

            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"SourceUrl [{behindTheSession.SourceUrl}]");
            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"Filename  [{behindTheSession.Filename}]");
            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"arrived   [{arrived}] bytes [{(true == arrived ? new FileInfo(behindTheSession.Filename).Length : 0)}]");

            return behindTheSession;
        }
    }
}
