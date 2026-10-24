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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GenerallyPositive;
using static GenerallyPositive.GPALForm.GPALForm;
using System.Diagnostics;
using static GenerallyPositive.Enums;
using GenerallyPositive.Browser;
using GenerallyPositive.GPALForm;
using System.Windows.Forms.DataVisualization.Charting;
using System.Drawing;
using System.Windows.Forms;
using Keys = System.Windows.Forms.Keys;

namespace GPALFormsTest
{
    class Program
    {
        // Resolves an icon in the "icons" folder shipped next to the exe (see .csproj copy rule).
        private static GPALFile Icon(string name) =>
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", name + ".png");

        static GPALBarItem menuAbout = (GPALBarItem)GPAL.BarItem
            .WithText("About")
            .WithImage(Icon("about"))
            .WithCallback<EventHandler>(AboutMenuHandler)
            .ToGPALObject();

        // greyed from the start, and switched on by the Enable button on the New Controls tab
        static GPALBarItem menuSave = (GPALBarItem)GPAL.BarItem
            .WithText("Save")
            .WithEnabled(false)
            .WithCallback<EventHandler>((s, e) => gPALStatus.Text = "Save clicked")
            .WithName("menuSave")
            .ToGPALObject();

        static GPALBarItem menuExit = (GPALBarItem)GPAL.BarItem
            .WithText("Exit")
            .WithImage(Icon("exit"))
            .WithShortcutKey(Keys.Control | Keys.Q)
            .WithCallback<EventHandler>(ExitMenuHandler)
            .ToGPALObject();

        static GPALMenuBar gPALMenuBar = (GPALMenuBar)GPAL.MenuBar
            .WithMenu("File")
            .WithItem(menuSave)
            .WithItem(menuExit)
            .WithMenu("Help")
            .WithItem(menuAbout)
            .WithName("MainMenu")
            .ToGPALObject();

        static GPALBarItem tbNew = (GPALBarItem)GPAL.BarItem
            .WithText("New")
            .WithImage(Icon("new"))
            .WithToolTip("New workflow")
            .WithCallback<EventHandler>(NewToolbarHandler)
            .ToGPALObject();

        static GPALBarItem tbOpen = (GPALBarItem)GPAL.BarItem
            .WithText("Open")
            .WithImage(Icon("open"))
            .WithToolTip("Open")
            .WithCallback<EventHandler>(OpenToolbarHandler)
            .ToGPALObject();

        static GPALBarItem tbSave = (GPALBarItem)GPAL.BarItem
            .WithEnabled(false)
            .WithText("Save")
            .WithImage(Icon("save"))
            .WithToolTip("Save")
            .WithCallback<EventHandler>(SaveToolbarHandler)
            .ToGPALObject();

        static GPALBarItem tbRun = (GPALBarItem)GPAL.BarItem
            .WithText("Run")
            .WithImage(Icon("run"))
            .WithToolTip("Run the workflow")
            .WithCallback<EventHandler>(RunToolbarHandler)
            .ToGPALObject();

        static GPALToolbar gPALToolbar = (GPALToolbar)GPAL.Toolbar
            .WithItem(tbNew)
            .WithItem(tbOpen)
            .WithItem(tbSave)
            .WithSeparator()
            .WithItem(tbRun)
            .WithName("MainToolbar")
            .ToGPALObject();

        static GPALButton gPalButton1 = GPAL.Button
            .WithName("button1")
            .WithText("Start The Workflow")
            .WithCallback<EventHandler>((s, e) => gPalButton1.Enabled = false)
            .WithCallbackOffUIThread<EventHandler>(StartButtonClickHandler)
            .WithShortcutKey(Keys.Alt | Keys.S)
            .ToGPALObject();

        // Enabled from the start: without Start it picks up a browser another program left running
        static GPALButton gPalButton2 = GPAL.Button
            .WithName("button2")
            .WithText("Continue The Workflow")
            .WithCallback<EventHandler>((s, e) => gPalButton2.Enabled = false)
            .WithCallbackOffUIThread<EventHandler>(ContinueButtonClickHandler)
            .WithShortcutKey(Keys.Alt | Keys.C)
            .ToGPALObject();

        static GPALButton gPalButton3 = GPAL.Button
            .Default
            .WithName("button3")
            .WithText("Exit")
            .WithWidth(250)
            .WithCallback<EventHandler>(ExitButtonClickHandler)
            .WithShortcutKey(Keys.Alt | Keys.E)
            .ToGPALObject();

        // Releases Start's browser and ends the program, leaving the browser running for another instance to Continue
        static GPALButton handOffBtn = GPAL.Button
            .WithEnabled(false)
            .WithName("handOff")
            .WithText("Hand Off And Exit")
            .WithCallback<EventHandler>((s, e) =>
            {
                startBrowser?.Release();
                startBrowser = null;

                ExitButtonClickHandler(s, e);
            })
            .ToGPALObject();

        static GPALRadioButton gPalRadioButton1 = GPAL.RadioButton
            .WithChecked(true)
            .WithName("Radiobutton1")
            .WithText("Chrome")
            .ToGPALObject();

        static GPALRadioButton gPalRadioButton2 = GPAL.RadioButton
            .WithName("Radiobutton2")
            .WithText("Edge")
            .ToGPALObject();

        // Firefox has no Puppeteer engine, so PuppeteerPort and PuppeteerPipe are off while Firefox is chosen. A Puppeteer choice moves
        // to Selenium, the engine GPAL itself steps Firefox down to.
        static GPALRadioButton gPalRadioButton3 = GPAL.RadioButton
            .WithName("Radiobutton3")
            .WithText("Firefox")
            .WithCallback<EventHandler>((s, e) =>
            {
                if (true == gPalRadioButton3.Checked && (true == enginePuppeteer.Checked || true == enginePuppeteerPipe.Checked))
                    engineSelenium.Checked = true;

                enginePuppeteer.Enabled = false == gPalRadioButton3.Checked;
                enginePuppeteerPipe.Enabled = false == gPalRadioButton3.Checked;
            })
            .ToGPALObject();

        static GPALRadioButton gPalRadioButton4 = GPAL.RadioButton
            .WithName("Use Selenium")
            .WithText("Selenium")
            .ToGPALObject();

        static GPALRadioButton gPalRadioButton5 = GPAL.RadioButton
            .WithName("Use JavaScript")
            .WithText("JavaScript (Selenium)")
            .ToGPALObject();

        static GPALRadioButton gPalRadioButton6 = GPAL.RadioButton
            .WithChecked(true)
            .WithName("Use Hardware")
            .WithText("Hardware (All Engines)")
            .ToGPALObject();

