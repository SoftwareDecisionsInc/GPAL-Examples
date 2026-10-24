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
using System.Diagnostics;
// our statements for using GPAL
using GenerallyPositive;
using GenerallyPositive.Browser;
using static GenerallyPositive.Enums;

namespace InputGridChaining
{
    public class Workflows
    {
        static int itemsToGet = 10;
        static int pagesToGet = 1;
        static IGPALGrid<string> linkGrid;
        static IGPALGrid<string> retGrid;
        static Selectors Selectors;
        static GPALFile fileList = null;
        static System.Drawing.Rectangle windowSize = new System.Drawing.Rectangle(10, 10, 1500, 900);


        #region <Input Grid Definitions>
        // we are only using our constructed grid for this example, but include code for file and database usage
        // construct our own input grid with two terms to enter and search for
        static IGPALGrid<string> inputGrid = GPAL.Grid.ToGPALObject();

        // Alternately, we can have input from a file, one word per line (csv is the default separator if you have multiple tokens per line)
        static GPALFile inputFile = "ebay.words.txt";

        // Or 2 top rows from database table TestInput, each column is a token in a row
        static GPALDatabase database = (GPALDatabase)GPAL.Database
            .WithServerName(@"SERVER\SqlServer")
            .WithDatabaseName("GPAL")
            .WithDatabaseType(DatabaseType.SQLServer)
            .WithTableName("TestInput")
            .WithRowCount(2);
        #endregion <Input Grid Definitions>

        // the search terms every pass fills in, three random ones picked once per run. cleared first, so a second
        // run from the form does not add three more to the ones before
        public static void PrepareInputGrid()
        {
            inputGrid.Clear();

            inputGrid.AddRow(new List<string>() { MagicHelper.GetRandomStarWarsQuery() });
            inputGrid.AddRow(new List<string>() { MagicHelper.GetRandomStarWarsQuery() });
            inputGrid.AddRow(new List<string>() { MagicHelper.GetRandomStarWarsQuery() });
        }

        /// <summary>
        /// There are three simple, chained workflows:<br/>
        /// 1) Open ebay and enter in a search term using tokens from our input file ebay.words.txt.<br/>
        /// 2) After the token is filled in, CallAfterFillIn then clicks the search button and waits for the results 'LINK' elements.<br/>
        /// 3) Once the results are shown and the 'LINK' elements are found, CallIfFound then opens the link in a new tab and retrieves results from the item page. And saves to a master grid.<br/>
        /// 4) After all the results are retrieved for top 10 results on 2 pages, the master grid results are saved with headers to a tab delimited file All.txt
        /// 
        /// We also define a persistent selector to deal with a popup that only shows once when we enter our second search term. That gets dealt with aside from the main workflow.
        /// </summary>
        // one pass on a browser the form has built. the form closes the browser after it, and a pass that throws
        // leaves that to the form as well
        public static void StartWorkflow(IBrowser browser, BrowserType bt, AutomationEngine ae, string mode, bool asHeadless)
        {
        #region <Working Variables>
            // new each pass, so a pass that failed part way does not hand its rows to the next one
            retGrid = GPAL.GridForType<string>();
            linkGrid = GPAL.Grid.ToGPALObject();
            Selectors = new Selectors();

        #endregion <Working Variables>

            #region <Workflow 1>
            // one file per browser, engine and mode, so a round does not overwrite the one before it
            fileList = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"all.{ae}.{bt}.{mode}.txt");

            browser
                .WithPersistentSelector(Selectors.persistentSelector)   // persistent selectors go into their own separare UOW
                                                                        //.WithPersistentSelector(Selectors.findSimilarSelector)   // persistent selectors go into their own separare UOW
                ;

            // Get opens the page headless, GoTo in a window
            if (true == asHeadless)
                browser
                    .WithOverwriteExistingFile(false)
                    .WithWindowSize(windowSize)
                    .Get("https://www.ebay.com/");
            else
                browser
                    .WithOverwriteExistingFile(false)
                    .WithWindowSize(windowSize)
                    .GoTo("https://www.ebay.com/");

            // ebay search input is somehow added late to the domain after document ready and network is idle, so use waitfor to check for existence before advancing
            browser
                .WithSelector(Selectors.searchSelector)
                .WaitFor(2_000)
                .CallAfterFillIn(CallAFterFillIn);
            browser
                .FillInFrom(inputGrid);

