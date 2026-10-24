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
//using System.Windows.Forms;
using System;
// GP usings, since we are creating a browser workflow, we include GenerallyPositive.Browser
using GenerallyPositive;
using GenerallyPositive.Browser;
using static GenerallyPositive.Enums;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;


namespace BookList
{
    public class Workflows
    {
        static IGPALGrid<string> outputGrid;
        static IGPALGrid<string> currentSearchGrid;

        #region RandomBookWorkflow
        // One pass on a browser the form built: the Goodreads list, the Barnes and Noble lookups, and the csv. The form
        // closes the browser afterwards, also when the pass throws.
        public static void RandomBookWorkflow(IBrowser browser, BrowserType bt, AutomationEngine ae, string mode, bool asHeadless)
        {
            // New grids each pass, so a pass that failed partway does not hand its rows to the next.
            outputGrid = GPAL.Grid.ToGPALObject();
            currentSearchGrid = GPAL.Grid.ToGPALObject();

            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"Running workflow for Automation Engine [{ae}]");

            StartBookListWorkflow(browser, asHeadless);
            //browser
            //    .WithSelector(SelectorSet.homepageDetectSelector)
            //    .CallIfFound(StartingRandomBookWorkflow)
            //    .StartWorkflow();

            // one csv per browser, engine and mode, so a round does not overwrite the one before it
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

            GPALFile outputFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $@"booklist\{bt}\{ae}\{mode}_{timestamp}.BookList.csv");
            outputFile.WithColumnsEnclosedInQuotes(true);

            if (true == outputGrid.Any())
                browser
                    .WithGridToSave(outputGrid)
                    .WithHeader("Title")
                    .WithHeader("Author")
                    .WithHeader("Price")
                    .SaveTo(outputFile);