        static GPALRadioButton engineSelenium = GPAL.RadioButton
            .WithName("Engine Selenium")
            .WithText("Selenium (Selenium)")
            .ToGPALObject();

        static GPALRadioButton enginePuppeteer = GPAL.RadioButton
            .WithChecked(true)
            .WithName("Engine PuppeteerPort")
            .WithText("PuppeteerPort")
            .ToGPALObject();

        static GPALRadioButton enginePuppeteerPipe = GPAL.RadioButton
            .WithName("Engine PuppeteerPipe")
            .WithText("PuppeteerPipe")
            .ToGPALObject();

        static GPALRadioButton engineOttoMagic = GPAL.RadioButton
            .WithName("Engine OttoMagic")
            .WithText("OttoMagic")
            .ToGPALObject();

        // Radio buttons are one set per container, so each set is in a group box of its own.
        static GPALGroupBox browserGroup = (GPALGroupBox)GPAL.GroupBox.WithText("Browser")
            .WithFormControl(gPalRadioButton1)
            .WithFormControl(gPalRadioButton2)
            .WithFormControl(gPalRadioButton3)
            .ToGPALObject();

        static GPALGroupBox engineGroup = (GPALGroupBox)GPAL.GroupBox.WithText("Engine")
            .WithFormControl(engineSelenium)
            .WithFormControl(enginePuppeteer)
            .WithFormControl(enginePuppeteerPipe)
            .WithFormControl(engineOttoMagic)
            .ToGPALObject();

        static GPALSplitter browserAndEngine = (GPALSplitter)GPAL.Splitter.HSplit(0.5)
            .WithFormControl(browserGroup)
            .WithFormControl(GPAL.SplitRight)
            .WithFormControl(engineGroup)
            .ToGPALObject();

        static GPALGroupBox interactionGroup = (GPALGroupBox)GPAL.GroupBox.WithText("Element interaction type")
            .WithFormControl(gPalRadioButton5)
            .WithFormControl(gPalRadioButton6)
            .WithFormControl(gPalRadioButton4)
            .ToGPALObject();

        static GPALInput gPalInput = GPAL.Input
            .WithText(System.AppDomain.CurrentDomain.BaseDirectory)
            .WithCallback<EventHandler>(InputHandler)
            .ToGPALObject();

        static GPALTextArea gPALTextArea = GPAL.TextArea
            .WithText("Type something here")
            .WithCallback<EventHandler>(InputHandler)
            .WithShortcutKey(Keys.Alt | Keys.T)
            .ToGPALObject();

        static Font font = new Font("Times New Roman", 16.0f);

        static GPALLabel gPALLabel = GPAL.Label
            .WithText("Form Tester")
            .WithFont(font)
            .ToGPALObject();

        static GPALCheckbox gPALCheckbox1 = GPAL.Checkbox
            .WithChecked(true)
            .WithText("Wait on document.ready")
            .ToGPALObject();

        static GPALCheckbox gPALCheckbox5 = GPAL.Checkbox
            .WithText("Overwrite")
            .ToGPALObject();

        static GPALCheckbox gPALCheckbox6 = GPAL.Checkbox
            .WithText("Search for Selector")
            .ToGPALObject();

        static GPALCheckbox gPALCheckbox7 = GPAL.Checkbox
            .WithText("Image Search")
            .WithCallback<EventHandler>(ImageSearchHandler)
            .ToGPALObject();

        // The first tab: what Start, Continue and Hand Off do, since nothing on the form says Continue joins a running browser
        static GPALTab howTab = GPAL.Tab.WithName("howTab").WithText("How It Works").ToGPALObject();

        static GPALTextArea howText = (GPALTextArea)GPAL.TextArea
            .WithReadOnly(true)
            .WithHeight(400)
            .WithName("howText")
            .WithText("Start The Workflow opens the browser and engine picked on Tab 1, goes to a Stack Exchange question about downloading files, saves the page behind its download link as page.html beside the exe, and hovers the link. The browser stays open." + Environment.NewLine + Environment.NewLine +
                      "Continue The Workflow does not open a browser. It connects to the one already running: Selenium and Puppeteer through its debug port (57005), OttoMagic through GPALRestAPI at the URL on the Profiles tab. It saves the link again as test.html, middle-clicks it into a new tab, steps between the tabs, closes the new one and closes the browser." + Environment.NewLine + Environment.NewLine +
                      "Continue also works from a second copy of this program. Click Start in one copy, then Hand Off And Exit, which ends that copy and leaves its browser running. Open another copy, pick the same browser and engine, and click Continue. Name a hidden desktop on the Profiles tab and both copies use it: the browser runs out of sight, and Peek shows it." + Environment.NewLine + Environment.NewLine +
                      "The other tabs show GPAL form controls: the menu and toolbar, a chart, a tree, a list, a grid, file and folder pickers, a slider and a progress bar, and controls and tabs switched on and off while the form is up.")
            .ToGPALObject();

        static GPALTab gPALTab1 = (GPALTab)GPAL.Tab
            .WithName("Tab1")
            .WithText("Tab 1")
            .WithShortcutKey(System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.D1);

        static GPALTab gPALTab2 = (GPALTab)GPAL.Tab
            .WithName("Tab2")
            .WithText("Tab 2")
            .WithShortcutKey(System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.D2);

        static GPALFile pictureFile = @"..\..\..\..\how.to.download.files.firefox.png";

        // images automatically use .WithHardware
        static Selector firefoxLinkSelector = (Selector)GPAL.Selector
            .WithSelectorName("Download Link")
            .WithImage(pictureFile)
            .WithOffsetX(210)
            .WithOffsetY(15);

        static Selector linkSelector = (Selector)GPAL.Selector
            .WithSelectorName("Download Link")
            .WithXPath(@"//a[normalize-space()='How to handle downloading a file in selenium webdriver?']") // 1 matched element
            .WithCSS(@"a[href='https://stackoverflow.com/questions/23510816/how-to-handle-downloading-a-file-in-selenium-webdriver']")
            .WithOffsetX(10)
            .WithOffsetY(25);

        static Browser startBrowser;
        static BrowserType browserType = BrowserType.Chrome;
        static Selector linkPictureSelector = linkSelector;
        static bool waitOnDocumentReady = false;

        static GPALFile inputFile = ((GPALFile)"ebay.words.txt").WithDelimiter(',').ToGPALObject();


        static GPALChart gPalChart = (GPALChart)GPAL.Chart
            .WithPalette(ChartColorPalette.Bright)
            .WithName("Chart 1");

        static GPALTab gPALNewControlsTab = (GPALTab)GPAL.Tab
            .WithName("NewControlsTab")
            .WithText("New Controls")
            .WithShortcutKey(Keys.Alt | Keys.N)  // Alt+N
            .ToGPALObject();