            #endregion <Workflow 1>

            #region <End Workflow, Save to File & Close>
            // after everything has been retrieved into the master grid retGrid, save it out with headers
            browser
                .WithGridToSave(retGrid)
                .WithHeader("Description")
                .WithHeader("Seller")
                .WithHeader("Rating")
                .WithHeader("Link")
                .SaveTo(fileList.Next);
            // the form closes the browser and [true] kills the webdriver that keeps running
            #endregion <End Workflow, Save to File & Close>

            // the output filename is added to the end of our filelist
            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"File saved to [{fileList.ReturnFilenames[0]}].");

            retGrid.Clear();
        }

        /// <summary>
        /// Called after a row of tokens are entered into input elements
        /// see: http://gpal.sditx.com/api/GenerallyPositive.Browser.Browser.CallAfterFillInDelegate.html
        /// </summary>
        public static CallIfStatus CallAFterFillIn(IBrowser myBrowser, IGPALGrid<string> tokens, int tokenIdx)
        {
            #region <Workflow 2>
            // click the search button after text entered
            myBrowser
                .WithSelector(Selectors.searchButton)
                .LeftClick()
                .WaitFor(1_500);    // ebay has a redirect page 'making sure you are safe for ebay' or something....

            if (0 < Selectors.searchButton.ElementsFoundAndMatchedCount)
                myBrowser
            // now find all the item links and iterate thru them to retrieve the item page information
                .WithSelector(Selectors.linkColumn)
                .CallIfFound(CallIfFound)
                .WithAllThatMatch(itemsToGet)   // only iterate over top 3 matching rows (on each page)
                .WithNextPageButton(Selectors.nextPageButton)
                .WithPages(pagesToGet)
                .StartWorkflow();
            #endregion <Workflow 2>

            // end of workflow 2
            return CallIfStatus.Handled;
        }
        /// <summary>
        /// When the item link search result elements are found, call this method to click a link and retrieve the product information.<br/>
        /// see: http://gpal.sditx.com/api/GenerallyPositive.Browser.Browser.CallIfDelegate.html
        /// </summary>
        public static CallIfStatus CallIfFound(IBrowser browser, List<IGPALElement> foundElements, List<IGPALElement> matchedElements, Selector selector, bool matchedAll)
        {
            CallIfStatus retVal = CallIfStatus.Handled;

            // the GPAL ElementAssistant allows you to use fluent syntax to manipulate WebElements
            // this allows you multiple methods to interact with an element (Selenium, hardware, javascript)
            // you can manipulate the webelements directly, but are limited to Selenium only
            ElementAssistant elementAssistant = (ElementAssistant)GPAL
                .ElementAssistant(selector);

            #region <Workflow 3>

            int count = 0;
            // iterate thru the matched elements
            foreach (GPALElement webElement in matchedElements)
            {
                if (count++ > 2)
                    break;

                // several possible ways to click on a search result item to get to the listing page, and then to retrieve data from the new page

                // #1
                // work with the webelement directly to GET THE HREF
                // A) instantiate a new browser and then on the new page retrieve the results
                // B) use existing browser to navigate to url in new tab

                string href = webElement.GetAttribute("href");

                // #1A
                // instantiate a new browser and open in a window, but it must use the same automation as the main workflow or errors will ensur
                //IBrowser browser2 = GPAL.Browser
                //    .WithBrowserType(BrowserType.Chrome)
                //    //.WithAutomationEngine(browser.AutomationEngine)
                //    .WithWindowSize(windowSize)
                //    .ToGPALObject();

                //browser2
                //    .GoTo(href)
                //    .WaitFor(2_000);

                //browser2
                //    .WithSelector(Selectors.descriptionColumn)
                //    .WithSelector(Selectors.sellerColumn)
                //    .WithSelector(Selectors.feedbackColumn)
                //    .WithSelector(href, "href")   // include literal string in output, must use second parameter - only one parm will implicitly convert string to selector
                //    .GetGrid(out IGPALGrid<string> tmpGrid)
                //    .Close();

                // #1B
                // existing browser in new tab
                //browser
                //    .NewTab(href)
                //    .WaitFor(2_000)
                //    .WithSelector(Selectors.descriptionColumn)
                //    .WithSelector(Selectors.sellerColumn)
                //    .WithSelector(Selectors.feedbackColumn)
                //    .WithSelector(() => { return href; })
                //    .GetGrid(out IGPALGrid<string> tmpGrid)
                //    .CloseTab();

                // #2
                // with the elementassistant, using GPAL to click on the webelement.
                // advantage is being able to use multiple interaction types, logging
                //elementAssistant
                //    .WithElement(webElement)
                //    //.WithSelector()  // TODO: add finding selector under element
                //    .WithHardware
                //    .PressModifierKey(ModifierKeys.Control)
                //    .LeftClick()
                //    .ReleaseModifierKey(ModifierKeys.Control);

                // #2
                // a click opens a new tab, but GPAL must know this, so we must use NextTab() to move GPAL to the newly opened tab
                // this is only necessary when a link opens a new tab
                // use the browser to switch to the next tab and retrieve the results
                //browser
                //    .WaitFor(1_000)
                //    .WithSelector(Selectors.descriptionColumn)
                //    .WithSelector(Selectors.sellerColumn)
                //    .WithSelector(Selectors.feedbackColumn)
                //    .WithSelector(() => { return href; })
                //    .GetGrid(out IGPALGrid<string> tmpGrid) // retrieve the above three elements data into tmpGrid so we can save it to a master grid
                //    ;
                //browser
                //    .CloseTab();

                // #3
                // use selenium to just click the element directly, then the same tab switching to get gpal to retrieve results
                // selenium clicks are sometimes 'intercepted' by other elements (popup message divs for example)

                // doesn't work using ottomagic, use middleclick
                // webElement.Click(ModifierKeys.Control); // will open in a new tab, but we don't track that
                //if (true == browser.UseSelenium)
                //    webElement.Click();
                //else
                    webElement.MiddleClick(); // will open in a new tab, but we don't track that

                browser.WaitFor(1_000);

                // middle click doesn't open in new tab with puppeteer or selenium, or hardware but does for javascript otthmagic?
                // NOTE: puppeteer tab switch will top the window, but we don't have to tab switch to scrape
                // CAVEAT: background tab throttling may not render the tab until it is foreground, but to run in the bazckground not topping, prolly use OttoMagic  instead
                _ = browser.NextTab;

                // 
                // use the browser to switch to the next tab and retrieve the results
                //browser
                //   .WaitFor(5_000);

                //browser
                //.GoToTab(href);
                //.NextTab();          // even tho it opens in a new tab, GPAL is still working on the main tab, so switch to the new tab.
                //.WaitFor(2_000);

                browser
                   .WithSelector(Selectors.descriptionColumn)
                   .WithSelector(Selectors.sellerColumn)
                   .WithSelector(Selectors.feedbackColumn)
                   .WithSelector(() => { return href; })
                   .GetGrid(ref retGrid) // getgrid adds all rows into the referenced grid
                   ;
                browser
                   .CloseTab();
            }
            #endregion <Workflow 3>

            // end of workflow 3
            return retVal;
        }

        /// <summary>
        /// Our persistent selector handler.<br/>
        /// Only on the second search, eBay pops up an alert "Not finding the results you want?" that blocks clicking on the first item.<br/>
        /// We want to dismiss this before we process our workflow.
        /// see: http://gpal.sditx.com/api/GenerallyPositive.Browser.Browser.CallIfDelegate.html
        /// </summary>
        /// public delegate int CallIfDelegate(Browser browser, List<IGPALElement> foundElements, List<IGPALElement> matchedElements, Selector selector, bool matchedAll);
        public static CallIfStatus KillNag(IBrowser browser, List<IGPALElement> foundElements, List<IGPALElement> matchedElements, Selector selector, bool matchedAll)
        {
            foreach (GPALElement element in matchedElements)
                if (true == element.Displayed)
                {
                    element.Click();   // use GPALelement click which feeds into the browser session

                    // new generic selector finds both the annopying pop ups
                    // nag will only appear once per session, remove ourselves
                    selector.Remove();  // remove the persistent selector once we deal with it

                    // since we only have one selector in the persistent UOW, the handler will never be called again once it is found. we don't HAVE to remove it, but do anyway
                    // NOTE: we have two possible nags, so we might not want to remove it cause we also have two workflows with the search inputs which will get their own nags
                    browser.RemoveCallIfHandlerEverywhere(KillNag);
                }

            return CallIfStatus.Handled;
        }
    }
}