            outputGrid.Clear();
        }

        // Listopia's Best Books Ever: 100 books a page across 100 pages, so each attempt reads a page of its own
        static string goodreadsListUrl = "https://www.goodreads.com/list/show/1.Best_Books_Ever";

        static int rowIdx = 0;

        // Attempts before the pass gives up.
        const int MaxAttempts = 3;

        private static int StartBookListWorkflow(IBrowser browser, bool asHeadless)
        {
            int attempts = 0;

            while (outputGrid.Count() < 3 && MaxAttempts > attempts)
            {
                attempts++;

                // Each attempt rereads the list, so start from its first row again.
                rowIdx = 0;

                GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"Starting workflow attempt [{attempts}] of [{MaxAttempts}]. Current grid count [{outputGrid.Count()}]");

                // attempt 1 reads page 1, attempt 2 page 2, and so on, so a retry looks at books it has not seen
                string listPage = $"{goodreadsListUrl}?page={attempts}";

                // Get opens the page headless, GoTo in a window
                if (true == asHeadless)
                    browser.Get(listPage);
                else
                    browser.GoTo(listPage);

                IGPALGrid<string> tempGrid = null;
                browser
                    .WithSelector(SelectorSet.ratingsCountSelector)
                    //.WithSelector(SelectorSet.reviewsCountSelector)
                    .WithAllThatMatch(3)   // all matches
                    .WaitFor(3_000)
                    .GetGrid(ref tempGrid)
                    ;

                browser
                    .WithSelector(SelectorSet.goodreadsTitleSelector)
                    .WithSelector(SelectorSet.goodreadsAuthorsSelector)
                    .WithAllThatMatch(3)
                    .GetGrid(ref currentSearchGrid);

                foreach (List<string> ratingsInfos in tempGrid)
                    foreach (string ratingInfo in ratingsInfos)
                    {
                        var avgRatingPattern = @"([\d.]+)\s+avg rating";
                        var ratingsCountPattern = @"([\d,]+)\s+ratings";

                        double avgRating = 0;
                        int ratingsCount = 0;

                        var avgMatch = Regex.Match(ratingInfo, avgRatingPattern);
                        if (avgMatch.Success)
                        {
                            avgRating = double.Parse(avgMatch.Groups[1].Value);
                        }

                        var countMatch = Regex.Match(ratingInfo, ratingsCountPattern);
                        if (countMatch.Success)
                        {
                            ratingsCount = int.Parse(countMatch.Groups[1].Value.Replace(",", ""));
                        }

                        // Ensure inner list at index 0 is not null and has at least one element
                        if (ratingsCount > 15 && avgRating > 4)
                        {
                            if (rowIdx < currentSearchGrid?.Count() && 0 < currentSearchGrid[rowIdx]?.Count)
                            {
                                string bookTitle = currentSearchGrid[rowIdx][0];
                                browser.NewTab("https://www.barnesandnoble.com/");

                                // if the browser is too narrow we get a menu
                                browser
                                    .WithSelector(SelectorSet.bnSearchMenuSelector)
                                    .WaitFor(2_000)
                                    .LeftClick();

                                string search = $"{bookTitle} {currentSearchGrid[rowIdx][1]}";

                                // On OttoMagic the search button gets a stealth click, sent as real input.
                                if (true == browser.UseOttoMagic)
                                    browser
                                        .WithSelector(SelectorSet.bnSearchBarSelector)
                                        .FillInFrom(search)
                                        .StealthLeftClick(SelectorSet.bnSearchButtonSelector);
                                else
                                    browser
                                        .WithSelector(SelectorSet.bnSearchBarSelector)
                                        .FillInFrom(search)
                                        .LeftClick(SelectorSet.bnSearchButtonSelector);

                                if (0 < SelectorSet.bnSearchMenuSelector.ElementsFoundAndMatchedCount || 0 < SelectorSet.bnSearchBarSelector.ElementsFoundAndMatchedCount)
                                    browser
                                        .WaitFor(2_000)
                                        .WithSelector(SelectorSet.bnSearchResultsLocator)
                                        .WaitFor(5_000)
                                        .CallIfFound(SelectBNSearchResult)
                                        .WithAllThatMatch(3)
                                        .StartWorkflow();

                                //.WithSelector(SelectorSet.bnIndividualBookLocator)
                                //.CallIfFound(SaveBNBookInfo)
                                //.StartWorkflow()

                                // closes the Barnes and Noble tab opened above, and only that one
                                browser
                                    .CloseTab();
                            }
                            else
                                GPAL.PublishSimpleEvent(GPALEventType.CAUTION, "No data in currentSearchGrid[rowIdx]");
                        }
                        rowIdx++;
                    }
            }

            if (3 > outputGrid.Count())
                GPAL.PublishSimpleEvent(GPALEventType.CAUTION, $"The search returned no results after [{attempts}] attempt(s). [{outputGrid.Count()}] book(s) collected.");

            return 1;
        }


        private static CallIfStatus SelectBNSearchResult(IBrowser browser, List<IGPALElement> foundElements, List<IGPALElement> matchedElements, Selector selector, bool matchedAll)
        {
            // TODO: convert to elementassistant, elementassistant need togpalobject, how to find nested element using gpal syntax.
            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, "Starting SelectBNSearchResult workflow");

            //foundElements[0].Click();

            // matchedElements: the first three results WithAllThatMatch kept. foundElements: every result on the page.
            foreach (GPALElement element in matchedElements)
            {
                string pattern = @"\s*\(.*?\)";
                string bookTitle = Regex.Replace(currentSearchGrid[rowIdx][0], pattern, "").Replace("\n","").Trim();

                Selector titleSelector = GPAL.CssSelector("h1[class *= 'product__title']").WithSelectorName("Title").ToGPALObject();
                Selector authorSelector = GPAL.CssSelector("div[class *= 'product__contributor'] > span > a").UseAttribute("text").WithSelectorName("Author").ToGPALObject();
                Selector priceSelector = GPAL.CssSelector("div[class *= 'product-price-on-sale']").WithSelectorName("Price").ToGPALObject();

                //browser
                //    .WithSelector(titleSelector)
                //    .WithSelector(authorSelector)
                //    .WithAllThatMatch(-1)
                //    .WaitFor(3_000)
                //    .StartWorkflow();

                //if (0 < titleSelector.ElementsFoundAndMatchedCount && 0 < authorSelector.ElementsFoundAndMatchedCount)
                {
                    element.MiddleClick();


                    IGPALGrid<string> tmpGrid = null;
                    browser
                        .WaitFor(1500)
                        .NextTab
                        .WithSelector(titleSelector)
                        .WithSelector(authorSelector)
                        .WithSelector(priceSelector)
                        .WithAllThatMatch(1)
                        .GetGrid(ref tmpGrid);
                    browser
                        .CloseTab();

                    outputGrid.Add(tmpGrid);
                }
                //if (element.FindElement(By.CssSelector("div[class *= \"product-shelf-title\"] > a")).Text.Contains(currentSearchGrid[0][0])
                //    && element.FindElement(By.CssSelector("div[class *= \"product-shelf-author\"] > a")).Text.Contains(currentSearchGrid[0][1]))
                //{
                //    element.FindElement(By.CssSelector("div[class *= \"product-shelf-title\"] > a")).Click();
                //    break;
                //}
            }

            return CallIfStatus.Handled;
        }

        #endregion RandomBookWorkflow


        public static CallIfStatus CloseLoginPopup(IBrowser browser, List<IGPALElement> foundElements, List<IGPALElement> matchedElements, Selector selector, bool matchedAll)
        {
            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"Starting CloseLoginPopup workflow. Found [{foundElements.Count}] element(s).");

            foreach (GPALElement element in foundElements)
                if (true == element.Displayed)
                {
                    element.Click();

                    selector.Remove();
                    browser.RemoveCallIfHandlerEverywhere(CloseLoginPopup);
                    break;
                }

            return CallIfStatus.Handled;
        }


        public static CallIfStatus CloseBNCookiesPopup(IBrowser browser, List<IGPALElement> foundElements, List<IGPALElement> matchedElements, Selector selector, bool matchedAll)
        {
            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"Starting CloseBNCookiesPopup workflow. Found [{foundElements.Count}] element(s).");

            foreach (GPALElement element in foundElements)
                element.Click();
            selector.Remove();
            browser.RemoveCallIfHandlerEverywhere(CloseBNCookiesPopup);
            return CallIfStatus.Handled;
        }


        public static void ExceptionEventHandler(object sender, EventArgs e)
        {
            Debug.WriteLine(((GPAL.GPALEventArgs)e).Message);
            Debug.WriteLine(((GPAL.GPALEventArgs)e).ExceptionRaised?.Message);
        }

    }
}