        // Held so the button below can re-enable it.
        static GPALTreeNode lockedNode = null;

        // Tab 3 starts disabled, so its toggle sits on tab 1, where it can be reached. Its text says which way it goes
        // next.
        static GPALButton tab3Button = (GPALButton)GPAL.Button
            .WithText("Enable Tab 3")
            .WithCallback<EventHandler>((s, e) =>
            {
                gPALNewControlsTab.Enabled = false == gPALNewControlsTab.Enabled;

                SyncTab3Button();
            })
            .WithName("tab3Toggle")
            .ToGPALObject();

        // The button text and the status line both show tab 3's state, and both the toggle and the enable-all button
        // change it, so they say it in one place.
        static void SyncTab3Button()
        {
            bool enabled = gPALNewControlsTab.Enabled;

            tab3Button.Text = true == enabled ? "Disable Tab 3" : "Enable Tab 3";
            gPALStatus.Text = true == enabled ? "Tab 3 (New Controls) enabled" : "Tab 3 (New Controls) disabled";
        }

        // Re-enables everything disabled, so the round trip is tested, not just the starting state.
        static GPALButton enableAllButton = (GPALButton)GPAL.Button
            .WithText("Enable disabled items")
            .WithCallback<EventHandler>((s, e) =>
            {
                menuSave.Enabled = true;
                tbSave.Enabled = true;
                gPALComboBox.EnableItem("Firefox");
                gPALListView.EnableItem(2);

                if (null != lockedNode) lockedNode.Enabled = true;

                // tab 3 is the disabled one, so turning everything back on has to include it
                gPALNewControlsTab.Enabled = true;
                SyncTab3Button();

                gPALStatus.Text = "Everything enabled";
            })
            .WithName("enableAll")
            .ToGPALObject();

        // ComboBox
        static string selected = null;

        static GPALComboBox gPALComboBox = (GPALComboBox)GPAL.ComboBox
            .WithItems("Chrome", "Edge", "Firefox"/*, "Opera", "Safari"*/)
            .WithSelectedIndex(0)  // pre-select Chrome
            .WithCallback<EventHandler>((s, e) =>
            {
                if (s is ComboBox cb && cb.SelectedItem != null)
                {
                    selected = cb.SelectedItem.ToString();
                    // Example: update status bar
                    gPALStatus.Text = $"Selected browser: {selected}";
                }
            })
            .WithCallback<EventHandler>((s, e) => Debug.WriteLine("Combo box clicked"))
            .ForEvent(ControlEventType.Click)
            .WithName("BrowserSelector")
            .WithToolTip("Select target browser (default = Chrome)")
            .ToGPALObject();

        static Array automationEngines = Enum.GetValues(typeof(AutomationEngine));
        static AutomationEngine ae = AutomationEngine.PuppeteerPort;

        // The port the first browser listens on and the second attaches to. Unset, GPAL takes the first free port from
        // 0xdead up, so a browser still on 57005 from an earlier run puts this one on 57006, and
        // .WithExistingCDPDebugPort(0xdead) finds nothing and opens a new window.
        static int debugPort = 0xdead;

        static GPALForm myForm;

        // The slider's two ends. The top one is full width, not a pixel count, so the demo shows both a set width and
        // no width.
        const int ButtonWidthSmallest = 75;
        const int ButtonWidthFullWidth = 340;

        static GPALComboBox comboBoxAutomationEngine = (GPALComboBox)GPAL.ComboBox
            .WithItems(automationEngines)
            .WithSelectedItem(ae.ToString())
            .WithCallback<EventHandler>((s, e) =>
            {
                if (s is ComboBox cb && cb.SelectedItem != null)
                {
                    selected = cb.SelectedItem.ToString();
                    // Example: update status bar
                    gPALStatus.Text = $"Selected browser: {selected}";
                }
            })
            .WithCallback<EventHandler>((s, e) => Enum.TryParse<AutomationEngine>(selected, out ae))
            .ForEvent(ControlEventType.Click)
            .WithName("Automaton Engine")
            .WithToolTip("Select target AutomationEngine)")
            .ToGPALObject();

        // Slider example. It reports its range to UIAutomation, which is what SetRange drives
        static GPALSlider gPALSlider = (GPALSlider)GPAL.Slider
            .WithMinimum(ButtonWidthSmallest)
            .WithMaximum(ButtonWidthFullWidth)
            .WithValue(175)
            .WithTickFrequency(25)
            .WithName("Slider")
            .WithCallback<EventHandler>((s, e) =>
            {
                if (false == (s is TrackBar bar) || null == myForm)
                    return;

                // The top of the range is full width, not 340 pixels, which WithButtonWidth reads as 0.
                bool fullWidth = ButtonWidthFullWidth == bar.Value;

                myForm.WithButtonWidth(true == fullWidth ? 0 : bar.Value);
                gPALStatus.Text = true == fullWidth ? "Buttons: full width" : $"Buttons: {bar.Value}px";
            })
            .WithToolTip("Drag me to size the buttons, or have a workflow set me with SetRange")
            .ToGPALObject();

        // Picture example
        static GPALPicture gPALPicture = (GPALPicture)GPAL.Picture
            .WithImage(Icon("about"))
            .WithSizeMode(PictureBoxSizeMode.Zoom)
            .WithHeight(120)
            .WithName("Picture")
            .WithCallback<EventHandler>((s, e) => gPALStatus.Text = "Picture clicked")
            .WithToolTip("An image on a form")
            .ToGPALObject();

        // ProgressBar example
        static GPALProgressBar gPALProgress = (GPALProgressBar)GPAL.ProgressBar
            .WithMinimum(0)
            .WithMaximum(100)
            .WithValue(42)
            .WithStyle(ProgressBarStyle.Continuous)
            .WithName("TaskProgress")
            .WithCallback<EventHandler>((s, e) =>
            {
                if (s is ProgressBar progres)
                    gPALStatus.Text = $"Progres: {progres.Value}";
            })
            .WithToolTip("Shows task progress")
            .ToGPALObject();

        // StatusStrip example
        static GPALStatusStrip gPALStatus = (GPALStatusStrip)GPAL.StatusStrip
            .WithName("AppStatus")
            .WithText("Ready - waiting for input")
            .WithCallback<EventHandler>((s, e) =>
            {
                if (s is StatusBar status)
                    gPALStatus.Text = $"Status bar clicked";
            })
            .WithToolTip("Application status")
            .ToGPALObject();

