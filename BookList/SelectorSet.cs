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


namespace BookList
{
    public class SelectorSet
    {
        

        public static Selector goodreadsSignupSelector = (Selector)GPAL.Selector.WithCSS("body > div:nth-child(3) > div > div > div.modal__close").WithSelectorName("Sign Up");

        public static Selector homepageDetectSelector = (Selector)GPAL.Selector.WithCSS("#newAccountBox").WithSelectorName("New Account");

        public static Selector bookStatsSelector = (Selector)GPAL.Selector.WithCSS("#bookMeta > a[class=\"gr-hyperlink\"]").WithSelectorName("Book Stats");
        public static Selector ratingsCountSelector = (Selector)GPAL.Selector
            .WithXPath(@"//span[contains(@class, 'minirating')]")
            .WithSelectorName("Ratings Count");

        public static Selector reviewsCountSelector = (Selector)GPAL.Selector
            .WithCSS("meta[itemprop=reviewCount]")
            .UseAttribute("content")
            .WithSelectorName("Reviews Count");

        public static Selector goodreadsMoreDetailsSelector = (Selector)GPAL.Selector.WithCSS("#bookDataBoxShow").WithSelectorName("More Details");

        public static Selector goodreadsTitleSelector = (Selector)GPAL.Selector.WithXPath(@"//a[contains(@class, 'bookTitle')]").UseAttribute("text").WithSelectorName("Book Title");
        public static Selector goodreadsAuthorsSelector = (Selector)GPAL.Selector
            .WithXPath(@"//a[contains(@class, 'authorName')]")
            .UseAttribute("text")
            .WithSelectorName("Author");

        // this selector changes often
        public static Selector bnSearchBarSelector = (Selector)GPAL.Selector
            .WithCSS(@"#search-header-desktop")
            .MatchPlaceholder(@"Search by Title, Author, Keyword, ISBN")    // cannot use .withpalceholder, there are hideen others, responsive design
            .WithHardware
            .WithSelectorName("Search Bar");

        // Inside an iframe.
        public static Selector bnSearchMenuSelector = (Selector)GPAL.Selector
            .WithCSS(@"button.header-nav__search")
            .WithHardware
            .WithSelectorName("Search Menu");

        public static Selector bnSearchButtonSelector = (Selector)GPAL.Selector
            .WithCSS(@"body > header > div.header__group > div > div > div.header-main__search > div > form > div > button")
            .WithHardware
            .WithSelectorName("Search Button");

        public static Selector bnSearchResultsLocator = (Selector)GPAL.Selector
            .WithXPath(@"//div[contains(@class, 'product-item-card__title-container')]")
            .WithHardware
            .WithSelectorName("Search Results");

        public static Selector bnIndividualBookLocator = (Selector)GPAL.Selector
            .WithCSS("#commerce-zone").WithSelectorName("Book Page Locator");

        //Persistents
        public static Selector goodreadsSignInPopup = (Selector)GPAL.Selector
            .WithCSS("div.modal__close > button")
            .CallIfFound(Workflows.CloseLoginPopup).WithSelectorName("Persistent: Login Popup");

        public static Selector bnCookiesPopup = (Selector)GPAL.Selector
            .WithCSS("#onetrust-close-btn-container > button")
            .CallIfFound(Workflows.CloseBNCookiesPopup).WithSelectorName("Persistent: BN Cookies Popup");


    }
}

