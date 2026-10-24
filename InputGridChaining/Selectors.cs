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
using GenerallyPositive.Browser;
using static GenerallyPositive.Enums;

namespace InputGridChaining
{
    // NOTE: for selectors, you can use implicit conversion and use strings to create the selector
    //      but assigning names or callifhandlers after that must be done in a method and cannot be static initializers.
    //      So, if we declared these variables, then had an init() method, we could use syntax like this in init()
    //          Select searchButton = "#gh-ac";
    //          searchButton.WithSelectorName("Search Button");
    public class Selectors
    {
        public Selector persistentSelector = (Selector)GPAL.Selector
            .WithSelectorName("Annoying Popups")
            .WithCSS(@"span.tourtip__mask > span > button")   // not sure i trust the xpath
            .CallIfFound(Workflows.KillNag)
            .WithSearchForSelector(false);  // we get false positives, do not search if not immediately found

        //public Selector findSimilarSelector = GPAL.Selector
        //    .WithSelectorName("Annopying popup 'Find Similar...'")
        //    .WithCSS(@"#s0-2-54-0-9-8-4-4-0-3-0-5[0]-1-0-1-1-1-2-9-5-1-overlay > span.tourtip__mask > span > button")
        //    .CallIfFound(Workflows.KillNag)
        //    .ToGPALObject();

        public Selector searchSelector = (Selector)GPAL.Selector
            .WithSelectorName("SearchInput")
            .WithCSS("#gh-ac")
            .WithXPath("//*[@id='gh-ac']");

        public Selector searchButton = (Selector)GPAL.Selector
            .WithSelectorName("SearchButton")
            .WithCSS("#gh-search-btn")
            .WithXPath("//*[@id='gh-search-btn']")
            .MatchText("Search");

        public Selector nextPageButton = GPAL.Selector
            .WithSelectorName("NextPageButton") // our friendly name for Information and Exception channel message - if not supplied: selector1, selector2, etc are assigned as they are added to the workflow
            .WithXPath(@"//a[contains(@class, 'pagination__next icon-link')]") // 1 matched element
//            .WithHardware
            .WithSimulateMouse(true)
            .ToGPALObject();    // full xpath to the element

        // the item page link
        public Selector linkColumn = GPAL.Selector
            // one link per real listing: /itm/ drops the promo tiles and 'Shop on eBay', normalize-space() drops the
            // image links, and the id length drops the placeholder cards, whose id is /itm/123456 rather than a real
            // 12 digit one. The concat appends a '?' so substring-before always has one to find
            .WithXPath(@"//a[contains(@class, 's-card__link') and contains(@href, '/itm/') and normalize-space() and string-length(substring-before(concat(substring-after(@href, '/itm/'), '?'), '?')) > 9]")
            .WithSelectorName("Link")
            .ToGPALObject();


        //public static Selector linkColumn = (Selector)GPAL.Selector
        //    .WithSelectorName("Link")
        //    //.UseAttribute("href")
        //    // .WithCSS("#srp-river-results > ul > li > div > div.s-item__info.clearfix > a")
        //    .WithXPath("html/body/div[5]/div[4]/div[3]/div[1]/div[3]/ul/li/div/div[2]/div[1]/a");

        // the item price, we only want items priced 12.12 in our list
        public static Selector priceColumn = (Selector)GPAL.Selector
            .WithSelectorName("Price")
            .WithCSS(@"#mainContent > div > div.vim.x-price-section.mar-t-20 > div > div > div.x-price-primary > span")
            .WithXPath(@"//span[contains(@class, 'ux-textspans')]") // 245 matched elements => use with .WithAllThatMatch(numToReturn), else first match
            // .MatchText("$3.87")
            ;

        // the item information we want to retrieve, the description, seller info and seller feedback
        public static Selector descriptionColumn = (Selector)GPAL.Selector
            .WithSelectorName("Description")
            .WithCSS(@"#mainContent > div > div.vim.x-item-title > h1 > span")
            .WithXPath(@"//span[contains(@class, 'ux-textspans ux-textspans--BOLD')]") // 16 matched elements => use with .WithAllThatMatch(numToReturn), else first match
                                                                           // .MatchTextRegex("^Find Me.*$")
            .ToGPALObject();

        public static Selector sellerColumn = (Selector)GPAL.Selector
            .WithSelectorName("Seller")
            .WithCSS(@"div.x-sellercard-atf__about-seller-item.x-sellercard-atf__about-seller-item--seller-name > a.ux-action")
            .UseAttribute("text")
            ;

        public Selector feedbackColumn = (Selector)GPAL.Selector
            .WithSelectorName("Feedback")
            .WithCSS(@"div.x-sellercard-atf__data-item-wrapper > div:nth-of-type(1) > button.ux-action.fake-link.fake-link--action > span.ux-textspans.ux-textspans--PSEUDOLINK")
            ;
    }
}