        // ListView example
        static GPALListView gPALListView = (GPALListView)GPAL.ListView
            .WithColumns("Name", "Value", "Status")
            // Shares, not pixels, so the three columns keep a quarter, a quarter and a half of the width as the form
            // resizes.
            .WithColumnWidth(0.25, 0.25, 0.5)
            .WithView(ListViewMode.Details)
            .WithFullRowSelect(true)
            // A box on the Name header ticks or clears every row but the disabled one, and shows ticked once they all
            // are.
            .WithTickAll(true)
            .WithName("SampleList")
            .WithCallback<EventHandler>((s, e) =>
            {
                if (s is ListView listview && 0 < listview.SelectedItems.Count)
                    gPALStatus.Text = $"{listview.SelectedItems[0].Text} selected";
            })
            .ForEvent(ControlEventType.SelectedIndexChanged)
            .WithCallback<ItemCheckedEventHandler>((s, e) =>
                gPALStatus.Text = $"{gPALListView.CheckedItems.Count(isChecked => isChecked)} of {gPALListView.CheckedItems.Count} rows ticked")
            .ForEvent(ControlEventType.ItemChecked)
            .WithToolTip("Sample list of items")
            .WithHeight(100)
            .ToGPALObject();

        // DataGridView example (with dummy grid)
        static IGPALGrid<string> dummyGrid = GPAL.Grid
            .AddRow(new List<string> { "2025-02-17", "INFO", "Application started" })
            .AddRow(new List<string> { "2025-02-17", "WARN", "Low disk space" })
            .AddRow(new List<string> { "2025-02-17", "ERROR", "Failed to connect" })
            .ToGPALObject();

        static GPALDataGridView gPALDataGrid = (GPALDataGridView)GPAL.DataGridView
            .WithColumns("Timestamp", "Level", "Message")
            // The same shares on the DataGridView, through FillWeight.
            .WithColumnWidth(0.25, 0.25, 0.5)
            .WithReadOnly(true)
            .WithInput(dummyGrid)
            .WithName("LogGrid")
            .WithCallback<DataGridViewCellEventHandler>((s, e) =>
            {
                if (s is DataGridView gridview)
                    gPALStatus.Text = $"{gridview.SelectedCells[0].Value} selected";
            })
            .WithToolTip("Execution log table")
            .ToGPALObject();

        // NumericUpDown example
        static GPALNumericUpDown gPALNumeric = (GPALNumericUpDown)GPAL.NumericUpDown
            .WithMinimum(0)
            .WithMaximum(50)
            .WithValue(5)
            .WithIncrement(1)
            .WithDecimalPlaces(0)
            .WithName("RetryCount")
            .WithCallback<EventHandler>((s, e) => Debug.WriteLine($"Retries set to {gPALNumeric.Value}"))
            .WithToolTip("Number of retry attempts")
            .ToGPALObject();

        // DateTimePicker example
        static GPALDateTimePicker gPALDatePicker = (GPALDateTimePicker)GPAL.DateTimePicker
            .WithValue(DateTime.Now.AddHours(2))
            .WithFormat(DateTimePickerFormat.Custom)
            .WithCustomFormat("yyyy-MM-dd HH:mm")
            .WithName("ScheduleTime")
            .WithCallback<EventHandler>((s, e) => gPALStatus.Text = $"{gPALDatePicker.Value} selected")
            .WithToolTip("Pick a future time")
            .ToGPALObject();

        // TreeView example (simple static nodes)
        static GPALTreeView gPALTree = (GPALTreeView)GPAL.TreeView
            .WithCheckBoxes(false)
            .WithShowLines(true)
            .WithShowPlusMinus(true)
            .WithName("WorkflowTree")
            .WithCallback<TreeViewEventHandler>((s, e) =>
            {
                var tv = (TreeView)s;
                gPALStatus.Text = $"{tv.SelectedNode.Text} selected";
            })
            .WithToolTip("Workflow steps or folder structure")
            .ToGPALObject();

        // Profiles tab. OttoMagic runs only on a profile it is installed in, so a temporary profile cannot run it.
        static GPALTab profilesTab = GPAL.Tab.WithName("profilesTab").WithText("Profiles").ToGPALObject();

        // Use profiles off, or a blank folder, runs that browser on a temporary profile.
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

        // Where Continue picks up an OttoMagic browser. GPALRestAPI serves on 3000 unless that port is taken, and
        // nothing else can tell the continuing program which port it got.
        static GPALInput restApiInput = GPAL.Input
            .WithText("http://localhost:3000/")
            .WithName("restApiUrl")
            .ToGPALObject();

        // The hidden desktop Start runs on and Continue joins by name. Blank runs on your own desktop.
        static GPALInput hiddenDesktopInput = GPAL.Input
            .WithText("")
            .WithName("hiddenDesktopName")
            .ToGPALObject();

        // shows the hidden desktop for a minute, or until Escape or its Return button
        static GPALButton peekBtn = GPAL.Button
            .WithEnabled(false)
            .WithName("peek")
            .WithText("👁 Peek")
            .WithCallback<EventHandler>((s, e) =>
            {
                string failed = GPAL.HiddenDesktop.Peek(60_000);

                gPALStatus.Text = null == failed ? "Showing the hidden desktop for a minute" : $"Cannot show the hidden desktop: {failed}";
            })
            .ToGPALObject();

        // New tab for file/folder selectors
        static GPALTab gPALFileTab = (GPALTab)GPAL.Tab
            .WithName("FileTab")
            .WithText("File & Folder Pickers")
            .WithShortcutKey(System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.F)
            .ToGPALObject();

        // File selector (opens OpenFileDialog)
        static GPALFileSelector gPALFilePicker = (GPALFileSelector)GPAL.FileSelector
            .WithTitle("Select Input File")
            .WithInitialDirectory(AppDomain.CurrentDomain.BaseDirectory)
            .WithSelectFolder(false)           // false = file dialog
            .WithFilter("Text files (*.txt)|*.txt|All files (*.*)|*.*")
            .WithMultiSelect(true)
            .WithCallback<EventHandler>((s, args) =>
            {
                if (((FileSelectorEventArgs)args).Cancelled)
                {
                    Debug.WriteLine("File selection cancelled");
                }
                else
                {
                    Debug.WriteLine($"Selected file: {((FileSelectorEventArgs)args).SelectedPath}");
                    foreach (string path in ((FileSelectorEventArgs)args).SelectedPaths)
                    {
                        Debug.WriteLine($"  - {path}");
                    }
                }
            })
            .WithToolTip("Click to browse for a text file (e.g. ebay.words.txt)")
            .WithName("FilePicker")
            .ToGPALObject();

        // Approval dialog shown from CallAFterFillIn after each row is filled in
        static GPALLabel gPALApprovalLabel = GPAL.Label
            .WithText("Approve this entry?")
            .ToGPALObject();

        static GPALInput gPALApprovalReason = GPAL.Input
            .WithText("")
            .ToGPALObject();

        static GPALButton gPALApproveButton = GPAL.Button
            .WithName("ApproveButton")
            .WithText("Approved")
            .WithCallback<EventHandler>(ApproveButtonClickHandler)
            .ToGPALObject();

        static GPALButton gPALNotApproveButton = GPAL.Button
            .WithName("NotApproveButton")
            .WithText("Not Approved")
            .WithCallback<EventHandler>(NotApproveButtonClickHandler)
            .ToGPALObject();

        static bool approvalResult = false;

        static GPALForm approvalForm = GPAL.Form
            .WithTitle("Approval")
            .WithLeft(1500)
            .WithTop(400)
            .WithWidth(300)
            .WithHeight(180)
            .WithFormControl(gPALApprovalLabel)
            .WithFormControl(gPALApprovalReason)
            .WithFormControl(gPALApproveButton)
            .WithFormControl(gPALNotApproveButton)
            .ToGPALObject();

        // Folder selector (opens FolderBrowserDialog)
        static GPALFileSelector gPALFolderPicker = (GPALFileSelector)GPAL.FileSelector
            .WithTitle("Select Output Folder")
            .WithInitialDirectory(AppDomain.CurrentDomain.BaseDirectory)
            .WithSelectFolder(true)            // true = folder dialog
            .WithCallback<EventHandler>((s, args) =>
            {
                if (((FileSelectorEventArgs)args).Cancelled)
                {
                    Debug.WriteLine("Folder selection cancelled");
                }
                else
                {
                    gPALStatus.Text = $"Selected folder: {((FileSelectorEventArgs)args).SelectedPath}";
                }
            })
            .WithToolTip("Click to choose a folder for saving downloaded files")
            .WithName("FolderPicker")
            .ToGPALObject();

        // Compact file selector - a slim editable textbox plus a "..." button, instead of the full
        // "Browse..." button + read-only box. The selector is not file-only: WithSelectFolder(true)
        // turns any selector (compact or full) into a folder browser. Here we keep it in file mode.
        static GPALFileSelector gPALCompactPicker = (GPALFileSelector)GPAL.FileSelector
            .WithTitle("Select Config File")
            .WithInitialDirectory(AppDomain.CurrentDomain.BaseDirectory)
            .WithSelectFolder(false)
            .WithCompact(true)                 // slim textbox + "..." button
            .WithPlaceholder("Type or browse for a config file...")
            .WithFilter("YAML files (*.yaml)|*.yaml|All files (*.*)|*.*")
            .WithCallback<EventHandler>((s, args) =>
            {
                if (!((FileSelectorEventArgs)args).Cancelled)
                    gPALStatus.Text = $"Config: {((FileSelectorEventArgs)args).SelectedPath}";
            })
            .WithToolTip("Compact selector: type a path directly or click ... to browse")
            .WithName("CompactPicker")
            .ToGPALObject();

        // RichTextBox demo - shows off all inline Markdown styles
        static GPALRichTextBox gPALOutput = (GPALRichTextBox)GPAL.RichTextBox
            .WithText(
                "*Welcome to* **G**{CornflowerBlue}**PAL**{} - __The Generally Positive Automation Library__\n\n" +
                "## What It Does\n" +
                "- *Automates* browsers, apps, and APIs\n" +
                "- **Bold** productivity gains\n" +
                "- ~~Old fragile scripts~~ gone\n" +
                "- Inline `code.LooksLikeThis()`\n\n" +
                "---\n\n" +
                "{Green}Everything just works.{}")
            .WithName("OutputPanel")
            .WithHeight(180)
            .WithToolTip("Renders Markdown: **bold** *italic* __underline__ ~~strike~~ `code` {color}text{} and ---")
            .ToGPALObject();

        // NOTE: [STAThread] is required in order to use the file dialog
        static GPALGroupBox engineBox = (GPALGroupBox)GPAL.GroupBox.WithText("Engine")
        .WithFormControl(GPAL.LabelFor("Automation Engine:"))
        .WithFormControl(comboBoxAutomationEngine)
        .WithFormControl(GPAL.LabelFor("Browser:"))
        .WithFormControl(gPALComboBox)
        .ToGPALObject();

    static GPALGroupBox runBox = (GPALGroupBox)GPAL.GroupBox.WithText("Run")
        .WithFormControl(GPAL.LabelFor("Retry Count:"))
        .WithFormControl(gPALNumeric)
        .WithFormControl(GPAL.LabelFor("Schedule Time:"))
        .WithFormControl(gPALDatePicker)
        .ToGPALObject();

    // a splitter is built whole and then added like any other control, and a group box in one of its panes
    // shows that containers hold containers
    static GPALSplitter engineAndRun = (GPALSplitter)GPAL.Splitter.HSplit(0.55)
        .WithFormControl(engineBox)
        .WithFormControl(GPAL.SplitRight)
        .WithFormControl(runBox)
        .ToGPALObject();

    [STAThread]
        static void Main(string[] args)
        {
            GPAL.WithPublishToConsole(GPALEventType.All & ~GPALEventType.DEEPDEBUG)
                .WithExceptionHandler(ExceptionEventHandler) // get an event anytime an exception occurs
                .WithInformationHandler(ExceptionEventHandler)   // TODO: information event logging and handling
                .WithTypingDelay(50);

            GenerateBarSeries();
            //GenerateSplineSeries();


            gPALCheckbox6.Checked = linkPictureSelector.SearchForSelector;

            myForm = GPAL.Form
                           .WithTitle("Simple Form Test")
                           .WithLeft(1500)
                           .WithTop(100)
                           .WithWidth(425)
                           .WithHeight(700)
                           .WithButtonWidth(175)
                           .WithFormControl(gPALMenuBar)
                           .WithFormControl(gPALToolbar)
                           .WithFormControl(gPALLabel)
                           .WithFormControl(gPalButton1)
                           .WithFormControl(gPalButton2)
                           .WithFormControl(handOffBtn)
                           .WithFormControl(gPalButton3)

                           .WithFormControl(howTab)
                           .WithFormControl(howText)

                           .WithFormControl(gPALTab1)
                           .WithFormControl(tab3Button)
                           .WithFormControl(enableAllButton)
                           .WithFormControl(browserAndEngine)
                           .WithFormControl(gPALCheckbox5)
                           .WithFormControl(gPalChart)

                           .WithFormControl(gPALTab2)
                           .WithFormControl(gPalInput)
                           .WithFormControl(gPALCheckbox1)
                           .WithFormControl(interactionGroup)
                           .WithFormControl(gPALCheckbox6)
                           .WithFormControl(gPALCheckbox7)
                           .WithFormControl(gPALTextArea)
                           .CallAfterFillIn(CallAFterFillIn)

                            .WithFormControl(gPALNewControlsTab)
                            .WithFormControl(engineAndRun)
                            .WithFormControl(gPALProgress)
                            .WithFormControl(GPAL.LabelFor("Button Size"))
                            .WithFormControl(gPALSlider)
                            .WithFormControl(gPALPicture)
                            .WithFormControl(gPALListView)
                            .WithFormControl(gPALDataGrid)
                            .WithFormControl(gPALTree)

                            .WithFormControl(gPALFileTab)
                            .WithFormControl(GPAL.LabelFor("File (full):"))
                            .WithFormControl(gPALFilePicker)
                            .WithFormControl(GPAL.LabelFor("Folder (full):"))
                            .WithFormControl(gPALFolderPicker)
                            .WithFormControl(GPAL.LabelFor("Config (compact):"))
                            .WithFormControl(gPALCompactPicker)
                            .WithFormControl(gPALOutput)

                            .WithFormControl(profilesTab)
                            .WithFormControl(useProfilesCheck)
                            .WithFormControl(profilesHint)
                            .WithFormControl(GPAL.LabelFor("Chrome"))
                            .WithFormControl(chromeProfile)
                            .WithFormControl(GPAL.LabelFor("Edge"))
                            .WithFormControl(edgeProfile)
                            .WithFormControl(GPAL.LabelFor("Firefox"))
                            .WithFormControl(firefoxProfile)
                            .WithFormControl(GPAL.LabelFor("OttoMagic GPALRestAPI URL"))
                            .WithFormControl(restApiInput)
                            .WithFormControl(GPAL.LabelFor("Hidden desktop name (blank runs on your desktop)"))
                            .WithFormControl(hiddenDesktopInput)
                            .WithFormControl(peekBtn)

                            .WithFormControl(gPALStatus)

                            .ToGPALObject();

            gPALListView.AddItem("File1.txt", "2.5 MB", "Done");
            gPALListView.AddItem("File2.pdf", "1.8 MB", "Pending");
            gPALListView.AddItem("File3.lock", "0 KB", "Locked by another run");

            // Greyed and unselectable; the row stays visible so it can say why it is out.
            gPALListView.DisableItem(2);

            // An option that does not apply to the selection elsewhere, greyed, not removed.
            gPALComboBox.DisableItem("Firefox");

            // A whole tab disabled until the workflow enables it.
            gPALNewControlsTab.Enabled = false;

            // Added through the GPAL call, not a raw TreeNode, so the handle is what gets disabled.
            lockedNode = gPALTree.AddNode("Locked workflow");

            if (null != lockedNode) lockedNode.Enabled = false;

            gPALProgress.Value = 75;
            gPALStatus.Text = "Processing 75%...";

            // The picture opened with the about icon; this replaces it while the form is on screen.
            gPALPicture.WithImage(Icon("run"));

            // demonstrate runtime updates via write-through property setters
            gPALLabel.Text = "GPAL Form Tester";
            gPALCheckbox5.Checked = true;
            gPalRadioButton1.Checked = true;
            gPALComboBox.SelectedIndex = 1; // Edge

            {
                // Declared outside so the selection after the finally can reach it.
                GPALTreeNode root = null;

                gPALTree.BeginUpdate();
                try
                {
                    // Root node
                    root = gPALTree.AddNode("Automation Dashboard");

                    // Level 1: Categories
                    GPALTreeNode browsers = root.AddChild("Browsers");
                    GPALTreeNode selectors = root.AddChild("Selectors");
                    GPALTreeNode tasks = root.AddChild("Tasks / Workflows");

                    // Level 2: Browsers. Tag carries whatever the workflow wants to find later
                    browsers.AddChild("Chrome").Tag = "chrome";
                    browsers.AddChild("Edge").Tag = "edge";
                    browsers.AddChild("Firefox").Tag = "firefox";

                    // Level 2: Selectors
                    selectors.AddChild("XPath Selector").Tag = linkSelector;
                    selectors.AddChild("Image Selector").Tag = firefoxLinkSelector;

                    // Level 2: Tasks
                    GPALTreeNode downloadTask = tasks.AddChild("Download File");

                    downloadTask.AddChild("Go to URL");
                    downloadTask.AddChild("Hover over link");
                    downloadTask.AddChild("Right-click > Save As");
                    downloadTask.AddChild("Verify file saved");

                    // Expand root by default
                    root.Expand();
                }
                finally
                {
                    gPALTree.EndUpdate();
                }

                // NOTE: select the root - will fire handler
                if (null != root) root.Select();
            }

            // The tab the form opens on, instead of the first.
            gPALTab2.Activate();

            // fill in the form (FillInFrom) - input opn tab 2 - to simulate an approval process
            // approve/not approve  and a message which appears in the main form status bar
            // used with CallAfterFillIn

            myForm
                //.Show()
                //.FillInFrom(inputFile)
                .ShowDialog();

            string username = gPalInput.Text;
            string password = gPalInput.Text;
            string ticker = gPalInput.Text;

            startBrowser?.Close(true);
        }

        private static void GenerateSplineSeries()
        {
            Series series = gPalChart.Series.Add("Total Income");
            gPalChart.Titles.Add("Total Income");

            series.ChartType = SeriesChartType.Spline;
            series.Points.AddXY("September", 100);
            series.Points.AddXY("Obtober", 300);
            series.Points.AddXY("November", 800);
            series.Points.AddXY("December", 200);
            series.Points.AddXY("January", 600);
            series.Points.AddXY("February", 400);
        }
        private static void GenerateBarSeries()
        {
            string[] seriesArray = { "Cat", "Dog", "Bird", "Monkey" };
            int[] pointsArray = { 2, 1, 7, 5 };

            gPalChart.Titles.Add("Animals");
            // ((Chart)gPALChart.WindowsControl).

            // Add series.
            for (int i = 0; i < seriesArray.Length; i++)
            {
                Series series = gPalChart.Series.Add(seriesArray[i]);
//                series.ChartType = SeriesChartType.Bar;
                series.Points.Add(pointsArray[i]);
                Legend legend = new Legend(seriesArray[i]);
                //                legend.Alignment = StringAlignment.Near;
                gPalChart.Legends.Add(legend);
            }
        }
        public static CallIfStatus CallAFterFillIn(GPALForm myForm, IGPALGrid<string> tokens, int tokenIdx)
        {
            gPALApprovalReason.Text = "";

            // default to the right of the main form, but fall back to the left if the right side runs off-screen
            Screen screen = Screen.FromPoint(new Point(myForm.Left, myForm.Top));
            int rightOfMain = myForm.Left + myForm.Width;
            int approvalLeft = rightOfMain + approvalForm.Width <= screen.WorkingArea.Right
                ? rightOfMain
                : myForm.Left - approvalForm.Width;

            approvalForm
                .WithLeft(approvalLeft)
                .WithTop(myForm.Top)
                .ShowDialog();

            string status = true == approvalResult ? "Approved" : "Not Approved";
            gPALStatus.Text = $"{status}: {gPALApprovalReason.Text}";

            return CallIfStatus.NotHandled;
        }
        private static void ApproveButtonClickHandler(object sender, EventArgs e)
        {
            approvalResult = true;
            CloseParentForm(sender);
        }
        private static void NotApproveButtonClickHandler(object sender, EventArgs e)
        {
            approvalResult = false;
            CloseParentForm(sender);
        }
        private static void CloseParentForm(object sender)
        {
            Control control = (Control)sender;

            while (null != control.Parent)
                control = control.Parent;

            control.Hide();
        }
        public static void ExceptionEventHandler(object sender, EventArgs e)
        {
            Debug.WriteLine(((GPAL.GPALEventArgs)e).Message);
            Debug.WriteLine(((GPAL.GPALEventArgs)e).ExceptionRaised?.Message);
        }
        private static void ImageSearchHandler(object sender, EventArgs e)
        {
            if (((CheckBox)sender).Checked)
            {
                switch (browserType)
                {
                    case BrowserType.Chrome:
                        break;
                    case BrowserType.Edge:
                        break;
                    case BrowserType.FireFox:
                        linkPictureSelector = firefoxLinkSelector;
                        break;
                }
            }
            else
                linkPictureSelector = linkSelector;
        }
        private static void InputHandler(object sender, EventArgs e)
        {
            Debug.WriteLine(((TextBox)sender).Text);
        }

        // Reads the browser, engine and interaction choices into the fields both workflows use. Continue reads them
        // itself when it runs without Start, as a second program would.
        private static bool ReadChoices()
        {
            bool retVal = true;

            if (true == gPalRadioButton1.Checked)
            {
                linkPictureSelector = linkSelector;
                browserType = BrowserType.Chrome;
            }
            else if (true == gPalRadioButton2.Checked)
            {
                linkPictureSelector = linkSelector;
                browserType = BrowserType.Edge;
            }
            else if (true == gPalRadioButton3.Checked)
            {
                browserType = BrowserType.FireFox;
            }
            else
            {
                MessageBox.Show("Please Select a Browser Type.");
                retVal = false;
            }

            if (true == engineSelenium.Checked)
                ae = AutomationEngine.Selenium;
            else if (true == engineOttoMagic.Checked)
                ae = AutomationEngine.OttoMagic;
            else if (true == enginePuppeteerPipe.Checked)
                ae = AutomationEngine.PuppeteerPipe;
            else
                ae = AutomationEngine.PuppeteerPort;

            waitOnDocumentReady = gPALCheckbox1.Checked;
            // when we use hardware, then the selector cannot be scrolled into view using javascript or selenium
            // so we have to search for the selector being in the viewport in order to move the mouse to physically click the link
            // this setting dictates whether or not we search for the selector on the browser page by pressing page down until it's in view
            linkPictureSelector.WithSearchForSelector(gPALCheckbox6.Checked);

            if (true == gPalRadioButton5.Checked)
                _ = linkSelector.WithJavaScript;
            else if (true == gPalRadioButton6.Checked)
                _ = linkSelector.WithHardware;
            else if (true == gPalRadioButton4.Checked)
                _ = linkSelector.WithSelenium;
            else
            {
                MessageBox.Show("Please Select an Interaction Type for Selenium engine.");
                retVal = false;
            }

            return retVal;
        }

        private static void StartButtonClickHandler(object sender, EventArgs e)
        {
            Selector allCookies = "#onetrust-close-btn-container > button";
            Selector allCookiesIframe = "body > iframe:nth-child(31)";

            GPALFile  pageFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "page.html");

            if (false == ReadChoices())
            {
                gPalButton1.Enabled = true;
                return;
            }

            // the chosen browser's profile folder, or null for a temporary profile
            GPALFileSelector profileFolder = BrowserType.Chrome == browserType ? chromeProfile : BrowserType.Edge == browserType ? edgeProfile : firefoxProfile;
            string profile = true == useProfilesCheck.Checked && false == string.IsNullOrWhiteSpace(profileFolder.SelectedPath) ? profileFolder.SelectedPath.Trim() : null;


            gPalButton1.Enabled = false;
            gPalRadioButton1.Enabled = false;
            gPalRadioButton2.Enabled = false;
            gPalRadioButton3.Enabled = false;
            engineSelenium.Enabled = false;
            enginePuppeteer.Enabled = false;
            enginePuppeteerPipe.Enabled = false;
            engineOttoMagic.Enabled = false;
            useProfilesCheck.Enabled = false;
            chromeProfile.Enabled = false;
            edgeProfile.Enabled = false;
            firefoxProfile.Enabled = false;
            restApiInput.Enabled = false;
            hiddenDesktopInput.Enabled = false;
            gPALCheckbox1.Enabled = false;
            gPalRadioButton4.Enabled = false;
            gPalRadioButton5.Enabled = false;
            gPalRadioButton6.Enabled = false;
            gPALCheckbox5.Enabled = false;
            gPALCheckbox6.Enabled = false;
            gPALCheckbox7.Enabled = false;
            gPalInput.Enabled = false;
            gPALTextArea.Enabled = false;

            startBrowser = (Browser)GPAL.Browser;

            // a named hidden desktop, so Continue, or another program, can join it by name
            string desktopName = hiddenDesktopInput.Text.Trim();

            if (0 < desktopName.Length)
            {
                startBrowser.WithHiddenDesktop(desktopName);
                peekBtn.Enabled = true;
            }

            startBrowser
                .WithPersistentSelector(allCookies)
                .PersistentCallIfFound(FoundAllCookies);


            startBrowser
                .WithBrowserType(browserType)
                .WithProfileDataDirectory(profile)   // null means a temporary profile
                .WithDriverLocation(gPalInput.Text)
                .WithCDPDebugPort(debugPort)
                .WithAutomationEngine(ae)            // after WithCDPDebugPort, which sets PuppeteerPort
                .WithLoadImages(false)
//                .WithWaitOnDocumentReady(true == waitOnDocumentReady ? 30_000 : 0)
                .WithWaitOnIdleConnection()
                //.GoTo("https://stackoverflow.com/questions/69858412/selenium-javascript-unable-to-get-location-of-anchor-in-paragraph?noredirect=1#comment123601561_69858412")
                .GoTo("https://sqa.stackexchange.com/questions/2197/how-to-download-a-file-using-seleniums-webdriver")
                ;

            startBrowser
                .WithSelector(linkPictureSelector)
                .RightClickAndDownload(pageFile);

            startBrowser
                .Hover();
            //startBrowser
            //    .Focus();

            gPalButton2.Enabled = true;
            handOffBtn.Enabled = true;
        }

        private static void ContinueButtonClickHandler(object sender, EventArgs e)
        {
            gPalButton2.Enabled = false;
            handOffBtn.Enabled = false;

            // Without Start, as a second program, Continue reads the choices itself
            if (null == startBrowser && false == ReadChoices())
            {
                gPalButton2.Enabled = true;
                return;
            }

            gPalButton1.Enabled = false;

            GPALFile  htmlFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test.html");

            bool overwrite;
            overwrite = gPALCheckbox5.Checked;

            Browser continueBrowser = (Browser)GPAL.Browser
                .WithBrowserType(browserType)
                .WithUseStealth(StealthType.PatchDriver)
                .WithDriverLocation(gPalInput.Text)
                .WithOverwriteExistingFile(overwrite)
                .WithWaitOnDocumentReady(true == waitOnDocumentReady ? 30_000 : 0)
                .WithWaitOnIdleConnection(false == waitOnDocumentReady)
                .ToGPALObject();

            // Join Start's hidden desktop by its name
            if (0 < hiddenDesktopInput.Text.Trim().Length)
            {
                continueBrowser.WithHiddenDesktop(hiddenDesktopInput.Text.Trim());
                peekBtn.Enabled = true;
            }

            // Firefox's Marionette takes one connection, so Start lets go of Firefox before Continue picks it up, as a
            // first program would when it hands its browser on.
            if (BrowserType.FireFox == browserType && AutomationEngine.Selenium == ae)
                startBrowser?.Release();

            // Pick up the browser Start left running: OttoMagic through GPALRestAPI, the others through the debug port.
            if (AutomationEngine.OttoMagic == ae)
                continueBrowser
                    .WithAutomationEngine(AutomationEngine.OttoMagic)
                    .WithGPALRestAPIUrl(restApiInput.Text.Trim());
            else
                continueBrowser
                    .WithExistingCDPDebugPort(debugPort);

            continueBrowser
                .WithSelector(linkPictureSelector)
                .Hover()
                .RightClickAndDownload(htmlFile);

            if (0 < htmlFile.ReturnFilenames.Count)
                Debug.WriteLine(htmlFile.ReturnFilenames[htmlFile.ReturnFilenames.Count - 1]); // filename saved is added to the list as the last entry

            continueBrowser
                .MiddleClick();

            continueBrowser
                .NextTab
                .WaitFor(1_000);

            _= continueBrowser
                .PreviousTab
                .WaitFor(1_000)
                .NextTab;

            continueBrowser
                .CloseTab()
                .WaitFor(1_000);

            // Both browsers close. The one Continue attached closes first, while the browser is still running.
            continueBrowser.Close(true);

            startBrowser?.Close(true);

 
            startBrowser = null;

            gPalRadioButton1.Enabled = true;
            gPalRadioButton2.Enabled = true;
            gPalRadioButton3.Enabled = true;
            engineSelenium.Enabled = true;
            enginePuppeteer.Enabled = false == gPalRadioButton3.Checked;
            enginePuppeteerPipe.Enabled = false == gPalRadioButton3.Checked;
            engineOttoMagic.Enabled = true;
            useProfilesCheck.Enabled = true;
            chromeProfile.Enabled = true;
            edgeProfile.Enabled = true;
            firefoxProfile.Enabled = true;
            restApiInput.Enabled = true;
            hiddenDesktopInput.Enabled = true;
            peekBtn.Enabled = false;
            gPALCheckbox1.Enabled = true;
            gPALCheckbox1.Checked = true;
            gPalRadioButton4.Enabled = true;
            gPalRadioButton5.Enabled = true;
            gPalRadioButton6.Enabled = true;
            gPALCheckbox5.Enabled = true;
            gPALCheckbox6.Enabled = true;
            gPALCheckbox7.Enabled = true;
            gPalInput.Enabled = true;
            gPALTextArea.Enabled = true;
            gPalButton1.Enabled = true;
            gPalButton2.Enabled = true;
        }
        private static void AboutMenuHandler(object sender, EventArgs e)
        {
            MessageBox.Show(
                "GPAL - Generally Positive Automation Library\n\n" +
                "Version 1.0  |  Copyright 2026 Software Decisions, Inc.\n\n" +
                "GPAL is a fluent C# library for building reliable, readable\n" +
                "automation workflows that drive browsers,\n" +
                "applications, and APIs without\n" +
                "fragile scripts or manual browser setup.\n\n" +
                "Designed and built in Texas by Michael B. Vederman.",
                "About GPAL",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        private static void ExitMenuHandler(object sender, EventArgs e)
        {
            Environment.Exit(0xdead);
        }
        // Toolbar button handlers - just report to the status bar so the wiring is visible.
        private static void SetToolbarStatus(string message)
        {
            gPALStatus.Text = message;
        }
        private static void NewToolbarHandler(object sender, EventArgs e) => SetToolbarStatus("Toolbar: New");
        private static void OpenToolbarHandler(object sender, EventArgs e) => SetToolbarStatus("Toolbar: Open");
        private static void SaveToolbarHandler(object sender, EventArgs e) => SetToolbarStatus("Toolbar: Save");
        private static void RunToolbarHandler(object sender, EventArgs e) => SetToolbarStatus("Toolbar: Run");
        private static void ExitButtonClickHandler(object sender, EventArgs e)
        {
            Control control = (Control)sender;

            while (null != control.Parent)
                control = control.Parent;

            control.Hide();
        }
        public static int CallIfFound(List<IGPALElement> foundElements, List<IGPALElement> matchedElements, Selector selector, bool matchedAll)
        {
            int retVal = 0;
            foreach (GPALElement webElement in foundElements)
            {
                string value = webElement.GetAttribute("value");
                Debug.WriteLine($"<{webElement.TagName}> : {webElement.Text} : {value}");
            }
            return retVal;
        }
        public static CallIfStatus FoundAllCookies(IBrowser browser, List<IGPALElement> foundElements, List<IGPALElement> matchedElements, Selector selector, bool matchedAll)
        {
            CallIfStatus retVal = CallIfStatus.NotHandled;

            foreach (GPALElement webElement in foundElements)
            {
                if (webElement.Text.Contains("cookies"))
                {
                    webElement.Click();
                    retVal = CallIfStatus.Handled;
                }
            }

            return retVal;
        }
    }
}

